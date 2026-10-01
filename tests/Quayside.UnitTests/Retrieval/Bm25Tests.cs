using Quayside.Core.Retrieval;

namespace Quayside.UnitTests.Retrieval;

public sealed class Bm25Tests
{
    private static double[] Score(Bm25Index index, string query)
    {
        var scores = new double[index.DocumentCount];
        index.Score(query, scores);
        return scores;
    }

    [Fact]
    public void Tokenizer_lowercases_splits_on_non_alphanumerics_and_drops_stopwords()
    {
        var terms = Tokenizer.Tokenize("The MSC-Gulçin is at Antwerp, and Gioia_Tauro! 2026 \U0001F6A2 #msccargo");

        Assert.Equal(["msc", "gulçin", "antwerp", "gioia", "tauro", "2026", "msccargo"], terms);
    }

    [Fact]
    public void Term_in_a_single_document_ranks_that_document_first()
    {
        var index = Bm25Index.Build(
        [
            "container ships carry general cargo across the world",
            "reefer plugs keep fruit cold during long voyages",
            "terminal gates process trucks and rail wagons",
            "the port handles container ships every day"
        ]);

        var scores = Score(index, "reefer");

        Assert.Equal(1, Array.IndexOf(scores, scores.Max()));
        Assert.Equal(1, scores.Count(s => s > 0));
    }

    [Fact]
    public void Matches_a_hand_computed_score()
    {
        var index = Bm25Index.Build(["alpha beta", "beta gamma gamma"]);

        var scores = Score(index, "alpha");

        var expected = Math.Log(2.0) * 2.2 / (1.0 + 1.2 * 0.85);
        Assert.Equal(expected, scores[0], 12);
        Assert.Equal(0.0, scores[1]);
    }

    [Fact]
    public void Idf_follows_the_non_negative_lucene_form()
    {
        Assert.Equal(Math.Log(1.0 + 1.5 / 1.5), Bm25Index.Idf(2, 1), 12);
        Assert.Equal(Math.Log(1.0 + 0.5 / 2.5), Bm25Index.Idf(2, 2), 12);
        Assert.True(Bm25Index.Idf(1000, 1000) > 0);
    }

    [Fact]
    public void Stopwords_do_not_dominate()
    {
        var index = Bm25Index.Build(
        [
            "the the the the of of of and and and to to to is is is",
            "the vessel Aurora sails from Genoa",
            "unrelated words about customs paperwork"
        ]);

        var scores = Score(index, "the of and to is Aurora");

        Assert.Equal(1, Array.IndexOf(scores, scores.Max()));
        Assert.Equal(0.0, scores[0]);
        Assert.Equal(0, Score(index, "the of and to is").Count(s => s > 0));
    }

    [Fact]
    public void Repeated_query_terms_are_counted_once()
    {
        var index = Bm25Index.Build(["alpha beta", "beta gamma"]);

        Assert.Equal(Score(index, "alpha"), Score(index, "alpha alpha ALPHA"));
    }

    [Fact]
    public void Rare_terms_outweigh_common_terms()
    {
        var index = Bm25Index.Build(
        [
            "cargo cargo cargo common",
            "cargo common sesame",
            "cargo common",
            "cargo common",
            "cargo common"
        ]);

        var scores = Score(index, "cargo sesame");

        Assert.Equal(1, Array.IndexOf(scores, scores.Max()));
    }

    [Fact]
    public void Shorter_documents_win_on_equal_term_frequency()
    {
        var index = Bm25Index.Build(["alpha", "alpha beta gamma delta epsilon zeta"]);

        var scores = Score(index, "alpha");

        Assert.True(scores[0] > scores[1]);
    }

    [Fact]
    public void Unknown_terms_and_empty_queries_score_nothing()
    {
        var index = Bm25Index.Build(["alpha beta"]);

        Assert.All(Score(index, "zzzz"), s => Assert.Equal(0.0, s));
        Assert.All(Score(index, string.Empty), s => Assert.Equal(0.0, s));
        Assert.All(Score(index, "   !!! "), s => Assert.Equal(0.0, s));
    }

    [Fact]
    public void Empty_index_is_safe()
    {
        var index = Bm25Index.Build([]);

        Assert.Equal(0, index.DocumentCount);
        Assert.Equal(0, index.Score("anything", Span<double>.Empty));
    }
}
