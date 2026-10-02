using System.Collections.Generic;
using ClosedXML.Excel;
using ConstructionFinance.Application.Printing;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Printing;

/// ExcelSheetName tách ra từ FinanceExportService.UniqueSheetName — tệp đó nằm trong
/// danh sách 7E sẽ xóa, mà tính năng in phụ thuộc quy tắc này, nên nó phải sống ở chỗ
/// dùng chung KÈM TEST trước khi 7E chạy.
///
/// Hành vi là của hàm GỐC, giữ nguyên: tên dự phòng "DA", và loại MỌI dấu nháy đơn
/// (Excel từ chối tên bắt đầu HOẶC kết thúc bằng dấu nháy). Các dòng InlineData bên dưới
/// là chốt duy nhất của hai hành vi này.
public class ExcelSheetNameTests
{
    /// Tập tên đã dùng đúng cách: Excel coi tên sheet là KHÔNG phân biệt hoa thường. Mọi test gọi Unique đều lấy tập từ đây,
    /// để không test nào vô tình dựng sai comparer (chính lỗi mà Unique nay tự chặn).
    private static HashSet<string> NewUsed() => new(StringComparer.OrdinalIgnoreCase);

    // Điều kiện đúng của Unique nằm ở comparer của tập `used`, không ở chữ ký hàm: cùng là HashSet<string> nhưng dựng bằng
    // new HashSet<string>() thì "Abc" và "abc" không bị nhận ra là trùng, và triệu chứng là ClosedXML ném ở AddWorksheet, xa chỗ
    // gây ra lỗi. Nên hàm tự kiểm và nói rõ cách sửa. Ba ca là ba cách dựng sai thật: mặc định, Ordinal, và một comparer
    // KHÔNG phân biệt hoa thường nhưng không phải OrdinalIgnoreCase.
    [Theory]
    [InlineData("mặc định: new HashSet<string>()")]
    [InlineData("StringComparer.Ordinal")]
    [InlineData("StringComparer.InvariantCultureIgnoreCase")]
    public void Tap_used_khong_phai_OrdinalIgnoreCase_thi_nem_loi_noi_ro_cach_sua(string kind)
    {
        var used = kind switch
        {
            "StringComparer.Ordinal" => new HashSet<string>(StringComparer.Ordinal),
            "StringComparer.InvariantCultureIgnoreCase" => new HashSet<string>(StringComparer.InvariantCultureIgnoreCase),
            _ => new HashSet<string>(),
        };

        var error = Should.Throw<ArgumentException>(() => ExcelSheetName.Unique("Vị trí 1", used), kind);

        // Đúng kiểu (so GetType): Should.Throw<T> một mình chấp nhận cả lớp con như ArgumentOutOfRangeException.
        error.GetType().ShouldBe(typeof(ArgumentException), kind);
        error.ParamName.ShouldBe("used", kind);
        error.Message.ShouldContain("OrdinalIgnoreCase", Case.Sensitive, kind);
        used.ShouldBeEmpty(kind);     // kiểm TRƯỚC khi động vào tập
    }

    // Tiền đề của cả điều kiện trên: ClosedXML thật sự từ chối hai sheet chỉ khác hoa thường. Nếu một ngày nó thôi từ chối thì
    // điều kiện comparer không còn cần, và test này đỏ trước để nói vậy.
    [Fact]
    public void ClosedXML_tu_choi_hai_sheet_chi_khac_hoa_thuong()
    {
        using var workbook = new XLWorkbook();
        workbook.AddWorksheet("Cầu A");

        var error = Should.Throw<Exception>(() => workbook.AddWorksheet("CẦU A"));

        error.GetType().ShouldBe(typeof(ArgumentException));
    }

    [Theory]
    [InlineData("Vị trí 1", "Vị trí 1")]
    [InlineData("A/B:C*D?E[F]G\\H", "ABCDEFGH")]   // ký tự Excel cấm bị loại
    [InlineData("'Mở đầu", "Mở đầu")]              // dấu nháy đơn đầu chuỗi bị loại
    [InlineData("Mở đầu'", "Mở đầu")]              // ...và cuối chuỗi: Excel cũng từ chối tên kết thúc bằng '
    [InlineData("Cầu O'Brien", "Cầu OBrien")]      // hàm gốc loại MỌI dấu nháy đơn, không chỉ ở hai đầu
    [InlineData("   ", "DA")]                       // rỗng sau khi làm sạch → tên dự phòng của hàm gốc
    [InlineData(null, "DA")]                        // null → cùng tên dự phòng, không ném
    public void Lam_sach_ten_sheet(string? raw, string expected)
    {
        ExcelSheetName.Unique(raw, NewUsed()).ShouldBe(expected);
    }

    [Fact]
    public void Cat_con_31_ky_tu()
    {
        // Mỗi vị trí một ký tự khác nhau: một lát cắt lệch (cụt 30, dư 32, hay lấy từ cuối)
        // không thể trùng với giá trị mong đợi — điều mà chuỗi toàn chữ x không phân biệt được.
        var raw = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        ExcelSheetName.Unique(raw, NewUsed()).ShouldBe("0123456789ABCDEFGHIJKLMNOPQRSTU");
    }

