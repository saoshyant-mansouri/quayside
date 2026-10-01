namespace Quayside.Core.Retrieval;

public static class Tokenizer
{
    public const int MaxTokenLength = 64;

    private static readonly HashSet<string> Stopwords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "are", "as", "at", "be", "but", "by", "for", "from", "has", "have", "he", "her",
        "his", "i", "in", "is", "it", "its", "me", "my", "of", "on", "or", "our", "she", "so", "t", "s",
        "that", "the", "their", "them", "they", "this", "to", "was", "we", "were", "what", "when", "where",
        "which", "who", "will", "with", "you", "your", "do", "does", "did", "how", "can", "about", "into",
        "than", "then", "there", "these", "those", "not", "no", "if", "also", "been", "being", "had", "us"
    };

    public static bool IsStopword(ReadOnlySpan<char> loweredToken) =>
        Stopwords.GetAlternateLookup<ReadOnlySpan<char>>().Contains(loweredToken);

    public static bool TryNextTerm(ReadOnlySpan<char> text, ref int position, Span<char> buffer, out int length)
    {
        while (position < text.Length)
        {
            while (position < text.Length && !char.IsLetterOrDigit(text[position]))
            {
                position++;
            }

            var start = position;
            while (position < text.Length && char.IsLetterOrDigit(text[position]))
            {
                position++;
            }

            var run = position - start;
            if (run == 0 || run > MaxTokenLength || run > buffer.Length)
            {
                continue;
            }

            for (var i = 0; i < run; i++)
            {
                buffer[i] = char.ToLowerInvariant(text[start + i]);
            }

            if (IsStopword(buffer[..run]))
            {
                continue;
            }

            length = run;
            return true;
        }

        length = 0;
        return false;
    }

    public static List<string> Tokenize(string text)
    {
        var terms = new List<string>();
        Span<char> buffer = stackalloc char[MaxTokenLength];
        var position = 0;
        while (TryNextTerm(text, ref position, buffer, out var length))
        {
            terms.Add(new string(buffer[..length]));
        }

        return terms;
    }
}
