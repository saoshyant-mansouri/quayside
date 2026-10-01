using System.Buffers;

namespace Quayside.Core.Retrieval;

internal readonly struct SearchBuffers : IDisposable
{
    private SearchBuffers(
        float[] query,
        double[] dense,
        double[] lexical,
        double[] fused,
        int[] denseTop,
        int[] lexicalTop,
        int[] candidates,
        double[] relevance,
        int[] selected)
    {
        Query = query;
        Dense = dense;
        Lexical = lexical;
        Fused = fused;
        DenseTop = denseTop;
        LexicalTop = lexicalTop;
        Candidates = candidates;
        Relevance = relevance;
        Selected = selected;
    }

    public float[] Query { get; }

    public double[] Dense { get; }

    public double[] Lexical { get; }

    public double[] Fused { get; }

    public int[] DenseTop { get; }

    public int[] LexicalTop { get; }

    public int[] Candidates { get; }

    public double[] Relevance { get; }

    public int[] Selected { get; }

    public static SearchBuffers Rent(int dimensions, int chunkCount, int depth, int poolSize, int hits) =>
        new(
            ArrayPool<float>.Shared.Rent(dimensions),
            ArrayPool<double>.Shared.Rent(chunkCount),
            ArrayPool<double>.Shared.Rent(chunkCount),
            ArrayPool<double>.Shared.Rent(chunkCount),
            ArrayPool<int>.Shared.Rent(depth),
            ArrayPool<int>.Shared.Rent(depth),
            ArrayPool<int>.Shared.Rent(depth * 2),
            ArrayPool<double>.Shared.Rent(poolSize),
            ArrayPool<int>.Shared.Rent(hits));

    public void Dispose()
    {
        ArrayPool<float>.Shared.Return(Query);
        ArrayPool<double>.Shared.Return(Dense);
        ArrayPool<double>.Shared.Return(Lexical);
        ArrayPool<double>.Shared.Return(Fused);
        ArrayPool<int>.Shared.Return(DenseTop);
        ArrayPool<int>.Shared.Return(LexicalTop);
        ArrayPool<int>.Shared.Return(Candidates);
        ArrayPool<double>.Shared.Return(Relevance);
        ArrayPool<int>.Shared.Return(Selected);
    }
}
