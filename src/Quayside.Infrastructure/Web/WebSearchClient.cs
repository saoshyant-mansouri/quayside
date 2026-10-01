using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Quayside.Core;
using Quayside.Infrastructure.Ai;

namespace Quayside.Infrastructure.Web;

public sealed class WebSearchClient(
    IHttpClientFactory httpClients,
    IWebSearchProvider provider,
    ILogger<WebSearchClient> logger,
    int maxRetries = WebSearchClient.DefaultMaxRetries,
    TimeSpan? baseDelay = null,
    TimeSpan? maxDelay = null,
    Func<double>? nextRandom = null,
    Func<TimeSpan, CancellationToken, Task>? sleep = null) : IWebSearch
{
    public const string HttpClientName = "quayside-web-search";
    public const int DefaultMaxRetries = 2;

    private static readonly TimeSpan DefaultBaseDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan DefaultMaxDelay = TimeSpan.FromSeconds(3);

    private readonly TimeSpan baseDelay = baseDelay ?? DefaultBaseDelay;
    private readonly TimeSpan maxDelay = maxDelay ?? DefaultMaxDelay;
    private readonly Func<double> nextRandom = nextRandom ?? Random.Shared.NextDouble;
    private readonly Func<TimeSpan, CancellationToken, Task> sleep = sleep ?? Task.Delay;

    public async Task<IReadOnlyList<WebResult>> SearchAsync(string query, int maxResults, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var limit = Math.Clamp(maxResults, 1, WebSearchOptions.CeilingMaxResults);
        for (var attempt = 1; ; attempt++)
        {
            var failure = await TryOnceAsync(query.Trim(), limit, ct);
            if (failure.Results is { } results)
            {
                return results;
            }

            if (!failure.Retryable || attempt > maxRetries)
            {
                throw new HttpRequestException($"Web search failed: {failure.Reason}");
            }

            var wait = failure.RetryAfter is { } requested
                ? (requested > maxDelay ? maxDelay : requested)
                : BackoffRetryPolicy.Backoff(attempt, baseDelay, maxDelay, nextRandom());
            logger.LogWarning("Web search attempt {Attempt} failed ({Reason}); retrying in {WaitMs} ms", attempt, failure.Reason, (int)wait.TotalMilliseconds);
            await sleep(wait, ct);
        }
    }

    private async Task<Attempt> TryOnceAsync(string query, int limit, CancellationToken ct)
    {
        try
        {
            using var request = provider.CreateRequest(query, limit);
            using var response = await httpClients.CreateClient(HttpClientName).SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                return Attempt.Success(provider.Parse(body));
            }

            var status = (int)response.StatusCode;
            var retryable = response.StatusCode == HttpStatusCode.RequestTimeout
                || response.StatusCode == HttpStatusCode.TooManyRequests
                || status >= 500;
            return Attempt.Failure($"HTTP {status}", retryable, RetryAfter(response));
        }
        catch (HttpRequestException ex)
        {
            return Attempt.Failure(ex.GetType().Name, true, null);
        }
        catch (JsonException)
        {
            return Attempt.Failure("unreadable response", false, null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Attempt.Failure("timeout", true, null);
        }
    }

    private static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta)
        {
            return delta;
        }

        if (header?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }

        return null;
    }

    private sealed record Attempt(IReadOnlyList<WebResult>? Results, string Reason, bool Retryable, TimeSpan? RetryAfter)
    {
        public static Attempt Success(IReadOnlyList<WebResult> results) => new(results, string.Empty, false, null);

        public static Attempt Failure(string reason, bool retryable, TimeSpan? retryAfter) => new(null, reason, retryable, retryAfter);
    }
}
