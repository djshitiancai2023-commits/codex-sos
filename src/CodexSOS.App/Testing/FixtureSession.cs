using System.Text.Json;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CodexSOS.Core;

namespace CodexSOS.App.Testing;

public sealed class FixtureSession
{
    private FixtureSession(
        string id,
        string title,
        string description,
        string mockOcrText,
        bool screenshotProvided,
        IReadOnlyList<string> knownPrivateTerms,
        SystemFacts system,
        DoctorResult doctor,
        IReadOnlyList<PublicIssue> issues,
        bool issueSearchSucceeded)
    {
        Id = id;
        Title = title;
        Description = description;
        MockOcrText = mockOcrText;
        ScreenshotProvided = screenshotProvided;
        KnownPrivateTerms = knownPrivateTerms;
        System = system;
        Doctor = doctor;
        Issues = issues;
        IssueSearchSucceeded = issueSearchSucceeded;
    }

    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public string MockOcrText { get; }
    public bool ScreenshotProvided { get; }
    public IReadOnlyList<string> KnownPrivateTerms { get; }
    public SystemFacts System { get; }
    public DoctorResult Doctor { get; }
    public IReadOnlyList<PublicIssue> Issues { get; }
    public bool IssueSearchSucceeded { get; }

    public static FixtureSession Load(string path)
    {
        var fullPath = Path.GetFullPath(path);
        using var document = JsonDocument.Parse(File.ReadAllText(fullPath), new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
            MaxDepth = 64
        });
        var root = document.RootElement;
        if (!GetBool(root, "allDataFictional"))
        {
            throw new ArgumentException("Only explicitly fictional scenarios are accepted.", nameof(path));
        }

        var input = root.GetProperty("input");
        var systemElement = root.GetProperty("system");
        var doctorElement = root.GetProperty("doctor");
        var issueSearch = root.GetProperty("issueSearch");
        var redactor = new PrivacyRedactor(GetStringArray(input, "knownPrivateTerms"));
        var doctor = ParseDoctor(doctorElement, redactor);
        var system = new SystemFacts(
            GetString(systemElement, "windowsVersion") ?? "Windows Fixture Edition",
            GetString(systemElement, "architecture") ?? "x64",
            Enum.TryParse<CodexSurface>(GetString(systemElement, "surface"), true, out var surface)
                ? surface
                : CodexSurface.Unknown,
            GetString(systemElement, "codexVersion"),
            GetBool(systemElement, "codexIsRunning"),
            GetBool(systemElement, "possibleDuplicateInstall"),
            GetStringArray(systemElement, "sanitizedInstallHints"));
        var issues = ParseIssues(issueSearch).ToArray();

