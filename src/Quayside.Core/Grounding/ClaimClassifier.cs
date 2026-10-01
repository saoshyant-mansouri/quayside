using System.Text.RegularExpressions;

namespace Quayside.Core.Grounding;

internal static partial class ClaimClassifier
{
    public static bool AssertsFact(string sentence)
    {
        var core = Core(sentence);
        if (!HasLetterOrDigit(core))
        {
            return false;
        }

        if (core.StartsWith('#') || core.EndsWith('?') || core.EndsWith(':'))
        {
            return false;
        }

        var remainder = StripOpener(core);
        if (!HasLetterOrDigit(remainder))
        {
            return false;
        }

        if (Pleasantry().IsMatch(remainder) || Offer().IsMatch(remainder))
        {
            return false;
        }

        return !(Refusal().IsMatch(remainder) && !Contrast().IsMatch(remainder));
    }

    private static string Core(string sentence)
    {
        var withoutMarkers = CitationMarkers.Pattern().Replace(sentence, string.Empty);
        var trimmed = withoutMarkers.Trim();
        trimmed = ListPrefix().Replace(trimmed, string.Empty);
        return trimmed.Trim().TrimEnd('"', '\'', '”', '’', ')', '*', '_', ' ');
    }

    private static string StripOpener(string core) => Opener().Replace(core, string.Empty).Trim();

    private static bool HasLetterOrDigit(string text)
    {
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c))
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"^\s*(?:[-*+>•]\s+|\d{1,3}[.)]\s+)+")]
    private static partial Regex ListPrefix();

    [GeneratedRegex(@"^(?:hello|hi|hey|greetings|good (?:morning|afternoon|evening)|sure|certainly|of course|absolutely)\b[\s,!.:;-]*", RegexOptions.IgnoreCase)]
    private static partial Regex Opener();

    [GeneratedRegex(@"^(?:thanks|thank you|you'?re welcome|you are welcome|no problem|goodbye|bye|great question|good question|happy to help|glad to help|i hope this helps|hope this helps)\b[^.!?]{0,40}[.!]*$", RegexOptions.IgnoreCase)]
    private static partial Regex Pleasantry();

    [GeneratedRegex(@"^(?:please\s+)?(?:let me know|feel free|would you like|do you want|is there anything|i can (?:also )?(?:help|look)|i'?d be happy|i would be happy|if you(?:'d| would)? like)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Offer();

    [GeneratedRegex(@"^(?:(?:unfortunately|sorry|i'?m sorry|i am sorry|sadly|regrettably|however)\s*,?\s*)*(?:i\s+(?:could\s*not|couldn'?t|can\s*not|can'?t|cannot|was\s+(?:not\s+able|unable)|am\s+(?:not\s+able|unable)|'?m\s+(?:not\s+able|unable)|do\s*not|don'?t)\s+(?:find|ground|answer|verify|confirm|provide|have|know|see|locate|support)|i\s+(?:do\s*not|don'?t)\s+know|there\s+(?:is|are)\s+no\s+(?:information|evidence|mention|source|sources|relevant)|no\s+(?:relevant\s+)?(?:information|sources?|evidence)\s+(?:was|were|is|are)?\s*(?:found|available|retrieved)|the\s+(?:retrieved\s+|available\s+)?(?:sources|documents|material|context)\s+(?:do\s*not|don'?t|does\s*not|doesn'?t|did\s*not|didn'?t)\s+(?:contain|mention|cover|include|say|state|provide|address))", RegexOptions.IgnoreCase)]
    private static partial Regex Refusal();

    [GeneratedRegex(@"\b(?:but|however|although|though|while|whereas|yet)\b|;", RegexOptions.IgnoreCase)]
    private static partial Regex Contrast();
}
