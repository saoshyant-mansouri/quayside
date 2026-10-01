using System.Numerics.Tensors;
using Quayside.Core;

namespace Quayside.Api.Offline;

public sealed class InMemoryAnswerCache : IAnswerCache
{
    private readonly Lock gate = new();
    private readonly List<Entry> entries = [];

    public Task<CachedAnswer?> TryGetAsync(ReadOnlyMemory<float> questionEmbedding, CancellationToken ct)
    {
        lock (gate)
        {
            var best = entries
                .Where(entry => entry.Embedding.Length == questionEmbedding.Length)
                .Select(entry => (entry, similarity: TensorPrimitives.CosineSimilarity(entry.Embedding.Span, questionEmbedding.Span)))
                .OrderByDescending(pair => pair.similarity)
                .FirstOrDefault();
            return Task.FromResult<CachedAnswer?>(best.entry is null
                ? null
                : new CachedAnswer(best.entry.Question, best.entry.Answer, best.entry.CitationsJson, best.similarity));
        }
    }

    public Task StoreAsync(string question, ReadOnlyMemory<float> embedding, string answer, string citationsJson, CancellationToken ct)
    {
        lock (gate)
        {
            entries.Add(new Entry(question, embedding.ToArray(), answer, citationsJson));
        }

        return Task.CompletedTask;
    }

    private sealed record Entry(string Question, ReadOnlyMemory<float> Embedding, string Answer, string CitationsJson);
}
