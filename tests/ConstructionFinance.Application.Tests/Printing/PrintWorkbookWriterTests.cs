using System.Collections;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using ConstructionFinance.Application.Printing;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Printing;

/// Ghi workbook ra bộ nhớ rồi ĐỌC LẠI bằng chính ClosedXML — kiểm cái tệp thật sự
/// chứa, chứ không kiểm lời hứa của bộ ghi.
public class PrintWorkbookWriterTests
{
    private static readonly PrintFooterInfo Footer = new("Dự án A", "Vị trí 1");

    /// ClosedXML cất chữ chân trang theo loại trang (lẻ / chẵn / đầu tiên). AddText mặc định là
    /// "AllPages" chỉ là lối vào để chép ra ba loại kia: GetText(AllPages) luôn trả chuỗi rỗng,
    /// kể cả trước khi lưu. OddPages là chuỗi được ghi vào thẻ oddFooter — thẻ Excel in trên
    /// mọi trang khi không bật "khác nhau chẵn/lẻ".
    private const XLHFOccurrence PrintedOccurrence = XLHFOccurrence.OddPages;

    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"in-{Guid.NewGuid():N}.xlsx");

    /// Độ dài chuỗi chân trang mà Excel thật sự nhận: "&L" + trái + "&R" + phải.
    private static int FooterLength(IXLHeaderFooter footer) =>
        "&L".Length + footer.Left.GetText(PrintedOccurrence).Length
        + "&R".Length + footer.Right.GetText(PrintedOccurrence).Length;

    private static PrintTableSheet SampleSheet() => new(
        "Vị trí 1",
        PrintPageProfiles.ViTriLedger,
        new[]
        {
            new PrintColumn("Ngày", 11, PrintAlign.Center),
            new PrintColumn("Nội dung", 38, PrintAlign.Left),
            new PrintColumn("Tạm ứng", 15, PrintAlign.Right),
        },
        new[]
        {
            new PrintRow(new[] { PrintCell.Empty, PrintCell.OfText("TỔNG CỘNG"), PrintCell.OfMoney(3_750_000m) },
                IndentLevel: 0, IsBold: true, IsShaded: true),
            new PrintRow(new[] { PrintCell.Empty, PrintCell.OfText("CHI PHÍ XÂY DỰNG"), PrintCell.Empty },
                IndentLevel: 0, IsBold: true),
            new PrintRow(new[]
                {
                    PrintCell.OfDate(new DateOnly(2026, 3, 15)),
                    PrintCell.OfText("Tạm ứng lần 1"),
                    PrintCell.OfMoney(3_750_000m),
                },
                IndentLevel: 2),
            new PrintRow(new[] { PrintCell.Empty, PrintCell.OfText("Đợt chưa nhập"), PrintCell.OfMoney(0m) },
                IndentLevel: 2),
        });

    private static XLWorkbook WriteAndReopen(params PrintSheet[] sheets) =>
        WriteAndReopen(Footer, sheets);

    private static XLWorkbook WriteAndReopen(PrintFooterInfo footer, params PrintSheet[] sheets)
    {
        using var buffer = new MemoryStream();
        new PrintWorkbookWriter().Write(sheets, buffer, footer);
        buffer.Position = 0;
        return new XLWorkbook(buffer);
    }

    [Fact]
    public void Ghi_dung_ten_va_thu_tu_sheet()
    {
        using var wb = WriteAndReopen(
            SampleSheet() with { SheetName = "Tổng hợp" },
            SampleSheet() with { SheetName = "Vị trí 1" });

        wb.Worksheets.Count.ShouldBe(2);
        wb.Worksheet(1).Name.ShouldBe("Tổng hợp");
        wb.Worksheet(2).Name.ShouldBe("Vị trí 1");
    }

    [Fact]
    public void Ten_sheet_trung_duoc_danh_so()
    {
        using var wb = WriteAndReopen(SampleSheet(), SampleSheet());
        wb.Worksheet(2).Name.ShouldBe("Vị trí 1 (2)");
    }

    [Fact]
    public void Dong_dau_la_tieu_de_cot()
    {
        using var wb = WriteAndReopen(SampleSheet());
        var ws = wb.Worksheet(1);

        ws.Cell(1, 1).GetString().ShouldBe("Ngày");
        ws.Cell(1, 2).GetString().ShouldBe("Nội dung");
        ws.Cell(1, 3).GetString().ShouldBe("Tạm ứng");
        ws.Cell(1, 1).Style.Font.Bold.ShouldBeTrue();
    }

    [Fact]
    public void Tien_ghi_thanh_SO_that_kem_dinh_dang_nghin()
    {
        using var wb = WriteAndReopen(SampleSheet());
        var cell = wb.Worksheet(1).Cell(2, 3);   // dòng TỔNG CỘNG, cột Tạm ứng

        cell.DataType.ShouldBe(XLDataType.Number);
        cell.GetDouble().ShouldBe(3_750_000d);
        cell.Style.NumberFormat.Format.ShouldBe("#,##0");
    }

    [Fact]
    public void Tien_bang_khong_ra_o_trong()
    {
        using var wb = WriteAndReopen(SampleSheet());
        wb.Worksheet(1).Cell(5, 3).IsEmpty().ShouldBeTrue();   // dòng "Đợt chưa nhập"
    }

    [Fact]
    public void Ngay_ghi_thanh_NGAY_that()
    {
        using var wb = WriteAndReopen(SampleSheet());
        var cell = wb.Worksheet(1).Cell(4, 1);

        cell.DataType.ShouldBe(XLDataType.DateTime);
        cell.GetDateTime().ShouldBe(new DateTime(2026, 3, 15));
        cell.Style.NumberFormat.Format.ShouldBe("dd/MM/yyyy");
    }

    [Fact]
    public void Dam_va_nen_theo_dung_loai_dong()
    {
        using var wb = WriteAndReopen(SampleSheet());
        var ws = wb.Worksheet(1);

        ws.Cell(2, 2).Style.Font.Bold.ShouldBeTrue();                       // TỔNG CỘNG
        ws.Cell(2, 2).Style.Fill.BackgroundColor.ShouldNotBe(XLColor.NoColor);
        ws.Cell(4, 2).Style.Font.Bold.ShouldBeFalse();                      // dòng Đợt
    }

    [Fact]
    public void Thut_bac_bang_Indent_cua_o_dau_tien_co_noi_dung()
    {
        using var wb = WriteAndReopen(SampleSheet());
        var ws = wb.Worksheet(1);

        ws.Cell(3, 2).Style.Alignment.Indent.ShouldBe(0);   // Nhóm
        ws.Cell(4, 2).Style.Alignment.Indent.ShouldBe(2);   // Đợt
    }

    [Fact]
    public void Chi_o_Text_dau_tien_co_chu_moi_bi_thut_cac_o_khac_giu_nguyen()
    {
        var sheet = new PrintTableSheet(
            "Gói thầu",
            PrintPageProfiles.ViTriLedger,
            new[]
            {
                new PrintColumn("Ngày", 11, PrintAlign.Left),
                new PrintColumn("Nội dung", 38, PrintAlign.Left),
                new PrintColumn("Nhà thầu / TK", 22, PrintAlign.Left),
                new PrintColumn("Giá trị HĐ", 15, PrintAlign.Right),
            },
            new[]
            {
                new PrintRow(
                    new[]
                    {
                        PrintCell.Empty,
                        PrintCell.OfText("Gói thầu số 01"),
                        PrintCell.OfText("Công ty A"),
                        PrintCell.OfMoney(1_000m),
                    },
                    IndentLevel: 1),
            });

        using var wb = WriteAndReopen(sheet);
        var ws = wb.Worksheet(1);

        ws.Cell(2, 1).Style.Alignment.Indent.ShouldBe(0);   // ô trống đứng trước
        ws.Cell(2, 2).Style.Alignment.Indent.ShouldBe(1);   // ô Text đầu tiên có chữ
        ws.Cell(2, 3).Style.Alignment.Indent.ShouldBe(0);   // ô Text thứ hai
        ws.Cell(2, 4).Style.Alignment.Indent.ShouldBe(0);   // ô tiền
    }

    [Fact]
    public void Dat_dung_can_trang_theo_ho_so()
    {
        using var wb = WriteAndReopen(SampleSheet());
        var page = wb.Worksheet(1).PageSetup;

        page.PaperSize.ShouldBe(XLPaperSize.A4Paper);
        page.PageOrientation.ShouldBe(XLPageOrientation.Landscape);
        page.PagesWide.ShouldBe(1);
        page.PagesTall.ShouldBe(0);
        page.CenterHorizontally.ShouldBeTrue();
    }

    [Fact]
    public void Chan_trang_co_so_trang_va_ten_du_an()
    {
        using var wb = WriteAndReopen(SampleSheet());
        var footer = wb.Worksheet(1).PageSetup.Footer;

        footer.Right.GetText(PrintedOccurrence).ShouldContain("&P");
        footer.Right.GetText(PrintedOccurrence).ShouldContain("&N");
        footer.Left.GetText(PrintedOccurrence).ShouldContain("Dự án A");
        footer.Left.GetText(PrintedOccurrence).ShouldContain("Vị trí 1");
    }

    [Fact]
    public void Be_rong_cot_lay_tu_PrintColumn()
    {
        using var wb = WriteAndReopen(SampleSheet());
        wb.Worksheet(1).Column(2).Width.ShouldBe(38d, 0.5);
    }

    [Fact]
    public void Font_dung_Times_New_Roman_13()
    {
        using var wb = WriteAndReopen(SampleSheet());
        var style = wb.Worksheet(1).Cell(4, 2).Style;

        style.Font.FontName.ShouldBe("Times New Roman");
        style.Font.FontSize.ShouldBe(13d);
    }

    // ---- Các hành vi còn lại của bộ ghi: đường ghi ra tệp và hủy giữa chừng, tên sheet chỉ khác
    // ---- hoa/thường, căn lề theo PrintAlign, WrapText, thụt bậc chỉ ở ô đầu, định dạng số, lặp
    // ---- dòng/cột, lề trang, kẻ khung, dòng nhiều ô hơn số cột, và chân trang (dấu &, tên quá
    // ---- dài, cặp surrogate, mọi loại trang).

    [Fact]
    public async Task WriteAsync_ghi_tep_that_va_mo_lai_duoc()
    {
        var path = TempPath();
        try
        {
            await new PrintWorkbookWriter().WriteAsync(new PrintSheet[] { SampleSheet() }, path, Footer);

            using var wb = new XLWorkbook(path);
            wb.Worksheet(1).Name.ShouldBe("Vị trí 1");
            wb.Worksheet(1).Cell(4, 2).GetString().ShouldBe("Tạm ứng lần 1");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Ten_sheet_chi_khac_hoa_thuong_van_duoc_danh_so()
    {
        // Excel coi "Cầu A" và "CẦU A" là cùng một tên; ClosedXML ném ArgumentException nếu
        // thêm sheet thứ hai. Hai Vị trí chỉ khác hoa/thường là chuyện có thật.
        using var wb = WriteAndReopen(
            SampleSheet() with { SheetName = "Cầu A" },
            SampleSheet() with { SheetName = "CẦU A" });

        wb.Worksheet(2).Name.ShouldBe("CẦU A (2)");
    }

    [Fact]
    public void Can_le_o_theo_PrintAlign_cua_cot()
    {
        using var wb = WriteAndReopen(SampleSheet());
        var ws = wb.Worksheet(1);

        ws.Cell(4, 1).Style.Alignment.Horizontal.ShouldBe(XLAlignmentHorizontalValues.Center);   // cột Ngày
        ws.Cell(4, 2).Style.Alignment.Horizontal.ShouldBe(XLAlignmentHorizontalValues.Left);     // cột Nội dung
        ws.Cell(4, 3).Style.Alignment.Horizontal.ShouldBe(XLAlignmentHorizontalValues.Right);    // cột Tạm ứng
    }

    [Fact]
    public void O_text_duoc_bat_WrapText_ca_chu_dai_lan_chu_co_ngat_dong()
    {
        var sheet = new PrintTableSheet(
            "Nhà thầu",
            PrintPageProfiles.ViTriLedger,
            new[]
            {
                new PrintColumn("Nội dung", 38, PrintAlign.Left),
                new PrintColumn("Nhà thầu / TK", 22, PrintAlign.Left),
            },
            new[]
            {
                new PrintRow(new[]
                {
                    PrintCell.OfText("Hạng mục rất dài cần được xuống dòng khi cột hẹp hơn nội dung của nó"),
                    PrintCell.OfText("Công ty A\n0123456789"),   // Gói thầu: nhà thầu và số TK ngăn bằng "\n"
                }),
            });

        using var wb = WriteAndReopen(sheet);
        var ws = wb.Worksheet(1);

        // ClosedXML chỉ tự bật WrapText khi chuỗi có ngắt dòng; chữ dài thì bộ ghi phải bật.
        ws.Cell(2, 1).Style.Alignment.WrapText.ShouldBeTrue();
        ws.Cell(2, 2).Style.Alignment.WrapText.ShouldBeTrue();
        ws.Cell(2, 2).GetString().ShouldBe("Công ty A\n0123456789");
    }

    [Fact]
    public void So_thuong_ghi_thanh_SO_that_khong_co_dau_phan_nghin()
    {
        var sheet = new PrintTableSheet(
            "Năm",
            PrintPageProfiles.YearSettlement,
            new[] { new PrintColumn("Năm", 12, PrintAlign.Center) },
            new[] { new PrintRow(new[] { PrintCell.OfNumber(2026m) }) });

        using var wb = WriteAndReopen(sheet);
        var cell = wb.Worksheet(1).Cell(2, 1);

        cell.DataType.ShouldBe(XLDataType.Number);
        cell.GetDouble().ShouldBe(2026d);
        cell.Style.NumberFormat.NumberFormatId.ShouldBe(0);   // General: năm 2026 không được in thành "2,026"
    }

    [Theory]
    [InlineData(0, 0)]   // biểu mẫu: không lặp gì — gọi ClosedXML với 0 sẽ ném
    [InlineData(1, 0)]   // Quyết toán năm: chỉ lặp dòng tiêu đề, không lặp cột
    [InlineData(2, 2)]   // Sổ Vị trí: tiêu đề + TỔNG CỘNG, và hai cột Ngày / Nội dung
    public void Lap_dong_dau_va_cot_trai_theo_ho_so(int repeatRows, int repeatCols)
    {
        var profile = new PrintPageProfile(PrintOrientation.Landscape, 1, 0, repeatRows, repeatCols);

        using var wb = WriteAndReopen(SampleSheet() with { Page = profile });
        var page = wb.Worksheet(1).PageSetup;

        // Không lặp thì ClosedXML đọc lại 0..0; có lặp thì luôn bắt đầu từ dòng/cột 1.
        page.FirstRowToRepeatAtTop.ShouldBe(repeatRows > 0 ? 1 : 0);
        page.LastRowToRepeatAtTop.ShouldBe(repeatRows);
        page.FirstColumnToRepeatAtLeft.ShouldBe(repeatCols > 0 ? 1 : 0);
        page.LastColumnToRepeatAtLeft.ShouldBe(repeatCols);
    }

    [Fact]
    public void Dat_dung_le_trang_va_chan_trang_khong_de_len_dong_cuoi()
    {
        using var wb = WriteAndReopen(SampleSheet());
        var margins = wb.Worksheet(1).PageSetup.Margins;

        margins.Left.ShouldBe(0.5);
        margins.Right.ShouldBe(0.5);
        margins.Top.ShouldBe(0.6);
        margins.Bottom.ShouldBe(0.6);
        // ClosedXML mặc định khoảng cách chân trang 0.75" — lớn hơn lề dưới thì chân trang
        // đè lên dòng cuối của mỗi trang.
        margins.Footer.ShouldBeLessThan(margins.Bottom);
    }

    [Fact]
    public void Dau_va_trong_ten_chan_trang_duoc_viet_doi()
    {
        // Excel hiểu & là mã điều khiển: "R&D" không viết đôi sẽ thành "R" + ngày hôm nay.
        using var wb = WriteAndReopen(new PrintFooterInfo("Nhà R&D", "Cầu A & B"), SampleSheet());

        wb.Worksheet(1).PageSetup.Footer.Left.GetText(PrintedOccurrence)
            .ShouldBe("Nhà R&&D · Cầu A && B");
    }

    [Fact]
    public void Ten_chan_trang_dai_nhat_van_con_vua_gioi_han_255_ky_tu()
    {
        // Tên toàn dấu & dài đúng mức trần: không bị cắt, và mỗi dấu viết đôi nên chuỗi chân
        // trang chạm sát giới hạn — ClosedXML ném ngay khi lưu nếu hơn 255.
        var max = PrintWorkbookWriter.MaxFooterLeftChars;
        using var wb = WriteAndReopen(new PrintFooterInfo(new string('&', max), null), SampleSheet());

        var footer = wb.Worksheet(1).PageSetup.Footer;
        footer.Left.GetText(PrintedOccurrence).ShouldBe(new string('&', 2 * max));
        // Mức trần phải dùng hết giới hạn của Excel, không chừa dư quá một ký tự.
        FooterLength(footer).ShouldBeInRange(254, 255);
    }

    [Fact]
    public void Ten_chan_trang_qua_dai_bi_cat_va_ket_thuc_bang_dau_ba_cham()
    {
        var max = PrintWorkbookWriter.MaxFooterLeftChars;
        using var wb = WriteAndReopen(new PrintFooterInfo(new string('&', max + 1), null), SampleSheet());

        var footer = wb.Worksheet(1).PageSetup.Footer;
        // Cắt còn max - 1 ký tự rồi thêm "…"; dấu & viết đôi.
        footer.Left.GetText(PrintedOccurrence).ShouldBe(new string('&', 2 * (max - 1)) + "…");
        FooterLength(footer).ShouldBeLessThanOrEqualTo(255);
    }

    [Fact]
    public void Cat_ten_chan_trang_khong_xe_doi_cap_surrogate()
    {
        // Emoji là một cặp surrogate (hai ký tự UTF-16). Đặt nó đúng chỗ dao rơi: cắt cứng theo
        // đơn vị UTF-16 sẽ để lại một surrogate lẻ, mà surrogate lẻ không phải XML hợp lệ.
        var max = PrintWorkbookWriter.MaxFooterLeftChars;
        var name = new string('a', max - 2) + "\U0001F600" + "tail";   // surrogate cao nằm ở ký tự giữ lại cuối cùng
        using var wb = WriteAndReopen(new PrintFooterInfo(name, null), SampleSheet());

        var left = wb.Worksheet(1).PageSetup.Footer.Left.GetText(PrintedOccurrence);
        left.ShouldBe(new string('a', max - 2) + "…");
        left.Any(char.IsSurrogate).ShouldBeFalse();
    }

    [Fact]
    public void Chan_trang_giong_nhau_o_moi_loai_trang_va_khong_bat_khac_nhau_chan_le()
    {
        using var wb = WriteAndReopen(SampleSheet());
        var page = wb.Worksheet(1).PageSetup;

        // Khi người dùng bật "khác nhau chẵn/lẻ" hay "trang đầu khác", Excel đọc chân trang từ thẻ
        // riêng của loại trang đó: cả ba thẻ phải mang cùng chữ, không thì bật công tắc là mất.
        foreach (var occurrence in new[] { XLHFOccurrence.OddPages, XLHFOccurrence.EvenPages, XLHFOccurrence.FirstPage })
        {
            page.Footer.Left.GetText(occurrence).ShouldBe("Dự án A · Vị trí 1", occurrence.ToString());
            page.Footer.Right.GetText(occurrence).ShouldBe("Trang &P/&N", occurrence.ToString());
        }

        page.DifferentOddEvenPagesOnHF.ShouldBeFalse();
        page.DifferentFirstPageOnHF.ShouldBeFalse();
    }

    [Fact]
    public void Ke_vien_mong_toan_bang_va_vien_duoi_tieu_de_dam_hon()
    {
        using var wb = WriteAndReopen(SampleSheet());   // 3 cột; tiêu đề + 4 dòng dữ liệu = A1:C5
        var ws = wb.Worksheet(1);
        const int lastRow = 5;
        const int lastColumn = 3;
        var grid = PrintWorkbookWriter.GridBorder;
        var headerEdge = PrintWorkbookWriter.HeaderBottomBorder;

        headerEdge.ShouldNotBe(grid);   // không thì tiêu đề chẳng có nét nào "đậm hơn"
        for (var r = 1; r <= lastRow; r++)
        {
            for (var c = 1; c <= lastColumn; c++)
            {
                var border = ws.Cell(r, c).Style.Border;
                var at = $"ô ({r}, {c})";
                border.LeftBorder.ShouldBe(grid, $"{at} cạnh trái");
                border.RightBorder.ShouldBe(grid, $"{at} cạnh phải");
                // Cạnh giữa tiêu đề và dòng dữ liệu đầu tiên là của cả hai ô liền kề; ClosedXML
                // không tự đồng bộ hai bên nên phải ghi cả hai, đừng để Excel chọn giữa hai nét.
                border.BottomBorder.ShouldBe(r == 1 ? headerEdge : grid, $"{at} cạnh dưới");
                border.TopBorder.ShouldBe(r == 2 ? headerEdge : grid, $"{at} cạnh trên");
            }
        }

        // Nét kẻ không tràn ra ngoài bảng.
        ws.Cell(1, lastColumn + 1).Style.Border.BottomBorder.ShouldBe(XLBorderStyleValues.None);
        ws.Cell(lastRow + 1, 1).Style.Border.RightBorder.ShouldBe(XLBorderStyleValues.None);
    }

    [Fact]
    public void Bang_chi_co_tieu_de_van_ke_vien_va_khong_dung_vao_dong_khong_ton_tai()
    {
        var sheet = SampleSheet() with { Rows = Array.Empty<PrintRow>() };

        using var wb = WriteAndReopen(sheet);
        var ws = wb.Worksheet(1);

        for (var c = 1; c <= 3; c++)
        {
            ws.Cell(1, c).Style.Border.BottomBorder.ShouldBe(PrintWorkbookWriter.HeaderBottomBorder);
            ws.Cell(1, c).Style.Border.LeftBorder.ShouldBe(PrintWorkbookWriter.GridBorder);
        }

        // Dòng 2 không có dữ liệu thì không được dính nét kẻ nào (sẽ kéo dài vùng in một dòng trống).
        ws.Cell(2, 1).Style.Border.TopBorder.ShouldBe(XLBorderStyleValues.None);
    }

    [Fact]
    public void Dong_nhieu_o_hon_so_cot_thi_bao_loi_neu_ro_sheet_va_so_luong()
    {
        var sheet = new PrintTableSheet(
            "Vị trí 7",
            PrintPageProfiles.YearSettlement,
            new[] { new PrintColumn("Năm", 12, PrintAlign.Center), new PrintColumn("NSNN", 18, PrintAlign.Right) },
            new[]
            {
                new PrintRow(new[] { PrintCell.OfText("2026"), PrintCell.Empty }),
                new PrintRow(new[] { PrintCell.OfText("2027"), PrintCell.Empty, PrintCell.Empty }),   // thừa một ô
            });
        using var buffer = new MemoryStream();

        var error = Should.Throw<ArgumentException>(
            () => new PrintWorkbookWriter().Write(new PrintSheet[] { sheet }, buffer, Footer));

        error.Message.ShouldContain("Vị trí 7");
        error.Message.ShouldContain("Rows[1]");
        error.Message.ShouldContain("3 ô");
        error.Message.ShouldContain("2 cột");
    }

    [Fact]
    public async Task WriteAsync_bi_huy_giua_chung_thi_khong_ghi_tep()
    {
        using var cts = new CancellationTokenSource();
        var sheets = new CancelWhenRead(new PrintSheet[] { SampleSheet() }, cts);
        var path = TempPath();
        try
        {
            await Should.ThrowAsync<OperationCanceledException>(
                () => new PrintWorkbookWriter().WriteAsync(sheets, path, Footer, cts.Token));

            File.Exists(path).ShouldBeFalse();
        }
        finally { File.Delete(path); }
    }

    /// Danh sách sheet hủy token ngay khi bị đọc đến. Lúc bộ ghi bắt đầu liệt kê sheet thì
    /// Task.Run đã chạy rồi, nên chỉ có kiểm tra ct SAU khi dựng xong mới bắt được — và test
    /// này không cần canh giờ hay ngủ.
    private sealed class CancelWhenRead(IReadOnlyList<PrintSheet> inner, CancellationTokenSource cts)
        : IReadOnlyList<PrintSheet>
    {
        public int Count { get { cts.Cancel(); return inner.Count; } }

        public PrintSheet this[int index] { get { cts.Cancel(); return inner[index]; } }

        public IEnumerator<PrintSheet> GetEnumerator() { cts.Cancel(); return inner.GetEnumerator(); }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
