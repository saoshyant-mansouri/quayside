using Quayside.Api.Orchestration;
using Quayside.Core.Grounding;

namespace Quayside.UnitTests.Api;

public sealed class GroundingGateTests
{
    private const string Hallucination = "MSC reported a net profit of 12 billion dollars in 2024.";

    [Fact]
    public async Task Below_the_cosine_floor_no_sources_are_offered_and_the_search_reports_refusal()
    {
        using var host = new ApiHost(new ApiSettings { MinTopCosine = 0.99 });

        var result = await host.AskWarmAsync(Questions.Grounded);

        Assert.Empty(result.Of("citations"));
        var completed = result.ToolEvents("search_knowledge", "completed").Single();
        var detail = completed["detail"];
        Assert.True(detail.GetProperty("refused").GetBoolean());
        Assert.True(detail.GetProperty("topCosine").GetDouble() < 0.99);
    }

    [Fact]
    public async Task Below_the_cosine_floor_a_model_that_answers_anyway_is_replaced_by_the_refusal()
    {
        using var host = new ApiHost(new ApiSettings { MinTopCosine = 0.99 });
        host.Model.Behaviour = ModelBehaviour.FixedAnswer;
        host.Model.Answer = Hallucination;

        var result = await host.AskWarmAsync(Questions.UnknownFigure);

        Assert.Empty(result.Of("citations"));
        Assert.Equal(Prompts.Ungrounded(Questions.UnknownFigure), result.Answer);
        Assert.DoesNotContain("12 billion", result.Raw);
    }

    [Fact]
    public async Task The_refusal_streams_as_tokens_only_after_the_gate_decision()
    {
        using var host = new ApiHost(new ApiSettings { MinTopCosine = 0.99 });
        host.Model.Behaviour = ModelBehaviour.FixedAnswer;
        host.Model.Answer = Hallucination;

        var result = await host.AskWarmAsync(Questions.UnknownFigure);

        var firstToken = result.IndexOfFirst("token");
        var completed = result.Events.ToList().IndexOf(result.ToolEvents("search_knowledge", "completed").Single());
        Assert.True(completed < firstToken);
        Assert.DoesNotContain("12 billion", result.Answer);
    }

    [Fact]
    public async Task A_refusal_still_ends_with_a_well_formed_done_event()
    {
        using var host = new ApiHost(new ApiSettings { MinTopCosine = 0.99 });
        host.Model.Behaviour = ModelBehaviour.FixedAnswer;
        host.Model.Answer = Hallucination;

        var result = await host.AskWarmAsync(Questions.UnknownFigure);

        var done = result.Done;
        Assert.Equal("done", result.Names[^1]);
        Assert.Equal(["cached", "conversationId", "grounding", "latencyMs", "usage"], done.Keys);
        Assert.False(done["cached"].GetBoolean());
        var expected = CitationEnforcer.Check(Prompts.Ungrounded(Questions.UnknownFigure), []);
        Assert.Equal(expected.Cited, done["grounding"].GetProperty("cited").GetInt32());
        Assert.Equal(expected.Uncited, done["grounding"].GetProperty("uncited").GetInt32());
        Assert.Empty(result.Of("error"));
    }

    [Fact]
    public async Task A_refusal_reports_no_uncited_claims()
    {
        using var host = new ApiHost(new ApiSettings { MinTopCosine = 0.99 });
        host.Model.Behaviour = ModelBehaviour.FixedAnswer;
        host.Model.Answer = Hallucination;

        var result = await host.AskWarmAsync(Questions.UnknownFigure);

        Assert.Equal(0, result.Done["grounding"].GetProperty("uncited").GetInt32());
    }

    [Fact]
    public async Task A_model_that_honestly_declines_below_the_floor_is_passed_through_unchanged()
    {
        using var host = new ApiHost(new ApiSettings { MinTopCosine = 0.99 });

        var result = await host.AskWarmAsync(Questions.UnknownFigure);

        Assert.Equal("I could not find anything in the MSC material I have about that, so I will not guess.", result.Answer);
        Assert.Equal(0, result.Done["grounding"].GetProperty("uncited").GetInt32());
    }

