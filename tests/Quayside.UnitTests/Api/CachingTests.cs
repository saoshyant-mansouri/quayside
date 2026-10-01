namespace Quayside.UnitTests.Api;

public sealed class CachingTests
{
    [Fact]
    public async Task The_first_ask_is_not_cached()
    {
        using var host = new ApiHost();

        var first = await host.AskWarmAsync(Questions.Grounded);

        Assert.False(first.Done["cached"].GetBoolean());
        Assert.True(host.Model.StreamCalls > 0);
    }

    [Fact]
    public async Task A_repeated_identical_question_is_served_from_the_cache()
    {
        using var host = new ApiHost();
        await host.AskWarmAsync(Questions.Grounded);

        var second = await host.AskAsync(Questions.Grounded);

        Assert.True(second.Done["cached"].GetBoolean());
    }

    [Fact]
    public async Task A_cache_hit_does_not_run_generation_again()
    {
        using var host = new ApiHost();
        await host.AskWarmAsync(Questions.Grounded);
        var streamCalls = host.Model.StreamCalls;
        var sqlCalls = host.Model.SqlCalls;

        await host.AskAsync(Questions.Grounded);

        Assert.Equal(streamCalls, host.Model.StreamCalls);
        Assert.Equal(sqlCalls, host.Model.SqlCalls);
    }

    [Fact]
    public async Task A_cache_hit_replays_the_same_answer_with_citations_first_and_zero_usage()
    {
        using var host = new ApiHost();
        var first = await host.AskWarmAsync(Questions.Grounded);

        var second = await host.AskAsync(Questions.Grounded);

        Assert.Equal(first.Answer, second.Answer);
        Assert.Equal(first.Single("citations")["citations"].GetRawText(), second.Single("citations")["citations"].GetRawText());
        Assert.True(second.IndexOfFirst("citations") < second.IndexOfFirst("token"));
        Assert.Equal(0, second.Done["usage"].GetProperty("prompt").GetInt64());
        Assert.Equal(0, second.Done["usage"].GetProperty("completion").GetInt64());
        Assert.Empty(second.Of("tool"));
        Assert.Equal("done", second.Names[^1]);
    }

    [Fact]
    public async Task A_cache_hit_reports_the_grounding_of_the_cached_answer()
    {
        using var host = new ApiHost();
        var first = await host.AskWarmAsync(Questions.Grounded);

        var second = await host.AskAsync(Questions.Grounded);

        Assert.Equal(first.Done["grounding"].GetRawText(), second.Done["grounding"].GetRawText());
    }

    [Fact]
    public async Task A_follow_up_inside_a_conversation_bypasses_the_cache()
    {
        using var host = new ApiHost();
        var first = await host.AskWarmAsync(Questions.Grounded);
        var streamCalls = host.Model.StreamCalls;

        var followUp = await host.AskAsync(Questions.Grounded, first.Done["conversationId"].GetString());

        Assert.False(followUp.Done["cached"].GetBoolean());
        Assert.True(host.Model.StreamCalls > streamCalls);
    }

    [Fact]
    public async Task Synthetic_operational_answers_are_never_cached()
    {
        using var host = new ApiHost();
        host.Model.SqlOverride = Questions.AcceptedSql;
        await host.AskWarmAsync(Questions.Ranking);
        var sqlCalls = host.Model.SqlCalls;

        var second = await host.AskAsync(Questions.Ranking);

        Assert.False(second.Done["cached"].GetBoolean());
        Assert.True(host.Model.SqlCalls > sqlCalls);
        Assert.Single(second.Of("sql"));
    }

    [Fact]
    public async Task An_answer_with_an_uncited_claim_is_not_cached()
    {
        using var host = new ApiHost();
        host.Model.Behaviour = ModelBehaviour.FixedAnswer;
        host.Model.Answer = "MSC employs 200000 people.";
        await host.AskWarmAsync(Questions.Grounded);
        var streamCalls = host.Model.StreamCalls;

        var second = await host.AskAsync(Questions.Grounded);

        Assert.False(second.Done["cached"].GetBoolean());
        Assert.True(host.Model.StreamCalls > streamCalls);
    }
}
