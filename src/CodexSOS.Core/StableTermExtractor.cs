using System.Text.RegularExpressions;

namespace CodexSOS.Core;

public sealed class StableTermExtractor
{
    private static readonly string[] FixedPhrases =
    [
        "stream disconnected before completion",
        "idle timeout waiting for websocket",
        "failed to resume task",
        "absolutepathbuf deserialized without a base path",
        "windows sandbox failed",
        "spawn setup refresh",
        "reconnecting",
        "c0000409",
        "c06d007f",
        "kernelbase.dll",
        "feedback upload failed",
        "unexpected argument '--json'",
        "authentication failed",
        "login required",
        "not responding",
        "exited unexpectedly"
    ];

    private static readonly HashSet<string> EnglishStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "from", "this", "that", "was", "are", "you", "your",
        "codex", "error", "failed", "issue", "problem", "please", "before", "after",
        "windows", "window", "desktop", "screen", "crash", "crashes", "working", "sign",
        "settings", "setting", "version", "update", "login", "general", "about",
        "account", "help", "preferences", "preference", "profile", "channel",
        "stable", "menu", "options"
    };

    private static readonly (string Phrase, string Target)[] ChineseTermMap =
    [
        ("连接中断", "stream disconnected"),
        ("突然断开", "stream disconnected"),
        ("无法恢复", "failed to resume task"),
        ("恢复不了", "failed to resume task"),
        ("续不上", "failed to resume task"),
        ("任务恢复", "failed to resume task"),
        ("一直转圈", "not responding"),
        ("卡死", "not responding"),
        ("卡住", "not responding"),
        ("卡在那里", "not responding"),
        ("没反应", "not responding"),
        ("闪退", "exited unexpectedly"),
        ("自己关掉", "exited unexpectedly"),
        ("突然退出", "exited unexpectedly"),
        ("自动退出", "exited unexpectedly"),
        ("反复退出", "exited unexpectedly"),
        ("窗口没了", "exited unexpectedly"),
        ("反馈上传失败", "feedback upload failed"),
        ("反馈传不上去", "feedback upload failed"),
        ("登录失败", "authentication failed")
    ];

    private readonly PrivacyRedactor _redactor;

    public StableTermExtractor(PrivacyRedactor redactor) => _redactor = redactor;

    /// <summary>
    /// Finds only fixed, allow-listed diagnostic signals before privacy redaction.
    /// No raw context is returned; callers may use the returned constants for
    /// classification/search while keeping the original evidence on the public
    /// redacted path.
    /// </summary>
    public IReadOnlyList<string> ExtractSafeSignals(params string?[] sources)
    {
        var raw = string.Join('\n', sources.Where(s => !string.IsNullOrWhiteSpace(s)));
        var signals = new List<string>();

        foreach (var phrase in FixedPhrases)
        {
            if (raw.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                signals.Add(phrase);
            }
        }

        foreach (var (phrase, target) in ChineseTermMap)
        {
            if (raw.Contains(phrase, StringComparison.Ordinal))
            {
                signals.Add(target);
            }
        }

        return signals.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IReadOnlyList<string> Extract(params string?[] sources)
    {
        var text = _redactor.Redact(string.Join('\n', sources.Where(s => !string.IsNullOrWhiteSpace(s))))
            .SanitizedText
            .ToLowerInvariant();

        text = Regex.Replace(text, @"\b\d{1,4}[-/:]\d{1,2}(?:[-/:]\d{1,4})?(?:[ t]\d{1,2}:\d{2}(?::\d{2})?)?\b", " ");
        text = Regex.Replace(text, @"(?<![a-z0-9])v?\d+(?:\.\d+)+(?:-[a-z0-9][a-z0-9.-]*)?(?![a-z0-9])", " ");
        text = Regex.Replace(text, @"<[A-Z_]+>", " ", RegexOptions.IgnoreCase);

        var fixedPhrase = FixedPhrases.FirstOrDefault(phrase =>
            text.Contains(phrase, StringComparison.OrdinalIgnoreCase));
        if (fixedPhrase is not null)
        {
            // A fixed phrase is more reliable than incidental words.  Keep
            // exactly one so the public search stays narrow and explainable.
            return [fixedPhrase];
        }

        var mappedTarget = ChineseTermMap
            .Where(pair => text.Contains(pair.Phrase, StringComparison.Ordinal))
            .Select(pair => pair.Target)
            .FirstOrDefault();
        if (mappedTarget is not null)
        {
            return [mappedTarget];
        }

        var terms = new List<string>();

        foreach (Match match in Regex.Matches(text, @"\b[a-z][a-z0-9_.-]{3,}\b"))
        {
            var word = match.Value.Trim('.', '-', '_');
            if (word.Length >= 4 && !EnglishStopWords.Contains(word) && !terms.Contains(word, StringComparer.OrdinalIgnoreCase))
            {
                terms.Add(word);
            }
        }

        return terms.Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToArray();
    }
}
