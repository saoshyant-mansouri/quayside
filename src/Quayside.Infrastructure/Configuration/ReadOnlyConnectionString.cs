using Microsoft.Data.SqlClient;

namespace Quayside.Infrastructure.Configuration;

public static class ReadOnlyConnectionString
{
    public static bool HasReadOnlyIntent(string connectionString)
    {
        try
        {
            return new SqlConnectionStringBuilder(connectionString).ApplicationIntent == ApplicationIntent.ReadOnly;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static string Require(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        if (!HasReadOnlyIntent(connectionString))
            throw new InvalidOperationException("The read-only SQL executor requires a connection string with ApplicationIntent=ReadOnly.");

        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            ApplicationIntent = ApplicationIntent.ReadOnly,
            ApplicationName = "quayside-readonly",
            Pooling = true
        };
        return builder.ConnectionString;
    }
}
