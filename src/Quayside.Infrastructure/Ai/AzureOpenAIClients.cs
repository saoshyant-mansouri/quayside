using System.ClientModel.Primitives;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Quayside.Infrastructure.Configuration;

namespace Quayside.Infrastructure.Ai;

public static class AzureOpenAIClients
{
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(60);

    public static TokenCredential CreateCredential(string? managedIdentityClientId)
    {
        var options = new DefaultAzureCredentialOptions();
        if (managedIdentityClientId is not null)
            options.ManagedIdentityClientId = managedIdentityClientId;
        return new DefaultAzureCredential(options);
    }

    public static AzureOpenAIClient Create(
        AzureOpenAIOptions settings,
        TokenCredential credential,
        PipelinePolicy? retryPolicy = null,
        PipelineTransport? transport = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(credential);

        var options = new AzureOpenAIClientOptions
        {
            RetryPolicy = retryPolicy ?? new BackoffRetryPolicy(),
            NetworkTimeout = RequestTimeout
        };
        if (transport is not null) options.Transport = transport;

        return new AzureOpenAIClient(new Uri(settings.Endpoint!), credential, options);
    }
}
