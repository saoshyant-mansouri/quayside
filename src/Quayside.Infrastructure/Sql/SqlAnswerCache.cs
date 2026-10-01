using Quayside.Core;

namespace Quayside.Infrastructure.Sql;

public sealed class SqlAnswerCache(IAnswerCacheStore store) : IAnswerCache
{
    public const int MaxQuestionLength = 2000;

    public async Task<CachedAnswer?> TryGetAsync(ReadOnlyMemory<float> questionEmbedding, CancellationToken ct)
    {
        var candidate = await store.NearestAsync(questionEmbedding, ct).ConfigureAwait(false);
        if (candidate is null || !AnswerCachePolicy.IsHit(candidate.Similarity))
            return null;

        await store.RecordHitAsync(candidate.Id, ct).ConfigureAwait(false);
        return new CachedAnswer(candidate.Question, candidate.Answer, candidate.CitationsJson, candidate.Similarity);
    }

    public Task StoreAsync(string question, ReadOnlyMemory<float> embedding, string answer, string citationsJson, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(citationsJson);

        var stored = question.Length > MaxQuestionLength ? question[..MaxQuestionLength] : question;
        return store.SaveAsync(AnswerCachePolicy.HashQuestion(question), stored, embedding, answer, citationsJson, ct);
    }
}
