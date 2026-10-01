using Quayside.Core;
using Quayside.Core.Sql;

namespace Quayside.Api.Offline;

public sealed class CannedSqlExecutor : IReadOnlySqlExecutor
{
    public Task<SqlResultSet> ExecuteAsync(string sql, CancellationToken ct) => Task.FromResult(Answer(sql));

    private static SqlResultSet Answer(string sql)
    {
        if (sql.Contains("FROM ops.ContainerMovements m", StringComparison.Ordinal))
        {
            return Result(
                ["MovementCode", "PortName", "UnLocode", "OccurredAtUtc", "IsLaden"],
                [["DISCHARGE", "Rotterdam", "NLRTM", new DateTime(2026, 9, 24, 6, 30, 0, DateTimeKind.Utc), true],
                 ["LOAD", "Antwerp", "BEANR", new DateTime(2026, 9, 17, 14, 5, 0, DateTimeKind.Utc), true]]);
        }

        if (sql.Contains("FROM ops.Containers c", StringComparison.Ordinal))
        {
            return sql.Contains("MSCU1234567", StringComparison.Ordinal)
                ? Result(["ContainerNumber", "TypeCode", "TypeName", "StatusName", "ManufactureYear"], [["MSCU1234567", "42G1", "40 foot dry", "In service", 2019]])
                : Result(["ContainerNumber"], []);
        }

        if (sql.Contains("FROM ops.SailingSchedules s", StringComparison.Ordinal))
        {
            return Result(
                ["VoyageNumber", "VesselName", "OriginPort", "DestinationPort", "ScheduledDeparture", "TransitDays"],
                [["QS2641E", "Quayside Aurora", "Rotterdam", "Singapore", new DateTime(2026, 10, 12, 18, 0, 0, DateTimeKind.Utc), 28]]);
        }

        if (sql.Contains("FROM ops.Vessels v", StringComparison.Ordinal))
        {
            return Result(["VesselName", "ImoNumber", "ClassName", "TeuCapacity"], [["Quayside Aurora", "9000001", "Ultra Large Container Vessel", 23656]]);
        }

        if (sql.Contains("FROM ops.Ports p", StringComparison.Ordinal))
        {
            return Result(["PortName", "UnLocode", "CountryName", "IsHubPort"], [["Rotterdam", "NLRTM", "Netherlands", true]]);
        }

        return Result(
            ["PortName", "Containers"],
            [["Rotterdam", 1840], ["Antwerp", 1522], ["Hamburg", 1210], ["Valencia", 987], ["Genoa", 864]]);
    }

    private static SqlResultSet Result(string[] columns, object?[][] rows) =>
        new(columns, rows.Select(row => (IReadOnlyList<object?>)row).ToArray(), 1.0);
}
