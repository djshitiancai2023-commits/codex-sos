using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using CodexSOS.Core;

namespace CodexSOS.App.Services;

public sealed class WindowsSystemCollector : ISystemCollector
{
    private static readonly TimeSpan CollectionBudget = TimeSpan.FromSeconds(3);

    public async Task<SystemFacts> CollectAsync(CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(CollectionBudget);
        try
        {
            // Process and PATH inspection is synchronous Windows work. Keep it
            // off the WPF dispatcher and bound the wait so the progress page
            // remains usable even when one item is slow or protected.
            return await Task.Run(() => CollectCore(budget.Token), budget.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return FallbackFacts();
        }
    }

    public static bool IsSafeLocalPath(string? path)
    {
        var value = path?.Trim().Trim('"') ?? string.Empty;
        if (value.Length == 0 || value.StartsWith("\\\\", StringComparison.Ordinal) ||
            value.StartsWith("//", StringComparison.Ordinal) ||
            value.Contains("://", StringComparison.Ordinal) ||
            !Path.IsPathFullyQualified(value))
        {
            return false;
        }

        try
        {
            var root = Path.GetPathRoot(value);
            if (string.IsNullOrWhiteSpace(root)) return false;
            return new DriveInfo(root).DriveType != DriveType.Network;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or System.Security.SecurityException)
        {
            return false;
        }
    }

    private static SystemFacts CollectCore(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var processes = GetCodexProcesses();
        var surface = processes.Any(p => p.HasWindow) ? CodexSurface.Desktop
            : processes.Count > 0 ? CodexSurface.Cli
            : CodexSurface.Unknown;
        var candidates = FindInstallCandidates(processes);
        var roots = candidates
            .Select(candidate => candidate.Root)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var versions = candidates
            .Select(candidate => candidate.Version)
            .Where(version => !string.IsNullOrWhiteSpace(version) && version != "0.0.0.0")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var possibleDuplicate = roots.Length > 1 && versions.Length > 1;
        var hints = candidates
            .Select(candidate => candidate.Kind)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();
        var version = versions.Length == 1 ? versions[0] : null;
        var windows = Environment.OSVersion.VersionString;

        return new SystemFacts(
            windows,
            RuntimeInformation.OSArchitecture.ToString(),
            surface,
            version,
            processes.Count > 0,
            possibleDuplicate,
            hints);
    }

    private static SystemFacts FallbackFacts() =>
        new("暂时无法确定", "暂时无法确定", CodexSurface.Unknown, null, false, false, []);

    private static List<ProcessFact> GetCodexProcesses()
    {
        var results = new List<ProcessFact>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    var name = process.ProcessName;
                    if (!IsOfficialDesktopProcess(name))
                    {
                        continue;
                    }

                    string? path = null;
                    string? version = null;
                    try
                    {
                        path = process.MainModule?.FileName;
                        version = process.MainModule?.FileVersionInfo.ProductVersion;
                    }
                    catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
                    {
                        // Process state is still useful even when Windows protects its executable path.
                    }

                    results.Add(new ProcessFact(process.MainWindowHandle != IntPtr.Zero, path, version));
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // Process exited or access changed during enumeration.
                }
            }
        }

        return results;
    }

    private static bool IsOfficialDesktopProcess(string name)
        => name.Equals("codex", StringComparison.OrdinalIgnoreCase)
            || name.Equals("chatgpt", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<InstallCandidate> FindInstallCandidates(IReadOnlyList<ProcessFact> processes)
    {
        var candidates = new List<InstallCandidate>();
        foreach (var process in processes.Where(process => !string.IsNullOrWhiteSpace(process.Path)))
        {
            if (IsSafeLocalPath(process.Path))
            {
                AddCandidate(candidates, process.Path!, process.Version);
            }
        }

        var directories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var directory in directories)
        {
            if (!IsSafeLocalPath(directory)) continue;
            foreach (var fileName in new[] { "codex.exe", "codex.cmd", "codex" })
            {
                try
                {
                    var path = Path.Combine(directory.Trim('"'), fileName);
                    if (!File.Exists(path)) continue;
                    string? version = null;
                    if (Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        try { version = FileVersionInfo.GetVersionInfo(path).ProductVersion; }
                        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException) { }
                    }

                    AddCandidate(candidates, path, version);
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    // Ignore malformed PATH entries.
                }
            }
        }

        return candidates
            .GroupBy(candidate => $"{candidate.Root}|{candidate.Version}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static void AddCandidate(ICollection<InstallCandidate> candidates, string path, string? version)
    {
        if (!IsSafeLocalPath(path)) return;
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or System.Security.SecurityException)
        {
            // Skip anomalous executable paths instead of failing the whole check.
            return;
        }

        var root = Path.GetDirectoryName(fullPath) ?? fullPath;
        var lower = fullPath.ToLowerInvariant();
        var kind = lower.Contains("windowsapps", StringComparison.Ordinal)
            ? "Windows 应用安装（位置已隐藏）"
            : lower.Contains("npm", StringComparison.Ordinal) || lower.EndsWith("codex.cmd", StringComparison.Ordinal)
                ? "命令行安装（位置已隐藏）"
                : "Codex 安装（位置已隐藏）";
        candidates.Add(new InstallCandidate(root, version, kind));
    }

    private sealed record ProcessFact(bool HasWindow, string? Path, string? Version);
    private sealed record InstallCandidate(string Root, string? Version, string Kind);
}
