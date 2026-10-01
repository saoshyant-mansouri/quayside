using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Quayside.Core;
using Quayside.Core.Documents;

namespace Quayside.Api.Offline;

public sealed partial class OfflineChunkStore(OfflineData data) : IChunkStore
{
    private readonly Lazy<(IReadOnlyList<Document> Documents, IReadOnlyList<EmbeddedChunk> Chunks)> corpus = new(() => Load(data));

    public Task<IReadOnlyList<Document>> LoadDocumentsAsync(CancellationToken ct) => Task.FromResult(corpus.Value.Documents);

    public Task<IReadOnlyList<EmbeddedChunk>> LoadChunksAsync(CancellationToken ct) => Task.FromResult(corpus.Value.Chunks);

    public Task UpsertAsync(IReadOnlyList<Document> documents, IReadOnlyList<EmbeddedChunk> chunks, CancellationToken ct) => Task.CompletedTask;

    private static (IReadOnlyList<Document>, IReadOnlyList<EmbeddedChunk>) Load(OfflineData data)
    {
        var documents = new Dictionary<string, Document>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(data.CorpusDirectory, "*.json").Order(StringComparer.Ordinal))
        {
            using var stream = File.OpenRead(file);
            using var json = JsonDocument.Parse(stream);
            var root = json.RootElement;
            var capturedAt = DateTimeOffset.Parse(root.GetProperty("capturedAt").GetString()!, CultureInfo.InvariantCulture);

            if (root.TryGetProperty("posts", out var posts))
            {
                foreach (var post in posts.EnumerateArray())
                {
                    var document = Post(post, capturedAt);
                    documents[document.Id] = document;
                }
            }

            if (root.TryGetProperty("pages", out var pages))
            {
                foreach (var page in pages.EnumerateArray())
                {
                    var document = Page(page, capturedAt);
                    documents[document.Id] = document;
                }
            }
        }

        var chunks = documents.Values
            .SelectMany(document => Chunker.Split(document))
            .Select(chunk => new EmbeddedChunk(chunk, HashingEmbeddingGenerator.Embed(chunk.Text)))
            .ToArray();
        return (documents.Values.ToArray(), chunks);
    }

    private static Document Post(JsonElement post, DateTimeOffset capturedAt)
    {
        var text = post.GetProperty("text").GetString() ?? string.Empty;
        var firstLine = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "LinkedIn post";
        var title = firstLine.Length <= 90 ? firstLine : firstLine[..90] + "...";
        return new Document(
            $"li-{post.GetProperty("urn").GetString()}",
            SourceKind.LinkedIn,
            post.GetProperty("url").GetString() ?? string.Empty,
            title,
            text,
            PublishedDate(post.GetProperty("age").GetString(), capturedAt),
            capturedAt,
            Hash(text));
    }

    private static Document Page(JsonElement page, DateTimeOffset capturedAt)
    {
        var url = page.GetProperty("url").GetString() ?? string.Empty;
        var text = page.GetProperty("text").GetString() ?? string.Empty;
        return new Document($"web-{Hash(url)[..16]}", SourceKind.Website, url, page.GetProperty("title").GetString() ?? url, text, null, capturedAt, Hash(text));
    }

    private static string? PublishedDate(string? age, DateTimeOffset capturedAt)
    {
        var match = AgePattern().Match(age ?? string.Empty);
        if (!match.Success)
        {
            return null;
        }

        var amount = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var published = match.Groups[2].Value switch
        {
            "d" => capturedAt.AddDays(-amount),
            "w" => capturedAt.AddDays(-7 * amount),
            "mo" => capturedAt.AddMonths(-amount),
            "yr" => capturedAt.AddYears(-amount),
            _ => capturedAt,
        };
        return published.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    [GeneratedRegex(@"^(\d+)(d|w|mo|yr)")]
    private static partial Regex AgePattern();
}
