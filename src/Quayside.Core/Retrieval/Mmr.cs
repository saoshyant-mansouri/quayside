using System.Buffers;
using System.Numerics.Tensors;

namespace Quayside.Core.Retrieval;

public static class Mmr
{
    public const double DefaultLambda = 0.7;

    public static int Select(
        ReadOnlySpan<int> candidates,
        ReadOnlySpan<double> relevance,
        ReadOnlySpan<float> vectors,
        int dimensions,
        double lambda,
        Span<int> selected)
    {
        var limit = Math.Min(candidates.Length, selected.Length);
        if (limit == 0)
        {
            return 0;
        }

        var maxSimilarity = ArrayPool<double>.Shared.Rent(candidates.Length);
        var taken = ArrayPool<bool>.Shared.Rent(candidates.Length);
        try
        {
            maxSimilarity.AsSpan(0, candidates.Length).Clear();
            taken.AsSpan(0, candidates.Length).Clear();

            for (var round = 0; round < limit; round++)
            {
                var best = -1;
                var bestScore = double.NegativeInfinity;
                for (var i = 0; i < candidates.Length; i++)
                {
                    if (taken[i])
                    {
                        continue;
                    }

                    var score = lambda * relevance[i] - (1.0 - lambda) * maxSimilarity[i];
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = i;
                    }
                }

                taken[best] = true;
                selected[round] = candidates[best];

                if (round == limit - 1)
                {
                    break;
                }

                var chosen = vectors.Slice(candidates[best] * dimensions, dimensions);
                for (var i = 0; i < candidates.Length; i++)
                {
                    if (taken[i])
                    {
                        continue;
                    }

                    var similarity = TensorPrimitives.Dot(chosen, vectors.Slice(candidates[i] * dimensions, dimensions));
                    if (similarity > maxSimilarity[i])
                    {
                        maxSimilarity[i] = similarity;
                    }
                }
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(maxSimilarity);
            ArrayPool<bool>.Shared.Return(taken);
        }

        return limit;
    }
}
