namespace Quayside.Core.Sql;

internal sealed class Bm25Index
{
    private const double K1 = 1.2;
    private const double B = 0.75;

    private readonly Dictionary<string, List<(int Doc, int Frequency)>> postings = new(StringComparer.Ordinal);
    private readonly int[] lengths;
    private readonly double averageLength;

    public Bm25Index(IReadOnlyList<IReadOnlyList<string>> documents)
    {
        lengths = new int[documents.Count];
        for (var doc = 0; doc < documents.Count; doc++)
        {
            lengths[doc] = documents[doc].Count;
            foreach (var group in documents[doc].GroupBy(t => t, StringComparer.Ordinal))
            {
                if (!postings.TryGetValue(group.Key, out var list)) postings[group.Key] = list = [];
                list.Add((doc, group.Count()));
            }
        }
        averageLength = documents.Count == 0 ? 0 : lengths.Average();
    }

    public double[] Score(IEnumerable<string> queryTokens)
    {
        var scores = new double[lengths.Length];
        foreach (var term in queryTokens.Distinct(StringComparer.Ordinal))
        {
            if (!postings.TryGetValue(term, out var list)) continue;
            var idf = Math.Log(1 + (lengths.Length - list.Count + 0.5) / (list.Count + 0.5));
            foreach (var (doc, frequency) in list)
            {
                var norm = frequency + K1 * (1 - B + B * lengths[doc] / averageLength);
                scores[doc] += idf * frequency * (K1 + 1) / norm;
            }
        }
        return scores;
    }
}
