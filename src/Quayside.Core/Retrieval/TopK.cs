namespace Quayside.Core.Retrieval;

public static class TopK
{
    public static int Select(ReadOnlySpan<double> scores, double minimumExclusive, Span<int> destination)
    {
        var count = 0;
        var capacity = destination.Length;
        if (capacity == 0)
        {
            return 0;
        }

        for (var index = 0; index < scores.Length; index++)
        {
            var score = scores[index];
            if (score <= minimumExclusive)
            {
                continue;
            }

            if (count == capacity && score <= scores[destination[count - 1]])
            {
                continue;
            }

            var position = count < capacity ? count : capacity - 1;
            while (position > 0 && scores[destination[position - 1]] < score)
            {
                destination[position] = destination[position - 1];
                position--;
            }

            destination[position] = index;
            if (count < capacity)
            {
                count++;
            }
        }

        return count;
    }
}
