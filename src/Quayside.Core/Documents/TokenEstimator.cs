namespace Quayside.Core.Documents;

public static class TokenEstimator
{
    public const int CharactersPerToken = 4;

    public static int Estimate(int characterCount) =>
        (characterCount + CharactersPerToken - 1) / CharactersPerToken;

    public static int Estimate(ReadOnlySpan<char> text) => Estimate(text.Length);
}
