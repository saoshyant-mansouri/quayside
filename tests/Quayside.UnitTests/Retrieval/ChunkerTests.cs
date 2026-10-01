using System.Globalization;
using Quayside.Core.Documents;

namespace Quayside.UnitTests.Retrieval;

public sealed class ChunkerTests
{
    private const string Family = "\U0001F468‍\U0001F469‍\U0001F467‍\U0001F466";

    [Fact]
    public void Same_document_in_gives_identical_ids_and_text()
    {
        var doc = Fixtures.Document("doc-a", Fixtures.Sentences(120));

        var first = Chunker.Split(doc, 100, 30);
        var second = Chunker.Split(doc, 100, 30);

        Assert.True(first.Count > 3);
        Assert.Equal(first.Select(c => c.Id), second.Select(c => c.Id));
        Assert.Equal(first.Select(c => c.Text), second.Select(c => c.Text));
        Assert.Equal(first, second);
    }

    [Fact]
    public void Ids_are_document_scoped_and_sequential()
    {
        var chunks = Chunker.Split(Fixtures.Document("doc-a", Fixtures.Sentences(120)), 100, 30);

        Assert.All(chunks, c => Assert.Equal("doc-a", c.DocumentId));
        Assert.Equal(Enumerable.Range(0, chunks.Count), chunks.Select(c => c.Ordinal));
        Assert.Equal(chunks.Select(c => $"doc-a#{c.Ordinal:D4}"), chunks.Select(c => c.Id));
    }

