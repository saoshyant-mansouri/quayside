using System.Data;
using Microsoft.Data.SqlClient;

namespace Quayside.Infrastructure.Sql;

public static class BulkStage
{
    public const int BatchSize = 1000;
    public const int TimeoutSeconds = 120;

    public static async Task ExecuteAsync(SqlConnection connection, SqlTransaction transaction, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.CommandTimeout = TimeoutSeconds;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task CopyAsync(SqlConnection connection, SqlTransaction transaction, string table, DataTable rows, CancellationToken cancellationToken)
    {
        if (rows.Rows.Count == 0) return;

        using var copy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction)
        {
            DestinationTableName = table,
            BatchSize = BatchSize,
            BulkCopyTimeout = TimeoutSeconds,
            EnableStreaming = true
        };
        foreach (DataColumn column in rows.Columns)
            copy.ColumnMappings.Add(column.ColumnName, column.ColumnName);

        await copy.WriteToServerAsync(rows, cancellationToken).ConfigureAwait(false);
    }
}
