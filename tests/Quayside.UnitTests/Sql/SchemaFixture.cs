using System.Text;
using Quayside.Core.Sql;

namespace Quayside.UnitTests.Sql;

internal static class SchemaFixture
{
    public const int Dimensions = 256;

    private static readonly Lazy<IReadOnlyList<EmbeddedTableCard>> LazyCards = new(Build);

    public static IReadOnlyList<EmbeddedTableCard> Cards => LazyCards.Value;

    public static SchemaRetriever Retriever => new(Cards);

    public static IReadOnlyList<string> Select(string question, int k = 8, int hops = 1) =>
        Retriever.Select(Embed(question), question, k, hops).Select(c => c.Table).ToArray();

    public static ReadOnlyMemory<float> Embed(string text)
    {
        var vector = new float[Dimensions];
        foreach (var token in Words(text))
        {
            var hash = 2166136261u;
            foreach (var c in token)
                hash = (hash ^ c) * 16777619u;
            vector[hash % Dimensions] += 1;
        }
        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        if (norm > 0)
            for (var i = 0; i < vector.Length; i++) vector[i] /= norm;
        return vector;
    }

    public static TableCard PlainCard(string table, string description, params string[] neighbours) =>
        new(table, "test", description, $"Table: {table}\nDescription: {description}\n", neighbours, $"CREATE TABLE ops.{table} (Id int);");

    public static EmbeddedTableCard Embedded(TableCard card) => new(card, Embed(card.Card));

    private static List<string> Words(string text)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsLetter(c))
            {
                if (current.Length > 0 && char.IsUpper(c) && char.IsLower(text[i - 1])) Flush();
                current.Append(char.ToLowerInvariant(c));
            }
            else Flush();
        }
        Flush();
        return words;

        void Flush()
        {
            if (current.Length > 2) words.Add(current.ToString());
            current.Clear();
        }
    }

    private static IReadOnlyList<EmbeddedTableCard> Build()
    {
        using var schema = File.OpenRead(Path.Combine(RepositoryRoot(), "data", "schema", "schema.json"));
        var spec = SchemaSpec.Load(schema);
        var builder = new SchemaCardBuilder(spec);
        return spec.Tables
            .Select(t => new TableCard(
                t.Name,
                t.Context,
                t.Description,
                builder.Cards[t.Name],
                builder.Adjacency[t.Name].ToArray(),
                Ddl(t)))
            .Select(Embedded)
            .ToArray();
    }

    private static string Ddl(TableSpec table)
    {
        var ddl = new StringBuilder($"CREATE TABLE ops.{table.Name} (\n");
        foreach (var column in table.Columns)
        {
            ddl.Append($"    {column.Name} {column.Type} {(column.Nullable ? "NULL" : "NOT NULL")}");
            if (column.PrimaryKey) ddl.Append(" PRIMARY KEY");
            if (column.References is { } target) ddl.Append($" REFERENCES ops.{target.Table}({target.Column})");
            ddl.Append(",\n");
        }
        return ddl.Append(");").ToString();
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "data", "schema", "schema.json")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("data/schema/schema.json not found above the test binaries.");
    }
}
