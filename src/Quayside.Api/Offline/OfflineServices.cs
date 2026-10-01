using Microsoft.Extensions.AI;
using Quayside.Core;
using Quayside.Core.Web;

namespace Quayside.Api.Offline;

public static class OfflineServices
{
    public const string Flag = "Quayside:Offline";

    public const string DataDirectoryKey = "Quayside:OfflineDataDirectory";

    public static IServiceCollection AddQuaysideOffline(this IServiceCollection services)
    {
        services.AddSingleton<OfflineData>();
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, HashingEmbeddingGenerator>();
        services.AddSingleton<IChatClient, ScriptedChatClient>();
        services.AddSingleton<IChunkStore, OfflineChunkStore>();
        services.AddSingleton<ISchemaCatalog, OfflineSchemaCatalog>();
        services.AddSingleton<IAnswerCache, InMemoryAnswerCache>();
        services.AddSingleton<IReadOnlySqlExecutor, CannedSqlExecutor>();
        services.AddSingleton<IWebSearch>(new NoWebSearch("offline mode"));
        services.PostConfigure<GroundingOptions>(options => options.MinTopCosine = Math.Min(options.MinTopCosine, HashingEmbeddingGenerator.SuggestedMinTopCosine));
        return services;
    }
}
