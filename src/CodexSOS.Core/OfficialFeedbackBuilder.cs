using System.Text;

namespace CodexSOS.Core;

public sealed class OfficialFeedbackBuilder
{
    private const string OfficialIssuePrefix = "https://github.com/openai/codex/issues/";
    private readonly PrivacyRedactor _redactor;

    public OfficialFeedbackBuilder(PrivacyRedactor redactor) => _redactor = redactor;

    public string Build(DiagnosticReport report)
    {
        var text = new StringBuilder();
        text.AppendLine("# Codex App bug report draft");
        text.AppendLine();
        text.AppendLine("> Prepared locally by the unofficial community tool Codex SOS. Status: NOT SUBMITTED.");
        text.AppendLine("> Review this draft before sharing. The original screenshot is not included.");
        text.AppendLine();
        text.AppendLine($"Suggested title: {SuggestedTitle(report.Diagnosis.Category)}");

        Heading(text, "What version of the Codex App are you using?");
        text.AppendLine(report.Doctor.CodexVersion ?? report.System.CodexVersion ?? "Unknown");

        Heading(text, "What subscription do you have?");
        text.AppendLine("Not collected by Codex SOS. Select your subscription on the official form.");

        Heading(text, "What platform is your computer?");
        text.AppendLine($"{report.System.WindowsVersion} · {report.System.Architecture}");

        Heading(text, "What issue are you seeing?");
        text.AppendLine(string.IsNullOrWhiteSpace(report.PublicEvidence.Description)
            ? "No written description was provided."
            : report.PublicEvidence.Description.Trim());
        if (!string.IsNullOrWhiteSpace(report.PublicEvidence.OcrText))
        {
            text.AppendLine();
            text.AppendLine("Error text recognized locally from the screenshot (the image itself is not attached):");
            text.AppendLine();
            text.AppendLine($"> {report.PublicEvidence.OcrText.Trim().Replace("\n", "\n> ", StringComparison.Ordinal)}");
        }
        text.AppendLine();
        text.AppendLine($"Codex SOS assessment: {report.Diagnosis.PlainSummary}");

        Heading(text, "What steps can reproduce the bug?");
        text.AppendLine("1. Open Codex on the Windows computer described above.");
        text.AppendLine("2. Continue the activity described in ‘What issue are you seeing?’.");
        text.AppendLine("3. Observe the reported behavior.");
        text.AppendLine();
        text.AppendLine("Exact repeatable steps were not captured automatically. Please edit this section if you know them.");
        text.AppendLine($"Approximate check time: {report.PublicEvidence.StartedAt:O}");

        Heading(text, "What is the expected behavior?");
        text.AppendLine(ExpectedBehavior(report.Diagnosis.Category));

        Heading(text, "Additional information");
        text.AppendLine($"- Official Codex check: {report.Doctor.PublicSummary}");
        foreach (var check in report.Doctor.Checks.Where(item => item.Status is "warning" or "fail").Take(12))
        {
            text.AppendLine($"- Doctor finding ({check.Id}): {check.Summary}");
        }
        text.AppendLine($"- Suggested safe next step: {report.Diagnosis.SafeNextStep}");
        foreach (var faultEvent in report.FaultEvents.Take(8))
        {
            text.AppendLine($"- Windows record: {faultEvent.Timestamp:O} · {faultEvent.Application} · " +
                            $"{faultEvent.FaultModule ?? "module unavailable"} · " +
                            $"{faultEvent.ExceptionCode ?? "exception code unavailable"}");
        }
        foreach (var match in report.SimilarIssues.Matches
                     .Where(match => match.Issue.HtmlUrl.StartsWith(OfficialIssuePrefix, StringComparison.Ordinal))
                     .Take(5))
        {
            text.AppendLine($"- Similar public issue: #{match.Issue.Number} {match.Issue.HtmlUrl} " +
                            $"({match.Tier}, score {match.Score})");
        }
        text.AppendLine($"- Codex SOS report ID: {report.RunId.ToString("N")[..8]}");
        text.AppendLine("- Session ID, token limit usage, and context window usage: not collected by Codex SOS. Add them only if you choose to share them.");
        text.AppendLine("- Limits: these clues do not prove a root cause. No match does not mean nobody else has encountered it.");
        text.AppendLine("- Privacy boundary: Codex SOS did not include the original screenshot or read full chats, prompts, project code, auth.json, tokens, or cookies.");

        return _redactor.Redact(text.ToString()).SanitizedText;
    }

    private static string ExpectedBehavior(IncidentCategory category) => category switch
    {
        IncidentCategory.DesktopApplication =>
            "Codex should remain open and the active task should continue unless the user closes the app.",
        IncidentCategory.Connection =>
            "Codex should keep the connection or show a recoverable error without losing the active task.",
        IncidentCategory.TaskRecovery =>
            "Codex should resume the existing task without deleting or losing local work.",
        IncidentCategory.Login =>
            "Codex should complete sign-in or show a clear, recoverable sign-in error.",
        _ => "Codex should complete the requested operation or show a clear, recoverable error without losing local work."
    };

    private static string SuggestedTitle(IncidentCategory category) => category switch
    {
        IncidentCategory.DesktopApplication => "[Windows] Codex App exits unexpectedly",
        IncidentCategory.Connection => "[Windows] Codex App connection is interrupted",
        IncidentCategory.TaskRecovery => "[Windows] Codex App cannot resume an existing task",
        IncidentCategory.Login => "[Windows] Codex App sign-in does not complete",
        _ => "[Windows] Codex App unexpected behavior"
    };

    private static void Heading(StringBuilder text, string heading)
    {
        text.AppendLine();
        text.AppendLine($"## {heading}");
        text.AppendLine();
    }
}
