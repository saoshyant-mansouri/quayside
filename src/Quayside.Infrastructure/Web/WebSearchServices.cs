using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Quayside.Core;
using Quayside.Core.Web;

namespace Quayside.Infrastructure.Web;

public static class WebSearchServices
{
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(8);

    public static IServiceCollection AddQuaysideWebSearch(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = WebSearchOptions.From(configuration);
        services.TryAddSingleton(options);

        if (!options.IsActive)
        {
            services.AddSingleton<IWebSearch>(new NoWebSearch(options.DisabledReason!));
            return services;
        }

        services.AddHttpClient(WebSearchClient.HttpClientName, client => client.Timeout = RequestTimeout)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
            });

        services.AddSingleton<IWebSearch>(provider => new WebSearchClient(
            provider.GetRequiredService<IHttpClientFactory>(),
            options.Provider == WebSearchOptions.Brave ? new BraveProvider(options.ApiKey!) : new TavilyProvider(options.ApiKey!),
            provider.GetRequiredService<ILogger<WebSearchClient>>()));

        return services;
    }
}
