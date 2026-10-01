using System.Text;

namespace Quayside.Ingest.Schema;

public sealed class SchemaCardBuilder
{
    public SchemaCardBuilder(SchemaSpec spec)
    {
        Cards = spec.Tables.ToDictionary(t => t.Name, Card, StringComparer.Ordinal);

        var neighbours = spec.Tables.ToDictionary(t => t.Name, _ => new SortedSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var fk in spec.ForeignKeys().Where(f => f.Table.Name != f.Target.Table))
        {
            neighbours[fk.Table.Name].Add(fk.Target.Table);
            neighbours[fk.Target.Table].Add(fk.Table.Name);
        }
        Adjacency = neighbours.ToDictionary(p => p.Key, p => (IReadOnlySet<string>)p.Value, StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<string, string> Cards { get; }

    public IReadOnlyDictionary<string, IReadOnlySet<string>> Adjacency { get; }

    private static string Card(TableSpec table)
    {
        var card = new StringBuilder();
        card.Append($"Table: {table.Name}\nContext: {table.Context}\nDescription: {table.Description}\nColumns:\n");
        foreach (var column in table.Columns)
        {
            card.Append($"{column.Name} {column.Type}{(column.Nullable ? "?" : "")}");
            if (column.PrimaryKey) card.Append(" pk");
            if (column.References is { } target) card.Append($" -> {target.Table}");
            card.Append($": {column.Description}\n");
        }
        return card.ToString();
    }
}
