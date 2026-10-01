using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Quayside.Api.Offline;

namespace Quayside.UnitTests.Api;

public enum ModelBehaviour
{
    PassThrough,
    FixedAnswer,
    StallAfterFirstToken,
    FailAfterFirstToken,
}

public sealed partial class ModelProbe : IChatClient
{
    private readonly ScriptedChatClient inner = new();
    private readonly ConcurrentQueue<int> turnStarts = new();
    private int streamCalls;
    private int sqlCalls;

    public ModelBehaviour Behaviour { get; set; }

    public string Answer { get; set; } = string.Empty;

    public string? SqlOverride { get; set; }

    public string FailureMessage { get; set; } = "model failure";

    public int StreamCalls => Volatile.Read(ref streamCalls);

    public int SqlCalls => Volatile.Read(ref sqlCalls);

    public IReadOnlyList<int> TurnStartMessageCounts => turnStarts.ToArray();

    public TaskCompletionSource Stalled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref sqlCalls);
        return SqlOverride is { } sql
            ? Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, sql)))
            : inner.GetResponseAsync(messages, options, cancellationToken);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var conversation = messages.ToList();
        Interlocked.Increment(ref streamCalls);
        if (conversation[^1].Role == ChatRole.User)
        {
            turnStarts.Enqueue(conversation.Count);
        }

        var afterTool = conversation[^1].Contents.OfType<FunctionResultContent>().Any();
        if (afterTool && Behaviour == ModelBehaviour.FixedAnswer)
        {
            foreach (var piece in Pieces(Answer))
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, piece);
            }

            yield break;
        }

        await foreach (var update in inner.GetStreamingResponseAsync(conversation, options, cancellationToken))
        {
            yield return update;
            if (!afterTool || string.IsNullOrEmpty(update.Text))
            {
                continue;
            }

            if (Behaviour == ModelBehaviour.StallAfterFirstToken)
            {
                await StallAsync(cancellationToken);
            }

            if (Behaviour == ModelBehaviour.FailAfterFirstToken)
            {
                throw new InvalidOperationException(FailureMessage);
            }
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }

    private async Task StallAsync(CancellationToken ct)
    {
        Stalled.TrySetResult();
        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException)
        {
            Cancelled.TrySetResult();
            throw;
        }
    }

    private static IEnumerable<string> Pieces(string text) => WordPattern().Matches(text).Select(match => match.Value);

    [GeneratedRegex(@"\s*\S+\s?")]
    private static partial Regex WordPattern();
}
