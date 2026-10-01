using System.Globalization;
using ConstructionFinance.Desktop.Common;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests;

public class PrintFileNameTests
{
    private static readonly DateTime Now = new(2026, 9, 10, 14, 32, 0);

    [Theory]
    [InlineData("DA-01", "Vị trí 1", "DA-01-Vi tri 1")]
    [InlineData("DA/01", "A:B*C", "DA01-ABC")]
    [InlineData("", "Tổng hợp", "Tong hop")]
    public void Bo_dau_va_loai_ky_tu_Windows_cam(string prefix, string sheet, string expectedStem)
    {
        var name = PrintFileName.Suggest(prefix, sheet, new DateTime(2026, 9, 10, 14, 32, 0));
        name.ShouldBe($"{expectedStem}-20260910-1432.xlsx");
    }

    // Ba ca của kế hoạch không có chữ đ/Đ nào, mà FormD KHÔNG tách được đ/Đ (không có dấu rời để loại),
    // nên quy tắc "riêng đ/Đ → d/D" chỉ được kiểm ở đây.
    [Fact]
    public void Chu_d_gach_ngang_thanh_d_ca_hoa_lan_thuong()
    {
        PrintFileName.Suggest("", "Đà Nẵng đẹp", Now).ShouldBe("Da Nang dep-20260910-1432.xlsx");
    }

    // Đủ chín ký tự Windows cấm, không chỉ ba ký tự (/ : *) mà ca của kế hoạch chạm tới. Mỗi ký tự đứng giữa hai
    // chữ để ký tự nào sót lại là lộ ngay, và chữ hai bên không bị dính vào nhau thành chữ khác.
    [Fact]
    public void Loai_du_chin_ky_tu_Windows_cam()
    {
        PrintFileName.Suggest("", "a\\b/c:d*e?f\"g<h>i|j", Now).ShouldBe("abcdefghij-20260910-1432.xlsx");
    }

    [Fact]
    public void Gop_moi_day_khoang_trang_thanh_mot_dau_cach_va_cat_hai_dau()
    {
        PrintFileName.Suggest("  DA   01 ", "a\t\tb \n c", Now).ShouldBe("DA 01-a b c-20260910-1432.xlsx");
    }

    // Tên Vị trí do người dùng gõ, nên sau khi làm sạch có thể chẳng còn gì ("???"). Phần rỗng bị bỏ cả dấu
    // gạch, không để lại "DA-01--2026…".
    [Fact]
    public void Phan_ten_rong_sau_khi_lam_sach_bi_bo_ca_dau_gach()
    {
        PrintFileName.Suggest("DA-01", "???", Now).ShouldBe("DA-01-20260910-1432.xlsx");
        PrintFileName.Suggest("   ", "Tổng hợp", Now).ShouldBe("Tong hop-20260910-1432.xlsx");
        PrintFileName.Suggest("", "", Now).ShouldBe("20260910-1432.xlsx");
    }

    // string.Normalize ném ArgumentException khi gặp cặp surrogate đứt đôi. Tên Vị trí chép từ nơi khác có thể mang
    // nó, và gợi ý tên tệp là hàm thuần nên không được ném: ký tự hỏng bị bỏ, phần còn lại giữ nguyên.
    [Fact]
    public void Surrogate_dut_doi_khong_lam_ham_ne_ma_bi_bo()
    {
        PrintFileName.Suggest("DA\uD800", "A\uDC00B", Now).ShouldBe("DA-AB-20260910-1432.xlsx");
    }

    // Cặp surrogate nguyên vẹn (emoji) là chữ hợp lệ: không phải ký tự Windows cấm nên được giữ.
    [Fact]
    public void Cap_surrogate_nguyen_ven_duoc_giu()
    {
        PrintFileName.Suggest("", "Ca😀", Now).ShouldBe("Ca😀-20260910-1432.xlsx");
    }

    // Máy đặt vùng th-TH dùng lịch Phật giáo theo mặc định: "yyyy" sẽ ra 2569 chứ không phải 2026. Tên tệp gợi ý
    // phải mang năm Gregorian dù máy đặt vùng nào.
    [Fact]
    public void Nam_theo_lich_Gregorian_du_may_dat_vung_dung_lich_Phat_giao()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("th-TH");
            // Điều kiện đầu: culture này đúng là in năm khác, nếu không test chẳng chứng minh được gì.
            Now.ToString("yyyy").ShouldBe("2569");

            PrintFileName.Suggest("", "Tổng hợp", Now).ShouldBe("Tong hop-20260910-1432.xlsx");
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }
}
