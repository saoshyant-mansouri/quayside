using Quayside.Core.Documents;
using Quayside.Core.Grounding;
using Quayside.Core.Retrieval;

namespace Quayside.Api.Orchestration;

public sealed class SourceLedger
{
    private readonly List<ScoredChunk> hits = [];
    private readonly HashSet<string> chunkIds = new(StringComparer.Ordinal);

    public IReadOnlyList<Citation> Citations { get; private set; } = [];

    public bool HasWebSources => hits.Any(IsWeb);

    public IReadOnlyList<SourceReference> Add(IReadOnlyList<ScoredChunk> incoming)
    {
        foreach (var hit in incoming)
        {
            if (chunkIds.Add(hit.Chunk.Id))
            {
                hits.Add(hit);
            }
        }

        var ordered = hits.Where(hit => !IsWeb(hit)).Concat(hits.Where(IsWeb)).ToArray();
        Citations = CitationBuilder.From(ordered);
        var markerByDocument = MarkerByDocument(ordered);
        return incoming
            .Select(hit => new SourceReference(markerByDocument[hit.Document.Id], hit))
            .ToArray();
    }

    private static bool IsWeb(ScoredChunk hit) => hit.Document.Source == SourceKind.Web;

    private Dictionary<string, int> MarkerByDocument(IReadOnlyList<ScoredChunk> ordered)
    {
        var markers = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var hit in ordered)
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
