using System.Diagnostics;

namespace Quayside.Infrastructure.Telemetry;

public static class SqlActivity
{
    public static Activity? Start(string operation, string? statement = null)
    {
        var activity = TelemetryNames.Sql.StartActivity(operation, ActivityKind.Client);
        if (activity is null) return null;

        activity.SetTag("db.system.name", "microsoft.sql_server");
        activity.SetTag("db.operation.name", operation);
        if (statement is not null) activity.SetTag("db.query.text", statement);
        return activity;
    }

    public static void Rows(this Activity? activity, long count) => activity?.SetTag("quayside.db.rows", count);

    public static void Fail(this Activity? activity, Exception exception)
    {
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity?.SetTag("error.type", exception.GetType().FullName);
    }
}
