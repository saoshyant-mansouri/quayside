using Microsoft.Extensions.Options;
using Quayside.Api;
using Quayside.Api.Conversations;

namespace Quayside.UnitTests.Api;

public sealed class ConversationTests
{
    [Fact]
    public async Task The_conversation_id_from_done_continues_the_same_conversation()
    {
        using var host = new ApiHost();
        var first = await host.AskWarmAsync(Questions.Grounded);
        var id = first.Done["conversationId"].GetString();

        var second = await host.AskAsync(Questions.Container, id);

        Assert.Equal(id, second.Done["conversationId"].GetString());
    }

    [Fact]
    public async Task A_follow_up_sends_the_earlier_turn_to_the_model()
    {
        using var host = new ApiHost();
        var first = await host.AskWarmAsync(Questions.Grounded);

        await host.AskAsync(Questions.Container, first.Done["conversationId"].GetString());

        Assert.Equal([2, 4], host.Model.TurnStartMessageCounts);
    }

    [Fact]
    public async Task Without_an_id_every_request_starts_a_fresh_conversation()
    {
        using var host = new ApiHost();
        await host.AskWarmAsync(Questions.Grounded);

        await host.AskAsync(Questions.Container);

        Assert.Equal([2, 2], host.Model.TurnStartMessageCounts);
    }

    [Fact]
    public async Task Separate_conversations_get_separate_ids()
    {
        using var host = new ApiHost();
        var first = await host.AskWarmAsync(Questions.Grounded);

        var second = await host.AskAsync(Questions.Container);

        Assert.NotEqual(first.Done["conversationId"].GetString(), second.Done["conversationId"].GetString());
    }

    [Theory]
    [InlineData("does-not-exist")]
    [InlineData("../../etc/passwd")]
    [InlineData("a b; DROP TABLE x")]
    public async Task An_unknown_or_malformed_id_is_replaced_not_adopted(string forged)
    {
        using var host = new ApiHost();

        var result = await host.AskWarmAsync(Questions.Grounded, forged);

        Assert.NotEqual(forged, result.Done["conversationId"].GetString());
        Assert.False(string.IsNullOrWhiteSpace(result.Done["conversationId"].GetString()));
        Assert.Equal([2], host.Model.TurnStartMessageCounts);
    }

    [Fact]
    public async Task The_bounded_store_evicts_the_oldest_conversation_over_http()
    {
        using var host = new ApiHost(new ApiSettings { MaxConversations = 2 });
        var first = await host.AskWarmAsync(Questions.Grounded);
        await host.AskAsync(Questions.Container);
        await host.AskAsync(Questions.Destructive);

        var resumed = await host.AskAsync(Questions.Container, first.Done["conversationId"].GetString());

        Assert.NotEqual(first.Done["conversationId"].GetString(), resumed.Done["conversationId"].GetString());
    }

    [Fact]
    public void The_store_never_holds_more_conversations_than_its_limit()
    {
        var store = Store(maxConversations: 3);

        for (var i = 0; i < 50; i++)
        {
            store.Open(null);
        }

        Assert.Equal(3, store.Count);
    }

    [Fact]
    public void The_store_evicts_the_least_recently_used_conversation()
    {
        var store = Store(maxConversations: 2);
        var oldest = store.Open(null).Id;
        var kept = store.Open(null).Id;
        store.Open(kept);

        store.Open(null);

        Assert.Equal(kept, store.Open(kept).Id);
        Assert.NotEqual(oldest, store.Open(oldest).Id);
    }

    [Fact]
    public void A_conversation_keeps_only_its_most_recent_turns()
    {
        var store = Store(maxTurns: 3);
        var id = store.Open(null).Id;

        for (var i = 0; i < 10; i++)
        {
            store.Append(id, new ChatTurn($"question {i}", $"answer {i}"));
        }

        var turns = store.Open(id).Turns;
        Assert.Equal(["question 7", "question 8", "question 9"], turns.Select(t => t.User));
    }

    [Fact]
    public void A_snapshot_is_not_changed_by_later_turns()
    {
        var store = Store();
        var snapshot = store.Open(null);

        store.Append(snapshot.Id, new ChatTurn("question", "answer"));

        Assert.Empty(snapshot.Turns);
    }

    [Fact]
    public void An_over_long_id_is_not_adopted()
    {
        var store = Store();
        var id = store.Open(null).Id;
        var forged = id + new string('a', 100);

        Assert.NotEqual(forged, store.Open(forged).Id);
    }

    private static ConversationStore Store(int maxConversations = 500, int maxTurns = 8) =>
        new(Options.Create(new ChatLimits { MaxConversations = maxConversations, MaxTurnsPerConversation = maxTurns }));
}
