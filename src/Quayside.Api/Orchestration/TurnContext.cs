using Quayside.Api.Contract;
using Quayside.Api.Streaming;

namespace Quayside.Api.Orchestration;

public sealed class TurnContext(IEventSink sink, string question, ReadOnlyMemory<float> questionEmbedding)
{
    public string Question { get; } = question;

    public ReadOnlyMemory<float> QuestionEmbedding { get; } = questionEmbedding;

    public SourceLedger Sources { get; } = new();

    public AnswerGate Gate { get; } = new(sink);

    public UsageTally Usage { get; } = new();

    public bool OperationalEvidence { get; set; }

    public bool SqlEventEmitted { get; set; }

    public bool CorpusSearched { get; set; }

    public bool WebFallbackUsed { get; set; }

    public CitationsEvent CitationsSnapshot() => new(
        Sources.Citations.Select(c => new CitationPayload(c.Marker, c.Title, c.Url, c.Source, c.PublishedLabel)).ToArray());

    public ValueTask EmitAsync(ISseEvent sseEvent, CancellationToken ct) => sink.EmitAsync(sseEvent, ct);
}
