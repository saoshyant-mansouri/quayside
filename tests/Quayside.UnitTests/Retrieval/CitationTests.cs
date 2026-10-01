using Quayside.Api.Orchestration;
using Quayside.Core.Documents;
using Quayside.Core.Grounding;
using Quayside.Core.Retrieval;

namespace Quayside.UnitTests.Retrieval;

public sealed class CitationTests
{
    private static readonly IReadOnlyList<Citation> TwoCitations =
    [
        new Citation(1, "d1#0000", "Fleet", "msc.test/fleet", "website", null),
        new Citation(2, "d2#0000", "Post", "linkedin.test/p", "linkedin", "1w")
    ];

    private static ScoredChunk Hit(string documentId, int ordinal, SourceKind source = SourceKind.Website, string? label = null) =>
        new(
            new Chunk($"{documentId}#{ordinal:D4}", documentId, ordinal, "text", 1),
            new Document(documentId, source, $"msc.test/{documentId}", $"Title {documentId}", "text", label, DateTimeOffset.UnixEpoch, "h"),
            1.0);

    [Fact]
    public void Builder_assigns_markers_in_hit_order()
    {
        var citations = CitationBuilder.From([Hit("b", 0), Hit("a", 3), Hit("c", 1)]);

        Assert.Equal([1, 2, 3], citations.Select(c => c.Marker));
        Assert.Equal(["b#0000", "a#0003", "c#0001"], citations.Select(c => c.ChunkId));
        Assert.Equal(["Title b", "Title a", "Title c"], citations.Select(c => c.Title));
    }

    [Fact]
    public void Builder_gives_chunks_of_one_document_a_single_marker()
    {
        var citations = CitationBuilder.From([Hit("a", 0), Hit("b", 0), Hit("a", 4), Hit("c", 0), Hit("b", 2)]);

        Assert.Equal(3, citations.Count);
        Assert.Equal(["a#0000", "b#0000", "c#0000"], citations.Select(c => c.ChunkId));
        Assert.Equal([1, 2, 3], citations.Select(c => c.Marker));
    }

    [Fact]
    public void Builder_carries_source_url_and_label()
    {
        var citation = Assert.Single(CitationBuilder.From([Hit("p", 0, SourceKind.LinkedIn, "2w")]));

        Assert.Equal("linkedin", citation.Source);
        Assert.Equal("msc.test/p", citation.Url);
        Assert.Equal("2w", citation.PublishedLabel);
    }

    [Fact]
    public void Builder_handles_no_hits()
    {
        Assert.Empty(CitationBuilder.From([]));
    }

    [Fact]
    public void Fully_cited_answer_reports_no_uncited_sentences()
    {
        var report = CitationEnforcer.Check("MSC operates a large container fleet [1]. It posts regular updates about new services [2].", TwoCitations);

        Assert.Equal(2, report.Cited);
        Assert.Equal(0, report.Uncited);
        Assert.Empty(report.UnsupportedSentences);
    }

    [Fact]
    public void Uncited_factual_claim_is_caught_verbatim()
    {
        var report = CitationEnforcer.Check("MSC was founded in 1970 [1]. It is the largest carrier in the world. It also serves Canada [2].", TwoCitations);

        Assert.Equal(2, report.Cited);
        Assert.Equal(1, report.Uncited);
        Assert.Equal(["It is the largest carrier in the world."], report.UnsupportedSentences);
    }

    [Theory]
    [InlineData("I could not find anything about that in the MSC sources.")]
    [InlineData("I couldn't find that in the available material.")]
    [InlineData("Sorry, I can't find information on that topic. Could you tell me which port you mean?")]
    [InlineData("Unfortunately, the retrieved sources do not mention vessel charter rates.")]
    [InlineData("There is no information about that in the sources I searched.")]
    [InlineData("Hello! How can I help you today?")]
    [InlineData("Which trade lane are you interested in?")]
    [InlineData("Let me know if you would like more detail.")]
    public void Refusals_greetings_and_questions_are_not_uncited_claims(string answer)
    {
        var report = CitationEnforcer.Check(answer, TwoCitations);

        Assert.Equal(0, report.Uncited);
        Assert.Equal(0, report.Cited);
        Assert.Empty(report.UnsupportedSentences);
    }

    [Theory]
    [InlineData("I can answer questions from MSC's public posts and web pages, with sources.")]
    [InlineData("I can also look up synthetic demo data on containers, vessels, ports and schedules.")]
    [InlineData("I can search the MSC material for that if you tell me which service you mean.")]
    [InlineData("I can query the demo database for counts and rankings.")]
    [InlineData("I can explain how the sources are cited.")]
    [InlineData("I'm able to retrieve sailing schedules from the demo data.")]
    [InlineData("I can list the ports in the demo data.")]
    public void First_person_capability_offers_are_not_uncited_claims(string answer)
    {
        var report = CitationEnforcer.Check(answer, TwoCitations);

        Assert.Equal(0, report.Uncited);
        Assert.Equal(0, report.Cited);
    }

