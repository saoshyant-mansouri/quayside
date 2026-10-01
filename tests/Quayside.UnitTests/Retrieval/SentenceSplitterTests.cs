using Quayside.Core.Documents;

namespace Quayside.UnitTests.Retrieval;

public sealed class SentenceSplitterTests
{
    private static string[] Sentences(string text) =>
        SentenceSplitter.Split(text).Select(s => text[s.Start..SentenceSplitter.TrimmedEnd(text, s)]).ToArray();

    [Fact]
    public void Splits_on_terminal_punctuation_followed_by_whitespace()
    {
        Assert.Equal(["One.", "Two!", "Three?", "Four."], Sentences("One. Two! Three? Four."));
    }

    [Fact]
    public void Does_not_split_inside_urls_decimals_or_abbreviations()
    {
        var text = "Dr. Smith visited lnkd.in/eHVF4mXX on 3.5 days in the U.S. last year. He left.";

        Assert.Equal(["Dr. Smith visited lnkd.in/eHVF4mXX on 3.5 days in the U.S. last year.", "He left."], Sentences(text));
    }

    [Fact]
    public void Treats_every_line_as_its_own_unit()
    {
        Assert.Equal(["Heading", "First line", "#tag"], Sentences("Heading\n\nFirst line\n#tag"));
    }

    [Fact]
    public void Keeps_citation_markers_with_the_sentence_they_follow()
    {
        Assert.Equal(["It is big.[1]", "It is old. [2]", "Done."], Sentences("It is big.[1] It is old. [2] Done."));
    }

    [Fact]
    public void Does_not_split_list_numbers_from_their_item()
    {
        Assert.Equal(["1. First item", "2. Second item"], Sentences("1. First item\n2. Second item"));
    }

    [Fact]
    public void Spans_tile_the_text_without_gaps()
    {
        var text = "  Leading space. Middle part!\n\nNew paragraph?  Trailing.  ";

        var spans = SentenceSplitter.Split(text);

        Assert.Equal(2, spans[0].Start);
        for (var i = 1; i < spans.Count; i++)
        {
            Assert.Equal(spans[i - 1].End, spans[i].Start);
        }

        Assert.Equal(text.Length, spans[^1].End);
    }
}
