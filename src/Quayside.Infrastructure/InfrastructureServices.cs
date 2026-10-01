using Azure.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Quayside.Core;
using Quayside.Infrastructure.Ai;
using Quayside.Infrastructure.Configuration;
using Quayside.Infrastructure.Data;
using Quayside.Infrastructure.Sql;
using Quayside.Infrastructure.Telemetry;

namespace Quayside.Infrastructure;

public static class InfrastructureServices
{
    public static IServiceCollection AddQuaysideInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IValidateOptions<QuaysideOptions>, QuaysideOptionsValidator>();
        services.TryAddSingleton(configuration);
        services.AddSingleton<IOptionsFactory<QuaysideOptions>, QuaysideOptionsFactory>();
        services.AddOptions<QuaysideOptions>().ValidateOnStart();

        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<QuaysideOptions>>().Value;
            return DataPaths.Resolve(options.CorpusDirectory, AppContext.BaseDirectory);
        });

        services.TryAddSingleton<TokenCredential>(provider =>
            AzureOpenAIClients.CreateCredential(provider.GetRequiredService<IOptions<QuaysideOptions>>().Value.ManagedIdentityClientId));

        services.AddQuaysideAi();

        services.AddSingleton(provider =>
            new SqlDatabase(provider.GetRequiredService<IOptions<QuaysideOptions>>().Value.Sql.ReadWrite!));

        services.AddDbContext<QuaysideDbContext>((provider, builder) =>
            builder.UseSqlServer(
                provider.GetRequiredService<IOptions<QuaysideOptions>>().Value.Sql.ReadWrite!,
                sql => sql.EnableRetryOnFailure()));

        services.AddSingleton<IChunkStore, SqlChunkStore>();
        services.AddSingleton<IAnswerCacheStore, SqlAnswerCacheStore>();
        services.AddSingleton<IAnswerCache, SqlAnswerCache>();
        services.AddSingleton<SqlSchemaCatalog>();
        services.AddSingleton<ISchemaCatalog>(provider => provider.GetRequiredService<SqlSchemaCatalog>());
        services.AddSingleton<IEmbeddedCardSource>(provider => provider.GetRequiredService<SqlSchemaCatalog>());
        services.AddSingleton<IVectorSearch, SqlVectorSearch>();
        services.AddSingleton<IQueryLog, SqlQueryLog>();
        services.AddSingleton<IReadOnlySqlExecutor>(provider =>
            new ReadOnlySqlExecutor(provider.GetRequiredService<IOptions<QuaysideOptions>>().Value.Sql.ReadOnly!));

        services.AddQuaysideTelemetry(configuration);

        return services;
    }
}
