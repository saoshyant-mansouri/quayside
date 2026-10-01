using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Quayside.Core;
using Quayside.Core.Retrieval;
using Quayside.Core.Sql;

namespace Quayside.Api.Hosting;

public sealed class IndexHydrator(
    IChunkStore chunkStore,
    ISchemaCatalog schemaCatalog,
    IEmbeddingGenerator<string, Embedding<float>> embeddings,
    Hydrated<CorpusIndex> corpus,
    Hydrated<SchemaRetriever> schema,
    IOptions<HydrationOptions> options,
    ILogger<IndexHydrator> logger) : BackgroundService
{
    private const int EmbeddingBatchSize = 64;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        await Task.WhenAll(HydrateCorpusAsync(stoppingToken), HydrateSchemaAsync(stoppingToken));
    }

    private Task HydrateCorpusAsync(CancellationToken ct) =>
        RetryAsync("corpus index", async token =>
        {
            var documents = await chunkStore.LoadDocumentsAsync(token);
            var chunks = await chunkStore.LoadChunksAsync(token);
            var index = HybridIndex.Build(documents, chunks);
            DateTimeOffset? capturedAt = documents.Count == 0 ? null : documents.Max(d => d.CapturedAt);
            corpus.Publish(new CorpusIndex(index, capturedAt));
            logger.LogInformation("Corpus index ready: {Chunks} chunks, {Documents} documents", index.ChunkCount, index.DocumentCount);
        }, ct);

    private Task HydrateSchemaAsync(CancellationToken ct) =>
        RetryAsync("schema catalog", async token =>
        {
            var cards = await schemaCatalog.LoadCardsAsync(token);
            var embedded = await EmbedCardsAsync(cards, token);
            schema.Publish(new SchemaRetriever(embedded));
            logger.LogInformation("Schema retriever ready: {Tables} tables", cards.Count);
        }, ct);

    private async Task<IReadOnlyList<EmbeddedTableCard>> EmbedCardsAsync(IReadOnlyList<TableCard> cards, CancellationToken ct)
    {
        var embedded = new List<EmbeddedTableCard>(cards.Count);
        try
        {
            foreach (var batch in cards.Chunk(EmbeddingBatchSize))
            {
                var vectors = await embeddings.GenerateAsync(batch.Select(card => card.Card), cancellationToken: ct);
                for (var i = 0; i < batch.Length; i++)
                {
                    embedded.Add(new EmbeddedTableCard(batch[i], vectors[i].Vector));
                }
            }

            return embedded;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Table card embedding failed ({ExceptionType}); schema retrieval falls back to lexical only", ex.GetType().Name);
            return cards.Select(card => new EmbeddedTableCard(card, ReadOnlyMemory<float>.Empty)).ToArray();
        }
    }

    private async Task RetryAsync(string stage, Func<CancellationToken, Task> hydrate, CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(Math.Max(options.Value.RetryDelaySeconds, 0));
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await hydrate(ct);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Hydrating the {Stage} failed ({ExceptionType}); retrying in {DelaySeconds}s", stage, ex.GetType().Name, delay.TotalSeconds);
                await Task.Delay(delay, ct).ContinueWith(_ => { }, CancellationToken.None);
            }
        }
    }
}
