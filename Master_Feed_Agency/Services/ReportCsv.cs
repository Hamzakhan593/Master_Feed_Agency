using System.Text;

namespace Master_Feed_Agency.Services;

public static class ReportCsv
{
    public static byte[] Build(IEnumerable<IEnumerable<object?>> rows)
    {
        var sb = new StringBuilder();
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(',', row.Select(Escape)));
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(sb.ToString());
    }

    private static string Escape(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            DateTime dt => BusinessTime.ToLocal(dt).ToString("yyyy-MM-dd HH:mm:ss"),
            decimal number => number.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
        };

        return $"\"{text.Replace("\"", "\"\"")}\"";
    }
}
