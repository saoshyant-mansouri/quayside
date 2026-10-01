using Quayside.Core.Documents;

namespace Quayside.Core.Grounding;

public static class CitationEnforcer
{
    public static GroundingReport Check(string answer, IReadOnlyList<Citation> citations)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return new GroundingReport(0, 0, []);
        }

        var known = new HashSet<int>(citations.Select(citation => citation.Marker));
        var masked = CodeMask.Apply(answer);

        var cited = 0;
        var unsupported = new List<string>();

        foreach (var span in SentenceSplitter.Split(masked))
        {
            var maskedSentence = masked.Substring(span.Start, span.Length);
            if (!ClaimClassifier.AssertsFact(maskedSentence))
            {
                continue;
            }

            if (CitationMarkers.In(maskedSentence).Any(known.Contains))
            {
                cited++;
            }
            else
            {
                unsupported.Add(answer.Substring(span.Start, SentenceSplitter.TrimmedEnd(answer, span) - span.Start));
            }
        }

        return new GroundingReport(cited, unsupported.Count, unsupported);
    }
}
