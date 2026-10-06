using ConstructionFinance.Application.Printing;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Printing;

/// Tiền trên bản in MigraDoc. Đường .xlsx ghi SỐ THẬT rồi để Excel tự định dạng theo máy
/// người dùng; MigraDoc chỉ nhận chuỗi, nên chuỗi phải do mình dựng đúng quy ước Việt.
public class PrintMoneyTextTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1")]
    [InlineData(999, "999")]
    [InlineData(1000, "1.000")]
    [InlineData(99_999_999_999L, "99.999.999.999")]
    [InlineData(-1_500_000L, "-1.500.000")]
    public void Dinh_dang_theo_quy_uoc_Viet_dau_cham_ngan(decimal value, string mong)
    {
        PrintMoneyText.Format(value).ShouldBe(mong);
    }

    /// Khác MoneyText của màn hình ĐÚNG MỘT CHỖ, có chủ ý: số 0. Màn hình và ô tiền của sheet
    /// bảng để TRỐNG (PrintCell.OfMoney(0) trả Empty), nhưng ba biểu mẫu gọi SetMoney(cell, 0)
    /// nên Excel in ra "0" — bản MigraDoc phải in "0" y hệt, nếu không hai bản in khác nhau.
    [Fact]
    public void So_khong_in_ra_chu_so_khong_chu_khong_phai_o_trong()
    {
        PrintMoneyText.Format(0m).ShouldBe("0");
    }
}
