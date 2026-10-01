using System.Globalization;
using Quayside.Api.Orchestration;
using Quayside.Core;
using Quayside.Core.Sql;

namespace Quayside.Api.Tools;

public sealed class OperationalLookups(IReadOnlySqlExecutor executor)
{
    public async Task<ToolOutcome> TrackContainerAsync(TurnContext turn, string containerNumber, CancellationToken ct)
    {
        var summary = await RunAsync(OperationalQueries.ContainerSummary(containerNumber), ct);
        var movements = summary.Rows.Count == 0
            ? summary
            : await RunAsync(OperationalQueries.ContainerMovements(containerNumber), ct);

        Mark(turn);
        if (summary.Rows.Count == 0)
        {
            return Found($"No container {containerNumber.Trim().ToUpperInvariant()} exists in the demo database.", 0);
        }

        var text = $"Container:\n{ResultText.Table(summary.Columns, summary.Rows)}\n\nLatest movements, newest first:\n{ResultText.Table(movements.Columns, movements.Rows)}";
        return Found(text, movements.Rows.Count);
    }

    public Task<ToolOutcome> FindSchedulesAsync(TurnContext turn, string origin, string destination, string? earliestDeparture, CancellationToken ct) =>
        LookUpAsync(turn, OperationalQueries.Schedules(origin, destination, ParseDate(earliestDeparture)), "No published sailings match that origin and destination.", ct);

    public Task<ToolOutcome> GetVesselAsync(TurnContext turn, string nameOrImo, CancellationToken ct) =>
        LookUpAsync(turn, OperationalQueries.Vessel(nameOrImo), "No vessel in the demo fleet matches that name or IMO number.", ct);

    public Task<ToolOutcome> GetPortAsync(TurnContext turn, string nameOrLocode, CancellationToken ct) =>
        LookUpAsync(turn, OperationalQueries.Port(nameOrLocode), "No port in the demo network matches that name or UN/LOCODE.", ct);

    private async Task<ToolOutcome> LookUpAsync(TurnContext turn, OperationalQuery query, string emptyMessage, CancellationToken ct)
    {
        var result = await RunAsync(query, ct);
        Mark(turn);
        return result.Rows.Count == 0
            ? Found(emptyMessage, 0)
            : Found(ResultText.Table(result.Columns, result.Rows), result.Rows.Count);
    }

    private async Task<SqlResultSet> RunAsync(OperationalQuery query, CancellationToken ct)
    {
        var validation = SqlGuard.Validate(query.Sql, query.Tables, query.MaxRows);
        if (!validation.Accepted)
        {
            throw new InvalidOperationException($"Built-in query rejected by the guard: {validation.Reason}");
        }

        return await executor.ExecuteAsync(query.Sql, ct);
    }

    private static void Mark(TurnContext turn)
    {
        turn.OperationalEvidence = true;
        turn.Gate.Open();
    }

    private static ToolOutcome Found(string text, int rows) => new(
        $"{ResultText.SyntheticLabel}\n{text}",
        new Dictionary<string, object?> { ["rows"] = rows, ["synthetic"] = true });

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var date)
            ? date
            : throw new InvalidToolInputException("The earliest departure must be a date such as 2026-11-01.");
    }
}
