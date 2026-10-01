using System.Globalization;

namespace Quayside.Core.Documents;

public static class Chunker
{
    public static IReadOnlyList<Chunk> Split(Document doc, int targetTokens = 700, int overlapTokens = 100)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentOutOfRangeException.ThrowIfLessThan(targetTokens, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(overlapTokens);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(overlapTokens, targetTokens);

        var text = doc.Text ?? string.Empty;
        var maxChars = targetTokens * TokenEstimator.CharactersPerToken;
        var overlapChars = overlapTokens * TokenEstimator.CharactersPerToken;

        var units = BuildUnits(text, maxChars);
        if (units.Count == 0)
        {
            return [];
        }

        var chunks = new List<Chunk>();
        var first = 0;

        while (first < units.Count)
        {
            var next = first + 1;
            while (next < units.Count && units[next].TrimmedEnd - units[first].Start <= maxChars)
            {
                next++;
            }

            chunks.Add(Materialise(doc, text, chunks.Count, units[first].Start, units[next - 1].TrimmedEnd));

            if (next >= units.Count)
            {
                break;
            }

            first = ChooseNextStart(units, first, next, maxChars, overlapChars);
        }

        return chunks;
    }

    public static string ChunkId(string documentId, int ordinal) =>
        string.Create(CultureInfo.InvariantCulture, $"{documentId}#{ordinal:D4}");

    private static int ChooseNextStart(List<Unit> units, int first, int next, int maxChars, int overlapChars)
    {
        var candidate = next;
        while (candidate > first + 1
            && units[next - 1].TrimmedEnd - units[candidate - 1].Start <= overlapChars
            && units[next].TrimmedEnd - units[candidate - 1].Start <= maxChars)
        {
            candidate--;
        }

        return candidate;
    }

    private static Chunk Materialise(Document doc, string text, int ordinal, int start, int end)
    {
        var body = text.Substring(start, end - start);
        return new Chunk(
            ChunkId(doc.Id, ordinal),
            doc.Id,
            ordinal,
            body,
            TokenEstimator.Estimate(body));
    }

    private static List<Unit> BuildUnits(string text, int maxChars)
    {
        var units = new List<Unit>();
        foreach (var sentence in SentenceSplitter.Split(text))
        {
            var trimmedEnd = SentenceSplitter.TrimmedEnd(text, sentence);
            if (trimmedEnd <= sentence.Start)
            {
                continue;
            }

            if (trimmedEnd - sentence.Start <= maxChars)
            {
                units.Add(new Unit(sentence.Start, trimmedEnd));
                continue;
            }

            SplitOversized(text, sentence.Start, trimmedEnd, maxChars, units);
        }

        return units;
    }

    private static void SplitOversized(string text, int start, int end, int maxChars, List<Unit> units)
    {
        var position = start;
        while (position < end)
        {
            var cut = ChooseCut(text, position, end, maxChars);
            var pieceEnd = cut;
            while (pieceEnd > position && char.IsWhiteSpace(text[pieceEnd - 1]))
            {
                pieceEnd--;
            }

            if (pieceEnd > position)
            {
                units.Add(new Unit(position, pieceEnd));
            }

            position = cut;
            while (position < end && char.IsWhiteSpace(text[position]))
            {
                position++;
            }
        }
    }

    private static int ChooseCut(string text, int position, int end, int maxChars)
    {
        if (end - position <= maxChars)
        {
            return end;
        }

        var limit = position + maxChars;
        var floor = position + maxChars / 2;
        for (var i = limit; i > floor; i--)
        {
            if (char.IsWhiteSpace(text[i - 1]) && !char.IsWhiteSpace(text[i]))
            {
                return i;
            }
        }

        return Math.Min(GraphemeCut(text, position, limit), end);
    }

    private static int GraphemeCut(string text, int position, int limit)
    {
        var cursor = position;
        while (cursor < text.Length)
        {
            var width = StringInfo.GetNextTextElementLength(text.AsSpan(cursor));
            if (cursor + width > limit && cursor > position)
            {
                break;
            }

            cursor += width;
            if (cursor >= limit)
            {
                break;
            }
        }

        return cursor;
    }

    private readonly record struct Unit(int Start, int TrimmedEnd);
}