    [Fact]
    public async Task A_refusal_is_never_stored_in_the_answer_cache()
    {
        using var host = new ApiHost(new ApiSettings { MinTopCosine = 0.99 });
        await host.WaitUntilWarmAsync();

        await host.AskAsync(Questions.UnknownFigure);
        var callsAfterFirst = host.Model.StreamCalls;
        var second = await host.AskAsync(Questions.UnknownFigure);

        Assert.False(second.Done["cached"].GetBoolean());
        Assert.True(host.Model.StreamCalls > callsAfterFirst);
    }

    [Fact]
    public async Task Above_the_floor_a_fully_cited_answer_reports_no_uncited_claims_and_no_note()
    {
        using var host = new ApiHost();
        host.Model.Behaviour = ModelBehaviour.FixedAnswer;
        host.Model.Answer = "MSC offers weekly sailings from Canada to Europe [1]. MSC also runs a bi-weekly service from Montreal [2].";

        var result = await host.AskWarmAsync(Questions.Grounded);

        Assert.Equal(host.Model.Answer, result.Answer);
        Assert.Equal(2, result.Done["grounding"].GetProperty("cited").GetInt32());
        Assert.Equal(0, result.Done["grounding"].GetProperty("uncited").GetInt32());
        Assert.DoesNotContain(Prompts.UnsourcedNote.Trim(), result.Answer);
    }

    [Fact]
    public async Task Above_the_floor_uncited_sentences_are_counted_and_flagged_in_the_stream()
    {
        using var host = new ApiHost();
        host.Model.Behaviour = ModelBehaviour.FixedAnswer;
        host.Model.Answer = "MSC offers weekly sailings from Canada to Europe [1]. MSC also won an industry award in 2020. MSC employs 200000 people.";

        var result = await host.AskWarmAsync(Questions.Grounded);

        var citations = result.Single("citations")["citations"].EnumerateArray()
            .Select(c => new Citation(
                c.GetProperty("n").GetInt32(),
                string.Empty,
                c.GetProperty("title").GetString()!,
                c.GetProperty("url").GetString()!,
                c.GetProperty("source").GetString()!,
                c.GetProperty("publishedAt").GetString()))
            .ToArray();
        var expected = CitationEnforcer.Check(host.Model.Answer, citations);
        Assert.Equal(1, expected.Cited);
        Assert.Equal(2, expected.Uncited);
        Assert.Equal(expected.Cited, result.Done["grounding"].GetProperty("cited").GetInt32());
        Assert.Equal(expected.Uncited, result.Done["grounding"].GetProperty("uncited").GetInt32());
        Assert.EndsWith(Prompts.UnsourcedNote, result.Answer);
    }

    [Fact]
    public async Task A_marker_the_search_never_returned_does_not_count_as_a_citation()
    {
        using var host = new ApiHost();
        host.Model.Behaviour = ModelBehaviour.FixedAnswer;
        host.Model.Answer = "MSC operates the largest container fleet in the world [99].";

        var result = await host.AskWarmAsync(Questions.Grounded);

        Assert.Equal(0, result.Done["grounding"].GetProperty("cited").GetInt32());
        Assert.Equal(1, result.Done["grounding"].GetProperty("uncited").GetInt32());
    }

    [Fact]
    public async Task Without_any_search_an_ungrounded_factual_answer_is_never_streamed()
    {
        using var host = new ApiHost(new ApiSettings { WarmupWaitSeconds = 0, HoldHydration = true });
        host.Model.Behaviour = ModelBehaviour.FixedAnswer;
        host.Model.Answer = Hallucination;

        var result = await host.AskAsync(Questions.UnknownFigure);

        Assert.Empty(result.Of("citations"));
        Assert.Equal(Prompts.Ungrounded(Questions.UnknownFigure), result.Answer);
        Assert.False(result.ToolEvents("search_knowledge", "completed").Single()["detail"].GetProperty("warm").GetBoolean());
    }
}
