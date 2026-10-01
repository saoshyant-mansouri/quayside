using Microsoft.Data.SqlClient;
using Quayside.Infrastructure.Telemetry;

namespace Quayside.Infrastructure.Sql;

public sealed class SqlAnswerCacheStore(SqlDatabase database) : IAnswerCacheStore
{
    private const int DuplicateKey = 2601;
    private const int UniqueConstraint = 2627;

    private const string Nearest = """
        SELECT TOP (1) Id, Question, Answer, CitationsJson,
               VECTOR_DISTANCE('cosine', QuestionEmbedding, @embedding) AS Distance
        FROM dbo.AnswerCache
        ORDER BY Distance
        """;

    private const string RecordHit = """
        UPDATE dbo.AnswerCache SET HitCount = HitCount + 1, LastHitAt = SYSDATETIMEOFFSET() WHERE Id = @id
        """;

    private const string Save = """
        UPDATE dbo.AnswerCache
        SET Question = @question, QuestionEmbedding = @embedding, Answer = @answer,
            CitationsJson = @citations, CreatedAt = SYSDATETIMEOFFSET()
        WHERE QuestionHash = @hash;

        IF @@ROWCOUNT = 0
            INSERT dbo.AnswerCache (QuestionHash, Question, QuestionEmbedding, Answer, CitationsJson, HitCount, CreatedAt)
            VALUES (@hash, @question, @embedding, @answer, @citations, 0, SYSDATETIMEOFFSET());
        """;

    public async Task<AnswerCandidate?> NearestAsync(ReadOnlyMemory<float> questionEmbedding, CancellationToken cancellationToken)
    {
        var query = Vectors.ToSql(questionEmbedding);
        using var activity = SqlActivity.Start("answer_cache.nearest", Nearest);
        try
        {
            await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = Nearest;
            command.Parameters.Add(new SqlParameter("@embedding", query));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return null;

            return new AnswerCandidate(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                AnswerCachePolicy.SimilarityFromCosineDistance(reader.GetDouble(4)));
        }
        catch (Exception exception)
        {
            activity.Fail(exception);
            throw;
        }
    }

    public async Task RecordHitAsync(long id, CancellationToken cancellationToken)
    {
        using var activity = SqlActivity.Start("answer_cache.hit", RecordHit);
        try
        {
            await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = RecordHit;
            command.Parameters.Add(new SqlParameter("@id", id));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            activity.Fail(exception);
            throw;
        }
    }

    public async Task SaveAsync(
        byte[] questionHash,
        string question,
        ReadOnlyMemory<float> embedding,
        string answer,
        string citationsJson,
        CancellationToken cancellationToken)
    {
        var vector = Vectors.ToSql(embedding);
        using var activity = SqlActivity.Start("answer_cache.save");
        try
        {
            await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = Save;
            command.Parameters.Add(new SqlParameter("@hash", System.Data.SqlDbType.Binary, 32) { Value = questionHash });
            command.Parameters.Add(new SqlParameter("@question", question));
            command.Parameters.Add(new SqlParameter("@embedding", vector));
            command.Parameters.Add(new SqlParameter("@answer", answer));
            command.Parameters.Add(new SqlParameter("@citations", citationsJson));

            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SqlException exception) when (exception.Number is DuplicateKey or UniqueConstraint)
            {
            }
        }
        catch (Exception exception)
        {
            activity.Fail(exception);
            throw;
        }
    }
}
