using Dapper;
using Quayside.Infrastructure.Telemetry;

namespace Quayside.Infrastructure.Sql;

public sealed class SqlQueryLog(SqlDatabase database) : IQueryLog
{
    private const string Insert = """
        INSERT dbo.QueryLog (At, Kind, Question, GeneratedSql, Outcome, Detail, RowCount, ElapsedMs)
        VALUES (SYSDATETIMEOFFSET(), @Kind, @Question, @GeneratedSql, @Outcome, @Detail, @RowCount, @ElapsedMs)
        """;

    public async Task AppendAsync(QueryLogEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        using var activity = SqlActivity.Start("query_log.append");
        try
        {
            await using var connection = await database.OpenAsync(cancellationToken).ConfigureAwait(false);
            var command = new CommandDefinition(
                Insert,
                new
                {
                    entry.Kind,
                    Question = Truncate(entry.Question, 2000),
                    entry.GeneratedSql,
                    Outcome = Truncate(entry.Outcome, 64),
                    Detail = entry.Detail is null ? null : Truncate(entry.Detail, 2000),
                    entry.RowCount,
                    entry.ElapsedMs
                },
                cancellationToken: cancellationToken);
            await connection.ExecuteAsync(command).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            activity.Fail(exception);
            throw;
        }
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
