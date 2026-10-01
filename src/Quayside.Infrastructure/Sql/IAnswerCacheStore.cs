namespace Quayside.Infrastructure.Sql;

public sealed record AnswerCandidate(long Id, string Question, string Answer, string CitationsJson, double Similarity);

public interface IAnswerCacheStore
{
    Task<AnswerCandidate?> NearestAsync(ReadOnlyMemory<float> questionEmbedding, CancellationToken cancellationToken);

    Task RecordHitAsync(long id, CancellationToken cancellationToken);

    Task SaveAsync(
        byte[] questionHash,
        string question,
        ReadOnlyMemory<float> embedding,
        string answer,
        string citationsJson,
        CancellationToken cancellationToken);
}
