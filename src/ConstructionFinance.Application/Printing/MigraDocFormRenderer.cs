using ConstructionFinance.Application.Forms;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace ConstructionFinance.Application.Printing;

/// Vẽ một tờ biểu mẫu đã trộn ra MigraDoc. Chỉ đọc ResolvedSheet — không biết gì về Mẫu hay builder.
/// Thay MigraDocFormSheets + FormGrid (ba bản chép tay) bằng MỘT bộ vẽ chung.
internal static class MigraDocFormRenderer
{
    private const double ThinPoints = 0.5;
    private const double HairPoints = 0.25;
    private const double CellPaddingPoints = 1.5;
    private const double PointsPerMillimeter = 72.0 / 25.4;

    /// Tỉ lệ co chung cho bề rộng cột, cỡ chữ và chiều cao hàng. Luôn co theo bề ngang giấy (như Excel
    /// FitToPages(1 ngang)); hồ sơ ép MỘT trang theo chiều dọc (`FitToPagesTall == 1`) thì co thêm cho vừa chiều cao.
    /// Không bao giờ phóng to (trần 1,0 — 13pt là cỡ của biểu mẫu).
    public static double ScaleOf(ResolvedSheet sheet, PrintPageProfile page)
    {
        var scale = ExcelFitScale.ScaleFor(sheet.ColumnWidths, page.Orientation);
        if (page.FitToPagesTall == 1)
        {
            var contentMm = sheet.RowHeights.Sum() / PointsPerMillimeter;
            if (contentMm > 0)
                scale = Math.Min(scale, ExcelFitScale.UsableHeightMm(page.Orientation) * VerticalSafety / contentMm);
        }
        return scale;
    }

    /// Chừa một chút: MigraDoc cộng thêm viền và làm tròn từng hàng, nên vừa khít là tràn sang trang hai.
    private const double VerticalSafety = 0.92;

    public static Table Write(Section section, ResolvedSheet sheet, PrintPageProfile page)
    {
        var scale = ScaleOf(sheet, page);

        var table = section.AddTable();
        table.Format.Font.Size = Unit.FromPoint(Forms.FormExcelExporter.FontSize * scale);
        table.LeftPadding = Unit.FromPoint(CellPaddingPoints * scale);
        table.RightPadding = Unit.FromPoint(CellPaddingPoints * scale);
        table.TopPadding = Unit.FromPoint(0);
        table.BottomPadding = Unit.FromPoint(0);

        foreach (var width in sheet.ColumnWidths)
            table.AddColumn(Unit.FromMillimeter(ExcelFitScale.ContentWidthMm(new[] { width }) * scale));

        foreach (var height in sheet.RowHeights)
        {
            var row = table.AddRow();
            // AtLeast: Excel cắt chữ ở đáy ô, tờ in thì để hàng nở ra — mất chữ trên giấy ký là lỗi nặng hơn tờ dài thêm vài mm.
            row.Height = Unit.FromPoint(height * scale);
            row.HeightRule = RowHeightRule.AtLeast;
        }

        var byAnchor = sheet.Cells.ToDictionary(cell => (cell.Row, cell.Col));
        var covered = new HashSet<(int Row, int Col)>();
        foreach (var cell in sheet.Cells)
        {
            var span = SpanOf(cell);
            for (var col = cell.Col; col < cell.Col + span; col++)
            for (var row = cell.Row; row < cell.Row + cell.RowSpan; row++)
                if ((row, col) != (cell.Row, cell.Col)) covered.Add((row, col));
        }

        foreach (var cell in sheet.Cells)
        {
            // Ô nằm dưới vùng gộp/tràn của ô khác: MigraDoc bỏ qua nó khi dàn trang, đừng đụng vào.
            if (covered.Contains((cell.Row, cell.Col))) continue;
            WriteCell(table, cell, byAnchor, scale);
        }

        return table;
    }

    /// Số cột ô thực sự chiếm trên giấy: vùng gộp của Mẫu, hoặc — với ô chữ tràn — cả dải chữ tràn sang.
    /// Excel cho chữ tràn sang ô trống bên phải; MigraDoc thì xuống dòng trong lòng ô, nên phải gộp ô
    /// (đường kẻ DỌC nằm giữa dải bị mất — chấp nhận, đỡ hơn đường kẻ cắt ngang chữ).
    private static int SpanOf(ResolvedCell cell) =>
        cell.RowSpan == 1 && cell.ColSpan == 1 && cell.TextColSpan > 1 && cell.DisplayText.Length > 0
            ? cell.TextColSpan
            : cell.ColSpan;

    private static void WriteCell(
        Table table, ResolvedCell cell, Dictionary<(int Row, int Col), ResolvedCell> byAnchor, double scale)
    {
        var target = table.Rows[cell.Row - 1]!.Cells[cell.Col - 1]!;
        var span = SpanOf(cell);
        if (span > 1) target.MergeRight = span - 1;
        if (cell.RowSpan > 1) target.MergeDown = cell.RowSpan - 1;

        // Cạnh phải của dải tràn là cạnh phải của ô cuối dải, không phải của ô đầu.
        var right = cell.Style.Borders.Right;
        if (span > cell.ColSpan && byAnchor.TryGetValue((cell.Row, cell.Col + span - 1), out var last))
            right = last.Style.Borders.Right;
        ApplyBorders(target, cell.Style.Borders with { Right = right });

        var style = cell.Style;
        target.Format.Font.Size = Unit.FromPoint(style.FontSize * scale);
        if (style.Bold) target.Format.Font.Bold = true;
        if (style.Italic) target.Format.Font.Italic = true;
        if (style.Underline) target.Format.Font.Underline = Underline.Single;
        target.VerticalAlignment = style.VAlign switch
        {
            CellVAlign.Center => VerticalAlignment.Center,
            CellVAlign.Top => VerticalAlignment.Top,
            _ => VerticalAlignment.Bottom,
        };

        var text = cell.DisplayText;
        if (text.Length == 0) return;

        var paragraph = target.AddParagraph(text);
        var numeric = cell.Segments.Any(segment => segment.Number is not null);
        paragraph.Format.Alignment = style.Align switch
        {
            CellAlign.Center => ParagraphAlignment.Center,
            CellAlign.Right => ParagraphAlignment.Right,
            CellAlign.Justify => ParagraphAlignment.Justify,
            CellAlign.Left => ParagraphAlignment.Left,
            _ => numeric ? ParagraphAlignment.Right : ParagraphAlignment.Left,   // Excel canh phải ô số theo mặc định
        };
    }

    private static void ApplyBorders(Cell target, CellBorders borders)
    {
        Set(target.Borders.Left, borders.Left);
        Set(target.Borders.Right, borders.Right);
        Set(target.Borders.Top, borders.Top);
        Set(target.Borders.Bottom, borders.Bottom);

        static void Set(Border border, BorderLine line)
        {
            if (line == BorderLine.None) return;
            border.Width = Unit.FromPoint(line == BorderLine.Hair ? HairPoints : ThinPoints);
        }
    }
}
