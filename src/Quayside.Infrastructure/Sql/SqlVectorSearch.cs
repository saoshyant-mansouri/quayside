using Microsoft.Data.SqlClient;
using Quayside.Infrastructure.Telemetry;

namespace Quayside.Infrastructure.Sql;

public sealed class SqlVectorSearch(SqlDatabase database) : IVectorSearch
{
    public const int MaxK = 200;

    internal const string ChunkQuery = """
        SELECT TOP (@k) Id, DocumentId, Ordinal, Text,
               VECTOR_DISTANCE('cosine', Embedding, @query) AS Distance
        FROM dbo.Chunks
        ORDER BY Distance, Id
        """;

    internal const string SchemaCardQuery = """
        SELECT TOP (@k) TableName,
               VECTOR_DISTANCE('cosine', Embedding, @query) AS Distance
        FROM dbo.SchemaCards
        ORDER BY Distance, TableName
        """;

    public Task<IReadOnlyList<ChunkHit>> SearchChunksAsync(ReadOnlyMemory<float> queryEmbedding, int k, CancellationToken cancellationToken) =>
        RunAsync("vector_search.chunks", ChunkQuery, queryEmbedding, k, reader => new ChunkHit(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt32(2),
            reader.GetString(3),
            AnswerCachePolicy.SimilarityFromCosineDistance(reader.GetDouble(4))), cancellationToken);

    public Task<IReadOnlyList<TableHit>> SearchSchemaCardsAsync(ReadOnlyMemory<float> queryEmbedding, int k, CancellationToken cancellationToken) =>
        RunAsync("vector_search.schema_cards", SchemaCardQuery, queryEmbedding, k, reader => new TableHit(
            reader.GetString(0),
            AnswerCachePolicy.SimilarityFromCosineDistance(reader.GetDouble(1))), cancellationToken);

    private async Task<IReadOnlyList<T>> RunAsync<T>(
        string operation,
        string sql,
        ReadOnlyMemory<float> queryEmbedding,
        int k,
        Func<SqlDataReader, T> map,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(k, 1);
        var limit = Math.Min(k, MaxK);
        var query = Vectors.ToSql(queryEmbedding);

        using var activity = SqlActivity.Start(operation, sql);
        try
        {
            await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.Add(new SqlParameter("@k", limit));
            command.Parameters.Add(new SqlParameter("@query", query));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var hits = new List<T>(limit);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                hits.Add(map(reader));

            activity.Rows(hits.Count);
            return hits;
        }
        catch (Exception exception)
        {
            activity.Fail(exception);
            throw;
        }
    }
}
