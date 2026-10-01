using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Quayside.Core.Sql;

internal sealed class SelectInspector : TSqlFragmentVisitor
{
    private static readonly HashSet<TableHintKind> PermittedHints = [TableHintKind.NoLock, TableHintKind.ReadUncommitted];

    private readonly IReadOnlyDictionary<string, string> allowed;
    private readonly HashSet<string> cteNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly SortedSet<string> touched = new(StringComparer.Ordinal);
    private readonly List<string> violations = [];

    public SelectInspector(IReadOnlyDictionary<string, string> allowed) => this.allowed = allowed;

    public IReadOnlyList<string> Violations => violations;

    public IReadOnlyList<string> TablesTouched => touched.ToArray();

    public void Inspect(SelectStatement select)
    {
        if (select.Into is not null) violations.Add("SELECT ... INTO is not allowed because it creates a table.");

        foreach (var cte in select.WithCtesAndXmlNamespaces?.CommonTableExpressions ?? [])
        {
            cteNames.Add(cte.ExpressionName.Value);
            cte.QueryExpression.Accept(this);
        }
        select.QueryExpression.Accept(this);
    }

    public override void Visit(NamedTableReference node)
    {
        var name = node.SchemaObject;

        if (name.ServerIdentifier is not null)
            violations.Add($"Linked-server name '{Spell(name)}' is not allowed.");
        else if (name.DatabaseIdentifier is not null)
            violations.Add($"Cross-database name '{Spell(name)}' is not allowed.");
        else if (name.SchemaIdentifier is null)
            InspectUnqualified(name);
        else if (!string.Equals(name.SchemaIdentifier.Value, SqlGuard.Schema, StringComparison.OrdinalIgnoreCase))
            violations.Add($"Table '{Spell(name)}' is outside schema {SqlGuard.Schema}.");
        else if (allowed.TryGetValue(name.BaseIdentifier.Value, out var canonical))
            touched.Add(canonical);
        else
            violations.Add($"Table '{SqlGuard.Schema}.{name.BaseIdentifier.Value}' is not in the allowed set for this question.");

        foreach (var hint in node.TableHints)
        {
            if (!PermittedHints.Contains(hint.HintKind))
                violations.Add($"Table hint {hint.HintKind} is not allowed.");
        }
    }

    public override void Visit(TableReference node)
    {
        var rejected = node switch
        {
            NamedTableReference or QueryDerivedTable or InlineDerivedTable or QualifiedJoin or UnqualifiedJoin
                or JoinParenthesisTableReference or PivotedTableReference or UnpivotedTableReference => null,
            OpenRowsetTableReference or BulkOpenRowset => "OPENROWSET",
            OpenQueryTableReference => "OPENQUERY",
            AdHocTableReference => "OPENDATASOURCE",
            OpenJsonTableReference => "OPENJSON",
            OpenXmlTableReference => "OPENXML",
            VariableTableReference => "A table variable",
            SchemaObjectFunctionTableReference or BuiltInFunctionTableReference => "A table-valued function",
            _ => node.GetType().Name
        };
        if (rejected is not null) violations.Add($"{rejected} cannot be used as a table source.");
    }

    public override void Visit(FunctionCall node)
    {
        var name = node.FunctionName.Value;
        if (node.CallTarget is not null)
            violations.Add($"Schema-qualified function '{name}' is not allowed.");
        else if (name.StartsWith("sp_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("xp_", StringComparison.OrdinalIgnoreCase))
            violations.Add($"Calling '{name}' is not allowed.");
    }

    public override void Visit(NextValueForExpression node) =>
        violations.Add("NEXT VALUE FOR is not allowed because it changes sequence state.");

    public override void Visit(SelectSetVariable node) =>
        violations.Add("Assigning to a variable inside SELECT is not allowed.");

    public override void Visit(CursorDefinition node) =>
        violations.Add("Cursors are not allowed.");

    private void InspectUnqualified(SchemaObjectName name)
    {
        if (cteNames.Contains(name.BaseIdentifier.Value)) return;
        violations.Add($"Table '{name.BaseIdentifier.Value}' is neither a CTE in scope nor qualified with schema {SqlGuard.Schema}.");
    }

    private static string Spell(SchemaObjectName name) =>
        string.Join('.', name.Identifiers.Select(i => i.Value));
}
