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
    private readonly ConcurrentQueue<string> toolResults = new();
    private int sqlCalls;

    public ModelBehaviour Behaviour { get; set; }

    public bool FallBackToWeb { get; set; }

    public IReadOnlyList<string> ToolResults => toolResults.ToArray();

    public IReadOnlyList<string> ToolNames { get; private set; } = [];

    public string SystemPrompt { get; private set; } = string.Empty;

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

        ToolNames = options?.Tools?.Select(t => t.Name).ToArray() ?? [];
        SystemPrompt = conversation.FirstOrDefault(m => m.Role == ChatRole.System)?.Text ?? SystemPrompt;
        var lastResult = conversation[^1].Contents.OfType<FunctionResultContent>().FirstOrDefault()?.Result?.ToString();
        if (lastResult is not null)
        {
            toolResults.Enqueue(lastResult);
        }

        var afterTool = lastResult is not null;
        if (FallBackToWeb && SearchedCorpusWithoutWeb(conversation, lastResult) && options?.Tools?.FirstOrDefault(t => t.Name.EndsWith("search_web", StringComparison.Ordinal)) is { } web)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent(Guid.NewGuid().ToString("N"), web.Name, new Dictionary<string, object?> { ["query"] = "MSC office Turin Italy" })]);
            yield break;
        }

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

    private static bool SearchedCorpusWithoutWeb(List<ChatMessage> conversation, string? lastResult) =>
        lastResult is not null
        && (lastResult.StartsWith("NO_SOURCES", StringComparison.Ordinal) || lastResult.StartsWith("Sources.", StringComparison.Ordinal))
        && !conversation.SelectMany(m => m.Contents).OfType<FunctionCallContent>().Any(c => c.Name.EndsWith("search_web", StringComparison.Ordinal));

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
