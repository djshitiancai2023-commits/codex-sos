using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using CodexSOS.Core;

namespace CodexSOS.App.Services;

public sealed class OpenAIStatusClient : IServiceStatusClient
{
    private readonly HttpClient _http;

    public OpenAIStatusClient(HttpClient http) => _http = http;

    public async Task<ServiceStatusResult> GetAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://status.openai.com/api/v2/summary.json");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Codex-SOS", "0.1"));
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return ServiceStatusResult.Unavailable();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions { MaxDepth = 32 }, cancellationToken)
            .ConfigureAwait(false);
        var root = document.RootElement;
        if (root.TryGetProperty("components", out var components) && components.ValueKind == JsonValueKind.Array)
        {
            var codex = components.EnumerateArray()
                .Select(component => new
                {
                    Name = GetString(component, "name") ?? string.Empty,
                    Status = GetString(component, "status") ?? "unknown"
                })
                .Where(component => component.Name.Contains("codex", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (codex.Length > 0)
            {
                var worst = codex.OrderByDescending(component => Severity(component.Status)).First();
                return new ServiceStatusResult(true, true, worst.Status,
                    $"OpenAI 官方状态页显示 Codex：{PlainStatus(worst.Status)}。", DateTimeOffset.UtcNow);
            }
        }

        var overall = root.TryGetProperty("status", out var status) ? GetString(status, "indicator") : null;
        var description = root.TryGetProperty("status", out status) ? GetString(status, "description") : null;
        return new ServiceStatusResult(true, false, overall ?? "unknown",
            $"OpenAI 官方整体状态：{description ?? PlainStatus(overall ?? "unknown")}。", DateTimeOffset.UtcNow);
    }

    private static int Severity(string status) => status switch
    {
        "major_outage" => 5,
        "partial_outage" => 4,
        "degraded_performance" => 3,
        "under_maintenance" => 2,
        "operational" => 0,
        _ => 1
    };

    private static string PlainStatus(string status) => status switch
    {
        "operational" or "none" => "运行正常",
        "degraded_performance" or "minor" => "部分性能下降",
        "partial_outage" or "major" => "部分服务中断",
        "major_outage" or "critical" => "大范围服务中断",
        "under_maintenance" or "maintenance" => "维护中",
        _ => "状态暂时无法解释"
    };

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
