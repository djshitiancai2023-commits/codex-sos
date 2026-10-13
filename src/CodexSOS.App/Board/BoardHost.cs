using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net.Http;
using System.Security.Principal;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using CodexSOS.App.Services;
using CodexSOS.App.Testing;
using CodexSOS.Core;

namespace CodexSOS.App.Board;

internal sealed class BoardHost : IDisposable
{
    private readonly FixtureSession? _fixture;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly CancellationTokenSource _closing = new();
    private readonly DiagnosticOrchestrator _orchestrator;
    private readonly SemaphoreSlim _replyLock = new(1, 1);
    private readonly Action<string> _openBrowser;
    private readonly Action<string> _copyText;
    private readonly string _applicationDirectory;
    private string? _previewText;
    private string? _previewId;
    private Process? _node;
    private DiagnosticReport? _report;
    private bool _checking;
    private MainWindow? _classic;
    private string? _url;
    private readonly string _pipeName;
    private NamedPipeServerStream? _pipe;
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public BoardHost(FixtureSession? fixture, Action<string>? openBrowser = null,
        Action<string>? copyText = null, string? applicationDirectory = null)
    {
        _fixture = fixture;
        _openBrowser = openBrowser ?? OpenBrowser;
        _copyText = copyText ?? Clipboard.SetText;
        _applicationDirectory = applicationDirectory ?? AppContext.BaseDirectory;
        _pipeName = "CodexSOS-dot-" + WindowsIdentity.GetCurrent().User?.Value + (fixture is null ? "" : "-fictional");
        var redactor = fixture is null ? new PrivacyRedactor([Environment.UserName]) : new PrivacyRedactor(fixture.KnownPrivateTerms);
        _orchestrator = fixture?.CreateOrchestrator(redactor) ?? new DiagnosticOrchestrator(
            new DoctorRunner(new DoctorJsonParser(redactor)), new WindowsSystemCollector(), new WindowsFaultEventCollector(),
            new GitHubIssueSearchClient(_http), new OpenAIStatusClient(_http), redactor, new StableTermExtractor(redactor),
            new DiagnosisEngine(), new SimilarIssueMatcher(), new PublicReportBuilder(redactor));
    }