    [Theory]
    [InlineData("I can confirm MSC operates 800 vessels.")]
    [InlineData("I can confirm that MSC is the largest carrier in the world.")]
    [InlineData("I can say that MSC operates the largest fleet.")]
    [InlineData("I can show that MSC operates 800 vessels.")]
    [InlineData("I can explain that MSC runs 24 services.")]
    [InlineData("I can answer this: MSC has 800 vessels.")]
    [InlineData("I can tell you MSC has hubs in Antwerp.")]
    public void First_person_statements_that_assert_a_fact_still_count_as_claims(string answer)
    {
        var report = CitationEnforcer.Check(answer, TwoCitations);

        Assert.Equal(1, report.Uncited);
        Assert.Equal([answer], report.UnsupportedSentences);
    }

    [Fact]
    public void A_capability_offer_does_not_launder_a_cited_fact_beside_it()
    {
        var report = CitationEnforcer.Check("I can also look up demo data. MSC operates a large fleet [1].", TwoCitations);

        Assert.Equal(1, report.Cited);
        Assert.Equal(0, report.Uncited);
    }

    [Fact]
    public void The_canned_refusal_reports_no_uncited_claims()
    {
        var report = CitationEnforcer.Check(Prompts.Ungrounded("What was MSC's net profit in 2024?"), []);

        Assert.Equal(0, report.Uncited);
        Assert.Equal(0, report.Cited);
    }

    [Fact]
    public void Refusal_with_a_claim_attached_still_flags_the_claim()
    {
        var report = CitationEnforcer.Check("I could not find the fleet size, but MSC has 800 ships.", TwoCitations);

        Assert.Equal(1, report.Uncited);
    }

    [Fact]
    public void Greeting_opener_does_not_launder_a_claim()
    {
        var report = CitationEnforcer.Check("Sure, MSC operates 800 ships.", TwoCitations);

        Assert.Equal(1, report.Uncited);
        Assert.Equal(["Sure, MSC operates 800 ships."], report.UnsupportedSentences);
    }

    [Fact]
    public void Marker_for_a_missing_citation_does_not_count_as_support()
    {
        var report = CitationEnforcer.Check("MSC has 800 ships [7]. MSC has hubs in Antwerp [1][9].", TwoCitations);

        Assert.Equal(1, report.Cited);
        Assert.Equal(1, report.Uncited);
        Assert.Equal(["MSC has 800 ships [7]."], report.UnsupportedSentences);
    }

    [Fact]
    public void Markers_inside_code_are_not_citations()
    {
        var answer = "Here is the lookup:\n```csharp\nvar first = ships[1];\n```\nMSC runs the Gioia Tauro hub [1].\nThe marker `[2]` is used in answers.";

        var report = CitationEnforcer.Check(answer, TwoCitations);

        Assert.Equal(1, report.Cited);
        Assert.Equal(1, report.Uncited);
        Assert.Equal(["The marker `[2]` is used in answers."], report.UnsupportedSentences);
    }

    [Fact]
    public void Code_block_contents_are_not_judged_as_claims()
    {
        var report = CitationEnforcer.Check("```\nThis line states a fact without a marker.\n```", TwoCitations);

        Assert.Equal(0, report.Cited);
        Assert.Equal(0, report.Uncited);
    }

    [Fact]
    public void Bare_number_lists_and_markdown_links_are_not_markers()
    {
        var report = CitationEnforcer.Check("The values are [1, 2] in the table. See [1](msc.test/fleet) for MSC details. Ranges like [1-2] appear too.", TwoCitations);

        Assert.Equal(0, report.Cited);
        Assert.Equal(3, report.Uncited);
    }

    [Fact]
    public void Adjacent_markers_and_markers_after_the_full_stop_both_work()
    {
        var report = CitationEnforcer.Check("MSC serves many ports [1][2]. MSC also posts updates.[2] Last claim stands. ", TwoCitations);

        Assert.Equal(2, report.Cited);
        Assert.Equal(["Last claim stands."], report.UnsupportedSentences);
    }

    [Fact]
    public void Bullets_are_judged_line_by_line()
    {
        var report = CitationEnforcer.Check("Key points:\n- The fleet is large [1]\n- The ports are many\n1. Hubs exist [2]", TwoCitations);

        Assert.Equal(2, report.Cited);
        Assert.Equal(["- The ports are many"], report.UnsupportedSentences);
    }

    [Fact]
    public void Empty_answers_and_empty_citation_lists_are_safe()
    {
        Assert.Equal(new GroundingReport(0, 0, []).Uncited, CitationEnforcer.Check(string.Empty, TwoCitations).Uncited);
        Assert.Equal(1, CitationEnforcer.Check("MSC has hubs [1].", []).Uncited);
    }

    [Fact]
    public void Emoji_sentences_do_not_crash_and_symbols_alone_are_ignored()
    {
        var report = CitationEnforcer.Check("\U0001F6A2\U0001F6A2 --- MSC sails to Antwerp [1] ⚓. \U0001F44B", TwoCitations);

        Assert.Equal(1, report.Cited);
        Assert.Equal(0, report.Uncited);
    }
}
