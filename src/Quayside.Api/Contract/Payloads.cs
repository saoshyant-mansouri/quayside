using System.Text.Json.Serialization;

namespace Quayside.Api.Contract;

public sealed record HealthPayload(
    string Status,
    int Chunks,
    int Documents,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTime? CorpusCapturedAt,
    bool IndexWarm);

public sealed record ExamplePayload(string Label, string Question);

public sealed record ProblemPayload(string Error);
