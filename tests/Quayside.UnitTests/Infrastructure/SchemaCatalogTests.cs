using Microsoft.SqlServer.TransactSql.ScriptDom;
using Quayside.Core.Sql;
using Quayside.Infrastructure.Schema;
using Quayside.Infrastructure.Sql;

namespace Quayside.UnitTests.Infrastructure;

public sealed class SchemaCatalogTests
{
    private static readonly Lazy<SchemaSpec> Spec = new(LoadSpec);
    private static readonly Lazy<IReadOnlyList<TableCard>> Cards = new(() => TableCardFactory.Build(Spec.Value));

    [Fact]
    public void Every_table_gets_a_card_with_populated_ddl()
    {
        Assert.Equal(Spec.Value.Tables.Count, Cards.Value.Count);
        Assert.True(Cards.Value.Count >= 200);
        Assert.All(Cards.Value, card => Assert.False(string.IsNullOrWhiteSpace(card.Ddl)));
    }

    [Fact]
    public void Ddl_is_scoped_to_its_own_table()
    {
        foreach (var card in Cards.Value)
        {
            Assert.StartsWith($"CREATE TABLE ops.{card.Table} (", card.Ddl, StringComparison.Ordinal);
            Assert.Single(card.Ddl.Split("CREATE TABLE"), s => s.Length > 0);
        }
    }

    [Fact]
    public void Ddl_lists_every_column_with_nullability_key_and_foreign_key()
    {
        var table = Spec.Value.Tables.First(t => t.Columns.Any(c => c.References is not null && !c.References.Self));
        var ddl = TableDdl.For(table);

        foreach (var column in table.Columns)
            Assert.Contains($"    {column.Name} {column.Type} {(column.Nullable ? "NULL" : "NOT NULL")}", ddl, StringComparison.Ordinal);

        Assert.Contains(" PRIMARY KEY", ddl, StringComparison.Ordinal);
        foreach (var column in table.Columns.Where(c => c.References is not null))
            Assert.Contains($"REFERENCES ops.{column.References!.Table}({column.References.Column})", ddl, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_ddl_statement_parses_as_t_sql()
    {
        var parser = new TSql170Parser(initialQuotedIdentifiers: true);
        foreach (var card in Cards.Value)
        {
            using var reader = new StringReader(card.Ddl);
            parser.Parse(reader, out var errors);
            Assert.True(errors.Count == 0, $"{card.Table}: {string.Join("; ", errors.Select(e => e.Message))}");
        }
    }

    [Fact]
    public void Cards_carry_the_builder_text_and_adjacency()
    {
        var builder = new SchemaCardBuilder(Spec.Value);
        foreach (var card in Cards.Value)
        {
            Assert.Equal(builder.Cards[card.Table], card.Card);
            Assert.Equal(builder.Adjacency[card.Table].Order(StringComparer.Ordinal), card.Neighbours.Order(StringComparer.Ordinal));
        }
    }

    [Fact]
    public void Descriptions_fit_the_column()
    {
        Assert.All(Cards.Value, card => Assert.True(card.Description.Length <= 1000, card.Table));
        Assert.All(Cards.Value, card => Assert.True(card.Context.Length <= 64, card.Table));
        Assert.All(Cards.Value, card => Assert.True(card.Table.Length <= 128, card.Table));
    }

    [Fact]
    public async Task The_catalog_refuses_cards_without_ddl_before_touching_the_database()
    {
        var catalog = new SqlSchemaCatalog(new SqlDatabase("Server=tcp:127.0.0.1,1;Initial Catalog=x;User ID=u;Password=p"));
        var card = Cards.Value[0] with { Ddl = " " };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            catalog.UpsertAsync([new EmbeddedTableCard(card, new float[1536])], CancellationToken.None));

        Assert.Contains(card.Table, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_catalog_refuses_wrong_sized_embeddings()
    {
        var catalog = new SqlSchemaCatalog(new SqlDatabase("Server=tcp:127.0.0.1,1;Initial Catalog=x;User ID=u;Password=p"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            catalog.UpsertAsync([new EmbeddedTableCard(Cards.Value[0], new float[256])], CancellationToken.None));
    }

    private static SchemaSpec LoadSpec()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "data", "schema", "schema.json")))
            directory = directory.Parent;
        using var stream = File.OpenRead(Path.Combine(
            directory?.FullName ?? throw new DirectoryNotFoundException("data/schema/schema.json not found above the test binaries."),
            "data", "schema", "schema.json"));
        return SchemaSpec.Load(stream);
    }
}
