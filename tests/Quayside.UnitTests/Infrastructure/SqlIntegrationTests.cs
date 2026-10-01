using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Quayside.Core.Documents;
using Quayside.Core.Sql;
using Quayside.Infrastructure.Data;
using Quayside.Infrastructure.Schema;
using Quayside.Infrastructure.Sql;

namespace Quayside.UnitTests.Infrastructure;

public sealed class IntegrationFactAttribute : FactAttribute
{
    public const string Variable = "QUAYSIDE_TEST_SQL";

    public IntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)))
            Skip = $"Set {Variable} to an administrator connection string for a SQL Server 2025 or Azure SQL server to run.";
    }
}

[Trait("Category", "Integration")]
public sealed class SqlIntegrationTests
{
    private static readonly Random Rng = new(7);

    [IntegrationFact]
    public async Task Round_trips_chunks_cache_search_catalog_and_executor_against_a_real_database()
    {
        await using var database = await TemporaryDatabase.CreateAsync();

        await ChunkStoreRoundTrips(database);
        await AnswerCacheHonoursTheThreshold(database);
        await VectorSearchRanksByCosine(database);
        await SchemaCatalogRoundTrips(database);
        await ExecutorIsLeastPrivilege(database);
    }

    private static async Task ChunkStoreRoundTrips(TemporaryDatabase database)
    {
        var store = new SqlChunkStore(new SqlDatabase(database.AdminConnection));
        var documents = new[] { Doc("d1", "https://example.com/a"), Doc("d2", "https://example.com/b") };
        var chunks = new[]
        {
            Chunk("d1", 0, Unit(1)), Chunk("d1", 1, Unit(2)), Chunk("d1", 2, Unit(3)),
            Chunk("d2", 0, Unit(4)), Chunk("d2", 1, Unit(5))
        };

        await store.UpsertAsync(documents, chunks, CancellationToken.None);
        await store.UpsertAsync(documents, chunks, CancellationToken.None);

        var loadedDocuments = await store.LoadDocumentsAsync(CancellationToken.None);
        var loaded = await store.LoadChunksAsync(CancellationToken.None);
        Assert.Equal(2, loadedDocuments.Count);
        Assert.Equal(5, loaded.Count);
        var first = loaded.Single(c => c.Chunk.Id == "d1:0");
        Assert.Equal(chunks[0].Embedding.ToArray(), first.Embedding.ToArray());
        Assert.Equal("Title d1", loadedDocuments.Single(d => d.Id == "d1").Title);

        await store.UpsertAsync(documents, [chunks[0], chunks[3]], CancellationToken.None);
        Assert.Equal(2, (await store.LoadChunksAsync(CancellationToken.None)).Count);

        await store.UpsertAsync(documents, chunks, CancellationToken.None);
    }

