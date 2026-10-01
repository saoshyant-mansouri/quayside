namespace Quayside.Api;

public sealed class GroundingOptions
{
    public const string Section = "Quayside:Grounding";

    public double MinTopCosine { get; set; } = 0.25;
}

public sealed class RetrievalOptions
{
    public const string Section = "Quayside:Retrieval";

    public int TopK { get; set; } = 6;
}

public sealed class SqlGenerationOptions
{
    public const string Section = "Quayside:Sql";

    public int MaxRows { get; set; } = 50;

    public int SchemaTables { get; set; } = 8;
}

public sealed class CacheOptions
{
    public const string Section = "Quayside:Cache";

    public double MinSimilarity { get; set; } = 0.97;
}

public sealed class ChatLimits
{
    public const string Section = "Quayside:Chat";

    public int MaxMessageLength { get; set; } = 2000;

    public int MaxConversations { get; set; } = 500;

    public int MaxTurnsPerConversation { get; set; } = 8;

    public int WarmupWaitSeconds { get; set; } = 15;

    public int MaxToolIterations { get; set; } = 6;
}

public sealed class HydrationOptions
{
    public const string Section = "Quayside:Hydration";

    public int RetryDelaySeconds { get; set; } = 5;
}

public sealed class CorsOptions
{
    public const string Section = "Cors";

    public string[] AllowedOrigins { get; set; } = [];
}
