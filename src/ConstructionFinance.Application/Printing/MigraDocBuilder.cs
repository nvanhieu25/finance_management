using System.Globalization;
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

    /// Lề trên và lề dưới của bản giấy (xem ApplyPage về lý do khác .xlsx).
    internal const double VerticalMarginInches = 0.4;

    /// Từ mép giấy đến chân trang; ràng buộc bởi bất biến ghi ở ApplyPage.
    internal const double FooterDistanceInches = 0.18;

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
            ApplyPage(section, sheet.Page, footer, FontScaleOf(sheet));

            switch (sheet)
            {
                case PrintTableSheet table:
                    WriteTable(section, table);
                    break;
                case ThongTriPrintSheet thongTri:
                    MigraDocFormSheets.WriteThongTri(section, thongTri.Form);
                    break;
                case FormTemplatePrintSheet template:
                    MigraDocFormRenderer.Write(section, template.Sheet, template.Page);
                    break;
                case KsChiPrintSheet ksChi:
                    MigraDocFormSheets.WriteKsChi(section, ksChi.Form);
                    break;
                case GdnttPrintSheet gdntt:
                    MigraDocFormSheets.WriteGdntt(section, gdntt.Form);
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
    ///
    /// Hai trường của hồ sơ KHÔNG có đối ứng ở đây, cố ý:
    /// - RepeatColsLeft: bảng MigraDoc không bao giờ cắt ngang sang trang khác — quá rộng thì
    ///   tràn ra ngoài chứ không tách cột; ExcelFitScale co bảng cho vừa bề ngang chính để
    ///   khỏi xảy ra điều đó, nên không có "cột lặp bên trái" để mà lặp.
    /// - CenterHorizontally: đường .xlsx căn giữa trang, bản in giấy thì KHÔNG. Spec §5: sheet
    ///   hẹp in "đúng 13pt với một dải trắng bên phải" — căn trái. Quyết toán năm là sheet bị
    ///   ảnh hưởng: .xlsx căn giữa, bản in giấy dồn trái, và đó là chủ ý.
    private static void ApplyPage(Section section, PrintPageProfile page, PrintFooterInfo footer, double fontScale)
    {
        var setup = section.PageSetup;
        setup.PageFormat = PageFormat.A4;
        setup.Orientation = page.Orientation == PrintOrientation.Landscape
            ? Orientation.Landscape
            : Orientation.Portrait;
        setup.LeftMargin = Unit.FromInch(ExcelFitScale.SideMarginInches);
        setup.RightMargin = Unit.FromInch(ExcelFitScale.SideMarginInches);
        // CỐ Ý khác đường .xlsx (PrintWorkbookWriter.VerticalMargin = 0,6"): bản giấy XUỐNG DÒNG
        // những dòng mà Excel cắt (GĐNTT dòng 6–16), nên cần thêm chỗ để biểu mẫu cỡ cố định
        // vẫn vừa một tờ. Xem Gdntt_dong_dai_van_vua_mot_trang.
        setup.TopMargin = Unit.FromInch(VerticalMarginInches);
        setup.BottomMargin = Unit.FromInch(VerticalMarginInches);
        // FooterDistance tính từ MÉP GIẤY. BẤT BIẾN: (lề dưới − FooterDistance) phải lớn hơn chiều
        // cao một dòng chân trang, nếu không chân trang đội lên vùng thân và đè dòng cuối của bảng
        // đầy trang (Sổ Vị trí). Bất biến này đã vỡ hai lần: mặc định 1,25cm của MigraDoc với lề
        // 0,6" (Task 4), rồi hạ lề xuống 0,4" mà giữ FooterDistance 0,3" (Task 9).
        // Xem Chan_trang_nam_gon_duoi_le_duoi.
        setup.FooterDistance = Unit.FromInch(FooterDistanceInches);

        // Chân trang: tên dự án (kèm tên Vị trí) bên trái, số trang bên phải. KHÔNG phải nhân
        // đôi dấu & hay cắt ở 255 ký tự như đường .xlsx — đó là luật riêng của Excel, MigraDoc
        // nhận chuỗi thuần. Chuỗi dài chỉ bị cắt khi vẽ, không làm vỡ tài liệu.
        var paragraph = section.Footers.Primary.AddParagraph();
        // Cùng cỡ chữ đã co với thân bảng. Để mặc định 13pt thì chân trang to gấp rưỡi thân
        // bảng, và tên dự án dài dễ xuống dòng thứ hai trên trang ngang.
        paragraph.Format.Font.Size = Unit.FromPoint(FormExcelExporter.FontSize * fontScale);
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

    /// "Tên dự án · Tên Vị trí", cắt ở ĐÚNG số ký tự của đường .xlsx
    /// (PrintWorkbookWriter.MaxFooterLeftChars) để hai bản in đọc giống hệt nhau. Mức cắt này
    /// KHÔNG nhằm bảo đảm chân trang vừa tờ giấy: một đoạn liền không dấu cách dài hơn trang vẫn
    /// chạy ra mép phải, vì MigraDoc không ngắt giữa từ. Tên dự án thật có dấu cách nên chúng
    /// xuống dòng chứ không tràn. KHÔNG nhân đôi dấu & như đường .xlsx — đó mới là luật riêng
    /// của Excel (& là mã điều khiển).
    private static string FooterLeftText(PrintFooterInfo footer)
    {
        var text = string.Join(" · ", new[] { footer.ProjectName, footer.ViTriName }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

        var cap = PrintWorkbookWriter.MaxFooterLeftChars;
        return text.Length > cap
            ? TextTruncation.Truncate(text, cap - 1) + "…"   // chừa một chỗ cho "…", như đường .xlsx
            : text;
    }

    /// Tỉ lệ co chữ của một sheet; chân trang dùng đúng tỉ lệ này để cùng cỡ với thân.
    ///
    /// Sheet dạng BẢNG tính từ cột của chính nó; sheet BIỂU MẪU lấy từ MigraDocFormSheets —
    /// cùng hai trường (bề rộng cột, hướng giấy) mà FormGrid.Render dùng để co thân. Một bản
    /// trước của hàm này chỉ xét PrintTableSheet và rơi xuống 1.0 cho cả ba biểu mẫu, nên
    /// chân trang in 13pt cạnh thân 10,0 / 12,3 / 11,9pt — đúng ba tờ người dùng KÝ, và cũng
    /// là trường hợp xấu nhất cho bất biến khoảng cách chân trang (Chan_trang_nam_gon_duoi_le_duoi).
    private static double FontScaleOf(PrintSheet sheet) => sheet switch
    {
        PrintTableSheet { Columns.Count: > 0 } table =>
            ExcelFitScale.ScaleFor(table.Columns.Select(column => column.WidthChars), table.Page.Orientation),
        FormTemplatePrintSheet template => MigraDocFormRenderer.ScaleOf(template.Sheet, template.Page),
        PrintFormSheet form => MigraDocFormSheets.LayoutOf(form).FontScale,
        _ => 1.0,
    };

    private static void WriteTable(Section section, PrintTableSheet sheet)
    {
        // Kiểm dòng TRƯỚC khi quyết định có vẽ không, để bảng 0 cột mà có dòng mang ô vẫn bị báo
        // lỗi như đường .xlsx — không im lặng nuốt dữ liệu.
        for (var index = 0; index < sheet.Rows.Count; index++)
            ValidateRow(sheet, index);

        // Bảng không có cột thì không có gì để vẽ: Table không cột sẽ không dựng được dòng nào
        // (Table.AddRow() rồi Cells[0] ném, chỗ dòng dữ liệu ghi ô). Để Section rỗng — một
        // trang trắng mang chân trang, chứ không phải một lần sập.
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

    private static void ValidateRow(PrintTableSheet sheet, int index)
    {
        var count = sheet.Rows[index].Cells.Count;
        if (count > sheet.Columns.Count)
            throw new ArgumentException(
                $"Sheet \"{sheet.SheetName}\": Rows[{index}] có {count} ô nhưng bảng chỉ có {sheet.Columns.Count} cột.",
                nameof(sheet));
    }

    private static void WriteRow(Table table, PrintTableSheet sheet, int index, double scale)
    {
        var source = sheet.Rows[index];
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
                    Unit.FromMillimeter(IndentWidthMm(source.IndentLevel) * scale);
        }
    }

    /// Bề rộng thụt: chỉ tính bề rộng KÝ TỰ. ContentWidthMm cộng thêm 5px đệm một lần cho mỗi
    /// cột; thụt không phải một cột nên phải trừ phần đệm đó đi.
    private static double IndentWidthMm(int level) =>
        ExcelFitScale.ContentWidthMm(new[] { IndentUnitsPerLevel * level })
        - ExcelFitScale.ContentWidthMm(new[] { 0.0 });

    /// Chữ của một ô. Giữ ĐÚNG quy ước của đường .xlsx: ô trống không ghi gì, tiền theo
    /// PrintMoneyText, ngày theo dd/MM/yyyy, số không-phải-tiền không có dấu phân nghìn
    /// (nếu không năm 2026 in thành "2.026"). Văn hóa cố định Invariant: trên máy Windows tiếng
    /// Việt dấu thập phân là "," và "/" trong chuỗi định dạng là dấu ngày của văn hóa; Excel
    /// coi cả hai là ký tự thường.
    private static string CellText(PrintCell cell) => cell.Kind switch
    {
        PrintCellKind.Text => cell.Text ?? "",
        PrintCellKind.Money => PrintMoneyText.Format(cell.Number!.Value),
        PrintCellKind.Number => cell.Number!.Value.ToString("0.################", CultureInfo.InvariantCulture),
        PrintCellKind.Date => cell.Date!.Value.ToString(DateFormat, CultureInfo.InvariantCulture),
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
