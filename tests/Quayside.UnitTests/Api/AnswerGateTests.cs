using Quayside.Api.Contract;
using Quayside.Api.Orchestration;
using Quayside.Api.Streaming;

namespace Quayside.UnitTests.Api;

public sealed class AnswerGateTests
{
    private sealed class RecordingSink : IEventSink
    {
        public List<ISseEvent> Events { get; } = [];

        public ValueTask EmitAsync(ISseEvent sseEvent, CancellationToken ct)
        {
            Events.Add(sseEvent);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task A_closed_gate_holds_text_back()
    {
        var sink = new RecordingSink();
        var gate = new AnswerGate(sink);

        await gate.WriteAsync("unverified claim", CancellationToken.None);

        Assert.Empty(sink.Events);
        Assert.Equal("unverified claim", gate.Pending);
        Assert.False(gate.HasEmittedTokens);
    }

    [Fact]
    public async Task An_open_gate_streams_text_straight_through()
    {
        var sink = new RecordingSink();
        var gate = new AnswerGate(sink);
        gate.Open();

        await gate.WriteAsync("first ", CancellationToken.None);
        await gate.WriteAsync("second", CancellationToken.None);

        Assert.Equal(["first ", "second"], sink.Events.Cast<TokenEvent>().Select(e => e.Text));
        Assert.Equal("first second", gate.Emitted);
    }

    [Fact]
    public async Task Opening_the_gate_drops_what_was_held_back()
    {
        var sink = new RecordingSink();
        var gate = new AnswerGate(sink);
        await gate.WriteAsync("preamble before the tool ran", CancellationToken.None);

        gate.Open();

        Assert.Equal(string.Empty, gate.Pending);
        Assert.Empty(sink.Events);
    }

    [Fact]
    public async Task Releasing_pending_text_streams_it_and_opens_the_gate()
    {
        var sink = new RecordingSink();
        var gate = new AnswerGate(sink);
        await gate.WriteAsync("held", CancellationToken.None);

        await gate.ReleasePendingAsync(CancellationToken.None);

        Assert.True(gate.IsOpen);
        Assert.Equal("held", Assert.Single(sink.Events.Cast<TokenEvent>()).Text);
        Assert.Equal(string.Empty, gate.Pending);
    }

    [Fact]
    public async Task Replacing_pending_text_streams_only_the_replacement()
    {
        var sink = new RecordingSink();
        var gate = new AnswerGate(sink);
        await gate.WriteAsync("ungrounded claim", CancellationToken.None);

        await gate.ReplacePendingAsync("refusal", CancellationToken.None);

        Assert.Equal("refusal", Assert.Single(sink.Events.Cast<TokenEvent>()).Text);
        Assert.DoesNotContain("ungrounded", gate.Emitted);
    }

    [Fact]
    public async Task Discarding_pending_text_emits_nothing()
    {
        var sink = new RecordingSink();
        var gate = new AnswerGate(sink);
        await gate.WriteAsync("thinking aloud", CancellationToken.None);

        gate.DiscardPending();
        await gate.ReleasePendingAsync(CancellationToken.None);

        Assert.Empty(sink.Events);
    }

    [Fact]
    public async Task Empty_text_is_ignored()
    {
        var sink = new RecordingSink();
        var gate = new AnswerGate(sink);
        gate.Open();

        await gate.WriteAsync(string.Empty, CancellationToken.None);

        Assert.Empty(sink.Events);
    }
}
