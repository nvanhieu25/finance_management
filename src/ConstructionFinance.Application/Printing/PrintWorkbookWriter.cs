using ClosedXML.Excel;
using ConstructionFinance.Application.Forms;

namespace ConstructionFinance.Application.Printing;

/// Chân trang dùng chung cho cả workbook: bên trái là tên dự án (kèm tên Vị trí khi chỉ
/// in riêng một Vị trí), bên phải là số trang. ViTriName null nghĩa là không có phần đó.
public sealed record PrintFooterInfo(string ProjectName, string? ViTriName);

public interface IPrintWorkbookWriter
{
    /// Ghi workbook vào `target`. Nhận Stream để test ghi thẳng vào bộ nhớ, không đụng đĩa.
    void Write(IReadOnlyList<PrintSheet> sheets, Stream target, PrintFooterInfo footer);

    /// Ghi workbook ra tệp. Ném IOException khi tệp đích đang được chương trình khác mở.
    Task WriteAsync(
        IReadOnlyList<PrintSheet> sheets,
        string targetPath,
        PrintFooterInfo footer,
        CancellationToken ct = default);
}

/// Biến các PrintSheet thành một workbook .xlsx đã căn sẵn trang in. Nơi DUY NHẤT trong
/// tính năng In chạm ClosedXML — tầng Desktop chỉ biết model thuần trong PrintModels.cs.
public sealed class PrintWorkbookWriter : IPrintWorkbookWriter
{
    private const string DateFormat = "dd/MM/yyyy";

    /// Lề trên/dưới, tính bằng inch. Lề trái/phải do FormExcelExporter.ApplyPageDefaults đặt.
    private const double VerticalMargin = 0.6;

    /// Khoảng cách từ mép giấy đến chân trang. ClosedXML mặc định 0.75", lớn hơn lề dưới
    /// 0.6" — chân trang khi đó đè lên dòng cuối của mỗi trang. 0.3" là mặc định của Excel.
    private const double FooterMargin = 0.3;

    private const string FooterRight = "Trang &P/&N";

    /// Excel (và ClosedXML — ném ArgumentOutOfRangeException ngay khi lưu) giới hạn cả chuỗi
    /// chân trang ở 255 ký tự.
    private const int MaxFooterChars = 255;

    /// Mã phân vùng "&L" và "&R", mỗi mã hai ký tự.
    private const int SectionCodeChars = 2;

    /// Chân trang là "&L" + trái + "&R" + phải, nên phần dành cho tên bên trái là 255 trừ hai
    /// mã phân vùng trừ FooterRight. Dấu & trong tên phải viết đôi, nên tệ nhất (tên toàn dấu
    /// &) mỗi ký tự chiếm hai chỗ: chia đôi phần còn lại. Tính từ chính các hằng số dựng nên
    /// chuỗi, để sửa FooterRight không thể làm vỡ giới hạn — lỗi đó chỉ nổ lúc lưu và chỉ với
    /// tên dự án dài, rất khó bắt bằng test. `internal` để test lấy đúng mức trần này.
    internal static readonly int MaxFooterLeftChars =
        (MaxFooterChars - 2 * SectionCodeChars - FooterRight.Length) / 2;

    // Nét kẻ và màu nền của bảng, đặt cạnh nhau vì đây là chỗ hay phải chỉnh sau khi xem bản
    // in thật: mỗi thứ chỉ sửa đúng một dòng. Hai nét kẻ là `internal` để test đối chiếu với
    // tệp đọc lại mà không phải chép giá trị, nên đổi độ đậm không phải sửa test.
    internal const XLBorderStyleValues GridBorder = XLBorderStyleValues.Thin;
    internal const XLBorderStyleValues HeaderBottomBorder = XLBorderStyleValues.Medium;

    /// Xám nhạt của Excel ("White, Background 1, Darker 15%"), cho dòng IsShaded.
    private static readonly XLColor ShadeColor = XLColor.FromHtml("#D9D9D9");

    public void Write(IReadOnlyList<PrintSheet> sheets, Stream target, PrintFooterInfo footer)
    {
        using var workbook = new XLWorkbook();

        // Excel coi tên sheet là KHÔNG phân biệt hoa thường: ClosedXML ném ArgumentException
        // khi thêm "Abc" vào workbook đã có "abc". ExcelSheetName.Unique nhìn comparer của
        // tập này để biết hai tên có trùng nhau không, nên phải là OrdinalIgnoreCase.
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sheet in sheets)
        {
            var ws = workbook.AddWorksheet(ExcelSheetName.Unique(sheet.SheetName, usedNames));
            switch (sheet)
            {
                case PrintTableSheet table:
                    WriteTable(ws, table, footer);
                    break;
                case PrintFormSheet:
                    throw new NotSupportedException("PrintFormSheet chưa được hỗ trợ — xem Task 3.");
                default:
                    throw new NotSupportedException($"Không hỗ trợ loại sheet {sheet.GetType().Name}.");
            }
        }

