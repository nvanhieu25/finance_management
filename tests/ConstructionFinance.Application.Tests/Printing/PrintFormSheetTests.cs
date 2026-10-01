using ClosedXML.Excel;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Printing;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Printing;

/// Nhánh PrintFormSheet của PrintWorkbookWriter: ghi workbook ra bộ nhớ rồi ĐỌC LẠI bằng
/// ClosedXML, như PrintWorkbookWriterTests. Các biểu mẫu mẫu bên dưới là của RIÊNG tệp này,
/// cố ý không dùng chung với test FormExcelExporter: hai bộ test độc lập nhau, đổi fixture
/// bên này không làm đỏ bên kia.
public class PrintFormSheetTests
{
    private static readonly PrintFooterInfo Footer = new("Dự án A", "Vị trí 1");

    /// Chữ chân trang được ClosedXML cất theo loại trang; OddPages là thẻ Excel in ra khi không
    /// bật "khác nhau chẵn/lẻ". Lý do đầy đủ nằm ở PrintWorkbookWriterTests.PrintedOccurrence.
    private const XLHFOccurrence PrintedOccurrence = XLHFOccurrence.OddPages;

    private static PrintTableSheet SampleTableSheet() => new(
        "Tổng hợp",
        PrintPageProfiles.ProjectTongHop,
        new[]
        {
            new PrintColumn("Nội dung", 30, PrintAlign.Left),
            new PrintColumn("Tạm ứng", 15, PrintAlign.Right),
        },
        new[]
        {
            new PrintRow(new[] { PrintCell.OfText("Dự án A"), PrintCell.OfMoney(10_000_000m) }),
        });

    private static ThongTriForm SampleThongTriForm() => new(
        DonViCapTren: "Sở Tài chính",
        DonViCoSo: "Ban QLDA số 1",
        SoThongTri: "12/TT",
        NgayGhiSo: "05/03/2026",
        TenCTMT: "CTMT X",
        MaSo: "A1",
        VeThangNam: "tháng 3 năm 2026",
        TaiKhoan1: "TK1", DonVi1: "ĐV1", SoChungTu1: "CT1",
        TaiKhoan2: "TK2", DonVi2: "ĐV2", SoChungTu2: "CT2",
        LoaiNganSach: "10", Khoan: "11", Muc: "9300", TieuMuc: "9301", TietMuc: "0", Nganh: "0",
        Lines: new ThongTriLine[]
        {
            new(0, "Dự án A", 10_000_000m),
            new(1, "Vị trí 1", 10_000_000m),
            new(2, "Tạm ứng lần 1", 10_000_000m),
        },
        Total: 10_000_000m,
        TotalInWords: "Mười triệu đồng.");

    private static KsChiForm SampleKsChiForm() => new(
        NgayNhanGiayDeNghi: "05/03/2026",
        MucTieuMucLines: new[] { "Mục 9300, tiểu mục 9301", "", "", "" },
        Accepted: new KsChiAmounts(240_000_000m, 210_000_000m, 30_000_000m),
        Recovered: new KsChiAmounts(5_000_000m, 5_000_000m, 0m),
        ThuHoiCacNamTruoc: 1_000_000m,
        ThuHoiNamNay: 4_000_000m,
        ThueGTGT: 2_000_000m,
        PaidToBeneficiary: new KsChiAmounts(235_000_000m, 205_000_000m, 30_000_000m),
        PaidInWords: "Hai trăm ba mươi lăm triệu đồng.",
        SoTuChoi: 0m,
        LyDoTuChoi: null,
        GhiChu: "Tạm ứng lần 1");

