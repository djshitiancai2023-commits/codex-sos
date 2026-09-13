using System.IO;
using System.Text.Json;

namespace CodexSOS.App;

/// <summary>
/// Stores only the user's Codex SOS display-language choice. No screenshot,
/// description, account, history, log, or external path is written here.
/// </summary>
public static class LanguageSettings
{
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CodexSOS",
        "settings.json");

    public static UiLanguage Load(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("language", out var value) ||
                value.ValueKind != JsonValueKind.String ||
                !Enum.TryParse<UiLanguage>(value.GetString(), ignoreCase: true, out var language) ||
                !Enum.IsDefined(language))
            {
                return UiLanguage.SimplifiedChinese;
            }

            return language;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or InvalidOperationException or ArgumentException)
        {
            return UiLanguage.SimplifiedChinese;
        }
    }

    public static bool TrySave(string path, UiLanguage language)
    {
        if (!Enum.IsDefined(language)) return false;
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory)) return false;
            Directory.CreateDirectory(directory);
            var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            var json = JsonSerializer.Serialize(new { language = language.ToString() });
            File.WriteAllText(temporary, json + Environment.NewLine);
            File.Move(temporary, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or InvalidOperationException or ArgumentException)
        {
            return false;
        }
    }
}
