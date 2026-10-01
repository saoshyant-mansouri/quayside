namespace Quayside.Api.Hosting;

public sealed class Hydrated<T> where T : class
{
    private readonly TaskCompletionSource<T> source = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public T? Value => source.Task.IsCompletedSuccessfully ? source.Task.Result : null;

    public bool IsReady => source.Task.IsCompletedSuccessfully;

    public void Publish(T value) => source.TrySetResult(value);

    public async Task<T?> WaitAsync(TimeSpan timeout, CancellationToken ct)
    {
        if (Value is { } ready)
        {
            return ready;
        }

        try
        {
            return await source.Task.WaitAsync(timeout, ct);
        }
        catch (TimeoutException)
        {
            return null;
        }
    }
}
