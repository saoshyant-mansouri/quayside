using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Quayside.Core;
using Quayside.Infrastructure.Ai;
using Quayside.Infrastructure.Data;
using Quayside.Infrastructure.Sql;

namespace Quayside.UnitTests.Infrastructure;

public sealed class ServiceRegistrationTests
{
    [Fact]
    public void Registers_every_port_and_both_ai_clients()
    {
        using var provider = InfrastructureFixtures.Provider(InfrastructureFixtures.ValidSettings(), new StaticTokenCredential());

        Assert.IsType<SqlChunkStore>(provider.GetRequiredService<IChunkStore>());
        Assert.IsType<SqlAnswerCache>(provider.GetRequiredService<IAnswerCache>());
        Assert.IsType<SqlSchemaCatalog>(provider.GetRequiredService<ISchemaCatalog>());
        Assert.IsType<ReadOnlySqlExecutor>(provider.GetRequiredService<IReadOnlySqlExecutor>());
        Assert.NotNull(provider.GetRequiredService<IChatClient>());
        Assert.IsType<BatchingEmbeddingGenerator>(provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>());
    }

    [Fact]
    public void The_schema_catalog_is_one_instance_behind_both_of_its_interfaces()
    {
        using var provider = InfrastructureFixtures.Provider(InfrastructureFixtures.ValidSettings(), new StaticTokenCredential());

        Assert.Same(provider.GetRequiredService<ISchemaCatalog>(), provider.GetRequiredService<IEmbeddedCardSource>());
    }

    [Fact]
    public void A_read_write_string_cannot_reach_the_read_only_executor()
    {
        var settings = InfrastructureFixtures.ValidSettings();
        settings["ConnectionStrings:SqlReadOnly"] = InfrastructureFixtures.ReadWrite;
        using var provider = InfrastructureFixtures.Provider(settings, new StaticTokenCredential());

        Assert.ThrowsAny<Exception>(() => provider.GetRequiredService<IReadOnlySqlExecutor>());
    }

    [Fact]
    public void The_credential_defaults_to_the_azure_default_chain_with_no_key_involved()
    {
        var credential = AzureOpenAIClients.CreateCredential("11111111-1111-1111-1111-111111111111");

        Assert.IsType<Azure.Identity.DefaultAzureCredential>(credential);
    }

    [Fact]
    public void The_model_has_the_five_tables_with_vector_columns_and_required_indexes()
    {
        using var context = new QuaysideDbContextFactory().CreateDbContext([]);
        var model = context.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model;

        var tables = model.GetEntityTypes().Select(e => e.GetTableName()!).Order().ToArray();
        Assert.Equal(["AnswerCache", "Chunks", "Documents", "QueryLog", "SchemaCards"], tables);

        string[] vectorColumns =
        [
            nameof(ChunkEntity.Embedding),
            nameof(AnswerCacheEntity.QuestionEmbedding)
        ];
        Assert.All(vectorColumns, column =>
            Assert.Equal("vector(1536)", model.GetEntityTypes().SelectMany(e => e.GetProperties()).First(p => p.Name == column).GetColumnType()));

        Assert.Contains(model.FindEntityType(typeof(DocumentEntity))!.GetIndexes(), i => i.Properties.Single().Name == nameof(DocumentEntity.Url));
        Assert.Contains(model.FindEntityType(typeof(AnswerCacheEntity))!.GetIndexes(), i => i.Properties.Single().Name == nameof(AnswerCacheEntity.QuestionHash));
    }

    [Fact]
    public void Committed_migrations_match_the_model()
    {
        using var context = new QuaysideDbContextFactory().CreateDbContext([]);

        Assert.NotEmpty(context.Database.GetMigrations());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Vector_search_orders_by_cosine_distance_in_t_sql()
    {
        Assert.Contains("VECTOR_DISTANCE('cosine'", SqlVectorSearch.ChunkQuery, StringComparison.Ordinal);
        Assert.Contains("VECTOR_DISTANCE('cosine'", SqlVectorSearch.SchemaCardQuery, StringComparison.Ordinal);
        Assert.Contains("ORDER BY Distance", SqlVectorSearch.ChunkQuery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Vector_search_validates_its_arguments_before_connecting()
    {
        var search = new SqlVectorSearch(new SqlDatabase(InfrastructureFixtures.ReadWrite));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => search.SearchChunksAsync(new float[1536], 0, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => search.SearchChunksAsync(new float[4], 5, CancellationToken.None));
    }
}
