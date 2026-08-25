using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CodexSOS.App.Infrastructure;
using CodexSOS.Core;

namespace CodexSOS.App.Services;

public sealed class WindowsFaultEventCollector : IFaultEventCollector
{
    public async Task<IReadOnlyList<FaultEvent>> CollectAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken)
    {
        var elapsed = DateTimeOffset.UtcNow - since.ToUniversalTime();
        var milliseconds = Math.Clamp((long)elapsed.TotalMilliseconds, 60_000L, 3_600_000L);
        var query = $"*[System[(EventID=1000 or EventID=1001 or EventID=1002) and TimeCreated[timediff(@SystemTime) <= {milliseconds}]]]";
        var run = await BoundedProcess.RunAsync(
            Path.Combine(Environment.SystemDirectory, "wevtutil.exe"),
            ["qe", "Application", $"/q:{query}", "/rd:true", "/c:40", "/f:xml"],
            TimeSpan.FromSeconds(5),
            500_000,
            cancellationToken).ConfigureAwait(false);

        if (!run.Started || run.TimedOut || run.ExitCode != 0 || string.IsNullOrWhiteSpace(run.StandardOutput))
        {
            return [];
        }

        return WindowsFaultEventParser.Parse(run.StandardOutput);
    }
}

/// <summary>
/// Parses only the small, structured part of Windows Application events that
/// is useful for Codex SOS. Keeping this pure makes the privacy and false-
/// positive boundaries testable with fictional XML instead of real event logs.
/// </summary>
public static class WindowsFaultEventParser
{
    private const string EventNamespace = "http://schemas.microsoft.com/win/2004/08/events/event";
    private sealed record EventValue(string Name, string Value);

    public static IReadOnlyList<FaultEvent> Parse(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return [];
        }

        try
        {
            var withoutDeclarations = Regex.Replace(xml, @"<\?xml[^>]*\?>", string.Empty,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var document = XDocument.Parse($"<Events>{withoutDeclarations}</Events>", LoadOptions.None);
            XNamespace ns = EventNamespace;
            var events = new List<FaultEvent>();

            foreach (var element in document.Root?.Elements(ns + "Event") ?? [])
            {
                var eventIdText = element.Descendants(ns + "EventID").FirstOrDefault()?.Value;
                if (!int.TryParse(eventIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var eventId) ||
                    eventId is not (1000 or 1001 or 1002))
                {
                    continue;
                }

                var values = element.Descendants(ns + "Data")
                    .Select(data => new EventValue(
                        data.Attribute("Name")?.Value ?? string.Empty,
                        data.Value.Trim()))
                    .Where(value => !string.IsNullOrWhiteSpace(value.Value))
                    .ToArray();

                string? First(params string[] names) => values.FirstOrDefault(value =>
                    names.Contains(value.Name, StringComparer.OrdinalIgnoreCase))?.Value;

                var eventName = First("EventName") ?? string.Empty;
                if (eventId == 1001 && !IsCrashOrHangReport(eventName))
                {
                    // RADAR_PRE_LEAK_64 and similar resource warnings are not
                    // application crashes and must never create crash evidence.
                    continue;
                }

                var applicationValue = First(
                    "AppName", "FaultingApplicationName", "ApplicationName", "P1") ?? string.Empty;
                var packageValue = First("PackageFullName", "ApplicationPackageFullName", "PackageName") ?? string.Empty;
                if (!IsOfficialCodexApplication(applicationValue, packageValue, values))
                {
                    continue;
                }

                var timestampText = element.Descendants(ns + "TimeCreated")
                    .FirstOrDefault()?.Attribute("SystemTime")?.Value;
                if (!DateTimeOffset.TryParse(timestampText, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp))
                {
                    continue;
                }

                var application = FileNameOnly(applicationValue);
                if (string.IsNullOrWhiteSpace(application))
                {
                    application = "Codex";
                }

                var module = First("ModuleName", "FaultingModuleName", "P4");
                module = string.IsNullOrWhiteSpace(module) ? null : FileNameOnly(module);
                var exception = First("ExceptionCode", "ExceptionCodeString", "P7", "P6");
                if (!string.IsNullOrWhiteSpace(exception) && exception.Length > 32)
                {
                    exception = exception[..32];
                }

                events.Add(new FaultEvent(timestamp, application, module, exception));
            }

            return events.OrderByDescending(item => item.Timestamp).Take(8).ToArray();
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or InvalidOperationException or ArgumentException)
        {
            return [];
        }
    }

    private static bool IsCrashOrHangReport(string eventName) =>
        eventName.Contains("APPCRASH", StringComparison.OrdinalIgnoreCase) ||
        eventName.Contains("APPHANG", StringComparison.OrdinalIgnoreCase);

    private static bool IsOfficialCodexApplication(
        string application,
        string package,
        IReadOnlyList<EventValue> values)
    {
        var appName = FileNameOnly(application);
        if (appName.Contains("codex", StringComparison.OrdinalIgnoreCase) ||
            package.Contains("OpenAI.Codex", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!appName.Equals("ChatGPT.exe", StringComparison.OrdinalIgnoreCase) &&
            !appName.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Older official Codex desktop builds have appeared as ChatGPT.exe.
        // Require the package identity too, so a browser/ordinary ChatGPT
        // record cannot be mistaken for a Codex crash.
        return package.Contains("OpenAI.Codex", StringComparison.OrdinalIgnoreCase) ||
               values.Any(value => value.Value.Contains("OpenAI.Codex", StringComparison.OrdinalIgnoreCase));
    }

    private static string FileNameOnly(string value)
    {
        var trimmed = value.Trim();
        var separator = Math.Max(trimmed.LastIndexOf('\\'), trimmed.LastIndexOf('/'));
        return separator >= 0 && separator + 1 < trimmed.Length
            ? trimmed[(separator + 1)..]
            : trimmed;
    }
}
