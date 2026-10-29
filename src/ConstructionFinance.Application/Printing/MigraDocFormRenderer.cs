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
        var contentMm = sheet.RowHeights.Take(UsedRowCount(sheet)).Sum() / PointsPerMillimeter;
        if (contentMm <= 0) return scale;

        var onePage = ExcelFitScale.UsableHeightMm(page.Orientation) * VerticalSafety / contentMm;
        // Ép một trang; hoặc tờ nhiều trang được (Thông tri) mà chỉ tràn NHẸ: co nhẹ cho vừa một tờ — tờ để ký mà sang trang
        // hai chỉ vì vài hàng thì phí giấy và khó ký. Tràn nhiều thì cứ nhiều trang, đừng co tới mức không đọc nổi.
        if (page.FitToPagesTall == 1 || (page.FitToPagesTall == 0 && onePage >= scale * MinOverflowShrink))
            scale = Math.Min(scale, onePage);
        return scale;
    }

    /// Co tối đa bao nhiêu để kéo một tờ tràn nhẹ về một trang.
    private const double MinOverflowShrink = 0.85;

    /// Số hàng cần in: tới hàng cuối cùng có chữ, ô nhập hoặc đường kẻ. Mẫu để trống nhiều hàng cuối (chỗ ký, ghi chú…) —
    /// Excel không in phần trống ở đáy, nhưng MigraDoc dựng đủ mọi hàng và hàng trống đẩy tờ sang một trang TRẮNG
    /// (đo được: Thông tri ra 2 trang, trang 2 trắng).
    internal static int UsedRowCount(ResolvedSheet sheet)
    {
        var last = 0;
        foreach (var cell in sheet.Cells)
            if (cell.DisplayText.Length > 0 || cell.IsEditable || cell.Style.Borders.Any)
                last = Math.Max(last, cell.Row + cell.RowSpan - 1);
        return Math.Min(Math.Max(last, 1), sheet.RowHeights.Count);
    }

    /// Hàng cuối của thân bảng: hàng cuối có ô kẻ cạnh trái hoặc phải. 0 nếu tờ không có bảng.
    internal static int LastBodyRow(ResolvedSheet sheet) =>
        sheet.Cells
            .Where(cell => cell.Style.Borders.Left != BorderLine.None || cell.Style.Borders.Right != BorderLine.None)
            .Select(cell => cell.Row + cell.RowSpan - 1)
            .DefaultIfEmpty(0)
            .Max();

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

        foreach (var height in sheet.RowHeights.Take(UsedRowCount(sheet)))
        {
            var row = table.AddRow();
            // AtLeast: Excel cắt chữ ở đáy ô, tờ in thì để hàng nở ra — mất chữ trên giấy ký là lỗi nặng hơn tờ dài thêm vài mm.
            row.Height = Unit.FromPoint(height * scale);
            row.HeightRule = RowHeightRule.AtLeast;
        }

        // Phần dưới bảng (Bằng chữ, ngày, chức danh, họ tên người ký) đi liền MỘT khối với hàng cuối của bảng: Thông tri dài
        // sang trang hai thì cả khối sang cùng, không để họ tên người ký lẻ loi một mình đầu trang sau (thấy khi in thử 2026-10-29).
        var lastBodyRow = LastBodyRow(sheet);
        var usedRowCount = UsedRowCount(sheet);
        if (lastBodyRow > 0 && lastBodyRow < usedRowCount)
            table.Rows[lastBodyRow - 1]!.KeepWith = usedRowCount - lastBodyRow;

        var byAnchor = sheet.Cells.ToDictionary(cell => (cell.Row, cell.Col));
        var covered = new HashSet<(int Row, int Col)>();
        foreach (var cell in sheet.Cells)
        {
            var span = SpanOf(cell);
            for (var col = cell.Col; col < cell.Col + span; col++)
            for (var row = cell.Row; row < cell.Row + cell.RowSpan; row++)
                if ((row, col) != (cell.Row, cell.Col)) covered.Add((row, col));
        }

        var usedRows = UsedRowCount(sheet);
        foreach (var cell in sheet.Cells)
        {
            if (cell.Row > usedRows) continue;      // hàng trống cuối tờ không dựng
            // Ô nằm dưới vùng gộp/tràn của ô khác: MigraDoc bỏ qua nó khi dàn trang, đừng đụng vào.
            if (covered.Contains((cell.Row, cell.Col))) continue;
            WriteCell(table, sheet, cell, byAnchor, scale);
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
        Table table, ResolvedSheet sheet, ResolvedCell cell, Dictionary<(int Row, int Col), ResolvedCell> byAnchor, double scale)
    {
        var target = table.Rows[cell.Row - 1]!.Cells[cell.Col - 1]!;
        var span = SpanOf(cell);
        if (span > 1) target.MergeRight = span - 1;
        var rowSpan = Math.Min(cell.RowSpan, UsedRowCount(sheet) - cell.Row + 1);   // không gộp xuống hàng đã bỏ
        if (rowSpan > 1) target.MergeDown = rowSpan - 1;

        // Cạnh phải của dải tràn là cạnh phải của ô cuối dải, không phải của ô đầu.
        var right = cell.Style.Borders.Right;
        if (span > cell.ColSpan && byAnchor.TryGetValue((cell.Row, cell.Col + span - 1), out var last))
            right = last.Style.Borders.Right;
        ApplyBorders(target, cell.Style.Borders with { Right = right });
        // MigraDoc vẽ cạnh phải của ô gộp theo ô CUỐI của dải, không theo ô đầu: đặt cả ở ô cuối.
        if (span > 1 && right != BorderLine.None)
            ApplyBorders(table.Rows[cell.Row - 1]!.Cells[cell.Col + span - 2]!,
                new CellBorders(BorderLine.None, right, BorderLine.None, BorderLine.None));

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

        var text = PreserveSpaces(cell.DisplayText);
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

    /// MigraDoc gộp mọi dãy dấu cách thành MỘT và bỏ dấu cách đầu dòng — nhưng Mẫu dùng chính dãy dấu cách để chừa chỗ viết tay
    /// ("Ngày        tháng    năm 2026") và để thụt đầu dòng (" + Mục…", "  Các năm trước"). Đổi dãy từ hai dấu cách trở lên và
    /// dấu cách đầu chuỗi sang dấu cách không ngắt (U+00A0) để bản in giữ nguyên chỗ trống.
    internal static string PreserveSpaces(string text)
    {
        if (text.Length == 0) return text;
        var builder = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != ' ') { builder.Append(text[i]); continue; }
            var end = i;
            while (end < text.Length && text[end] == ' ') end++;
            var run = end - i;
            builder.Append(run >= 2 || i == 0 ? new string('\u00A0', run) : " ");
            i = end - 1;
        }
        return builder.ToString();
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
