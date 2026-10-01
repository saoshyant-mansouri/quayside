using Microsoft.Extensions.AI;
using Quayside.Api.Contract;
using Quayside.Api.Conversations;
using Quayside.Api.Endpoints;
using Quayside.Api.Hosting;
using Quayside.Api.Offline;
using Quayside.Api.Orchestration;
using Quayside.Api.Tools;
using Quayside.Core.Sql;
using Quayside.Infrastructure;

namespace Quayside.Api;

public static class QuaysideApplication
{
    public const string CorsPolicy = "quayside-web";

    public static WebApplication Build(string[] args, Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = SseJson.Options.PropertyNamingPolicy;
            options.SerializerOptions.DefaultIgnoreCondition = SseJson.Options.DefaultIgnoreCondition;
        });

        AddOptions(builder.Services, builder.Configuration);
        AddCors(builder.Services, builder.Configuration);

        if (builder.Configuration.GetValue<bool>(OfflineServices.Flag))
        {
            builder.Services.AddQuaysideOffline();
        }
        else
        {
            builder.Services.AddQuaysideInfrastructure(builder.Configuration);
        }

        AddOrchestration(builder.Services);
        configureServices?.Invoke(builder.Services);

        var app = builder.Build();
        app.Logger.LogInformation("{WebFallbackStatus}", app.Services.GetRequiredService<WebFallback>().StatusLine);
        app.UseCors(CorsPolicy);
        ChatEndpoint.Map(app);
        HealthEndpoint.Map(app);
        ExamplesEndpoint.Map(app);
        return app;
    }

    private static void AddOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GroundingOptions>(configuration.GetSection(GroundingOptions.Section));
        services.Configure<RetrievalOptions>(configuration.GetSection(RetrievalOptions.Section));
        services.Configure<SqlGenerationOptions>(configuration.GetSection(SqlGenerationOptions.Section));
        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.Section));
        services.Configure<ChatLimits>(configuration.GetSection(ChatLimits.Section));
        services.Configure<HydrationOptions>(configuration.GetSection(HydrationOptions.Section));
    }

    private static void AddCors(IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection($"{CorsOptions.Section}:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
        {
            if (origins.Length > 0)
            {
                policy.WithOrigins(origins).WithMethods("GET", "POST", "OPTIONS").AllowAnyHeader();
            }
        }));
    }

    private static void AddOrchestration(IServiceCollection services)
    {
        services.AddSingleton<Hydrated<CorpusIndex>>();
        services.AddSingleton<Hydrated<SchemaRetriever>>();
        services.AddHostedService<IndexHydrator>();
        services.AddSingleton<ConversationStore>();
        services.AddSingleton<ToolRunner>();
        services.AddSingleton<WebFallback>();
        services.AddSingleton<WebSearchTool>();
        services.AddSingleton<KnowledgeSearch>();
        services.AddSingleton<DatabaseQuery>();
        services.AddSingleton<OperationalLookups>();
        services.AddSingleton<ToolCallingChat>();
        services.AddSingleton<ChatOrchestrator>();
    }
}
