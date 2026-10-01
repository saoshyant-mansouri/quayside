using Quayside.Core.Documents;
using Quayside.Core.Retrieval;
using Xunit.Abstractions;

namespace Quayside.UnitTests.Retrieval;

public sealed class HybridIndexTests(ITestOutputHelper output)
{
    private static HybridIndex OrthogonalIndex() => HybridIndex.Build(
        [
            Fixtures.Document("d1", string.Empty),
            Fixtures.Document("d2", string.Empty),
            Fixtures.Document("d3", string.Empty)
        ],
        [
            Fixtures.Embedded("d1", 0, "alpha", 1, 0, 0, 0),
            Fixtures.Embedded("d1", 1, "beta", 0, 1, 0, 0),
            Fixtures.Embedded("d2", 0, "gamma delta", 0, 0, 1, 0),
            Fixtures.Embedded("d3", 0, "delta", 0, 0, 0, 1)
        ]);

    [Fact]
    public void Counts_reflect_chunks_and_documents()
    {
        var index = OrthogonalIndex();

        Assert.Equal(4, index.ChunkCount);
        Assert.Equal(3, index.DocumentCount);
    }

    [Fact]
    public void Exact_ordering_follows_hand_computed_fusion()
    {
        var index = OrthogonalIndex();

        var result = index.Search(new float[] { 0.8f, 0.6f, 0f, 0f }, "delta", 4);

        Assert.Equal(["d3#0000", "d2#0000", "d1#0000", "d1#0001"], result.Hits.Select(h => h.Chunk.Id));
        Assert.Equal(4, result.Considered);
        Assert.Equal(1.0 / 64 + 1.0 / 61, result.Hits[0].Score, 12);
        Assert.Equal(1.0 / 63 + 1.0 / 62, result.Hits[1].Score, 12);
        Assert.Equal(1.0 / 61, result.Hits[2].Score, 12);
        Assert.Equal(1.0 / 62, result.Hits[3].Score, 12);
        Assert.Equal(["d3", "d2", "d1", "d1"], result.Hits.Select(h => h.Document.Id));
    }

    [Fact]
    public void Chunk_found_by_both_signals_beats_chunks_found_by_one()
    {
        var index = HybridIndex.Build(
            [Fixtures.Document("a", string.Empty), Fixtures.Document("b", string.Empty), Fixtures.Document("c", string.Empty)],
            [
                Fixtures.Embedded("a", 0, "reefer plugs", 1, 0, 0),
                Fixtures.Embedded("b", 0, "dry containers for general goods", 0.9f, 0.43589f, 0),
                Fixtures.Embedded("c", 0, "reefer monitoring dashboards", 0, 0, 1)
            ]);

        var result = index.Search(new float[] { 1f, 0f, 0f }, "reefer", 3);

        Assert.Equal("a#0000", result.Hits[0].Chunk.Id);
        Assert.Equal(1.0 / 61 + 1.0 / 61, result.Hits[0].Score, 12);
    }

    [Fact]
    public void Lexical_only_match_surfaces_when_the_embedding_is_unrelated()
    {
        var index = OrthogonalIndex();

        var result = index.Search(new float[] { 0f, 0f, 0f, 0f }, "gamma", 2);

        Assert.Equal("d2#0000", result.Hits[0].Chunk.Id);
        Assert.Single(result.Hits);
    }

    [Fact]
    public void Near_duplicate_chunks_do_not_both_appear_in_the_top_k()
    {
        var documents = Enumerable.Range(1, 5).Select(n => Fixtures.Document($"d{n}", string.Empty)).ToArray();
        var index = HybridIndex.Build(
            documents,
            [
                Fixtures.Embedded("d1", 0, "one", 1f, 0f, 0f, 0f),
                Fixtures.Embedded("d2", 0, "two", 0.9999f, 0.0141f, 0f, 0f),
                Fixtures.Embedded("d3", 0, "three", 0.3f, 0.954f, 0f, 0f),
                Fixtures.Embedded("d4", 0, "four", 0.25f, 0f, 0.968f, 0f),
                Fixtures.Embedded("d5", 0, "five", 0.2f, 0f, 0f, 0.98f)
            ]);

        var query = new float[] { 1f, 0f, 0f, 0f };

        var top2 = index.Search(query, string.Empty, 2);

        Assert.Equal(["d1#0000", "d3#0000"], top2.Hits.Select(h => h.Chunk.Id));
        Assert.DoesNotContain(top2.Hits, h => h.Chunk.Id == "d2#0000");
    }

    [Fact]
    public void Requesting_more_than_exists_returns_everything_once()
    {
        var result = OrthogonalIndex().Search(new float[] { 1f, 0f, 0f, 0f }, "alpha", 50);

        Assert.Equal(4, result.Hits.Count);
        Assert.Equal(4, result.Hits.Select(h => h.Chunk.Id).Distinct().Count());
    }

    [Fact]
    public void Non_positive_k_and_empty_indexes_return_nothing()
    {
        Assert.Empty(OrthogonalIndex().Search(new float[] { 1f, 0f, 0f, 0f }, "alpha", 0).Hits);

        var empty = HybridIndex.Build([], []);
        var result = empty.Search(ReadOnlyMemory<float>.Empty, "anything", 5);

        Assert.Empty(result.Hits);
        Assert.Equal(0, empty.ChunkCount);
        Assert.Equal(0, empty.DocumentCount);
    }

