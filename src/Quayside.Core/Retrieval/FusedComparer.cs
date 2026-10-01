namespace Quayside.Core.Retrieval;

internal readonly struct FusedComparer(double[] fused) : IComparer<int>
{
    public int Compare(int left, int right)
    {
        var byScore = fused[right].CompareTo(fused[left]);
        return byScore != 0 ? byScore : left.CompareTo(right);
    }
}
