using CodexSOS.Core;

namespace CodexSOS.App;

public static class OfficialFeedbackRoutes
{
    public const string DesktopUrl = "https://github.com/openai/codex/issues/new?template=1-codex-app.yml";
    public const string CliUrl = "https://github.com/openai/codex/issues/new?template=3-cli.yml";
    public const string ChooseUrl = "https://github.com/openai/codex/issues/new/choose";

    public static string For(CodexSurface surface) => surface switch
    {
        CodexSurface.Desktop => DesktopUrl,
        CodexSurface.Cli => CliUrl,
        _ => ChooseUrl
    };

    public static bool IsAllowed(string? url) =>
        string.Equals(url, DesktopUrl, StringComparison.Ordinal) ||
        string.Equals(url, CliUrl, StringComparison.Ordinal) ||
        string.Equals(url, ChooseUrl, StringComparison.Ordinal);
}
