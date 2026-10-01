using System.Globalization;
using System.Text;

namespace Quayside.Api.Tools;

public static class ResultText
{
    public const string SyntheticLabel = "Synthetic demo data, not live MSC systems.";

    public static string Table(IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        var text = new StringBuilder();
        text.AppendLine(string.Join(" | ", columns));
        foreach (var row in rows)
        {
            text.AppendLine(string.Join(" | ", row.Select(Cell)));
        }

        return text.ToString().TrimEnd();
    }

    public static string Cell(object? value) => value switch
    {
        null or DBNull => "NULL",
        DateTime moment => moment.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        DateTimeOffset moment => moment.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };
}
