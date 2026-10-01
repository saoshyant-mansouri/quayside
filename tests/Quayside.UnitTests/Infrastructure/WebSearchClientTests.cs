using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Quayside.Infrastructure.Web;

namespace Quayside.UnitTests.Infrastructure;

public sealed class WebSearchClientTests
{
    private const string TavilyBody = """
        {"query":"q","results":[
          {"title":"MSC Technology Italia","url":"https://www.msc.com/turin","content":"Via Nizza 262/Int.27, 10125 Torino","score":0.9,"published_date":"2025-06-01T10:00:00Z"},
          {"title":"No date","url":"https://example.com/x","content":"text"},
          "not an object"
        ]}
        """;

    private const string BraveBody = """
        {"web":{"results":[
          {"title":"MSC <strong>Turin</strong>","url":"https://www.msc.com/turin","description":"Via <strong>Nizza</strong> 262","age":"2 days ago"},
          {"title":"Page","url":"https://example.com/y","description":"text","page_age":"2025-06-01T00:00:00"}
        ]}}
        """;

    [Fact]
    public async Task Tavily_sends_a_bearer_authenticated_post_and_maps_the_results()
    {
        var handler = new ScriptedHandler(_ => Json(TavilyBody));
        var client = Client(handler, new TavilyProvider("sekret"));

        var results = await client.SearchAsync("msc turin", 4, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.tavily.com/search", request.Uri);
        Assert.Equal("Bearer sekret", request.Authorization);
        Assert.DoesNotContain("sekret", request.Uri + request.Body, StringComparison.Ordinal);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("msc turin", body.RootElement.GetProperty("query").GetString());
        Assert.Equal(4, body.RootElement.GetProperty("max_results").GetInt32());
        Assert.False(body.RootElement.GetProperty("include_answer").GetBoolean());

        Assert.Equal(2, results.Count);
        Assert.Equal("MSC Technology Italia", results[0].Title);
        Assert.Equal("https://www.msc.com/turin", results[0].Url);
        Assert.Equal("Via Nizza 262/Int.27, 10125 Torino", results[0].Snippet);
        Assert.Equal("2025-06-01", results[0].PublishedLabel);
        Assert.Null(results[1].PublishedLabel);
    }

    [Fact]
    public async Task Brave_sends_a_get_with_the_subscription_token_and_maps_the_results()
    {
        var handler = new ScriptedHandler(_ => Json(BraveBody));
        var client = Client(handler, new BraveProvider("sekret"));

        var results = await client.SearchAsync("msc turin & co", 3, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.StartsWith("https://api.search.brave.com/res/v1/web/search?q=msc%20turin%20%26%20co&count=3", request.Uri, StringComparison.Ordinal);
        Assert.Equal("sekret", request.Token);
        Assert.DoesNotContain("sekret", request.Uri, StringComparison.Ordinal);
        Assert.Equal(2, results.Count);
        Assert.Equal("2 days ago", results[0].PublishedLabel);
        Assert.Equal("2025-06-01T00:00:00", results[1].PublishedLabel);
    }

    [Fact]
    public async Task A_response_without_results_is_an_empty_list()
    {
        var client = Client(new ScriptedHandler(_ => Json("{}")), new TavilyProvider("k"));

        Assert.Empty(await client.SearchAsync("q", 5, CancellationToken.None));
    }

    [Fact]
    public async Task A_blank_query_makes_no_request()
    {
        var handler = new ScriptedHandler(_ => Json(TavilyBody));
        var client = Client(handler, new TavilyProvider("k"));

        Assert.Empty(await client.SearchAsync("   ", 5, CancellationToken.None));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_rate_limit_is_retried_honouring_retry_after_then_succeeds()
    {
        var waits = new List<TimeSpan>();
        var handler = new ScriptedHandler(call => call == 1
            ? new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Headers = { RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(1)) } }
            : Json(TavilyBody));
        var client = Client(handler, new TavilyProvider("k"), waits);

        var results = await client.SearchAsync("q", 5, CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal([TimeSpan.FromSeconds(1)], waits);
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task A_retry_after_longer_than_the_cap_is_capped()
    {
        var waits = new List<TimeSpan>();
        var handler = new ScriptedHandler(call => call == 1
            ? new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Headers = { RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMinutes(5)) } }
            : Json(TavilyBody));
        var client = Client(handler, new TavilyProvider("k"), waits);

        await client.SearchAsync("q", 5, CancellationToken.None);

        Assert.Equal([TimeSpan.FromSeconds(3)], waits);
    }

    [Fact]
    public async Task Server_errors_back_off_exponentially_then_give_up_after_the_retry_budget()
    {
        var waits = new List<TimeSpan>();
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));
        var client = Client(handler, new TavilyProvider("k"), waits);

