using Quayside.Core.Sql;

namespace Quayside.UnitTests.Sql;

public sealed class SqlPromptTests
{
    private const string Question = "What was the supply air temperature of the reefer container over time?";

    private static IReadOnlyList<TableCard> Selected() =>
        SchemaFixture.Retriever.Select(SchemaFixture.Embed(Question), Question);

    [Fact]
    public void SameInputGivesByteIdenticalOutput()
    {
        var first = SqlPrompt.Build(Selected(), Question, 100);
        var second = SqlPrompt.Build(Selected(), Question, 100);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ContainsTheRulesTheGuardEnforces()
    {
        var prompt = SqlPrompt.Build(Selected(), Question, 100);

        Assert.Contains("Exactly one SELECT", prompt);
        Assert.Contains("Read-only", prompt);
        Assert.Contains("TOP (100)", prompt);
        Assert.Contains("schema ops", prompt);
        Assert.Contains("Use only the tables and columns listed below", prompt);
        Assert.Contains(SqlPrompt.Unanswerable, prompt);
    }

    [Fact]
    public void ContainsTheQuestionAndOnlyTheSelectedTablesDdl()
    {
        var tables = Selected();
        var prompt = SqlPrompt.Build(tables, Question, 100);

        Assert.Contains(Question, prompt);
        foreach (var table in tables) Assert.Contains($"CREATE TABLE ops.{table.Table} (", prompt);

        var omitted = SchemaFixture.Cards.Select(c => c.Card.Table).Except(tables.Select(t => t.Table)).ToList();
        Assert.NotEmpty(omitted);
        foreach (var table in omitted) Assert.DoesNotContain($"CREATE TABLE ops.{table} (", prompt);
    }

    [Fact]
    public void PromptIsAFractionOfTheFullSchema()
    {
        var prompt = SqlPrompt.Build(Selected(), Question, 100);
        var fullSchema = SchemaFixture.Cards.Sum(c => c.Card.Ddl.Length);

        Assert.True(prompt.Length * 10 < fullSchema, $"{prompt.Length} chars against {fullSchema}");
    }

    [Fact]
    public void TableOrderFollowsTheOrderGiven()
    {
        var cards = SchemaFixture.Cards.Select(c => c.Card).Take(3).ToArray();

        var forward = SqlPrompt.Build(cards, Question, 10);
        var reversed = SqlPrompt.Build(cards.Reverse().ToArray(), Question, 10);

        Assert.NotEqual(forward, reversed);
        Assert.True(forward.IndexOf(cards[0].Ddl, StringComparison.Ordinal) < forward.IndexOf(cards[2].Ddl, StringComparison.Ordinal));
    }

    [Fact]
    public void InvalidArgumentsAreCallerBugs()
    {
        Assert.Throws<ArgumentNullException>(() => SqlPrompt.Build(null!, Question, 10));
        Assert.Throws<ArgumentNullException>(() => SqlPrompt.Build([], null!, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => SqlPrompt.Build([], Question, 0));
    }
}
