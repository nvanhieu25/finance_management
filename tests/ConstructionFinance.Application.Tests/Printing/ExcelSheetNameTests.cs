using System.Collections.Generic;
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
        ExcelSheetName.Unique(raw, new HashSet<string>()).ShouldBe(expected);
    }

    [Fact]
    public void Cat_con_31_ky_tu()
    {
        // Mỗi vị trí một ký tự khác nhau: một lát cắt lệch (cụt 30, dư 32, hay lấy từ cuối)
        // không thể trùng với giá trị mong đợi — điều mà chuỗi toàn chữ x không phân biệt được.
        var raw = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        ExcelSheetName.Unique(raw, new HashSet<string>()).ShouldBe("0123456789ABCDEFGHIJKLMNOPQRSTU");
    }

    [Fact]
    public void Ten_trung_duoc_danh_so_va_van_khong_qua_31_ky_tu()
    {
        var used = new HashSet<string>();

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

        var result = ExcelSheetName.Unique(raw, new HashSet<string>());

        result.ShouldBe(new string('a', 30) + "b");
        result.ShouldNotStartWith("'");
        result.ShouldNotEndWith("'");
    }

    [Fact]
    public void Ghi_ten_da_dung_vao_tap_used()
    {
        var used = new HashSet<string>();
        ExcelSheetName.Unique("Tổng hợp", used);
        used.ShouldContain("Tổng hợp");
    }
}
