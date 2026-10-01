using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Quayside.Infrastructure.Configuration;

public sealed class QuaysideOptionsValidator : IValidateOptions<QuaysideOptions>
{
    public ValidateOptionsResult Validate(string? name, QuaysideOptions options)
    {
        var failures = new List<string>();

        Require(failures, options.AzureOpenAI.Endpoint, ConfigurationKeys.OpenAIEndpoint);
        Require(failures, options.AzureOpenAI.ChatDeployment, ConfigurationKeys.OpenAIChatDeployment);
        Require(failures, options.AzureOpenAI.EmbeddingDeployment, ConfigurationKeys.OpenAIEmbeddingDeployment);
        Require(failures, options.Sql.ReadWrite, ConfigurationKeys.SqlConnection);
        Require(failures, options.Sql.ReadOnly, ConfigurationKeys.SqlReadOnlyConnection);

        if (options.AzureOpenAI.Endpoint is { } endpoint && !IsHttpsEndpoint(endpoint))
            failures.Add($"Configuration key '{ConfigurationKeys.OpenAIEndpoint}' must be an absolute https URL.");

        if (options.Sql.ReadWrite is { } readWrite && !IsConnectionString(readWrite))
            failures.Add($"Configuration key '{ConfigurationKeys.SqlConnection}' is not a valid SQL connection string.");

        if (options.Sql.ReadOnly is { } readOnly)
            failures.AddRange(ReadOnlyFailures(readOnly, options.Sql.ReadWrite));

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void Require(List<string> failures, string? value, string key)
    {
        if (value is null)
            failures.Add($"Missing required configuration key '{key}'.");
    }

    private static bool IsHttpsEndpoint(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private static bool IsConnectionString(string value)
    {
        try
        {
            _ = new SqlConnectionStringBuilder(value);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static IEnumerable<string> ReadOnlyFailures(string readOnly, string? readWrite)
    {
        if (!IsConnectionString(readOnly))
        {
            yield return $"Configuration key '{ConfigurationKeys.SqlReadOnlyConnection}' is not a valid SQL connection string.";
            yield break;
        }

        if (!ReadOnlyConnectionString.HasReadOnlyIntent(readOnly))
            yield return $"Configuration key '{ConfigurationKeys.SqlReadOnlyConnection}' must contain ApplicationIntent=ReadOnly.";

        if (readWrite is not null && IsConnectionString(readWrite) && SameLogin(readOnly, readWrite))
            yield return $"Configuration key '{ConfigurationKeys.SqlReadOnlyConnection}' must use a different login from '{ConfigurationKeys.SqlConnection}'.";
    }

    private static bool SameLogin(string left, string right)
    {
        var a = new SqlConnectionStringBuilder(left);
        var b = new SqlConnectionStringBuilder(right);
        return string.Equals(a.UserID, b.UserID, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.DataSource, b.DataSource, StringComparison.OrdinalIgnoreCase);
    }
}
