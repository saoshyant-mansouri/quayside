namespace Quayside.Core.Grounding;

internal static class CodeMask
{
    public static string Apply(string text)
    {
        var chars = text.ToCharArray();
        MaskFencedBlocks(text, chars);
        MaskInlineCode(chars);
        return new string(chars);
    }

    private static void MaskFencedBlocks(string text, char[] chars)
    {
        var inFence = false;
        var fenceMarker = '`';
        var lineStart = 0;

        while (lineStart < text.Length)
        {
            var lineEnd = text.IndexOf('\n', lineStart);
            var next = lineEnd < 0 ? text.Length : lineEnd + 1;
            var contentEnd = lineEnd < 0 ? text.Length : lineEnd;
            var line = text.AsSpan(lineStart, contentEnd - lineStart).TrimStart();
            var isFence = line.StartsWith("```", StringComparison.Ordinal) || line.StartsWith("~~~", StringComparison.Ordinal);

            if (isFence && (!inFence || line[0] == fenceMarker))
            {
                inFence = !inFence;
                fenceMarker = line[0];
                Blank(chars, lineStart, contentEnd);
            }
            else if (inFence)
            {
                Blank(chars, lineStart, contentEnd);
            }

            lineStart = next;
        }
    }

    private static void MaskInlineCode(char[] chars)
    {
        var i = 0;
        while (i < chars.Length)
        {
            if (chars[i] != '`')
            {
                i++;
                continue;
            }

            var close = i + 1;
            while (close < chars.Length && chars[close] != '`' && chars[close] != '\n')
            {
                close++;
            }

            if (close >= chars.Length || chars[close] != '`' || close == i + 1)
            {
                i++;
                continue;
            }

            for (var j = i; j <= close; j++)
            {
                chars[j] = 'x';
            }

            i = close + 1;
        }
    }

    private static void Blank(char[] chars, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            chars[i] = ' ';
        }
    }
}
