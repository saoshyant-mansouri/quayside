using Quayside.Core.Grounding;
using Quayside.Core.Retrieval;

namespace Quayside.Api.Orchestration;

public sealed class SourceLedger
{
    private readonly List<ScoredChunk> hits = [];
    private readonly HashSet<string> chunkIds = new(StringComparer.Ordinal);

    public IReadOnlyList<Citation> Citations { get; private set; } = [];

    public IReadOnlyList<SourceReference> Add(IReadOnlyList<ScoredChunk> incoming)
    {
        foreach (var hit in incoming)
        {
            if (chunkIds.Add(hit.Chunk.Id))
            {
                hits.Add(hit);
            }
        }

        Citations = CitationBuilder.From(hits);
        var markerByDocument = MarkerByDocument();
        return incoming
            .Select(hit => new SourceReference(markerByDocument[hit.Document.Id], hit))
            .ToArray();
    }

    private Dictionary<string, int> MarkerByDocument()
    {
        var markers = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var hit in hits)
        {
            if (!markers.ContainsKey(hit.Document.Id))
            {
                markers[hit.Document.Id] = Citations[markers.Count].Marker;
            }
        }

        return markers;
    }
}

public sealed record SourceReference(int Marker, ScoredChunk Hit);
