using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodexSOS.App;
using CodexSOS.App.Services;
using CodexSOS.App.Testing;
using CodexSOS.Core;

namespace CodexSOS.Tests;

internal static class Program
{
    private const string FakeDoctorModeVariable = "CODEXSOS_FIXTURE_DOCTOR_MODE";
    private static readonly DateTimeOffset FrozenNow = DateTimeOffset.Parse("2030-01-02T03:04:05Z");

    public static async Task<int> Main(string[] args)
    {
        if (args.SequenceEqual(["doctor", "--json"]))
        {
            return await RunFakeNativeDoctorAsync().ConfigureAwait(false);
        }

        var tests = new (string Name, Func<Task> Run)[]
        {
            ("privacy: all fictional fixture cases", TestPrivacyFixtureAsync),
            ("doctor parser: ok, warning, fail, unknown schema, malformed", TestDoctorParserAsync),
            ("doctor process: unsupported, timeout, malformed, exit 1", TestFakeDoctorProcessAsync),
            ("diagnosis: green doctor never says Codex is fine", TestGreenDoctorCannotExplainAsync),
            ("diagnosis: one Chinese sentence identifies task recovery", TestChineseRecoveryDescriptionAsync),
            ("diagnosis: repeated desktop exits stay cautious without an event", TestRepeatedDesktopExitDiagnosisAsync),
            ("diagnosis: external startup window does not become a Codex bug", TestExternalStartupScopeAsync),
            ("events: crash parser filters leak warnings and unrelated ChatGPT", TestWindowsFaultEventParserAsync),
            ("diagnosis: screenshot states, menu words, and all-clear input stay honest", TestScreenshotStatesAsync),
            ("orchestrator: fixed signal survives path redaction", TestPathFixedSignalProductionAsync),
            ("search: three states and narrow terms", TestSearchStatesAsync),
            ("search: ordinary menu screenshot sends no request", TestMenuScreenshotNoSearchAsync),
            ("fixtures: fictional UI scenarios load cleanly", TestScenarioFixturesLoadAsync),
            ("diagnosis: ties stay honest and unknown doctor checks stay evidence", TestTieAndUnknownDoctorAsync),
            ("capture: browser exclusion and ambiguous desktop safety", TestWindowSelectionAsync),
            ("system collection: remote paths are skipped", TestSystemPathSafetyAsync),
            ("packaging: Windows manifest is declared", TestWindowsManifestAsync),
            ("similar issues: high matches and no-match restraint", TestSimilarityAsync),
            ("similar issues: crash exception codes are explainable", TestCrashCodeSimilarityAsync),
            ("orchestrator: every collector failure degrades safely", TestOrchestratorFailureFallbackAsync),
            ("public export: privacy canaries never escape", TestPublicExportPrivacyAsync),
            ("official feedback: form fields are complete and privacy checked", TestOfficialFeedbackDraftAsync),
            ("follow-up: at most one plain-language choice", TestAtMostOneFollowUpAsync),
            ("localization: Simplified Chinese default plus complete Traditional Chinese and English UI", TestLocalizationAsync),
            ("boundaries: no private-state read, OpenAI API, or write request", TestSourceBoundariesAsync),
            ("fake doctor: no dependency on real user data", TestFakeDoctorDoesNotNeedRealUserDataAsync)
        };

        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("Codex SOS executable test harness — all diagnostic data is fictional");
        Console.WriteLine($"Runtime: {Environment.Version}; OS: {Environment.OSVersion.VersionString}");
        Console.WriteLine();

        var failed = 0;
        var stopwatch = Stopwatch.StartNew();
        foreach (var test in tests)
        {
            var testTimer = Stopwatch.StartNew();
            try
            {
                await test.Run().ConfigureAwait(false);
                Console.WriteLine($"PASS  {test.Name} ({testTimer.ElapsedMilliseconds} ms)");
            }
            catch (Exception exception)
            {
                failed++;
                Console.WriteLine($"FAIL  {test.Name} ({testTimer.ElapsedMilliseconds} ms)");
                Console.WriteLine($"      {exception.GetType().Name}: {exception.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"RESULT: {tests.Length - failed}/{tests.Length} passed; {failed} failed; {stopwatch.ElapsedMilliseconds} ms total");
        return failed == 0 ? 0 : 1;
    }

    private static Task TestPrivacyFixtureAsync()
    {
        var repository = FindRepositoryRoot();
        var fixturePath = Path.Combine(repository, "tests", "fixtures", "privacy", "redaction-cases.json");
        using var document = JsonDocument.Parse(File.ReadAllText(fixturePath));
        Assert(document.RootElement.GetProperty("allDataFictional").GetBoolean(), "Privacy fixture must declare fictional data.");

        var redactor = new PrivacyRedactor();
        var count = 0;
        foreach (var item in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            count++;
            var id = item.GetProperty("id").GetString() ?? "unknown";
            var kind = item.GetProperty("kind").GetString() ?? string.Empty;
            var input = item.GetProperty("input").GetString() ?? string.Empty;
            var terms = item.TryGetProperty("knownPrivateTerms", out var knownTerms)
                ? knownTerms.EnumerateArray().Select(term => term.GetString() ?? string.Empty).ToArray()
                : [];
            var result = redactor.Redact(input, terms);

            if (item.TryGetProperty("expected", out var expected))
            {
                Equal(expected.GetString(), result.SanitizedText, $"Privacy case {id}");
            }
            if (item.TryGetProperty("expectedMustContain", out var mustContain))
            {
                Contains(result.SanitizedText, mustContain.GetString() ?? string.Empty, $"Privacy case {id}");
            }
            if (item.TryGetProperty("expectedMustNotContain", out var mustNotContain))
            {
                NotContains(result.SanitizedText, mustNotContain.GetString() ?? string.Empty, $"Privacy case {id}");
            }

            Assert(result.Findings.Any(finding => finding.Kind == kind && finding.Count > 0),
                $"Privacy case {id} did not report finding kind {kind}.");
            NotContains(result.SanitizedText, input, $"Privacy case {id} retained its complete original value");
        }

        Assert(count >= 17, "The complete privacy fixture set was not exercised.");
        return Task.CompletedTask;
    }

    private static Task TestDoctorParserAsync()
    {
        var parser = new DoctorJsonParser(new PrivacyRedactor());

        var ok = parser.Parse(DoctorJson("ok"), 0);
        Equal(DoctorState.Ok, ok.State, "Doctor ok state");
        Contains(ok.PublicSummary, "无法单独解释", "Doctor ok disclaimer");
        NotContains(ok.PublicSummary, "Codex 没问题", "Doctor ok false reassurance");

        var warning = parser.Parse(DoctorJson("warning", "network", "warning", "Connection may be slow"), 0);
        Equal(DoctorState.Warning, warning.State, "Doctor warning state");
        Equal(1, warning.Checks.Count, "Doctor warning check count");

        var failed = parser.Parse(DoctorJson("fail", "auth", "fail", "Authentication failed"), 1);
        Equal(DoctorState.Failed, failed.State, "Doctor failed state");
        Contains(failed.PublicSummary, "不等于已经确定根因", "Doctor failure restraint");

        var unknownSchema = parser.Parse("""
            {"schemaVersion":77,"overallStatus":"ok","codexVersion":"0.99.7-fixture","checks":{}}
            """, 0);
        Equal(DoctorState.UnknownSchema, unknownSchema.State, "Doctor unknown schema state");
        Contains(unknownSchema.PublicSummary, "没有猜测", "Unknown schema restraint");

        var malformed = parser.Parse("}{not-json", 0);
        Equal(DoctorState.Malformed, malformed.State, "Doctor malformed state");
        NotContains(malformed.PublicSummary, "}{not-json", "Raw malformed output must not be shown");
        return Task.CompletedTask;
    }

    private static async Task TestFakeDoctorProcessAsync()
    {
        var executable = GetTestAppHost();
        var parser = new DoctorJsonParser(new PrivacyRedactor());

        var unsupported = await RunDoctorModeAsync(executable, parser, "unsupported", TimeSpan.FromSeconds(2));
        Equal(DoctorState.Unsupported, unsupported.State, "Fake doctor unsupported state");

        var timer = Stopwatch.StartNew();
        var timedOut = await RunDoctorModeAsync(executable, parser, "timeout", TimeSpan.FromMilliseconds(180));
        Equal(DoctorState.TimedOut, timedOut.State, "Fake doctor timeout state");
        Assert(timer.Elapsed < TimeSpan.FromSeconds(3), "A timed-out doctor held the application open too long.");

        var malformed = await RunDoctorModeAsync(executable, parser, "malformed", TimeSpan.FromSeconds(2));
        Equal(DoctorState.Malformed, malformed.State, "Fake doctor malformed state");

        var exitOne = await RunDoctorModeAsync(executable, parser, "exit1", TimeSpan.FromSeconds(2));
        Equal(DoctorState.Failed, exitOne.State, "Fake doctor exit-1 state");
        Equal(1, exitOne.ExitCode, "Fake doctor exit code");
    }

    private static Task TestGreenDoctorCannotExplainAsync()
    {
        var diagnosis = new DiagnosisEngine().Diagnose(
            new UserEvidence("感觉不太对", string.Empty, false, FrozenNow),
            FixtureSystem(CodexSurface.Desktop),
            new DoctorResult(DoctorState.Ok, "0.99.7-fixture", [],
                "Codex 官方体检暂未发现异常；这份检查无法单独解释所有运行中的故障。", 0),
            []);

        Equal(IncidentCategory.DoctorCannotExplain, diagnosis.Category, "Green doctor category");
        Equal(ConfidenceLevel.CannotDetermine, diagnosis.Confidence, "Green doctor confidence");
        Equal("这份检查无法解释当前故障。", diagnosis.PlainSummary, "Green doctor plain summary");
        NotContains(diagnosis.PlainSummary, "Codex 没问题", "Green doctor false reassurance");
        NotContains(diagnosis.PlainSummary, "没有故障", "Green doctor false reassurance");
        return Task.CompletedTask;
    }

    private static Task TestChineseRecoveryDescriptionAsync()
    {
        var diagnosis = new DiagnosisEngine().Diagnose(
            new UserEvidence("上一个任务做到一半断开了，点继续一直恢复不了。", string.Empty, false, FrozenNow),
            FixtureSystem(CodexSurface.Desktop),
            DoctorResult.Unavailable("fixture"),
            []);

        Equal(IncidentCategory.TaskRecovery, diagnosis.Category, "Chinese recovery category");
        Assert(diagnosis.Confidence is ConfidenceLevel.LikelyRelated or ConfidenceLevel.PossiblyRelated,
            "Chinese recovery description did not produce a cautious useful confidence.");
        Contains(diagnosis.SafeNextStep, "不要删除", "Recovery safe action");
        NotContains(diagnosis.PlainSummary, "已经确定", "Recovery overclaim");
        return Task.CompletedTask;
    }

    private static Task TestRepeatedDesktopExitDiagnosisAsync()
    {
        var engine = new DiagnosisEngine();
        const string description = "Codex 最近反复自己关掉，重新打开后过一会儿又退出了，正在做的任务也被打断。";

        var withoutEvent = engine.Diagnose(
            new UserEvidence(description, string.Empty, false, FrozenNow),
            FixtureSystem(CodexSurface.Desktop),
            DoctorResult.Unavailable("fixture"),
            []);
        Equal(IncidentCategory.DesktopApplication, withoutEvent.Category, "Repeated desktop exit category");
        Equal(ConfidenceLevel.PossiblyRelated, withoutEvent.Confidence, "Exit without Windows event stays cautious");
        Contains(withoutEvent.Limitations.Single(item => item.Contains("Windows", StringComparison.Ordinal)),
            "不等于没有闪退", "Missing Windows event limitation");
        Contains(withoutEvent.SafeNextStep, "不要删除", "Repeated exit safe action");

        var confirmed = engine.Diagnose(
            new UserEvidence(description, string.Empty, false, FrozenNow),
            FixtureSystem(CodexSurface.Desktop),
            DoctorResult.Unavailable("fixture"),
            [new FaultEvent(FrozenNow, "Codex.exe", "KERNELBASE.dll", "c0000409")]);
        Equal(IncidentCategory.DesktopApplication, confirmed.Category, "Confirmed desktop crash category");
        Equal(ConfidenceLevel.LikelyRelated, confirmed.Confidence, "Description plus event confidence");
        Contains(confirmed.Evidence.Single(item => item.Contains("异常记录", StringComparison.Ordinal)),
            "异常记录", "Confirmed event evidence");
        NotContains(confirmed.PlainSummary, "已经确定根因", "Confirmed crash must not claim root cause");

        var normalClose = engine.Diagnose(
            new UserEvidence("我手动关闭了 Codex，之后没有问题。", string.Empty, false, FrozenNow),
            FixtureSystem(CodexSurface.Desktop),
            DoctorResult.Unavailable("fixture"),
            []);
        Assert(normalClose.Category != IncidentCategory.DesktopApplication,
            "A normal user-requested close was treated as a crash.");
        return Task.CompletedTask;
    }

    private static Task TestWindowsFaultEventParserAsync()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Event xmlns="http://schemas.microsoft.com/win/2004/08/events/event">
              <System><EventID>1000</EventID><TimeCreated SystemTime="2030-01-02T03:04:05Z" /></System>
              <EventData>
                <Data Name="AppName">C:\\Fixture\\Codex.exe</Data>
                <Data Name="ModuleName">KERNELBASE.dll</Data>
                <Data Name="ExceptionCode">c0000409</Data>
              </EventData>
            </Event>
            <Event xmlns="http://schemas.microsoft.com/win/2004/08/events/event">
              <System><EventID>1001</EventID><TimeCreated SystemTime="2030-01-02T03:04:06Z" /></System>
              <EventData>
                <Data Name="EventName">RADAR_PRE_LEAK_64</Data>
                <Data Name="AppName">Codex.exe</Data>
              </EventData>
            </Event>
            <Event xmlns="http://schemas.microsoft.com/win/2004/08/events/event">
              <System><EventID>1001</EventID><TimeCreated SystemTime="2030-01-02T03:04:07Z" /></System>
              <EventData>
                <Data Name="EventName">APPCRASH</Data>
                <Data Name="P1">ChatGPT.exe</Data>
                <Data Name="PackageFullName">OpenAI.Codex_2p2nqsd0c76g0</Data>
                <Data Name="P4">KERNELBASE.dll</Data>
                <Data Name="P7">c06d007f</Data>
              </EventData>
            </Event>
            <Event xmlns="http://schemas.microsoft.com/win/2004/08/events/event">
              <System><EventID>1000</EventID><TimeCreated SystemTime="2030-01-02T03:04:08Z" /></System>
              <EventData>
                <Data Name="AppName">ChatGPT.exe</Data>
                <Data Name="ModuleName">KERNELBASE.dll</Data>
                <Data Name="ExceptionCode">c0000409</Data>
              </EventData>
            </Event>
            <Event xmlns="http://schemas.microsoft.com/win/2004/08/events/event">
              <System><EventID>1000</EventID><TimeCreated SystemTime="2030-01-02T03:04:09Z" /></System>
              <EventData>
                <Data Name="AppName">notcodex-helper.exe</Data>
                <Data Name="ModuleName">KERNELBASE.dll</Data>
                <Data Name="ExceptionCode">c0000409</Data>
              </EventData>
            </Event>
            """;

        var parsed = WindowsFaultEventParser.Parse(xml);
        Equal(2, parsed.Count, "Crash parser accepted only Codex crash records");
        Assert(parsed.Any(item => item.Application.Equals("Codex.exe", StringComparison.OrdinalIgnoreCase) &&
                                  item.ExceptionCode == "c0000409"),
            "Codex Application Error was lost.");
        Assert(parsed.Any(item => item.Application.Equals("ChatGPT.exe", StringComparison.OrdinalIgnoreCase) &&
                                  item.ExceptionCode == "c06d007f"),
            "Official Codex package using ChatGPT.exe was lost.");
        Assert(parsed.All(item => item.ExceptionCode is "c0000409" or "c06d007f"),
            "Unexpected event data escaped the parser.");
        return Task.CompletedTask;
    }

    private static Task TestScreenshotStatesAsync()
    {
        var engine = new DiagnosisEngine();

        var menuScreen = engine.Diagnose(
            new UserEvidence(string.Empty,
                "Codex 0.99.7-fixture\nSettings\nGeneral\nVersion 0.99.7-fixture\nUpdate channel: stable\nLogin with account",
                true, FrozenNow),
            FixtureSystem(CodexSurface.Desktop),
            DoctorResult.Unavailable("fixture"),
            []);
        Assert(menuScreen.Category is not IncidentCategory.Login and not IncidentCategory.InstallationOrVersion,
            "Menu words alone selected a fault category.");
        Equal(ConfidenceLevel.CannotDetermine, menuScreen.Confidence, "Normal-screen confidence");
        Contains(menuScreen.PlainSummary, "暂未看到明确报错", "Normal-screen summary");
        Contains(menuScreen.PlainSummary, "不能确认 Codex 一定正常", "Normal-screen restraint");
        NotContains(menuScreen.PlainSummary, "Codex 没问题", "Normal-screen false reassurance");

        var unknownError = engine.Diagnose(
            new UserEvidence(string.Empty,
                "Unhandled exception: quux_florb_2030 blew up while syncing.",
                true, FrozenNow),
            FixtureSystem(CodexSurface.Desktop),
            DoctorResult.Unavailable("fixture"),
            []);
        Equal(IncidentCategory.Unknown, unknownError.Category, "Unknown-error category");
        Contains(unknownError.PlainSummary, "像是有报错", "Unknown-error summary");
        Contains(unknownError.PlainSummary, "认不出", "Unknown-error honesty");
        Contains(unknownError.SafeNextStep, "重新打开", "Unknown-error restart step");
        Contains(unknownError.SafeNextStep, "不要删除", "Unknown-error safety");

        var blankShot = engine.Diagnose(
            new UserEvidence(string.Empty, string.Empty, true, FrozenNow),
            FixtureSystem(CodexSurface.Desktop),
            DoctorResult.Unavailable("fixture"),
            []);
        Contains(blankShot.PlainSummary, "没有从这张截图里读到足够的文字", "Blank-screenshot summary");
        Contains(blankShot.PlainSummary, "不能把它当成正常", "Blank-screenshot restraint");

        var ocrConnection = engine.Diagnose(
            new UserEvidence(string.Empty,
                "Stream disconnected before completion; reconnecting...",
                true, FrozenNow),
            FixtureSystem(CodexSurface.Desktop),
            DoctorResult.Unavailable("fixture"),
            []);
        Equal(IncidentCategory.Connection, ocrConnection.Category, "Fixed OCR connection phrase kept original behavior.");

        var ocrRecovery = engine.Diagnose(
            new UserEvidence(string.Empty,
                "failed to resume task: AbsolutePathBuf missing in fixture",
                true, FrozenNow),
            FixtureSystem(CodexSurface.Desktop),
            DoctorResult.Unavailable("fixture"),
            []);
        Equal(IncidentCategory.TaskRecovery, ocrRecovery.Category, "Fixed OCR recovery phrase kept original behavior.");
        Contains(ocrRecovery.SafeNextStep, "不要删除", "OCR recovery safe action");

        var describedLogin = engine.Diagnose(
            new UserEvidence("Codex 提示我重新登录，账号怎么都进不去", string.Empty, false, FrozenNow),
            FixtureSystem(CodexSurface.Desktop),
            DoctorResult.Unavailable("fixture"),
            []);
        Equal(IncidentCategory.Login, describedLogin.Category, "User-described login still classifies.");

        var confirmInput = engine.Diagnose(
            new UserEvidence("画面正常，我只是想确认一下有没有问题", string.Empty, false, FrozenNow),
            FixtureSystem(CodexSurface.Desktop),
            DoctorResult.Unavailable("fixture"),
            []);
        Equal(ConfidenceLevel.CannotDetermine, confirmInput.Confidence, "All-clear input confidence");
        Contains(confirmInput.PlainSummary, "没有看到明确的故障线索", "All-clear summary");
        NotContains(confirmInput.PlainSummary, "完全正常", "All-clear must not overclaim");
        NotContains(confirmInput.PlainSummary, "Codex 没问题", "All-clear must stay cautious");
        return Task.CompletedTask;
    }

    private static async Task TestPathFixedSignalProductionAsync()
    {
        var redactor = new PrivacyRedactor();
        var issueClient = new CapturingIssues();
        var orchestrator = new DiagnosticOrchestrator(
            new FixtureDoctor(),
            new FixtureSystemCollector(),
            new FixtureEvents(),
            issueClient,
            new FixtureStatus(),
            redactor,
            new StableTermExtractor(redactor),
            new DiagnosisEngine(),
            new SimilarIssueMatcher(),
            new PublicReportBuilder(redactor));

        var report = await orchestrator.RunAsync(
            new UserEvidence(
                "C:\\Users\\Fixture.User\\p\\m.rs — failed to resume task; PATH_CANARY_FIXTURE",
                string.Empty,
                false,
                FrozenNow),
            null,
            CancellationToken.None).ConfigureAwait(false);

        Equal(IncidentCategory.TaskRecovery, report.Diagnosis.Category,
            "Path-masked fixed phrase category");
        Assert(issueClient.LastTerms.Contains("failed to resume task", StringComparer.OrdinalIgnoreCase),
            "Path-masked fixed phrase was not passed to search.");
        Assert(issueClient.LastTerms.All(term => !term.Contains("PATH_CANARY_FIXTURE", StringComparison.OrdinalIgnoreCase)),
            "Private canary leaked into search terms.");
        NotContains(report.PublicReportMarkdown, "C:\\Users\\Fixture.User", "Path leaked into public report");
        NotContains(report.PublicReportMarkdown, "PATH_CANARY_FIXTURE", "Search canary leaked into public report");
        var privacyCount = report.PrivacyFindings.Sum(finding => finding.Count);
        Assert(privacyCount > 0, "Production privacy ledger did not record the fictional path.");
        var reviewMatch = Regex.Match(report.PrivacyReviewMarkdown, @"遮住了 (\d+) 处");
        Assert(reviewMatch.Success, "Production privacy review did not show a count.");
        Equal(privacyCount, int.Parse(reviewMatch.Groups[1].Value),
            "Production privacy screen/export count parity");

        var chineseIssueClient = new CapturingIssues();
        var chineseOrchestrator = new DiagnosticOrchestrator(
            new FixtureDoctor(),
            new FixtureSystemCollector(),
            new FixtureEvents(),
            chineseIssueClient,
            new FixtureStatus(),
            redactor,
            new StableTermExtractor(redactor),
            new DiagnosisEngine(),
            new SimilarIssueMatcher(),
            new PublicReportBuilder(redactor));
        var chineseReport = await chineseOrchestrator.RunAsync(
            new UserEvidence(
                "Codex 路径是 C:\\Users\\Fixture.User\\Apps\\Codex\\Codex.exe，双击以后没反应；PATH_ORDER_CANARY_FIXTURE",
                string.Empty,
                false,
                FrozenNow),
            null,
            CancellationToken.None).ConfigureAwait(false);

        Equal(IncidentCategory.DesktopApplication, chineseReport.Diagnosis.Category,
            "Path-masked Chinese symptom category");
        Assert(chineseIssueClient.LastTerms.Contains("not responding", StringComparer.OrdinalIgnoreCase),
            "Path-masked Chinese symptom was not converted to a stable search term.");
        Assert(chineseIssueClient.LastTerms.All(term =>
                !term.Contains("PATH_ORDER_CANARY_FIXTURE", StringComparison.OrdinalIgnoreCase)),
            "Path-order canary leaked into search terms.");
        NotContains(chineseReport.PublicReportMarkdown, "C:\\Users\\Fixture.User",
            "Chinese symptom fixture path leaked into public report");
        NotContains(chineseReport.PublicReportMarkdown, "PATH_ORDER_CANARY_FIXTURE",
            "Chinese symptom fixture canary leaked into public report");
    }

    private static async Task TestExternalStartupScopeAsync()
    {
        const string description =
            "最近几天每次开机都会弹出一个没有任何文字的黑窗口，卡在那里。我怀疑是另一款工具的开机启动程序，这正常吗？";
        var diagnosis = new DiagnosisEngine().Diagnose(
            new UserEvidence(description, string.Empty, false, FrozenNow),
            FixtureSystem(CodexSurface.Desktop),
            new DoctorResult(DoctorState.Ok, "0.99.7-fixture", [],
                "Codex 官方体检暂未发现异常；这份检查无法单独解释所有使用中的故障。"),
            []);

        Equal(IncidentCategory.Unknown, diagnosis.Category, "External startup category");
        Assert(!diagnosis.OfficialFeedbackAppropriate,
            "An external startup program must not offer the Codex bug form.");
        Contains(diagnosis.PlainSummary, "看不出这是 Codex 自己的问题", "External startup summary");
        NotContains(diagnosis.SafeNextStep, "重新打开 Codex", "External startup next step");
        Assert(OneQuestionRules.Build(false, diagnosis, false, OcrAttemptOutcome.None) is null,
            "A clearly external startup program should not trigger a Codex symptom question.");

        var redactor = new PrivacyRedactor();
        var issueClient = new CapturingIssues();
        var orchestrator = new DiagnosticOrchestrator(
            new FixtureDoctor(),
            new FixtureSystemCollector(),
            new FixtureEvents(),
            issueClient,
            new FixtureStatus(),
            redactor,
            new StableTermExtractor(redactor),
            new DiagnosisEngine(),
            new SimilarIssueMatcher(),
            new PublicReportBuilder(redactor));
        var report = await orchestrator.RunAsync(
            new UserEvidence(description, string.Empty, false, FrozenNow),
            null,
            CancellationToken.None).ConfigureAwait(false);
        Equal(0, issueClient.CallCount, "External startup issue-search call count");
        Contains(report.SimilarIssues.PlainSummary, "没有搜索 Codex 官方问题",
            "External startup search summary");

        var feedbackBlocked = false;
        try
        {
            _ = new OfficialFeedbackBuilder(redactor).Build(report);
        }
        catch (InvalidOperationException)
        {
            feedbackBlocked = true;
        }
        Assert(feedbackBlocked, "External startup report must not become an official Codex draft.");

        var namedOtherProgram = new DiagnosisEngine().Diagnose(
            new UserEvidence("Orchid Workbench 程序闪退了，不是 Codex。", string.Empty, false, FrozenNow),
            FixtureSystem(CodexSurface.Desktop),
            DoctorResult.Unavailable("fixture"),
            []);
        Assert(!namedOtherProgram.OfficialFeedbackAppropriate,
            "An explicitly named other program must not become a Codex bug.");

        foreach (var externalDescription in new[]
                 {
                     "Codex 没问题，是另一个程序闪退了。",
                     "Codex 正常，Chrome 崩溃了。"
                 })
        {
            var externalDiagnosis = new DiagnosisEngine().Diagnose(
                new UserEvidence(externalDescription, string.Empty, false, FrozenNow),
                FixtureSystem(CodexSurface.Desktop),
                DoctorResult.Unavailable("fixture"),
                []);
            Equal(IncidentCategory.Unknown, externalDiagnosis.Category,
                $"Explicitly healthy Codex category: {externalDescription}");
            Assert(!externalDiagnosis.OfficialFeedbackAppropriate,
                $"Another program's failure must not become a Codex bug: {externalDescription}");
        }

        var realCodexDescriptions = new[]
        {
            "Codex 打开网页时突然断开，再也连不上。",
            "Codex 登录时跳到浏览器，然后一直转圈进不去。",
            "开机后 Codex 弹出黑窗口，卡在那里。",
            "安装另一个程序后，Codex 开始闪退。",
            "Codex 正常打开网页时突然断开，再也连不上。",
            "Codex 正常使用浏览器时突然断开。",
            "Codex 正常启动后黑窗口卡住了。"
        };
        foreach (var codexDescription in realCodexDescriptions)
        {
            var codexDiagnosis = new DiagnosisEngine().Diagnose(
                new UserEvidence(codexDescription, string.Empty, false, FrozenNow),
                FixtureSystem(CodexSurface.Desktop),
                DoctorResult.Unavailable("fixture"),
                []);
            Assert(codexDiagnosis.OfficialFeedbackAppropriate,
                $"A real Codex symptom was incorrectly routed away: {codexDescription}");
            NotContains(codexDiagnosis.PlainSummary, "另一款程序",
                "Real Codex symptom external-program summary");
        }
    }

    private static async Task TestSearchStatesAsync()
    {
        var redactor = new PrivacyRedactor();
        var extractor = new StableTermExtractor(redactor);
        var fixedTerms = extractor.Extract(
            "stream disconnected before completion; kernelbase.dll and incidentalword");
        Equal(1, fixedTerms.Count, "Fixed phrase query width");
        Equal("stream disconnected before completion", fixedTerms[0], "Fixed phrase priority");
        var feedbackTerms = extractor.Extract("Feedback upload failed. Feedback ID unchanged.");
        Equal(1, feedbackTerms.Count, "Feedback failure query width");
        Equal("feedback upload failed", feedbackTerms[0], "Feedback failure fixed phrase");
        var chineseFeedbackTerms = extractor.Extract("Codex 反馈传不上去，反馈编号没变化。");
        Equal(1, chineseFeedbackTerms.Count, "Chinese feedback failure query width");
        Equal("feedback upload failed", chineseFeedbackTerms[0], "Chinese feedback failure mapping");
        Equal(0, extractor.Extract("这是一句没有固定映射的中文描述").Count,
            "Unmapped Chinese terms");

        var noRequestHandler = new CountingHandler();
        var client = new GitHubIssueSearchClient(new HttpClient(noRequestHandler));
        var noTerms = await client.SearchAsync([], CancellationToken.None).ConfigureAwait(false);
        Equal(IssueSearchState.NoUsableTerms, noTerms.State, "No-term search state");
        Equal(0, noRequestHandler.CallCount, "No-term HTTP call count");

        var matcher = new SimilarIssueMatcher();
        var noUsable = matcher.Match("中文", IncidentCategory.Unknown, CodexSurface.Unknown,
            [], IssueSearchState.NoUsableTerms, null);
        Contains(noUsable.PlainSummary, "稳定", "No-term summary");
        var unavailable = matcher.Match("stream disconnected", IncidentCategory.Connection,
            CodexSurface.Unknown, [], IssueSearchState.Unavailable,
            "https://github.com/openai/codex/issues?q=is%3Aissue+fixture");
        Contains(unavailable.PlainSummary, "没能连上", "Unavailable summary");
        var completed = matcher.Match("stream disconnected", IncidentCategory.Connection,
            CodexSurface.Unknown, [], IssueSearchState.Completed, null);
        Contains(completed.PlainSummary, "暂未找到", "Completed zero-result summary");
        NotContains(completed.PlainSummary, "没能连上", "Completed must not look offline");

        var feedbackMatch = matcher.Match(
            "feedback upload failed",
            IncidentCategory.Unknown,
            CodexSurface.Desktop,
            [new PublicIssue(
                990020,
                "[Windows] Feedback upload failed in fixture",
                "Fictional upload failure with an unchanged feedback ID.",
                "https://issues.example.test/openai/codex/990020",
                "open",
                ["fixture"])],
            IssueSearchState.Completed,
            null);
        Equal(1, feedbackMatch.Matches.Count, "Feedback failure match count");
        Equal(IssueSimilarityTier.Possible, feedbackMatch.Matches[0].Tier,
            "Feedback failure must remain a cautious possible match");
        Contains(string.Join(' ', feedbackMatch.Matches[0].Reasons), "feedback upload failed",
            "Feedback failure explainable reason");
    }

    private static async Task TestMenuScreenshotNoSearchAsync()
    {
        var redactor = new PrivacyRedactor();
        var extractor = new StableTermExtractor(redactor);
        const string menuOcr =
            "Codex 0.99.7-fixture\nSettings\nGeneral\nVersion 0.99.7-fixture\nUpdate channel: stable\nLogin with account";

        var menuTerms = extractor.Extract(menuOcr);
        Equal(0, menuTerms.Count, "Ordinary settings-menu OCR produced public search terms.");

        var handler = new CountingHandler();
        var client = new GitHubIssueSearchClient(new HttpClient(handler));
        var direct = await client.SearchAsync(menuTerms, CancellationToken.None).ConfigureAwait(false);
        Equal(IssueSearchState.NoUsableTerms, direct.State, "Menu-only OCR search state");
        Equal(0, handler.CallCount, "Menu-only OCR triggered an HTTP request.");

        var known = extractor.Extract("Stream disconnected before completion");
        Equal(1, known.Count, "Known fixed error phrase still searchable.");
        Contains(string.Join('|', known), "stream disconnected before completion", "Known fixed phrase content");

        var issueClient = new CapturingIssues();
        var orchestrator = new DiagnosticOrchestrator(
            new FixtureDoctor(),
            new FixtureSystemCollector(),
            new FixtureEvents(),
            issueClient,
            new FixtureStatus(),
            redactor,
            extractor,
            new DiagnosisEngine(),
            new SimilarIssueMatcher(),
            new PublicReportBuilder(redactor));
        var report = await orchestrator.RunAsync(
            new UserEvidence(string.Empty, menuOcr, true, FrozenNow),
            null,
            CancellationToken.None).ConfigureAwait(false);
        Equal(0, issueClient.LastTerms.Count, "Orchestrator issued a public search for menu-only OCR.");
        Equal(IssueSearchState.NoUsableTerms, report.SimilarIssues.SearchState, "Orchestrator menu search state");
        Contains(report.SimilarIssues.PlainSummary, "稳定", "Menu-only OCR no-term summary");
    }

    private static Task TestScenarioFixturesLoadAsync()
    {
        var root = FindRepositoryRoot();
        var files = Directory.EnumerateFiles(Path.Combine(root, "tests", "fixtures", "scenarios"), "*.json")
            .OrderBy(path => path, StringComparer.Ordinal).ToArray();
        Assert(files.Length >= 17, "The fictional UI scenario set is incomplete.");
        foreach (var file in files)
        {
            var session = FixtureSession.Load(file);
            Assert(session.Id.StartsWith("UI-", StringComparison.Ordinal),
                $"{Path.GetFileName(file)} produced an unexpected id {session.Id}.");
            Assert(!string.IsNullOrWhiteSpace(session.Title), $"{Path.GetFileName(file)} lost its title.");
        }

        return Task.CompletedTask;
    }

    private static Task TestTieAndUnknownDoctorAsync()
    {
        var engine = new DiagnosisEngine();
        foreach (var sentence in new[]
        {
            "任务突然断开，重新登录也没用",
            "一直转圈，然后提示我重新登录",
            "Codex 做了很久突然断开了，我的账号还登着呢"
        })
        {
            var diagnosis = engine.Diagnose(
                new UserEvidence(sentence, string.Empty, false, FrozenNow),
                FixtureSystem(CodexSurface.Desktop),
                DoctorResult.Unavailable("fixture"),
                []);
            Contains(diagnosis.PlainSummary, "或", $"Tie summary for {sentence}");
            Assert(diagnosis.Candidates.Count >= 2, $"Tie candidates were lost for {sentence}");
            Assert(diagnosis.Category != IncidentCategory.Login,
                $"Tie silently selected Login for {sentence}");
            Contains(diagnosis.SafeNextStep, "重新打开", "Tie safe next step");
            NotContains(diagnosis.SafeNextStep, "登录页面", "Tie must use generic safe next step");
        }

        const string unknownSummary = "Fictional doctor introduced a new check name";
        var unknownDoctor = new DoctorResult(
            DoctorState.Warning,
            "0.99.7-fixture",
            [new DoctorCheck("new_check_2030", "warning", unknownSummary)],
            "官方体检发现一项新提示；这只是线索。", 0);
        var unknownDiagnosis = engine.Diagnose(
            new UserEvidence(string.Empty, string.Empty, false, FrozenNow),
            FixtureSystem(CodexSurface.Unknown),
            unknownDoctor,
            []);
        Assert(unknownDiagnosis.Category != IncidentCategory.DoctorFailure,
            "Unknown doctor check was classified as DoctorFailure");

        var built = new PublicReportBuilder(new PrivacyRedactor()).Build(
            Guid.Parse("00000000-0000-0000-0000-000000001030"),
            FrozenNow,
            new UserEvidence(string.Empty, string.Empty, false, FrozenNow),
            FixtureSystem(CodexSurface.Unknown),
            unknownDoctor,
            unknownDiagnosis,
            new SimilarIssueSummary([], true, "暂未找到足够相似的问题。"),
            ServiceStatusResult.Unavailable(),
            []);
        Contains(built.PublicReport, unknownSummary, "Unknown doctor evidence was hidden");
        return Task.CompletedTask;
    }

    private static Task TestWindowSelectionAsync()
    {
        static CodexWindowCapture.WindowCandidate Candidate(
            string processName, bool foreground = false, long area = 100) =>
            new(IntPtr.Zero, processName, foreground, area);

        Assert(CodexWindowCapture.ChooseCandidate(
            [Candidate("Chrome", true), Candidate("msedge", false)]) is null,
            "Browser process entered screenshot candidates.");

        var codex = CodexWindowCapture.ChooseCandidate(
            [Candidate("Codex"), Candidate("ChatGPT", area: 900)]);
        Equal("Codex", codex?.ProcessName, "Codex priority over ChatGPT without foreground evidence");

        var foregroundChatGpt = CodexWindowCapture.ChooseCandidate(
            [Candidate("Codex", area: 900), Candidate("ChatGPT", true, 100)]);
        Equal("ChatGPT", foregroundChatGpt?.ProcessName, "Foreground desktop window priority");

        Assert(CodexWindowCapture.ChooseCandidate(
            [Candidate("Codex", area: 900), Candidate("Codex", area: 100)]) is null,
            "Multiple Codex windows were selected by area instead of safe fallback.");
        Equal("ChatGPT", CodexWindowCapture.ChooseCandidate(
            [Candidate("ChatGPT")])?.ProcessName, "Single ChatGPT desktop selection");
        Assert(CodexWindowCapture.ChooseCandidate(
            [Candidate("ChatGPT"), Candidate("ChatGPT", area: 900)]) is null,
            "Multiple ChatGPT windows were selected by area instead of safe fallback.");
        return Task.CompletedTask;
    }

    private static Task TestSystemPathSafetyAsync()
    {
        Assert(WindowsSystemCollector.IsSafeLocalPath("C:\\Users\\Fixture\\bin"),
            "A fictional local path was rejected.");
        Assert(!WindowsSystemCollector.IsSafeLocalPath("\\\\fixture-server\\share\\codex.exe"),
            "UNC path was not rejected before file access.");
        Assert(!WindowsSystemCollector.IsSafeLocalPath("https://fixture.invalid/codex"),
            "URI-like PATH entry was not rejected.");
        Assert(!WindowsSystemCollector.IsSafeLocalPath("relative\\codex"),
            "Relative PATH entry was not rejected.");
        return Task.CompletedTask;
    }

    private static Task TestWindowsManifestAsync()
    {
        var root = FindRepositoryRoot();
        var manifest = Path.Combine(root, "src", "CodexSOS.App", "app.manifest");
        var project = Path.Combine(root, "src", "CodexSOS.App", "CodexSOS.App.csproj");
        Assert(File.Exists(manifest), "Windows application manifest is missing.");
        var manifestText = File.ReadAllText(manifest);
        Contains(manifestText, "supportedOS", "Manifest supported Windows declaration");
        Contains(manifestText, "PerMonitorV2", "Manifest DPI declaration");
        Contains(File.ReadAllText(project), "<ApplicationManifest>app.manifest</ApplicationManifest>",
            "Project does not embed the Windows manifest");
        return Task.CompletedTask;
    }

    private static Task TestSimilarityAsync()
    {
        var matcher = new SimilarIssueMatcher();
        var issues = Enumerable.Range(1, 3)
            .Select(index => new PublicIssue(
                900000 + index,
                $"Stream disconnected before completion in Windows desktop fixture {index}",
                "Fictional Codex desktop connection disconnected and remained reconnecting on Windows.",
                $"https://issues.example.test/openai/codex/{900000 + index}",
                index <= 2 ? "open" : "closed",
                ["fixture", "connection"]))
            .ToArray();

        var high = matcher.Match(
            "Windows 桌面版突然显示 Stream disconnected before completion，然后一直 reconnecting。",
            IncidentCategory.Connection,
            CodexSurface.Desktop,
            issues,
            true,
            null);
        Equal(3, high.Matches.Count(match => match.Tier == IssueSimilarityTier.High), "High similarity count");
        Assert(high.Matches.All(match => match.Score >= 75), "A high match scored below the documented threshold.");
        Contains(high.PlainSummary, "找到了 3 个高度相似的公开问题，其中 2 个仍未关闭", "High similarity summary");
        Assert(high.Matches.All(match => match.Reasons.Any(reason => reason.Contains("固定错误短语", StringComparison.Ordinal)) &&
                                                match.Reasons.Any(reason => reason.Contains("故障类型一致", StringComparison.Ordinal))),
            "High matches lack explainable independent reasons.");

        var unrelated = matcher.Match(
            "窗口颜色看起来有些不同",
            IncidentCategory.Unknown,
            CodexSurface.Unknown,
            [new PublicIssue(910001, "Documentation punctuation fixture", "Fictional wording-only report.",
                "https://issues.example.test/openai/codex/910001", "open", ["fixture"])],
            true,
            null);
        Equal(0, unrelated.Matches.Count, "Unrelated issue must not be surfaced");
        Equal("暂未找到足够相似的问题。", unrelated.PlainSummary, "No-match summary");
        return Task.CompletedTask;
    }

    private static Task TestCrashCodeSimilarityAsync()
    {
        var terms = new StableTermExtractor(new PrivacyRedactor()).Extract("Windows desktop crash c0000409");
        Equal("c0000409", terms.Single(), "Crash exception code stable search term");

        var issue = new PublicIssue(
            920001,
            "Codex desktop crash c0000409 on Windows fixture",
            "Fictional Codex desktop application crash with exception c0000409.",
            "https://issues.example.test/openai/codex/920001",
            "open",
            ["fixture", "desktop"]);
        var match = new SimilarIssueMatcher().Match(
            "Windows desktop Codex c0000409",
            IncidentCategory.DesktopApplication,
            CodexSurface.Desktop,
            [issue],
            IssueSearchState.Completed,
            null);
        Equal(IssueSimilarityTier.High, match.Matches.Single().Tier, "Crash exception code high match");
        Assert(match.Matches.Single().Reasons.Any(reason => reason.Contains("固定错误短语", StringComparison.Ordinal)),
            "Crash code match lacks an explainable exact reason.");
        return Task.CompletedTask;
    }

    private static async Task TestOrchestratorFailureFallbackAsync()
    {
        var redactor = new PrivacyRedactor();
        var orchestrator = new DiagnosticOrchestrator(
            new ThrowingDoctor(),
            new ThrowingSystem(),
            new ThrowingEvents(),
            new ThrowingIssues(),
            new ThrowingStatus(),
            redactor,
            new StableTermExtractor(redactor),
            new DiagnosisEngine(),
            new SimilarIssueMatcher(),
            new PublicReportBuilder(redactor));

        var report = await orchestrator.RunAsync(
            new UserEvidence("Codex 突然断开", string.Empty, false, FrozenNow),
            null,
            CancellationToken.None);

        Equal(DoctorState.Unavailable, report.Doctor.State, "Orchestrator doctor fallback");
        Equal("暂时无法确定", report.System.WindowsVersion, "Orchestrator system fallback");
        Assert(!report.SimilarIssues.SearchSucceeded, "Issue search failure was presented as success.");
        Contains(report.SimilarIssues.PlainSummary, "没能连上", "Offline issue-search fallback");
        Assert(report.SimilarIssues.BrowserFallbackUrl?.StartsWith(
            "https://github.com/openai/codex/issues?q=", StringComparison.Ordinal) == true,
            "Offline fallback did not provide the official public issue-search entrance.");
        Assert(!report.ServiceStatus.Succeeded, "Status failure was presented as success.");
        Assert(!report.ScreenshotSaved, "A screenshot was incorrectly marked as saved.");
        Contains(report.PublicReportMarkdown, "原截图没有放进", "Public report screenshot boundary");
    }

    private static Task TestPublicExportPrivacyAsync()
    {
        var knownTerms = new[] { "Rowan Fixture", "Project-Orchid-Fixture", "Example Fixture Labs" };
        var redactor = new PrivacyRedactor(knownTerms);
        var builder = new PublicReportBuilder(redactor);
        var evidence = new UserEvidence(
            "Authentication failed\nName: Rowan Fixture\nEmail: rowan.fixture@example.test\n" +
            "Path: C:\\Users\\Rowan.Fixture\\Documents\\Project-Orchid-Fixture\\trace.log\n" +
            "Company: Example Fixture Labs\nPrivate remote: git@example.test:fixture-labs/private-orchid.git\n" +
            "Internal URL: https://build.internal:8443/job/fixture\nPrivate IP: 10.24.7.19\n" +
            "Session ID: SESSIONFIXTURE123456\nRequest ID: REQUESTFIXTURE123456\n" +
            "Account ID: ACCOUNTFIXTURE123456\nAuthorization: Bearer FICTIONAL_BEARER_7qX9pL4nV8mK2sR6\n" +
            "password=DefinitelyFixture42!\nRandom: Z7qP4nV8mK2sR6tY9wB3cD5fG1hJ8kL0",
            "Authentication failed for rowan.fixture@example.test",
            true,
            FrozenNow);
        var doctor = new DoctorResult(DoctorState.Failed, "0.99.7-fixture",
            [new DoctorCheck("auth", "fail", "Authentication failed at C:\\Users\\Rowan.Fixture\\fixture")],
            "官方体检发现了异常；这仍只是线索，不等于已经确定根因。", 1);
        var diagnosis = new Diagnosis(
            IncidentCategory.Login,
            ConfidenceLevel.PossiblyRelated,
            "可能有关：Codex 登录问题。",
            "先重新打开 Codex；不要删除账号或重置本地数据。",
            ["登录提示可能有关"],
            ["不是已经确定的根因"]);
        var built = builder.Build(
            Guid.Parse("00000000-0000-0000-0000-000000009999"),
            FrozenNow,
            evidence,
            FixtureSystem(CodexSurface.Desktop),
            doctor,
            diagnosis,
            new SimilarIssueSummary([], true, "暂未找到足够相似的问题。"),
            ServiceStatusResult.Unavailable(),
            [new FaultEvent(FrozenNow, "Codex.exe", "C:\\Users\\Rowan.Fixture\\KERNELBASE.dll", "0xfixture")]);

        var combined = built.PublicReport + "\n" + built.PrivacyReview;
        foreach (var canary in new[]
        {
            "Rowan Fixture", "rowan.fixture@example.test", "C:\\Users\\Rowan.Fixture",
            "Project-Orchid-Fixture", "Example Fixture Labs", "git@example.test:fixture-labs/private-orchid.git",
            "https://build.internal:8443/job/fixture", "10.24.7.19", "SESSIONFIXTURE123456",
            "REQUESTFIXTURE123456", "ACCOUNTFIXTURE123456", "FICTIONAL_BEARER_7qX9pL4nV8mK2sR6",
            "DefinitelyFixture42!", "Z7qP4nV8mK2sR6tY9wB3cD5fG1hJ8kL0"
        })
        {
            NotContains(combined, canary, $"Public export privacy canary {canary}");
        }

        Contains(built.PublicReport, "<EMAIL>", "Public export email replacement");
        Contains(built.PublicReport, "<LOCAL_PATH>", "Public export path replacement");
        Contains(built.PublicReport, "原截图没有放进", "Public export screenshot exclusion");
        Contains(built.PrivacyReview, "自动遮盖不能保证 100% 安全", "Privacy review honest limitation");
        Assert(built.Findings.Count > 0, "Public export did not report privacy findings.");
        return Task.CompletedTask;
    }

    private static Task TestOfficialFeedbackDraftAsync()
    {
        var report = new DiagnosticReport(
            Guid.Parse("00000000-0000-0000-0000-000000001313"),
            FrozenNow,
            new UserEvidence(
                "Codex exited while working. Contact rowan.fixture@example.test; details were under C:\\Users\\Rowan.Fixture\\Private-Fixture.",
                "The app exited unexpectedly.",
                true,
                FrozenNow),
            FixtureSystem(CodexSurface.Desktop),
            new DoctorResult(DoctorState.Ok, "0.99.7-fixture", [],
                "The official check found no current warning; it cannot explain every runtime failure.", 0),
            new Diagnosis(
                IncidentCategory.DesktopApplication,
                ConfidenceLevel.PossiblyRelated,
                "Possibly related to a Codex desktop app problem.",
                "Fully close and reopen Codex. Do not delete local tasks or data.",
                ["The description says the desktop app exited"],
                ["No root cause has been confirmed"]),
            new SimilarIssueSummary(
                [new MatchedIssue(
                    new PublicIssue(1313, "Fictional Codex desktop exit", "Fictional body",
                        "https://github.com/openai/codex/issues/1313", "open", ["app"]),
                    IssueSimilarityTier.High, 82, ["Same problem type"])],
                true,
                "One highly similar public issue was found."),
            ServiceStatusResult.Unavailable(),
            [new FaultEvent(FrozenNow, "Codex.exe", "C:\\Users\\Rowan.Fixture\\KERNELBASE.dll", "c0000409")],
            [],
            false,
            "fictional public report",
            "fictional privacy review");

        var draft = new OfficialFeedbackBuilder(new PrivacyRedactor()).Build(report);
        foreach (var heading in new[]
                 {
                     "What version of the Codex App are you using?",
                     "What subscription do you have?",
                     "What platform is your computer?",
                     "What issue are you seeing?",
                     "What steps can reproduce the bug?",
                     "What is the expected behavior?",
                     "Additional information",
                     "OpenAI technical follow-up"
                 })
        {
            Contains(draft, heading, $"Official feedback field {heading}");
        }

        Contains(draft, "NOT SUBMITTED", "Official feedback local-only status");
        Contains(draft, "Suggested title: [Windows] Codex App exits unexpectedly", "Official feedback suggested title");
        Contains(draft, "Session ID, token limit usage, and context window usage: not collected", "Official feedback optional private fields");
        Contains(draft, "Incident/check time and time zone", "Official feedback incident time");
        Contains(draft, "Visible error evidence", "Official feedback visible-error status");
        Contains(draft, "Feedback ID: not collected", "Official feedback ID boundary");
        Contains(draft, "run `/feedback`", "Official feedback ID guidance");
        Contains(draft, "Do not reproduce the bug just to obtain one", "Official feedback no-reproduction guidance");
        Contains(draft, "unchanged Feedback ID does not prove", "Official feedback delivery-proof boundary");
        Contains(draft, "whether this affects one task or several", "Official feedback scope guidance");
        Contains(draft, "attach only reviewed, sanitized material", "Official feedback sanitized-log guidance");
        Contains(draft, "original screenshot is not included", "Official feedback screenshot boundary");
        Contains(draft, "https://github.com/openai/codex/issues/1313", "Official feedback similar issue");
        Contains(draft, "<EMAIL>", "Official feedback email redaction");
        Contains(draft, "<LOCAL_PATH>", "Official feedback path redaction");
        NotContains(draft, "rowan.fixture@example.test", "Official feedback raw email");
        NotContains(draft, "C:\\Users\\Rowan.Fixture", "Official feedback raw path");
        return Task.CompletedTask;
    }

    private static Task TestAtMostOneFollowUpAsync()
    {
        var cannotDetermine = new Diagnosis(
            IncidentCategory.Unknown,
            ConfidenceLevel.CannotDetermine,
            "目前还无法判断是哪一类问题。",
            "先完全关闭并重新打开 Codex；暂时不要删除任务或本地数据。",
            ["现有信息里没有足够稳定的故障特征"],
            ["没有足够证据时，SOS 不会强行猜根因"]);
        var unknownError = cannotDetermine with
        {
            PlainSummary = "截图里像是有报错，但暂时还认不出是哪一类问题。",
            Evidence = ["截图文字里有疑似报错的字样，但没有命中已知的固定错误特征"]
        };
        var classified = cannotDetermine with
        {
            Confidence = ConfidenceLevel.PossiblyRelated,
            PlainSummary = "可能有关：Codex 连接中断。"
        };

        Assert(OneQuestionRules.Build(true, cannotDetermine, false, OcrAttemptOutcome.None) is null,
            "A second follow-up was offered after one was already shown.");
        Assert(OneQuestionRules.Build(false, classified, true, OcrAttemptOutcome.Success) is null,
            "A follow-up was offered even though the diagnosis already had a clear classification.");

        var question = OneQuestionRules.Build(false, cannotDetermine, false, OcrAttemptOutcome.None);
        Assert(question is not null, "CannotDetermine diagnosis lost its single chance to clarify.");
        Equal(3, question!.Choices.Count, "Follow-up must offer exactly the three fixed plain choices.");
        Equal(1, question.Choices.Count(choice => choice.Recommended), "Recommended follow-up choice count");
        Equal(
            $"{OneQuestionRules.NormalLabel}|{OneQuestionRules.FrozenLabel}|{OneQuestionRules.UnrecognizedLabel}",
            string.Join("|", question.Choices.Select(choice => choice.Label)),
            "Fixed follow-up labels");
        var plainCopy = question.Question + " " + question.RecommendationReason + " " +
                        string.Join(' ', question.Choices.Select(choice => choice.Label + " " + choice.Meaning));
        foreach (var forbiddenWord in new[]
                 {
                     "doctor", "json", "cli", "api", "github", "命令行", "终端", "日志", "最常见"
                 })
        {
            NotContains(plainCopy, forbiddenWord, "Follow-up technical wording");
        }
        Contains(question.RecommendationReason, "依据", "Recommendation must state its basis");

        var noClue = OneQuestionRules.Build(false, cannotDetermine, true, OcrAttemptOutcome.Success);
        Assert(noClue is not null, "A readable screenshot without failure clues lost its follow-up.");
        Equal(OneQuestionRules.NormalLabel,
            noClue!.Choices.Single(choice => choice.Recommended).Label,
            "Readable screenshot without failure clues should recommend the all-clear choice.");

        foreach (var outcome in new[]
                 {
                     OcrAttemptOutcome.NoText,
                     OcrAttemptOutcome.Timeout,
                     OcrAttemptOutcome.Unavailable,
                     OcrAttemptOutcome.Failure
                 })
        {
            var unreadable = OneQuestionRules.Build(false, cannotDetermine, true, outcome);
            Assert(unreadable is not null, $"Unreadable screenshot ({outcome}) lost its follow-up.");
            Equal(OneQuestionRules.UnrecognizedLabel,
                unreadable!.Choices.Single(choice => choice.Recommended).Label,
                $"Unreadable screenshot ({outcome}) should recommend the unrecognized-error choice.");
        }

        var unknown = OneQuestionRules.Build(false, unknownError, true, OcrAttemptOutcome.Success);
        Assert(unknown is not null, "An unrecognized error lost its follow-up.");
        Equal(OneQuestionRules.UnrecognizedLabel,
            unknown!.Choices.Single(choice => choice.Recommended).Label,
            "An unrecognized error should recommend the unrecognized-error choice.");
        return Task.CompletedTask;
    }

    private static Task TestLocalizationAsync()
    {
        Equal(UiLanguage.SimplifiedChinese, default(UiLanguage), "Default UI language");
        Assert(UiText.Keys.Count >= 35, "The visible UI localization catalog is unexpectedly incomplete.");
        foreach (var key in UiText.Keys)
        {
            foreach (var language in Enum.GetValues<UiLanguage>())
            {
                var value = UiText.Get(language, key);
                Assert(!string.IsNullOrWhiteSpace(value), $"Localization key {key} is empty for {language}.");
                Assert(!string.Equals(value, key, StringComparison.Ordinal),
                    $"Localization key {key} is missing for {language}.");
            }
        }

        foreach (var key in new[] { "StartTitle", "StartSubtitle", "StartButton", "ResultWhat", "ReviewWarning", "OfficialFeedbackHint", "CopyOfficialFeedbackButton", "OpenOfficialFeedbackButton", "Footer" })
        {
            var english = UiText.Get(UiLanguage.English, key);
            Assert(!Regex.IsMatch(english, @"[\u4e00-\u9fff]"), $"English UI text still contains Chinese characters: {key}");
        }

        var diagnosis = new Diagnosis(
            IncidentCategory.Connection,
            ConfidenceLevel.PossiblyRelated,
            "可能有关：Codex 连接中断。",
            "先重新打开 Codex，并查看官方服务状态；不要修改系统网络设置。",
            ["截图或描述里出现了连接中断特征"],
            ["这些是可核对的线索，不是已经确定的根因"]);
        Contains(UiText.DiagnosisSummary(UiLanguage.TraditionalChinese, diagnosis), "可能", "Traditional diagnosis");
        Contains(UiText.DiagnosisSummary(UiLanguage.English, diagnosis), "Possibly", "English diagnosis");
        Contains(UiText.SafeNextStep(UiLanguage.English, diagnosis), "Do not", "English safe next step");
        Contains(UiText.Get(UiLanguage.SimplifiedChinese, "OfficialFeedbackHint"), "不代表发送成功",
            "Simplified Chinese delivery-proof guidance");
        Contains(UiText.Get(UiLanguage.English, "OfficialFeedbackHint"), "reproduce the failure",
            "English no-reproduction guidance");
        Contains(UiText.Get(UiLanguage.English, "OfficialFeedbackHint"), "does not prove delivery",
            "English delivery-proof guidance");
        Contains(UiText.Get(UiLanguage.SimplifiedChinese, "CopyOfficialFeedbackButton"), "无需登录",
            "Simplified Chinese copy-only sign-in guidance");
        Contains(UiText.Get(UiLanguage.English, "CopyOfficialFeedbackButton"), "no sign-in",
            "English copy-only sign-in guidance");

        var externalDiagnosis = new Diagnosis(
            IncidentCategory.Unknown,
            ConfidenceLevel.CannotDetermine,
            "目前看不出这是 Codex 自己的问题，更像是另一款程序或开机项目的窗口。",
            "先别删除 Codex、这个程序或本地资料；先确认这个窗口属于哪个程序。",
            ["描述明确提到了浏览器、网页、开机项目或另一款程序"],
            ["Codex SOS 只检查 Codex，不能替其他程序判断根因"],
            OfficialFeedbackAppropriate: false);
        Contains(UiText.DiagnosisSummary(UiLanguage.TraditionalChinese, externalDiagnosis), "另一個程式",
            "Traditional external-program diagnosis");
        Contains(UiText.DiagnosisSummary(UiLanguage.English, externalDiagnosis), "another program",
            "English external-program diagnosis");
        Contains(UiText.SafeNextStep(UiLanguage.English, externalDiagnosis), "identify",
            "English external-program next step");
        NotContains(UiText.SafeNextStep(UiLanguage.English, externalDiagnosis), "reopen Codex",
            "English external-program next step must not restart Codex");
        var externalSimilar = new SimilarIssueSummary(
            [],
            false,
            "这更像其他程序的问题，所以没有搜索 Codex 官方问题。",
            SearchState: IssueSearchState.NoUsableTerms);
        Contains(UiText.SimilarSummary(UiLanguage.English, externalSimilar),
            "no Codex issues were searched", "English external-program search summary");
        return Task.CompletedTask;
    }

    private static Task TestSourceBoundariesAsync()
    {
        var root = FindRepositoryRoot();
        var sourceFiles = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories)
            .Where(path => (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                            path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) &&
                           !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                           !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert(sourceFiles.Length > 0, "No production source files were found for the boundary audit.");

        var sources = sourceFiles.Select(path => (Path: path, Text: File.ReadAllText(path))).ToArray();
        var all = string.Join("\n", sources.Select(source => source.Text));

        NotContains(all, "api.openai.com", "OpenAI API endpoint boundary");
        NotContains(all, "OPENAI_API_KEY", "OpenAI API-key boundary");
        Assert(!Regex.IsMatch(all, @"PackageReference\s+Include\s*=\s*[\""'](?:OpenAI|Azure\.AI\.OpenAI)[\""']",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "An OpenAI model SDK package is referenced.");
        Assert(!Regex.IsMatch(all, @"HttpMethod\.(?:Post|Put|Patch|Delete)|\.(?:PostAsync|PutAsync|PatchAsync|DeleteAsync)\s*\(",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "A production network write method exists.");

        foreach (var source in sources.Where(source => source.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            var sensitiveRead = Regex.IsMatch(source.Text,
                @"(?:File\.(?:Read|Open)|Directory\.(?:Enumerate|GetFiles|GetDirectories))[^;\r\n]*(?:auth\.json|rollout\.jsonl|sessions?|\.codex)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            Assert(!sensitiveRead, $"Sensitive Codex state read found in {Path.GetFileName(source.Path)}.");
        }

        var requestMethods = Regex.Matches(all, @"new\s+HttpRequestMessage\s*\(\s*HttpMethod\.([A-Za-z]+)",
                RegexOptions.CultureInvariant)
            .Select(match => match.Groups[1].Value)
            .ToArray();
        Assert(requestMethods.Length >= 2, "Expected read-only public network clients were not found.");
        Assert(requestMethods.All(method => string.Equals(method, "Get", StringComparison.Ordinal)),
            "A non-GET automatic request was found.");
        Contains(all,
            "https://github.com/openai/codex/issues/new?template=1-codex-app.yml",
            "Exact official Codex App bug-form URL");
        NotContains(all, "issues/new?template=1-codex-app.yml&", "Official feedback URL must not carry report data");
        NotContains(all, "issues/new?body=", "Official feedback URL must not carry a report body");
        Contains(all, "CopyOfficialFeedbackButton_Click", "Copy-only official-feedback action");
        Contains(all, "OpenOfficialFeedbackButton_Click", "Explicit public-form action");
        return Task.CompletedTask;
    }

    private static async Task TestFakeDoctorDoesNotNeedRealUserDataAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "CodexSOS-Fictional-Boundary-" + Guid.NewGuid().ToString("N"));
        var fakeCodexHome = Path.Combine(root, "fake-user", ".codex");
        Directory.CreateDirectory(Path.Combine(fakeCodexHome, "sessions", "session-fixture"));
        var authPath = Path.Combine(fakeCodexHome, "auth.json");
        var sessionPath = Path.Combine(fakeCodexHome, "sessions", "session-fixture", "rollout.jsonl");
        await File.WriteAllTextAsync(authPath, "AUTH_CANARY_DO_NOT_READ_2030");
        await File.WriteAllTextAsync(sessionPath, "SESSION_CANARY_DO_NOT_READ_2030");

        var originalCodexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        try
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", fakeCodexHome);
            await using var authLock = new FileStream(authPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            await using var sessionLock = new FileStream(sessionPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var parser = new DoctorJsonParser(new PrivacyRedactor());
            var result = await RunDoctorModeAsync(GetTestAppHost(), parser, "exit1", TimeSpan.FromSeconds(2));
            Equal(DoctorState.Failed, result.State, "Locked private-state fake doctor result");
            var publicText = result.PublicSummary + "\n" + string.Join('\n', result.Checks.Select(check => check.Summary));
            NotContains(publicText, "AUTH_CANARY_DO_NOT_READ_2030", "auth canary boundary");
            NotContains(publicText, "SESSION_CANARY_DO_NOT_READ_2030", "session canary boundary");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", originalCodexHome);
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static async Task<DoctorResult> RunDoctorModeAsync(
        string executable,
        DoctorJsonParser parser,
        string mode,
        TimeSpan timeout)
    {
        var previous = Environment.GetEnvironmentVariable(FakeDoctorModeVariable);
        try
        {
            Environment.SetEnvironmentVariable(FakeDoctorModeVariable, mode);
            return await new DoctorRunner(parser, executable, timeout).RunAsync(CancellationToken.None)
                .ConfigureAwait(false);
        }
        finally
        {
            Environment.SetEnvironmentVariable(FakeDoctorModeVariable, previous);
        }
    }

    private static async Task<int> RunFakeNativeDoctorAsync()
    {
        switch (Environment.GetEnvironmentVariable(FakeDoctorModeVariable))
        {
            case "unsupported":
                await Console.Error.WriteLineAsync("error: unexpected argument '--json' found");
                return 2;
            case "timeout":
                await Task.Delay(TimeSpan.FromMilliseconds(1500));
                await Console.Out.WriteAsync(DoctorJson("ok"));
                return 0;
            case "malformed":
                await Console.Out.WriteAsync("}{not-json RAW_MALFORMED_FIXTURE_CANARY");
                return 0;
            case "exit1":
                await Console.Out.WriteAsync(DoctorJson("fail", "auth", "fail", "Authentication failed in fixture"));
                return 1;
            default:
                await Console.Out.WriteAsync(DoctorJson("ok"));
                return 0;
        }
    }

    private static string DoctorJson(
        string overallStatus,
        string? checkId = null,
        string? checkStatus = null,
        string? checkSummary = null)
    {
        var checks = checkId is null
            ? "{}"
            : JsonSerializer.Serialize(new Dictionary<string, object>
            {
                [checkId] = new
                {
                    id = checkId,
                    status = checkStatus,
                    summary = checkSummary
                }
            });
        return $$"""
            {"schemaVersion":1,"overallStatus":"{{overallStatus}}","codexVersion":"0.99.7-fixture","checks":{{checks}}}
            """;
    }

    private static SystemFacts FixtureSystem(CodexSurface surface) =>
        new("Windows Fixture Edition 24H2", "x64", surface, "0.99.7-fixture", true, false, ["<LOCAL_APP_INSTALL>"]);

    private static string GetTestAppHost()
    {
        var assembly = Assembly.GetEntryAssembly()?.Location ?? throw new InvalidOperationException("No entry assembly.");
        var appHost = Path.ChangeExtension(assembly, ".exe");
        Assert(File.Exists(appHost), $"The native test app host does not exist: {appHost}");
        return appHost;
    }

    private static string FindRepositoryRoot()
    {
        var cursor = new DirectoryInfo(Environment.CurrentDirectory);
        for (var depth = 0; cursor is not null && depth < 10; depth++, cursor = cursor.Parent)
        {
            if (File.Exists(Path.Combine(cursor.FullName, "src", "CodexSOS.Core", "CodexSOS.Core.csproj")) &&
                Directory.Exists(Path.Combine(cursor.FullName, "tests", "fixtures")))
            {
                return cursor.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find the Codex SOS repository root.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Equal<T>(T expected, T actual, string context)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{context}: expected '{expected}', got '{actual}'.");
        }
    }

    private static void Contains(string actual, string expected, string context)
    {
        if (!actual.Contains(expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{context}: expected text was not found: {expected}");
        }
    }

    private static void NotContains(string actual, string forbidden, string context)
    {
        if (actual.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{context}: forbidden text was present: {forbidden}");
        }
    }

    private sealed class ThrowingDoctor : IDoctorRunner
    {
        public Task<DoctorResult> RunAsync(CancellationToken cancellationToken) =>
            Task.FromException<DoctorResult>(new IOException("fictional doctor failure"));
    }

    private sealed class ThrowingSystem : ISystemCollector
    {
        public Task<SystemFacts> CollectAsync(CancellationToken cancellationToken) =>
            Task.FromException<SystemFacts>(new IOException("fictional system failure"));
    }

    private sealed class ThrowingEvents : IFaultEventCollector
    {
        public Task<IReadOnlyList<FaultEvent>> CollectAsync(DateTimeOffset since, CancellationToken cancellationToken) =>
            Task.FromException<IReadOnlyList<FaultEvent>>(new IOException("fictional event failure"));
    }

    private sealed class ThrowingIssues : IIssueSearchClient
    {
        public Task<IssueSearchResult> SearchAsync(
            IReadOnlyList<string> stableTerms,
            CancellationToken cancellationToken) =>
            Task.FromException<IssueSearchResult>(new IOException("fictional network failure"));
    }

    private sealed class ThrowingStatus : IServiceStatusClient
    {
        public Task<ServiceStatusResult> GetAsync(CancellationToken cancellationToken) =>
            Task.FromException<ServiceStatusResult>(new IOException("fictional status failure"));
    }

    private sealed class CapturingIssues : IIssueSearchClient
    {
        public IReadOnlyList<string> LastTerms { get; private set; } = [];
        public int CallCount { get; private set; }

        public Task<IssueSearchResult> SearchAsync(
            IReadOnlyList<string> stableTerms,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastTerms = stableTerms.ToArray();
            return Task.FromResult(new IssueSearchResult([], IssueSearchState.Completed));
        }
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"items\":[]}")
            });
        }
    }

    private sealed class FixtureDoctor : IDoctorRunner
    {
        public Task<DoctorResult> RunAsync(CancellationToken cancellationToken) =>
            Task.FromResult(DoctorResult.Unavailable("fixture doctor unavailable"));
    }

    private sealed class FixtureSystemCollector : ISystemCollector
    {
        public Task<SystemFacts> CollectAsync(CancellationToken cancellationToken) =>
            Task.FromResult(FixtureSystem(CodexSurface.Desktop));
    }

    private sealed class FixtureEvents : IFaultEventCollector
    {
        public Task<IReadOnlyList<FaultEvent>> CollectAsync(DateTimeOffset since, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FaultEvent>>([]);
    }

    private sealed class FixtureStatus : IServiceStatusClient
    {
        public Task<ServiceStatusResult> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ServiceStatusResult.Unavailable());
    }
}
