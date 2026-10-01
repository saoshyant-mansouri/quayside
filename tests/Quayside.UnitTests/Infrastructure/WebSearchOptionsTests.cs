using Microsoft.Extensions.DependencyInjection;
using Quayside.Core;
using Quayside.Core.Web;
using Quayside.Infrastructure.Web;

namespace Quayside.UnitTests.Infrastructure;

public sealed class WebSearchOptionsTests
{
    private static WebSearchOptions Options(params (string Key, string? Value)[] pairs) =>
        WebSearchOptions.From(InfrastructureFixtures.Configuration(pairs.ToDictionary(p => p.Key, p => p.Value)));

    [Fact]
    public void Binds_every_key()
    {
        var options = Options(("WebSearch:Provider", "Brave"), ("WebSearch:ApiKey", " k-123 "), ("WebSearch:MaxResults", "7"), ("WebSearch:Enabled", "true"));

        Assert.Equal("brave", options.Provider);
        Assert.Equal("k-123", options.ApiKey);
        Assert.Equal(7, options.MaxResults);
        Assert.True(options.Enabled);
        Assert.True(options.IsActive);
        Assert.Null(options.DisabledReason);
    }

    [Fact]
    public void Defaults_to_tavily_five_results_and_enabled_but_inactive_without_a_key()
    {
        var options = Options();

        Assert.Equal("tavily", options.Provider);
        Assert.Equal(5, options.MaxResults);
        Assert.True(options.Enabled);
        Assert.Null(options.ApiKey);
        Assert.False(options.IsActive);
        Assert.Contains("WebSearch:ApiKey", options.DisabledReason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_whitespace_key_counts_as_missing()
    {
        Assert.False(Options(("WebSearch:ApiKey", "   ")).IsActive);
    }

    [Fact]
    public void Enabled_false_disables_even_with_a_key()
    {
        var options = Options(("WebSearch:ApiKey", "k"), ("WebSearch:Enabled", "false"));

        Assert.False(options.IsActive);
        Assert.Contains("WebSearch:Enabled", options.DisabledReason, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unparseable_enabled_value_keeps_the_default_of_enabled()
    {
        Assert.True(Options(("WebSearch:ApiKey", "k"), ("WebSearch:Enabled", "maybe")).IsActive);
    }

    [Fact]
    public void An_unsupported_provider_disables_and_says_why()
    {
        var options = Options(("WebSearch:ApiKey", "k"), ("WebSearch:Provider", "bing"));

        Assert.False(options.IsActive);
        Assert.Contains("'bing'", options.DisabledReason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0", 5)]
    [InlineData("-3", 5)]
    [InlineData("many", 5)]
    [InlineData("", 5)]
    [InlineData("3", 3)]
    [InlineData("50", 10)]
    public void Max_results_falls_back_or_clamps(string value, int expected)
    {
        Assert.Equal(expected, Options(("WebSearch:MaxResults", value)).MaxResults);
    }

    [Fact]
    public void Without_a_key_the_infrastructure_registers_the_no_op_and_the_rest_is_unaffected()
    {
        using var provider = InfrastructureFixtures.Provider(InfrastructureFixtures.ValidSettings(), new StaticTokenCredential());

        var search = Assert.IsType<NoWebSearch>(provider.GetRequiredService<IWebSearch>());
        Assert.Contains("WebSearch:ApiKey", search.Reason, StringComparison.Ordinal);
        Assert.False(provider.GetRequiredService<WebSearchOptions>().IsActive);
        Assert.NotNull(provider.GetRequiredService<IChunkStore>());
    }

    [Theory]
    [InlineData("tavily")]
    [InlineData("brave")]
    public void With_a_key_the_infrastructure_registers_the_http_client(string providerName)
    {
        var settings = InfrastructureFixtures.ValidSettings();
        settings["WebSearch:ApiKey"] = "k";
        settings["WebSearch:Provider"] = providerName;
        using var provider = InfrastructureFixtures.Provider(settings, new StaticTokenCredential());

        Assert.IsType<WebSearchClient>(provider.GetRequiredService<IWebSearch>());
        Assert.True(provider.GetRequiredService<WebSearchOptions>().IsActive);
    }

    [Fact]
    public void The_web_search_keys_are_optional_for_startup_validation()
    {
        using var provider = InfrastructureFixtures.Provider(InfrastructureFixtures.ValidSettings(), new StaticTokenCredential());

        provider.GetRequiredService<Microsoft.Extensions.Options.IStartupValidator>().Validate();
    }
}
