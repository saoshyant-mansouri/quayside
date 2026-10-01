using Quayside.Core.Documents;
using Quayside.Core.Retrieval;

namespace Quayside.UnitTests.Retrieval;

public sealed class RefusalSignalTests
{
    private static HybridIndex Index()
    {
        var documents = new[]
        {
            Fixtures.Document("reefer", "Reefer containers keep perishable cargo cold.", "Reefer cargo"),
            Fixtures.Document("vessel", "Vessel schedules list every port call.", "Vessel schedules"),
        };

        var chunks = new[]
        {
            Fixtures.Embedded("reefer", 0, "Reefer containers keep perishable cargo cold.", 1f, 0f, 0f),
            Fixtures.Embedded("vessel", 0, "Vessel schedules list every port call.", 0f, 1f, 0f),
        };

        return HybridIndex.Build(documents, chunks);
    }

    [Fact]
    public void Top_cosine_is_the_best_cosine_among_the_hits()
    {
        var result = Index().Search(new float[] { 1f, 0f, 0f }, "reefer", 5);

        Assert.NotEmpty(result.Hits);
        Assert.Equal(result.Hits.Max(h => h.Cosine), result.TopCosine, 10);
        Assert.InRange(result.TopCosine, -1.0, 1.0);
        Assert.Equal(1.0, result.TopCosine, 6);
    }

    [Fact]
    public void An_unrelated_query_scores_a_lower_top_cosine_than_a_matching_one()
    {
        var index = Index();

        var near = index.Search(new float[] { 1f, 0f, 0f }, "reefer", 5);
        var far = index.Search(new float[] { 0f, 0f, 1f }, "zzzz", 5);

        Assert.True(far.TopCosine < near.TopCosine);
    }

    [Fact]
    public void A_zero_query_vector_reports_no_cosine_evidence()
    {
        var result = Index().Search(new float[] { 0f, 0f, 0f }, "reefer", 5);

        Assert.Equal(0.0, result.TopCosine);
        Assert.All(result.Hits, h => Assert.Equal(0.0, h.Cosine));
    }
}