        return new FixtureSession(
            GetString(root, "id") ?? "UI-fixture",
            GetString(root, "title") ?? "虚构验收场景",
            GetString(input, "description") ?? string.Empty,
            GetString(input, "mockOcrText") ?? string.Empty,
            GetBool(input, "screenshotProvided"),
            GetStringArray(input, "knownPrivateTerms"),
            system,
            doctor,
            issues,
            GetBool(issueSearch, "succeeded"));
    }

    public BitmapSource CreateSyntheticScreenshot()
    {
        const int width = 1200;
        const int height = 720;
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(new SolidColorBrush(Color.FromRgb(22, 25, 31)), null,
                new Rect(0, 0, width, height));
            context.DrawRectangle(new SolidColorBrush(Color.FromRgb(34, 39, 48)), null,
                new Rect(0, 0, width, 78));
            DrawText(context, "CODEX · FICTIONAL ACCEPTANCE WINDOW", 25, FontWeights.SemiBold,
                Color.FromRgb(232, 237, 244), 42, 22);
            var card = new Rect(96, 142, 1008, 410);
            context.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(42, 47, 57)),
                new Pen(new SolidColorBrush(Color.FromRgb(90, 101, 119)), 1), card, 14, 14);
            DrawText(context, "SOMETHING INTERRUPTED CODEX", 34, FontWeights.Bold,
                Color.FromRgb(245, 172, 121), 145, 194);
            var text = string.IsNullOrWhiteSpace(MockOcrText)
                ? "This is fictional UI evidence. No real account or task data is present."
                : MockOcrText;
            DrawText(context, text, 27, FontWeights.Normal,
                Color.FromRgb(240, 242, 246), 145, 272);
            DrawText(context, "Fixture request: req_fixture_000000", 19, FontWeights.Normal,
                Color.FromRgb(164, 174, 190), 145, 350);
            DrawText(context, "Your work has not been deleted.", 23, FontWeights.Normal,
                Color.FromRgb(201, 209, 221), 145, 416);
        }

        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    public DiagnosticOrchestrator CreateOrchestrator(PrivacyRedactor redactor)
    {
        return new DiagnosticOrchestrator(
            new FixtureDoctorRunner(Doctor),
            new FixtureSystemCollector(System),
            new FixtureFaultEventCollector(),
            new FixtureIssueClient(Issues, IssueSearchSucceeded),
            new FixtureStatusClient(),
            redactor,
            new StableTermExtractor(redactor),
            new DiagnosisEngine(),
            new SimilarIssueMatcher(),
            new PublicReportBuilder(redactor));
    }

    private static DoctorResult ParseDoctor(JsonElement doctor, PrivacyRedactor redactor)
    {
        var expected = GetString(doctor, "expectedState") ?? "Unavailable";
        var exitCode = GetInt(doctor, "exitCode") ?? -1;
        if (doctor.TryGetProperty("stdoutJson", out var json))
        {
            return new DoctorJsonParser(redactor).Parse(json.GetRawText(), exitCode);
        }

        var state = Enum.TryParse<DoctorState>(expected, true, out var parsed)
            ? parsed
            : DoctorState.Unavailable;
        var summary = state switch
        {
            DoctorState.Unsupported => "官方体检在这个版本暂不可用，但仍然完成了其他检查。",
            DoctorState.TimedOut => "Codex 官方体检等待时间过长，SOS 已先继续完成其他检查。",
            DoctorState.Malformed => "官方体检返回的内容无法识别，但仍然完成了其他检查。",
            _ => "Codex 官方体检这次无法运行，但仍然完成了其他检查。"
        };
        return new DoctorResult(state, null, [], summary, exitCode);
    }

    private static IEnumerable<PublicIssue> ParseIssues(JsonElement search)
    {
        if (!search.TryGetProperty("issues", out var issues) || issues.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var issue in issues.EnumerateArray())
        {
            yield return new PublicIssue(
                GetLong(issue, "number") ?? 0,
                GetString(issue, "title") ?? string.Empty,
                GetString(issue, "body") ?? string.Empty,
                GetString(issue, "htmlUrl") ?? string.Empty,
                GetString(issue, "state") ?? "unknown",
                GetStringArray(issue, "labels"));
        }
    }

    private static void DrawText(DrawingContext context, string text, double size,
        FontWeight weight, Color color, double x, double y)
    {
        var formatted = new FormattedText(
            text,
            global::System.Globalization.CultureInfo.GetCultureInfo("en-US"),
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
            size,
            new SolidColorBrush(color),
            1.0)
        {
            MaxTextWidth = 910,
            MaxLineCount = 3,
            Trimming = TextTrimming.CharacterEllipsis
        };
        context.DrawText(formatted, new Point(x, y));
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    private static bool GetBool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
    private static int? GetInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : null;
    private static long? GetLong(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt64(out var number) ? number : null;
    private static string[] GetStringArray(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!).ToArray()
            : [];

    private sealed class FixtureDoctorRunner(DoctorResult result) : IDoctorRunner
    {
        public async Task<DoctorResult> RunAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(180, cancellationToken).ConfigureAwait(false);
            return result;
        }
    }

    private sealed class FixtureSystemCollector(SystemFacts facts) : ISystemCollector
    {
        public Task<SystemFacts> CollectAsync(CancellationToken cancellationToken) => Task.FromResult(facts);
    }

    private sealed class FixtureFaultEventCollector : IFaultEventCollector
    {
        public Task<IReadOnlyList<FaultEvent>> CollectAsync(DateTimeOffset since, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FaultEvent>>([]);
    }

    private sealed class FixtureIssueClient(IReadOnlyList<PublicIssue> issues, bool succeeded) : IIssueSearchClient
    {
        public Task<IssueSearchResult> SearchAsync(
            IReadOnlyList<string> stableTerms,
            CancellationToken cancellationToken) =>
            Task.FromResult(stableTerms.Count == 0
                ? IssueSearchResult.NoUsableTerms()
                : new IssueSearchResult(
                    issues,
                    succeeded ? IssueSearchState.Completed : IssueSearchState.Unavailable,
                    "https://github.com/openai/codex/issues?q=" +
                    Uri.EscapeDataString(string.Join(' ', stableTerms.Take(4)))));
    }

    private sealed class FixtureStatusClient : IServiceStatusClient
    {
        public Task<ServiceStatusResult> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ServiceStatusResult(true, true, "operational",
                "OpenAI 官方状态页显示 Codex：运行正常。", DateTimeOffset.UtcNow));
    }
}

public sealed class FixtureOcrService(string text) : Services.ILocalOcrService
{
    public string Name => "虚构验收识字结果";
    public bool IsAvailable => true;
    public Task<string> ReadAsync(BitmapSource image, CancellationToken cancellationToken) => Task.FromResult(text);
}