    [Fact]
    public void Ten_trung_duoc_danh_so_va_van_khong_qua_31_ky_tu()
    {
        var used = NewUsed();

        ExcelSheetName.Unique("Vị trí 1", used).ShouldBe("Vị trí 1");
        ExcelSheetName.Unique("Vị trí 1", used).ShouldBe("Vị trí 1 (2)");
        ExcelSheetName.Unique("Vị trí 1", used).ShouldBe("Vị trí 1 (3)");

        // Tên đã đủ 31 ký tự: hậu tố " (2)" phải ăn vào chỗ của tên chứ không đẩy tổng quá 31.
        var longName = "ABCDEFGHIJKLMNOPQRSTUVWXYZ01234";
        ExcelSheetName.Unique(longName, used).ShouldBe(longName);
        ExcelSheetName.Unique(longName, used).ShouldBe("ABCDEFGHIJKLMNOPQRSTUVWXYZ0 (2)");

        // Lần thứ mười hậu tố thành " (10)" — dài hơn một ký tự, nên tên bị cắt thêm một ký tự.
        for (var n = 3; n <= 9; n++)
            ExcelSheetName.Unique(longName, used);
        ExcelSheetName.Unique(longName, used).ShouldBe("ABCDEFGHIJKLMNOPQRSTUVWXYZ (10)");
    }

    [Fact]
    public void Ten_chi_khac_hoa_thuong_van_duoc_danh_so_khi_used_la_OrdinalIgnoreCase()
    {
        // Excel coi tên sheet là KHÔNG phân biệt hoa thường, nên điều kiện tiên quyết của Unique
        // là tập `used` dựng bằng OrdinalIgnoreCase (xem doc của hàm). Test này chốt điều kiện
        // đó: sai comparer thì hết phát hiện được trùng, và ClosedXML ném khi thêm sheet.
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CẦU A" };

        ExcelSheetName.Unique("Cầu A", used).ShouldBe("Cầu A (2)");
        used.ShouldContain("Cầu A (2)");
    }

    [Fact]
    public void Dau_nhay_don_giua_ten_khong_thanh_dau_cuoi_sau_khi_cat()
    {
        // Lý do loại MỌI dấu nháy đơn chứ không chỉ ở hai đầu: dấu nháy nằm giữa tên có thể
        // trở thành dấu CUỐI sau khi cắt còn 31 ký tự, mà ClosedXML từ chối tên kết thúc bằng '.
        var raw = new string('a', 30) + "'bc";

        var result = ExcelSheetName.Unique(raw, NewUsed());

        result.ShouldBe(new string('a', 30) + "b");
        result.ShouldNotStartWith("'");
        result.ShouldNotEndWith("'");
    }

    // Ba test dưới đây chốt MỘT chỗ khác với hàm gốc, và là chỗ cố ý: cắt không xé đôi cặp
    // surrogate. Hàm gốc cắt cứng theo đơn vị UTF-16, nên một tên có emoji vắt qua ranh giới để
    // lại surrogate cao lẻ ở cuối — ClosedXML ném ArgumentException khi LƯU workbook (không phải
    // lúc AddWorksheet), làm hỏng cả lần in.

    [Fact]
    public void Cat_con_31_ky_tu_khong_xe_doi_cap_surrogate()
    {
        var raw = new string('a', 30) + "\U0001F600" + "tail";   // surrogate cao đúng ở ký tự thứ 31

        var name = ExcelSheetName.Unique(raw, NewUsed());

        name.ShouldBe(new string('a', 30));
        name.Any(char.IsSurrogate).ShouldBeFalse();
        AssertWorkbookSaves(name);
    }

    [Fact]
    public void Cat_de_nhuong_cho_hau_to_khong_xe_doi_cap_surrogate()
    {
        // Tên 30 ký tự thì hợp lệ. Thêm " (2)" phải cắt còn 27 ký tự, và dao rơi đúng giữa cặp
        // surrogate nằm ở ký tự thứ 27-28.
        var used = NewUsed();
        var raw = new string('a', 26) + "\U0001F600" + "bc";

        ExcelSheetName.Unique(raw, used).ShouldBe(raw);
        var second = ExcelSheetName.Unique(raw, used);

        second.ShouldBe(new string('a', 26) + " (2)");
        second.Any(char.IsSurrogate).ShouldBeFalse();
        AssertWorkbookSaves(second);
    }

    [Fact]
    public void Cap_surrogate_nam_tron_trong_31_ky_tu_duoc_giu_nguyen()
    {
        // Mặt kia của hai test trên: cặp surrogate kết thúc đúng ở ký tự thứ 31 thì còn nguyên,
        // không được lùi. Lùi nhầm ở đây để lại surrogate cao lẻ — đúng lỗi đang cần tránh.
        var raw = new string('a', 29) + "\U0001F600" + "tail";

        var name = ExcelSheetName.Unique(raw, NewUsed());

        name.ShouldBe(new string('a', 29) + "\U0001F600");
        name.Length.ShouldBe(31);
        AssertWorkbookSaves(name);
    }

    /// ClosedXML từ chối tên sheet hỏng ở lúc lưu, nên "lưu được" mới là chuẩn đối chiếu thật.
    private static void AssertWorkbookSaves(string sheetName)
    {
        using var workbook = new XLWorkbook();
        workbook.AddWorksheet(sheetName);
        using var buffer = new MemoryStream();

        Should.NotThrow(() => workbook.SaveAs(buffer));
    }

    [Fact]
    public void Ghi_ten_da_dung_vao_tap_used()
    {
        var used = NewUsed();
        ExcelSheetName.Unique("Tổng hợp", used);
        used.ShouldContain("Tổng hợp");
    }
}
