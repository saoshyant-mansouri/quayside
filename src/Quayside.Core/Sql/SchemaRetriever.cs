using System.Numerics.Tensors;

namespace Quayside.Core.Sql;

public sealed class SchemaRetriever
{
    public const int MaxTables = 14;

    private const double ReciprocalRankConstant = 60;
    private const float DenseFloor = 0.1f;

    private readonly TableCard[] cards;
    private readonly ReadOnlyMemory<float>[] embeddings;
    private readonly int[][] adjacency;
    private readonly Bm25Index lexical;

    public SchemaRetriever(IReadOnlyList<EmbeddedTableCard> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        var distinct = cards
            .GroupBy(c => c.Card.Table, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(c => c.Card.Table, StringComparer.Ordinal)
            .ToArray();

        this.cards = distinct.Select(c => c.Card).ToArray();
        embeddings = distinct.Select(c => c.Embedding).ToArray();

        var indexByName = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < this.cards.Length; i++) indexByName[this.cards[i].Table] = i;

        var links = this.cards.Select(_ => new SortedSet<int>()).ToArray();
        for (var i = 0; i < this.cards.Length; i++)
        {
            foreach (var name in this.cards[i].Neighbours)
            {
                if (!indexByName.TryGetValue(name, out var j) || j == i) continue;
                links[i].Add(j);
                links[j].Add(i);
            }
        }
        adjacency = links.Select(s => s.ToArray()).ToArray();

        lexical = new Bm25Index(this.cards.Select(Document).ToArray());
    }

    public IReadOnlyList<TableCard> Select(ReadOnlyMemory<float> queryEmbedding, string queryText, int k = 8, int hops = 1)
    {
        if (k <= 0 || cards.Length == 0) return [];

        var fused = Fuse(queryEmbedding, queryText ?? string.Empty);
        var seeds = fused.Take(Math.Min(k, MaxTables)).Select(p => p.Index).ToList();
        if (seeds.Count == 0) return [];

        var strength = new Dictionary<int, double>();
        foreach (var (index, score) in fused) strength[index] = score;

        var selected = new List<int>(seeds);
        var chosen = new HashSet<int>(seeds);
        var frontier = seeds;

        for (var hop = 0; hop < hops && selected.Count < MaxTables && frontier.Count > 0; hop++)
        {
            var pull = new Dictionary<int, double>();
            foreach (var from in frontier)
            {
                var weight = strength.GetValueOrDefault(from);
                foreach (var to in adjacency[from])
                {
                    if (chosen.Contains(to)) continue;
                    pull[to] = pull.GetValueOrDefault(to) + weight;
                }
            }

            var additions = pull
                .OrderByDescending(p => p.Value)
                .ThenByDescending(p => strength.GetValueOrDefault(p.Key))
                .ThenBy(p => cards[p.Key].Table, StringComparer.Ordinal)
                .Take(MaxTables - selected.Count)
                .ToList();

            foreach (var (index, weight) in additions)
            {
                strength[index] = weight;
                chosen.Add(index);
                selected.Add(index);
            }
            frontier = additions.Select(p => p.Key).ToList();
        }

        return selected.Select(i => cards[i]).ToArray();
    }

    private List<(int Index, double Score)> Fuse(ReadOnlyMemory<float> queryEmbedding, string queryText)
    {
        var scores = new Dictionary<int, double>();

        var denseRanking = DenseRanking(queryEmbedding);
        for (var rank = 0; rank < denseRanking.Count; rank++)
            scores[denseRanking[rank]] = 1.0 / (ReciprocalRankConstant + rank + 1);

        var lexicalRanking = LexicalRanking(queryText);
        for (var rank = 0; rank < lexicalRanking.Count; rank++)
        {
            var index = lexicalRanking[rank];
            scores[index] = scores.GetValueOrDefault(index) + 1.0 / (ReciprocalRankConstant + rank + 1);
        }

        return scores
            .OrderByDescending(p => p.Value)
            .ThenBy(p => cards[p.Key].Table, StringComparer.Ordinal)
            .Select(p => (p.Key, p.Value))
            .ToList();
    }

    private List<int> DenseRanking(ReadOnlyMemory<float> queryEmbedding)
    {
        if (queryEmbedding.IsEmpty) return [];

        var similarities = new List<(int Index, float Similarity)>();
        for (var i = 0; i < embeddings.Length; i++)
        {
            if (embeddings[i].Length != queryEmbedding.Length) continue;
            var similarity = TensorPrimitives.CosineSimilarity(queryEmbedding.Span, embeddings[i].Span);
            if (float.IsNaN(similarity) || similarity < DenseFloor) continue;
            similarities.Add((i, similarity));
        }

        return similarities
            .OrderByDescending(s => s.Similarity)
            .ThenBy(s => cards[s.Index].Table, StringComparer.Ordinal)
            .Select(s => s.Index)
            .ToList();
    }

    private List<int> LexicalRanking(string queryText)
    {
        var tokens = Lexicon.Tokenize(queryText);
        if (tokens.Count == 0) return [];

        var scores = lexical.Score(tokens);
        return Enumerable.Range(0, scores.Length)
            .Where(i => scores[i] > 0)
            .OrderByDescending(i => scores[i])
            .ThenBy(i => cards[i].Table, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<string> Document(TableCard card)
    {
        var name = Lexicon.Tokenize(card.Table);
        var description = Lexicon.Tokenize(card.Description);
        var tokens = new List<string>();
        for (var i = 0; i < 3; i++) tokens.AddRange(name);
        tokens.AddRange(description);
        tokens.AddRange(Lexicon.Tokenize(card.Card));
        return tokens;
    }
}
