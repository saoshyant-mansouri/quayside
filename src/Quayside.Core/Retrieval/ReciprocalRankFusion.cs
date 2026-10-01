namespace Quayside.Core.Retrieval;

public static class ReciprocalRankFusion
{
    public const int K = 60;

    public static double Contribution(int rank) => 1.0 / (K + rank);

    public static void Accumulate(ReadOnlySpan<int> ranking, Span<double> fused)
    {
        for (var position = 0; position < ranking.Length; position++)
        {
            fused[ranking[position]] += Contribution(position + 1);
        }
    }
}
