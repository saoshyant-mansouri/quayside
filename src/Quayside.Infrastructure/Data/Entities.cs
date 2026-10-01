using Microsoft.Data.SqlTypes;

namespace Quayside.Infrastructure.Data;

public static class VectorSpec
{
    public const int Dimensions = 1536;
    public const string ColumnType = "vector(1536)";
}

public sealed class DocumentEntity
{
    public required string Id { get; set; }
    public int Source { get; set; }
    public required string Url { get; set; }
    public required string Title { get; set; }
    public required string Text { get; set; }
    public string? PublishedLabel { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public required string ContentHash { get; set; }
    public List<ChunkEntity> Chunks { get; set; } = [];
}

public sealed class ChunkEntity
{
    public required string Id { get; set; }
    public required string DocumentId { get; set; }
    public int Ordinal { get; set; }
    public required string Text { get; set; }
    public int TokenCount { get; set; }
    public SqlVector<float> Embedding { get; set; }
    public DocumentEntity? Document { get; set; }
}

public sealed class AnswerCacheEntity
{
    public long Id { get; set; }
    public required byte[] QuestionHash { get; set; }
    public required string Question { get; set; }
    public SqlVector<float> QuestionEmbedding { get; set; }
    public required string Answer { get; set; }
    public required string CitationsJson { get; set; }
    public int HitCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastHitAt { get; set; }
}

public sealed class SchemaCardEntity
{
    public required string TableName { get; set; }
    public required string Context { get; set; }
    public required string Description { get; set; }
    public required string Card { get; set; }
    public required string NeighboursJson { get; set; }
    public required string Ddl { get; set; }
    public SqlVector<float> Embedding { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class QueryLogEntity
{
    public long Id { get; set; }
    public DateTimeOffset At { get; set; }
    public required string Kind { get; set; }
    public required string Question { get; set; }
    public string? GeneratedSql { get; set; }
    public required string Outcome { get; set; }
    public string? Detail { get; set; }
    public int? RowCount { get; set; }
    public double ElapsedMs { get; set; }
}
