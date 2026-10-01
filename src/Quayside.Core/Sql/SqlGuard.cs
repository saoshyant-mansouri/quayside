using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Quayside.Core.Sql;

public static class SqlGuard
{
    public const string Schema = "ops";
    public const int MaxLength = 8000;
    public const int MaxNesting = 32;

    public static SqlValidation Validate(string sql, IReadOnlySet<string> allowedTables, int maxRows)
    {
        ArgumentNullException.ThrowIfNull(allowedTables);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRows, 1);

        if (string.IsNullOrWhiteSpace(sql)) return Reject("The SQL is empty.");
        if (sql.Length > MaxLength) return Reject($"The SQL is longer than {MaxLength} characters.");

        var parser = new TSql170Parser(initialQuotedIdentifiers: true);

        IList<TSqlParserToken> tokens;
        IList<ParseError> errors;
        using (var reader = new StringReader(sql))
            tokens = parser.GetTokenStream(reader, out errors);
        if (errors.Count > 0) return Reject(Describe(errors));

        var tokenViolation = InspectTokens(tokens);
        if (tokenViolation is not null) return Reject(tokenViolation);

        var fragment = parser.Parse(tokens, out errors);
        if (errors.Count > 0) return Reject(Describe(errors));

        var statements = (fragment as TSqlScript)?.Batches.SelectMany(b => b.Statements).ToList() ?? [];
        var structural = InspectStatements(statements);
        if (structural.Count > 0) return Reject(structural);

        var select = (SelectStatement)statements[0];
        var violations = new List<string>();

        var allowed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in allowedTables) allowed[BareName(name)] = BareName(name);

        var inspector = new SelectInspector(allowed);
        inspector.Inspect(select);
        violations.AddRange(inspector.Violations);
        violations.AddRange(InspectRowLimit(select, maxRows));

        if (violations.Count > 0) return Reject(violations);

        return new SqlValidation(true, null, inspector.TablesTouched);
    }

    private static string? InspectTokens(IList<TSqlParserToken> tokens)
    {
        var depth = 0;
        foreach (var token in tokens)
        {
            switch (token.TokenType)
            {
                case TSqlTokenType.SingleLineComment:
                case TSqlTokenType.MultilineComment:
                    return "Comments are not allowed in generated SQL.";
                case TSqlTokenType.LeftParenthesis:
                    if (++depth > MaxNesting) return $"Parentheses are nested deeper than {MaxNesting} levels.";
                    break;
                case TSqlTokenType.RightParenthesis:
                    depth--;
                    break;
            }
        }
        return null;
    }

    private static List<string> InspectStatements(IReadOnlyList<TSqlStatement> statements)
    {
        var violations = new List<string>();
        if (statements.Count != 1)
            violations.Add(statements.Count == 0
                ? "No statement found; exactly one SELECT is required."
                : $"Exactly one statement is allowed but {statements.Count} were found.");

        foreach (var statement in statements)
        {
            if (statement is not SelectStatement) violations.Add(Disallowed(statement));
        }
        return violations;
    }

    private static string Disallowed(TSqlStatement statement) => statement switch
    {
        InsertStatement => "INSERT is not allowed; only a read-only SELECT is.",
        UpdateStatement => "UPDATE is not allowed; only a read-only SELECT is.",
        DeleteStatement => "DELETE is not allowed; only a read-only SELECT is.",
        MergeStatement => "MERGE is not allowed; only a read-only SELECT is.",
        TruncateTableStatement => "TRUNCATE is not allowed; only a read-only SELECT is.",
        ExecuteStatement => "EXEC is not allowed: no stored procedures, sp_/xp_ calls or dynamic SQL.",
        GrantStatement or DenyStatement or RevokeStatement => "Permission statements (GRANT, DENY, REVOKE) are not allowed.",
        WaitForStatement => "WAITFOR is not allowed.",
        DeclareCursorStatement or OpenCursorStatement or FetchCursorStatement or CloseCursorStatement or DeallocateCursorStatement
            => "Cursors are not allowed.",
        DeclareVariableStatement or SetVariableStatement => "Variables are not allowed.",
        PredicateSetStatement => "SET statements are not allowed; only a read-only SELECT is.",
        _ => Verb(statement.GetType().Name) is { } verb
            ? $"{verb} is not allowed; only a read-only SELECT is."
            : "This kind of statement is not allowed; only a read-only SELECT is."
    };

    private static string? Verb(string typeName)
    {
        foreach (var verb in new[] { "Create", "Drop", "Alter" })
            if (typeName.StartsWith(verb, StringComparison.Ordinal)) return verb.ToUpperInvariant();
        return null;
    }

    private static List<string> InspectRowLimit(SelectStatement select, int maxRows)
    {
        var query = select.QueryExpression;
        while (query is QueryParenthesisExpression parenthesised) query = parenthesised.QueryExpression;

        if (query is not QuerySpecification specification)
            return ["The outermost query must be a single SELECT with its own TOP (n); wrap UNION or INTERSECT in a derived table."];

        var top = specification.TopRowFilter;
        if (top is null) return [$"A TOP (n) clause is required, with n at most {maxRows}."];
        if (top.Percent) return ["TOP ... PERCENT is not allowed; use TOP (n) with a row count."];
        if (top.WithTies) return ["TOP ... WITH TIES is not allowed because it can return more than n rows."];

        var expression = top.Expression;
        while (expression is ParenthesisExpression inner) expression = inner.Expression;

        if (expression is not IntegerLiteral literal || !long.TryParse(literal.Value, out var rows))
            return ["TOP must be a plain integer literal."];

        return rows > maxRows ? [$"TOP ({rows}) is above the maximum of {maxRows} rows."] : [];
    }

    private static string Describe(IList<ParseError> errors)
    {
        var first = errors[0];
        return $"The SQL does not parse: {first.Message} (line {first.Line}, column {first.Column}).";
    }

    private static string BareName(string table)
    {
        var dot = table.LastIndexOf('.');
        return (dot < 0 ? table : table[(dot + 1)..]).Trim('[', ']');
    }

    private static SqlValidation Reject(string reason) => new(false, reason, []);

    private static SqlValidation Reject(IEnumerable<string> reasons) => Reject(string.Join(" ", reasons.Distinct()));
}
