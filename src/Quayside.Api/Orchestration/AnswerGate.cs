using System.Text;
using Quayside.Api.Contract;
using Quayside.Api.Streaming;

namespace Quayside.Api.Orchestration;

public sealed class AnswerGate(IEventSink sink)
{
    private readonly StringBuilder pending = new();
    private readonly StringBuilder emitted = new();

    public bool IsOpen { get; private set; }

    public string Pending => pending.ToString();

    public string Emitted => emitted.ToString();

    public bool HasEmittedTokens => emitted.Length > 0;

    public async ValueTask WriteAsync(string text, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (!IsOpen)
        {
            pending.Append(text);
            return;
        }

        emitted.Append(text);
        await sink.EmitAsync(new TokenEvent(text), ct);
    }

    public void DiscardPending() => pending.Clear();

    public void Open()
    {
        IsOpen = true;
        pending.Clear();
    }

    public async ValueTask ReleasePendingAsync(CancellationToken ct)
    {
        var text = pending.ToString();
        pending.Clear();
        IsOpen = true;
        await WriteAsync(text, ct);
    }

    public async ValueTask ReplacePendingAsync(string replacement, CancellationToken ct)
    {
        pending.Clear();
        pending.Append(replacement);
        await ReleasePendingAsync(ct);
    }
}
