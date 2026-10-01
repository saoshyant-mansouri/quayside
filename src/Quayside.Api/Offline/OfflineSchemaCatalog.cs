using System.Text;
using Quayside.Core;
using Quayside.Core.Sql;

namespace Quayside.Api.Offline;

public sealed class OfflineSchemaCatalog(OfflineData data) : ISchemaCatalog
{
    private readonly Lazy<IReadOnlyList<TableCard>> cards = new(() => Build(data));

    public Task<IReadOnlyList<TableCard>> LoadCardsAsync(CancellationToken ct) => Task.FromResult(cards.Value);

    public Task UpsertAsync(IReadOnlyList<EmbeddedTableCard> cards, CancellationToken ct) => Task.CompletedTask;

    private static IReadOnlyList<TableCard> Build(OfflineData data)
    {
        using var stream = File.OpenRead(data.SchemaFile);
        var spec = SchemaSpec.Load(stream);
        var builder = new SchemaCardBuilder(spec);
        return spec.Tables
            .Select(table => new TableCard(
                table.Name,
                table.Context,
                table.Description,
                builder.Cards[table.Name],
                builder.Adjacency[table.Name].ToArray(),
                Ddl(table)))
            .ToArray();
    }

    private static string Ddl(TableSpec table)
    {
        var ddl = new StringBuilder($"CREATE TABLE ops.{table.Name} (\n");
        foreach (var column in table.Columns)
        {
            ddl.Append($"    {column.Name} {column.Type} {(column.Nullable ? "NULL" : "NOT NULL")}");
            if (column.PrimaryKey)
            {
                ddl.Append(" PRIMARY KEY");
            }

            if (column.References is { } target)
            {
                ddl.Append($" REFERENCES ops.{target.Table}({target.Column})");
            }

            ddl.Append(",\n");
        }

        return ddl.Append(");").ToString();
    }
}
