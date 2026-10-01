using Quayside.Core.Sql;

namespace Quayside.Ingest.Schema;

public static class SchemaFile
{
    public static SchemaSpec Load(string path)
    {
        using var stream = File.OpenRead(path);
        return SchemaSpec.Load(stream);
    }
}
