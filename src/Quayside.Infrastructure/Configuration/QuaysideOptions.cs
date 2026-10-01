using Microsoft.Extensions.Configuration;

namespace Quayside.Infrastructure.Configuration;

public sealed record AzureOpenAIOptions(string? Endpoint, string? ChatDeployment, string? EmbeddingDeployment);

public sealed record SqlOptions(string? ReadWrite, string? ReadOnly);

public sealed record QuaysideOptions(
    AzureOpenAIOptions AzureOpenAI,
    SqlOptions Sql,
    string CorpusDirectory,
    string? ApplicationInsightsConnectionString,
    string? ManagedIdentityClientId)
{
    public const string DefaultCorpusDirectory = "/app/data";

    public static QuaysideOptions From(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new QuaysideOptions(
            new AzureOpenAIOptions(
                Read(configuration, ConfigurationKeys.OpenAIEndpoint),
                Read(configuration, ConfigurationKeys.OpenAIChatDeployment),
                Read(configuration, ConfigurationKeys.OpenAIEmbeddingDeployment)),
            new SqlOptions(
                Read(configuration, ConfigurationKeys.SqlConnection),
                Read(configuration, ConfigurationKeys.SqlReadOnlyConnection)),
            Read(configuration, ConfigurationKeys.CorpusDirectory) ?? DefaultCorpusDirectory,
            Read(configuration, ConfigurationKeys.ApplicationInsightsConnection),
            Read(configuration, ConfigurationKeys.ManagedIdentityClientId));
    }

    private static string? Read(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
