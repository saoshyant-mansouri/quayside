using System.Text;
using Microsoft.Extensions.AI;

namespace Quayside.Api.Offline;

public sealed class HashingEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public const int Dimensions = 512;

    public const double SuggestedMinTopCosine = 0.12;

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var result = new GeneratedEmbeddings<Embedding<float>>();
        foreach (var value in values)
        {
            result.Add(new Embedding<float>(Embed(value)));
        }

        return Task.FromResult(result);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }

    public static float[] Embed(string text)
    {
        var vector = new float[Dimensions];
        foreach (var word in Words(text))
        {
            vector[(int)(Hash(word) % Dimensions)] += 1f;
        }

        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = vector[i] > 0f ? 1f + MathF.Log(vector[i]) : 0f;
        }

        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        if (norm > 0f)
        {
            for (var i = 0; i < vector.Length; i++)
            {
                vector[i] /= norm;
            }
        }

        return vector;
    }

    private static uint Hash(string word)
    {
        var hash = 2166136261u;
        foreach (var c in word)
        {
            hash = (hash ^ c) * 16777619u;
        }

        return hash;
    }

    private static IEnumerable<string> Words(string text)
    {
        var current = new StringBuilder();
        foreach (var c in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                current.Append(c);
                continue;
            }

            if (current.Length > 2)
            {
                yield return current.ToString();
            }

            current.Clear();
        }

        if (current.Length > 2)
        {
            yield return current.ToString();
        }
    }
}
