using System.Text.Json;

namespace Quayside.UnitTests.Api;

public sealed class SqlSafetyTests
{
    [Fact]
    public async Task A_destructive_statement_is_rejected_with_a_reason_and_empty_rows()
    {
        using var host = new ApiHost();

        var result = await host.AskWarmAsync(Questions.Destructive);

        var sql = result.Single("sql");
        Assert.Equal("DELETE FROM ops.Bookings", sql["sql"].GetString());
        Assert.False(string.IsNullOrWhiteSpace(sql["rejected"].GetString()));
        Assert.Empty(sql["rows"].EnumerateArray());
        Assert.Empty(sql["columns"].EnumerateArray());
    }

    [Fact]
    public async Task A_rejected_statement_is_never_executed()
    {
        using var host = new ApiHost();

        await host.AskWarmAsync(Questions.Destructive);

        Assert.Empty(host.Sql.Statements);
    }

    [Fact]
    public async Task A_rejected_statement_still_completes_the_tool_and_the_turn()
    {
        using var host = new ApiHost();

        var result = await host.AskWarmAsync(Questions.Destructive);

        Assert.Single(result.ToolEvents("query_database", "started"));
        var completed = result.ToolEvents("query_database", "completed").Single();
        Assert.True(completed["detail"].GetProperty("rejected").GetBoolean());
        Assert.Equal("done", result.Names[^1]);
        Assert.Empty(result.Of("error"));
    }

    [Theory]
    [InlineData("SELECT * FROM ops.Invoices")]
    [InlineData("SELECT TOP (5) p.PortName FROM ops.Ports p; DROP TABLE ops.Ports")]
    [InlineData("UPDATE ops.Ports SET PortName = 'x'")]
    [InlineData("SELECT TOP (5) 1 FROM sys.sql_logins")]
    [InlineData("EXEC xp_cmdshell 'dir'")]
    [InlineData("SELECT TOP (5) PortName FROM ops.Ports p WHERE 1 = 1 -- ")]
    public async Task Hostile_model_output_is_rejected_and_nothing_reaches_the_executor(string hostileSql)
    {
        using var host = new ApiHost();
        host.Model.SqlOverride = hostileSql;

        var result = await host.AskWarmAsync(Questions.Ranking);

        var sql = result.Single("sql");
        Assert.False(string.IsNullOrWhiteSpace(sql["rejected"].GetString()));
        Assert.Empty(sql["rows"].EnumerateArray());
        Assert.Empty(host.Sql.Statements);
    }

    [Fact]
    public async Task A_statement_over_the_retrieved_tables_is_accepted_and_returns_columns_and_rows()
    {
        using var host = new ApiHost();
        host.Model.SqlOverride = Questions.AcceptedSql;

        var result = await host.AskWarmAsync(Questions.Ranking);

        var sql = result.Single("sql");
        Assert.Equal(JsonValueKind.Null, sql["rejected"].ValueKind);
        Assert.Equal(Questions.AcceptedSql, sql["sql"].GetString());
        Assert.Equal(["PortName", "UnLocode", "CountryName", "IsHubPort"], sql["columns"].EnumerateArray().Select(c => c.GetString()));
        var rows = sql["rows"].EnumerateArray().ToArray();
        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Equal(4, row.GetArrayLength()));
        Assert.Equal([Questions.AcceptedSql], host.Sql.Statements);
    }

    [Fact]
    public async Task The_sql_event_precedes_the_tool_completion_and_the_answer()
    {
        using var host = new ApiHost();
        host.Model.SqlOverride = Questions.AcceptedSql;

        var result = await host.AskWarmAsync(Questions.Ranking);

        Assert.True(result.IndexOfFirst("sql") < result.IndexOfFirst("token"));
        Assert.Equal(["tool", "sql", "tool"], result.Names.Take(3));
    }

    [Fact]
    public async Task A_question_the_schema_cannot_answer_is_refused_without_executing_anything()
    {
        using var host = new ApiHost();

        var result = await host.AskWarmAsync(Questions.Unanswerable);

        var sql = result.Single("sql");
        Assert.False(string.IsNullOrWhiteSpace(sql["rejected"].GetString()));
        Assert.Empty(sql["rows"].EnumerateArray());
        Assert.Empty(host.Sql.Statements);
    }

    [Fact]
    public async Task A_database_failure_surfaces_as_a_rejection_without_leaking_the_exception()
    {
        using var host = new ApiHost();
        host.Model.SqlOverride = Questions.AcceptedSql;
        host.Sql.Failure = new InvalidOperationException(ApiHost.Secret);

        var result = await host.AskWarmAsync(Questions.Ranking);

        var sql = result.Single("sql");
        Assert.False(string.IsNullOrWhiteSpace(sql["rejected"].GetString()));
        Assert.Empty(sql["rows"].EnumerateArray());
        Assert.DoesNotContain(ApiHost.Secret, result.Raw);
        Assert.Equal("done", result.Names[^1]);
    }

    [Fact]
    public async Task Operational_answers_are_labelled_synthetic_in_the_tool_detail()
    {
        using var host = new ApiHost();
        host.Model.SqlOverride = Questions.AcceptedSql;

        var result = await host.AskWarmAsync(Questions.Ranking);

        var completed = result.ToolEvents("query_database", "completed").Single();
        Assert.True(completed["detail"].GetProperty("synthetic").GetBoolean());
        Assert.Contains("synthetic", result.Answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Operational_answers_carry_no_grounding_claims()
    {
        using var host = new ApiHost();
        host.Model.SqlOverride = Questions.AcceptedSql;

        var result = await host.AskWarmAsync(Questions.Ranking);

        Assert.Equal(0, result.Done["grounding"].GetProperty("cited").GetInt32());
        Assert.Equal(0, result.Done["grounding"].GetProperty("uncited").GetInt32());
    }
}
