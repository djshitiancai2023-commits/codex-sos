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
        if (!diagnosis.OfficialFeedbackAppropriate) return null;
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
    private UiLanguage _language = UiLanguage.SimplifiedChinese;

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
        LanguageSelector.SelectedIndex = 0;
        ApplyLanguage();
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
            SetScreenshot(capture, UiText.CaptureMessage(_language, message));
        }
        else
        {
            ScreenshotStatus.Text = UiText.CaptureMessage(_language, message);
        }
    }

    private void PasteButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!Clipboard.ContainsImage())
            {
                ScreenshotStatus.Text = L("剪贴板里没有图片。你可以先截图，再点一次“粘贴截图”。", "剪貼簿裡沒有圖片。你可以先截圖，再點一次「貼上截圖」。", "There is no image on the clipboard. Take a screenshot, then choose “Paste screenshot” again.");
                return;
            }

            var image = Clipboard.GetImage();
            if (image is null)
            {
                ScreenshotStatus.Text = L("这张图片暂时无法读取。你也可以选择已有截图。", "暫時無法讀取這張圖片。你也可以選擇已有截圖。", "This image could not be read. You can choose an existing screenshot instead.");
                return;
            }

            image.Freeze();
            SetScreenshot(image, L("已粘贴截图。图片只在本机处理，不会放进公开材料。", "已貼上截圖。圖片只在本機處理，不會放進公開資料。", "Screenshot pasted. The image stays on this computer and is not included in the public report."));
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            ScreenshotStatus.Text = L("剪贴板暂时正忙。请再点一次，或选择已有截图。", "剪貼簿暫時忙碌。請再點一次，或選擇已有截圖。", "The clipboard is busy. Try again or choose an existing screenshot.");
        }
    }

    private void ChooseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = L("选择 Codex 报错截图", "選擇 Codex 錯誤截圖", "Choose a Codex error screenshot"),
            Filter = L("图片|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|所有文件|*.*", "圖片|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|所有檔案|*.*", "Images|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|All files|*.*"),
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
            SetScreenshot(image, L("已选择截图。图片只在本机处理，不会放进公开材料。", "已選擇截圖。圖片只在本機處理，不會放進公開資料。", "Screenshot selected. The image stays on this computer and is not included in the public report."));
        }
        catch (Exception ex) when (ex is NotSupportedException or IOException or UriFormatException)
        {
            ScreenshotStatus.Text = L("这张图片暂时无法读取。请换一张 PNG、JPG 或 BMP 图片。", "暫時無法讀取這張圖片。請改用 PNG、JPG 或 BMP 圖片。", "This image could not be read. Try a PNG, JPG, or BMP image.");
        }
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        var description = DescriptionBox.Text.Trim();
        if (_screenshot is null && string.IsNullOrWhiteSpace(description))
        {
            StartError.Text = L("请粘贴一张截图，或随便说一句发生了什么。二选一就行。", "請貼上一張截圖，或簡單說一句發生了什麼。二選一即可。", "Paste a screenshot or briefly say what happened. Either one is enough.");
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
                ProgressText.Text = L("正在本机识别截图里的错误文字…", "正在本機辨識截圖中的錯誤文字…", "Reading error text from the screenshot on this computer…");
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
                            ProgressText.Text = L("这次没读到截图文字，其他检查仍会继续。", "這次沒有讀到截圖文字，其他檢查仍會繼續。", "No text was read from the screenshot. The other checks will continue.");
                        }
                    }
                    else
                    {
                        _ocrAttempt = OcrAttemptOutcome.Unavailable;
                        ProgressText.Text = L("这次没读到截图文字，其他检查仍会继续。", "這次沒有讀到截圖文字，其他檢查仍會繼續。", "No text was read from the screenshot. The other checks will continue.");
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
                    ProgressText.Text = L("这次没读到截图文字，其他检查仍会继续。", "這次沒有讀到截圖文字，其他檢查仍會繼續。", "No text was read from the screenshot. The other checks will continue.");
                }
                catch (Exception)
                {
                    ocrText = string.Empty;
                    _ocrAttempt = OcrAttemptOutcome.Failure;
                    ProgressText.Text = L("这次没读到截图文字，其他检查仍会继续。", "這次沒有讀到截圖文字，其他檢查仍會繼續。", "No text was read from the screenshot. The other checks will continue.");
                }
            }

            var evidence = new UserEvidence(description, ocrText, _screenshot is not null, DateTimeOffset.UtcNow);
            var progress = new Progress<string>(text => ProgressText.Text = UiText.Progress(_language, text));
            _report = await _orchestrator.RunAsync(evidence, progress, token);
            ShowResult(_report);
        }
        catch (OperationCanceledException)
        {
            // Closing or starting a new run intentionally cancels the old work.
        }
        catch (Exception)
        {
            StartError.Text = L("这次检查没有完成。你的截图没有上传，也没有改动 Codex 数据。请再试一次。", "這次檢查沒有完成。你的截圖沒有上傳，也沒有改動 Codex 資料。請再試一次。", "The check did not complete. Your screenshot was not uploaded and no Codex data was changed. Please try again.");
            StartError.Visibility = Visibility.Visible;
            ShowPanel(StartPanel);
        }
    }

    private void ShowResult(DiagnosticReport report)
    {
        DiagnosisText.Text = UiText.DiagnosisSummary(_language, report.Diagnosis);
        SimilarText.Text = UiText.SimilarSummary(_language, report.SimilarIssues);
        NextStepText.Text = UiText.SafeNextStep(_language, report.Diagnosis);
        ResultMoreAnswer.Text = report.Diagnosis.OfficialFeedbackAppropriate
            ? UiText.Get(_language, "ResultMoreAnswer")
            : L(
                "这更像其他程序的问题。材料可以留在本机，但先别交给 Codex 官方。",
                "這更像其他程式的問題。資料可以留在本機，但先不要交給 Codex 官方。",
                "This looks more like another program's problem. You can save the report locally, but do not send it to the Codex maintainers yet.");
        var screenshotNote = report.PublicEvidence.ScreenshotProvided
            ? _ocrAttempt switch
            {
                OcrAttemptOutcome.Success =>
                    L("截图已在本机读出文字；原图没有保存，也没有上传。", "截圖文字已在本機讀出；原圖沒有儲存，也沒有上傳。", "Text was read from the screenshot locally; the original image was not saved or uploaded."),
                OcrAttemptOutcome.NoText =>
                    L("这次没从截图里读出文字；原图没有保存，也没有上传，结论主要来自其他检查。", "這次沒有從截圖讀出文字；原圖沒有儲存或上傳，結論主要來自其他檢查。", "No text was read from the screenshot. The original image was not saved or uploaded; the result mainly uses the other checks."),
                OcrAttemptOutcome.Timeout =>
                    L("截图识字等得太久，这次先跳过；原图没有保存，也没有上传，结论主要来自其他检查。", "截圖辨識等候過久，這次先跳過；原圖沒有儲存或上傳，結論主要來自其他檢查。", "Screenshot text recognition took too long and was skipped. The original image was not saved or uploaded; the result mainly uses the other checks."),
                OcrAttemptOutcome.Unavailable =>
                    L("这台电脑上的本地识字组件暂时不可用；原图没有保存，也没有上传，结论主要来自其他检查。", "這台電腦上的本機文字辨識暫時不可用；原圖沒有儲存或上傳，結論主要來自其他檢查。", "Local text recognition is unavailable on this computer. The original image was not saved or uploaded; the result mainly uses the other checks."),
                OcrAttemptOutcome.Failure =>
                    L("截图识字这次没能完成；原图没有保存，也没有上传，结论主要来自其他检查。", "這次截圖文字辨識未能完成；原圖沒有儲存或上傳，結論主要來自其他檢查。", "Screenshot text recognition did not complete. The original image was not saved or uploaded; the result mainly uses the other checks."),
                _ =>
                    L("原截图没有保存或上传；这次结论主要来自其他检查。", "原始截圖沒有儲存或上傳；這次結論主要來自其他檢查。", "The original screenshot was not saved or uploaded; this result mainly uses the other checks.")
            }
            : L("你只提供了一句话，其余环境信息已自动检查。", "你只提供了一句描述，其餘環境資訊已自動檢查。", "You provided one short description; the remaining environment information was checked automatically.");
        ResultNote.Text = $"{UiText.DoctorSummary(_language, report.Doctor)} {screenshotNote}";
        RenderSimilarIssues(report.SimilarIssues);
        var asking = TryShowClarifyingQuestion(report);
        ResultTitle.Text = asking
            ? L("还差一个选择", "還差一個選擇", "One quick choice needed")
            : L("检查好了", "檢查好了", "Check complete");
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

            ClarifyingQuestionText.Text = L(question.Question, "剛才更像是哪一種情況？選一個就好。", "Which best describes what happened? Choose one.");
            ClarifyingReasonText.Text = LocalizeClarifyingReason(question.RecommendationReason);
            foreach (var choice in question.Choices)
            {
                var label = LocalizeClarifyingLabel(choice.Label);
                var button = new Button
                {
                    Content = choice.Recommended
                        ? $"{label}{L("（推荐）", "（推薦）", " (Recommended)")}"
                        : label,
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
    private const string OfficialFeedbackUrl = "https://github.com/openai/codex/issues/new?template=1-codex-app.yml";

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
        var reasons = match.Reasons.Count == 0
            ? string.Empty
            : $" · {L("依据", "依據", "Reason")}: {string.Join(L("、", "、", ", "), match.Reasons.Select(reason => UiText.MatchReason(_language, reason)))}";
        var isOfficialUrl = match.Issue.HtmlUrl.StartsWith(IssueUrlPrefix, StringComparison.Ordinal);
        var demoNote = isOfficialUrl
            ? string.Empty
            : L(" · 演示链接，不会打开", " · 示範連結，不會開啟", " · Demo link; it will not open");
        var detail = new TextBlock
        {
            Text = $"{UiText.Tier(_language, match.Tier)}{reasons}{demoNote}",
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

    private bool OpenSafeExternalUrl(string? url, string allowedPrefix)
    {
        try
        {
            if (url is null) return false;
            var allowed = string.Equals(allowedPrefix, OfficialFeedbackUrl, StringComparison.Ordinal)
                ? string.Equals(url, OfficialFeedbackUrl, StringComparison.Ordinal)
                : url.StartsWith(allowedPrefix, StringComparison.Ordinal);
            if (!allowed) return false;
            using var _ = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return true;
        }
        catch (Exception)
        {
            ResultNote.Text = L("暂时打不开这个链接。请稍后再试，或自己在浏览器打开 GitHub 的 Codex issues 页面。", "暫時無法開啟這個連結。請稍後再試，或在瀏覽器中開啟 GitHub 的 Codex issues 頁面。", "This link could not be opened. Try again later or open the Codex issues page on GitHub in your browser.");
            return false;
        }
    }

    private void ReviewButton_Click(object sender, RoutedEventArgs e)
    {
        if (_report is null) return;
        ReportPreview.Text = UiText.BuildPublicReport(_report, _language);
        var count = _report.PrivacyFindings.Sum(finding => finding.Count);
        PrivacyCountText.Text = count == 0
            ? L("自动检查暂未发现需要遮住的内容。", "自動檢查暫未發現需要遮蔽的內容。", "The automatic check found nothing that needed redaction.")
            : L($"自动检查已经遮住 {count} 处可能的私人信息。", $"自動檢查已遮蔽 {count} 處可能的私人資訊。", $"The automatic check redacted {count} possible piece(s) of private information.");
        OfficialFeedbackHint.Text = _report.Diagnosis.OfficialFeedbackAppropriate
            ? UiText.Get(_language, "OfficialFeedbackHint")
            : L(
                "这次更像其他程序的问题，所以不会提供 Codex 官方反馈入口。你仍可把材料保存在本机。",
                "這次更像其他程式的問題，因此不會提供 Codex 官方回報入口。你仍可把資料儲存在本機。",
                "This appears to concern another program, so the Codex bug-report action is unavailable. You can still save the report locally.");
        OfficialFeedbackButton.Visibility = _report.Diagnosis.OfficialFeedbackAppropriate
            ? Visibility.Visible
            : Visibility.Collapsed;
        FeedbackStatusText.Text = string.Empty;
        ShowPanel(ReviewPanel);
        MainScroll.ScrollToTop();
    }

    private void OfficialFeedbackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_report is null) return;
        if (!_report.Diagnosis.OfficialFeedbackAppropriate)
        {
            FeedbackStatusText.Text = L(
                "这次更像其他程序的问题，没有打开 Codex 官方反馈页。",
                "這次更像其他程式的問題，沒有開啟 Codex 官方回報頁。",
                "This appears to concern another program, so the Codex bug form was not opened.");
            return;
        }

        try
        {
            var draft = new OfficialFeedbackBuilder(new PrivacyRedactor()).Build(_report);
            Clipboard.SetText(draft);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            FeedbackStatusText.Text = L(
                "剪贴板暂时正忙，材料没有复制，也没有打开网页。请再点一次。",
                "剪貼簿暫時忙碌，資料沒有複製，也沒有開啟網頁。請再點一次。",
                "The clipboard is busy. Nothing was copied and no page was opened. Please try again.");
            return;
        }

        var opened = OpenSafeExternalUrl(OfficialFeedbackUrl, OfficialFeedbackUrl);
        FeedbackStatusText.Text = opened
            ? L(
                "已复制官方需要的材料并打开反馈页。请按页面栏目粘贴、快速检查后再提交；SOS 不会代你发布。账号套餐需要你自己选择。",
                "已複製官方需要的資料並開啟回報頁。請依頁面欄位貼上、快速檢查後再提交；SOS 不會代你發佈。帳號方案需要你自己選擇。",
                "The official-format draft was copied and the bug form opened. Paste it into the matching fields, review it, and submit only when ready. SOS never submits for you; select your subscription yourself.")
            : L(
                "材料已经复制，但网页暂时打不开。稍后打开 openai/codex 的 Codex App Bug 页面再粘贴即可。",
                "資料已複製，但網頁暫時無法開啟。稍後開啟 openai/codex 的 Codex App Bug 頁面再貼上即可。",
                "The draft was copied, but the page could not be opened. Later, open the Codex App Bug form in openai/codex and paste the draft.");
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_report is null) return;
        var dialog = new SaveFileDialog
        {
            Title = L("保存 Codex SOS 完整材料", "儲存 Codex SOS 完整資料", "Save the complete Codex SOS report"),
            Filter = L("Markdown 文档|*.md", "Markdown 文件|*.md", "Markdown document|*.md"),
            FileName = $"codex-sos-{_report.CreatedAt:yyyyMMdd-HHmm}.md",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            File.WriteAllText(dialog.FileName, UiText.BuildPublicReport(_report, _language), new System.Text.UTF8Encoding(false));
            var directory = Path.GetDirectoryName(dialog.FileName) ?? Environment.CurrentDirectory;
            var reviewPath = Path.Combine(directory,
                Path.GetFileNameWithoutExtension(dialog.FileName) + L("-隐私复核.md", "-隱私複核.md", "-privacy-review.md"));
            File.WriteAllText(reviewPath, UiText.BuildPrivacyReview(_report, _language), new System.Text.UTF8Encoding(false));
            MessageBox.Show(this,
                L("已经保存两份文件：公开材料和隐私复核说明。原截图没有保存，也不会自动发布。", "已儲存兩份檔案：公開資料和隱私複核說明。原始截圖沒有儲存，也不會自動發佈。", "Two files were saved: the public report and the privacy review. The original screenshot was not saved, and nothing is published automatically."),
                L("保存完成", "儲存完成", "Saved"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            MessageBox.Show(this,
                L("这个位置暂时无法保存。请选择另一个文件夹。", "暫時無法儲存到這個位置。請選擇其他資料夾。", "This location could not be used. Choose another folder."),
                L("没有保存", "未儲存", "Not saved"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CopyResultButton_Click(object sender, RoutedEventArgs e)
    {
        if (_report is null) return;
        var fourthResult = _report.Diagnosis.OfficialFeedbackAppropriate
            ? L("完整材料已经准备好，可在 Codex SOS 中查看并保存。", "完整資料已經準備好，可在 Codex SOS 中查看並儲存。", "The complete report is ready to review and save in Codex SOS.")
            : L("这更像其他程序的问题；材料可以保存在本机，但先别交给 Codex 官方。", "這更像其他程式的問題；資料可以儲存在本機，但先不要交給 Codex 官方。", "This looks more like another program's problem; save the report locally, but do not send it to the Codex maintainers yet.");
        var text = $"1. {UiText.DiagnosisSummary(_language, _report.Diagnosis)}\n\n" +
                   $"2. {UiText.SimilarSummary(_language, _report.SimilarIssues)}\n\n" +
                   $"3. {UiText.SafeNextStep(_language, _report.Diagnosis)}\n\n" +
                   $"4. {fourthResult}";
        try
        {
            Clipboard.SetText(text);
            ResultNote.Text = L("四条结果已复制。截图、日志和完整材料没有复制。", "四項結果已複製。截圖、記錄和完整資料沒有複製。", "The four results were copied. The screenshot, logs, and full report were not copied.");
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            ResultNote.Text = L("剪贴板暂时正忙，请再点一次。", "剪貼簿暫時忙碌，請再點一次。", "The clipboard is busy. Please try again.");
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
        ScreenshotStatus.Text = L("截图默认不保存，也不会上传。", "截圖預設不儲存，也不會上傳。", "Screenshots are not saved or uploaded by default.");
        DescriptionBox.Text = string.Empty;
        StartError.Visibility = Visibility.Collapsed;
        ShowPanel(StartPanel);
        MainScroll.ScrollToTop();
    }

    private void BackToResultButton_Click(object sender, RoutedEventArgs e) => ShowPanel(ResultPanel);

    private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageSelector is null || LanguageSelector.SelectedIndex < 0) return;
        _language = LanguageSelector.SelectedIndex switch
        {
            1 => UiLanguage.TraditionalChinese,
            2 => UiLanguage.English,
            _ => UiLanguage.SimplifiedChinese
        };
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        ApplyLanguageToVisualTree(this);
        ScreenshotStatus.Text = _screenshot is null
            ? L("截图默认不保存，也不会上传。", "截圖預設不儲存，也不會上傳。", "Screenshots are not saved or uploaded by default.")
            : L("截图已准备好，只在本机处理。", "截圖已準備好，只在本機處理。", "The screenshot is ready and stays on this computer.");

        if (_report is null)
        {
            if (ProgressPanel.Visibility == Visibility.Visible)
                ProgressText.Text = L("正在准备…", "正在準備…", "Preparing…");
            return;
        }

        var reviewWasVisible = ReviewPanel.Visibility == Visibility.Visible;
        ShowResult(_report);
        if (reviewWasVisible) ReviewButton_Click(this, new RoutedEventArgs());
    }

    private void ApplyLanguageToVisualTree(DependencyObject parent)
    {
        var childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (var index = 0; index < childCount; index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);
            if (child is FrameworkElement { Uid.Length: > 0 } element)
            {
                var value = UiText.Get(_language, element.Uid);
                switch (element)
                {
                    case TextBlock textBlock:
                        textBlock.Text = value;
                        break;
                    case Button button:
                        button.Content = value;
                        break;
                    case Expander expander:
                        expander.Header = value;
                        break;
                }
            }
            ApplyLanguageToVisualTree(child);
        }
    }

    private string LocalizeClarifyingReason(string source) => source switch
    {
        "依据：这次检查没有发现明确的失败线索，先按“画面正常”确认一次即可，不会改动任何东西。" =>
            L(source, "依據：這次檢查沒有發現明確的失敗線索，先按「畫面正常」確認即可，不會改動任何東西。", "Reason: This check found no clear failure clue. Confirming that the screen looks normal will not change anything."),
        "依据：这次没能从截图里读出可用的文字，所以先按“有报错但没认出来”继续。" =>
            L(source, "依據：這次未能從截圖讀出可用文字，所以先按「有錯誤但沒辨識出來」繼續。", "Reason: No usable text was read from the screenshot, so continue as an unrecognized error."),
        _ => L(source, "依據：截圖中讀到了像是錯誤的文字，但還無法判斷屬於哪一類問題。", "Reason: The screenshot contains error-like text, but its problem type is not yet recognized.")
    };

    private string LocalizeClarifyingLabel(string source) => source switch
    {
        OneQuestionRules.NormalLabel => L(source, "畫面正常，我只是想確認", "The screen looks normal; I just want to check"),
        OneQuestionRules.FrozenLabel => L(source, "視窗卡住、一直轉圈或突然斷線", "The window froze, kept spinning, or disconnected"),
        OneQuestionRules.UnrecognizedLabel => L(source, "畫面有錯誤，但這次沒辨識出來", "There is an error on screen, but it was not recognized"),
        _ => source
    };

    private string L(string simplified, string traditional, string english) =>
        UiText.Get(_language, simplified, traditional, english);

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
