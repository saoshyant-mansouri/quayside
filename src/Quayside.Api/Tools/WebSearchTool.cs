using System.Globalization;
using System.Text;
using Quayside.Api.Orchestration;
using Quayside.Core.Web;

namespace Quayside.Api.Tools;

public sealed class WebSearchTool(WebFallback web)
{
    public async Task<ToolOutcome> SearchAsync(TurnContext turn, string query, CancellationToken ct)
    {
        if (!turn.CorpusSearched)
        {
            return new ToolOutcome(Prompts.SearchCorpusFirst, new Dictionary<string, object?> { ["refused"] = true });
        }

        var text = string.IsNullOrWhiteSpace(query) ? turn.Question : query;
        var raw = await web.Search.SearchAsync(text, web.MaxResults, ct);
        var results = WebResultSanitizer.Clean(raw, web.MaxResults);
        var detail = new Dictionary<string, object?> { ["results"] = results.Count };

        if (results.Count == 0)
        {
            return new ToolOutcome(Prompts.NoWebResults, detail);
        }

        var capturedAt = DateTimeOffset.UtcNow;
        var references = turn.Sources.Add(results.Select(result => WebSources.ToHit(result, capturedAt)).ToArray());
        turn.WebFallbackUsed = true;
        turn.Gate.Open();
        await turn.EmitAsync(turn.CitationsSnapshot(), ct);
        return new ToolOutcome(Format(references), detail);
    }

    private static string Format(IReadOnlyList<SourceReference> references)
    {
        var text = new StringBuilder(Prompts.WebSourcesHeader);
        text.AppendLine();
        foreach (var reference in references)
        {
            var document = reference.Hit.Document;
            var host = new Uri(document.Url).Host;
            text.Append(string.Create(CultureInfo.InvariantCulture, $"\n[{reference.Marker}] {document.Title} (web, {host}"));
            text.Append(document.PublishedLabel is { } published ? $", {published})\n" : ")\n");
            text.AppendLine(reference.Hit.Chunk.Text);
        }

        text.AppendLine();
        text.Append(Prompts.WebSourcesFooter);
        return text.ToString();
    }
}
