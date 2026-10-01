namespace Quayside.Api.Tools;

public sealed record ToolOutcome(string ForModel, IReadOnlyDictionary<string, object?>? Detail = null);
