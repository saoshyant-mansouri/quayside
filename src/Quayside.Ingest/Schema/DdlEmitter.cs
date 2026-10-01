using System.Text;

namespace Quayside.Ingest.Schema;

public static class DdlEmitter
{
    public const string SchemaName = "ops";

    public static string Emit(SchemaSpec spec)
    {
        var sql = new StringBuilder();
        sql.Append("IF SCHEMA_ID(N'ops') IS NULL EXEC(N'CREATE SCHEMA ops');\n");

        foreach (var table in spec.Tables)
        {
            sql.Append($"\nIF OBJECT_ID(N'ops.{table.Name}', N'U') IS NULL\n");
            sql.Append($"CREATE TABLE ops.{table.Name} (\n");
            foreach (var column in table.Columns)
                sql.Append($"    {column.Name} {column.Type}{(column.Identity ? " IDENTITY(1,1)" : "")} {(column.Nullable ? "NULL" : "NOT NULL")},\n");
            foreach (var column in table.Columns.Where(c => c.Unique))
                sql.Append($"    CONSTRAINT UQ_{table.Name}_{column.Name} UNIQUE ({column.Name}),\n");
            sql.Append($"    CONSTRAINT PK_{table.Name} PRIMARY KEY ({table.Key.Name})\n);\n");
        }

        foreach (var fk in spec.ForeignKeys())
        {
            var name = $"FK_{fk.Table.Name}_{fk.Column.Name}";
            sql.Append($"\nIF OBJECT_ID(N'ops.{name}', N'F') IS NULL\n");
            sql.Append($"ALTER TABLE ops.{fk.Table.Name} ADD CONSTRAINT {name} FOREIGN KEY ({fk.Column.Name}) REFERENCES ops.{fk.Target.Table} ({fk.Target.Column});\n");
        }

        foreach (var fk in spec.ForeignKeys().Where(f => !f.Column.Unique))
        {
            var name = $"IX_{fk.Table.Name}_{fk.Column.Name}";
            sql.Append($"\nIF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{name}' AND object_id = OBJECT_ID(N'ops.{fk.Table.Name}'))\n");
            sql.Append($"CREATE INDEX {name} ON ops.{fk.Table.Name} ({fk.Column.Name});\n");
        }

        return sql.ToString();
    }
}