        workbook.SaveAs(target);
    }

    public Task WriteAsync(
        IReadOnlyList<PrintSheet> sheets,
        string targetPath,
        PrintFooterInfo footer,
        CancellationToken ct = default) =>
        Task.Run(() =>
        {
            // Dựng xong cả workbook trong bộ nhớ rồi mới chạm đĩa: sheet lỗi thì không để lại
            // tệp rỗng/hỏng, và không đè mất bản in cũ cùng tên.
            using var buffer = new MemoryStream();
            Write(sheets, buffer, footer);
            // Task.Run chỉ xét ct trước khi chạy; hủy giữa chừng thì dừng ở đây, trước khi chạm đĩa.
            ct.ThrowIfCancellationRequested();
            File.WriteAllBytes(targetPath, buffer.ToArray());
        }, ct);

    /// Căn trang + chân trang theo hồ sơ. Một chỗ duy nhất cho mọi loại sheet, để bảng và
    /// biểu mẫu không có hai đường căn trang.
    private static void ApplyPage(IXLWorksheet ws, PrintPageProfile page, PrintFooterInfo footer)
    {
        // Font, khổ A4, hướng giấy, lề trái/phải — dùng lại đúng bộ đang áp cho ba biểu mẫu.
        FormExcelExporter.ApplyPageDefaults(ws, ToExcel(page.Orientation));

        var setup = ws.PageSetup;
        setup.Margins.Top = VerticalMargin;
        setup.Margins.Bottom = VerticalMargin;
        setup.Margins.Footer = FooterMargin;
        setup.CenterHorizontally = true;
        setup.FitToPages(page.FitToPagesWide, page.FitToPagesTall);

        // Gọi với 0 dòng/cột sẽ ném, nên 0 nghĩa là bỏ qua.
        if (page.RepeatRowsTop > 0) setup.SetRowsToRepeatAtTop(1, page.RepeatRowsTop);
        if (page.RepeatColsLeft > 0) setup.SetColumnsToRepeatAtLeft(1, page.RepeatColsLeft);

        setup.Footer.Left.AddText(FooterLeftText(footer));
        setup.Footer.Right.AddText(FooterRight);
    }

    /// "Tên dự án · Tên Vị trí". Excel hiểu & là mã điều khiển (&D = ngày, &P = số trang,
    /// &B = đậm…) nên & trong tên phải viết đôi — ClosedXML không tự làm việc đó.
    private static string FooterLeftText(PrintFooterInfo footer)
    {
        var parts = new[] { footer.ProjectName, footer.ViTriName }
            .Where(part => !string.IsNullOrWhiteSpace(part));
        var text = string.Join(" · ", parts);

        if (text.Length > MaxFooterLeftChars)
        {
            // Cắt theo đơn vị UTF-16, chừa một chỗ cho "…". Nếu dao rơi giữa một cặp surrogate
            // thì lùi một ký tự: surrogate lẻ không phải XML hợp lệ, ClosedXML ném khi lưu.
            var cut = MaxFooterLeftChars - 1;
            if (char.IsHighSurrogate(text[cut - 1])) cut--;
            text = text[..cut] + "…";
        }
        return text.Replace("&", "&&");
    }

    private static void WriteTable(IXLWorksheet ws, PrintTableSheet table, PrintFooterInfo footer)
    {
        ApplyPage(ws, table.Page, footer);

        WriteHeader(ws, table.Columns);
        for (var r = 0; r < table.Rows.Count; r++)
            WriteRow(ws, table, r);

        DrawGrid(ws, lastRow: table.Rows.Count + 1, columnCount: table.Columns.Count);
    }

    /// Kẻ khung cả bảng (tiêu đề + dữ liệu). Excel mặc định không in đường lưới, nên không kẻ
    /// thì bản in chỉ là chữ rời rạc. Cạnh dưới tiêu đề kẻ đậm hơn để tách phần đầu bảng.
    private static void DrawGrid(IXLWorksheet ws, int lastRow, int columnCount)
    {
        var grid = ws.Range(1, 1, lastRow, columnCount).Style.Border;
        grid.OutsideBorder = GridBorder;
        grid.InsideBorder = GridBorder;

        // Cạnh này là của cả hai ô liền kề (đáy tiêu đề, đỉnh dòng dữ liệu đầu). ClosedXML không
        // tự đồng bộ hai bên, nên ghi cả hai để Excel không phải chọn giữa hai nét khác nhau.
        ws.Range(1, 1, 1, columnCount).Style.Border.BottomBorder = HeaderBottomBorder;
        if (lastRow > 1)
            ws.Range(2, 1, 2, columnCount).Style.Border.TopBorder = HeaderBottomBorder;
    }

    private static void WriteHeader(IXLWorksheet ws, IReadOnlyList<PrintColumn> columns)
    {
        for (var c = 0; c < columns.Count; c++)
        {
            ws.Column(c + 1).Width = columns[c].WidthChars;
            ws.Cell(1, c + 1).Value = columns[c].Header;
        }

        // Tiêu đề căn giữa và tự xuống dòng: cột hẹp mà tên dài ("Tạm ứng chưa thu hồi") sẽ
        // bị cụt chữ nếu không xuống dòng.
        var style = ws.Range(1, 1, 1, columns.Count).Style;
        style.Font.Bold = true;
        style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        style.Alignment.WrapText = true;
    }

    private static void WriteRow(IXLWorksheet ws, PrintTableSheet table, int index)
    {
        var row = table.Rows[index];
        var columns = table.Columns;
        if (row.Cells.Count > columns.Count)
            throw new ArgumentException(
                $"Sheet \"{table.SheetName}\": Rows[{index}] có {row.Cells.Count} ô nhưng bảng chỉ có {columns.Count} cột.");

        var sheetRow = index + 2;   // dòng 1 là tiêu đề cột

        // Đậm và nền đặt cho cả dải cột của bảng, không chỉ ô có chữ — để dải nền liền nhau.
        var rowStyle = ws.Range(sheetRow, 1, sheetRow, columns.Count).Style;
        if (row.IsBold) rowStyle.Font.Bold = true;
        if (row.IsShaded) rowStyle.Fill.BackgroundColor = ShadeColor;

        // Thụt bậc đặt lên ô Text đầu tiên có chữ, không phải cả dòng: ô Ngày/Tiền ở trước
        // đó không có gì để thụt. Excel chỉ thụt được ô căn trái/phải — ô căn giữa mà bậc > 0
        // thì ClosedXML ném ArgumentException; đó là lỗi của bên dựng PrintRow, không che đi.
        var indentAt = IndexOfFirstText(row.Cells);
        for (var c = 0; c < row.Cells.Count; c++)
        {
            var cell = ws.Cell(sheetRow, c + 1);
            WriteValue(cell, row.Cells[c]);
            cell.Style.Alignment.Horizontal = ToExcel(columns[c].Align);
            if (c == indentAt) cell.Style.Alignment.Indent = row.IndentLevel;
        }
    }

    private static void WriteValue(IXLCell cell, PrintCell value)
    {
        switch (value.Kind)
        {
            case PrintCellKind.Text:
                if (string.IsNullOrEmpty(value.Text)) break;   // ô trống: không ghi gì
                cell.Value = value.Text;
                // Chữ dài thì phải xuống dòng, và ô như "Nhà thầu / TK" ghép hai dòng bằng "\n"
                // — Excel không hiện chỗ ngắt dòng nếu không bật WrapText.
                cell.Style.Alignment.WrapText = true;
                break;
            case PrintCellKind.Money:
                FormExcelExporter.SetMoney(cell, value.Number!.Value);
                break;
            case PrintCellKind.Number:
                // Số không phải tiền (năm, STT): để định dạng General, không có dấu phân nghìn
                // — nếu không năm 2026 sẽ in thành "2,026".
                cell.Value = value.Number!.Value;
                break;
            case PrintCellKind.Date:
                cell.Value = value.Date!.Value.ToDateTime(TimeOnly.MinValue);
                cell.Style.DateFormat.Format = DateFormat;
                break;
        }
    }

    private static int IndexOfFirstText(IReadOnlyList<PrintCell> cells)
    {
        for (var i = 0; i < cells.Count; i++)
            if (cells[i].Kind == PrintCellKind.Text && !string.IsNullOrEmpty(cells[i].Text))
                return i;
        return -1;
    }

    // Model in cố tình không dùng kiểu ClosedXML (tầng Desktop không được thấy chúng), nên
    // việc đổi sang kiểu của Excel nằm ở đây.
    private static XLPageOrientation ToExcel(PrintOrientation orientation) => orientation switch
    {
        PrintOrientation.Portrait => XLPageOrientation.Portrait,
        PrintOrientation.Landscape => XLPageOrientation.Landscape,
        _ => throw new ArgumentOutOfRangeException(nameof(orientation), orientation, null),
    };

    private static XLAlignmentHorizontalValues ToExcel(PrintAlign align) => align switch
    {
        PrintAlign.Left => XLAlignmentHorizontalValues.Left,
        PrintAlign.Center => XLAlignmentHorizontalValues.Center,
        PrintAlign.Right => XLAlignmentHorizontalValues.Right,
        _ => throw new ArgumentOutOfRangeException(nameof(align), align, null),
    };
}
