using Quayside.Core.Sql;

namespace Quayside.UnitTests.Sql;

public sealed class SchemaRetrieverTests
{
    [Fact]
    public void FixtureUsesTheRealSchema()
    {
        Assert.Equal(226, SchemaFixture.Cards.Count);
    }

    [Fact]
    public void ReeferTemperatureQuestionRetrievesReeferTables()
    {
        var tables = SchemaFixture.Select("What was the supply air temperature of the reefer container over time?");

        Assert.Equal("ReeferReadings", tables[0]);
        Assert.Contains("ReeferUnits", tables);
        Assert.True(tables.Count(t => t.StartsWith("Reefer", StringComparison.Ordinal)) >= 4);
    }

    [Fact]
    public void ForeignKeyExpansionReachesJoinPartnersRankedBelowDirectHits()
    {
        const string question = "What was the supply air temperature of the reefer container over time?";
        var direct = SchemaFixture.Select(question, hops: 0);
        var expanded = SchemaFixture.Select(question, hops: 1);
        var neighbours = SchemaFixture.Cards.ToDictionary(c => c.Card.Table, c => c.Card.Neighbours, StringComparer.Ordinal);

        Assert.Equal(direct, expanded.Take(direct.Count));
        Assert.True(expanded.Count > direct.Count);
        foreach (var added in expanded.Skip(direct.Count))
            Assert.Contains(direct, d => neighbours[d].Contains(added));
    }

    [Fact]
    public void ExpansionMakesTheContainersJoinReachable()
    {
        var question = "Which gate in and discharge movements did container MSCU1234567 have?";

        Assert.Contains("Containers", SchemaFixture.Select(question, k: 3, hops: 1));
    }

    [Theory]
    [InlineData("Where was container MSCU1234567 gate in, loaded and discharged, and how long did it dwell at the terminal?", "ContainerMovements")]
    [InlineData("Which customer-facing tracking milestones were published for this container, such as empty released or estimated arrival?", "ContainerEvents")]
    [InlineData("Which chassis and genset movements happened between depots?", "EquipmentMovements")]
    public void ConfusableMovementTablesAreToldApart(string question, string expectedFirst)
    {
        var tables = SchemaFixture.Select(question);

        Assert.Equal(expectedFirst, tables[0]);
        var others = new[] { "ContainerMovements", "ContainerEvents", "EquipmentMovements" }.Where(t => t != expectedFirst);
        foreach (var other in others)
        {
            var position = tables.ToList().IndexOf(other);
            Assert.True(position < 0 || position > tables.ToList().IndexOf(expectedFirst));
        }
    }

    [Fact]
    public void ResultsAreStableAcrossCallsAndInputOrder()
    {
        const string question = "Which chassis and genset movements happened between depots?";
        var first = SchemaFixture.Select(question);
        var second = SchemaFixture.Select(question);
        var shuffled = new SchemaRetriever(SchemaFixture.Cards.Reverse().ToArray());
        var fromShuffled = shuffled.Select(SchemaFixture.Embed(question), question).Select(c => c.Table).ToArray();

        Assert.Equal(first, second);
        Assert.Equal(first, fromShuffled);
    }

    [Fact]
    public void ResultNeverExceedsTheCap()
    {
        var tables = SchemaFixture.Select("booking customer invoice container vessel voyage port tariff customs reefer", k: 500, hops: 5);

        Assert.True(tables.Count <= SchemaRetriever.MaxTables);
        Assert.Equal(tables.Count, tables.Distinct().Count());
    }

    [Fact]
    public void QueryMatchingNothingReturnsNoTables()
    {
        var result = SchemaFixture.Retriever.Select(default, "zzqxv wwkjh", 8, 1);

        Assert.Empty(result);
    }

    [Fact]
    public void NonPositiveKReturnsNothing()
    {
        Assert.Empty(SchemaFixture.Retriever.Select(SchemaFixture.Embed("reefer"), "reefer", 0));
    }

    [Fact]
    public void KLargerThanTableCountReturnsEveryReachableTable()
    {
        var cards = new[]
        {
            SchemaFixture.PlainCard("Alpha", "alpha ships", "Beta"),
            SchemaFixture.PlainCard("Beta", "beta cargo", "Alpha"),
            SchemaFixture.PlainCard("Gamma", "gamma cargo")
        }.Select(SchemaFixture.Embedded).ToArray();

        var result = new SchemaRetriever(cards).Select(SchemaFixture.Embed("cargo ships"), "cargo ships", k: 50, hops: 2);

        Assert.Equal(["Alpha", "Beta", "Gamma"], result.Select(c => c.Table).Order(StringComparer.Ordinal));
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void TableWithoutNeighboursIsReturnedWithoutExpansion()
    {
        var cards = new[]
        {
            SchemaFixture.PlainCard("Island", "isolated lighthouse registry"),
            SchemaFixture.PlainCard("Harbour", "harbour berths", "Pier"),
            SchemaFixture.PlainCard("Pier", "pier structures", "Harbour")
        }.Select(SchemaFixture.Embedded).ToArray();

        var result = new SchemaRetriever(cards).Select(SchemaFixture.Embed("lighthouse"), "lighthouse registry", k: 1, hops: 3);

        Assert.Equal(["Island"], result.Select(c => c.Table));
    }

    [Fact]
    public void NeighboursNamingUnknownTablesAreIgnored()
    {
        var cards = new[] { SchemaFixture.PlainCard("Solo", "solo table", "Ghost") }.Select(SchemaFixture.Embedded).ToArray();

        var result = new SchemaRetriever(cards).Select(SchemaFixture.Embed("solo"), "solo", 5, 2);

        Assert.Equal(["Solo"], result.Select(c => c.Table));
    }

    [Fact]
    public void EmbeddingOfTheWrongSizeFallsBackToLexicalOnly()
    {
        var result = SchemaFixture.Retriever.Select(new float[] { 1, 0, 0 }, "reefer temperature readings", 4, 0);

        Assert.Contains(result, c => c.Table == "ReeferReadings");
    }

    [Fact]
    public void HopsOfZeroReturnOnlyDirectHits()
    {
        var result = SchemaFixture.Select("reefer supply air temperature", k: 3, hops: 0);

        Assert.Equal(3, result.Count);
    }
}
