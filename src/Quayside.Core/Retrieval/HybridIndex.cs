using System.Diagnostics;
using System.Numerics.Tensors;
using Quayside.Core.Documents;

namespace Quayside.Core.Retrieval;

public sealed class HybridIndex
{
    public const int MinimumDepth = 50;
    public const int DepthPerHit = 5;
    public const int MinimumPool = 20;
    public const int PoolPerHit = 3;

    private readonly float[] vectors;
    private readonly int dimensions;
    private readonly Chunk[] chunks;
    private readonly Document[] owners;
    private readonly Bm25Index lexical;

    private HybridIndex(float[] vectors, int dimensions, Chunk[] chunks, Document[] owners, Bm25Index lexical, int documentCount)
    {
        this.vectors = vectors;
        this.dimensions = dimensions;
        this.chunks = chunks;
        this.owners = owners;
        this.lexical = lexical;
        DocumentCount = documentCount;
    }

    public int ChunkCount => chunks.Length;

    public int DocumentCount { get; }

    public static HybridIndex Build(IReadOnlyList<Document> documents, IReadOnlyList<EmbeddedChunk> chunks)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(chunks);

        var byId = new Dictionary<string, Document>(documents.Count, StringComparer.Ordinal);
        foreach (var document in documents)
        {
            byId[document.Id] = document;
        }

        var dimensions = chunks.Count == 0 ? 0 : chunks[0].Embedding.Length;
        var total = (long)dimensions * chunks.Count;
        if (total > Array.MaxLength)
        {
            throw new ArgumentException("Embedding buffer would exceed the maximum array length.", nameof(chunks));
        }

        var buffer = new float[total];
        var chunkArray = new Chunk[chunks.Count];
        var owners = new Document[chunks.Count];
        var texts = new string[chunks.Count];

        for (var i = 0; i < chunks.Count; i++)
        {
            var embedded = chunks[i];
            if (embedded.Embedding.Length != dimensions)
            {
                throw new ArgumentException($"Chunk '{embedded.Chunk.Id}' has {embedded.Embedding.Length} dimensions, expected {dimensions}.", nameof(chunks));
            }

            if (!byId.TryGetValue(embedded.Chunk.DocumentId, out var owner))
            {
                throw new ArgumentException($"Chunk '{embedded.Chunk.Id}' references unknown document '{embedded.Chunk.DocumentId}'.", nameof(chunks));
            }

            var slot = buffer.AsSpan(i * dimensions, dimensions);
            embedded.Embedding.Span.CopyTo(slot);
            Normalise(slot);

            chunkArray[i] = embedded.Chunk;
            owners[i] = owner;
            texts[i] = string.Concat(owner.Title, " ", embedded.Chunk.Text);
        }

        return new HybridIndex(buffer, dimensions, chunkArray, owners, Bm25Index.Build(texts), byId.Count);
    }

    public RetrievalResult Search(ReadOnlyMemory<float> queryEmbedding, string queryText, int k)
    {
        var started = Stopwatch.GetTimestamp();
        var count = chunks.Length;

        if (k <= 0 || count == 0)
        {
            return new RetrievalResult([], 0, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }

        if (queryEmbedding.Length != dimensions)
        {
            throw new ArgumentException($"Query has {queryEmbedding.Length} dimensions, index has {dimensions}.", nameof(queryEmbedding));
        }

        var depth = Math.Min(count, Math.Max(MinimumDepth, k * DepthPerHit));
        var poolSize = Math.Min(count, Math.Max(MinimumPool, k * PoolPerHit));
        var hitCount = Math.Min(k, poolSize);

        using var buffers = SearchBuffers.Rent(dimensions, count, depth, poolSize, hitCount);

        var query = buffers.Query.AsSpan(0, dimensions);
        queryEmbedding.Span.CopyTo(query);
        var queryIsZero = !Normalise(query);

        var dense = buffers.Dense.AsSpan(0, count);
        var lexicalScores = buffers.Lexical.AsSpan(0, count);
        var fused = buffers.Fused.AsSpan(0, count);

        var denseCount = 0;
        if (!queryIsZero)
        {
            ScoreDense(query, dense);
            denseCount = TopK.Select(dense, double.NegativeInfinity, buffers.DenseTop.AsSpan(0, depth));
        }

        lexicalScores.Clear();
        lexical.Score(queryText ?? string.Empty, lexicalScores);
        var lexicalCount = TopK.Select(lexicalScores, 0.0, buffers.LexicalTop.AsSpan(0, depth));

        fused.Clear();
        var candidates = buffers.Candidates;
        var candidateCount = 0;

        var denseRanking = buffers.DenseTop.AsSpan(0, denseCount);
        ReciprocalRankFusion.Accumulate(denseRanking, fused);
        denseRanking.CopyTo(candidates);
        candidateCount += denseCount;

        var lexicalRanking = buffers.LexicalTop.AsSpan(0, lexicalCount);
        foreach (var id in lexicalRanking)
        {
            if (fused[id] == 0.0)
            {
                candidates[candidateCount++] = id;
            }
        }

        ReciprocalRankFusion.Accumulate(lexicalRanking, fused);

        var ordered = candidates.AsSpan(0, candidateCount);
        ordered.Sort(new FusedComparer(buffers.Fused));
        var pool = ordered[..Math.Min(poolSize, candidateCount)];

        var relevance = buffers.Relevance.AsSpan(0, pool.Length);
        NormaliseRelevance(pool, fused, relevance);

        var selected = buffers.Selected.AsSpan(0, Math.Min(hitCount, pool.Length));
        var selectedCount = Mmr.Select(pool, relevance, vectors, dimensions, Mmr.DefaultLambda, selected);

        var hits = new ScoredChunk[selectedCount];
        var topCosine = 0.0;
        for (var i = 0; i < selectedCount; i++)
        {
            var id = selected[i];
            var cosine = queryIsZero ? 0.0 : dense[id];
            if (cosine > topCosine)
            {
                topCosine = cosine;
            }

            hits[i] = new ScoredChunk(chunks[id], owners[id], fused[id]) { Cosine = cosine };
        }

        return new RetrievalResult(hits, count, Stopwatch.GetElapsedTime(started).TotalMilliseconds)
        {
            TopCosine = topCosine,
        };
    }

    private static void NormaliseRelevance(ReadOnlySpan<int> pool, ReadOnlySpan<double> fused, Span<double> relevance)
    {
        if (pool.Length == 0)
        {
            return;
        }

        var high = fused[pool[0]];
        var low = fused[pool[^1]];
        var range = high - low;
        for (var i = 0; i < pool.Length; i++)
        {
            relevance[i] = range > 0.0 ? (fused[pool[i]] - low) / range : 1.0;
        }
    }

    private void ScoreDense(ReadOnlySpan<float> query, Span<double> scores)
    {
        var all = vectors.AsSpan();
        for (var i = 0; i < scores.Length; i++)
        {
            scores[i] = TensorPrimitives.Dot(all.Slice(i * dimensions, dimensions), query);
        }
    }

    private static bool Normalise(Span<float> vector)
    {
        var norm = TensorPrimitives.Norm(vector);
        if (norm <= 0f || !float.IsFinite(norm))
        {
            return false;
        }

        TensorPrimitives.Divide(vector, norm, vector);
        return true;
    }
}
