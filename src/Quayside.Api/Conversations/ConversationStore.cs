using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Quayside.Api.Conversations;

public sealed partial class ConversationStore(IOptions<ChatLimits> limits)
{
    private const int MaxIdLength = 64;

    private readonly Lock gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private long clock;

    public int Count
    {
        get
        {
            lock (gate)
            {
                return entries.Count;
            }
        }
    }

    public ConversationSnapshot Open(string? requestedId)
    {
        lock (gate)
        {
            var id = IsAcceptable(requestedId) && entries.ContainsKey(requestedId!) ? requestedId! : NewId();
            var entry = GetOrAdd(id);
            entry.Touched = ++clock;
            return new ConversationSnapshot(id, [.. entry.Turns]);
        }
    }

    public void Append(string id, ChatTurn turn)
    {
        lock (gate)
        {
            var entry = GetOrAdd(id);
            entry.Touched = ++clock;
            entry.Turns.Add(turn);
            var excess = entry.Turns.Count - Math.Max(limits.Value.MaxTurnsPerConversation, 1);
            if (excess > 0)
            {
                entry.Turns.RemoveRange(0, excess);
            }
        }
    }

    private Entry GetOrAdd(string id)
    {
        if (entries.TryGetValue(id, out var existing))
        {
            return existing;
        }

        var capacity = Math.Max(limits.Value.MaxConversations, 1);
        while (entries.Count >= capacity)
        {
            entries.Remove(entries.MinBy(pair => pair.Value.Touched).Key);
        }

        var created = new Entry();
        entries[id] = created;
        return created;
    }

    private static string NewId() => Guid.NewGuid().ToString("N");

    private static bool IsAcceptable(string? id) =>
        !string.IsNullOrEmpty(id) && id.Length <= MaxIdLength && SafeId().IsMatch(id);

    [GeneratedRegex("^[A-Za-z0-9-]+$")]
    private static partial Regex SafeId();

    private sealed class Entry
    {
        public List<ChatTurn> Turns { get; } = [];

        public long Touched { get; set; }
    }
}

public sealed record ConversationSnapshot(string Id, IReadOnlyList<ChatTurn> Turns);
