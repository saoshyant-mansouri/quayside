using System.Text.Json;
using Quayside.Core;

namespace Quayside.Infrastructure.Web;

public sealed class BraveProvider(string apiKey) : IWebSearchProvider
{
    public const string Endpoint = "https://api.search.brave.com/res/v1/web/search";
    public const int MaxQueryLength = 400;

    public HttpRequestMessage CreateRequest(string query, int maxResults)
    {
        var trimmed = query.Length <= MaxQueryLength ? query : query[..MaxQueryLength];
        var request = new HttpRequestMessage(HttpMethod.Get, $"{Endpoint}?q={Uri.EscapeDataString(trimmed)}&count={maxResults}&safesearch=moderate");
        request.Headers.Add("X-Subscription-Token", apiKey);
        request.Headers.Accept.ParseAdd("application/json");
        return request;
    }

    public IReadOnlyList<WebResult> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("web", out var web)
            || !web.TryGetProperty("results", out var results)
            || results.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return results.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => new WebResult(
                Read(item, "title"),
                Read(item, "url"),
                Read(item, "description"),
                NullIfEmpty(Read(item, "age")) ?? NullIfEmpty(Read(item, "page_age"))))
            .ToArray();
    }

    private static string Read(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;
}
