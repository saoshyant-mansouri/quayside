using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Quayside.Core.Sql;

namespace Quayside.Api.Offline;

public sealed partial class ScriptedChatClient : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var prompt = string.Join('\n', messages.Select(m => m.Text));
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, WriteSql(prompt)))
        {
            Usage = new UsageDetails { InputTokenCount = prompt.Length / 4, OutputTokenCount = 60 },
        });
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var conversation = messages.ToList();
        var last = conversation[^1];
        var toolResult = last.Contents.OfType<FunctionResultContent>().FirstOrDefault();
        FunctionCallContent? call = null;
        var text = toolResult is null ? PlanTool(conversation, options, out call) : Summarise(toolResult.Result?.ToString() ?? string.Empty);

        if (call is not null)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, [call]);
            yield return Usage(40, 12);
            yield break;
        }

        foreach (var piece in Pieces(text))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(8, cancellationToken);
            yield return new ChatResponseUpdate(ChatRole.Assistant, piece);
        }

        yield return Usage(120, 30);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }

    private static ChatResponseUpdate Usage(int prompt, int completion) =>
        new() { Contents = [new UsageContent(new UsageDetails { InputTokenCount = prompt, OutputTokenCount = completion })] };

    private static string PlanTool(List<ChatMessage> conversation, ChatOptions? options, out FunctionCallContent? call)
    {
        var question = conversation.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? string.Empty;
        call = null;

        string name;
        Dictionary<string, object?> arguments = [];
        var container = ContainerPattern().Match(question);
        if (container.Success)
        {
            name = "track_container";
            arguments["containerNumber"] = container.Value;
        }
        else if (Regex.IsMatch(question, "which|most|top|how many|average|total", RegexOptions.IgnoreCase))
        {
            name = "query_database";
            arguments["question"] = question;
        }
        else if (Regex.IsMatch(question, "schedule|sailing", RegexOptions.IgnoreCase))
        {
            name = "find_schedules";
            arguments["origin"] = "Rotterdam";
            arguments["destination"] = "Singapore";
        }
        else
        {
            name = "search_knowledge";
            arguments["query"] = question;
        }

        var tool = options?.Tools?.FirstOrDefault(t => t.Name.EndsWith(name, StringComparison.Ordinal));
        if (tool is null)
        {
            return "I have no tools available.";
        }

        call = new FunctionCallContent(Guid.NewGuid().ToString("N"), tool.Name, arguments);
        return string.Empty;
    }

    private static string Summarise(string toolResult)
    {
        if (toolResult.StartsWith("NO_SOURCES", StringComparison.Ordinal))
        {
            return "I could not find anything in the MSC material I have about that, so I will not guess.";
        }

        if (toolResult.StartsWith("Sources.", StringComparison.Ordinal))
        {
            var source = SourcePattern().Match(toolResult);
            var sentence = SentencePattern().Match(source.Groups["text"].Value.Trim());
            return $"According to the retrieved material, {Lower(sentence.Value.Trim())} [{source.Groups["marker"].Value}]";
        }

        if (toolResult.StartsWith("REJECTED", StringComparison.Ordinal))
        {
            return "I could not run that query: " + toolResult["REJECTED: ".Length..].Split("Nothing was executed", StringSplitOptions.None)[0].Trim();
        }

        return "Here is what the demo database returned. It is synthetic demo data.\n\n" + toolResult;
    }

    private static string WriteSql(string prompt)
    {
        var question = prompt[(prompt.LastIndexOf("Question:", StringComparison.Ordinal) + "Question:".Length)..].Trim();
        if (Regex.IsMatch(question, "delete|drop|update|insert", RegexOptions.IgnoreCase))
        {
            return "DELETE FROM ops.Bookings";
        }

        if (Regex.IsMatch(question, "weather|forecast|stock price", RegexOptions.IgnoreCase))
        {
            return SqlPrompt.Unanswerable;
        }

        var table = TablePattern().Match(prompt);
        if (!table.Success)
        {
            return SqlPrompt.Unanswerable;
        }

        var columns = ColumnPattern().Matches(table.Groups["body"].Value)
            .Select(match => $"t.{match.Groups["name"].Value}")
            .Take(4);
        return $"SELECT TOP (5) {string.Join(", ", columns)} FROM ops.{table.Groups["name"].Value} t";
    }

    private static IEnumerable<string> Pieces(string text) => WordPattern().Matches(text).Select(match => match.Value);

    private static string Lower(string text) => text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text[1..];

    [GeneratedRegex("[A-Za-z]{4}[0-9]{7}")]
    private static partial Regex ContainerPattern();

    [GeneratedRegex(@"\[(?<marker>\d+)\][^\n]*\n(?<text>[^\n]+)")]
    private static partial Regex SourcePattern();

    [GeneratedRegex(@"^.{20,300}?[.!?](?=\s|$)|^.{1,300}", RegexOptions.Singleline)]
    private static partial Regex SentencePattern();

    [GeneratedRegex(@"\s*\S+\s?")]
    private static partial Regex WordPattern();

    [GeneratedRegex(@"CREATE TABLE ops\.(?<name>\w+) \(\n(?<body>.*?)\n\);", RegexOptions.Singleline)]
    private static partial Regex TablePattern();

    [GeneratedRegex(@"^\s+(?<name>\w+) \w+", RegexOptions.Multiline)]
    private static partial Regex ColumnPattern();
}
