using Quayside.Api.Contract;

namespace Quayside.Api.Streaming;

public interface IEventSink
{
    ValueTask EmitAsync(ISseEvent sseEvent, CancellationToken ct);
}
