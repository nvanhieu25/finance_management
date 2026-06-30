using System.Globalization;

namespace ConstructionFinance.Desktop.Common;

/// Pure parsers for Excel-style cell text pasted/typed into the entry grid.
/// Vietnamese convention: '.' groups thousands, ',' is the decimal separator.
public static class FinanceCellParsing
{
    public static decimal ParseMoney(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0m;
        var cleaned = raw.Trim().Replace(" ", "").Replace(".", "").Replace(",", ".");
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0m;
    }

    private static readonly string[] DateFormats = { "dd/MM/yyyy", "yyyy-MM-dd" };

    public static DateOnly? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return DateOnly.TryParseExact(raw.Trim(), DateFormats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)
            ? date
            : null;
    }
}
