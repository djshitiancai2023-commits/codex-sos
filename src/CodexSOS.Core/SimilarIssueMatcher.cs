using System.Text.RegularExpressions;

namespace CodexSOS.Core;

public sealed class SimilarIssueMatcher
{
    private static readonly string[] DistinctivePhrases =
    [
        "stream disconnected before completion",
        "idle timeout waiting for websocket",
        "failed to resume task",
        "absolutepathbuf deserialized without a base path",
        "windows sandbox failed",
        "spawn setup refresh",
        "c0000409",
        "c06d007f",
        "kernelbase.dll"
    ];

    public SimilarIssueSummary Match(
        string evidenceText,
        IncidentCategory category,
        CodexSurface surface,
        IReadOnlyList<PublicIssue> issues,
        bool searchSucceeded,
        string? fallbackUrl)
        => Match(evidenceText, category, surface, issues,
            searchSucceeded ? IssueSearchState.Completed : IssueSearchState.Unavailable,
            fallbackUrl);

    public SimilarIssueSummary Match(
        string evidenceText,
        IncidentCategory category,
        CodexSurface surface,
        IReadOnlyList<PublicIssue> issues,
        IssueSearchState searchState,
        string? fallbackUrl)
        => Match(evidenceText, [category], surface, issues, searchState, fallbackUrl);

    public SimilarIssueSummary Match(
        string evidenceText,
        IReadOnlyList<IncidentCategory> categories,
        CodexSurface surface,
        IReadOnlyList<PublicIssue> issues,
        IssueSearchState searchState,
        string? fallbackUrl)
    {
        var normalizedEvidence = Normalize(evidenceText);
        var matches = issues
            .Select(issue => Score(issue, normalizedEvidence, categories, surface))
            .OrderByDescending(match => match.Score)
            .ThenByDescending(match => match.Issue.Number)
            .Take(5)
            .ToArray();

        var meaningful = matches.Where(m => m.Tier != IssueSimilarityTier.Coincidental).ToArray();
        var high = meaningful.Count(m => m.Tier == IssueSimilarityTier.High);
        var openHigh = meaningful.Count(m => m.Tier == IssueSimilarityTier.High &&
            string.Equals(m.Issue.State, "open", StringComparison.OrdinalIgnoreCase));

        string summary;
        if (searchState == IssueSearchState.NoUsableTerms)
        {
            summary = "暂时没有足够稳定的错误文字，先不乱猜。";
        }
        else if (searchState == IssueSearchState.Unavailable)
        {
            summary = "现在没能连上公开问题列表。检查仍已完成，你可以稍后再试，或打开准备好的搜索入口。";
        }
        else if (high > 0)
        {
            summary = $"找到了 {high} 个高度相似的公开问题，其中 {openHigh} 个仍未关闭。";
        }
        else if (meaningful.Length > 0)
        {
            summary = $"找到了 {meaningful.Length} 个可能相关的公开问题，但证据还不足以说是同一个问题。";
        }
        else
        {
            summary = "暂未找到足够相似的问题。";
        }

        return new SimilarIssueSummary(
            meaningful,
            searchState == IssueSearchState.Completed,
            summary,
            searchState == IssueSearchState.Unavailable ? fallbackUrl : null,
            searchState);
    }

    private static MatchedIssue Score(
        PublicIssue issue,
        string evidence,
        IReadOnlyList<IncidentCategory> categories,
        CodexSurface surface)
    {
        var issueText = Normalize(issue.Title + "\n" + issue.Body);
        var score = 0;
        var reasons = new List<string>();

        var exactPhrases = DistinctivePhrases
            .Where(phrase => evidence.Contains(phrase, StringComparison.Ordinal) &&
                             issueText.Contains(phrase, StringComparison.Ordinal))
            .ToArray();
        if (exactPhrases.Length > 0)
        {
            score += 45;
            reasons.Add($"相同的固定错误短语：{exactPhrases[0]}");
        }

        if (CategoryMatches(issueText, categories))
        {
            score += 20;
            reasons.Add("故障类型一致");
        }

        if ((surface == CodexSurface.Desktop && ContainsAny(issueText, "desktop", "windows app", "codex app")) ||
            (surface == CodexSurface.Cli && ContainsAny(issueText, "cli", "terminal", "powershell")))
        {
            score += 10;
            reasons.Add("使用方式一致");
        }

        var evidenceWords = Tokenize(evidence);
        var issueWords = Tokenize(issueText);
        var overlap = evidenceWords.Intersect(issueWords, StringComparer.OrdinalIgnoreCase).Take(6).ToArray();
        if (overlap.Length >= 3)
        {
            score += Math.Min(15, overlap.Length * 3);
            reasons.Add($"多个症状词一致：{string.Join("、", overlap.Take(3))}");
        }

        if (ContainsAny(issueText, "windows", "win11", "win 11"))
        {
            score += 10;
            reasons.Add("同为 Windows");
        }

        var exactEvidence = exactPhrases.Length > 0;
        var tier = score >= 75 && exactEvidence && CategoryMatches(issueText, categories)
            ? IssueSimilarityTier.High
            : score >= 45 && reasons.Count >= 2
                ? IssueSimilarityTier.Possible
                : IssueSimilarityTier.Coincidental;

        return new MatchedIssue(issue, tier, Math.Clamp(score, 0, 100), reasons);
    }

    private static bool CategoryMatches(string text, IReadOnlyList<IncidentCategory> categories) =>
        categories.Any(category => category switch
        {
            IncidentCategory.Login => ContainsAny(text, "login", "auth", "authentication", "sign in"),
            IncidentCategory.Connection => ContainsAny(text, "disconnect", "websocket", "connection", "reconnecting", "timeout"),
            IncidentCategory.InstallationOrVersion => ContainsAny(text, "install", "version", "update", "upgrade", "not recognized", "command not found"),
            IncidentCategory.DuplicateInstallation => ContainsAny(text, "multiple install", "duplicate install", "two versions"),
            IncidentCategory.DesktopApplication => ContainsAny(text, "desktop", "freeze", "crash", "kernelbase", "c0000409", "c06d007f", "appcrash", "cpu"),
            IncidentCategory.CodexService => ContainsAny(text, "service", "outage", "backend", "server"),
            IncidentCategory.TaskRecovery => ContainsAny(text, "resume", "recover", "interrupted", "task", "thread"),
            IncidentCategory.DoctorFailure => ContainsAny(text, "doctor", "health check"),
            _ => false
        });

    private static HashSet<string> Tokenize(string text) => Regex.Matches(text, @"\b[a-z][a-z0-9_.-]{4,}\b")
        .Select(match => match.Value)
        .Where(word => word is not ("codex" or "error" or "issue" or "failed" or "windows"))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string Normalize(string value) => Regex.Replace(value.ToLowerInvariant(), @"\s+", " ").Trim();

    private static bool ContainsAny(string text, params string[] needles) =>
        needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
}
