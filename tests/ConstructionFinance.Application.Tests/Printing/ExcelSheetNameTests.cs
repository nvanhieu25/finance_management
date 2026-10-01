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
        var raw = new string('x', 50);
        ExcelSheetName.Unique(raw, new HashSet<string>()).Length.ShouldBe(31);
    }

    [Fact]
    public void Ten_trung_duoc_danh_so_va_van_khong_qua_31_ky_tu()
    {
        var used = new HashSet<string>();

        ExcelSheetName.Unique("Vị trí 1", used).ShouldBe("Vị trí 1");
        ExcelSheetName.Unique("Vị trí 1", used).ShouldBe("Vị trí 1 (2)");
        ExcelSheetName.Unique("Vị trí 1", used).ShouldBe("Vị trí 1 (3)");

        var longName = new string('y', 31);
        ExcelSheetName.Unique(longName, used);
        ExcelSheetName.Unique(longName, used).Length.ShouldBeLessThanOrEqualTo(31);
    }

    [Fact]
    public void Ghi_ten_da_dung_vao_tap_used()
    {
        var used = new HashSet<string>();
        ExcelSheetName.Unique("Tổng hợp", used);
        used.ShouldContain("Tổng hợp");
    }
}
