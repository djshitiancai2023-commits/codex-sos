namespace CodexSOS.App;

public static class ScreenshotPastePolicy
{
    public static bool ShouldUseShortcut(
        bool inputPageVisible,
        bool checkRunning,
        bool descriptionFocused,
        bool anotherTextBoxFocused,
        bool clipboardHasImage,
        bool clipboardHasText) =>
        inputPageVisible &&
        !checkRunning &&
        clipboardHasImage &&
        !clipboardHasText &&
        !anotherTextBoxFocused &&
        (descriptionFocused || !anotherTextBoxFocused);
}
