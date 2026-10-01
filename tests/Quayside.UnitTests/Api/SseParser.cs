using System.Text.Json;

namespace Quayside.UnitTests.Api;

public sealed record SseEvent(string Name, string Data, JsonElement Json)
{
    public string[] Keys => Json.ValueKind == JsonValueKind.Object
        ? Json.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray()
        : [];

    public JsonElement this[string property] => Json.GetProperty(property);
}

public static class SseParser
{
    private const string EventPrefix = "event: ";
    private const string DataPrefix = "data: ";

    public static IReadOnlyList<SseEvent> Parse(string raw)
    {
        if (raw.Length == 0)
        {
            return [];
        }

        if (!raw.EndsWith("\n\n", StringComparison.Ordinal))
        {
            throw new FormatException("The stream does not end with a blank line.");
        }

        var events = new List<SseEvent>();
        foreach (var block in raw[..^2].Split("\n\n"))
        {
            events.Add(ParseBlock(block));
        }

        return events;
    }

    private static SseEvent ParseBlock(string block)
    {
        var lines = block.Split('\n');
        if (lines.Length != 2)
        {
            throw new FormatException($"An event must be exactly an event line and a data line, got {lines.Length} lines.");
        }

        if (!lines[0].StartsWith(EventPrefix, StringComparison.Ordinal))
        {
            throw new FormatException($"The first line of an event must start with '{EventPrefix}'.");
        }

        if (!lines[1].StartsWith(DataPrefix, StringComparison.Ordinal))
        {
            throw new FormatException($"The second line of an event must start with '{DataPrefix}'.");
        }

        var name = lines[0][EventPrefix.Length..];
        var data = lines[1][DataPrefix.Length..];
        using var document = JsonDocument.Parse(data);
        return new SseEvent(name, data, document.RootElement.Clone());
    }
}
