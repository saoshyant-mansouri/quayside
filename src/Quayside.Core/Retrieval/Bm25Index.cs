namespace Quayside.Core.Retrieval;

public sealed class Bm25Index
{
    public const double K1 = 1.2;
    public const double B = 0.75;
    private const int MaxQueryTerms = 64;

    private readonly Dictionary<string, TermEntry> terms;
    private readonly Dictionary<string, TermEntry>.AlternateLookup<ReadOnlySpan<char>> lookup;
    private readonly int[] lengths;
    private readonly double averageLength;

    private Bm25Index(Dictionary<string, TermEntry> terms, int[] lengths, double averageLength)
    {
        this.terms = terms;
        lookup = terms.GetAlternateLookup<ReadOnlySpan<char>>();
        this.lengths = lengths;
        this.averageLength = averageLength;
    }

    public int DocumentCount => lengths.Length;

    public int TermCount => terms.Count;

    public static double Idf(int documentCount, int documentFrequency) =>
        Math.Log(1.0 + (documentCount - documentFrequency + 0.5) / (documentFrequency + 0.5));

    public static double TermScore(double idf, int termFrequency, int documentLength, double averageDocumentLength)
    {
        var normalisation = 1.0 - B + B * documentLength / averageDocumentLength;
        return idf * termFrequency * (K1 + 1.0) / (termFrequency + K1 * normalisation);
    }

    public static Bm25Index Build(IReadOnlyList<string> texts)
    {
        var building = new Dictionary<string, List<Posting>>(StringComparer.Ordinal);
        var lengths = new int[texts.Count];
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        long totalLength = 0;

        for (var document = 0; document < texts.Count; document++)
        {
            counts.Clear();
            var tokens = Tokenizer.Tokenize(texts[document]);
            lengths[document] = tokens.Count;
            totalLength += tokens.Count;

            foreach (var token in tokens)
            {
                counts[token] = counts.GetValueOrDefault(token) + 1;
            }

            foreach (var (token, frequency) in counts)
            {
                if (!building.TryGetValue(token, out var postings))
                {
                    postings = [];
                    building[token] = postings;
                }

                postings.Add(new Posting(document, frequency));
            }
        }

        var average = texts.Count == 0 || totalLength == 0 ? 1.0 : (double)totalLength / texts.Count;
        var entries = new Dictionary<string, TermEntry>(building.Count, StringComparer.Ordinal);
        var nextId = 0;
        foreach (var (token, postings) in building.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            entries[token] = new TermEntry(nextId++, Idf(texts.Count, postings.Count), [.. postings]);
        }

        return new Bm25Index(entries, lengths, average);
    }

    public int Score(ReadOnlySpan<char> query, Span<double> scores)
    {
        Span<char> buffer = stackalloc char[Tokenizer.MaxTokenLength];
        Span<int> seen = stackalloc int[MaxQueryTerms];
        var seenCount = 0;
        var position = 0;

        while (seenCount < MaxQueryTerms && Tokenizer.TryNextTerm(query, ref position, buffer, out var length))
        {
            if (!lookup.TryGetValue(buffer[..length], out var entry) || Contains(seen[..seenCount], entry.Id))
            {
                continue;
            }

            seen[seenCount++] = entry.Id;
            foreach (var posting in entry.Postings)
            {
                scores[posting.Document] += TermScore(entry.Idf, posting.Frequency, lengths[posting.Document], averageLength);
            }
        }

        return seenCount;
    }

    private static bool Contains(ReadOnlySpan<int> ids, int id) => ids.Contains(id);

    private readonly record struct Posting(int Document, int Frequency);

    private sealed record TermEntry(int Id, double Idf, Posting[] Postings);
}