    private static GdnttForm SampleGdnttForm() => new(
        DonViCapTren: "Sở Tài chính",
        DonViCoSo: "Ban QLDA số 1",
        So: "07/GĐN",
        NgayThangNam: "05/03/2026",
        KinhGui: "KBNN Hải Phòng",
        TenDuAnCongTrinh: "Dự án A",
        ChuDauTu: "Ban QLDA số 1",
        CanCuQuyetDinh: null,
        CanCuHopDong: null,
        CanCuGiayDeNghi: null,
        ThuocKeHoach: null,
        PackageName: "Gói A",
        ContractorName: "Cty XL A",
        BankAccount: "123456789",
        TaiNganHang: "Vietcombank Hải Phòng",
        UnrecoveredAdvanceBefore: 0m,
        ContractTotal: 52_000_000_000m,
        CumulativeBefore: 0m,
        RequestedThisPeriod: 30_000_000m,
        RequestedInWords: "Ba mươi triệu đồng.",
        RecoveredThisPeriod: 0m,
        ChuyenTienBaoHanh: 0m,
        ChuyenTienTamGiu: 0m,
        PaidToContractor: 30_000_000m,
        NguonVon: "Quỹ dự trữ ngoại hối");

    private static XLWorkbook WriteAndReopen(params PrintSheet[] sheets)
    {
        using var buffer = new MemoryStream();
        new PrintWorkbookWriter().Write(sheets, buffer, Footer);
        buffer.Position = 0;
        return new XLWorkbook(buffer);
    }

    /// Ghi `payload` vào một sheet biểu mẫu tên `sheetName` và đòi bộ ghi ném ĐÚNG ArgumentException,
    /// không phải lớp con, với thông báo nêu tên sheet. Should.Throw<ArgumentException> chấp nhận cả
    /// lớp con, nên chỉ kiểm kiểu thì ArgumentOutOfRangeException văng ra từ việc lấy hàm vẽ trên danh
    /// sách rỗng cũng làm test xanh dù lời bảo vệ chưa hề chạy. Hai nhánh (không có biểu mẫu nào, và
    /// quá một) cùng đi qua hàm này nên không nhánh nào xanh được bằng đường vòng.
    private static void AssertRejectsPayload(string sheetName, FormPayload payload, string label)
    {
        var sheets = new PrintSheet[] { new PrintFormSheet(sheetName, PrintPageProfiles.ThongTri, payload) };
        using var buffer = new MemoryStream();

        var error = Should.Throw<ArgumentException>(() =>
            new PrintWorkbookWriter().Write(sheets, buffer, Footer), label);

        error.GetType().ShouldBe(typeof(ArgumentException), label);
        error.Message.ShouldContain(sheetName, Case.Sensitive, label);
    }

    private static XLBorderStyleValues[] Sides(IXLCell cell)
    {
        var border = cell.Style.Border;
        return new[] { border.LeftBorder, border.RightBorder, border.TopBorder, border.BottomBorder };
    }

    [Fact]
    public void Ghep_duoc_bieu_mau_va_bang_trong_cung_mot_workbook()
    {
        var sheets = new PrintSheet[]
        {
            SampleTableSheet(),
            new PrintFormSheet("Thông tri", PrintPageProfiles.ThongTri,
                new FormPayload(ThongTri: SampleThongTriForm())),
        };

        using var buffer = new MemoryStream();
        new PrintWorkbookWriter().Write(sheets, buffer, new PrintFooterInfo("Dự án A", null));
        buffer.Position = 0;

        using var wb = new XLWorkbook(buffer);
        wb.Worksheets.Count.ShouldBe(2);
        wb.Worksheet(2).Name.ShouldBe("Thông tri");
        wb.Worksheet(2).Cell(4, 1).GetString().ShouldBe("THÔNG TRI");
        wb.Worksheet(2).PageSetup.PageOrientation.ShouldBe(XLPageOrientation.Portrait);
        wb.Worksheet(2).PageSetup.PagesTall.ShouldBe(0);   // chỉ ép vừa bề ngang; dọc bao nhiêu trang cũng được
    }

    [Fact]
    public void FormPayload_rong_thi_nem_loi_ro_rang()
    {
        var sheets = new PrintSheet[]
        {
            new PrintFormSheet("Trống", PrintPageProfiles.ThongTri, new FormPayload()),
        };

        using var buffer = new MemoryStream();
        Should.Throw<ArgumentException>(() =>
            new PrintWorkbookWriter().Write(sheets, buffer, new PrintFooterInfo("Dự án A", null)));
    }

