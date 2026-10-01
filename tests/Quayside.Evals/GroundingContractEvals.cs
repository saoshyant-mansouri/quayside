using Quayside.Core.Grounding;

namespace Quayside.Evals;

public sealed class GroundingContractEvals
{
    private static readonly IReadOnlyList<Citation> AvailableCitations =
    [
        new Citation(1, "web-001#0000", "Fleet Overview", "https://www.msc.com/fleet", "website", null),
        new Citation(2, "web-002#0000", "Sustainability", "https://www.msc.com/sustainability", "website", null),
        new Citation(3, "li-001#0000", "Decarbonization Update", "https://www.linkedin.com/feed/update/urn:li:activity:1", "linkedin", "2w")
    ];

    [Fact]
    public void Valid_grounded_answer_with_citations_passes_completely()
    {
        var answer = "MSC operates a global container fleet of over 1,000 vessels [1]. The carrier offers biofuel options for decarbonization [2].";
        var report = CitationEnforcer.Check(answer, AvailableCitations);

        Assert.Equal(2, report.Cited);
        Assert.Equal(0, report.Uncited);
        Assert.Empty(report.UnsupportedSentences);
    }

    [Fact]
    public void Ungrounded_factual_claims_without_citations_are_flagged()
    {
        var answer = "MSC operates a global container fleet [1]. MSC is building secret lunar space ports. Biofuel saves emissions [2].";
        var report = CitationEnforcer.Check(answer, AvailableCitations);

        Assert.Equal(2, report.Cited);
        Assert.Equal(1, report.Uncited);
        Assert.Equal(["MSC is building secret lunar space ports."], report.UnsupportedSentences);
    }

    [Fact]
    public void Fabricated_citation_marker_out_of_range_is_rejected()
    {
        var answer = "MSC has headquarters located in Geneva [99].";
        var report = CitationEnforcer.Check(answer, AvailableCitations);

        Assert.Equal(0, report.Cited);
        Assert.Equal(1, report.Uncited);
        Assert.Equal(["MSC has headquarters located in Geneva [99]."], report.UnsupportedSentences);
    }

    [Theory]
    [InlineData("I could not find any information about that in the MSC knowledge base.")]
    [InlineData("Unfortunately, the retrieved documents do not contain details regarding flight bookings.")]
    [InlineData("There is no information about internal database passwords in the available sources.")]
    [InlineData("I cannot find any relevant sources for that request.")]
    public void Clean_refusal_responses_pass_without_requiring_citations(string refusalAnswer)
    {
        var report = CitationEnforcer.Check(refusalAnswer, AvailableCitations);

        Assert.Equal(0, report.Cited);
        Assert.Equal(0, report.Uncited);
        Assert.Empty(report.UnsupportedSentences);
    }

    [Fact]
    public void Refusal_with_hallucinated_claim_attached_flags_the_claim()
    {
        var answer = "I could not find the exact schedule, but MSC is acquiring 500 airlines next week.";
        var report = CitationEnforcer.Check(answer, AvailableCitations);

        Assert.Equal(0, report.Cited);
        Assert.Equal(1, report.Uncited);
        Assert.Equal(["I could not find the exact schedule, but MSC is acquiring 500 airlines next week."], report.UnsupportedSentences);
    }

    [Fact]
    public void Conversational_pleasantries_and_offers_do_not_count_as_uncited_claims()
    {
        var answer = "Hello! MSC operates across 155 countries [1]. Let me know if you would like more detail. Happy to help!";
        var report = CitationEnforcer.Check(answer, AvailableCitations);

        Assert.Equal(1, report.Cited);
        Assert.Equal(0, report.Uncited);
        Assert.Empty(report.UnsupportedSentences);
    }

    [Fact]
    public void Markdown_code_blocks_and_inline_code_are_not_flagged_as_unsupported()
    {
        var answer = "Here is an example query:\n```sql\nSELECT * FROM ops.Containers WHERE ContainerNumber = 'MSCU1234567';\n```\nMSC provides digital tracking services [1].";
        var report = CitationEnforcer.Check(answer, AvailableCitations);

        Assert.Equal(1, report.Cited);
        Assert.Equal(0, report.Uncited);
        Assert.Empty(report.UnsupportedSentences);
    }

    [Fact]
    public void Bulleted_lists_are_evaluated_per_item()
    {
        var answer = "Summary of capabilities:\n- Global routes connecting 520 ports [1]\n- Biofuel solution achieving Scope 3 reduction [2]\n- Free submarine taxi rides for every passenger\n- Real-time container telemetry via iReefer [1]";
        var report = CitationEnforcer.Check(answer, AvailableCitations);

        Assert.Equal(3, report.Cited);
        Assert.Equal(1, report.Uncited);
        Assert.Equal(["- Free submarine taxi rides for every passenger"], report.UnsupportedSentences);
    }

    [Fact]
    public void Empty_or_whitespace_answers_produce_zero_counts()
    {
        var emptyReport = CitationEnforcer.Check(string.Empty, AvailableCitations);
        var whitespaceReport = CitationEnforcer.Check("   \n\t  ", AvailableCitations);

        Assert.Equal(0, emptyReport.Cited);
        Assert.Equal(0, emptyReport.Uncited);
        Assert.Equal(0, whitespaceReport.Cited);
        Assert.Equal(0, whitespaceReport.Uncited);
    }

    [Fact]
    public void Answers_when_no_citations_available_flag_factual_assertions()
    {
        var answer = "MSC operates 1,000 vessels [1].";
        var report = CitationEnforcer.Check(answer, []);

        Assert.Equal(0, report.Cited);
        Assert.Equal(1, report.Uncited);
        Assert.Equal(["MSC operates 1,000 vessels [1]."], report.UnsupportedSentences);
    }
}
