using System.Text.Json.Serialization;

namespace Quayside.Api.Contract;

public sealed record ToolEvent(string Name, string Status, IReadOnlyDictionary<string, object?>? Detail = null) : ISseEvent
{
    string ISseEvent.EventName => "tool";
}

public sealed record CitationPayload(
    int N,
    string Title,
    string Url,
    string Source,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? PublishedAt);

public sealed record CitationsEvent(IReadOnlyList<CitationPayload> Citations) : ISseEvent
{
    string ISseEvent.EventName => "citations";
}

public sealed record SqlEvent(
    string Sql,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Rejected) : ISseEvent
{
    string ISseEvent.EventName => "sql";
}

public sealed record TokenEvent(string Text) : ISseEvent
{
    string ISseEvent.EventName => "token";
}

public sealed record UsagePayload(long Prompt, long Completion);

public sealed record GroundingPayload(int Cited, int Uncited);

public sealed record DoneEvent(
    string ConversationId,
    long LatencyMs,
    bool Cached,
    UsagePayload Usage,
    GroundingPayload Grounding) : ISseEvent
{
    string ISseEvent.EventName => "done";
}

public sealed record ErrorEvent(string Message) : ISseEvent
{
    string ISseEvent.EventName => "error";
}
