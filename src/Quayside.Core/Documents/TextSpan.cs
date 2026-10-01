namespace Quayside.Core.Documents;

public readonly record struct TextSpan(int Start, int End)
{
    public int Length => End - Start;
}
