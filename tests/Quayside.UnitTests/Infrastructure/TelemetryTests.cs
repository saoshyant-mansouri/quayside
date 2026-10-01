using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using Quayside.Infrastructure.Telemetry;

namespace Quayside.UnitTests.Infrastructure;

public sealed class TelemetryTests
{
    [Fact]
    public void Sql_spans_are_sampled_once_telemetry_is_registered()
    {
        using var provider = InfrastructureFixtures.Provider(InfrastructureFixtures.ValidSettings(), new StaticTokenCredential());
        using var tracer = provider.GetRequiredService<TracerProvider>();

        using var activity = SqlActivity.Start("probe", "SELECT 1");

        Assert.NotNull(activity);
        Assert.Equal("SELECT 1", activity.GetTagItem("db.query.text"));
        Assert.Equal("microsoft.sql_server", activity.GetTagItem("db.system.name"));
    }

    [Fact]
    public void The_azure_monitor_exporter_is_wired_only_with_a_connection_string()
    {
        var settings = InfrastructureFixtures.ValidSettings();
        settings["APPLICATIONINSIGHTS_CONNECTION_STRING"] = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=http://127.0.0.1:1/";
        using var provider = InfrastructureFixtures.Provider(settings, new StaticTokenCredential());

        using var tracer = provider.GetRequiredService<TracerProvider>();

        Assert.NotNull(tracer);
    }

    [Fact]
    public void Failures_are_recorded_on_the_span()
    {
        using var provider = InfrastructureFixtures.Provider(InfrastructureFixtures.ValidSettings(), new StaticTokenCredential());
        using var tracer = provider.GetRequiredService<TracerProvider>();

        using var activity = SqlActivity.Start("probe");
        activity.Fail(new InvalidOperationException("boom"));

        Assert.Equal(System.Diagnostics.ActivityStatusCode.Error, activity!.Status);
        Assert.Equal("System.InvalidOperationException", activity.GetTagItem("error.type"));
    }
}
