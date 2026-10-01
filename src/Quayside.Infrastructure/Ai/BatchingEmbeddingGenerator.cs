using System.Diagnostics;
using Microsoft.Extensions.AI;
using Quayside.Infrastructure.Telemetry;

namespace Quayside.Infrastructure.Ai;

public sealed class BatchingEmbeddingGenerator : DelegatingEmbeddingGenerator<string, Embedding<float>>
{
    public const int DefaultBatchSize = 32;
    public const int DefaultConcurrency = 2;

    private readonly int batchSize;
    private readonly int concurrency;

    public BatchingEmbeddingGenerator(
        IEmbeddingGenerator<string, Embedding<float>> inner,
        int batchSize = DefaultBatchSize,
        int concurrency = DefaultConcurrency)
        : base(inner)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(concurrency, 1);
        this.batchSize = batchSize;
        this.concurrency = concurrency;
    }

    public override async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);

        var inputs = values.ToArray();
        foreach (var input in inputs)
            ArgumentException.ThrowIfNullOrWhiteSpace(input, nameof(values));

        using var activity = TelemetryNames.Ai.StartActivity("embeddings.generate", ActivityKind.Internal);
        activity?.SetTag("gen_ai.operation.name", "embeddings");
        activity?.SetTag("quayside.embedding.inputs", inputs.Length);

        if (inputs.Length == 0)
            return [];

        var batches = (inputs.Length + batchSize - 1) / batchSize;
        activity?.SetTag("quayside.embedding.batches", batches);

        var results = new Embedding<float>[inputs.Length];
        long inputTokens = 0;
        long totalTokens = 0;

        var parallel = new ParallelOptions { MaxDegreeOfParallelism = concurrency, CancellationToken = cancellationToken };
        await Parallel.ForAsync(0, batches, parallel, async (batch, token) =>
        {
            var start = batch * batchSize;
            var length = Math.Min(batchSize, inputs.Length - start);
            var slice = new ArraySegment<string>(inputs, start, length);

            var generated = await InnerGenerator.GenerateAsync(slice, options, token).ConfigureAwait(false);
            if (generated.Count != length)
                throw new InvalidOperationException($"Embedding service returned {generated.Count} vectors for {length} inputs.");

            for (var i = 0; i < length; i++) results[start + i] = generated[i];
            Interlocked.Add(ref inputTokens, generated.Usage?.InputTokenCount ?? 0);
            Interlocked.Add(ref totalTokens, generated.Usage?.TotalTokenCount ?? 0);
        }).ConfigureAwait(false);

        activity?.SetTag(TelemetryNames.InputTokens, inputTokens);

        return new GeneratedEmbeddings<Embedding<float>>(results)
        {
            Usage = new UsageDetails { InputTokenCount = inputTokens, TotalTokenCount = totalTokens }
        };
    }
}
