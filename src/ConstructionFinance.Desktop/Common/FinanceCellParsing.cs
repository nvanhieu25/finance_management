using System.Globalization;

namespace ConstructionFinance.Desktop.Common;

/// Pure parsers for cell text typed/pasted into the Vị trí ledger grid. (Tiền: xem MoneyText.)
public static class FinanceCellParsing
{
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
