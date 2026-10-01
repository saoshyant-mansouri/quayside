using Quayside.Infrastructure.Sql;

namespace Quayside.UnitTests.Infrastructure;

public sealed class AnswerCacheTests
{
    private static readonly ReadOnlyMemory<float> Embedding = new float[] { 1, 0 };

    [Theory]
    [InlineData(1.0, true)]
    [InlineData(0.98, true)]
    [InlineData(0.97, true)]
    [InlineData(0.9699, false)]
    [InlineData(0.9, false)]
    [InlineData(0.0, false)]
    [InlineData(-1.0, false)]
    [InlineData(double.NaN, false)]
    public void The_threshold_is_inclusive_at_0_97(double similarity, bool hit)
    {
        Assert.Equal(hit, AnswerCachePolicy.IsHit(similarity));
    }

    [Fact]
    public void Similarity_is_one_minus_cosine_distance()
    {
        Assert.Equal(0.97, AnswerCachePolicy.SimilarityFromCosineDistance(0.03), precision: 12);
        Assert.True(AnswerCachePolicy.IsHit(AnswerCachePolicy.SimilarityFromCosineDistance(0.03)));
    }

    [Fact]
    public async Task A_hit_returns_the_answer_and_records_it()
    {
        var store = new FakeStore { Candidate = Candidate(0.985) };
        var cache = new SqlAnswerCache(store);

        var answer = await cache.TryGetAsync(Embedding, CancellationToken.None);

        Assert.NotNull(answer);
        Assert.Equal("cached answer", answer.Answer);
        Assert.Equal("[1]", answer.CitationsJson);
        Assert.Equal(0.985, answer.Similarity, precision: 12);
        Assert.Equal([42L], store.Hits);
    }

    [Fact]
    public async Task A_near_miss_returns_nothing_and_records_nothing()
    {
        var store = new FakeStore { Candidate = Candidate(0.969) };
        var cache = new SqlAnswerCache(store);

        Assert.Null(await cache.TryGetAsync(Embedding, CancellationToken.None));
        Assert.Empty(store.Hits);
    }

    [Fact]
    public async Task An_empty_cache_returns_nothing()
    {
        var cache = new SqlAnswerCache(new FakeStore());

        Assert.Null(await cache.TryGetAsync(Embedding, CancellationToken.None));
    }

    [Fact]
    public async Task Storing_hashes_the_normalised_question()
    {
        var store = new FakeStore();
        var cache = new SqlAnswerCache(store);

        await cache.StoreAsync("  Where IS  the   head office? ", Embedding, "a", "[]", CancellationToken.None);
        await cache.StoreAsync("where is the head office?", Embedding, "b", "[]", CancellationToken.None);

        Assert.Equal(2, store.Saved.Count);
        Assert.Equal(store.Saved[0].Hash, store.Saved[1].Hash);
        Assert.Equal(32, store.Saved[0].Hash.Length);
    }

    [Fact]
    public async Task Different_questions_hash_differently()
    {
        var store = new FakeStore();
        var cache = new SqlAnswerCache(store);

        await cache.StoreAsync("who are you", Embedding, "a", "[]", CancellationToken.None);
        await cache.StoreAsync("where are you", Embedding, "a", "[]", CancellationToken.None);

        Assert.NotEqual(store.Saved[0].Hash, store.Saved[1].Hash);
    }

    [Fact]
    public async Task An_overlong_question_is_truncated_for_storage_but_hashed_in_full()
    {
        var store = new FakeStore();
        var cache = new SqlAnswerCache(store);
        var question = new string('q', SqlAnswerCache.MaxQuestionLength + 50);

        await cache.StoreAsync(question, Embedding, "a", "[]", CancellationToken.None);

        Assert.Equal(SqlAnswerCache.MaxQuestionLength, store.Saved[0].Question.Length);
        Assert.Equal(AnswerCachePolicy.HashQuestion(question), store.Saved[0].Hash);
    }

    [Fact]
    public async Task A_blank_question_is_rejected()
    {
        var cache = new SqlAnswerCache(new FakeStore());

        await Assert.ThrowsAnyAsync<ArgumentException>(() => cache.StoreAsync(" ", Embedding, "a", "[]", CancellationToken.None));
    }

    [Fact]
    public async Task A_wrong_sized_embedding_is_refused_by_the_database_layer()
    {
        var store = new SqlAnswerCacheStore(new SqlDatabase("Server=tcp:127.0.0.1,1;Initial Catalog=x;User ID=u;Password=p"));

        await Assert.ThrowsAsync<ArgumentException>(() => store.NearestAsync(new float[3], CancellationToken.None));
    }

    private static AnswerCandidate Candidate(double similarity) => new(42, "q", "cached answer", "[1]", similarity);

    private sealed class FakeStore : IAnswerCacheStore
    {
        public AnswerCandidate? Candidate { get; init; }

        public List<long> Hits { get; } = [];

        public List<(byte[] Hash, string Question)> Saved { get; } = [];

        public Task<AnswerCandidate?> NearestAsync(ReadOnlyMemory<float> questionEmbedding, CancellationToken cancellationToken) =>
            Task.FromResult(Candidate);

        public Task RecordHitAsync(long id, CancellationToken cancellationToken)
        {
            Hits.Add(id);
            return Task.CompletedTask;
        }

        public Task SaveAsync(byte[] questionHash, string question, ReadOnlyMemory<float> embedding, string answer, string citationsJson, CancellationToken cancellationToken)
        {
            Saved.Add((questionHash, question));
            return Task.CompletedTask;
        }
    }
}
