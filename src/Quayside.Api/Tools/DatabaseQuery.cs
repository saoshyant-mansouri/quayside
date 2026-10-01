using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Quayside.Api.Contract;
using Quayside.Api.Hosting;
using Quayside.Api.Orchestration;
using Quayside.Core;
using Quayside.Core.Sql;

namespace Quayside.Api.Tools;

public sealed class DatabaseQuery(
    Hydrated<SchemaRetriever> schema,
    IChatClient chat,
    IReadOnlySqlExecutor executor,
    IOptions<SqlGenerationOptions> sqlOptions,
    IOptions<ChatLimits> limits,
    ILogger<DatabaseQuery> logger)
{
    public async Task<ToolOutcome> RunAsync(TurnContext turn, string question, CancellationToken ct)
    {
        var retriever = await schema.WaitAsync(TimeSpan.FromSeconds(limits.Value.WarmupWaitSeconds), ct);
        if (retriever is null)
        {
            return new ToolOutcome(Prompts.SchemaWarming, new Dictionary<string, object?> { ["warm"] = false });
        }

        var text = string.IsNullOrWhiteSpace(question) ? turn.Question : question;
        var options = sqlOptions.Value;
        var tables = retriever.Select(turn.QuestionEmbedding, text, options.SchemaTables);
        var detail = new Dictionary<string, object?> { ["tables"] = tables.Select(t => t.Table).ToArray(), ["synthetic"] = true };

        if (tables.Count == 0)
        {
            return await RefuseAsync(turn, string.Empty, "No table in the demo schema relates to this question.", detail, ct);
        }

        var prompt = SqlPrompt.Build(tables, text, options.MaxRows);
        var response = await chat.GetResponseAsync(prompt, cancellationToken: ct);
        turn.Usage.Add(response.Usage);
        var sql = StripFences(response.Text);

        if (IsUnanswerable(sql))
        {
            detail["cannotAnswer"] = true;
            return await RefuseAsync(turn, string.Empty, "The retrieved tables cannot answer this question.", detail, ct);
        }

        var allowed = tables.Select(t => t.Table).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var validation = SqlGuard.Validate(sql, allowed, options.MaxRows);
        if (!validation.Accepted)
        {
            detail["rejected"] = true;
            return await RefuseAsync(turn, sql, validation.Reason ?? "The statement was rejected.", detail, ct);
        }

        SqlResultSet result;
        try
        {
            result = await executor.ExecuteAsync(sql, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Generated SQL failed to execute ({ExceptionType})", ex.GetType().Name);
            detail["error"] = true;
            return await RefuseAsync(turn, sql, "The database could not run the query right now. It may be waking from idle; try again in about thirty seconds.", detail, ct);
        }

        var rows = result.Rows.Take(options.MaxRows).Select(Sanitise).ToArray();
        await Emit(turn, new SqlEvent(sql, result.Columns, rows, null), ct);
        detail["rows"] = rows.Length;
        detail["elapsedMs"] = Math.Round(result.ElapsedMs, 1);
        return new ToolOutcome(
            $"{ResultText.SyntheticLabel}\nSQL executed:\n{sql}\n\nResult ({rows.Length} rows):\n{ResultText.Table(result.Columns, rows)}",
            detail);
    }

    private static async Task<ToolOutcome> RefuseAsync(TurnContext turn, string sql, string reason, Dictionary<string, object?> detail, CancellationToken ct)
    {
        await Emit(turn, new SqlEvent(sql, [], [], reason), ct);
        return new ToolOutcome(
            $"REJECTED: {reason} Nothing was executed. Explain this to the user in one or two sentences and do not invent results.",
            detail);
    }

    private static async Task Emit(TurnContext turn, SqlEvent sqlEvent, CancellationToken ct)
    {
        turn.SqlEventEmitted = true;
        turn.OperationalEvidence = true;
        turn.Gate.Open();
        await turn.EmitAsync(sqlEvent, ct);
    }

    private static IReadOnlyList<object?> Sanitise(IReadOnlyList<object?> row) =>
        row.Select(cell => cell is DBNull ? null : cell).ToArray();

    private static bool IsUnanswerable(string sql) =>
        sql.Length <= 40 && sql.Trim().Trim('.', '`', '"', '\'', ';').Equals(SqlPrompt.Unanswerable, StringComparison.OrdinalIgnoreCase);

    private static string StripFences(string text)
    {
        var lines = text.Trim().Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        if (lines.Count > 0 && lines[0].TrimStart().StartsWith("```", StringComparison.Ordinal))
        {
            lines.RemoveAt(0);
        }

        if (lines.Count > 0 && lines[^1].Trim() == "```")
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Join('\n', lines).Trim();
    }
}