    [Fact]
    public void FormPayload_rong_thi_nem_loi_neu_ten_sheet()
    {
        // Bản của brief ở trên chỉ kiểm kiểu ngoại lệ nên không phân biệt được lời bảo vệ với một
        // ArgumentOutOfRangeException văng ra khi lấy hàm vẽ trên danh sách rỗng. Test này đòi đúng
        // kiểu và đòi thông báo gọi đúng tên sheet.
        AssertRejectsPayload("Trống", new FormPayload(), "không có biểu mẫu nào");
    }

    [Fact]
    public void FormPayload_co_hon_mot_bieu_mau_thi_nem_loi_neu_ten_sheet()
    {
        var thongTri = SampleThongTriForm();
        var ksChi = SampleKsChiForm();
        var gdntt = SampleGdnttForm();

        // Mọi cách có từ hai biểu mẫu trở lên, không chỉ cặp đầu tiên.
        var payloads = new (string Label, FormPayload Payload)[]
        {
            ("ThongTri + KsChi", new FormPayload(ThongTri: thongTri, KsChi: ksChi)),
            ("ThongTri + Gdntt", new FormPayload(ThongTri: thongTri, Gdntt: gdntt)),
            ("KsChi + Gdntt", new FormPayload(KsChi: ksChi, Gdntt: gdntt)),
            ("cả ba", new FormPayload(ThongTri: thongTri, KsChi: ksChi, Gdntt: gdntt)),
        };

        foreach (var (label, payload) in payloads)
            AssertRejectsPayload("Hai biểu mẫu", payload, label);
    }

    [Fact]
    public void FormPayload_chi_co_ba_bieu_mau_ma_bo_ghi_biet_ve()
    {
        // Tripwire. PrintWorkbookWriter.WriteForm dựng danh sách hàm vẽ từ TỪNG thành viên của
        // FormPayload. Thêm biểu mẫu thứ tư (KH vốn / Vốn / QĐ...) mà quên thêm hàm vẽ thì payload
        // mang nó cùng một biểu mẫu cũ sẽ chỉ vẽ biểu mẫu cũ, lặng lẽ bỏ biểu mẫu mới. Test này đỏ
        // để ai thêm thành viên phải mở WriteForm ra: thêm hàm vẽ ở đó rồi sửa danh sách bên dưới.
        typeof(FormPayload).GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ShouldBe(new[] { "Gdntt", "KsChi", "ThongTri" });
    }

    [Fact]
    public void Moi_bieu_mau_duoc_ve_bang_ham_cua_no_va_dat_huong_giay_theo_ho_so_cua_no()
    {
        using var wb = WriteAndReopen(
            new PrintFormSheet("Thông tri", PrintPageProfiles.ThongTri, new FormPayload(ThongTri: SampleThongTriForm())),
            new PrintFormSheet("KS chi", PrintPageProfiles.KsChi, new FormPayload(KsChi: SampleKsChiForm())),
            new PrintFormSheet("GĐNTT", PrintPageProfiles.Gdntt, new FormPayload(Gdntt: SampleGdnttForm())));

        wb.Worksheets.Count.ShouldBe(3);

        // Mỗi sheet mang đúng tiêu đề của biểu mẫu mình: gọi nhầm hàm vẽ là lộ ngay.
        wb.Worksheet(1).Cell(4, 1).GetString().ShouldBe("THÔNG TRI");
        wb.Worksheet(2).Cell(1, 1).GetString().ShouldBe("PHẦN GHI CỦA CƠ QUAN KIỂM SOÁT, THANH TOÁN");
        wb.Worksheet(3).Cell(4, 1).GetString().ShouldBe("GIẤY ĐỀ NGHỊ THANH TOÁN VỐN ĐẦU TƯ");

        // GĐNTT có chín cột nên in ngang; hai biểu mẫu kia in dọc. Cả ba chỉ ép vừa bề ngang: ép luôn
        // chiều dọc thì Excel thu nhỏ vô hạn, một Thông tri dài co đến không đọc nổi thay vì sang trang.
        wb.Worksheet(1).PageSetup.PageOrientation.ShouldBe(XLPageOrientation.Portrait);
        wb.Worksheet(2).PageSetup.PageOrientation.ShouldBe(XLPageOrientation.Portrait);
        wb.Worksheet(3).PageSetup.PageOrientation.ShouldBe(XLPageOrientation.Landscape);
        foreach (var ws in wb.Worksheets)
        {
            ws.PageSetup.PagesWide.ShouldBe(1, ws.Name);
            ws.PageSetup.PagesTall.ShouldBe(0, ws.Name);
        }
    }

