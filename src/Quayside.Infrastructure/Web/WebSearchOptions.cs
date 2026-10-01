using System.Globalization;
using Microsoft.Extensions.Configuration;
using Quayside.Infrastructure.Configuration;

namespace Quayside.Infrastructure.Web;

public sealed record WebSearchOptions(string Provider, string? ApiKey, int MaxResults, bool Enabled)
{
    public const string Tavily = "tavily";
    public const string Brave = "brave";
    public const int DefaultMaxResults = 5;
    public const int CeilingMaxResults = 10;

    public bool IsActive => DisabledReason is null;

    public string? DisabledReason
    {
        get
        {
            if (!Enabled)
            {
                return $"{ConfigurationKeys.WebSearchEnabled} is false";
            }

            if (ApiKey is null)
            {
                return $"{ConfigurationKeys.WebSearchApiKey} is not set";
            }

            return Provider is Tavily or Brave
                ? null
                : $"{ConfigurationKeys.WebSearchProvider} '{Provider}' is not supported (use '{Tavily}' or '{Brave}')";
        }
    }

    public static WebSearchOptions From(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new WebSearchOptions(
            (Read(configuration, ConfigurationKeys.WebSearchProvider) ?? Tavily).ToLowerInvariant(),
            Read(configuration, ConfigurationKeys.WebSearchApiKey),
            ReadMaxResults(configuration),
            !bool.TryParse(Read(configuration, ConfigurationKeys.WebSearchEnabled), out var enabled) || enabled);
    }

    private static int ReadMaxResults(IConfiguration configuration)
    {
        var parsed = int.TryParse(Read(configuration, ConfigurationKeys.WebSearchMaxResults), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value);
        return parsed && value >= 1 ? Math.Min(value, CeilingMaxResults) : DefaultMaxResults;
    }

    private static string? Read(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
