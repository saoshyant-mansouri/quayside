using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Quayside.Core;
using Quayside.Core.Sql;
using Quayside.Infrastructure.Configuration;
using Quayside.Infrastructure.Telemetry;

namespace Quayside.Infrastructure.Sql;

public sealed class ReadOnlySqlExecutor : IReadOnlySqlExecutor
{
    public const int CommandTimeoutSeconds = 5;
    public const int MaxRows = 1000;

    private const string PrivilegeProbe = """
        SELECT
            ISNULL(IS_MEMBER('db_owner'), 0)
          + ISNULL(IS_MEMBER('db_datawriter'), 0)
          + ISNULL(IS_MEMBER('db_ddladmin'), 0)
          + ISNULL(IS_MEMBER('db_securityadmin'), 0)
          + ISNULL(IS_MEMBER('db_accessadmin'), 0)
        """;

    private readonly string connectionString;
    private int privilegesVerified;

    public ReadOnlySqlExecutor(string readOnlyConnectionString)
    {
        connectionString = ReadOnlyConnectionString.Require(readOnlyConnectionString);
    }

    public async Task<SqlResultSet> ExecuteAsync(string sql, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        using var activity = SqlActivity.Start("query.execute", sql);
        var clock = Stopwatch.StartNew();
        try
        {
            await using var connection = await SqlRetry.OpenAsync(connectionString, ct).ConfigureAwait(false);
            await EnsureLeastPrivilegeAsync(connection, ct).ConfigureAwait(false);

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = CommandTimeoutSeconds;

            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleResult, ct).ConfigureAwait(false);

            var columns = new string[reader.FieldCount];
            for (var i = 0; i < columns.Length; i++) columns[i] = reader.GetName(i);

            var rows = new List<IReadOnlyList<object?>>();
            while (rows.Count < MaxRows && await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var row = new object?[columns.Length];
                for (var i = 0; i < row.Length; i++)
                    row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(row);
            }

            clock.Stop();
            activity.Rows(rows.Count);
            return new SqlResultSet(columns, rows, clock.Elapsed.TotalMilliseconds);
        }
        catch (Exception exception)
        {
            activity.Fail(exception);
            throw;
        }
    }

    private async Task EnsureLeastPrivilegeAsync(SqlConnection connection, CancellationToken ct)
    {
        if (Volatile.Read(ref privilegesVerified) == 1) return;

        await using var probe = connection.CreateCommand();
        probe.CommandText = PrivilegeProbe;
        probe.CommandTimeout = CommandTimeoutSeconds;
        var elevated = Convert.ToInt32(await probe.ExecuteScalarAsync(ct).ConfigureAwait(false));
        if (elevated != 0)
            throw new InvalidOperationException("The read-only SQL login is a member of a write-capable database role; refusing to execute generated SQL.");

        Volatile.Write(ref privilegesVerified, 1);
    }
}
