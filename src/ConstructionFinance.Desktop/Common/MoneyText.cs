using System.Globalization;

namespace ConstructionFinance.Desktop.Common;

/// Formats and parses money (đồng) with Vietnamese "." thousand-grouping convention.
/// Display: 10000000 → "10.000.000", 0 → "" (blank, Excel-like).
/// Parse: "10.000.000" → 10000000, strips "." and spaces, empty → 0m.
public static class MoneyText
{
    /// Format decimal value as Vietnamese thousand-grouped string (no decimals).
    /// Zero returns empty string (Excel-like).
    /// Negative values keep a leading "-".
    public static string Format(decimal value)
    {
        if (value == 0m)
            return "";

        var numberFormat = new NumberFormatInfo
        {
            NumberGroupSeparator = ".",
            NumberDecimalDigits = 0,
            NumberGroupSizes = new[] { 3 }
        };

        return value.ToString("N", numberFormat);
    }

    /// Try to parse a string into a decimal, handling Vietnamese "." thousand grouping.
    /// Null, empty, or whitespace-only strings return 0m with true.
    /// Strips all "." and spaces before parsing.
    /// Invalid input returns false with value=0m.
    public static bool TryParse(string? s, out decimal value)
    {
        value = 0m;

        if (string.IsNullOrWhiteSpace(s))
            return true;

        var cleaned = s.Trim().Replace(" ", "").Replace(".", "");

        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
