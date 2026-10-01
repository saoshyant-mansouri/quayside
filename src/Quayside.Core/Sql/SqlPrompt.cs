using System.Globalization;
using System.Text;

namespace Quayside.Core.Sql;

public static class SqlPrompt
{
    public const string Unanswerable = "CANNOT_ANSWER";

    public static string Build(IReadOnlyList<TableCard> tables, string question, int maxRows)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(question);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRows, 1);

        var limit = maxRows.ToString(CultureInfo.InvariantCulture);
        var prompt = new StringBuilder();
        prompt.Append("Write one T-SQL query for Azure SQL Database that answers the question. Reply with the SQL only: no markdown fences, no comments, no explanation.\n\n");
        prompt.Append("Rules:\n");
        prompt.Append("- Exactly one SELECT statement. Read-only. No INTO, EXEC, variables, temp tables or semicolon-separated statements.\n");
        prompt.Append($"- The outermost SELECT must start with TOP ({limit}) or a smaller number.\n");
        prompt.Append("- Every table is in schema ops. Qualify each one, for example ops.Bookings.\n");
        prompt.Append("- Use only the tables and columns listed below. Do not invent names.\n");
        prompt.Append("- Join on the foreign keys shown in the DDL.\n");
        prompt.Append($"- If the listed tables cannot answer the question, reply with exactly {Unanswerable}.\n\n");
        prompt.Append("Tables:\n");
        foreach (var table in tables)
            prompt.Append(table.Ddl.Trim()).Append("\n\n");
        prompt.Append("Question: ").Append(question.Trim()).Append('\n');
        return prompt.ToString();
    }
}
