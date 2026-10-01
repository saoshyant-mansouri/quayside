using System.Security.Cryptography;
using System.Text;
using Quayside.Core.Documents;
using Quayside.Core.Retrieval;

namespace Quayside.Core.Web;

public static class WebSources
{
    public static ScoredChunk ToHit(WebResult result, DateTimeOffset capturedAt)
    {
        var id = "web-" + Hash(result.Url)[..16];
        var document = new Document(id, SourceKind.Web, result.Url, result.Title, result.Snippet, result.PublishedLabel, capturedAt, Hash(result.Snippet));
        var chunk = new Chunk($"{id}#0000", id, 0, result.Snippet, TokenEstimator.Estimate(result.Snippet));
        return new ScoredChunk(chunk, document, 0);
    }

    private static string Hash(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
