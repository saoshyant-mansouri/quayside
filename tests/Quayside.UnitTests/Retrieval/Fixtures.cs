using Quayside.Core.Documents;

namespace Quayside.UnitTests.Retrieval;

internal static class Fixtures
{
    public static Document Document(string id, string text, string title = "", SourceKind source = SourceKind.Website) =>
        new(id, source, $"msc.test/{id}", title, text, null, DateTimeOffset.UnixEpoch, "hash-" + id);

    public static EmbeddedChunk Embedded(string documentId, int ordinal, string text, params float[] embedding) =>
        new(new Chunk($"{documentId}#{ordinal:D4}", documentId, ordinal, text, TokenEstimator.Estimate(text)), embedding);

    public static bool IsWellFormed(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]))
            {
                if (i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1]))
                {
                    return false;
                }

                i++;
            }
            else if (char.IsLowSurrogate(text[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static string Sentences(int count) =>
        string.Join(" ", Enumerable.Range(1, count).Select(n => $"Sentence number {n} talks about vessels and ports."));
}
