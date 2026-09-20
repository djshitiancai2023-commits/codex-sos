using System.IO;

namespace CodexSOS.App;

public static class SavedReportLocator
{
    public static IReadOnlyList<string> SuccessfulPaths(
        ReportSaveResult result,
        string publicPath,
        string privacyPath) =>
        new[]
        {
            result.PublicReportSaved ? publicPath : null,
            result.PrivacyReviewSaved ? privacyPath : null
        }
        .Where(path => path is not null)
        .Select(path => path!)
        .ToArray();

    public static string? FirstExisting(
        IEnumerable<string> paths,
        Func<string, bool>? exists = null)
    {
        exists ??= File.Exists;
        return paths.FirstOrDefault(exists);
    }
}
