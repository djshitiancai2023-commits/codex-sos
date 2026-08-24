using System.Text.RegularExpressions;

namespace CodexSOS.Core;

public sealed class PrivacyRedactor
{
    private sealed record Rule(string Kind, string Replacement, Regex Pattern);

    private static readonly IReadOnlyList<Rule> Rules =
    [
        New("secret", "<SECRET>", @"(?i)\b(?:bearer\s+|password\s*[:=]\s*|api[_-]?key\s*[:=]\s*|token\s*[:=]\s*)[^\s,;]+"),
        New("private_git_remote", "<PRIVATE_GIT_REMOTE>", @"(?i)(?!(?:git@github\.com:openai/codex\.git|ssh://git@github\.com/openai/codex\.git|https?://github\.com/openai/codex(?:\.git)?)(?:\s|$))(?:(?:git@[a-z0-9.-]+:[^\s]+|ssh://git@[a-z0-9.-]+(?::\d+)?/[^\s]+|https?://(?:[^/@\s]+@)?[a-z0-9.-]+/[^\s]+)\.git)\b"),
        New("email", "<EMAIL>", @"(?i)(?<![\w.+-])[\w.+-]+@[a-z0-9.-]+\.[a-z]{2,}(?![\w.-])"),
        New("local_path", "<LOCAL_PATH>", @"(?i)(?<![\w])(?:[a-z]:[\\/]|\\\\|/(?:mnt/[a-z]|home|workspace)(?=[/\\]))[^\r\n\""'<>|?*]+"),
        New("internal_url", "<INTERNAL_URL>", @"(?i)https?://(?:localhost|127\.0\.0\.1|[^/\s.]*(?:corp|internal|intranet)[^/\s.]*|[^/\s]+\.(?:local|internal))(?:[:/]\S*)?"),
        New("private_ip", "<PRIVATE_IP>", @"(?<!\d)(?:10(?:\.\d{1,3}){3}|127(?:\.\d{1,3}){3}|192\.168(?:\.\d{1,3}){2}|172\.(?:1[6-9]|2\d|3[01])(?:\.\d{1,3}){2})(?!\d)"),
        New("session_id", "<SESSION_ID>", @"(?i)\b(?:session|thread|conversation)[-_ ]?(?:id)?\s*[:=#/]?\s*[a-z0-9][a-z0-9_-]{7,}\b"),
        New("request_id", "<REQUEST_ID>", @"(?i)\b(?:request|req)[-_ ]?(?:id)?\s*[:=#/]?\s*[a-z0-9][a-z0-9_-]{7,}\b"),
        New("account_id", "<ACCOUNT_ID>", @"(?i)\b(?:account|acct|user)[-_ ]?(?:id)?\s*[:=#/]?\s*[a-z0-9][a-z0-9_-]{7,}\b"),
        New("known_private_term", "<PRIVATE_TERM>", @"(?im)^\s*(?:name|full name|company|client|customer|internal product|姓名|名字|公司|客户|内部产品)\s*[:=：]\s*[^\r\n]{2,160}"),
        New("known_private_term", "<PRIVATE_TERM>", @"(?i)\bproject[-_ ][a-z0-9][a-z0-9._-]{2,}\b"),
    ];

    private readonly IReadOnlyList<string> _defaultKnownPrivateTerms;

    public PrivacyRedactor(IEnumerable<string>? defaultKnownPrivateTerms = null)
    {
        _defaultKnownPrivateTerms = NormalizeKnownTerms(defaultKnownPrivateTerms);
    }

    private static Rule New(string kind, string replacement, string pattern) =>
        new(kind, replacement, new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant));

    public PrivacyResult Redact(string? input, IEnumerable<string>? knownPrivateTerms = null)
    {
        var sanitized = input ?? string.Empty;
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in Rules)
        {
            sanitized = rule.Pattern.Replace(sanitized, match =>
            {
                counts[rule.Kind] = counts.GetValueOrDefault(rule.Kind) + 1;
                return rule.Replacement;
            });
        }

        sanitized = RedactLongRandomStrings(sanitized, counts);
        var combinedKnownTerms = _defaultKnownPrivateTerms
            .Concat(NormalizeKnownTerms(knownPrivateTerms))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(term => term.Length)
            .ToArray();
        foreach (var term in combinedKnownTerms)
        {
            var pattern = new Regex(
                $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(term)}(?![\p{{L}}\p{{N}}])",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            sanitized = pattern.Replace(sanitized, _ =>
            {
                counts["known_private_term"] = counts.GetValueOrDefault("known_private_term") + 1;
                return "<PRIVATE_TERM>";
            });
        }

        var findings = counts
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new PrivacyFinding(pair.Key, pair.Value))
            .ToArray();

        return new PrivacyResult(sanitized, findings, findings.Length > 0);
    }

    public string RedactPath(string? path) => Redact(path).SanitizedText;

    private static IReadOnlyList<string> NormalizeKnownTerms(IEnumerable<string>? terms) =>
        terms?
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Select(term => term.Trim())
            .Where(term => term.Length >= 3 && term.Length <= 160)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

    private static string RedactLongRandomStrings(string input, IDictionary<string, int> counts)
    {
        var candidate = new Regex(@"(?<![A-Za-z0-9])[A-Za-z0-9_\-]{28,}(?![A-Za-z0-9])",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        return candidate.Replace(input, match =>
        {
            var value = match.Value;
            if (!value.Any(char.IsLetter) || !value.Any(char.IsDigit) ||
                value.All(c => char.IsDigit(c) || c == '-'))
            {
                return value;
            }

            counts.TryGetValue("possible_secret", out var current);
            counts["possible_secret"] = current + 1;
            return "<POSSIBLE_SECRET>";
        });
    }
}
