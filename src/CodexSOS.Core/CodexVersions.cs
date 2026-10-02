namespace CodexSOS.Core;

/// <summary>Doctor reports the CLI build version, never the desktop app version.</summary>
public static class CodexVersions
{
    public static string? ForSurface(SystemFacts system, DoctorResult doctor) =>
        system.Surface == CodexSurface.Cli
            ? doctor.CodexVersion ?? system.CodexVersion
            : system.CodexVersion;

    public static (string? Version, bool PossibleDuplicate) ResolveInstallations(
        CodexSurface surface, IEnumerable<(string Root, string? Version, CodexSurface Surface)> candidates)
    {
        var relevant = candidates.Where(item => item.Surface == surface &&
            !string.IsNullOrWhiteSpace(item.Version) && item.Version != "0.0.0.0").ToArray();
        var versions = relevant.Select(item => item.Version).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var roots = relevant.Select(item => item.Root).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        return (versions.Length == 1 ? versions[0] : null, roots > 1 && versions.Length > 1);
    }
}