        var failure = await Assert.ThrowsAsync<HttpRequestException>(() => client.SearchAsync("q", 5, CancellationToken.None));

        Assert.Equal(WebSearchClient.DefaultMaxRetries + 1, handler.Requests.Count);
        Assert.Equal(WebSearchClient.DefaultMaxRetries, waits.Count);
        Assert.True(waits[1] > waits[0]);
        Assert.Contains("502", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_rejected_key_is_not_retried_and_the_key_never_appears_in_the_error()
    {
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("bad key sekret") });
        var client = Client(handler, new TavilyProvider("sekret"));

        var failure = await Assert.ThrowsAsync<HttpRequestException>(() => client.SearchAsync("q", 5, CancellationToken.None));

        Assert.Single(handler.Requests);
        Assert.Contains("401", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sekret", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_network_failure_is_retried()
    {
        var handler = new ScriptedHandler(call => call == 1 ? throw new HttpRequestException("reset") : Json(TavilyBody));
        var client = Client(handler, new TavilyProvider("k"));

        Assert.Equal(2, (await client.SearchAsync("q", 5, CancellationToken.None)).Count);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task A_timeout_is_retried_but_a_caller_cancellation_is_not()
    {
        var handler = new ScriptedHandler(call => call == 1 ? throw new TaskCanceledException("timeout") : Json(TavilyBody));
        var client = Client(handler, new TavilyProvider("k"));
        Assert.Equal(2, (await client.SearchAsync("q", 5, CancellationToken.None)).Count);

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var cancelHandler = new ScriptedHandler(_ => Json(TavilyBody));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(cancelHandler, new TavilyProvider("k")).SearchAsync("q", 5, cancelled.Token));
    }

    [Fact]
    public async Task An_unreadable_body_fails_without_retrying()
    {
        var handler = new ScriptedHandler(_ => Json("<html>not json</html>"));
        var client = Client(handler, new TavilyProvider("k"));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SearchAsync("q", 5, CancellationToken.None));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task The_requested_count_is_clamped_to_the_ceiling()
    {
        var handler = new ScriptedHandler(_ => Json(TavilyBody));

        await Client(handler, new TavilyProvider("k")).SearchAsync("q", 500, CancellationToken.None);

        using var body = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.Equal(WebSearchOptions.CeilingMaxResults, body.RootElement.GetProperty("max_results").GetInt32());
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static WebSearchClient Client(ScriptedHandler handler, IWebSearchProvider provider, List<TimeSpan>? waits = null) =>
        new(
            new StubFactory(handler),
            provider,
            NullLogger<WebSearchClient>.Instance,
            nextRandom: () => 0.5,
            sleep: (time, _) =>
            {
                waits?.Add(time);
                return Task.CompletedTask;
            });

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed record Seen(HttpMethod Method, string Uri, string? Authorization, string? Token, string Body);

    private sealed class ScriptedHandler(Func<int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        private int calls;

        public List<Seen> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            request.Headers.TryGetValues("X-Subscription-Token", out var token);
            Requests.Add(new Seen(request.Method, request.RequestUri!.AbsoluteUri, request.Headers.Authorization?.ToString(), token?.SingleOrDefault(), body));
            return respond(Interlocked.Increment(ref calls));
        }
    }
}
