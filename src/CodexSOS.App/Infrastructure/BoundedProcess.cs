using System.Diagnostics;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace CodexSOS.App.Infrastructure;

internal sealed record ProcessRunResult(
    bool Started,
    bool TimedOut,
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    Exception? StartError = null);

internal static class BoundedProcess
{
    private static readonly ConcurrentDictionary<int, Task> DetachedProcesses = new();

    public static async Task<ProcessRunResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        TimeSpan timeout,
        int maximumCharacters,
        CancellationToken cancellationToken,
        string? workingDirectory = null,
        bool leaveRunningOnTimeout = false)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                    ? Environment.CurrentDirectory
                    : workingDirectory
            }
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        try
        {
            if (!process.Start())
            {
                process.Dispose();
                return new(false, false, null, string.Empty, string.Empty);
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            process.Dispose();
            return new(false, false, null, string.Empty, string.Empty, ex);
        }

        var outputTask = ReadBoundedAsync(process.StandardOutput, maximumCharacters, cancellationToken);
        var errorTask = ReadBoundedAsync(process.StandardError, maximumCharacters, cancellationToken);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        var timedOut = false;

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            if (leaveRunningOnTimeout)
            {
                TrackUntilNaturalExit(process, outputTask, errorTask);
                return new(true, true, null, string.Empty, string.Empty);
            }

            TryKillTree(process);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            if (leaveRunningOnTimeout)
            {
                TrackUntilNaturalExit(process, outputTask, errorTask);
            }
            else
            {
                TryKillTree(process);
                process.Dispose();
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        // Killing closes the redirected pipes. Use a small independent ceiling so a broken child
        // can never keep the application waiting forever.
        var drain = Task.WhenAll(outputTask, errorTask);
        var completedDrain = await Task.WhenAny(drain, Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None))
            .ConfigureAwait(false);
        var output = completedDrain == drain && outputTask.IsCompletedSuccessfully ? outputTask.Result : string.Empty;
        var error = completedDrain == drain && errorTask.IsCompletedSuccessfully ? errorTask.Result : string.Empty;
        int? exitCode = !timedOut && process.HasExited ? process.ExitCode : null;
        process.Dispose();
        return new(true, timedOut, exitCode, output, error);
    }

    private static void TrackUntilNaturalExit(
        Process process,
        Task<string> outputTask,
        Task<string> errorTask)
    {
        var id = process.Id;
        var task = FinishDetachedAsync(process, outputTask, errorTask);
        DetachedProcesses[id] = task;
        _ = task.ContinueWith(
            completedTask =>
            {
                DetachedProcesses.TryRemove(id, out var ignoredTask);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static async Task FinishDetachedAsync(
        Process process,
        Task<string> outputTask,
        Task<string> errorTask)
    {
        try
        {
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            // A detached official diagnostic may exit while its handles are being observed.
        }
        finally
        {
            process.Dispose();
        }
    }

    private static async Task<string> ReadBoundedAsync(
        StreamReader reader,
        int maximumCharacters,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder(Math.Min(maximumCharacters, 16_384));
        var buffer = new char[4096];
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            var remaining = maximumCharacters - builder.Length;
            if (remaining > 0)
            {
                builder.Append(buffer, 0, Math.Min(remaining, read));
            }
        }

        return builder.ToString();
    }

    private static void TryKillTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // The process may have exited between the check and the kill request.
        }
    }
}
