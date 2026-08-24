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

        try
        {
            var withoutDeclarations = Regex.Replace(run.StandardOutput, @"<\?xml[^>]*\?>", string.Empty,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var document = XDocument.Parse($"<Events>{withoutDeclarations}</Events>", LoadOptions.None);
            XNamespace ns = "http://schemas.microsoft.com/win/2004/08/events/event";
            var events = new List<FaultEvent>();
            foreach (var element in document.Root?.Elements(ns + "Event") ?? [])
            {
                var values = element.Descendants(ns + "Data")
                    .Select(data => new
                    {
                        Name = data.Attribute("Name")?.Value ?? string.Empty,
                        Value = data.Value
                    })
                    .ToArray();
                var all = string.Join(' ', values.Select(value => value.Value));
                if (!all.Contains("codex", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var timestampText = element.Descendants(ns + "TimeCreated").FirstOrDefault()?.Attribute("SystemTime")?.Value;
                if (!DateTimeOffset.TryParse(timestampText, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out var timestamp))
                {
                    continue;
                }

                string? First(params string[] names) => values.FirstOrDefault(value =>
                    names.Contains(value.Name, StringComparer.OrdinalIgnoreCase))?.Value;
                var application = First("AppName", "FaultingApplicationName", "ApplicationName") ?? "Codex";
                application = Path.GetFileName(application);
                var module = First("ModuleName", "FaultingModuleName");
                module = string.IsNullOrWhiteSpace(module) ? null : Path.GetFileName(module);
                var exception = First("ExceptionCode", "ExceptionCodeString");
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
}
