using ClosedXML.Excel;
using ConstructionFinance.Application.Printing;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using Shouldly;
using Xunit;
using Xunit.Sdk;

namespace ConstructionFinance.Application.Tests.Printing;

/// Ba sheet dạng bảng: hai bộ dựng phải cho ra cùng nội dung. Và trước đó, bản thân phép so
/// phải PHÂN BIỆT ĐƯỢC — một phép so luôn xanh thì không bảo vệ gì.
public class SheetTextMapTests
{
    private static readonly PrintFooterInfo Footer = new("Dự án A", "Vị trí 1");

    private static PrintTableSheet Fixture() => new(
        "Sổ Vị trí",
        PrintPageProfiles.ViTriLedger,
        new PrintColumn[]
        {
            new("Ngày", 11, PrintAlign.Center),
            new("Nội dung", 38, PrintAlign.Left),
            new("Giá trị HĐ", 16, PrintAlign.Right),
            new("Nhà thầu / TK", 24, PrintAlign.Left),
        },
        new[]
        {
            new PrintRow(new[]
            {
                PrintCell.OfDate(new DateOnly(2026, 10, 7)),
                PrintCell.OfText("Gói thầu số 01"),
                PrintCell.OfMoney(50_000_000_000m),
                PrintCell.OfText("Công ty CP Thành Đạt\n0123456789"),
            }),
            new PrintRow(new[]
            {
                PrintCell.Empty,
                PrintCell.OfText("TỔNG CỘNG"),
                PrintCell.OfMoney(50_000_000_000m),
                PrintCell.Empty,
            }, IsBold: true, Shade: PrintShade.Light),
        });

    private static IXLWorksheet ExcelSheet(PrintSheet sheet, out XLWorkbook workbook)
    {
        using var buffer = new MemoryStream();
        new PrintWorkbookWriter().Write(new[] { sheet }, buffer, Footer);
        buffer.Position = 0;
        workbook = new XLWorkbook(buffer);
        return workbook.Worksheets.First();
    }

    private static Table MigraDocTable(PrintSheet sheet) =>
        new MigraDocBuilder().Build(new[] { sheet }, Footer)
            .Sections[0]!.Elements.OfType<Table>().First();

    [Fact]
    public void Hai_bo_dung_cho_ra_cung_noi_dung_cho_so_Vi_tri()
    {
        var sheet = Fixture();
        var worksheet = ExcelSheet(sheet, out var workbook);
        using (workbook)
            SheetTextMap.AssertSameText(sheet.SheetName, worksheet, MigraDocTable(sheet));
    }

    /// Phép so có phân biệt được không: bỏ một ô khỏi bản MigraDoc thì nó PHẢI đỏ.
    [Fact]
    public void Phep_so_bat_duoc_khi_ban_MigraDoc_thieu_mot_o()
    {
        var sheet = Fixture();
        var worksheet = ExcelSheet(sheet, out var workbook);
        var table = MigraDocTable(sheet);

        // Xoá chữ ở ô (2,2) của bản MigraDoc — tức "Gói thầu số 01".
        table.Rows[1]!.Cells[1]!.Elements.Clear();

        using (workbook)
        {
            var ex = Should.Throw<XunitException>(() =>
                SheetTextMap.AssertSameText(sheet.SheetName, worksheet, table));
            ex.Message.ShouldContain("Gói thầu số 01");
        }
    }

    /// Và bắt được cả khi chữ còn đó nhưng LỆCH CHỖ — lỗi hay gặp nhất khi chép tay bố cục.
    [Fact]
    public void Phep_so_bat_duoc_khi_chu_lech_sang_cot_khac()
    {
        var sheet = Fixture();
        var worksheet = ExcelSheet(sheet, out var workbook);
        var table = MigraDocTable(sheet);

        table.Rows[1]!.Cells[1]!.Elements.Clear();
        table.Rows[1]!.Cells[2]!.AddParagraph("Gói thầu số 01");

        using (workbook)
        {
            var ex = Should.Throw<XunitException>(() =>
                SheetTextMap.AssertSameText(sheet.SheetName, worksheet, table));
            // Hai lệch: (2,2) mất chữ, (2,3) mang chữ không phải của nó.
            ex.Message.ShouldContain("(2,2)");
            ex.Message.ShouldContain("(2,3)");
        }
    }

