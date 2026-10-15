using ClosedXML.Excel;
using ConstructionFinance.Application.Printing;

namespace ConstructionFinance.Application.Forms;

public partial class FormExcelExporter
{
    public Task ExportAsync(ResolvedSheet sheet, string targetPath, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            using var wb = new XLWorkbook();
            WriteSheet(wb.AddWorksheet(sheet.SheetName), sheet);
            wb.SaveAs(targetPath);
        }, ct);

    /// Vẽ một tờ đã trộn lên worksheet CÓ SẴN: bề rộng cột, chiều cao hàng, ô gộp, phông/cỡ chữ/đậm/nghiêng/
    /// gạch chân, canh lề, viền từng cạnh, định dạng số. Số tiền ghi là SỐ thật (Excel cộng được).
    internal static void WriteSheet(IXLWorksheet ws, ResolvedSheet sheet)
    {
        ApplyPaper(ws, sheet.Orientation == PrintOrientation.Landscape
            ? XLPageOrientation.Landscape : XLPageOrientation.Portrait);
        ws.Style.Font.FontName = FontName;
        ws.Style.Font.FontSize = FontSize;

        for (var col = 0; col < sheet.ColumnWidths.Count; col++)
            ws.Column(col + 1).Width = sheet.ColumnWidths[col];
        for (var row = 0; row < sheet.RowHeights.Count; row++)
            ws.Row(row + 1).Height = sheet.RowHeights[row];

        foreach (var cell in sheet.Cells)
        {
            var range = ws.Range(cell.Row, cell.Col, cell.Row + cell.RowSpan - 1, cell.Col + cell.ColSpan - 1);
            if (cell.RowSpan > 1 || cell.ColSpan > 1) range.Merge();

            ApplyStyle(range, cell.Style);
            WriteValue(ws.Cell(cell.Row, cell.Col), cell);
        }
    }

    private static void ApplyStyle(IXLRange range, CellStyle style)
    {
        var font = range.Style.Font;
        font.FontName = FontName;
        font.FontSize = style.FontSize;
        font.Bold = style.Bold;
        font.Italic = style.Italic;
        font.Underline = style.Underline ? XLFontUnderlineValues.Single : XLFontUnderlineValues.None;

        range.Style.Alignment.Horizontal = style.Align switch
        {
            CellAlign.Left => XLAlignmentHorizontalValues.Left,
            CellAlign.Center => XLAlignmentHorizontalValues.Center,
            CellAlign.Right => XLAlignmentHorizontalValues.Right,
            CellAlign.Justify => XLAlignmentHorizontalValues.Justify,
            _ => XLAlignmentHorizontalValues.General,
        };
        range.Style.Alignment.Vertical = style.VAlign switch
        {
            CellVAlign.Center => XLAlignmentVerticalValues.Center,
            CellVAlign.Top => XLAlignmentVerticalValues.Top,
            _ => XLAlignmentVerticalValues.Bottom,
        };
        range.Style.Alignment.WrapText = style.Wrap;

        // Viền: chỉ cạnh NGOÀI của vùng (ô gộp cũng là một vùng). Đặt trên cả vùng sẽ kẻ mọi cạnh trong.
        var borders = style.Borders;
        if (borders.Left != BorderLine.None) range.FirstColumn().Style.Border.LeftBorder = ToExcel(borders.Left);
        if (borders.Right != BorderLine.None) range.LastColumn().Style.Border.RightBorder = ToExcel(borders.Right);
        if (borders.Top != BorderLine.None) range.FirstRow().Style.Border.TopBorder = ToExcel(borders.Top);
        if (borders.Bottom != BorderLine.None) range.LastRow().Style.Border.BottomBorder = ToExcel(borders.Bottom);
    }

    private static XLBorderStyleValues ToExcel(BorderLine line) =>
        line == BorderLine.Hair ? XLBorderStyleValues.Hair : XLBorderStyleValues.Thin;

    private static void WriteValue(IXLCell target, ResolvedCell cell)
    {
        // Một đoạn số duy nhất → ô số thật (cộng được, mang định dạng của Mẫu).
        if (cell.Segments.Count == 1 && cell.Segments[0].Number is { } number)
        {
            target.Value = number;
            target.Style.NumberFormat.Format = cell.Style.NumberFormat ?? FormNumberFormat.Default;
            return;
        }

        var text = cell.DisplayText;
        if (text.Length == 0) return;

        // Chữ người dùng gõ có thể bắt đầu bằng "=" hay "'": gán thẳng một XLCellValue chữ (không qua
        // trình phân tích công thức) và đặt định dạng Text để Excel cũng không coi là công thức khi mở lại.
        target.Style.NumberFormat.Format = "@";
        target.Value = XLCellValue.FromObject(text);
    }
}
