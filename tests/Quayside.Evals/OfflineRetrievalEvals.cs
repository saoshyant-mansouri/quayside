using Microsoft.Extensions.Configuration;
using Quayside.Api.Offline;
using Quayside.Core.Documents;
using Quayside.Core.Retrieval;
using Quayside.Core.Sql;

namespace Quayside.Evals;

public sealed class OfflineRetrievalEvals
{
    private static readonly OfflineData Data = new(new ConfigurationBuilder().Build());
    private static readonly Lazy<(HybridIndex Index, IReadOnlyList<Document> Documents)> CorpusFixture = new(LoadCorpus);
    private static readonly Lazy<SchemaRetriever> SchemaFixture = new(LoadSchema);

    private static (HybridIndex Index, IReadOnlyList<Document> Documents) LoadCorpus()
    {
        var store = new OfflineChunkStore(Data);
        var documents = store.LoadDocumentsAsync(CancellationToken.None).GetAwaiter().GetResult();
        var chunks = store.LoadChunksAsync(CancellationToken.None).GetAwaiter().GetResult();
        var index = HybridIndex.Build(documents, chunks);
        return (index, documents);
    }

    private static SchemaRetriever LoadSchema()
    {
        var catalog = new OfflineSchemaCatalog(Data);
        var cards = catalog.LoadCardsAsync(CancellationToken.None).GetAwaiter().GetResult();
        var embedded = cards
            .Select(card => new EmbeddedTableCard(card, HashingEmbeddingGenerator.Embed(card.Card)))
            .ToArray();
        return new SchemaRetriever(embedded);
    }

    [Fact]
    public void Corpus_snapshot_loads_both_website_and_linkedin_documents()
    {
        var (_, documents) = CorpusFixture.Value;

        Assert.NotEmpty(documents);
        Assert.Contains(documents, d => d.Source == SourceKind.Website);
        Assert.Contains(documents, d => d.Source == SourceKind.LinkedIn);
    }

    [Theory]
    [MemberData(nameof(GetFleetAndNetworkCases))]
    public void Fleet_and_network_queries_retrieve_chunks_with_expected_keywords(GoldenCase testCase)
    {
        var (index, _) = CorpusFixture.Value;
        var queryEmbedding = HashingEmbeddingGenerator.Embed(testCase.Question);
        var result = index.Search(queryEmbedding, testCase.Question, k: 6);

        Assert.NotEmpty(result.Hits);
        var combinedRetrievedText = string.Join(" ", result.Hits.Select(h => $"{h.Document.Title} {h.Chunk.Text}"));

        foreach (var keyword in testCase.ExpectedKeywords)
        {
            Assert.Contains(keyword, combinedRetrievedText, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [MemberData(nameof(GetSustainabilityAndCargoCases))]
    public void Sustainability_and_cargo_queries_retrieve_chunks_with_expected_keywords(GoldenCase testCase)
    {
        var (index, _) = CorpusFixture.Value;
        var queryEmbedding = HashingEmbeddingGenerator.Embed(testCase.Question);
        var result = index.Search(queryEmbedding, testCase.Question, k: 6);

        Assert.NotEmpty(result.Hits);
        var combinedRetrievedText = string.Join(" ", result.Hits.Select(h => $"{h.Document.Title} {h.Chunk.Text}"));

        foreach (var keyword in testCase.ExpectedKeywords)
        {
            Assert.Contains(keyword, combinedRetrievedText, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [MemberData(nameof(GetRefusalTrapCases))]
    public void Out_of_scope_questions_lack_grounding_evidence_triggering_refusals(GoldenCase testCase)
    {
        var (index, _) = CorpusFixture.Value;
        var queryEmbedding = HashingEmbeddingGenerator.Embed(testCase.Question);
        var result = index.Search(queryEmbedding, testCase.Question, k: 6);

        Assert.True(testCase.ExpectRefusal);
        var combinedRetrievedText = string.Join(" ", result.Hits.Select(h => $"{h.Document.Title} {h.Chunk.Text}"));

        foreach (var keyword in testCase.ExpectedKeywords)
        {
            Assert.DoesNotContain(keyword, combinedRetrievedText, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [MemberData(nameof(GetOperationalSqlCases))]
    public void Schema_retriever_matches_operational_queries_to_expected_tables(GoldenCase testCase)
    {
        var retriever = SchemaFixture.Value;
        var queryEmbedding = HashingEmbeddingGenerator.Embed(testCase.Question);
        var tables = retriever.Select(queryEmbedding, testCase.Question, k: 8, hops: 1)
            .Select(t => t.Table)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.NotNull(testCase.ExpectedTables);
        foreach (var expectedTable in testCase.ExpectedTables!)
        {
            Assert.Contains(expectedTable, tables);
        }
    }

    public static TheoryData<GoldenCase> GetFleetAndNetworkCases()
    {
        var data = new TheoryData<GoldenCase>();
        foreach (var item in GoldenDataset.Cases.Where(c => c.Category == GoldenDataset.FleetAndNetwork))
        {
            data.Add(item);
        }
        return data;
    }

    public static TheoryData<GoldenCase> GetSustainabilityAndCargoCases()
    {
        var data = new TheoryData<GoldenCase>();
        foreach (var item in GoldenDataset.Cases.Where(c => c.Category == GoldenDataset.SustainabilityAndCargo))
        {
            data.Add(item);
        }
        return data;
    }

    public static TheoryData<GoldenCase> GetRefusalTrapCases()
    {
        var data = new TheoryData<GoldenCase>();
        foreach (var item in GoldenDataset.Cases.Where(c => c.Category == GoldenDataset.RefusalTraps))
        {
            data.Add(item);
        }
        return data;
    }

    public static TheoryData<GoldenCase> GetOperationalSqlCases()
    {
        var data = new TheoryData<GoldenCase>();
        foreach (var item in GoldenDataset.Cases.Where(c => c.Category == GoldenDataset.OperationalSql))
        {
            data.Add(item);
        }
        return data;
    }
}
