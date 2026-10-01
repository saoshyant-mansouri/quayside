using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using Quayside.Infrastructure.Ai;
using Quayside.Infrastructure.Configuration;

namespace Quayside.UnitTests.Infrastructure;

public sealed class RetryPolicyTests
{
    private const string EmbeddingResponse =
        """{"object":"list","data":[{"object":"embedding","index":0,"embedding":[0.5,0.25]}],"model":"m","usage":{"prompt_tokens":3,"total_tokens":3}}""";

    private static readonly AzureOpenAIOptions Settings = new("https://aoai.example.net/", "chat", "embed");

    [Fact]
    public async Task Retries_a_429_until_the_service_recovers()
    {
        var handler = new ScriptedHandler(Status(429), Status(429), Ok());
        var sleeps = new List<TimeSpan>();

        var result = await Embed(handler, new BackoffRetryPolicy(sleep: Record(sleeps)));

        Assert.Equal(3, handler.Calls);
        Assert.Equal(2, sleeps.Count);
        Assert.Equal(0.5f, result.ToFloats().Span[0]);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task Retries_server_errors(int status)
    {
        var handler = new ScriptedHandler(Status(status), Ok());

        await Embed(handler, new BackoffRetryPolicy(sleep: Record([])));

        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Does_not_retry_a_client_error()
    {
        var handler = new ScriptedHandler(Status(400), Ok());

        await Assert.ThrowsAsync<ClientResultException>(() => Embed(handler, new BackoffRetryPolicy(sleep: Record([]))));

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Gives_up_after_the_configured_number_of_retries()
    {
        var handler = new ScriptedHandler(Status(429));

        var exception = await Assert.ThrowsAsync<ClientResultException>(() =>
            Embed(handler, new BackoffRetryPolicy(maxRetries: 3, sleep: Record([]))));

        Assert.Equal(429, exception.Status);
        Assert.Equal(4, handler.Calls);
    }

    [Fact]
    public async Task Honours_retry_after_and_caps_it()
    {
        var sleeps = new List<TimeSpan>();
        var handler = new ScriptedHandler(Status(429, retryAfter: "2"), Status(429, retryAfter: "3600"), Ok());

        await Embed(handler, new BackoffRetryPolicy(maxDelay: TimeSpan.FromSeconds(10), sleep: Record(sleeps)));

        Assert.Equal(TimeSpan.FromSeconds(2), sleeps[0]);
        Assert.Equal(TimeSpan.FromSeconds(10), sleeps[1]);
    }

    [Fact]
    public async Task Backs_off_exponentially_without_a_retry_after_header()
    {
        var sleeps = new List<TimeSpan>();
        var handler = new ScriptedHandler(Status(429), Status(429), Status(429), Ok());

        await Embed(handler, new BackoffRetryPolicy(baseDelay: TimeSpan.FromSeconds(1), nextRandom: () => 1.0, sleep: Record(sleeps)));

        Assert.Equal(3, sleeps.Count);
        Assert.Equal(TimeSpan.FromSeconds(1), sleeps[0]);
        Assert.Equal(TimeSpan.FromSeconds(2), sleeps[1]);
        Assert.Equal(TimeSpan.FromSeconds(4), sleeps[2]);
    }

    [Theory]
    [InlineData(1, 0.0, 250)]
    [InlineData(1, 1.0, 500)]
    [InlineData(3, 0.0, 1000)]
    [InlineData(3, 1.0, 2000)]
    [InlineData(30, 1.0, 30000)]
    [InlineData(30, 0.0, 15000)]
    public void Backoff_applies_jitter_within_half_to_full_of_the_exponential_ceiling(int attempt, double random, double expectedMs)
    {
        var delay = BackoffRetryPolicy.Backoff(attempt, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(30), random);

        Assert.Equal(expectedMs, delay.TotalMilliseconds, precision: 3);
    }

    [Fact]
    public void Jitter_never_exceeds_the_cap()
    {
        for (var attempt = 1; attempt < 64; attempt++)
        {
            var delay = BackoffRetryPolicy.Backoff(attempt, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), 1.0);
            Assert.True(delay <= TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public void The_request_timeout_is_bounded()
    {
        Assert.InRange(AzureOpenAIClients.RequestTimeout, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5));
    }

    private static Func<TimeSpan, CancellationToken, Task> Record(List<TimeSpan> sleeps) => (delay, _) =>
    {
        sleeps.Add(delay);
        return Task.CompletedTask;
    };

    private static async Task<OpenAI.Embeddings.OpenAIEmbedding> Embed(ScriptedHandler handler, BackoffRetryPolicy policy)
    {
        var transport = new HttpClientPipelineTransport(new HttpClient(handler));
        var client = AzureOpenAIClients.Create(Settings, new StaticTokenCredential(), policy, transport);
        var result = await client.GetEmbeddingClient("embed").GenerateEmbeddingAsync("hello");
        return result.Value;
    }

    private static Func<HttpResponseMessage> Status(int status, string? retryAfter = null) => () =>
    {
        var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("""{"error":{"message":"x"}}""", Encoding.UTF8, "application/json") };
        if (retryAfter is not null) response.Headers.TryAddWithoutValidation("Retry-After", retryAfter);
        return response;
    };

    private static Func<HttpResponseMessage> Ok() => () =>
        new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(EmbeddingResponse, Encoding.UTF8, "application/json") };

    private sealed class ScriptedHandler(params Func<HttpResponseMessage>[] script) : HttpMessageHandler
    {
        private int calls;

        public int Calls => Volatile.Read(ref calls);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var index = Interlocked.Increment(ref calls) - 1;
            return Task.FromResult(script[Math.Min(index, script.Length - 1)]());
        }
    }
}