    [Fact]
    public void Embeddings_are_normalised_so_magnitude_does_not_matter()
    {
        var small = HybridIndex.Build(
            [Fixtures.Document("a", string.Empty), Fixtures.Document("b", string.Empty)],
            [Fixtures.Embedded("a", 0, "x", 1, 0), Fixtures.Embedded("b", 0, "y", 0, 1)]);
        var large = HybridIndex.Build(
            [Fixtures.Document("a", string.Empty), Fixtures.Document("b", string.Empty)],
            [Fixtures.Embedded("a", 0, "x", 500, 0), Fixtures.Embedded("b", 0, "y", 0, 0.001f)]);

        var query = new float[] { 3f, 4f };

        Assert.Equal(
            small.Search(query, string.Empty, 2).Hits.Select(h => (h.Chunk.Id, h.Score)),
            large.Search(query, string.Empty, 2).Hits.Select(h => (h.Chunk.Id, h.Score)));
    }

    [Fact]
    public void Mismatched_dimensions_and_unknown_documents_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => HybridIndex.Build(
            [Fixtures.Document("a", string.Empty)],
            [Fixtures.Embedded("a", 0, "x", 1, 0), Fixtures.Embedded("a", 1, "y", 1, 0, 0)]));
        Assert.Throws<ArgumentException>(() => HybridIndex.Build(
            [Fixtures.Document("a", string.Empty)],
            [Fixtures.Embedded("ghost", 0, "x", 1, 0)]));
        Assert.Throws<ArgumentException>(() => OrthogonalIndex().Search(new float[] { 1f, 0f }, "alpha", 2));
    }

    [Fact]
    public void Elapsed_time_is_measured_and_considered_counts_all_chunks()
    {
        var index = RandomIndex(2000, 64, 11);

        var result = index.Search(RandomVector(64, new Random(5)), "vessel schedule", 8);

        Assert.True(result.ElapsedMs > 0.0);
        Assert.Equal(2000, result.Considered);
        Assert.Equal(8, result.Hits.Count);
    }

    [Fact]
    public void Concurrent_searches_match_serial_results_exactly()
    {
        var index = RandomIndex(1500, 48, 21);
        var queries = Enumerable.Range(0, 300)
            .Select(i => (Vector: RandomVector(48, new Random(1000 + i)), Text: $"vessel{i % 17} port{i % 11} schedule"))
            .ToArray();

        var serial = queries.Select(q => Signature(index.Search(q.Vector, q.Text, 8))).ToArray();
        var parallel = new string[queries.Length];

        Parallel.For(0, queries.Length, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i =>
        {
            parallel[i] = Signature(index.Search(queries[i].Vector, queries[i].Text, 8));
        });

        Assert.Equal(serial, parallel);
    }

    [Fact]
    public void Corpus_scale_search_latency_is_reported()
    {
        const int chunks = 4000;
        const int dimensions = 1536;
        var index = RandomIndex(chunks, dimensions, 99);
        var random = new Random(3);
        var queryVectors = Enumerable.Range(0, 300).Select(_ => RandomVector(dimensions, random)).ToArray();

        for (var i = 0; i < 50; i++)
        {
            index.Search(queryVectors[i], "vessel schedule port", 8);
        }

        var timings = queryVectors
            .Select(q => index.Search(q, "vessel schedule port " + q[0], 8).ElapsedMs)
            .Order()
            .ToArray();

        var p50 = timings[timings.Length / 2];
        var p99 = timings[(int)(timings.Length * 0.99)];
        output.WriteLine($"{chunks} chunks x {dimensions} dims, k=8: p50={p50:F3} ms p99={p99:F3} ms");

        Assert.True(p50 < 50.0, $"p50 {p50:F3} ms is unreasonably slow");
    }

    private static string Signature(RetrievalResult result) =>
        string.Join("|", result.Hits.Select(h => $"{h.Chunk.Id}:{h.Score:R}"));

    private static float[] RandomVector(int dimensions, Random random)
    {
        var vector = new float[dimensions];
        for (var i = 0; i < dimensions; i++)
        {
            vector[i] = (float)(random.NextDouble() * 2 - 1);
        }

        return vector;
    }

    private static HybridIndex RandomIndex(int chunkCount, int dimensions, int seed)
    {
        var random = new Random(seed);
        var documentCount = Math.Max(1, chunkCount / 8);
        var documents = Enumerable.Range(0, documentCount)
            .Select(d => Fixtures.Document($"doc{d}", string.Empty, $"Document {d}"))
            .ToArray();
        var chunks = new List<EmbeddedChunk>(chunkCount);
        for (var i = 0; i < chunkCount; i++)
        {
            var text = $"vessel{i % 17} port{i % 11} schedule terminal{i % 29} container{i % 7} voyage";
            chunks.Add(new EmbeddedChunk(
                new Chunk($"doc{i % documentCount}#{i:D5}", $"doc{i % documentCount}", i, text, TokenEstimator.Estimate(text)),
                RandomVector(dimensions, random)));
        }

        return HybridIndex.Build(documents, chunks);
    }
}
