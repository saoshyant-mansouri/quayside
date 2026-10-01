namespace Quayside.Infrastructure.Configuration;

public static class ConfigurationKeys
{
    public const string OpenAIEndpoint = "AzureOpenAI:Endpoint";
    public const string OpenAIChatDeployment = "AzureOpenAI:ChatDeployment";
    public const string OpenAIEmbeddingDeployment = "AzureOpenAI:EmbeddingDeployment";
    public const string SqlConnection = "ConnectionStrings:Sql";
    public const string SqlReadOnlyConnection = "ConnectionStrings:SqlReadOnly";
    public const string CorpusDirectory = "Corpus:Directory";
    public const string ApplicationInsightsConnection = "APPLICATIONINSIGHTS_CONNECTION_STRING";
    public const string ManagedIdentityClientId = "AZURE_CLIENT_ID";
    public const string WebSearchProvider = "WebSearch:Provider";
    public const string WebSearchApiKey = "WebSearch:ApiKey";
    public const string WebSearchMaxResults = "WebSearch:MaxResults";
    public const string WebSearchEnabled = "WebSearch:Enabled";
}
