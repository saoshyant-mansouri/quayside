namespace Quayside.Core.Grounding;

public sealed record Citation(
    int Marker,
    string ChunkId,
    string Title,
    string Url,
    string Source,
    string? PublishedLabel);

public sealed record GroundingReport(int Cited, int Uncited, IReadOnlyList<string> UnsupportedSentences);
