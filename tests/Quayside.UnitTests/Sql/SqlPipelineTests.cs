using Quayside.Core.Sql;

namespace Quayside.UnitTests.Sql;

public sealed class SqlPipelineTests
{
    private const string Question = "What was the supply air temperature of the reefer container over time?";

    [Fact]
    public void SqlOverRetrievedTablesPassesTheGuardAndSqlOverOthersDoesNot()
    {
        var tables = SchemaFixture.Retriever.Select(SchemaFixture.Embed(Question), Question);
        var allowed = tables.Select(t => t.Table).ToHashSet(StringComparer.Ordinal);

        var legitimate = SqlGuard.Validate(
            "SELECT TOP (100) r.ReadAtUtc, r.SupplyAirTempC FROM ops.ReeferReadings r JOIN ops.Containers c ON c.ContainerId = r.ContainerId ORDER BY r.ReadAtUtc DESC",
            allowed,
            100);
        var outside = SqlGuard.Validate("SELECT TOP (100) * FROM ops.Invoices", allowed, 100);

        Assert.True(legitimate.Accepted, legitimate.Reason);
        Assert.Equal(["Containers", "ReeferReadings"], legitimate.TablesTouched);
        Assert.False(outside.Accepted);
    }
}
