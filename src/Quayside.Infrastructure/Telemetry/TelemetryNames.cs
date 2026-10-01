using System.Diagnostics;

namespace Quayside.Infrastructure.Telemetry;

public static class TelemetryNames
{
    public const string AiSourceName = "Quayside.AI";
    public const string SqlSourceName = "Quayside.Sql";
    public const string DefaultServiceName = "quayside";

    public const string InputTokens = "gen_ai.usage.input_tokens";
    public const string OutputTokens = "gen_ai.usage.output_tokens";

    public static readonly ActivitySource Ai = new(AiSourceName);
    public static readonly ActivitySource Sql = new(SqlSourceName);
}
