using Quayside.Core;
using Quayside.Core.Documents;
using Quayside.Core.Grounding;
using Quayside.Core.Web;

namespace Quayside.UnitTests.Web;

public sealed class WebResultSanitizerTests
{
    private static WebResult Result(string title = "Title", string url = "https://example.com/a", string snippet = "Snippet", string? label = null) =>
        new(title, url, snippet, label);

    [Fact]
    public void Markup_is_stripped_and_entities_are_decoded()
    {
        var cleaned = Assert.Single(WebResultSanitizer.Clean([Result("<b>MSC</b> &amp; Co", snippet: "&lt;script&gt;x()&lt;/script&gt;<p>Via   Nizza</p>")], 5));

        Assert.Equal("MSC & Co", cleaned.Title);
        Assert.Equal("x() Via Nizza", cleaned.Snippet);
    }

    [Fact]
    public void Citation_markers_inside_text_are_neutralised()
    {
        var cleaned = Assert.Single(WebResultSanitizer.Clean([Result("A [1]", snippet: "see [2] and [99]")], 5));

        Assert.Equal("A (1)", cleaned.Title);
        Assert.Equal("see (2) and (99)", cleaned.Snippet);
    }

    [Fact]
    public void Control_format_and_newline_characters_collapse_to_single_spaces()
    {
        var cleaned = Assert.Single(WebResultSanitizer.Clean([Result(snippet: "one\r\n\r\ntwo\u0007three​four‮tab\there")], 5));

        Assert.Equal("one two three four tab here", cleaned.Snippet);
    }

    [Fact]
    public void Long_text_is_truncated_on_a_word_boundary()
    {
        var cleaned = Assert.Single(WebResultSanitizer.Clean([Result(snippet: string.Join(' ', Enumerable.Repeat("harbour", 200)))], 5));

        Assert.True(cleaned.Snippet.Length <= WebResultSanitizer.MaxSnippetLength);
        Assert.EndsWith("harbour...", cleaned.Snippet, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,hi")]
    [InlineData("ftp://example.com/file")]
    [InlineData("https://user:pass@example.com/")]
    [InlineData("/relative/path")]
    [InlineData("not a url")]
    [InlineData("")]
    public void Unsafe_or_malformed_urls_drop_the_result(string url)
    {
        Assert.Empty(WebResultSanitizer.Clean([Result(url: url)], 5));
    }

    [Fact]
    public void A_result_with_no_snippet_is_dropped_and_a_missing_title_falls_back_to_the_host()
    {
        var cleaned = WebResultSanitizer.Clean([Result(snippet: "  <br> "), Result(title: "", url: "https://www.example.com/x")], 5);

        var only = Assert.Single(cleaned);
        Assert.Equal("www.example.com", only.Title);
    }

    [Fact]
    public void Duplicate_urls_collapse_and_the_cap_is_applied()
    {
        var cleaned = WebResultSanitizer.Clean(
            [Result(url: "https://a.example/x"), Result(url: "https://A.example/x"), Result(url: "https://b.example/"), Result(url: "https://c.example/")],
            2);

        Assert.Equal(["https://a.example/x", "https://b.example/"], cleaned.Select(r => r.Url));
    }

    [Fact]
    public void An_empty_label_becomes_null()
    {
        var cleaned = Assert.Single(WebResultSanitizer.Clean([Result(label: " <i></i> ")], 5));

        Assert.Null(cleaned.PublishedLabel);
    }

    [Fact]
    public void A_result_maps_to_a_web_hit_that_the_citation_builder_labels_web()
    {
        var hit = WebSources.ToHit(Result(label: "2025-06-01"), DateTimeOffset.UnixEpoch);

        Assert.Equal(SourceKind.Web, hit.Document.Source);
        Assert.Equal("Snippet", hit.Chunk.Text);
        var citation = Assert.Single(CitationBuilder.From([hit]));
        Assert.Equal("web", citation.Source);
        Assert.Equal("https://example.com/a", citation.Url);
        Assert.Equal("2025-06-01", citation.PublishedLabel);
    }

    [Fact]
    public void The_same_url_always_maps_to_the_same_document_id()
    {
        var first = WebSources.ToHit(Result(snippet: "one"), DateTimeOffset.UnixEpoch);
        var second = WebSources.ToHit(Result(snippet: "two"), DateTimeOffset.UnixEpoch);

        Assert.Equal(first.Document.Id, second.Document.Id);
        Assert.StartsWith("web-", first.Document.Id, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_no_op_search_returns_nothing_and_carries_its_reason()
    {
        var none = new NoWebSearch("WebSearch:ApiKey is not set");

        Assert.Empty(await none.SearchAsync("anything", 5, CancellationToken.None));
        Assert.Equal("WebSearch:ApiKey is not set", none.Reason);
    }
}
