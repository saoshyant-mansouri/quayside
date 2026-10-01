using System.Globalization;
using System.Text.RegularExpressions;

namespace Quayside.Api.Tools;

public static partial class SqlLiteral
{
    public static string ContainerNumber(string value)
    {
        var normalised = Strip(value).ToUpperInvariant();
        if (!ContainerPattern().IsMatch(normalised))
        {
            throw new InvalidToolInputException("A container number is four letters followed by seven digits, for example MSCU1234567.");
        }

        return $"'{normalised}'";
    }

    public static string Imo(string value)
    {
        var normalised = Strip(value);
        if (!ImoPattern().IsMatch(normalised))
        {
            throw new InvalidToolInputException("An IMO number is seven digits.");
        }

        return $"'{normalised}'";
    }

    public static string Locode(string value)
    {
        var normalised = Strip(value).ToUpperInvariant();
        if (!LocodePattern().IsMatch(normalised))
        {
            throw new InvalidToolInputException("A UN/LOCODE is five letters, for example NLRTM.");
        }

        return $"'{normalised}'";
    }

    public static string NameContains(string value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (!NamePattern().IsMatch(trimmed))
        {
            throw new InvalidToolInputException("A name may contain letters, digits, spaces, apostrophes, hyphens and full stops, up to 60 characters.");
        }

        return $"N'%{trimmed.Replace("'", "''", StringComparison.Ordinal)}%'";
    }

    public static string Date(DateOnly value) => $"'{value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}'";

    public static bool LooksLikeLocode(string value) => LocodePattern().IsMatch(Strip(value).ToUpperInvariant());

    public static bool LooksLikeImo(string value) => ImoPattern().IsMatch(Strip(value));

    private static string Strip(string value) => (value ?? string.Empty).Trim().Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);

    [GeneratedRegex("^[A-Z]{4}[0-9]{7}$")]
    private static partial Regex ContainerPattern();

    [GeneratedRegex("^[0-9]{7}$")]
    private static partial Regex ImoPattern();

    [GeneratedRegex("^[A-Z]{5}$")]
    private static partial Regex LocodePattern();

    [GeneratedRegex(@"^[\p{L}\p{N} .'\-]{2,60}$")]
    private static partial Regex NamePattern();
}
