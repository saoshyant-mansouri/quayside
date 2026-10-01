using System.Net;
using System.Text.Json;

namespace Quayside.UnitTests.Api;

public sealed class SseContractTests
{
    private static readonly string[] AllowedEvents = ["tool", "citations", "sql", "token", "done", "error"];

    [Fact]
    public async Task The_response_is_an_event_stream()
    {
        using var host = new ApiHost();

        var result = await host.AskWarmAsync(Questions.Grounded);

        Assert.Equal(HttpStatusCode.OK, result.Status);
        Assert.Equal("text/event-stream", result.MediaType);
    }

    [Fact]
    public async Task The_stream_is_not_cached_or_buffered_by_intermediaries()
    {
        using var host = new ApiHost();
        await host.WaitUntilWarmAsync();

        using var request = ApiHost.ChatRequest(Questions.Grounded);
        using var response = await host.Client.SendAsync(request);

        Assert.Contains("no-cache", response.Headers.CacheControl!.ToString());
        Assert.Equal("no", response.Headers.GetValues("X-Accel-Buffering").Single());
    }

    [Theory]
    [MemberData(nameof(Questions.Scenarios), MemberType = typeof(Questions))]
    public async Task Every_event_is_framed_as_event_line_data_line_blank_line(string question)
    {
        using var host = new ApiHost();
        host.Model.SqlOverride = Questions.SqlFor(question);

        var result = await host.AskWarmAsync(question);

        Assert.NotEmpty(result.Events);
        Assert.EndsWith("\n\n", result.Raw);
        Assert.All(result.Events, e => Assert.Equal(JsonValueKind.Object, e.Json.ValueKind));
    }

    [Theory]
    [MemberData(nameof(Questions.Scenarios), MemberType = typeof(Questions))]
    public async Task Only_documented_event_names_are_emitted(string question)
    {
        using var host = new ApiHost();
        host.Model.SqlOverride = Questions.SqlFor(question);

        var result = await host.AskWarmAsync(question);

        Assert.All(result.Names, name => Assert.Contains(name, AllowedEvents));
    }

    [Fact]
    public async Task Citations_arrive_before_the_first_token()
    {
        using var host = new ApiHost();

        var result = await host.AskWarmAsync(Questions.Grounded);

        Assert.Single(result.Of("citations"));
        Assert.True(result.IndexOfFirst("token") >= 0);
        Assert.True(result.IndexOfFirst("citations") < result.IndexOfFirst("token"));
    }

    [Theory]
    [MemberData(nameof(Questions.Scenarios), MemberType = typeof(Questions))]
    public async Task Done_is_the_final_event_and_appears_exactly_once(string question)
    {
        using var host = new ApiHost();
        host.Model.SqlOverride = Questions.SqlFor(question);

        var result = await host.AskWarmAsync(question);

        Assert.Single(result.Of("done"));
        Assert.Equal("done", result.Names[^1]);
        Assert.Empty(result.Of("error"));
    }

    [Fact]
    public async Task Done_carries_every_documented_field()
    {
        using var host = new ApiHost();

        var done = (await host.AskWarmAsync(Questions.Grounded)).Done;

        Assert.Equal(["cached", "conversationId", "grounding", "latencyMs", "usage"], done.Keys);
        Assert.False(string.IsNullOrWhiteSpace(done["conversationId"].GetString()));
        Assert.True(done["latencyMs"].GetInt64() >= 0);
        Assert.False(done["cached"].GetBoolean());
        Assert.Equal(["completion", "prompt"], new SseEvent("usage", done["usage"].GetRawText(), done["usage"]).Keys);
        Assert.Equal(["cited", "uncited"], new SseEvent("grounding", done["grounding"].GetRawText(), done["grounding"]).Keys);
        Assert.True(done["usage"].GetProperty("prompt").GetInt64() > 0);
        Assert.True(done["usage"].GetProperty("completion").GetInt64() > 0);
    }

