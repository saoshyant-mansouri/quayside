using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Quayside.Api.Contract;
using Quayside.Api.Conversations;
using Quayside.Api.Streaming;
using Quayside.Api.Tools;
using Quayside.Core;
using Quayside.Core.Grounding;

namespace Quayside.Api.Orchestration;

public sealed class ChatOrchestrator(
    ToolCallingChat chat,
    IEmbeddingGenerator<string, Embedding<float>> embeddings,
    IAnswerCache cache,
    ConversationStore conversations,
    ToolRunner toolRunner,
    KnowledgeSearch knowledge,
    DatabaseQuery database,
    OperationalLookups operations,
    IOptions<CacheOptions> cacheOptions,
    ILogger<ChatOrchestrator> logger)
{
    public const string GenericFailure = "Something went wrong while answering. Please try again.";

    public async Task RunAsync(ChatRequest request, IEventSink sink, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            await AnswerAsync(request.Message!.Trim(), request.ConversationId, sink, started, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Chat turn failed");
            await TryEmitErrorAsync(sink);
        }
    }

    private async Task AnswerAsync(string question, string? requestedConversationId, IEventSink sink, long started, CancellationToken ct)
    {
        var conversation = conversations.Open(requestedConversationId);
        var embedding = (await embeddings.GenerateAsync([question], cancellationToken: ct))[0].Vector;

        if (conversation.Turns.Count == 0 && await TryServeFromCacheAsync(question, embedding, conversation, sink, started, ct))
        {
            return;
        }

        var turn = new TurnContext(sink, question, embedding);
        await StreamModelAsync(turn, conversation, ct);
        var report = await FinaliseAsync(turn, ct);

        var answer = turn.Gate.Emitted;
        conversations.Append(conversation.Id, new ChatTurn(question, answer));
        await StoreInCacheAsync(turn, conversation, report, answer, ct);
        await sink.EmitAsync(
            new DoneEvent(conversation.Id, ElapsedMs(started), false, turn.Usage.ToPayload(), new GroundingPayload(report.Cited, report.Uncited)),
            ct);
    }

    private async Task StreamModelAsync(TurnContext turn, ConversationSnapshot conversation, CancellationToken ct)
    {
        var kernel = new Kernel();
        kernel.Plugins.AddFromObject(new QuaysideTools(turn, toolRunner, knowledge, database, operations), QuaysideTools.PluginName);

        var history = new ChatHistory(Prompts.System);
        foreach (var previous in conversation.Turns)
        {
            history.AddUserMessage(previous.User);
            history.AddAssistantMessage(previous.Assistant);
        }

        history.AddUserMessage(turn.Question);

        var settings = new PromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: true) };
        await foreach (var update in chat.Service.GetStreamingChatMessageContentsAsync(history, settings, kernel, ct))
        {
            if (update.Metadata?.TryGetValue("Usage", out var usage) == true && usage is UsageContent usageContent)
            {
                turn.Usage.Add(usageContent.Details);
            }

            if (!string.IsNullOrEmpty(update.Content))
            {
                await turn.Gate.WriteAsync(update.Content, ct);
            }
        }
    }

    private static async Task<GroundingReport> FinaliseAsync(TurnContext turn, CancellationToken ct)
    {
        var citations = turn.Sources.Citations;

        if (!turn.Gate.IsOpen)
        {
            var report = CitationEnforcer.Check(turn.Gate.Pending, citations);
            if (report.Uncited == 0)
            {
                await turn.Gate.ReleasePendingAsync(ct);
                return report;
            }

            var refusal = Prompts.Ungrounded(turn.Question);
            await turn.Gate.ReplacePendingAsync(refusal, ct);
            return CitationEnforcer.Check(refusal, citations);
        }

        var written = CitationEnforcer.Check(turn.Gate.Emitted, citations);
        if (turn.OperationalEvidence)
        {
            return citations.Count == 0 ? new GroundingReport(0, 0, []) : written;
        }

        if (written.Uncited > 0)
        {
            await turn.Gate.WriteAsync(Prompts.UnsourcedNote, ct);
        }

        return written;
    }

    private async Task<bool> TryServeFromCacheAsync(
        string question,
        ReadOnlyMemory<float> embedding,
        ConversationSnapshot conversation,
        IEventSink sink,
        long started,
        CancellationToken ct)
    {
        CachedAnswer? hit;
        try
        {
            hit = await cache.TryGetAsync(embedding, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Answer cache lookup failed ({ExceptionType})", ex.GetType().Name);
            return false;
        }

        if (hit is null || hit.Similarity < cacheOptions.Value.MinSimilarity)
        {
            return false;
        }

        var payloads = JsonSerializer.Deserialize<List<CitationPayload>>(hit.CitationsJson, SseJson.Options) ?? [];
        if (payloads.Count > 0)
        {
            await sink.EmitAsync(new CitationsEvent(payloads), ct);
        }

        await sink.EmitAsync(new TokenEvent(hit.Answer), ct);
        var citations = payloads.Select(p => new Citation(p.N, string.Empty, p.Title, p.Url, p.Source, p.PublishedAt)).ToArray();
        var report = CitationEnforcer.Check(hit.Answer, citations);

        conversations.Append(conversation.Id, new ChatTurn(question, hit.Answer));
        await sink.EmitAsync(
            new DoneEvent(conversation.Id, ElapsedMs(started), true, new UsagePayload(0, 0), new GroundingPayload(report.Cited, report.Uncited)),
            ct);
        return true;
    }

    private async Task StoreInCacheAsync(TurnContext turn, ConversationSnapshot conversation, GroundingReport report, string answer, CancellationToken ct)
    {
        var cacheable = conversation.Turns.Count == 0
            && turn.Sources.Citations.Count > 0
            && !turn.OperationalEvidence
            && report.Cited > 0
            && report.Uncited == 0;
        if (!cacheable)
        {
            return;
        }

        var payloads = turn.Sources.Citations
            .Select(c => new CitationPayload(c.Marker, c.Title, c.Url, c.Source, c.PublishedLabel))
            .ToArray();
        try
        {
            await cache.StoreAsync(turn.Question, turn.QuestionEmbedding, answer, JsonSerializer.Serialize(payloads, SseJson.Options), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Answer cache store failed ({ExceptionType})", ex.GetType().Name);
        }
    }

    private static async Task TryEmitErrorAsync(IEventSink sink)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await sink.EmitAsync(new ErrorEvent(GenericFailure), timeout.Token);
        }
        catch (Exception)
        {
        }
    }

    private static long ElapsedMs(long started) => (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
}
