using Quayside.Core.Sql;

namespace Quayside.Infrastructure.Schema;

public static class TableCardFactory
{
    public static IReadOnlyList<TableCard> Build(SchemaSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var builder = new SchemaCardBuilder(spec);
        return spec.Tables
            .Select(table => new TableCard(
                table.Name,
                table.Context,
                table.Description,
                builder.Cards[table.Name],
                builder.Adjacency[table.Name].ToArray(),
                TableDdl.For(table)))
            .ToArray();
    }

    public static IReadOnlyList<TableCard> Load(string schemaFile)
    {
        using var stream = File.OpenRead(schemaFile);
        return Build(SchemaSpec.Load(stream));
    }
}
