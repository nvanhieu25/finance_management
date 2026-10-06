using ConstructionFinance.Application.Printing;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Printing;

/// Số học co tỉ lệ của bản in. Các con số trong lớp này tính tay theo đúng công thức của
/// Excel (xem spec §5) chứ không chép lại từ mã, nên nếu mã đổi công thức thì test đỏ.
public class ExcelFitScaleTests
{
    private const double Money = 16;

    /// 14 cột của Sổ Vị trí. px = 237*7 + 14*5 = 1729 → 1729*25,4/96 = 457,4646mm.
    private static readonly double[] ViTriColumns =
    {
        11, 38, 22, Money, Money, 11, Money, Money, Money, 11, Money, Money, Money, Money,
    };

    [Fact]
    public void Be_rong_noi_dung_cong_ca_phan_dem_nam_pixel_moi_cot()
    {
        ExcelFitScale.ContentWidthMm(ViTriColumns).ShouldBe(457.4646, 0.001);
    }

    /// Bỏ phần đệm 5px/cột ra 438,9mm — lệch 18,6mm, đủ để bản in tràn mép giấy.
    /// Test này tồn tại vì bản thiết kế đầu tiên đã bỏ sót đúng chỗ này.
    [Fact]
    public void Phan_dem_khong_duoc_bo_qua()
    {
        var withoutPadding = 237 * 7 * 25.4 / 96;
        ExcelFitScale.ContentWidthMm(ViTriColumns).ShouldBeGreaterThan(withoutPadding + 18);
    }

    [Theory]
    [InlineData(PrintOrientation.Landscape, 271.6)]   // 297 - 2*0,5"
    [InlineData(PrintOrientation.Portrait, 184.6)]    // 210 - 2*0,5"
    public void Be_ngang_giay_dung_duoc_tru_le_nua_inch_moi_ben(
        PrintOrientation orientation, double expected)
    {
        ExcelFitScale.UsableWidthMm(orientation).ShouldBe(expected, 0.001);
    }

    [Fact]
    public void So_Vi_tri_co_con_khoang_sau_muoi_phan_tram()
    {
        ExcelFitScale.ScaleFor(ViTriColumns, PrintOrientation.Landscape).ShouldBe(0.5937, 0.0001);
    }

    /// Quyết toán năm chỉ 4 cột nên HẸP HƠN giấy: tỉ lệ thô 1,4475. Chốt của spec §5 là
    /// chặn trên ở 1,0 — in đúng 13pt với dải trắng bên phải, không kéo giãn thành 18,8pt.
    [Fact]
    public void Noi_dung_hep_hon_giay_thi_khong_phong_to()
    {
        double[] quyetToanNam = { 12, 18, 18, 18 };
        ExcelFitScale.ScaleFor(quyetToanNam, PrintOrientation.Portrait).ShouldBe(1.0);
    }

    [Fact]
    public void Bang_khong_cot_thi_khong_co_gi_de_co()
    {
        ExcelFitScale.ScaleFor(Array.Empty<double>(), PrintOrientation.Portrait).ShouldBe(1.0);
    }

    /// Tỉ lệ co thật của cả sáu tài liệu, đo lúc viết plan (2026-10-07). Không phải để khóa
    /// con số — bề rộng cột đổi thì số này đổi theo — mà để người sửa THẤY mình vừa làm bản
    /// in nhỏ đi bao nhiêu, ngay trong lần chạy test.
    [Theory]
    [InlineData("Sổ Vị trí", PrintOrientation.Landscape, 237, 14, 0.5937)]
    [InlineData("Tổng hợp", PrintOrientation.Landscape, 231, 13, 0.6103)]
    [InlineData("Quyết toán năm", PrintOrientation.Portrait, 66, 4, 1.0)]
    [InlineData("Thông tri", PrintOrientation.Portrait, 124, 8, 0.7684)]
    [InlineData("KS chi", PrintOrientation.Portrait, 102, 5, 0.9441)]
    [InlineData("GĐNTT", PrintOrientation.Landscape, 154, 9, 0.9141)]
    public void Ti_le_co_cua_sau_tai_lieu(
        string name, PrintOrientation orientation, double totalUnits, int columnCount, double expected)
    {
        // Dựng lại đúng tổng đơn vị và số cột: tỉ lệ chỉ phụ thuộc hai con số đó.
        var columns = new double[columnCount];
        columns[0] = totalUnits - (columnCount - 1);
        for (var i = 1; i < columnCount; i++) columns[i] = 1;

        ExcelFitScale.ScaleFor(columns, orientation).ShouldBe(expected, 0.0001, name);
    }
}
