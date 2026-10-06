using ConstructionFinance.Application.Forms;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace ConstructionFinance.Application.Printing;

public interface IMigraDocBuilder
{
    /// Dựng MỘT tài liệu cho cả danh sách sheet: "In toàn bộ" là một lệnh in, một hộp thoại.
    Document Build(IReadOnlyList<PrintSheet> sheets, PrintFooterInfo footer);
}

/// Biến các PrintSheet thành một MigraDoc Document đã dàn trang. Nơi DUY NHẤT của tính năng
/// in-ra-giấy chạm MigraDoc; tầng Desktop chỉ thấy IMigraDocBuilder và model thuần.
///
/// Song song với PrintWorkbookWriter (đường .xlsx) và cố ý giống nó về cấu trúc: cùng model
/// vào, cùng hai bậc xám, cùng định dạng ngày/tiền. Hai bộ dựng phải cho ra cùng NỘI DUNG —
/// SheetTextMapTests so từng ô để bắt lúc chúng trôi khỏi nhau.
public sealed class MigraDocBuilder : IMigraDocBuilder
{
    private const string DateFormat = "dd/MM/yyyy";

    /// Hai bậc xám, đúng hai giá trị PrintWorkbookWriter đang dùng cho đường .xlsx.
    private static readonly Color LightShadeColor = Color.Parse("#D9D9D9");
    private static readonly Color VeryLightShadeColor = Color.Parse("#F2F2F2");

    /// Nét kẻ lưới, quy ra point. Excel "Thin" ≈ 0,5pt.
    private const double GridBorderPoints = 0.5;

    /// Cạnh dưới dòng tiêu đề kẻ đậm hơn để tách phần đầu bảng, như đường .xlsx ("Medium").
    private const double HeaderBorderPoints = 1.0;

    /// Thụt một bậc bằng bao nhiêu. Excel thụt theo "ký tự"; 3 đơn vị bề rộng cho một bậc là
    /// xấp xỉ gần nhất ở cỡ chữ đã co.
    private const double IndentUnitsPerLevel = 3.0;

    public Document Build(IReadOnlyList<PrintSheet> sheets, PrintFooterInfo footer)
    {
        var document = new Document();

        var normal = document.Styles["Normal"]
            ?? throw new InvalidOperationException("MigraDoc không có style \"Normal\".");
        normal.Font.Name = FormExcelExporter.FontName;
        normal.Font.Size = Unit.FromPoint(FormExcelExporter.FontSize);

        foreach (var sheet in sheets)
        {
            var section = document.AddSection();
            ApplyPage(section, sheet.Page, footer);

            switch (sheet)
            {
                case PrintTableSheet table:
                    WriteTable(section, table);
                    break;
                default:
                    throw new NotSupportedException(
                        $"Sheet \"{sheet.SheetName}\": chưa có hàm vẽ MigraDoc cho {sheet.GetType().Name}.");
            }
        }

        return document;
    }

    /// Khổ giấy, hướng giấy, lề và chân trang. Lề trái/phải lấy đúng con số của đường .xlsx,
    /// vì ExcelFitScale tính tỉ lệ co dựa trên chính nó.
    private static void ApplyPage(Section section, PrintPageProfile page, PrintFooterInfo footer)
    {
        var setup = section.PageSetup;
        setup.PageFormat = PageFormat.A4;
        setup.Orientation = page.Orientation == PrintOrientation.Landscape
            ? Orientation.Landscape
            : Orientation.Portrait;
        setup.LeftMargin = Unit.FromInch(ExcelFitScale.SideMarginInches);
        setup.RightMargin = Unit.FromInch(ExcelFitScale.SideMarginInches);
        setup.TopMargin = Unit.FromInch(0.6);
        setup.BottomMargin = Unit.FromInch(0.6);
        // Khoảng cách từ mép giấy đến chân trang. Mặc định của MigraDoc (1,25cm) cao hơn mép
        // trên của lề dưới 0,6" nên chân trang đè lên dòng cuối; 0,3" như đường .xlsx.
        setup.FooterDistance = Unit.FromInch(0.3);

        // Chân trang: tên dự án (kèm tên Vị trí) bên trái, số trang bên phải. KHÔNG phải nhân
        // đôi dấu & hay cắt ở 255 ký tự như đường .xlsx — đó là luật riêng của Excel, MigraDoc
        // nhận chuỗi thuần. Chuỗi dài chỉ bị cắt khi vẽ, không làm vỡ tài liệu.
        var paragraph = section.Footers.Primary.AddParagraph();
        // Tab dừng phải ở mép phải vùng chữ, để "Trang x/y" dạt sang phải như đường .xlsx.
        paragraph.Format.TabStops.AddTabStop(
            Unit.FromMillimeter(ExcelFitScale.UsableWidthMm(page.Orientation)), TabAlignment.Right);
        paragraph.AddText(FooterLeftText(footer));
        paragraph.AddTab();
        paragraph.AddText("Trang ");
        paragraph.AddPageField();
        paragraph.AddText("/");
        paragraph.AddNumPagesField();
    }

