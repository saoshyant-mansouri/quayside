using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Quayside.Core.Web;

public static partial class WebResultSanitizer
{
    public const int MaxTitleLength = 160;
    public const int MaxSnippetLength = 500;
    public const int MaxUrlLength = 500;
    public const int MaxLabelLength = 40;

    public static IReadOnlyList<WebResult> Clean(IEnumerable<WebResult> results, int maxResults)
    {
        var cleaned = new List<WebResult>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var result in results)
        {
            if (cleaned.Count >= Math.Max(maxResults, 0))
            {
                break;
            }

            var url = Url(result.Url);
            var snippet = Text(result.Snippet, MaxSnippetLength);
            if (url is null || snippet.Length == 0 || !seen.Add(url.AbsoluteUri))
            {
                continue;
            }

            var title = Text(result.Title, MaxTitleLength);
            var label = Text(result.PublishedLabel, MaxLabelLength);
            cleaned.Add(new WebResult(
                title.Length == 0 ? url.Host : title,
                url.AbsoluteUri,
                snippet,
                label.Length == 0 ? null : label));
        }

        return cleaned;
    }

    public static string Text(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var stripped = TagPattern().Replace(WebUtility.HtmlDecode(value), " ");
        var plain = new StringBuilder(stripped.Length);
        foreach (var character in stripped)
        {
            plain.Append(Replacement(character));
        }

        var collapsed = WhitespacePattern().Replace(plain.ToString(), " ").Trim();
        return Truncate(collapsed, maxLength);
    }

    public static Uri? Url(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxUrlLength)
        {
            return null;
        }

        var parsed = Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri);
        var allowed = parsed
            && uri!.Scheme is "http" or "https"
            && uri.Host.Length > 0
            && uri.UserInfo.Length == 0
            && uri.AbsoluteUri.Length <= MaxUrlLength;
        return allowed ? uri : null;
    }

    private static char Replacement(char character) => character switch
    {
        '[' => '(',
        ']' => ')',
        _ when char.IsWhiteSpace(character) => ' ',
        _ when char.IsControl(character) => ' ',
        _ when CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.Format => ' ',
        _ => character,
    };

    private static string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        var cut = text.LastIndexOf(' ', maxLength - 3);
        var end = cut > maxLength / 2 ? cut : maxLength - 3;
        return text[..end].TrimEnd() + "...";
    }

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
