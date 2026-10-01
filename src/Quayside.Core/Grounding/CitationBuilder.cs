using System.Globalization;
using Quayside.Core.Retrieval;

namespace Quayside.Core.Grounding;

public static class CitationBuilder
{
    public static IReadOnlyList<Citation> From(IReadOnlyList<ScoredChunk> hits)
    {
        var citations = new List<Citation>();
        var markerByDocument = new HashSet<string>(StringComparer.Ordinal);

        foreach (var hit in hits)
        {
            if (!markerByDocument.Add(hit.Document.Id))
            {
                continue;
            }

            citations.Add(new Citation(
                citations.Count + 1,
                hit.Chunk.Id,
                hit.Document.Title,
                hit.Document.Url,
                hit.Document.Source.ToString().ToLower(CultureInfo.InvariantCulture),
                hit.Document.PublishedLabel));
        }

        return citations;
    }
}