    [Fact]
    public void Document_shorter_than_target_yields_exactly_one_chunk()
    {
        var doc = Fixtures.Document("short", "MSC operates worldwide. It calls at 520 ports.");

        var chunks = Chunker.Split(doc);

        var only = Assert.Single(chunks);
        Assert.Equal("MSC operates worldwide. It calls at 520 ports.", only.Text);
        Assert.Equal(0, only.Ordinal);
        Assert.Equal(TokenEstimator.Estimate(only.Text), only.TokenCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\n \t \n")]
    public void Empty_or_whitespace_text_yields_no_chunks(string text)
    {
        Assert.Empty(Chunker.Split(Fixtures.Document("empty", text)));
    }

    [Fact]
    public void Chunks_respect_the_token_target()
    {
        var chunks = Chunker.Split(Fixtures.Document("doc-a", Fixtures.Sentences(300)), 100, 30);

        Assert.All(chunks, c => Assert.InRange(c.TokenCount, 1, 100));
    }

    [Fact]
    public void Chunks_start_and_end_on_sentence_boundaries()
    {
        var text = Fixtures.Sentences(200);
        var spans = SentenceSplitter.Split(text);
        var starts = spans.Select(s => s.Start).ToHashSet();
        var ends = spans.Select(s => SentenceSplitter.TrimmedEnd(text, s)).ToHashSet();

        var chunks = Chunker.Split(Fixtures.Document("doc-a", text), 100, 30);

        Assert.True(chunks.Count > 3);
        var cursor = 0;
        foreach (var chunk in chunks)
        {
            var at = text.IndexOf(chunk.Text, cursor, StringComparison.Ordinal);
            Assert.True(at >= 0);
            Assert.Contains(at, starts);
            Assert.Contains(at + chunk.Text.Length, ends);
            cursor = at + 1;
        }
    }

    [Fact]
    public void Consecutive_chunks_overlap_by_whole_sentences_within_the_budget()
    {
        var text = Fixtures.Sentences(200);
        var chunks = Chunker.Split(Fixtures.Document("doc-a", text), 100, 40);

        for (var i = 0; i + 1 < chunks.Count; i++)
        {
            var left = chunks[i].Text;
            var right = chunks[i + 1].Text;
            var shared = LongestSuffixPrefix(left, right);

            Assert.NotEmpty(shared);
            Assert.EndsWith(".", shared);
            Assert.StartsWith("Sentence", shared);
            Assert.True(TokenEstimator.Estimate(shared) <= 40);
            Assert.True(shared.Length < left.Length);
        }
    }

    [Fact]
    public void Zero_overlap_produces_disjoint_chunks()
    {
        var text = Fixtures.Sentences(200);
        var chunks = Chunker.Split(Fixtures.Document("doc-a", text), 100, 0);

        for (var i = 0; i + 1 < chunks.Count; i++)
        {
            Assert.Empty(LongestSuffixPrefix(chunks[i].Text, chunks[i + 1].Text));
        }

        Assert.Equal(text, string.Join(" ", chunks.Select(c => c.Text)));
    }

    [Fact]
    public void Reassembling_chunks_loses_no_content()
    {
        var text = Fixtures.Sentences(200);

        var chunks = Chunker.Split(Fixtures.Document("doc-a", text), 100, 30);

        AssertCovers(text, chunks);
        Assert.StartsWith(chunks[0].Text, text);
        Assert.EndsWith(chunks[^1].Text, text);
    }

    [Fact]
    public void Paragraph_structure_is_preserved_inside_chunks()
    {
        var text = "First paragraph here.\n\nSecond paragraph follows.\n\nThird one ends it.";

        var chunk = Assert.Single(Chunker.Split(Fixtures.Document("para", text)));

        Assert.Equal(text, chunk.Text);
    }

    [Fact]
    public void Text_without_sentence_punctuation_still_splits_on_word_boundaries()
    {
        var words = Enumerable.Range(0, 3000).Select(n => $"word{n}").ToArray();
        var text = string.Join(" ", words);

        var chunks = Chunker.Split(Fixtures.Document("nopunct", text), 100, 20);

        Assert.True(chunks.Count > 5);
        Assert.All(chunks, c => Assert.InRange(c.TokenCount, 1, 100));
        var wordSet = words.ToHashSet();
        Assert.All(chunks, c => Assert.All(c.Text.Split(' '), w => Assert.Contains(w, wordSet)));
        AssertCovers(text, chunks);
    }

    [Fact]
    public void Very_long_single_sentence_without_whitespace_still_splits()
    {
        var text = new string('x', 10_000) + ".";

        var chunks = Chunker.Split(Fixtures.Document("long", text), 50, 10);

        Assert.True(chunks.Count >= 50);
        Assert.All(chunks, c => Assert.InRange(c.TokenCount, 1, 50));
        Assert.Equal(text, string.Concat(chunks.Select(c => c.Text)));
    }

    [Fact]
    public void Very_long_single_sentence_with_spaces_splits_between_words()
    {
        var text = string.Join(" ", Enumerable.Repeat("containerised", 2000)) + ".";

        var chunks = Chunker.Split(Fixtures.Document("long", text), 50, 10);

        Assert.True(chunks.Count > 10);
        Assert.All(chunks, c => Assert.All(c.Text.TrimEnd('.').Split(' '), w => Assert.Equal("containerised", w)));
    }

    [Fact]
    public void Grapheme_clusters_are_never_split()
    {
        var text = string.Concat(Enumerable.Repeat(Family, 600));

        var chunks = Chunker.Split(Fixtures.Document("family", text), 5, 1);

        Assert.True(chunks.Count > 100);
        Assert.All(chunks, c =>
        {
            Assert.True(Fixtures.IsWellFormed(c.Text));
            Assert.Equal(0, c.Text.Length % Family.Length);
            Assert.Equal(c.Text.Length / Family.Length, new StringInfo(c.Text).LengthInTextElements);
        });
        Assert.Equal(text, string.Concat(chunks.Select(c => c.Text)));
    }

    [Fact]
    public void Emoji_heavy_social_text_never_splits_a_surrogate_pair()
    {
        var post = "Connecting Canada to markets across Europe \U0001F1E8\U0001F1E6 \U0001F1EA\U0001F1FA\n\nWeekly sailings \U0001F6A2 from Montréal! Learn more → now \U0001F44B\n\n#msccargo\n#trade";
        var text = string.Join("\n\n", Enumerable.Range(1, 40).Select(n => $"Post {n}: {post}"));

        foreach (var target in new[] { 3, 7, 25, 60 })
        {
            var chunks = Chunker.Split(Fixtures.Document("post", text), target, target / 3);

            Assert.All(chunks, c => Assert.True(Fixtures.IsWellFormed(c.Text)));
            AssertCovers(text, chunks);
        }
    }

    [Fact]
    public void Non_ascii_text_is_chunked_without_loss()
    {
        var text = string.Join(" ", Enumerable.Range(1, 80).Select(n => $"船舶在港口装载货物第{n}批。"));

        var chunks = Chunker.Split(Fixtures.Document("cjk", text), 40, 10);

        Assert.True(chunks.Count > 3);
        Assert.All(chunks, c => Assert.EndsWith("。", c.Text));
        AssertCovers(text, chunks);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, 10)]
    [InlineData(10, 20)]
    [InlineData(10, -1)]
    public void Invalid_budgets_are_rejected(int target, int overlap)
    {
        var doc = Fixtures.Document("x", "Some text.");

        Assert.ThrowsAny<ArgumentException>(() => Chunker.Split(doc, target, overlap));
    }

    [Fact]
    public void Splitting_the_real_post_shape_keeps_links_and_hashtags_intact()
    {
        var text = "Houston, let’s talk project cargo \U0001F3D7️ \U0001F1FA\U0001F1F8\n\nWe're at Breakbulk Americas 2026, where our team is joined by MSC colleagues.\n\nLearn more → lnkd.in/eKtnieTg\n\n#msccargo\n#project";

        var chunk = Assert.Single(Chunker.Split(Fixtures.Document("p", text)));

        Assert.Contains("lnkd.in/eKtnieTg", chunk.Text);
        Assert.EndsWith("#project", chunk.Text);
    }

    private static string LongestSuffixPrefix(string left, string right)
    {
        for (var length = Math.Min(left.Length, right.Length); length > 0; length--)
        {
            if (string.CompareOrdinal(left, left.Length - length, right, 0, length) == 0)
            {
                return right[..length];
            }
        }

        return string.Empty;
    }

    private static void AssertCovers(string text, IReadOnlyList<Chunk> chunks)
    {
        var covered = new bool[text.Length];
        var cursor = 0;
        foreach (var chunk in chunks)
        {
            var at = text.IndexOf(chunk.Text, cursor, StringComparison.Ordinal);
            Assert.True(at >= 0, $"Chunk {chunk.Ordinal} is not a verbatim slice of the source.");
            Array.Fill(covered, true, at, chunk.Text.Length);
            cursor = at + 1;
        }

        for (var i = 0; i < text.Length; i++)
        {
            Assert.True(covered[i] || char.IsWhiteSpace(text[i]), $"Character at {i} was lost.");
        }
    }
}
