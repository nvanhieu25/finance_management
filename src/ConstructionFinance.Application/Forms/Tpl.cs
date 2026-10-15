namespace ConstructionFinance.Application.Forms;

/// Cú pháp rút gọn cho các tệp bản gốc (ThongTriTemplate…). Chỉ để các tệp đó đọc được cạnh Mẫu.xlsx.
internal static class Tpl
{
    public static CellStyle Style(
        double size, bool bold, bool italic, bool underline, CellAlign align, CellVAlign v, bool wrap,
        string borders, string? format) =>
        new(size, bold, italic, underline, align, v, wrap, CellBorders.Parse(borders), format);

    public static TemplateCell Cell(int row, int col, CellStyle style, params TemplateSegment[] segments) =>
        new(row, col, 1, 1, style, segments);

    public static TemplateCell Merged(int row, int col, int rows, int cols, CellStyle style,
        params TemplateSegment[] segments) =>
        new(row, col, rows, cols, style, segments);

    public static TemplateSegment Fixed(string text) => new(SegmentKind.Fixed, text);

    public static TemplateSegment Input(string key, string placeholder, string? @default = null, string? hint = null) =>
        new(SegmentKind.Input, placeholder, key, @default, hint);

    public static TemplateSegment Number(string key) => new(SegmentKind.InputNumber, "", key);

    public static TemplateSegment Setting(string key, string placeholder) =>
        new(SegmentKind.SettingInput, placeholder, key);

    public static TemplateSegment Computed(string key) => new(SegmentKind.Computed, "", key);

    public static TemplateSegment Over(string key, string? hint = null) =>
        new(SegmentKind.Overridable, "", key, null, hint);
}
