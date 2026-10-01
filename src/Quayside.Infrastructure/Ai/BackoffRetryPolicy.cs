using System.ClientModel.Primitives;
using System.Globalization;

namespace Quayside.Infrastructure.Ai;

public sealed class BackoffRetryPolicy : ClientRetryPolicy
{
    public const int DefaultMaxRetries = 8;

    private static readonly TimeSpan DefaultBaseDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan DefaultMaxDelay = TimeSpan.FromSeconds(90);

    private readonly TimeSpan baseDelay;
    private readonly TimeSpan maxDelay;
    private readonly Func<double> nextRandom;
    private readonly Func<TimeSpan, CancellationToken, Task> sleep;

    public BackoffRetryPolicy(
        int maxRetries = DefaultMaxRetries,
        TimeSpan? baseDelay = null,
        TimeSpan? maxDelay = null,
        Func<double>? nextRandom = null,
        Func<TimeSpan, CancellationToken, Task>? sleep = null)
        : base(maxRetries)
    {
        this.baseDelay = baseDelay ?? DefaultBaseDelay;
        this.maxDelay = maxDelay ?? DefaultMaxDelay;
        this.nextRandom = nextRandom ?? Random.Shared.NextDouble;
        this.sleep = sleep ?? Task.Delay;
    }

    public static TimeSpan Backoff(int attempt, TimeSpan baseDelay, TimeSpan maxDelay, double random)
    {
        var exponent = Math.Min(Math.Max(attempt - 1, 0), 20);
        var ceiling = Math.Min(baseDelay.TotalMilliseconds * Math.Pow(2, exponent), maxDelay.TotalMilliseconds);
        var floor = ceiling / 2;
        return TimeSpan.FromMilliseconds(floor + (ceiling - floor) * Math.Clamp(random, 0, 1));
    }

    protected override TimeSpan GetNextDelay(PipelineMessage message, int tryCount)
    {
        if (RetryAfter(message) is { } requested)
            return requested > maxDelay ? maxDelay : requested;

        return Backoff(tryCount, baseDelay, maxDelay, nextRandom());
    }

    protected override Task WaitAsync(TimeSpan time, CancellationToken cancellationToken) => sleep(time, cancellationToken);

    protected override void Wait(TimeSpan time, CancellationToken cancellationToken) =>
        sleep(time, cancellationToken).GetAwaiter().GetResult();

    private static TimeSpan? RetryAfter(PipelineMessage message)
    {
        if (message.Response is null || !message.Response.Headers.TryGetValue("Retry-After", out var value) || value is null)
            return null;

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0)
            return TimeSpan.FromSeconds(seconds);

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at))
        {
            var wait = at - DateTimeOffset.UtcNow;
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }

        return null;
    }
}
