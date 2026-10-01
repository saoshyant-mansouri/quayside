using Quayside.Api.Orchestration;
using Quayside.Core;
using Quayside.Core.Documents;
using Quayside.Core.Retrieval;
using Quayside.Core.Web;

namespace Quayside.UnitTests.Api;

public sealed class SourceLedgerTests
{
    private static ScoredChunk Corpus(string id) =>
        new(
            new Chunk($"{id}#0000", id, 0, "corpus text", 3),
            new Document(id, SourceKind.Website, $"https://msc.test/{id}", $"Page {id}", "corpus text", null, DateTimeOffset.UnixEpoch, "h"),
            1.0);

    private static ScoredChunk Web(string path) =>
        WebSources.ToHit(new WebResult($"Result {path}", $"https://web.test/{path}", "web snippet", null), DateTimeOffset.UnixEpoch);

    [Fact]
    public void Corpus_sources_are_numbered_before_web_sources_whatever_the_arrival_order()
    {
        var ledger = new SourceLedger();

        var webReferences = ledger.Add([Web("a")]);
        var corpusReferences = ledger.Add([Corpus("x"), Corpus("y")]);

        Assert.Equal(["website", "website", "web"], ledger.Citations.Select(c => c.Source));
        Assert.Equal([1, 2, 3], ledger.Citations.Select(c => c.Marker));
        Assert.Equal([1, 2], corpusReferences.Select(r => r.Marker));
        Assert.Equal(1, webReferences.Single().Marker);
    }

    [Fact]
    public void Web_sources_appended_after_corpus_sources_keep_the_corpus_markers()
    {
        var ledger = new SourceLedger();
        ledger.Add([Corpus("x"), Corpus("y")]);

        var web = ledger.Add([Web("a"), Web("b")]);

        Assert.Equal([3, 4], web.Select(r => r.Marker));
        Assert.Equal(["web", "web"], ledger.Citations.Skip(2).Select(c => c.Source));
        Assert.True(ledger.HasWebSources);
    }

    [Fact]
    public void The_same_web_result_returned_twice_is_cited_once()
    {
        var ledger = new SourceLedger();

        ledger.Add([Web("a")]);
        ledger.Add([Web("a")]);

        Assert.Single(ledger.Citations);
    }

    [Fact]
    public void A_ledger_with_only_corpus_sources_reports_no_web_sources()
    {
        var ledger = new SourceLedger();
        ledger.Add([Corpus("x")]);

        Assert.False(ledger.HasWebSources);
    }
}
