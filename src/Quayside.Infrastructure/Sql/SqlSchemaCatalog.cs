using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlTypes;
using Quayside.Core;
using Quayside.Core.Sql;
using Quayside.Infrastructure.Telemetry;

namespace Quayside.Infrastructure.Sql;

public sealed class SqlSchemaCatalog(SqlDatabase database) : ISchemaCatalog, IEmbeddedCardSource
{
    private const string CreateStaging = """
        CREATE TABLE #SchemaCards (
            TableName nvarchar(128) NOT NULL PRIMARY KEY,
            Context nvarchar(64) NOT NULL,
            Description nvarchar(1000) NOT NULL,
            Card nvarchar(max) NOT NULL,
            NeighboursJson nvarchar(max) NOT NULL,
            Ddl nvarchar(max) NOT NULL,
            Embedding vector(1536) NOT NULL);
        """;

    private const string Apply = """
        UPDATE t SET
            Context = s.Context, Description = s.Description, Card = s.Card,
            NeighboursJson = s.NeighboursJson, Ddl = s.Ddl, Embedding = s.Embedding,
            UpdatedAt = SYSDATETIMEOFFSET()
        FROM dbo.SchemaCards t JOIN #SchemaCards s ON s.TableName = t.TableName;

        INSERT dbo.SchemaCards (TableName, Context, Description, Card, NeighboursJson, Ddl, Embedding, UpdatedAt)
        SELECT s.TableName, s.Context, s.Description, s.Card, s.NeighboursJson, s.Ddl, s.Embedding, SYSDATETIMEOFFSET()
        FROM #SchemaCards s
        WHERE NOT EXISTS (SELECT 1 FROM dbo.SchemaCards t WHERE t.TableName = s.TableName);
        """;

    private sealed record CardRow(string TableName, string Context, string Description, string Card, string NeighboursJson, string Ddl);

    public async Task<IReadOnlyList<TableCard>> LoadCardsAsync(CancellationToken ct)
    {
        using var activity = SqlActivity.Start("schema_cards.load");
        try
        {
            await using var connection = await database.OpenAsync(ct).ConfigureAwait(false);
            var command = new CommandDefinition(
                "SELECT TableName, Context, Description, Card, NeighboursJson, Ddl FROM dbo.SchemaCards ORDER BY TableName",
                commandTimeout: BulkStage.TimeoutSeconds,
                cancellationToken: ct);
            var rows = (await connection.QueryAsync<CardRow>(command).ConfigureAwait(false)).ToList();
            activity.Rows(rows.Count);
            return rows.Select(ToCard).ToArray();
        }
        catch (Exception exception)
        {
            activity.Fail(exception);
            throw;
        }
    }

    public async Task<IReadOnlyList<EmbeddedTableCard>> LoadEmbeddedCardsAsync(CancellationToken cancellationToken)
    {
        using var activity = SqlActivity.Start("schema_cards.load_embedded");
        try
        {
            await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT TableName, Context, Description, Card, NeighboursJson, Ddl, Embedding FROM dbo.SchemaCards ORDER BY TableName";
            command.CommandTimeout = BulkStage.TimeoutSeconds;

            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);
            var cards = new List<EmbeddedTableCard>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var row = new CardRow(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5));
                cards.Add(new EmbeddedTableCard(ToCard(row), reader.GetSqlVector<float>(6).Memory));
            }

            activity.Rows(cards.Count);
            return cards;
        }
        catch (Exception exception)
        {
            activity.Fail(exception);
            throw;
        }
    }

    public async Task UpsertAsync(IReadOnlyList<EmbeddedTableCard> cards, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cards);
        if (cards.Count == 0) return;

        foreach (var embedded in cards)
        {
            if (string.IsNullOrWhiteSpace(embedded.Card.Ddl))
                throw new ArgumentException($"Table card '{embedded.Card.Table}' has no DDL; build cards with TableCardFactory.", nameof(cards));
        }

        using var activity = SqlActivity.Start("schema_cards.upsert");
        activity.Rows(cards.Count);
        try
        {
            var table = Stage(cards);

            await using var connection = await database.OpenAsync(ct).ConfigureAwait(false);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

            await BulkStage.ExecuteAsync(connection, transaction, CreateStaging, ct).ConfigureAwait(false);
            await BulkStage.CopyAsync(connection, transaction, "#SchemaCards", table, ct).ConfigureAwait(false);
            await BulkStage.ExecuteAsync(connection, transaction, Apply, ct).ConfigureAwait(false);

            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            activity.Fail(exception);
            throw;
        }
    }

    private static TableCard ToCard(CardRow row) => new(
        row.TableName,
        row.Context,
        row.Description,
        row.Card,
        JsonSerializer.Deserialize<string[]>(row.NeighboursJson) ?? [],
        row.Ddl);

    private static DataTable Stage(IReadOnlyList<EmbeddedTableCard> cards)
    {
        var table = new DataTable();
        table.Columns.Add("TableName", typeof(string));
        table.Columns.Add("Context", typeof(string));
        table.Columns.Add("Description", typeof(string));
        table.Columns.Add("Card", typeof(string));
        table.Columns.Add("NeighboursJson", typeof(string));
        table.Columns.Add("Ddl", typeof(string));
        table.Columns.Add("Embedding", typeof(SqlVector<float>));

        foreach (var embedded in cards.GroupBy(c => c.Card.Table, StringComparer.Ordinal).Select(g => g.Last()))
        {
            var card = embedded.Card;
            table.Rows.Add(
                card.Table,
                card.Context,
                card.Description,
                card.Card,
                JsonSerializer.Serialize(card.Neighbours),
                card.Ddl,
                Vectors.ToSql(embedded.Embedding));
        }
        return table;
    }
}