    public async Task<bool> StartAsync()
    {
        try
        {
            using var existing = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            await existing.ConnectAsync(200);
            await existing.WriteAsync(new byte[] { 1 });
            return false;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException) { }

        _pipe = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var directory = Path.Combine(_applicationDirectory, "dot-assistant");
        var data = _fixture is null
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexSOS", "TaskBoard")
            : Path.Combine(Path.GetTempPath(), "CodexSOS-Fictional-Board", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(data);
        var start = new ProcessStartInfo
        {
            FileName = Path.Combine(directory, "runtime", "node.exe"),
            WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(Path.Combine(directory, "launch.mjs"));
        start.Environment["SOS_BOARD_DATA"] = data;
        start.Environment["SOS_CODEX_EXE"] = _fixture is null ? OfficialExecutable.Find() ?? "" : "";
        start.Environment["SOS_BOARD_FIXTURE"] = _fixture is null ? "0" : "1";
        _node = Process.Start(start) ?? throw new IOException("Board could not start.");
        // Never persist raw process streams. Drain bounded messages only.
        _node.ErrorDataReceived += (_, _) => { }; _node.BeginErrorReadLine();
        _ = ReceiveAsync(); _ = ListenForOpenAsync();
        await _ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        return true;
    }

    private async Task ListenForOpenAsync()
    {
        try
        {
            while (!_closing.IsCancellationRequested && _pipe is not null)
            {
                await _pipe.WaitForConnectionAsync(_closing.Token);
                var bytes = new byte[1]; await _pipe.ReadExactlyAsync(bytes, _closing.Token);
                if (bytes[0] == 1 && _url is not null) _openBrowser(_url);
                _pipe.Disconnect();
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException) { }
    }

    private async Task ReceiveAsync()
    {
        try
        {
            while (_node is not null && await _node.StandardOutput.ReadLineAsync(_closing.Token) is { } line)
            {
                if (line.Length > 100_000) continue;
                using var document = JsonDocument.Parse(line);
                var message = document.RootElement.Clone();
                if (message.GetProperty("method").GetString() == "ready")
                {
                    var url = message.GetProperty("url").GetString();
                    if (url is not null && Regex.IsMatch(url, @"^http://127\.0\.0\.1:\d{1,5}/#[A-Za-z0-9_-]{43}$"))
                    { _url = url; _openBrowser(url); _ready.TrySetResult(); }
                    continue;
                }
                // Dispatch on the STA thread for native clipboard/dialog operations.
                _ = Application.Current.Dispatcher.InvokeAsync(() => HandleAndReplyAsync(message)).Task.Unwrap();
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or JsonException or InvalidOperationException) { }
        finally
        {
            if (!_ready.Task.IsCompleted) _ready.TrySetException(new IOException("Board did not start."));
            else if (!_closing.IsCancellationRequested) Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
        }
    }

    private async Task HandleAndReplyAsync(JsonElement message)
    {
        var id = message.GetProperty("id").GetInt32();
        object response;
        try { response = new { id, result = await HandleAsync(message.GetProperty("method").GetString()!, message.GetProperty("params")) }; }
        catch { response = new { id, error = "operation_failed" }; }
        try
        {
            await _replyLock.WaitAsync(_closing.Token);
            try { if (_node is { HasExited: false }) await _node.StandardInput.WriteLineAsync(JsonSerializer.Serialize(response, Json)); }
            finally { _replyLock.Release(); }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException) { }
    }

    internal async Task<object> HandleAsync(string method, JsonElement args)
    {
        var language = args.TryGetProperty("language", out var item) ? ParseLanguage(item.GetString()) : LanguageSettings.Load(LanguageSettings.DefaultPath);
        switch (method)
        {
            case "diagnose":
                if (_checking || _classic is not null) throw new InvalidOperationException();
                _checking = true;
                _previewId = null; _previewText = null;
                try
                {
                    // Task titles, goals, identifiers and paths never become diagnostic/search input.
                    _report = await _orchestrator.RunAsync(new UserEvidence(
                        "Codex 本机任务报告 systemError 状态，原因未知；请核对本机状态。", "", false, DateTimeOffset.UtcNow), null, _closing.Token);
                    return Report(language);
                }
                finally { _checking = false; }
            case "report": return Report(language);
            case "copy-report":
                if (_report is null || _previewText is null ||
                    args.GetProperty("reportId").GetString() != _report.RunId.ToString() ||
                    args.GetProperty("previewId").GetString() != _previewId) throw new InvalidOperationException();
                _copyText(_previewText); // Copy exactly the last presented, redacted preview.
                return new { copied = true, sent = false };
            case "language":
                if (_fixture is null && !LanguageSettings.TrySave(LanguageSettings.DefaultPath, language)) throw new IOException();
                return new { ok = true };
            case "classic":
                if (_checking) throw new InvalidOperationException();
                if (_classic is null)
                {
                    _classic = new MainWindow(_fixture, _orchestrator);
                    _classic.ReportReady += report =>
                    {
                        _report = report;
                        _ = SendReportAsync(_classic?.DisplayLanguage ?? language);
                    };
                    _classic.Closed += (_, _) => _classic = null;
                }
                _classic.SetDisplayLanguage(language);
                _classic.Show(); _classic.Activate();
                return new { opened = true };
            case "open":
                var url = args.GetProperty("url").GetString() ?? "";
                if (url != "https://chatgpt.com/" && !Regex.IsMatch(url, @"^(?:codex://threads/|https://chatgpt\.com/c/)[a-fA-F0-9]{8}(?:-[a-fA-F0-9]{4}){3}-[a-fA-F0-9]{12}$")) throw new ArgumentException();
                _openBrowser(url); return new { opened = true, sent = false };
            case "quit":
                if (args.TryGetProperty("idle", out var idle) && idle.GetBoolean() && (_classic is not null || _checking)) return new { ok = false };
                _ = Application.Current.Dispatcher.BeginInvoke(() => Application.Current.Shutdown());
                return new { ok = true };
            default: throw new ArgumentException();
        }
    }

    internal async Task SendReportAsync(UiLanguage language)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { method = "report-ready", result = Report(language) }, Json);
            await _replyLock.WaitAsync(_closing.Token);
            try { if (_node is { HasExited: false }) await _node.StandardInput.WriteLineAsync(payload); }
            finally { _replyLock.Release(); }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException) { }
    }

    private object Report(UiLanguage language)
    {
        if (_report is null) throw new InvalidOperationException();
        _previewText = UiText.BuildPublicReport(_report, language);
        _previewId = Guid.NewGuid().ToString();
        return new
        {
            id = _report.RunId.ToString(),
            previewId = _previewId,
            language = language == UiLanguage.English ? "en" : language == UiLanguage.TraditionalChinese ? "zh-TW" : "zh-CN",
            answers = new[] { UiText.DiagnosisSummary(language, _report.Diagnosis), UiText.SimilarSummary(language, _report.SimilarIssues),
                UiText.SafeNextStep(language, _report.Diagnosis), _report.Diagnosis.OfficialFeedbackAppropriate
                    ? UiText.Get(language, "ResultMoreAnswer")
                    : UiText.Get(language, "这更像其他程序的问题，请先不要交给 Codex 官方。", "這更像其他程式的問題，請先不要交給 Codex 官方。", "This looks like another program's problem; do not send it to the Codex maintainers yet.") },
            text = _previewText, privacy = UiText.BuildPrivacyReview(_report, language)
        };
    }

    private static UiLanguage ParseLanguage(string? language) => language switch { "en" => UiLanguage.English, "zh-TW" => UiLanguage.TraditionalChinese, _ => UiLanguage.SimplifiedChinese };
    private static void OpenBrowser(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    public void Dispose()
    {
        _closing.Cancel(); _pipe?.Dispose();
        try { _node?.StandardInput.Close(); } catch { }
        _http.Dispose(); _classic?.Close();
    }
}
