using System.Text;
using Quayside.Core.Sql;

namespace Quayside.Infrastructure.Schema;

public static class TableDdl
{
    public const string SchemaName = "ops";

    public static string For(TableSpec table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var ddl = new StringBuilder($"CREATE TABLE {SchemaName}.{table.Name} (\n");
        foreach (var column in table.Columns)
        {
            ddl.Append($"    {column.Name} {column.Type} {(column.Nullable ? "NULL" : "NOT NULL")}");
            if (column.PrimaryKey) ddl.Append(" PRIMARY KEY");
            if (column.References is { } target) ddl.Append($" REFERENCES {SchemaName}.{target.Table}({target.Column})");
            ddl.Append(",\n");
        }
        return ddl.Append(");").ToString();
    }
}
