using Quayside.Core.Retrieval;

namespace Quayside.UnitTests.Retrieval;

public sealed class FusionTests
{
    [Fact]
    public void Rrf_constant_is_sixty()
    {
        Assert.Equal(60, ReciprocalRankFusion.K);
        Assert.Equal(1.0 / 61.0, ReciprocalRankFusion.Contribution(1), 15);
    }

    [Fact]
    public void Rrf_scores_match_hand_computation()
    {
        var fused = new double[5];

        ReciprocalRankFusion.Accumulate([0, 1, 2], fused);
        ReciprocalRankFusion.Accumulate([3, 0, 4], fused);

        Assert.Equal(1.0 / 61 + 1.0 / 62, fused[0], 15);
        Assert.Equal(1.0 / 62, fused[1], 15);
        Assert.Equal(1.0 / 63, fused[2], 15);
        Assert.Equal(1.0 / 61, fused[3], 15);
        Assert.Equal(1.0 / 63, fused[4], 15);
        Assert.Equal(0.032522, fused[0], 6);
    }

    [Fact]
    public void Document_ranked_well_by_both_signals_beats_one_ranked_first_by_one()
    {
        var fused = new double[4];

        ReciprocalRankFusion.Accumulate([2, 0, 1, 3], fused);
        ReciprocalRankFusion.Accumulate([3, 0, 1, 2], fused);

        var order = Enumerable.Range(0, 4).OrderByDescending(i => fused[i]).ToArray();
        Assert.Equal(0, order[0]);
        Assert.True(fused[0] > fused[2]);
        Assert.True(fused[0] > fused[3]);
        Assert.Equal(1.0 / 62 + 1.0 / 62, fused[0], 15);
        Assert.Equal(1.0 / 61 + 1.0 / 64, fused[2], 15);
    }

    [Fact]
    public void Top_k_selects_descending_with_lowest_index_winning_ties()
    {
        double[] scores = [0.1, 0.9, 0.5, 0.9, 0.0, 0.7];
        var top = new int[3];

        var count = TopK.Select(scores, double.NegativeInfinity, top);

        Assert.Equal(3, count);
        Assert.Equal([1, 3, 5], top);
    }

    [Fact]
    public void Top_k_honours_the_exclusive_floor()
    {
        double[] scores = [0.0, 2.0, 0.0, 1.0];
        var top = new int[10];

        var count = TopK.Select(scores, 0.0, top);

        Assert.Equal(2, count);
        Assert.Equal([1, 3], top[..count]);
    }

    [Fact]
    public void Top_k_matches_a_full_sort_on_random_data()
    {
        var random = new Random(7);
        var scores = Enumerable.Range(0, 500).Select(_ => Math.Round(random.NextDouble(), 2)).ToArray();
        var top = new int[25];

        TopK.Select(scores, double.NegativeInfinity, top);

        var expected = Enumerable.Range(0, scores.Length).OrderByDescending(i => scores[i]).ThenBy(i => i).Take(25);
        Assert.Equal(expected, top);
    }

    [Fact]
    public void Mmr_drops_a_near_duplicate_in_favour_of_a_different_candidate()
    {
        float[] vectors = [1f, 0f, 0.99995f, 0.01f, 0f, 1f];
        var selected = new int[2];

        var count = Mmr.Select([0, 1, 2], [1.0, 0.9, 0.6], vectors, 2, 0.7, selected);

        Assert.Equal(2, count);
        Assert.Equal([0, 2], selected);
    }

    [Fact]
    public void Mmr_scores_match_hand_computation()
    {
        float[] vectors = [1f, 0f, 0.8f, 0.6f, 0f, 1f];
        var selected = new int[3];

        Mmr.Select([0, 1, 2], [1.0, 0.9, 0.8], vectors, 2, 0.7, selected);

        Assert.Equal([0, 2, 1], selected);
    }

    [Fact]
    public void Mmr_with_lambda_one_is_pure_relevance_order()
    {
        float[] vectors = [1f, 0f, 1f, 0f, 1f, 0f];
        var selected = new int[3];

        Mmr.Select([2, 0, 1], [1.0, 0.8, 0.5], vectors, 2, 1.0, selected);

        Assert.Equal([2, 0, 1], selected);
    }

    [Fact]
    public void Mmr_never_returns_more_than_requested_or_available()
    {
        float[] vectors = [1f, 0f, 0f, 1f];

        Assert.Equal(2, Mmr.Select([0, 1], [1.0, 0.5], vectors, 2, 0.7, new int[5]));
        Assert.Equal(1, Mmr.Select([0, 1], [1.0, 0.5], vectors, 2, 0.7, new int[1]));
        Assert.Equal(0, Mmr.Select([], [], vectors, 2, 0.7, new int[3]));
    }
}
