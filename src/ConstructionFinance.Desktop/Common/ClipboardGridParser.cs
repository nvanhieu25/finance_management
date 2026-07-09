namespace ConstructionFinance.Desktop.Common;

/// Parses a clipboard block copied from Excel/Sheets (TSV) into rows of cells.
public static class ClipboardGridParser
{
    public static string[][] Parse(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return Array.Empty<string[]>();

        var lines = raw.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        // Excel appends a trailing newline → drop a single empty final line.
        var count = lines.Length;
        if (count > 0 && lines[count - 1].Length == 0) count--;

        var rows = new string[count][];
        for (var i = 0; i < count; i++)
            rows[i] = lines[i].Split('\t');
        return rows;
    }
}
