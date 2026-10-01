using System.Text.Json;
using System.Text.Json.Serialization;

namespace Quayside.Api.Contract;

public static class SseJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(ISseEvent sseEvent) => JsonSerializer.Serialize(sseEvent, sseEvent.GetType(), Options);

    public static string Frame(ISseEvent sseEvent) => $"event: {sseEvent.EventName}\ndata: {Serialize(sseEvent)}\n\n";
}
