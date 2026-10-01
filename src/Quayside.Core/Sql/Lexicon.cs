using System.Text;

namespace Quayside.Core.Sql;

internal static class Lexicon
{
    private static readonly HashSet<string> Stopwords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "did", "do", "does", "for", "from", "give", "has", "have",
        "how", "i", "in", "is", "it", "list", "many", "me", "much", "of", "on", "or", "show", "that", "the", "their",
        "there", "this", "to", "was", "were", "what", "when", "where", "which", "who", "with"
    };

    public static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();

        void Flush()
        {
            if (current.Length == 0) return;
            var token = Stem(current.ToString().ToLowerInvariant());
            current.Clear();
            if (token.Length > 1 && !Stopwords.Contains(token)) tokens.Add(token);
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (!char.IsLetterOrDigit(c))
            {
                Flush();
                continue;
            }
            var startsNewWord = current.Length > 0
                && char.IsUpper(c)
                && (char.IsLower(text[i - 1]) || (i + 1 < text.Length && char.IsLower(text[i + 1]) && char.IsUpper(text[i - 1])));
            if (startsNewWord) Flush();
            current.Append(c);
        }
        Flush();
        return tokens;
    }

    private static string Stem(string word)
    {
        if (word.Length > 4 && word.EndsWith("ies", StringComparison.Ordinal)) return word[..^3] + "y";
        if (word.Length > 4 && word.EndsWith("ses", StringComparison.Ordinal)) return word[..^2];
        if (word.Length > 3 && word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal)) return word[..^1];
        return word;
    }
}
