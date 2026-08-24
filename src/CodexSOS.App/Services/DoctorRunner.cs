using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using CodexSOS.App.Infrastructure;
using CodexSOS.Core;

namespace CodexSOS.App.Services;

public sealed class DoctorRunner : IDoctorRunner
{
    private static readonly TimeSpan DefaultWallClockLimit = TimeSpan.FromSeconds(25);
    private const int OutputLimit = 1_000_000;
    private readonly DoctorJsonParser _parser;
    private readonly string? _explicitCommand;
    private readonly TimeSpan _wallClockLimit;
    private bool _timedOutInThisApp;

    public DoctorRunner(
        DoctorJsonParser parser,
        string? explicitCommand = null,
        TimeSpan? wallClockLimit = null)
    {
        _parser = parser;
        _explicitCommand = explicitCommand;
        _wallClockLimit = wallClockLimit ?? DefaultWallClockLimit;
    }

    public async Task<DoctorResult> RunAsync(CancellationToken cancellationToken)
    {
        if (_timedOutInThisApp)
        {
            return new DoctorResult(DoctorState.TimedOut, null, [],
                "本次打开 SOS 后，Codex 官方体检已经有一次等待过久。为避免重复运行，其他检查已直接继续。");
        }

        var command = ResolveCommand(_explicitCommand);
        if (command is null)
        {
            return DoctorResult.Unavailable("没有找到可运行的 Codex 官方体检，但其他检查已继续完成。");
        }

        var run = await BoundedProcess.RunAsync(
            command.FileName,
            command.Arguments,
            _wallClockLimit,
            OutputLimit,
            cancellationToken,
            GetNeutralWorkingDirectory(),
            leaveRunningOnTimeout: true).ConfigureAwait(false);

        if (!run.Started)
        {
            return DoctorResult.Unavailable("Codex 官方体检这次无法启动，但其他检查已继续完成。");
        }

        if (run.TimedOut)
        {
            _timedOutInThisApp = true;
            return new DoctorResult(DoctorState.TimedOut, null, [],
                "Codex 官方体检等待时间过长，SOS 已先继续完成其他检查；不会在后台重复启动第二次体检。");
        }

        var error = run.StandardError.ToLowerInvariant();
        if (ContainsAny(error, "unexpected argument '--json'", "unrecognized option", "unknown command", "not supported"))
        {
            return new DoctorResult(DoctorState.Unsupported, null, [],
                "官方体检在这个版本暂不可用，但仍然完成了其他检查。", run.ExitCode);
        }

        // The raw buffers are local variables only. The parser keeps a small allowlist and redacts
        // every displayed field; neither raw stream is persisted or exported.
        return _parser.Parse(run.StandardOutput, run.ExitCode ?? -1);
    }

    private static ResolvedCommand? ResolveCommand(string? explicitCommand)
    {
        if (!string.IsNullOrWhiteSpace(explicitCommand))
        {
            return ForPath(explicitCommand);
        }

        var directories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var extension in new[] { ".exe", string.Empty })
        {
            foreach (var directory in directories)
            {
                try
                {
                    var candidate = Path.Combine(directory.Trim('"'), "codex" + extension);
                    if (File.Exists(candidate))
                    {
                        var resolved = ForPath(candidate);
                        if (IsTrustedCodexExecutable(resolved.FileName))
                        {
                            return resolved;
                        }
                    }
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    // Ignore malformed PATH entries.
                }
            }
        }

        return null;
    }

    private static ResolvedCommand ForPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath) ||
            string.Equals(Path.GetExtension(fullPath), ".cmd", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetExtension(fullPath), ".bat", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetExtension(fullPath), ".ps1", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only a native Codex executable can run the official diagnostic.");
        }

        return new ResolvedCommand(fullPath, ["doctor", "--json"]);
    }

    private static string GetNeutralWorkingDirectory()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var directory = Path.Combine(root, "CodexSOS", "NeutralWork");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static bool IsTrustedCodexExecutable(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var normalized = fullPath.Replace('/', '\\');
        if (normalized.Contains("\\WindowsApps\\OpenAI.Codex_", StringComparison.OrdinalIgnoreCase))
        {
            return HasOpenAiCertificate(fullPath);
        }

        // The official npm package installs the native binary below a vendor directory. Reject a
        // same-name executable placed directly on PATH or next to an unrelated script.
        if (normalized.Contains("\\node_modules\\@openai\\codex\\vendor\\", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool HasOpenAiCertificate(string path)
    {
        try
        {
            using var certificate = X509CertificateLoader.LoadCertificateFromFile(path);
            return certificate.Subject.Contains("OpenAI", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool ContainsAny(string text, params string[] values) =>
        values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));

    private sealed record ResolvedCommand(string FileName, IReadOnlyList<string> Arguments);
}