    [Fact]
    public async Task Citations_use_the_documented_field_names()
    {
        using var host = new ApiHost();

        var citations = (await host.AskWarmAsync(Questions.Grounded)).Single("citations");

        Assert.Equal(["citations"], citations.Keys);
        var entries = citations["citations"].EnumerateArray().ToArray();
        Assert.NotEmpty(entries);
        Assert.All(entries, entry =>
        {
            Assert.Equal(["n", "publishedAt", "source", "title", "url"], new SseEvent("citation", entry.GetRawText(), entry).Keys);
            Assert.Contains(entry.GetProperty("source").GetString(), new[] { "linkedin", "website" });
        });
        Assert.Equal(Enumerable.Range(1, entries.Length), entries.Select(e => e.GetProperty("n").GetInt32()));
    }

    [Fact]
    public async Task Token_events_carry_only_text()
    {
        using var host = new ApiHost();

        var tokens = (await host.AskWarmAsync(Questions.Grounded)).Of("token");

        Assert.NotEmpty(tokens);
        Assert.All(tokens, token => Assert.Equal(["text"], token.Keys));
    }

    [Fact]
    public async Task Tool_events_pair_started_with_completed()
    {
        using var host = new ApiHost();

        var result = await host.AskWarmAsync(Questions.Grounded);

        var tools = result.Of("tool");
        Assert.Equal(2, tools.Count);
        Assert.Equal(["started", "completed"], tools.Select(t => t["status"].GetString()));
        Assert.All(tools, tool => Assert.Equal("search_knowledge", tool["name"].GetString()));
        Assert.Equal(["name", "status"], tools[0].Keys);
        Assert.Equal(["detail", "name", "status"], tools[1].Keys);
    }

    [Theory]
    [MemberData(nameof(Questions.Scenarios), MemberType = typeof(Questions))]
    public async Task Every_started_tool_completes_after_it_starts(string question)
    {
        using var host = new ApiHost();
        host.Model.SqlOverride = Questions.SqlFor(question);

        var result = await host.AskWarmAsync(question);

        var tools = result.Of("tool");
        Assert.NotEmpty(tools);
        foreach (var name in tools.Select(t => t["name"].GetString()!).Distinct())
        {
            var started = result.ToolEvents(name, "started");
            var completed = result.ToolEvents(name, "completed");
            Assert.Equal(started.Count, completed.Count);
            Assert.True(result.Events.ToList().IndexOf(started[0]) < result.Events.ToList().IndexOf(completed[0]));
        }
    }

    [Fact]
    public async Task A_sql_event_appears_only_when_the_database_tool_ran()
    {
        using var host = new ApiHost();
        host.Model.SqlOverride = Questions.AcceptedSql;
        await host.WaitUntilWarmAsync();

        var knowledge = await host.AskAsync(Questions.Grounded);
        var container = await host.AskAsync(Questions.Container);
        var database = await host.AskAsync(Questions.Ranking);

        Assert.Empty(knowledge.Of("sql"));
        Assert.Empty(container.Of("sql"));
        Assert.Single(database.Of("sql"));
        Assert.Equal(["query_database", "query_database"], database.Of("tool").Select(t => t["name"].GetString()));
    }

    [Fact]
    public async Task Sql_events_use_the_documented_field_names()
    {
        using var host = new ApiHost();
        host.Model.SqlOverride = Questions.AcceptedSql;

        var sql = (await host.AskWarmAsync(Questions.Ranking)).Single("sql");

        Assert.Equal(["columns", "rejected", "rows", "sql"], sql.Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_message_is_a_bad_request_not_a_stream(string message)
    {
        using var host = new ApiHost();

        var result = await host.AskAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, result.Status);
        Assert.NotEqual("text/event-stream", result.MediaType);
        Assert.Empty(result.Events);
        Assert.Equal(0, host.Model.StreamCalls);
    }

    [Fact]
    public async Task An_over_long_message_is_a_bad_request_and_never_reaches_the_model()
    {
        using var host = new ApiHost();

        var result = await host.AskAsync(new string('a', 2001));

        Assert.Equal(HttpStatusCode.BadRequest, result.Status);
        Assert.Equal(0, host.Model.StreamCalls);
    }
}
