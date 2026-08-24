using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using CodexSOS.Core;

namespace CodexSOS.App.Services;

public sealed class GitHubIssueSearchClient : IIssueSearchClient
{
    private readonly HttpClient _http;

    public GitHubIssueSearchClient(HttpClient http) => _http = http;

    public async Task<IssueSearchResult> SearchAsync(
        IReadOnlyList<string> stableTerms,
        CancellationToken cancellationToken)
    {
        var terms = stableTerms
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Select(term => term.Length > 80 ? term[..80] : term)
            .Take(4)
            .ToArray();
        var humanQuery = string.Join(' ', terms.Select(QuoteIfNeeded));
        var fallback = "https://github.com/openai/codex/issues?q=" +
                       Uri.EscapeDataString($"is:issue {humanQuery}");
        if (terms.Length == 0)
        {
            return IssueSearchResult.NoUsableTerms();
        }

        var apiQuery = Uri.EscapeDataString($"repo:openai/codex is:issue in:title,body {humanQuery}");
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.github.com/search/issues?q={apiQuery}&sort=updated&order=desc&per_page=20");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Codex-SOS", "0.1"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests || !response.IsSuccessStatusCode)
        {
            return IssueSearchResult.Unavailable(fallback);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions { MaxDepth = 32 }, cancellationToken)
            .ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return IssueSearchResult.Unavailable(fallback);
        }

        var results = new List<PublicIssue>();
        foreach (var item in items.EnumerateArray().Take(20))
        {
            if (item.TryGetProperty("pull_request", out _)) continue;
            var number = GetInt64(item, "number") ?? 0;
            var title = GetString(item, "title") ?? string.Empty;
            var body = GetString(item, "body") ?? string.Empty;
            var url = GetString(item, "html_url") ?? string.Empty;
            var state = GetString(item, "state") ?? "unknown";
            var labels = item.TryGetProperty("labels", out var labelArray) && labelArray.ValueKind == JsonValueKind.Array
                ? labelArray.EnumerateArray()
                    .Select(label => GetString(label, "name"))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name!)
                    .Take(12)
                    .ToArray()
                : [];
            results.Add(new PublicIssue(number, title, body.Length > 20_000 ? body[..20_000] : body,
                url, state, labels));
        }

        return new IssueSearchResult(results, IssueSearchState.Completed, fallback);
    }

    private static string QuoteIfNeeded(string value) => value.Contains(' ') ? $"\"{value.Replace("\"", string.Empty, StringComparison.Ordinal)}\"" : value;
    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static long? GetInt64(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt64(out var number) ? number : null;
}
