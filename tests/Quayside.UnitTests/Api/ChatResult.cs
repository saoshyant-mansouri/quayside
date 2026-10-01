using System.Net;

namespace Quayside.UnitTests.Api;

public sealed record ChatResult(HttpStatusCode Status, string? MediaType, string Raw, IReadOnlyList<SseEvent> Events)
{
    public IReadOnlyList<string> Names => Events.Select(e => e.Name).ToArray();

    public SseEvent Done => Single("done");

    public IReadOnlyList<SseEvent> Of(string name) => Events.Where(e => e.Name == name).ToArray();

    public SseEvent Single(string name) => Of(name).Single();

    public int IndexOfFirst(string name) => Events.ToList().FindIndex(e => e.Name == name);

    public string Answer => string.Concat(Of("token").Select(e => e["text"].GetString()));

    public IReadOnlyList<SseEvent> ToolEvents(string name, string status) =>
        Of("tool").Where(e => e["name"].GetString() == name && e["status"].GetString() == status).ToArray();

    public static ChatResult From(HttpStatusCode status, string? mediaType, string raw)
    {
        var isStream = string.Equals(mediaType, "text/event-stream", StringComparison.Ordinal);
        return new ChatResult(status, mediaType, raw, isStream ? SseParser.Parse(raw) : []);
    }
}
