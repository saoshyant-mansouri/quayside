using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Quayside.Infrastructure.Configuration;

namespace Quayside.Infrastructure.Telemetry;

public static class TelemetryServices
{
    public static IServiceCollection AddQuaysideTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = QuaysideOptions.From(configuration).ApplicationInsightsConnectionString;
        var serviceName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name?.ToLowerInvariant() ?? TelemetryNames.DefaultServiceName;

        var builder = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddSource(TelemetryNames.AiSourceName)
                .AddSource(TelemetryNames.SqlSourceName))
            .WithMetrics(metrics => metrics.AddMeter(TelemetryNames.AiSourceName));

        if (connectionString is not null)
            builder.UseAzureMonitorExporter(options =>
            {
                options.ConnectionString = connectionString;
                options.DisableOfflineStorage = true;
            });

        return services;
    }
}
