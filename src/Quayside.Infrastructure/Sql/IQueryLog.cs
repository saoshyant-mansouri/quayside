namespace Quayside.Infrastructure.Sql;

public sealed record QueryLogEntry(
    string Kind,
    string Question,
    string? GeneratedSql,
    string Outcome,
    string? Detail,
    int? RowCount,
    double ElapsedMs);

public interface IQueryLog
{
    Task AppendAsync(QueryLogEntry entry, CancellationToken cancellationToken);
}
