using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Quayside.Core;

namespace Quayside.Infrastructure.Web;

public sealed class TavilyProvider(string apiKey) : IWebSearchProvider
{
    public const string Endpoint = "https://api.tavily.com/search";
    public const int MaxQueryLength = 400;

    public HttpRequestMessage CreateRequest(string query, int maxResults)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(new
            {
                query = Truncate(query),
                max_results = maxResults,
                search_depth = "basic",
                include_answer = false,
                include_raw_content = false,
                include_images = false,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return request;
    }

    public IReadOnlyList<WebResult> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return results.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => new WebResult(
                Read(item, "title"),
                Read(item, "url"),
                Read(item, "content"),
                DateLabel(Read(item, "published_date"))))
            .ToArray();
    }

    private static string Truncate(string query) => query.Length <= MaxQueryLength ? query : query[..MaxQueryLength];

    private static string Read(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static string? DateLabel(string value) =>
        DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var date)
            ? date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            : null;
}