    private static string FooterLeftText(PrintFooterInfo footer) =>
        string.Join(" · ", new[] { footer.ProjectName, footer.ViTriName }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    private static void WriteTable(Section section, PrintTableSheet sheet)
    {
        // Bảng không có cột thì không có gì để vẽ: Table.AddRow() rồi Cells[0] sẽ ném. Để
        // Section rỗng — một trang trắng mang chân trang, chứ không phải một lần sập.
        if (sheet.Columns.Count == 0) return;

        var scale = ExcelFitScale.ScaleFor(sheet.Columns.Select(column => column.WidthChars), sheet.Page.Orientation);

        var table = section.AddTable();
        table.Format.Font.Size = Unit.FromPoint(FormExcelExporter.FontSize * scale);
        table.Borders.Width = Unit.FromPoint(GridBorderPoints);

        foreach (var column in sheet.Columns)
        {
            var added = table.AddColumn(Unit.FromMillimeter(ExcelFitScale.ContentWidthMm(new[] { column.WidthChars }) * scale));
            added.Format.Alignment = ToMigraDoc(column.Align);
        }

        WriteHeaderRow(table, sheet.Columns);

        for (var index = 0; index < sheet.Rows.Count; index++)
            WriteRow(table, sheet, index, scale);
    }

    private static void WriteHeaderRow(Table table, IReadOnlyList<PrintColumn> columns)
    {
        var row = table.AddRow();
        row.Format.Font.Bold = true;

        // HeadingFormat: MigraDoc lặp lại dòng này ở đầu MỖI trang. Đúng việc
        // PageSetup.SetRowsToRepeatAtTop làm cho đường .xlsx.
        row.HeadingFormat = true;
        row.Borders.Bottom.Width = Unit.FromPoint(HeaderBorderPoints);
        row.VerticalAlignment = VerticalAlignment.Center;

        for (var index = 0; index < columns.Count; index++)
        {
            var paragraph = row.Cells[index]!.AddParagraph(columns[index].Header);
            paragraph.Format.Alignment = ParagraphAlignment.Center;
        }
    }

    private static void WriteRow(Table table, PrintTableSheet sheet, int index, double scale)
    {
        var source = sheet.Rows[index];
        if (source.Cells.Count > sheet.Columns.Count)
            throw new ArgumentException(
                $"Sheet \"{sheet.SheetName}\": Rows[{index}] có {source.Cells.Count} ô nhưng bảng chỉ có {sheet.Columns.Count} cột.",
                nameof(sheet));

        var row = table.AddRow();
        if (source.IsBold) row.Format.Font.Bold = true;
        if (source.Shade != PrintShade.None) row.Shading.Color = ShadeColor(source.Shade);

        var indentAt = IndexOfFirstIndentableText(source.Cells, sheet.Columns);

        for (var column = 0; column < source.Cells.Count; column++)
        {
            var text = CellText(source.Cells[column]);
            if (text.Length == 0) continue;

            var paragraph = row.Cells[column]!.AddParagraph(text);
            paragraph.Format.Alignment = ToMigraDoc(sheet.Columns[column].Align);

            // LeftIndent, KHÔNG phải FirstLineIndent: Alignment.Indent của Excel dịch MỌI dòng của
            // ô, kể cả dòng đã xuống dòng; FirstLineIndent chỉ dịch dòng đầu.
            if (column == indentAt && source.IndentLevel > 0)
                paragraph.Format.LeftIndent =
                    Unit.FromMillimeter(ExcelFitScale.ContentWidthMm(new[] { IndentUnitsPerLevel * source.IndentLevel }) * scale);
        }
    }

    /// Chữ của một ô. Giữ ĐÚNG quy ước của đường .xlsx: ô trống không ghi gì, tiền theo
    /// PrintMoneyText, ngày theo dd/MM/yyyy, số không-phải-tiền không có dấu phân nghìn
    /// (nếu không năm 2026 in thành "2.026").
    private static string CellText(PrintCell cell) => cell.Kind switch
    {
        PrintCellKind.Text => cell.Text ?? "",
        PrintCellKind.Money => PrintMoneyText.Format(cell.Number!.Value),
        PrintCellKind.Number => cell.Number!.Value.ToString("0.################"),
        PrintCellKind.Date => cell.Date!.Value.ToString(DateFormat),
        _ => "",
    };

    /// Ô nhận bậc thụt: ô Text đầu tiên có chữ nằm ở cột căn trái. Cùng luật với
    /// PrintWorkbookWriter.IndexOfFirstIndentableText — hai bộ dựng phải thụt cùng một ô.
    private static int IndexOfFirstIndentableText(IReadOnlyList<PrintCell> cells, IReadOnlyList<PrintColumn> columns)
    {
        for (var index = 0; index < cells.Count; index++)
            if (columns[index].Align == PrintAlign.Left
                && cells[index].Kind == PrintCellKind.Text
                && !string.IsNullOrEmpty(cells[index].Text))
                return index;
        return -1;
    }

    private static Color ShadeColor(PrintShade shade) => shade switch
    {
        PrintShade.Light => LightShadeColor,
        PrintShade.VeryLight => VeryLightShadeColor,
        _ => throw new ArgumentOutOfRangeException(nameof(shade), shade, null),
    };

    private static ParagraphAlignment ToMigraDoc(PrintAlign align) => align switch
    {
        PrintAlign.Left => ParagraphAlignment.Left,
        PrintAlign.Center => ParagraphAlignment.Center,
        PrintAlign.Right => ParagraphAlignment.Right,
        _ => throw new ArgumentOutOfRangeException(nameof(align), align, null),
    };
}
