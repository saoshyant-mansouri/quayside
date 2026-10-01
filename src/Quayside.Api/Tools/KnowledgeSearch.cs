using System.Globalization;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Quayside.Api.Contract;
using Quayside.Api.Hosting;
using Quayside.Api.Orchestration;

namespace Quayside.Api.Tools;

public sealed class KnowledgeSearch(
    Hydrated<CorpusIndex> corpus,
    IEmbeddingGenerator<string, Embedding<float>> embeddings,
    IOptions<GroundingOptions> grounding,
    IOptions<RetrievalOptions> retrieval,
    IOptions<ChatLimits> limits)
{
    public async Task<ToolOutcome> SearchAsync(TurnContext turn, string query, CancellationToken ct)
    {
        var ready = await corpus.WaitAsync(TimeSpan.FromSeconds(limits.Value.WarmupWaitSeconds), ct);
        if (ready is null)
        {
            return new ToolOutcome(Prompts.IndexWarming, new Dictionary<string, object?> { ["warm"] = false });
        }

        var text = string.IsNullOrWhiteSpace(query) ? turn.Question : query;
        var embedding = string.Equals(text, turn.Question, StringComparison.Ordinal)
            ? turn.QuestionEmbedding
            : (await embeddings.GenerateAsync([text], cancellationToken: ct))[0].Vector;

        var result = ready.Index.Search(embedding, text, retrieval.Value.TopK);
        var topCosine = Math.Round(result.TopCosine, 3);
        var detail = new Dictionary<string, object?> { ["hits"] = result.Hits.Count, ["topCosine"] = topCosine };

        if (result.Hits.Count == 0 || result.TopCosine < grounding.Value.MinTopCosine)
        {
            detail["refused"] = true;
            return new ToolOutcome(Prompts.NoSources, detail);
        }

        var references = turn.Sources.Add(result.Hits);
        turn.Gate.Open();
        await turn.EmitAsync(new CitationsEvent(Payloads(turn)), ct);
        return new ToolOutcome(Format(references), detail);
    }

    private static IReadOnlyList<CitationPayload> Payloads(TurnContext turn) =>
        turn.Sources.Citations
            .Select(c => new CitationPayload(c.Marker, c.Title, c.Url, c.Source, c.PublishedLabel))
            .ToArray();

    private static string Format(IReadOnlyList<SourceReference> references)
    {
        var text = new StringBuilder("Sources. Cite a fact with the [n] of the source it came from.\n");
        foreach (var reference in references)
        {
            var document = reference.Hit.Document;
            text.Append(string.Create(CultureInfo.InvariantCulture, $"\n[{reference.Marker}] {document.Title} ({document.Source}"));
            text.Append(document.PublishedLabel is { } published ? $", {published})\n" : ")\n");
            text.AppendLine(reference.Hit.Chunk.Text.Trim());
        }

        return text.ToString();
    }
}
