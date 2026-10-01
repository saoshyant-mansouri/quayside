namespace Quayside.Core.Documents;

public static class SentenceSplitter
{
    private static readonly HashSet<string> Abbreviations = new(StringComparer.Ordinal)
    {
        "mr", "mrs", "ms", "dr", "prof", "sr", "jr", "st", "vs", "inc", "ltd", "corp", "co",
        "e.g", "i.e", "u.s", "u.k", "approx", "fig", "vol", "dept"
    };

    public static IReadOnlyList<TextSpan> Split(string text)
    {
        var spans = new List<TextSpan>();
        var length = text.Length;
        var start = SkipWhitespace(text, 0);
        var i = start;

        while (i < length)
        {
            var c = text[i];

            if (c == '\n')
            {
                var end = SkipWhitespace(text, i);
                spans.Add(new TextSpan(start, end));
                start = i = end;
                continue;
            }

            if (IsTerminator(c))
            {
                var afterRun = ConsumeTerminatorRun(text, i, out var terminatorsEnd);
                if (IsBoundary(text, start, i, terminatorsEnd, afterRun))
                {
                    var end = SkipWhitespace(text, afterRun);
                    spans.Add(new TextSpan(start, end));
                    start = i = end;
                    continue;
                }

                i = afterRun;
                continue;
            }

            i++;
        }

        if (start < length)
        {
            spans.Add(new TextSpan(start, length));
        }

        return spans;
    }

    public static int TrimmedEnd(string text, TextSpan span)
    {
        var end = span.End;
        while (end > span.Start && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        return end;
    }

    private static bool IsTerminator(char c) =>
        c is '.' or '!' or '?' or '…' or '。' or '！' or '？';

    private static bool IsCloser(char c) =>
        c is ')' or ']' or '}' or '"' or '\'' or '”' or '’' or '»' or '*';

    private static int SkipWhitespace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index;
    }

    private static int ConsumeTerminatorRun(string text, int index, out int terminatorsEnd)
    {
        var j = index;
        while (j < text.Length && IsTerminator(text[j]))
        {
            j++;
        }

        terminatorsEnd = j;

        while (j < text.Length)
        {
            if (IsCloser(text[j]))
            {
                j++;
                continue;
            }

            if (TryMeasureMarker(text, j, out var markerLength))
            {
                j += markerLength;
                continue;
            }

            var gap = j;
            while (gap < text.Length && text[gap] is ' ' or '\t')
            {
                gap++;
            }

            if (gap > j && gap < text.Length && TryMeasureMarker(text, gap, out markerLength))
            {
                j = gap + markerLength;
                continue;
            }

            break;
        }

        return j;
    }

    private static bool TryMeasureMarker(string text, int index, out int length)
    {
        length = 0;
        if (text[index] != '[')
        {
            return false;
        }

        var j = index + 1;
        var digits = 0;
        while (j < text.Length && char.IsAsciiDigit(text[j]) && digits < 4)
        {
            j++;
            digits++;
        }

        if (digits == 0 || j >= text.Length || text[j] != ']')
        {
            return false;
        }

        length = j + 1 - index;
        return true;
    }

    private static bool IsBoundary(string text, int sentenceStart, int runStart, int terminatorsEnd, int afterRun)
    {
        if (afterRun >= text.Length)
        {
            return true;
        }

        var last = text[terminatorsEnd - 1];
        if (last is '。' or '！' or '？')
        {
            return true;
        }

        if (!char.IsWhiteSpace(text[afterRun]))
        {
            return false;
        }

        if (last != '.')
        {
            return true;
        }

        if (IsListNumber(text, sentenceStart, runStart))
        {
            return false;
        }

        if (terminatorsEnd - runStart == 1 && IsAbbreviation(text, sentenceStart, runStart))
        {
            return false;
        }

        return !NextWordStartsLowercase(text, afterRun);
    }

    private static bool IsListNumber(string text, int sentenceStart, int dotIndex)
    {
        if (dotIndex == sentenceStart)
        {
            return false;
        }

        for (var i = sentenceStart; i < dotIndex; i++)
        {
            if (!char.IsAsciiDigit(text[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAbbreviation(string text, int sentenceStart, int dotIndex)
    {
        var wordStart = dotIndex;
        while (wordStart > sentenceStart && (char.IsLetter(text[wordStart - 1]) || text[wordStart - 1] == '.'))
        {
            wordStart--;
        }

        var word = text.AsSpan(wordStart, dotIndex - wordStart);
        if (word.Length == 0)
        {
            return false;
        }

        if (word.Length == 1 && char.IsUpper(word[0]))
        {
            return true;
        }

        Span<char> lowered = stackalloc char[word.Length];
        word.ToLowerInvariant(lowered);
        return Abbreviations.GetAlternateLookup<ReadOnlySpan<char>>().Contains(lowered);
    }

    private static bool NextWordStartsLowercase(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            if (text[index] == '\n')
            {
                return false;
            }

            index++;
        }

        return index < text.Length && char.IsLower(text[index]);
    }
}
