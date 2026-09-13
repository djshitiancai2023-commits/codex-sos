using System.IO;
using System.Text;

namespace CodexSOS.App;

public sealed record ReportSaveResult(
    bool PublicReportSaved,
    bool PrivacyReviewSaved,
    string? PublicError,
    string? PrivacyError)
{
    public bool BothSaved => PublicReportSaved && PrivacyReviewSaved;
}

public static class ReportFileWriter
{
    public static ReportSaveResult TrySave(
        string publicPath,
        string publicText,
        string privacyPath,
        string privacyText,
        Action<string, string>? write = null)
    {
        if (write is null)
        {
            write = static (path, text) => File.WriteAllText(path, text, new UTF8Encoding(false));
        }
        var publicSaved = false;
        var privacySaved = false;
        string? publicError = null;
        string? privacyError = null;
        try
        {
            write(publicPath, publicText);
            publicSaved = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            publicError = ex.GetType().Name;
        }

        try
        {
            write(privacyPath, privacyText);
            privacySaved = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            privacyError = ex.GetType().Name;
        }

        return new ReportSaveResult(publicSaved, privacySaved, publicError, privacyError);
    }
}