    /// Danh sách lệch là chẩn đoán duy nhất của ba task chép biểu mẫu, và ô biểu mẫu thì dài.
    /// Nếu thông báo bị cắt ngắn thì hai ô khác nhau ở cuối chuỗi hiện ra giống hệt nhau, và người
    /// sửa không biết lệch chỗ nào. Test này ghim rằng thông báo mang NGUYÊN chữ của ô.
    [Fact]
    public void Thong_bao_lech_mang_nguyen_chu_cua_o_khong_bi_cat_ngan()
    {
        const string longText = "Lũy kế số vốn đã T/Toán từ K/công đến cuối kỳ trước (Gồm cả tạm ứng)";
        var sheet = new PrintTableSheet(
            "Sổ Vị trí",
            PrintPageProfiles.ViTriLedger,
            new PrintColumn[] { new("Cột dài", 60, PrintAlign.Left) },
            new[] { new PrintRow(new[] { PrintCell.OfText(longText) }) });
        var worksheet = ExcelSheet(sheet, out var workbook);
        var table = MigraDocTable(sheet);

        table.Rows[1]!.Cells[0]!.Elements.Clear();

        using (workbook)
        {
            var ex = Should.Throw<XunitException>(() =>
                SheetTextMap.AssertSameText(sheet.SheetName, worksheet, table));
            ex.Message.ShouldContain(longText);
        }
    }

    /// Ô nhiều dòng ("Nhà thầu / TK") phải được đọc đủ ở cả hai bên; xoá nó khỏi MigraDoc phải đỏ.
    [Fact]
    public void O_nhieu_dong_duoc_so_du_chu_o_ca_hai_ben()
    {
        var sheet = Fixture();
        var worksheet = ExcelSheet(sheet, out var workbook);
        var table = MigraDocTable(sheet);
        SheetTextMap.FromMigraDoc(table)[(2, 4)].ShouldBe("Công ty CP Thành Đạt 0123456789");

        table.Rows[1]!.Cells[3]!.Elements.Clear();
        using (workbook)
            Should.Throw<XunitException>(() => SheetTextMap.AssertSameText(sheet.SheetName, worksheet, table))
                .Message.ShouldContain("Công ty CP Thành Đạt 0123456789");
    }

    [Fact]
    public void Chuan_hoa_bo_dau_phan_nghin_de_hai_van_hoa_so_sanh_duoc()
    {
        SheetTextMap.Normalize("50,000,000,000").ShouldBe(SheetTextMap.Normalize("50.000.000.000"));
    }

    /// Dòng điền của KS chi " + Mục ........, tiểu mục ........" người dùng viết tay lên bản in:
    /// số chấm khác nhau phải bị phát hiện. Chỉ ô SỐ mới bị bỏ dấu phân cách.
    [Fact]
    public void Chuan_hoa_giu_dau_cham_cua_dong_dien_chu()
    {
        SheetTextMap.Normalize(" + Mục ........, tiểu mục ........")
            .ShouldNotBe(SheetTextMap.Normalize(" + Mục ...., tiểu mục ...."));
    }

    /// Ô chỉ có dấu chấm dẫn "........" không phải số: không được bị bỏ hết dấu thành rỗng
    /// (FromExcel bỏ chuỗi rỗng, ô sẽ thành không phân biệt được với ô vắng).
    [Fact]
    public void Chuan_hoa_khong_xoa_o_chi_co_dau_cham_dan()
    {
        SheetTextMap.Normalize("........").ShouldNotBeEmpty();
        SheetTextMap.Normalize("50,000,000,000").ShouldBe(SheetTextMap.Normalize("50.000.000.000"));
    }
}