    [Fact]
    public void Huong_giay_theo_ho_so_chu_khong_theo_hang_so_trong_ham_ve_bieu_mau()
    {
        // Hàm vẽ Thông tri tự đặt khổ dọc bằng một hằng số. Hồ sơ nói ngang thì hồ sơ phải thắng:
        // nếu bộ ghi áp hồ sơ TRƯỚC khi vẽ, hằng số của hàm vẽ sẽ đè lên.
        var ngang = PrintPageProfiles.ThongTri with { Orientation = PrintOrientation.Landscape };

        using var wb = WriteAndReopen(
            new PrintFormSheet("Thông tri", ngang, new FormPayload(ThongTri: SampleThongTriForm())));

        wb.Worksheet(1).PageSetup.PageOrientation.ShouldBe(XLPageOrientation.Landscape);
    }

    [Fact]
    public void Chan_trang_cua_bieu_mau_giong_chan_trang_cua_sheet_bang()
    {
        using var wb = WriteAndReopen(
            SampleTableSheet(),
            new PrintFormSheet("Thông tri", PrintPageProfiles.ThongTri, new FormPayload(ThongTri: SampleThongTriForm())));

        var table = wb.Worksheet(1).PageSetup.Footer;
        var form = wb.Worksheet(2).PageSetup.Footer;

        // Chỉ kiểm SỰ TƯƠNG ĐƯƠNG: chân trang của biểu mẫu phải y hệt chân trang của sheet bảng. So với
        // sheet bảng chứ không so với chuỗi cứng, nên đổi định dạng chân trang không làm đỏ test này.
        // Áp hồ sơ hai lần lên một sheet có nhân đôi chân trang hay không là việc của
        // ApplyPage_ap_hai_lan_len_cung_mot_sheet_cho_ket_qua_nhu_mot_lan, không phải của test này.
        form.Left.GetText(PrintedOccurrence).ShouldBe(table.Left.GetText(PrintedOccurrence));
        form.Right.GetText(PrintedOccurrence).ShouldBe(table.Right.GetText(PrintedOccurrence));
        // Hai bên cùng rỗng cũng là "giống nhau": hai dòng dưới ghim rằng chân trang có chữ thật.
        form.Left.GetText(PrintedOccurrence).ShouldContain("Dự án A");
        form.Right.GetText(PrintedOccurrence).ShouldContain("&P");
    }

    [Fact]
    public void Bieu_mau_giu_nguyen_duong_ke_cua_chinh_no_va_khong_bi_ke_luoi_cua_bang()
    {
        using var wb = WriteAndReopen(
            new PrintFormSheet("Thông tri", PrintPageProfiles.ThongTri, new FormPayload(ThongTri: SampleThongTriForm())));
        var ws = wb.Worksheet(1);

        // Dòng tên đơn vị nằm ngoài khung bảng của Thông tri, nên không có nét kẻ nào. Lưới của
        // sheet bảng mà tràn sang đây thì Mẫu để trắng chỗ nào cũng bị bao khung.
        Sides(ws.Cell(1, 1)).ShouldAllBe(side => side == XLBorderStyleValues.None);

        // Còn khung bảng do chính biểu mẫu kẻ (dòng chữ cái A..G) vẫn nguyên.
        Sides(ws.Cell(14, 2)).ShouldAllBe(side => side == XLBorderStyleValues.Thin);
    }

