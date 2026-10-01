using System.Text;
using Quayside.Api.Contract;
using Quayside.Api.Streaming;

namespace Quayside.UnitTests.Api;

public sealed class SseWireTests
{
    [Fact]
    public async Task The_sink_writes_one_framed_event_per_emit()
    {
        var body = new MemoryStream();
        var sink = new SseStreamSink(body);

        await sink.EmitAsync(new TokenEvent("hello"), CancellationToken.None);
        await sink.EmitAsync(new ErrorEvent("nope"), CancellationToken.None);

        var events = SseParser.Parse(Encoding.UTF8.GetString(body.ToArray()));
        Assert.Equal(["token", "error"], events.Select(e => e.Name));
        Assert.Equal("hello", events[0]["text"].GetString());
        Assert.Equal("nope", events[1]["message"].GetString());
    }

    [Fact]
    public async Task Concurrent_emits_never_interleave_inside_a_frame()
    {
        var body = new MemoryStream();
        var sink = new SseStreamSink(body);

        await Task.WhenAll(Enumerable.Range(0, 200).Select(i => sink.EmitAsync(new TokenEvent($"t{i}"), CancellationToken.None).AsTask()));

        var events = SseParser.Parse(Encoding.UTF8.GetString(body.ToArray()));
        Assert.Equal(200, events.Count);
        Assert.Equal(200, events.Select(e => e["text"].GetString()).Distinct().Count());
    }

    [Fact]
    public void Property_names_are_camel_case_and_null_optionals_are_omitted()
    {
        var tool = SseJson.Serialize(new ToolEvent("search_knowledge", "started"));

        Assert.Equal("""{"name":"search_knowledge","status":"started"}""", tool);
    }

    [Fact]
    public void A_null_rejection_and_a_null_publication_date_stay_explicit()
    {
        var sql = SseJson.Serialize(new SqlEvent("SELECT 1", ["a"], [[1]], null));
        var citations = SseJson.Serialize(new CitationsEvent([new CitationPayload(1, "t", "u", "website", null)]));

        Assert.Contains("\"rejected\":null", sql);
        Assert.Contains("\"publishedAt\":null", citations);
    }

    [Fact]
    public void Text_with_newlines_cannot_break_the_frame()
    {
        var frame = SseJson.Frame(new TokenEvent("line one\n\nevent: done\n"));

        var events = SseParser.Parse(frame);
        Assert.Equal("token", Assert.Single(events).Name);
        Assert.Equal("line one\n\nevent: done\n", events[0]["text"].GetString());
    }

    [Theory]
    [InlineData("data: {}\n\n")]
    [InlineData("event: token\n\n")]
    [InlineData("event: token\ndata: {}\n")]
    [InlineData("event: token\ndata: {}\nid: 1\n\n")]
    [InlineData("event: token\ndata: not json\n\n")]
    [InlineData("event: token\ndata: {}\n\n\n")]
    public void The_parser_rejects_malformed_streams(string raw)
    {
        Assert.ThrowsAny<Exception>(() => SseParser.Parse(raw));
    }

    [Fact]
    public void The_parser_accepts_an_empty_stream()
    {
        Assert.Empty(SseParser.Parse(string.Empty));
    }
}
