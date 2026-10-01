using Microsoft.Data.SqlClient;

namespace Quayside.Infrastructure.Sql;

public sealed class SqlDatabase
{
    private readonly string connectionString;

    public SqlDatabase(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var builder = new SqlConnectionStringBuilder(connectionString);
        if (builder.ApplicationIntent == ApplicationIntent.ReadOnly)
            throw new ArgumentException("The read-write database must not use ApplicationIntent=ReadOnly.", nameof(connectionString));
        this.connectionString = builder.ConnectionString;
    }

    public Task<SqlConnection> OpenAsync(CancellationToken cancellationToken) =>
        SqlRetry.OpenAsync(connectionString, cancellationToken);
}