    private static async Task AnswerCacheHonoursTheThreshold(TemporaryDatabase database)
    {
        var cache = new SqlAnswerCache(new SqlAnswerCacheStore(new SqlDatabase(database.AdminConnection)));
        var stored = Unit(10);

        await cache.StoreAsync("What is MSC?", stored, "A shipping line.", "[1]", CancellationToken.None);
        await cache.StoreAsync("what is  msc?", stored, "A shipping line, updated.", "[1]", CancellationToken.None);

        var hit = await cache.TryGetAsync(stored, CancellationToken.None);
        Assert.NotNull(hit);
        Assert.Equal("A shipping line, updated.", hit.Answer);
        Assert.True(hit.Similarity > 0.999);

        Assert.Null(await cache.TryGetAsync(Unit(11), CancellationToken.None));

        var near = Perturb(stored, 0.05f);
        Assert.NotNull(await cache.TryGetAsync(near, CancellationToken.None));

        await using var connection = new SqlConnection(database.AdminConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT HitCount FROM dbo.AnswerCache";
        Assert.Equal(2, Convert.ToInt32(await command.ExecuteScalarAsync()));
    }

    private static async Task VectorSearchRanksByCosine(TemporaryDatabase database)
    {
        var search = new SqlVectorSearch(new SqlDatabase(database.AdminConnection));

        var hits = await search.SearchChunksAsync(Unit(4), 2, CancellationToken.None);

        Assert.Equal(2, hits.Count);
        Assert.Equal("d2:0", hits[0].ChunkId);
        Assert.True(hits[0].Similarity > hits[1].Similarity);
    }

    private static async Task SchemaCatalogRoundTrips(TemporaryDatabase database)
    {
        var catalog = new SqlSchemaCatalog(new SqlDatabase(database.AdminConnection));
        var cards = TableCardFactory.Load(SchemaPath()).Select((card, i) => new EmbeddedTableCard(card, Unit(100 + i))).ToArray();

        await catalog.UpsertAsync(cards, CancellationToken.None);
        await catalog.UpsertAsync(cards, CancellationToken.None);

        var loaded = await catalog.LoadCardsAsync(CancellationToken.None);
        Assert.Equal(cards.Length, loaded.Count);
        Assert.All(loaded, card => Assert.StartsWith("CREATE TABLE ops.", card.Ddl, StringComparison.Ordinal));
        var original = cards.Single(c => c.Card.Table == "Bookings").Card;
        var roundTripped = loaded.Single(c => c.Table == "Bookings");
        Assert.Equal(original.Ddl, roundTripped.Ddl);
        Assert.Equal(original.Neighbours.Order().ToArray(), roundTripped.Neighbours.Order().ToArray());

        var embedded = await catalog.LoadEmbeddedCardsAsync(CancellationToken.None);
        Assert.Equal(cards.Length, embedded.Count);

        var search = new SqlVectorSearch(new SqlDatabase(database.AdminConnection));
        var tables = await search.SearchSchemaCardsAsync(Unit(100), 1, CancellationToken.None);
        Assert.Equal(cards[0].Card.Table, tables[0].TableName);
    }

    private static async Task ExecutorIsLeastPrivilege(TemporaryDatabase database)
    {
        var executor = new ReadOnlySqlExecutor(database.ReaderConnection);

        var result = await executor.ExecuteAsync("SELECT TOP (1) Id AS DocumentKey, Title FROM dbo.Documents ORDER BY Id", CancellationToken.None);
        Assert.Equal(["DocumentKey", "Title"], result.Columns);
        Assert.Single(result.Rows);
        Assert.Equal("d1", result.Rows[0][0]);

        await Assert.ThrowsAsync<SqlException>(() =>
            executor.ExecuteAsync("DELETE FROM dbo.Documents", CancellationToken.None));

        var timeout = await Assert.ThrowsAsync<SqlException>(() =>
            executor.ExecuteAsync("WAITFOR DELAY '00:00:15'; SELECT 1", CancellationToken.None));
        Assert.Contains("Timeout", timeout.Message, StringComparison.OrdinalIgnoreCase);

        var privileged = new ReadOnlySqlExecutor(database.AdminConnection + ";ApplicationIntent=ReadOnly");
        await Assert.ThrowsAsync<InvalidOperationException>(() => privileged.ExecuteAsync("SELECT 1", CancellationToken.None));
    }

    private static Document Doc(string id, string url) =>
        new(id, SourceKind.Website, url, $"Title {id}", $"Text {id}", null, DateTimeOffset.UtcNow, new string('a', 64));

    private static EmbeddedChunk Chunk(string documentId, int ordinal, float[] embedding) =>
        new(new Chunk($"{documentId}:{ordinal}", documentId, ordinal, $"text {documentId} {ordinal}", 10), embedding);

    private static float[] Unit(int seed)
    {
        var random = new Random(seed);
        var vector = new float[1536];
        for (var i = 0; i < vector.Length; i++) vector[i] = (float)(random.NextDouble() - 0.5);
        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        for (var i = 0; i < vector.Length; i++) vector[i] /= norm;
        return vector;
    }

    private static float[] Perturb(float[] vector, float amount)
    {
        var result = (float[])vector.Clone();
        for (var i = 0; i < result.Length; i++) result[i] += (float)(Rng.NextDouble() - 0.5) * amount / 20;
        return result;
    }

    private static string SchemaPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "data", "schema", "schema.json")))
            directory = directory.Parent;
        return Path.Combine(directory!.FullName, "data", "schema", "schema.json");
    }

    private sealed class TemporaryDatabase : IAsyncDisposable
    {
        private readonly string name;
        private readonly string login;

        private TemporaryDatabase(string name, string login, string admin, string reader)
        {
            this.name = name;
            this.login = login;
            AdminConnection = admin;
            ReaderConnection = reader;
        }

        public string AdminConnection { get; }

        public string ReaderConnection { get; }

        public static async Task<TemporaryDatabase> CreateAsync()
        {
            var master = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable(IntegrationFactAttribute.Variable)!);
            var suffix = Guid.NewGuid().ToString("N")[..10];
            var name = $"quayside_it_{suffix}";
            var login = $"qs_reader_{suffix}";
            var password = $"Rd!{Guid.NewGuid():N}";

            await Execute(master.ConnectionString, $"CREATE DATABASE [{name}]");

            var admin = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name };
            var options = new DbContextOptionsBuilder<QuaysideDbContext>().UseSqlServer(admin.ConnectionString).Options;
            await using (var context = new QuaysideDbContext(options))
                await context.Database.MigrateAsync();

            await Execute(master.ConnectionString, $"CREATE LOGIN [{login}] WITH PASSWORD = '{password}', CHECK_POLICY = OFF");
            await Execute(admin.ConnectionString, $"CREATE USER [{login}] FOR LOGIN [{login}]; ALTER ROLE db_datareader ADD MEMBER [{login}]");

            var reader = new SqlConnectionStringBuilder(admin.ConnectionString)
            {
                UserID = login,
                Password = password,
                ApplicationIntent = ApplicationIntent.ReadOnly
            };
            return new TemporaryDatabase(name, login, admin.ConnectionString, reader.ConnectionString);
        }

        public async ValueTask DisposeAsync()
        {
            SqlConnection.ClearAllPools();
            var master = Environment.GetEnvironmentVariable(IntegrationFactAttribute.Variable)!;
            await Execute(master, $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]; DROP LOGIN [{login}]");
        }

        private static async Task Execute(string connectionString, string sql)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
    }
}
