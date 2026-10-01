using Quayside.Core.Documents;
using Quayside.Core.Sql;

namespace Quayside.Core;

public interface IChunkStore
{
    Task<IReadOnlyList<Document>> LoadDocumentsAsync(CancellationToken ct);
    Task<IReadOnlyList<EmbeddedChunk>> LoadChunksAsync(CancellationToken ct);
    Task UpsertAsync(IReadOnlyList<Document> documents, IReadOnlyList<EmbeddedChunk> chunks, CancellationToken ct);
}

public interface IAnswerCache
{
    Task<CachedAnswer?> TryGetAsync(ReadOnlyMemory<float> questionEmbedding, CancellationToken ct);
    Task StoreAsync(string question, ReadOnlyMemory<float> embedding, string answer, string citationsJson, CancellationToken ct);
}

public sealed record CachedAnswer(string Question, string Answer, string CitationsJson, double Similarity);

public interface ISchemaCatalog
{
    Task<IReadOnlyList<TableCard>> LoadCardsAsync(CancellationToken ct);
    Task UpsertAsync(IReadOnlyList<EmbeddedTableCard> cards, CancellationToken ct);
}

public interface IReadOnlySqlExecutor
{
    Task<SqlResultSet> ExecuteAsync(string sql, CancellationToken ct);
}
