namespace Quayside.Core.Documents;

public enum SourceKind
{
    LinkedIn,
    Website,
    Web
}

public sealed record Document(
    string Id,
    SourceKind Source,
    string Url,
    string Title,
    string Text,
    string? PublishedLabel,
    DateTimeOffset CapturedAt,
    string ContentHash);

public sealed record Chunk(
    string Id,
    string DocumentId,
    int Ordinal,
    string Text,
    int TokenCount);

public sealed record EmbeddedChunk(Chunk Chunk, ReadOnlyMemory<float> Embedding);