    [Fact]
    public void Ten_sheet_bieu_mau_trung_ten_sheet_bang_thi_van_duoc_danh_so()
    {
        // Sheet bảng và sheet biểu mẫu dùng chung một tập tên, không phân biệt hoa thường.
        using var wb = WriteAndReopen(
            SampleTableSheet() with { SheetName = "Thông tri" },
            new PrintFormSheet("THÔNG TRI", PrintPageProfiles.ThongTri, new FormPayload(ThongTri: SampleThongTriForm())));

        wb.Worksheet(2).Name.ShouldBe("THÔNG TRI (2)");
        wb.Worksheet(2).Cell(4, 1).GetString().ShouldBe("THÔNG TRI");
    }

    [Fact]
    public void Bieu_mau_giu_chu_dam_va_font_Times_New_Roman_13_sau_khi_ap_ho_so()
    {
        using var wb = WriteAndReopen(
            new PrintFormSheet("Thông tri", PrintPageProfiles.ThongTri, new FormPayload(ThongTri: SampleThongTriForm())),
            new PrintFormSheet("KS chi", PrintPageProfiles.KsChi, new FormPayload(KsChi: SampleKsChiForm())),
            new PrintFormSheet("GĐNTT", PrintPageProfiles.Gdntt, new FormPayload(Gdntt: SampleGdnttForm())));

        // Bộ ghi áp hồ sơ SAU khi hàm vẽ đã vẽ ô, và hồ sơ đặt lại FontName/FontSize của cả sheet.
        // Ô tiêu đề do hàm vẽ in đậm phải vẫn đậm, và phải đúng font của Mẫu.
        foreach (var (sheet, row) in new[] { (1, 4), (2, 1), (3, 4) })
        {
            var ws = wb.Worksheet(sheet);
            var font = ws.Cell(row, 1).Style.Font;
            font.Bold.ShouldBeTrue(ws.Name);
            font.FontName.ShouldBe("Times New Roman", ws.Name);
            font.FontSize.ShouldBe(13d, ws.Name);
        }
    }

    [Fact]
    public void ApplyPage_ap_hai_lan_len_cung_mot_sheet_cho_ket_qua_nhu_mot_lan()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Vị trí 1");

        // Hồ sơ có lặp dòng/cột, để phần đó cũng bị kiểm bên dưới. Ép vừa trang không kiểm ở đây: nó chỉ
        // là một lời gọi đặt giá trị, và các test hồ sơ căn trang khác đã lo.
        PrintWorkbookWriter.ApplyPage(ws, PrintPageProfiles.ViTriLedger, Footer);
        PrintWorkbookWriter.ApplyPage(ws, PrintPageProfiles.ViTriLedger, Footer);

        using var buffer = new MemoryStream();
        wb.SaveAs(buffer);
        buffer.Position = 0;
        using var reopened = new XLWorkbook(buffer);
        var page = reopened.Worksheet(1).PageSetup;

        // AddText nối thêm vào chữ đã có, nên ApplyPage phải xóa chân trang cũ trước khi thêm. Kiểm đủ
        // ba loại trang vì AddText chép chữ ra cả ba thẻ lẻ / chẵn / đầu.
        foreach (var occurrence in new[] { XLHFOccurrence.OddPages, XLHFOccurrence.EvenPages, XLHFOccurrence.FirstPage })
        {
            page.Footer.Left.GetText(occurrence).ShouldBe("Dự án A · Vị trí 1", occurrence.ToString());
            page.Footer.Right.GetText(occurrence).ShouldBe("Trang &P/&N", occurrence.ToString());
        }

        page.FirstRowToRepeatAtTop.ShouldBe(1);
        page.LastRowToRepeatAtTop.ShouldBe(2);
        page.FirstColumnToRepeatAtLeft.ShouldBe(1);
        page.LastColumnToRepeatAtLeft.ShouldBe(2);
    }
}
