using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quayside.Infrastructure.Configuration;
using Quayside.Infrastructure.Telemetry;

namespace Quayside.Infrastructure.Ai;

public static class AiServices
{
    public static IServiceCollection AddQuaysideAi(this IServiceCollection services)
    {
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<QuaysideOptions>>().Value;
            return AzureOpenAIClients.Create(
                options.AzureOpenAI,
                provider.GetRequiredService<Azure.Core.TokenCredential>());
        });

        services.AddSingleton<IChatClient>(provider =>
        {
            var deployment = provider.GetRequiredService<IOptions<QuaysideOptions>>().Value.AzureOpenAI.ChatDeployment!;
            return provider.GetRequiredService<AzureOpenAIClient>()
                .GetChatClient(deployment)
                .AsIChatClient()
                .AsBuilder()
                .UseOpenTelemetry(sourceName: TelemetryNames.AiSourceName)
                .Build();
        });

        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(provider =>
        {
            var deployment = provider.GetRequiredService<IOptions<QuaysideOptions>>().Value.AzureOpenAI.EmbeddingDeployment!;
            var instrumented = provider.GetRequiredService<AzureOpenAIClient>()
                .GetEmbeddingClient(deployment)
                .AsIEmbeddingGenerator()
                .AsBuilder()
                .UseOpenTelemetry(sourceName: TelemetryNames.AiSourceName)
                .Build();
            return new BatchingEmbeddingGenerator(instrumented);
        });

        return services;
    }
}
