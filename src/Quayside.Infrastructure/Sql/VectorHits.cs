namespace Quayside.Infrastructure.Sql;

public sealed record ChunkHit(string ChunkId, string DocumentId, int Ordinal, string Text, double Similarity);

public sealed record TableHit(string TableName, double Similarity);

public interface IVectorSearch
{
    Task<IReadOnlyList<ChunkHit>> SearchChunksAsync(ReadOnlyMemory<float> queryEmbedding, int k, CancellationToken cancellationToken);

    Task<IReadOnlyList<TableHit>> SearchSchemaCardsAsync(ReadOnlyMemory<float> queryEmbedding, int k, CancellationToken cancellationToken);
}
