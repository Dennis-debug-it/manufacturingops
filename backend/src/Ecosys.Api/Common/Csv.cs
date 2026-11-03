using System.Text;

namespace Ecosys.Api.Common;

/// <summary>
/// Helpers for emitting safe CSV cells. Neutralizes CSV/formula injection (a leading
/// <c>= + - @</c>, tab, or CR is prefixed with a <c>'</c> so spreadsheet apps treat the cell as
/// text) and doubles embedded quotes per RFC 4180.
/// </summary>
public static class Csv
{
    /// <summary>Escapes a value for placement inside a quoted CSV field (no surrounding quotes).</summary>
    public static string Escape(object? value)
    {
        var raw = value?.ToString() ?? string.Empty;
        if (raw.Length > 0 && "=+-@\t\r".IndexOf(raw[0]) >= 0)
        {
            raw = "'" + raw;
        }

        return raw.Replace("\"", "\"\"");
    }

    /// <summary>Returns a fully-quoted, escaped CSV field: <c>"..."</c>.</summary>
    public static string Field(object? value) => string.Concat("\"", Escape(value), "\"");

    /// <summary>Joins values into one quoted-and-escaped CSV row (no trailing newline).</summary>
    public static string Row(IEnumerable<object?> values) => string.Join(",", values.Select(Field));
}
