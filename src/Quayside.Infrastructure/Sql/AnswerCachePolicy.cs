using System.Security.Cryptography;
using System.Text;

namespace Quayside.Infrastructure.Sql;

public static class AnswerCachePolicy
{
    public const double MinimumSimilarity = 0.97;

    private const double Tolerance = 1e-9;

    public static bool IsHit(double similarity) =>
        !double.IsNaN(similarity) && similarity >= MinimumSimilarity - Tolerance;

    public static double SimilarityFromCosineDistance(double distance) => 1.0 - distance;

    public static byte[] HashQuestion(string question)
    {
        ArgumentNullException.ThrowIfNull(question);
        var normalised = string.Join(' ', question.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToLowerInvariant();
        return SHA256.HashData(Encoding.UTF8.GetBytes(normalised));
    }
}
