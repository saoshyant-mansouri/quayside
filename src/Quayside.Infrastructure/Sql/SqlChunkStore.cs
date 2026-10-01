using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlTypes;
using Quayside.Core;
using Quayside.Core.Documents;
using Quayside.Infrastructure.Telemetry;

namespace Quayside.Infrastructure.Sql;

public sealed class SqlChunkStore(SqlDatabase database) : IChunkStore
{
    private const string CreateStaging = """
        CREATE TABLE #Documents (
            Id nvarchar(128) NOT NULL PRIMARY KEY,
            Source int NOT NULL,
            Url nvarchar(800) NOT NULL,
            Title nvarchar(500) NOT NULL,
            Text nvarchar(max) NOT NULL,
            PublishedLabel nvarchar(64) NULL,
            CapturedAt datetimeoffset NOT NULL,
            ContentHash char(64) NOT NULL);
        CREATE TABLE #Chunks (
            Id nvarchar(192) NOT NULL PRIMARY KEY,
            DocumentId nvarchar(128) NOT NULL,
            Ordinal int NOT NULL,
            Text nvarchar(max) NOT NULL,
            TokenCount int NOT NULL,
            Embedding vector(1536) NOT NULL);
        """;

    private const string Apply = """
        UPDATE t SET
            Source = s.Source, Url = s.Url, Title = s.Title, Text = s.Text,
            PublishedLabel = s.PublishedLabel, CapturedAt = s.CapturedAt, ContentHash = s.ContentHash
        FROM dbo.Documents t JOIN #Documents s ON s.Id = t.Id;

        INSERT dbo.Documents (Id, Source, Url, Title, Text, PublishedLabel, CapturedAt, ContentHash)
        SELECT s.Id, s.Source, s.Url, s.Title, s.Text, s.PublishedLabel, s.CapturedAt, s.ContentHash
        FROM #Documents s
        WHERE NOT EXISTS (SELECT 1 FROM dbo.Documents t WHERE t.Id = s.Id);

        DELETE c FROM dbo.Chunks c
        WHERE c.DocumentId IN (SELECT DISTINCT DocumentId FROM #Chunks)
          AND NOT EXISTS (SELECT 1 FROM #Chunks s WHERE s.Id = c.Id);

        UPDATE t SET
            DocumentId = s.DocumentId, Ordinal = s.Ordinal, Text = s.Text,
            TokenCount = s.TokenCount, Embedding = s.Embedding
        FROM dbo.Chunks t JOIN #Chunks s ON s.Id = t.Id;

        INSERT dbo.Chunks (Id, DocumentId, Ordinal, Text, TokenCount, Embedding)
        SELECT s.Id, s.DocumentId, s.Ordinal, s.Text, s.TokenCount, s.Embedding
        FROM #Chunks s
        WHERE NOT EXISTS (SELECT 1 FROM dbo.Chunks t WHERE t.Id = s.Id);
        """;

    private sealed record DocumentRow(
        string Id,
        int Source,
        string Url,
        string Title,
        string Text,
        string? PublishedLabel,
        DateTimeOffset CapturedAt,
        string ContentHash);

    public async Task<IReadOnlyList<Document>> LoadDocumentsAsync(CancellationToken ct)
    {
        using var activity = SqlActivity.Start("documents.load");
        try
        {
            await using var connection = await database.OpenAsync(ct).ConfigureAwait(false);
            var command = new CommandDefinition(
                "SELECT Id, Source, Url, Title, Text, PublishedLabel, CapturedAt, ContentHash FROM dbo.Documents ORDER BY Id",
                commandTimeout: BulkStage.TimeoutSeconds,
                cancellationToken: ct);
            var rows = (await connection.QueryAsync<DocumentRow>(command).ConfigureAwait(false)).ToList();
            activity.Rows(rows.Count);
            return rows
                .Select(r => new Document(r.Id, (SourceKind)r.Source, r.Url, r.Title, r.Text, r.PublishedLabel, r.CapturedAt, r.ContentHash))
                .ToArray();
        }
        catch (Exception exception)
        {
            activity.Fail(exception);
            throw;
        }
    }

