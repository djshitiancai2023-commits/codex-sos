using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using CodexSOS.App.Services;
using CodexSOS.App.Testing;
using CodexSOS.Core;
using Microsoft.Win32;

namespace CodexSOS.App;

public enum OcrAttemptOutcome
{
    None,
    Success,
    NoText,
    Timeout,
    Unavailable,
    Failure
}

// 首屏唯一一次追问的固定规则：只看最终诊断是否仍无法判断，最多问一次。
public static class OneQuestionRules
{
    public const string NormalLabel = "画面正常，我只是想确认";
    public const string FrozenLabel = "窗口卡住、一直转圈或突然断开";
    public const string UnrecognizedLabel = "画面有报错，但这次没认出来";

    // Core 诊断证据中“未知报错”分支的固定标记词。
    public const string UnrecognizedErrorEvidenceMarker = "疑似报错";

    private const string QuestionText = "刚才更像是哪一种情况？选一个就行。";
    private const string NormalMeaning = "会当作一次正常确认收尾，不做任何修复操作";
    private const string FrozenMeaning = "会重点核对连接中断和任务恢复这两类固定线索";
    private const string UnrecognizedMeaning = "会按还没认出的报错继续排查，并保留你原来的一句话";
    private const string NoClueReason =
        "依据：这次检查没有发现明确的失败线索，先按“画面正常”确认一次即可，不会改动任何东西。";
    private const string UnreadableReason =
        "依据：这次没能从截图里读出可用的文字，所以先按“有报错但没认出来”继续。";
    private const string UnknownErrorReason =
        "依据：截图里读到了像报错的文字，但还没认出属于哪一类问题。";

    public static ClarifyingQuestion? Build(
        bool alreadyAsked,
        Diagnosis diagnosis,
        bool screenshotProvided,
        OcrAttemptOutcome ocrAttempt)
    {
        if (alreadyAsked) return null;
        if (diagnosis.Confidence != ConfidenceLevel.CannotDetermine) return null;

        var unreadable = screenshotProvided && ocrAttempt is
            OcrAttemptOutcome.NoText or OcrAttemptOutcome.Timeout or
            OcrAttemptOutcome.Unavailable or OcrAttemptOutcome.Failure;
        var unrecognizedError = diagnosis.Evidence.Any(e =>
            e.Contains(UnrecognizedErrorEvidenceMarker, StringComparison.Ordinal));
        var preferUnrecognized = unreadable || unrecognizedError;

        var reason = preferUnrecognized
            ? unreadable ? UnreadableReason : UnknownErrorReason
            : NoClueReason;

        return new ClarifyingQuestion(
            QuestionText,
            reason,
            [
                new(NormalLabel, NormalMeaning, !preferUnrecognized),
                new(FrozenLabel, FrozenMeaning, false),
                new(UnrecognizedLabel, UnrecognizedMeaning, preferUnrecognized)
            ]);
    }
}

public partial class MainWindow : Window
{
    private readonly HttpClient _httpClient;
    private readonly DiagnosticOrchestrator _orchestrator;
    private readonly DiagnosisEngine _diagnosisEngine;
    private readonly CodexWindowCapture _windowCapture = new();
    private readonly ILocalOcrService _ocr;
    private CancellationTokenSource? _activeRun;
    private BitmapSource? _screenshot;
    private DiagnosticReport? _report;
    private bool _clarifyingQuestionShown = false;
    private OcrAttemptOutcome _ocrAttempt = OcrAttemptOutcome.None;

    private readonly FixtureSession? _fixture;

