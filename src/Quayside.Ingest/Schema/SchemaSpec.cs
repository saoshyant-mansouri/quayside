using System.Text.Json;
using System.Text.Json.Serialization;

namespace Quayside.Ingest.Schema;

public sealed record ReferenceSpec(string Table, string Column, bool Self = false);

public sealed record ColumnSpec(
    string Name,
    string Type,
    bool Nullable,
    string Description,
    bool PrimaryKey = false,
    bool Identity = false,
    bool Unique = false,
    ReferenceSpec? References = null);

public sealed record TableSpec(string Name, string Context, string Description, IReadOnlyList<ColumnSpec> Columns)
{
    public ColumnSpec Key => Columns.First(c => c.PrimaryKey);
}

public sealed record ContextSpec(string Name, string Description);

public sealed record ForeignKey(TableSpec Table, ColumnSpec Column, ReferenceSpec Target);

public sealed record SchemaSpec(int SchemaVersion, IReadOnlyList<ContextSpec> Contexts, IReadOnlyList<TableSpec> Tables)
{
    public IEnumerable<ForeignKey> ForeignKeys() =>
        Tables.SelectMany(t => t.Columns.Where(c => c.References is not null).Select(c => new ForeignKey(t, c, c.References!)));

    public static SchemaSpec Load(Stream stream) =>
        JsonSerializer.Deserialize(stream, SchemaJson.Default.SchemaSpec)
        ?? throw new InvalidDataException("Schema document is empty.");

    public static SchemaSpec Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Load(stream);
    }
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(SchemaSpec))]
internal sealed partial class SchemaJson : JsonSerializerContext;
