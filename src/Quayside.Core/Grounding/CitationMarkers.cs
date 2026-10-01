using System.Globalization;
using System.Text.RegularExpressions;

namespace Quayside.Core.Grounding;

internal static partial class CitationMarkers
{
    [GeneratedRegex(@"\[([1-9][0-9]{0,8})\](?!\()")]
    public static partial Regex Pattern();

    public static IEnumerable<int> In(string sentence)
    {
        foreach (Match match in Pattern().Matches(sentence))
        {
            yield return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }
    }
}