    public async Task<IReadOnlyList<EmbeddedChunk>> LoadChunksAsync(CancellationToken ct)
    {
        using var activity = SqlActivity.Start("chunks.load");
        try
        {
            await using var connection = await database.OpenAsync(ct).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, DocumentId, Ordinal, Text, TokenCount, Embedding FROM dbo.Chunks ORDER BY Id";
            command.CommandTimeout = BulkStage.TimeoutSeconds;

            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct).ConfigureAwait(false);
            var chunks = new List<EmbeddedChunk>();
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var id = reader.GetString(0);
                var documentId = reader.GetString(1);
                var ordinal = reader.GetInt32(2);
                var text = reader.GetString(3);
                var tokens = reader.GetInt32(4);
                var embedding = reader.GetSqlVector<float>(5);
                chunks.Add(new EmbeddedChunk(new Chunk(id, documentId, ordinal, text, tokens), embedding.Memory));
            }

            activity.Rows(chunks.Count);
            return chunks;
        }
        catch (Exception exception)
        {
            activity.Fail(exception);
            throw;
        }
    }

    public async Task UpsertAsync(IReadOnlyList<Document> documents, IReadOnlyList<EmbeddedChunk> chunks, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(chunks);
        if (documents.Count == 0 && chunks.Count == 0) return;

        using var activity = SqlActivity.Start("chunks.upsert");
        activity.Rows(chunks.Count);
        try
        {
            var documentTable = DocumentTable(documents);
            var chunkTable = ChunkTable(chunks);

            await using var connection = await database.OpenAsync(ct).ConfigureAwait(false);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

            await BulkStage.ExecuteAsync(connection, transaction, CreateStaging, ct).ConfigureAwait(false);
            await BulkStage.CopyAsync(connection, transaction, "#Documents", documentTable, ct).ConfigureAwait(false);
            await BulkStage.CopyAsync(connection, transaction, "#Chunks", chunkTable, ct).ConfigureAwait(false);
            await BulkStage.ExecuteAsync(connection, transaction, Apply, ct).ConfigureAwait(false);

            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            activity.Fail(exception);
            throw;
        }
    }

    private static DataTable DocumentTable(IReadOnlyList<Document> documents)
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(string));
        table.Columns.Add("Source", typeof(int));
        table.Columns.Add("Url", typeof(string));
        table.Columns.Add("Title", typeof(string));
        table.Columns.Add("Text", typeof(string));
        table.Columns.Add("PublishedLabel", typeof(string));
        table.Columns.Add("CapturedAt", typeof(DateTimeOffset));
        table.Columns.Add("ContentHash", typeof(string));

        foreach (var document in documents.GroupBy(d => d.Id, StringComparer.Ordinal).Select(g => g.Last()))
        {
            table.Rows.Add(
                document.Id,
                (int)document.Source,
                document.Url,
                document.Title,
                document.Text,
                (object?)document.PublishedLabel ?? DBNull.Value,
                document.CapturedAt,
                document.ContentHash);
        }
        return table;
    }

    private static DataTable ChunkTable(IReadOnlyList<EmbeddedChunk> chunks)
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(string));
        table.Columns.Add("DocumentId", typeof(string));
        table.Columns.Add("Ordinal", typeof(int));
        table.Columns.Add("Text", typeof(string));
        table.Columns.Add("TokenCount", typeof(int));
        table.Columns.Add("Embedding", typeof(SqlVector<float>));

        foreach (var embedded in chunks.GroupBy(c => c.Chunk.Id, StringComparer.Ordinal).Select(g => g.Last()))
        {
            var chunk = embedded.Chunk;
            table.Rows.Add(chunk.Id, chunk.DocumentId, chunk.Ordinal, chunk.Text, chunk.TokenCount, Vectors.ToSql(embedded.Embedding));
        }
        return table;
    }
}
