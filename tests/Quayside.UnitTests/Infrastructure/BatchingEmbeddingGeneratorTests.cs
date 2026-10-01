using System.Diagnostics;
using Microsoft.Extensions.AI;
using Quayside.Infrastructure.Ai;
using Quayside.Infrastructure.Telemetry;

namespace Quayside.UnitTests.Infrastructure;

public sealed class BatchingEmbeddingGeneratorTests
{
    [Fact]
    public async Task Splits_into_batches_no_larger_than_the_limit()
    {
        var inner = new FakeEmbeddingGenerator();
        var generator = new BatchingEmbeddingGenerator(inner, batchSize: 10, concurrency: 1);

        await generator.GenerateAsync(Numbers(25));

        Assert.Equal([10, 10, 5], inner.BatchSizes.OrderByDescending(s => s).ToArray());
        Assert.Equal(3, inner.BatchSizes.Count);
    }

    [Fact]
    public async Task Preserves_input_order_even_when_batches_finish_out_of_order()
    {
        var inner = new FakeEmbeddingGenerator { DelayFor = batchStart => TimeSpan.FromMilliseconds(batchStart == 0 ? 80 : 1) };
        var generator = new BatchingEmbeddingGenerator(inner, batchSize: 7, concurrency: 4);
        var inputs = Numbers(100);

        var result = await generator.GenerateAsync(inputs);

        Assert.Equal(100, result.Count);
        for (var i = 0; i < inputs.Count; i++)
            Assert.Equal(float.Parse(inputs[i]), result[i].Vector.Span[0]);
    }

    [Fact]
    public async Task Aggregates_token_usage_across_batches()
    {
        var generator = new BatchingEmbeddingGenerator(new FakeEmbeddingGenerator(), batchSize: 10);

        var result = await generator.GenerateAsync(Numbers(25));

        Assert.Equal(25, result.Usage!.InputTokenCount);
        Assert.Equal(25, result.Usage.TotalTokenCount);
    }

    [Fact]
    public async Task Empty_input_makes_no_service_call()
    {
        var inner = new FakeEmbeddingGenerator();
        var generator = new BatchingEmbeddingGenerator(inner);

        var result = await generator.GenerateAsync(Array.Empty<string>());

        Assert.Empty(result);
        Assert.Empty(inner.BatchSizes);
    }

    [Fact]
    public async Task Blank_input_is_rejected_before_any_call()
    {
        var inner = new FakeEmbeddingGenerator();
        var generator = new BatchingEmbeddingGenerator(inner);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => generator.GenerateAsync(["1", " "]));
        Assert.Empty(inner.BatchSizes);
    }

    [Fact]
    public async Task A_short_response_is_an_error_not_a_silent_misalignment()
    {
        var generator = new BatchingEmbeddingGenerator(new FakeEmbeddingGenerator { DropLast = true }, batchSize: 10);

        await Assert.ThrowsAsync<InvalidOperationException>(() => generator.GenerateAsync(Numbers(10)));
    }

    [Fact]
    public async Task Records_the_input_token_count_on_its_span()
    {
        var captured = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TelemetryNames.AiSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = captured.Add
        };
        ActivitySource.AddActivityListener(listener);
        var generator = new BatchingEmbeddingGenerator(new FakeEmbeddingGenerator(), batchSize: 10);

        await generator.GenerateAsync(Numbers(25));

        var span = Assert.Single(captured, a => a.OperationName == "embeddings.generate");
        Assert.Equal(25L, span.GetTagItem(TelemetryNames.InputTokens));
        Assert.Equal(3, span.GetTagItem("quayside.embedding.batches"));
    }

    [Fact]
    public void Rejects_non_positive_limits()
    {
        var inner = new FakeEmbeddingGenerator();

        Assert.Throws<ArgumentOutOfRangeException>(() => new BatchingEmbeddingGenerator(inner, batchSize: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BatchingEmbeddingGenerator(inner, concurrency: 0));
    }

    private static List<string> Numbers(int count) => Enumerable.Range(0, count).Select(i => i.ToString()).ToList();

    private sealed class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        private readonly List<int> batchSizes = [];

        public IReadOnlyList<int> BatchSizes
        {
            get
            {
                lock (batchSizes) return batchSizes.ToArray();
            }
        }

        public Func<int, TimeSpan>? DelayFor { get; init; }

        public bool DropLast { get; init; }

        public EmbeddingGeneratorMetadata Metadata { get; } = new("fake");

        public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var inputs = values.ToArray();
            lock (batchSizes) batchSizes.Add(inputs.Length);

            if (DelayFor is not null)
                await Task.Delay(DelayFor(int.Parse(inputs[0])), cancellationToken);

            var embeddings = inputs.Select(v => new Embedding<float>(new[] { float.Parse(v), 0f })).ToList();
            if (DropLast) embeddings.RemoveAt(embeddings.Count - 1);

            return new GeneratedEmbeddings<Embedding<float>>(embeddings)
            {
                Usage = new UsageDetails { InputTokenCount = inputs.Length, TotalTokenCount = inputs.Length }
            };
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
