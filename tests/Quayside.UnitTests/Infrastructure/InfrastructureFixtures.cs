using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quayside.Infrastructure;

namespace Quayside.UnitTests.Infrastructure;

internal static class InfrastructureFixtures
{
    public const string ReadWrite = "Server=tcp:db.example.net,1433;Initial Catalog=quayside;User ID=app;Password=x;Encrypt=True";
    public const string ReadOnly = "Server=tcp:db.example.net,1433;Initial Catalog=quayside;User ID=reader;Password=y;Encrypt=True;ApplicationIntent=ReadOnly";

    public static Dictionary<string, string?> ValidSettings() => new()
    {
        ["AzureOpenAI:Endpoint"] = "https://aoai.example.net/",
        ["AzureOpenAI:ChatDeployment"] = "gpt-5-mini",
        ["AzureOpenAI:EmbeddingDeployment"] = "text-embedding-3-small",
        ["ConnectionStrings:Sql"] = ReadWrite,
        ["ConnectionStrings:SqlReadOnly"] = ReadOnly
    };

    public static IConfiguration Configuration(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    public static ServiceProvider Provider(Dictionary<string, string?> settings, TokenCredential? credential = null)
    {
        var services = new ServiceCollection();
        if (credential is not null) services.AddSingleton(credential);
        services.AddQuaysideInfrastructure(Configuration(settings));
        return services.BuildServiceProvider();
    }
}

internal sealed class StaticTokenCredential : TokenCredential
{
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        new("fake-token", DateTimeOffset.UtcNow.AddHours(1));

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        ValueTask.FromResult(GetToken(requestContext, cancellationToken));
}
