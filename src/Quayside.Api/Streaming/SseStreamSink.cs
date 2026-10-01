using System.Text;
using Quayside.Api.Contract;

namespace Quayside.Api.Streaming;

public sealed class SseStreamSink(Stream body) : IEventSink
{
    private readonly SemaphoreSlim writeLock = new(1, 1);

    public async ValueTask EmitAsync(ISseEvent sseEvent, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(SseJson.Frame(sseEvent));
        await writeLock.WaitAsync(ct);
        try
        {
            await body.WriteAsync(bytes, ct);
            await body.FlushAsync(ct);
        }
        finally
        {
            writeLock.Release();
        }
    }
}
