using System.Globalization;
using System.Text;
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

    // ===== Độ dài =====
    //
    // ViTri.Name và FinanceProject.Name đều HasMaxLength(255), nên tên gợi ý (mã/tên dự án + tên Vị trí + mốc giờ) có thể dài ~530 ký
    // tự, vượt giới hạn 255 của MỘT thành phần tên tệp ở mọi hệ điều hành. Mỗi phần tên bị cắt còn 60 ký tự; mốc giờ thì không bao giờ.

    /// Chuỗi dài n ký tự mà mỗi vị trí một chữ cái khác nhau (a..z lặp lại), để một lát cắt lệch một ký tự đổi hẳn kết quả:
    /// chuỗi toàn một chữ không phân biệt được cắt ở 59, 60 hay 61.
    private static string Letters(int n) =>
        new(Enumerable.Range(0, n).Select(i => (char)('a' + i % 26)).ToArray());

    [Fact]
    public void Moi_phan_ten_bi_cat_con_60_ky_tu_con_moc_gio_giu_nguyen()
    {
        var prefix = Letters(255);
        var sheet = Letters(255).ToUpperInvariant();

        var name = PrintFileName.Suggest(prefix, sheet, Now);

        name.ShouldBe($"{prefix[..60]}-{sheet[..60]}-20260910-1432.xlsx");
        name.Length.ShouldBeLessThan(255);
    }

    [Fact]
    public void Dung_60_ky_tu_thi_giu_nguyen_61_ky_tu_thi_cat_con_60()
    {
        PrintFileName.Suggest("", Letters(60), Now).ShouldBe($"{Letters(60)}-20260910-1432.xlsx");
        PrintFileName.Suggest("", Letters(61), Now).ShouldBe($"{Letters(60)}-20260910-1432.xlsx");
        PrintFileName.Suggest(Letters(61), "", Now).ShouldBe($"{Letters(60)}-20260910-1432.xlsx");
    }

    // Dao cắt không được xé đôi cặp surrogate: một surrogate cao lẻ ở cuối là chuỗi hỏng, sinh ra tên tệp hỏng.
    [Fact]
    public void Cat_con_60_ky_tu_khong_xe_doi_cap_surrogate()
    {
        var straddling = new string('a', 59) + "\U0001F600" + "tail";   // emoji chiếm ký tự 60 và 61: dao rơi giữa cặp
        var fitting = new string('a', 58) + "\U0001F600" + "tail";     // emoji kết thúc đúng ở ký tự 60: còn nguyên

        var cutBefore = PrintFileName.Suggest("", straddling, Now);
        var kept = PrintFileName.Suggest("", fitting, Now);

        cutBefore.ShouldBe(new string('a', 59) + "-20260910-1432.xlsx");
        kept.ShouldBe(new string('a', 58) + "\U0001F600" + "-20260910-1432.xlsx");
        cutBefore.EnumerateRunes().ShouldNotContain(Rune.ReplacementChar);   // chuỗi hợp lệ, không surrogate lẻ
    }

    // Dao cắt có thể rơi ngay sau một dấu cách, để lại khoảng trắng cuối phần tên ("...a -2026").
    [Fact]
    public void Dau_cach_con_sot_o_cuoi_phan_ten_sau_khi_cat_bi_bo()
    {
        var sheet = new string('a', 59) + " " + "bbb";

        PrintFileName.Suggest("", sheet, Now).ShouldBe(new string('a', 59) + "-20260910-1432.xlsx");
    }

    // ===== Ký tự điều khiển =====
    //
    // Không gõ được từ bàn phím nhưng vào được qua nhập Excel. \t \n \r đã được nhánh khoảng trắng gộp thành một dấu cách; các ký
    // tự điều khiển còn lại (C0 như \u0001..\u0008, DEL, C1) phải bị loại hẳn, và KHÔNG được loại trước nhánh khoảng trắng,
    // nếu không "a\tb" thành "ab" thay vì "a b" (test Gop_moi_day_khoang_trang... giữ chỗ đó).

    [Fact]
    public void Loai_ky_tu_dieu_khien_con_tab_xuong_dong_van_thanh_dau_cach()
    {
        const string withControls = "a\0b\u0001c\u0008d\u000Ee\u001Ff\u007Fg\u0080h\u009Fi";

        PrintFileName.Suggest("", withControls, Now).ShouldBe("abcdefghi-20260910-1432.xlsx");
        PrintFileName.Suggest("", "a\u0001\tb\u0002\nc", Now).ShouldBe("a b c-20260910-1432.xlsx");
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
