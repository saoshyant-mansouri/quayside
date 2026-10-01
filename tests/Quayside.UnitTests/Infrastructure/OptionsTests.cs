using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quayside.Infrastructure.Configuration;

namespace Quayside.UnitTests.Infrastructure;

public sealed class OptionsTests
{
    [Fact]
    public void Binds_every_key_from_configuration()
    {
        var settings = InfrastructureFixtures.ValidSettings();
        settings["Corpus:Directory"] = "/somewhere/data";
        settings["APPLICATIONINSIGHTS_CONNECTION_STRING"] = "InstrumentationKey=00000000-0000-0000-0000-000000000000";
        settings["AZURE_CLIENT_ID"] = "11111111-1111-1111-1111-111111111111";

        var options = QuaysideOptions.From(InfrastructureFixtures.Configuration(settings));

        Assert.Equal("https://aoai.example.net/", options.AzureOpenAI.Endpoint);
        Assert.Equal("gpt-5-mini", options.AzureOpenAI.ChatDeployment);
        Assert.Equal("text-embedding-3-small", options.AzureOpenAI.EmbeddingDeployment);
        Assert.Equal(InfrastructureFixtures.ReadWrite, options.Sql.ReadWrite);
        Assert.Equal(InfrastructureFixtures.ReadOnly, options.Sql.ReadOnly);
        Assert.Equal("/somewhere/data", options.CorpusDirectory);
        Assert.Equal("InstrumentationKey=00000000-0000-0000-0000-000000000000", options.ApplicationInsightsConnectionString);
        Assert.Equal("11111111-1111-1111-1111-111111111111", options.ManagedIdentityClientId);
    }

    [Fact]
    public void Corpus_directory_defaults_to_the_container_path()
    {
        var options = QuaysideOptions.From(InfrastructureFixtures.Configuration(InfrastructureFixtures.ValidSettings()));

        Assert.Equal("/app/data", options.CorpusDirectory);
        Assert.Null(options.ApplicationInsightsConnectionString);
        Assert.Null(options.ManagedIdentityClientId);
    }

    [Theory]
    [InlineData("AzureOpenAI:Endpoint")]
    [InlineData("AzureOpenAI:ChatDeployment")]
    [InlineData("AzureOpenAI:EmbeddingDeployment")]
    [InlineData("ConnectionStrings:Sql")]
    [InlineData("ConnectionStrings:SqlReadOnly")]
    public void A_missing_key_fails_validation_and_names_the_key(string key)
    {
        var settings = InfrastructureFixtures.ValidSettings();
        settings.Remove(key);

        var result = Validate(settings);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains($"'{key}'", StringComparison.Ordinal));
    }

    [Fact]
    public void Whitespace_counts_as_missing()
    {
        var settings = InfrastructureFixtures.ValidSettings();
        settings["AzureOpenAI:ChatDeployment"] = "   ";

        Assert.Contains(Validate(settings).Failures!, f => f.Contains("AzureOpenAI:ChatDeployment", StringComparison.Ordinal));
    }

    [Fact]
    public void All_missing_keys_are_reported_together()
    {
        var result = Validate([]);

        Assert.Equal(5, result.Failures!.Count());
    }

    [Fact]
    public void Valid_settings_pass()
    {
        Assert.True(Validate(InfrastructureFixtures.ValidSettings()).Succeeded);
    }

    [Theory]
    [InlineData("http://aoai.example.net/")]
    [InlineData("not a url")]
    public void The_endpoint_must_be_an_https_url(string endpoint)
    {
        var settings = InfrastructureFixtures.ValidSettings();
        settings["AzureOpenAI:Endpoint"] = endpoint;

        Assert.Contains(Validate(settings).Failures!, f => f.Contains("https", StringComparison.Ordinal));
    }

    [Fact]
    public void The_read_only_connection_string_must_declare_read_only_intent()
    {
        var settings = InfrastructureFixtures.ValidSettings();
        settings["ConnectionStrings:SqlReadOnly"] = "Server=tcp:db.example.net,1433;Initial Catalog=quayside;User ID=reader;Password=y";

        Assert.Contains(Validate(settings).Failures!, f => f.Contains("ApplicationIntent=ReadOnly", StringComparison.Ordinal));
    }

    [Fact]
    public void The_read_only_login_must_differ_from_the_read_write_login()
    {
        var settings = InfrastructureFixtures.ValidSettings();
        settings["ConnectionStrings:SqlReadOnly"] = InfrastructureFixtures.ReadWrite + ";ApplicationIntent=ReadOnly";

        Assert.Contains(Validate(settings).Failures!, f => f.Contains("different login", StringComparison.Ordinal));
    }

    [Fact]
    public void Resolving_options_with_a_missing_key_throws_naming_the_key()
    {
        var settings = InfrastructureFixtures.ValidSettings();
        settings.Remove("ConnectionStrings:Sql");
        using var provider = InfrastructureFixtures.Provider(settings, new StaticTokenCredential());

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<QuaysideOptions>>().Value);

        Assert.Contains("ConnectionStrings:Sql", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_validation_fails_fast_when_a_key_is_missing()
    {
        var settings = InfrastructureFixtures.ValidSettings();
        settings.Remove("AzureOpenAI:Endpoint");
        using var provider = InfrastructureFixtures.Provider(settings, new StaticTokenCredential());

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());

        Assert.Contains("AzureOpenAI:Endpoint", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_validation_passes_with_complete_settings()
    {
        using var provider = InfrastructureFixtures.Provider(InfrastructureFixtures.ValidSettings(), new StaticTokenCredential());

        provider.GetRequiredService<IStartupValidator>().Validate();
    }

    [Fact]
    public void Data_paths_prefer_an_existing_configured_directory()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var paths = DataPaths.Resolve(root, AppContext.BaseDirectory);

            Assert.Equal(root, paths.Root);
            Assert.Equal(Path.Combine(root, "corpus"), paths.CorpusDirectory);
            Assert.Equal(Path.Combine(root, "schema", "schema.json"), paths.SchemaFile);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Data_paths_fall_back_to_the_repository_data_directory()
    {
        var paths = DataPaths.Resolve("/definitely/not/here/app/data", AppContext.BaseDirectory);

        Assert.True(File.Exists(paths.SchemaFile));
        Assert.True(Directory.Exists(paths.CorpusDirectory));
    }

    private static ValidateOptionsResult Validate(Dictionary<string, string?> settings) =>
        new QuaysideOptionsValidator().Validate(null, QuaysideOptions.From(InfrastructureFixtures.Configuration(settings)));
}
