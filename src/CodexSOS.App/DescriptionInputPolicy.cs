namespace CodexSOS.App;

/// <summary>
/// Keeps the description box honest at the same UTF-16 length boundary used by
/// WPF TextBox.MaxLength, without silently trimming a paste.
/// </summary>
public static class DescriptionInputPolicy
{
    public const int MaximumLength = 1200;
    public const int WarningThreshold = 1080;

    public static int RemainingCapacity(string currentText, int selectionStart, int selectionLength)
    {
        var safeTextLength = currentText?.Length ?? 0;
        var safeStart = Math.Clamp(selectionStart, 0, safeTextLength);
        var safeLength = Math.Clamp(selectionLength, 0, safeTextLength - safeStart);
        return Math.Max(0, MaximumLength - (safeTextLength - safeLength));
    }

    public static bool FitsPaste(string currentText, int selectionStart, int selectionLength, string pastedText)
    {
        if (pastedText is null) return false;
        return pastedText.Length <= RemainingCapacity(currentText, selectionStart, selectionLength);
    }

    public static bool ShouldShowCounter(int length) => length >= WarningThreshold;
}
