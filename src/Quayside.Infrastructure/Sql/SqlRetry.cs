using Microsoft.Data.SqlClient;
using Quayside.Infrastructure.Ai;

namespace Quayside.Infrastructure.Sql;

public static class SqlRetry
{
    public const int MaxAttempts = 6;

    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(15);

    public static async Task<SqlConnection> OpenAsync(string connectionString, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var connection = new SqlConnection(connectionString);
            try
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                return connection;
            }
            catch (SqlException exception) when (exception.IsTransient && attempt < MaxAttempts)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                var delay = BackoffRetryPolicy.Backoff(attempt, BaseDelay, MaxDelay, Random.Shared.NextDouble());
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
    }
}