    public MainWindow(FixtureSession? fixture = null)
    {
        InitializeComponent();
        _fixture = fixture;
        _httpClient = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        })
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        var redactor = fixture is null
            ? new PrivacyRedactor([Environment.UserName])
            : new PrivacyRedactor(fixture.KnownPrivateTerms);
        var parser = new DoctorJsonParser(redactor);
        _diagnosisEngine = new DiagnosisEngine();
        _orchestrator = fixture?.CreateOrchestrator(redactor) ?? new DiagnosticOrchestrator(
                new DoctorRunner(parser),
                new WindowsSystemCollector(),
                new WindowsFaultEventCollector(),
                new GitHubIssueSearchClient(_httpClient),
                new OpenAIStatusClient(_httpClient),
                redactor,
                new StableTermExtractor(redactor),
                _diagnosisEngine,
                new SimilarIssueMatcher(),
                new PublicReportBuilder(redactor));
        _ocr = fixture is null ? LocalOcrFactory.Create() : new FixtureOcrService(fixture.MockOcrText);
        Closed += MainWindow_Closed;
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_fixture is null) return;
        Title = $"Codex SOS · {_fixture.Id}（全部虚构）";
        DescriptionBox.Text = _fixture.Description;
        if (_fixture.ScreenshotProvided)
        {
            SetScreenshot(_fixture.CreateSyntheticScreenshot(),
                $"已载入 {_fixture.Id} 虚构截图；不含真实账号、任务或项目。 ");
        }

        StartError.Text = $"虚构验收场景：{_fixture.Id} · {_fixture.Title}";
        StartError.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(23, 107, 135));
        StartError.Visibility = Visibility.Visible;
    }

    private void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        var capture = _windowCapture.Capture(out var message);
        if (capture is not null)
        {
            SetScreenshot(capture, message);
        }
        else
        {
            ScreenshotStatus.Text = message;
        }
    }

    private void PasteButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!Clipboard.ContainsImage())
            {
                ScreenshotStatus.Text = "剪贴板里没有图片。你可以先截图，再点一次“粘贴截图”。";
                return;
            }

            var image = Clipboard.GetImage();
            if (image is null)
            {
                ScreenshotStatus.Text = "这张图片暂时无法读取。你也可以选择已有截图。";
                return;
            }

            image.Freeze();
            SetScreenshot(image, "已粘贴截图。图片只在本机处理，不会放进公开材料。");
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            ScreenshotStatus.Text = "剪贴板暂时正忙。请再点一次，或选择已有截图。";
        }
    }

    private void ChooseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择 Codex 报错截图",
            Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|所有文件|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(dialog.FileName, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            SetScreenshot(image, "已选择截图。图片只在本机处理，不会放进公开材料。");
        }
        catch (Exception ex) when (ex is NotSupportedException or IOException or UriFormatException)
        {
            ScreenshotStatus.Text = "这张图片暂时无法读取。请换一张 PNG、JPG 或 BMP 图片。";
        }
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        var description = DescriptionBox.Text.Trim();
        if (_screenshot is null && string.IsNullOrWhiteSpace(description))
        {
            StartError.Text = "请粘贴一张截图，或随便说一句发生了什么。二选一就行。";
            StartError.Visibility = Visibility.Visible;
            return;
        }

        StartError.Visibility = Visibility.Collapsed;
        ShowPanel(ProgressPanel);
        // Let WPF paint the progress page before any local collection/OCR work
        // begins.  The user should always see that the click was accepted.
        await Dispatcher.InvokeAsync(
            () =>
            {
                MainScroll.UpdateLayout();
                MainScroll.ScrollToTop();
            },
            System.Windows.Threading.DispatcherPriority.Render);
        _ocrAttempt = OcrAttemptOutcome.None;
        _activeRun?.Cancel();
        _activeRun?.Dispose();
        _activeRun = new CancellationTokenSource();
        var token = _activeRun.Token;

        try
        {
            var ocrText = string.Empty;
            if (_screenshot is not null)
            {
                ProgressText.Text = "正在本机识别截图里的错误文字…";
                try
                {
                    if (_ocr.IsAvailable)
                    {
                        ocrText = await _ocr.ReadAsync(_screenshot, token);
                        if (string.IsNullOrWhiteSpace(ocrText)) ocrText = string.Empty;
                        _ocrAttempt = ocrText.Length > 0
                            ? OcrAttemptOutcome.Success
                            : OcrAttemptOutcome.NoText;
                        if (!token.IsCancellationRequested && ocrText.Length == 0)
                        {
                            ProgressText.Text = "这次没读到截图文字，其他检查仍会继续。";
                        }
                    }
                    else
                    {
                        _ocrAttempt = OcrAttemptOutcome.Unavailable;
                        ProgressText.Text = "这次没读到截图文字，其他检查仍会继续。";
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (TimeoutException)
                {
                    ocrText = string.Empty;
                    _ocrAttempt = OcrAttemptOutcome.Timeout;
                    ProgressText.Text = "这次没读到截图文字，其他检查仍会继续。";
                }
                catch (Exception)
                {
                    ocrText = string.Empty;
                    _ocrAttempt = OcrAttemptOutcome.Failure;
                    ProgressText.Text = "这次没读到截图文字，其他检查仍会继续。";
                }
            }

            var evidence = new UserEvidence(description, ocrText, _screenshot is not null, DateTimeOffset.UtcNow);
            var progress = new Progress<string>(text => ProgressText.Text = text);
            _report = await _orchestrator.RunAsync(evidence, progress, token);
            ShowResult(_report);
        }
        catch (OperationCanceledException)
        {
            // Closing or starting a new run intentionally cancels the old work.
        }
        catch (Exception)
        {
            StartError.Text = "这次检查没有完成。你的截图没有上传，也没有改动 Codex 数据。请再试一次。";
            StartError.Visibility = Visibility.Visible;
            ShowPanel(StartPanel);
        }
    }

    private void ShowResult(DiagnosticReport report)
    {
        DiagnosisText.Text = report.Diagnosis.PlainSummary;
        SimilarText.Text = report.SimilarIssues.PlainSummary;
        NextStepText.Text = report.Diagnosis.SafeNextStep;
        var screenshotNote = report.PublicEvidence.ScreenshotProvided
            ? _ocrAttempt switch
            {
                OcrAttemptOutcome.Success =>
                    "截图已在本机读出文字；原图没有保存，也没有上传。",
                OcrAttemptOutcome.NoText =>
                    "这次没从截图里读出文字；原图没有保存，也没有上传，结论主要来自其他检查。",
                OcrAttemptOutcome.Timeout =>
                    "截图识字等得太久，这次先跳过；原图没有保存，也没有上传，结论主要来自其他检查。",
                OcrAttemptOutcome.Unavailable =>
                    "这台电脑上的本地识字组件暂时不可用；原图没有保存，也没有上传，结论主要来自其他检查。",
                OcrAttemptOutcome.Failure =>
                    "截图识字这次没能完成；原图没有保存，也没有上传，结论主要来自其他检查。",
                _ =>
                    "原截图没有保存或上传；这次结论主要来自其他检查。"
            }
            : "你只提供了一句话，其余环境信息已自动检查。";
        ResultNote.Text = $"{report.Doctor.PublicSummary} {screenshotNote}";
        RenderSimilarIssues(report.SimilarIssues);
        var asking = TryShowClarifyingQuestion(report);
        ResultTitle.Text = asking ? "还差一个选择" : "检查好了";
        ShowPanel(ResultPanel);
        MainScroll.ScrollToTop();
        // 动态按钮在下一轮布局后才完成测量，这里再回到顶部一次，确保结果页从最上方开始。
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                MainScroll.UpdateLayout();
                MainScroll.ScrollToVerticalOffset(0);
            }),
            System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private bool TryShowClarifyingQuestion(DiagnosticReport report)
    {
        try
        {
            ClarifyingChoicesPanel.Children.Clear();
            var question = OneQuestionRules.Build(
                _clarifyingQuestionShown,
                report.Diagnosis,
                _screenshot is not null,
                _ocrAttempt);
            if (question is null)
            {
                ClarifyingPanel.Visibility = Visibility.Collapsed;
                return false;
            }

            ClarifyingQuestionText.Text = question.Question;
            ClarifyingReasonText.Text = question.RecommendationReason;
            foreach (var choice in question.Choices)
            {
                var button = new Button
                {
                    Content = choice.Recommended ? $"{choice.Label}（推荐）" : choice.Label,
                    Style = TryFindResource("SoftButton") as Style,
                    Tag = choice,
                    Padding = new Thickness(16, 8, 16, 8),
                    Margin = new Thickness(0, 0, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                button.Click += ClarifyingChoiceButton_Click;
                ClarifyingChoicesPanel.Children.Add(button);
            }

            ClarifyingPanel.Visibility = Visibility.Visible;
            return true;
        }
        catch (Exception)
        {
            ClarifyingChoicesPanel.Children.Clear();
            ClarifyingPanel.Visibility = Visibility.Collapsed;
            return false;
        }
    }

    private void ClarifyingChoiceButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button { Tag: ClarifyingChoice choice }) return;
            _clarifyingQuestionShown = true;
            var original = DescriptionBox.Text.TrimEnd();
            var room = Math.Max(0, DescriptionBox.MaxLength - original.Length - 1);
            if (room > 0)
            {
                var addition = choice.Label[..Math.Min(room, choice.Label.Length)];
                DescriptionBox.Text = string.IsNullOrEmpty(original)
                    ? addition
                    : $"{original}\n{addition}";
                DescriptionBox.CaretIndex = DescriptionBox.Text.Length;
            }
            ClarifyingChoicesPanel.Children.Clear();
            ClarifyingPanel.Visibility = Visibility.Collapsed;
            StartError.Visibility = Visibility.Collapsed;
            StartButton_Click(StartButton, new RoutedEventArgs());
        }
        catch (Exception)
        {
        }
    }

    private const string IssueUrlPrefix = "https://github.com/openai/codex/issues/";
    private const string FallbackUrlPrefix = "https://github.com/openai/codex/issues?q=";

    private void RenderSimilarIssues(SimilarIssueSummary similar)
    {
        SimilarMatchesPanel.Children.Clear();
        foreach (var match in similar.Matches.Take(5))
        {
            SimilarMatchesPanel.Children.Add(CreateSimilarIssueButton(match));
        }
        SimilarMatchesPanel.Visibility = SimilarMatchesPanel.Children.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        var fallbackUrl = similar.BrowserFallbackUrl;
        var fallbackAllowed = similar.SearchState == IssueSearchState.Unavailable
            && !string.IsNullOrEmpty(fallbackUrl)
            && fallbackUrl.StartsWith(FallbackUrlPrefix, StringComparison.Ordinal);
        BrowserFallbackButton.Tag = fallbackUrl;
        BrowserFallbackButton.Visibility = fallbackAllowed ? Visibility.Visible : Visibility.Collapsed;
    }

    private Button CreateSimilarIssueButton(MatchedIssue match)
    {
        var title = new TextBlock
        {
            Text = $"#{match.Issue.Number}  {match.Issue.Title}",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        var reasons = match.Reasons.Count == 0 ? string.Empty : $" · 依据：{string.Join("、", match.Reasons)}";
        var isOfficialUrl = match.Issue.HtmlUrl.StartsWith(IssueUrlPrefix, StringComparison.Ordinal);
        var demoNote = isOfficialUrl ? string.Empty : " · 演示链接，不会打开";
        var detail = new TextBlock
        {
            Text = $"{TierText(match.Tier)}{reasons}{demoNote}",
            FontSize = 12,
            Foreground = FindResource("MutedBrush") as System.Windows.Media.Brush
                ?? System.Windows.Media.Brushes.Gray,
            TextWrapping = TextWrapping.Wrap
        };
        var button = new Button
        {
            Content = new StackPanel { Children = { title, detail } },
            Tag = match.Issue.HtmlUrl,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE6, 0xF1, 0xF4)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        button.Click += SimilarIssueButton_Click;
        return button;
    }

    private static string TierText(IssueSimilarityTier tier) => tier switch
    {
        IssueSimilarityTier.High => "高度相似",
        IssueSimilarityTier.Possible => "可能相关",
        _ => "仅供参考"
    };

    private void OpenSafeExternalUrl(string? url, string allowedPrefix)
    {
        try
        {
            if (url is null || !url.StartsWith(allowedPrefix, StringComparison.Ordinal)) return;
            using var _ = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
            ResultNote.Text = "暂时打不开这个链接。请稍后再试，或自己在浏览器打开 GitHub 的 Codex issues 页面。";
        }
    }

    private void ReviewButton_Click(object sender, RoutedEventArgs e)
    {
        if (_report is null) return;
        ReportPreview.Text = _report.PublicReportMarkdown;
        var count = _report.PrivacyFindings.Sum(finding => finding.Count);
        PrivacyCountText.Text = count == 0
            ? "自动检查暂未发现需要遮住的内容。"
            : $"自动检查已经遮住 {count} 处可能的私人信息。";
        ShowPanel(ReviewPanel);
        MainScroll.ScrollToTop();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_report is null) return;
        var dialog = new SaveFileDialog
        {
            Title = "保存 Codex SOS 完整材料",
            Filter = "Markdown 文档|*.md",
            FileName = $"codex-sos-{_report.CreatedAt:yyyyMMdd-HHmm}.md",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            File.WriteAllText(dialog.FileName, _report.PublicReportMarkdown, new System.Text.UTF8Encoding(false));
            var directory = Path.GetDirectoryName(dialog.FileName) ?? Environment.CurrentDirectory;
            var reviewPath = Path.Combine(directory,
                Path.GetFileNameWithoutExtension(dialog.FileName) + "-隐私复核.md");
            File.WriteAllText(reviewPath, _report.PrivacyReviewMarkdown, new System.Text.UTF8Encoding(false));
            MessageBox.Show(this,
                "已经保存两份文件：公开材料和隐私复核说明。原截图没有保存，也不会自动发布。",
                "保存完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            MessageBox.Show(this, "这个位置暂时无法保存。请选择另一个文件夹。", "没有保存",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CopyResultButton_Click(object sender, RoutedEventArgs e)
    {
        if (_report is null) return;
        var text = $"1. {_report.Diagnosis.PlainSummary}\n\n" +
                   $"2. {_report.SimilarIssues.PlainSummary}\n\n" +
                   $"3. {_report.Diagnosis.SafeNextStep}\n\n" +
                   "4. 完整材料已经准备好，可在 Codex SOS 中查看并保存。";
        try
        {
            Clipboard.SetText(text);
            ResultNote.Text = "四条结果已复制。截图、日志和完整材料没有复制。";
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            ResultNote.Text = "剪贴板暂时正忙，请再点一次。";
        }
    }

    private void SimilarIssueButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string url }) OpenSafeExternalUrl(url, IssueUrlPrefix);
    }

    private void BrowserFallbackButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string url }) OpenSafeExternalUrl(url, FallbackUrlPrefix);
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _activeRun?.Cancel();
        _report = null;
        _screenshot = null;
        _clarifyingQuestionShown = false;
        _ocrAttempt = OcrAttemptOutcome.None;
        ScreenshotPreview.Source = null;
        ScreenshotPreview.Visibility = Visibility.Collapsed;
        ScreenshotEmptyText.Visibility = Visibility.Visible;
        ScreenshotStatus.Text = "截图默认不保存，也不会上传。";
        DescriptionBox.Text = string.Empty;
        StartError.Visibility = Visibility.Collapsed;
        ShowPanel(StartPanel);
        MainScroll.ScrollToTop();
    }

    private void BackToResultButton_Click(object sender, RoutedEventArgs e) => ShowPanel(ResultPanel);

    private void DescriptionBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        DescriptionHint.Visibility = string.IsNullOrEmpty(DescriptionBox.Text) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetScreenshot(BitmapSource image, string message)
    {
        _screenshot = image;
        ScreenshotPreview.Source = image;
        ScreenshotPreview.Visibility = Visibility.Visible;
        ScreenshotEmptyText.Visibility = Visibility.Collapsed;
        ScreenshotStatus.Text = message;
        StartError.Visibility = Visibility.Collapsed;
    }

    private void ShowPanel(FrameworkElement panel)
    {
        StartPanel.Visibility = panel == StartPanel ? Visibility.Visible : Visibility.Collapsed;
        ProgressPanel.Visibility = panel == ProgressPanel ? Visibility.Visible : Visibility.Collapsed;
        ResultPanel.Visibility = panel == ResultPanel ? Visibility.Visible : Visibility.Collapsed;
        ReviewPanel.Visibility = panel == ReviewPanel ? Visibility.Visible : Visibility.Collapsed;
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _activeRun?.Cancel();
        _activeRun?.Dispose();
        _httpClient.Dispose();
    }
}

internal static class LocalOcrFactory
{
    public static ILocalOcrService Create()
    {
        try
        {
            return new TesserNetOcrService();
        }
        catch
        {
            return new UnavailableOcrService();
        }
    }
}
