using ClosedXML.Excel;
using ConstructionFinance.Application.Printing;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

/// Ba màn số liệu (Sổ Vị trí, Tổng hợp, Quyết toán năm) khai báo mình in ra cái gì qua
/// IPrintablePage. Phần lớn chỉ kiểm cái màn hình nói ra (PrintSheet); việc ghi ra .xlsx là của
/// PrintWorkbookWriter, trừ test cuối canh đúng chỗ hai bên chạm nhau.
public class PrintablePageTests
{
    // ===== Dữ liệu mẫu =====

    /// Một nhóm / một gói / một đợt — đủ chạm mọi loại dòng, mọi ô tiền đều 0.
    /// Cùng hình dạng với BuildOneOfEachRowKindFixture trong VitriLedgerViewTests.cs.
    private static ViTri Fixture()
    {
        var viTri = new ViTri { Name = "Vị trí 1 — Nhà điều hành" };
        var group = new CostGroup { Name = "CHI PHÍ XÂY DỰNG", SortOrder = 0 };
        var package = new Package { Name = "Gói thầu số 01", SortOrder = 0 };
        package.Dots.Add(new Dot { Content = "Đợt 1", SortOrder = 0, Fund = Fund.NSNN });
        group.Packages.Add(package);
        viTri.CostGroups.Add(group);
        return viTri;
        // Rows: [0] Total, [1] Nhóm, [2] Gói, [3] Đợt
    }

    /// Như Fixture() nhưng đợt có tiền, để các cột tính sẵn khác 0 — cần cho test
    /// "in ra SỐ thật", vì tiền bằng 0 ra ô trống nên không phân biệt được kiểu.
    private static ViTri FixtureWithMoney()
    {
        var viTri = Fixture();
        var dot = viTri.CostGroups[0].Packages[0].Dots[0];
        dot.Advance = 3_750_000m;
        dot.Date = new DateOnly(2026, 3, 15);
        return viTri;
    }

    /// Một nhóm / một gói / hai đợt, mọi số tiền đều khác 0 và khác nhau từng đôi một — để cột nào bị
    /// đặt nhầm chỗ thì giá trị lệch ngay. Số liệu tính tay (giải ngân = Tạm ứng + KLHT − Thu hồi):
    ///   đợt 1, NSNN,      năm tài chính 2026: 3.750.000 + 2.000.000 − 500.000 = 5.250.000
    ///   đợt 2, Ngoại hối, năm tài chính 2027: 1.000.000 +   800.000 − 300.000 = 1.500.000
    ///   Tạm ứng 4.750.000 · KLHT 2.800.000 · Thu hồi 800.000 · Tạm ứng chưa thu hồi 3.950.000
    ///   Tổng đã giải ngân 6.750.000 · Giá trị HĐ + phụ lục 80.000.000 + 6.000.000 = 86.000.000
    /// Rows: [0] Total, [1] Nhóm, [2] Gói, [3] Đợt 1, [4] Đợt 2
    private static ViTri RichFixture()
    {
        var viTri = new ViTri { Name = "Vị trí 1 — Nhà điều hành" };
        var group = new CostGroup { Name = "CHI PHÍ XÂY DỰNG", SortOrder = 0 };
        var package = new Package
        {
            Name = "Gói thầu số 01",
            ContractorName = "Công ty An Phát",
            BankAccount = "TK 0123456789",
            ContractValue = 80_000_000m,
            ContractAnnexValue = 6_000_000m,
            SortOrder = 0,
        };
        package.Dots.Add(new Dot
        {
            SortOrder = 0, Date = new DateOnly(2026, 3, 15), Content = "Tạm ứng lần 1", DocumentNo = "CT-01",
            Fund = Fund.NSNN, Advance = 3_750_000m, PaymentKLHT = 2_000_000m, RecoveredAdvance = 500_000m,
        });
        package.Dots.Add(new Dot
        {
            SortOrder = 1, Date = new DateOnly(2027, 3, 20), Content = "Thanh toán đợt 2", DocumentNo = "CT-02",
            Fund = Fund.NgoaiHoi, Advance = 1_000_000m, PaymentKLHT = 800_000m, RecoveredAdvance = 300_000m,
        });
        group.Packages.Add(package);
        viTri.CostGroups.Add(group);
        return viTri;
    }

    // ===== Dụng cụ đọc PrintSheet =====

    private static PrintTableSheet LedgerSheet(ViTri viTri) =>
        (PrintTableSheet)new ViTriPageViewModel(new VitriLedgerViewModel(viTri)).BuildPrintSheet()!;

    private static PrintTableSheet TongHopSheet(ViTri viTri)
    {
        var inner = new ProjectTongHopViewModel();
        inner.Refresh(new[] { viTri });
        return (PrintTableSheet)new ProjectTongHopPageViewModel(inner).BuildPrintSheet()!;
    }

    private static PrintTableSheet QuyetToanSheet(ViTri viTri)
    {
        var inner = new YearSettlementViewModel();
        inner.Refresh(new[] { viTri });
        return (PrintTableSheet)new YearSettlementPageViewModel(inner).BuildPrintSheet()!;
    }

    /// Ô nằm dưới cột có tiêu đề `header` — đọc như đọc tờ giấy in, không phụ thuộc số thứ tự cột.
    private static PrintCell CellAt(PrintTableSheet sheet, int row, string header)
    {
        var column = sheet.Columns.Select((c, i) => (c, i)).Single(x => x.c.Header == header).i;
        return sheet.Rows[row].Cells[column];
    }

    /// Tiêu đề của những cột mà dòng này có ghi gì đó. Đem so với danh sách mong đợi là cách chứng
    /// minh "mọi ô còn lại là ô trống" mà không phải liệt kê từng ô trống. "Trống" theo IsBlank, không phải so với
    /// PrintCell.Empty: bản in lấy thẳng chuỗi rỗng của màn hình nên một ô trống thường là OfText("") chứ không phải Empty.
    private static string[] FilledHeaders(PrintTableSheet sheet, int row) =>
        sheet.Columns
            .Where((_, i) => !IsBlank(sheet.Rows[row].Cells[i]))
            .Select(c => c.Header)
            .ToArray();

    /// Ô chữ không in ra gì: PrintCell.Empty hoặc chữ rỗng. Đó là hai cách viết cùng một ô trống, nên
    /// so bằng cái in ra chứ không so bằng cách viết.
    private static bool IsBlank(PrintCell cell) =>
        cell.Kind == PrintCellKind.Text && string.IsNullOrEmpty(cell.Text);

    [Theory]
    [InlineData("Sổ Vị trí")]
    [InlineData("Tổng hợp")]
    [InlineData("Quyết toán năm")]
    public void Moi_cot_tien_du_rong_cho_so_duoi_mot_tram_ty(string sheetName)
    {
        // Ô số KHÔNG xuống dòng được: hẹp hơn chuỗi là Excel in "####". Bề rộng cột đo bằng chữ "0"
        // của Calibri 11 (7px @96dpi) còn ô lại in Times New Roman 13, nên sức chứa THỰC nhỏ hơn bề
        // rộng danh nghĩa — đo 2026-10-06: 16 ký tự vừa đủ 99.999.999.999, 15 ký tự thì 10 tỷ đã tràn.
        // Ép vừa trang không cứu được vì nó thu cột và chữ cùng tỉ lệ.
        // Trong cả ba sheet, cột căn PHẢI đúng là cột tiền (ngày/STT/nguồn căn giữa, chữ căn trái),
        // nên đây là cách nói "mọi cột tiền" mà không phải liệt kê tên từng cột.
        var viTri = Fixture();
        var sheet = sheetName switch
        {
            "Sổ Vị trí" => LedgerSheet(viTri),
            "Tổng hợp" => TongHopSheet(viTri),
            _ => QuyetToanSheet(viTri),
        };

        var hep = sheet.Columns.Where(c => c.Align == PrintAlign.Right && c.WidthChars < 16).ToList();
        hep.ShouldBeEmpty(
            $"{sheet.SheetName}: cột tiền hẹp hơn 16 sẽ in \"####\" từ 10 tỷ trở lên — " +
            string.Join(", ", hep.Select(c => $"{c.Header}={c.WidthChars}")));
    }

    private static void AssertColumns(PrintTableSheet sheet, params PrintColumn[] expected)
    {
        sheet.Columns.Count.ShouldBe(expected.Length);
        for (var i = 0; i < expected.Length; i++)
            sheet.Columns[i].ShouldBe(expected[i], $"cột {i}");

        // Danh sách cột của trang dùng chung cho mọi lần in. Nếu là mảng thì người nhận ép kiểu về
        // PrintColumn[] rồi sửa được, làm hỏng mọi sheet in sau đó.
        (sheet.Columns is PrintColumn[]).ShouldBeFalse($"{sheet.SheetName}: Columns không được là mảng dùng chung");
    }

    // ===== IPrintablePage: mặc định của lớp cha =====

    [Fact]
    public void Moi_trang_cua_workbook_deu_la_IPrintablePage()
    {
        // Kiểm trên kiểu chứ không trên một đối tượng: lớp cha khai báo IPrintablePage thì mọi trang con
        // đều là IPrintablePage, kể cả trang chưa có. (typeof(...).ShouldBeAssignableTo<>() sẽ kiểm chính
        // đối tượng Type chứ không phải kiểu nó đại diện.)
        typeof(WorkbookPageViewModel).IsAssignableTo(typeof(IPrintablePage)).ShouldBeTrue();
    }

    /// Trang giả không override gì ngoài Content bắt buộc, nên nó ghim đúng giá trị mặc định của lớp
    /// cha. Cố ý không dùng ProjectPageViewModel / SummaryPageViewModel (cũng không override): cả hai
    /// nằm trong danh sách xóa của 7E, test nêu tên chúng sẽ chết cùng.
    private sealed class TrangChuaOverride : WorkbookPageViewModel
    {
        public override object Content => new object();
    }

    [Fact]
    public void Trang_chua_override_mac_dinh_la_khong_in_duoc()
    {
        IPrintablePage page = new TrangChuaOverride();

        page.PrintSheetName.ShouldBeNull();
        page.BuildPrintSheet().ShouldBeNull();
    }

    // ===== Sổ Vị trí =====

    [Fact]
    public void So_Vi_tri_in_ra_dung_so_dong_va_dung_thu_tu_nhu_tren_man_hinh()
    {
        var ledger = new VitriLedgerViewModel(Fixture());
        var page = new ViTriPageViewModel(ledger);

        var sheet = (PrintTableSheet)page.BuildPrintSheet()!;

        // Bản in phải khớp màn hình: mỗi dòng hiển thị là một dòng in, cùng thứ tự.
        sheet.Rows.Count.ShouldBe(ledger.Rows.Count);
        sheet.Columns.Count.ShouldBe(14);
        sheet.Columns[0].Header.ShouldBe("Ngày");
        sheet.Columns[13].Header.ShouldBe("Tổng đã GN");
        sheet.Page.ShouldBe(PrintPageProfiles.ViTriLedger);
    }

    [Fact]
    public void Cot_tinh_san_in_ra_SO_that_chu_khong_phai_chuoi()
    {
        var ledger = new VitriLedgerViewModel(FixtureWithMoney());   // có ít nhất một đợt có tiền
        var sheet = (PrintTableSheet)new ViTriPageViewModel(ledger).BuildPrintSheet()!;

        var totalRow = sheet.Rows[0];                                 // dòng TỔNG CỘNG
        var cell = totalRow.Cells[13];                                // cột Tổng đã GN

        cell.Kind.ShouldBe(PrintCellKind.Money);
        cell.Number.ShouldBe(ledger.Rows[0].Aggregate!.TotalDisbursed);
    }

    [Fact]
    public void Thut_bac_dung_theo_loai_dong()
    {
        var ledger = new VitriLedgerViewModel(Fixture());
        var sheet = (PrintTableSheet)new ViTriPageViewModel(ledger).BuildPrintSheet()!;

        for (var i = 0; i < ledger.Rows.Count; i++)
        {
            var expected = ledger.Rows[i].Kind switch
            {
                RowKind.Total or RowKind.Group => 0,
                RowKind.Package => 1,
                _ => 2,
            };
            sheet.Rows[i].IndentLevel.ShouldBe(expected, $"dòng {i} ({ledger.Rows[i].Kind})");
        }
    }

    [Fact]
    public void Ten_sheet_cua_So_Vi_tri_la_ten_vi_tri()
    {
        var ledger = new VitriLedgerViewModel(Fixture());
        new ViTriPageViewModel(ledger).PrintSheetName.ShouldBe(ledger.Name);
    }

    [Fact]
    public void Ten_sheet_cua_Vi_tri_chua_dat_ten_la_vi_tri_moi()
    {
        var page = new ViTriPageViewModel(new VitriLedgerViewModel(new ViTri { Name = "  " }));

        page.PrintSheetName.ShouldBe("(vị trí mới)");
        page.BuildPrintSheet()!.SheetName.ShouldBe("(vị trí mới)");
    }

    [Fact]
    public void Sheet_So_Vi_tri_co_dung_tieu_de_be_rong_va_canh_cua_14_cot()
    {
        AssertColumns(LedgerSheet(Fixture()),
            new("Ngày", 11, PrintAlign.Center),
            new("Nội dung", 38, PrintAlign.Left),
            new("Nhà thầu / TK", 22, PrintAlign.Left),
            new("Giá trị HĐ", 16, PrintAlign.Right),
            new("Phụ lục", 16, PrintAlign.Right),
            new("Số CT", 11, PrintAlign.Center),
            new("Tạm ứng", 16, PrintAlign.Right),
            new("KLHT", 16, PrintAlign.Right),
            new("Thu hồi", 16, PrintAlign.Right),
            new("Nguồn", 11, PrintAlign.Center),
            new("GN NSNN", 16, PrintAlign.Right),
            new("GN Ngoại hối", 16, PrintAlign.Right),
            new("Số dư TU", 16, PrintAlign.Right),
            new("Tổng đã GN", 16, PrintAlign.Right));
    }

    [Fact]
    public void Dong_TONG_CONG_co_nhan_va_bon_cot_tinh_san()
    {
        var sheet = LedgerSheet(RichFixture());
        var total = sheet.Rows[0];

        FilledHeaders(sheet, 0).ShouldBe(new[] { "Nội dung", "GN NSNN", "GN Ngoại hối", "Số dư TU", "Tổng đã GN" });
        // Màn hình viết chữ "TỔNG CỘNG" thẳng trong XAML chứ không lấy từ Name (Name của dòng này là
        // null), nên bản in phải tự ghi nhãn — thiếu thì dòng được lặp ở đầu mỗi trang mà không có tên.
        CellAt(sheet, 0, "Nội dung").ShouldBe(PrintCell.OfText("TỔNG CỘNG"));
        CellAt(sheet, 0, "GN NSNN").ShouldBe(PrintCell.OfMoney(5_250_000m));
        CellAt(sheet, 0, "GN Ngoại hối").ShouldBe(PrintCell.OfMoney(1_500_000m));
        CellAt(sheet, 0, "Số dư TU").ShouldBe(PrintCell.OfMoney(3_950_000m));
        CellAt(sheet, 0, "Tổng đã GN").ShouldBe(PrintCell.OfMoney(6_750_000m));
        total.IndentLevel.ShouldBe(0);
        total.IsBold.ShouldBeTrue();
        total.Shade.ShouldBe(PrintShade.Light);
    }

    [Fact]
    public void Dong_Nhom_chi_ghi_ten_va_bon_cot_tinh_san()
    {
        var sheet = LedgerSheet(RichFixture());
        var group = sheet.Rows[1];

        FilledHeaders(sheet, 1).ShouldBe(new[] { "Nội dung", "GN NSNN", "GN Ngoại hối", "Số dư TU", "Tổng đã GN" });
        CellAt(sheet, 1, "Nội dung").ShouldBe(PrintCell.OfText("CHI PHÍ XÂY DỰNG"));
        CellAt(sheet, 1, "GN NSNN").ShouldBe(PrintCell.OfMoney(5_250_000m));
        CellAt(sheet, 1, "GN Ngoại hối").ShouldBe(PrintCell.OfMoney(1_500_000m));
        CellAt(sheet, 1, "Số dư TU").ShouldBe(PrintCell.OfMoney(3_950_000m));
        CellAt(sheet, 1, "Tổng đã GN").ShouldBe(PrintCell.OfMoney(6_750_000m));
        group.IndentLevel.ShouldBe(0);
        group.IsBold.ShouldBeTrue();
        group.Shade.ShouldBe(PrintShade.VeryLight);
    }

    [Fact]
    public void Dong_Goi_thau_ghi_ten_nha_thau_gia_tri_hop_dong_va_bon_cot_tinh_san()
    {
        var sheet = LedgerSheet(RichFixture());
        var package = sheet.Rows[2];

        FilledHeaders(sheet, 2).ShouldBe(new[]
        {
            "Nội dung", "Nhà thầu / TK", "Giá trị HĐ", "Phụ lục",
            "GN NSNN", "GN Ngoại hối", "Số dư TU", "Tổng đã GN",
        });
        CellAt(sheet, 2, "Nội dung").ShouldBe(PrintCell.OfText("Gói thầu số 01"));
        CellAt(sheet, 2, "Nhà thầu / TK").ShouldBe(PrintCell.OfText("Công ty An Phát\nTK 0123456789"));
        CellAt(sheet, 2, "Giá trị HĐ").ShouldBe(PrintCell.OfMoney(80_000_000m));
        CellAt(sheet, 2, "Phụ lục").ShouldBe(PrintCell.OfMoney(6_000_000m));
        CellAt(sheet, 2, "GN NSNN").ShouldBe(PrintCell.OfMoney(5_250_000m));
        CellAt(sheet, 2, "GN Ngoại hối").ShouldBe(PrintCell.OfMoney(1_500_000m));
        CellAt(sheet, 2, "Số dư TU").ShouldBe(PrintCell.OfMoney(3_950_000m));
        CellAt(sheet, 2, "Tổng đã GN").ShouldBe(PrintCell.OfMoney(6_750_000m));
        package.IndentLevel.ShouldBe(1);
        package.IsBold.ShouldBeTrue();
        package.Shade.ShouldBe(PrintShade.None);
    }

    [Fact]
    public void Dong_Dot_ghi_ngay_noi_dung_so_chung_tu_ba_cot_tien_va_nguon()
    {
        var sheet = LedgerSheet(RichFixture());

        // Đợt 1 (NSNN): bốn cột tính sẵn, Nhà thầu / TK, Giá trị HĐ, Phụ lục đều để trống.
        FilledHeaders(sheet, 3).ShouldBe(new[] { "Ngày", "Nội dung", "Số CT", "Tạm ứng", "KLHT", "Thu hồi", "Nguồn" });
        CellAt(sheet, 3, "Ngày").ShouldBe(PrintCell.OfDate(new DateOnly(2026, 3, 15)));
        CellAt(sheet, 3, "Nội dung").ShouldBe(PrintCell.OfText("Tạm ứng lần 1"));
        CellAt(sheet, 3, "Số CT").ShouldBe(PrintCell.OfText("CT-01"));
        CellAt(sheet, 3, "Tạm ứng").ShouldBe(PrintCell.OfMoney(3_750_000m));
        CellAt(sheet, 3, "KLHT").ShouldBe(PrintCell.OfMoney(2_000_000m));
        CellAt(sheet, 3, "Thu hồi").ShouldBe(PrintCell.OfMoney(500_000m));
        CellAt(sheet, 3, "Nguồn").ShouldBe(PrintCell.OfText("NSNN"));
        sheet.Rows[3].IndentLevel.ShouldBe(2);
        sheet.Rows[3].IsBold.ShouldBeFalse();
        sheet.Rows[3].Shade.ShouldBe(PrintShade.None);

        // Đợt 2: cột Nguồn viết chữ giống ô chọn nguồn trên màn hình.
        CellAt(sheet, 4, "Ngày").ShouldBe(PrintCell.OfDate(new DateOnly(2027, 3, 20)));
        CellAt(sheet, 4, "Nguồn").ShouldBe(PrintCell.OfText("Ngoại hối"));
    }

    [Theory]
    [InlineData("Công ty A", "TK 123", "Công ty A\nTK 123")]
    [InlineData("Công ty A", null, "Công ty A")]
    [InlineData(null, "TK 123", "TK 123")]
    [InlineData(null, null, null)]
    [InlineData("  ", "", null)]
    public void O_Nha_thau_TK_chi_ghep_phan_co_chu(string? contractor, string? bank, string? expected)
    {
        var viTri = Fixture();
        var package = viTri.CostGroups[0].Packages[0];
        package.ContractorName = contractor;
        package.BankAccount = bank;

        var cell = CellAt(LedgerSheet(viTri), 2, "Nhà thầu / TK");

        cell.ShouldBe(expected is null ? PrintCell.Empty : PrintCell.OfText(expected));
    }

    // ===== Tổng hợp =====

    [Fact]
    public void Tong_hop_in_ra_dung_so_dong_dang_hien()
    {
        var inner = new ProjectTongHopViewModel();
        inner.Refresh(new[] { Fixture() });
        var sheet = (PrintTableSheet)new ProjectTongHopPageViewModel(inner).BuildPrintSheet()!;

        sheet.SheetName.ShouldBe("Tổng hợp");
        sheet.Rows.Count.ShouldBe(inner.Rows.Count);
        sheet.Page.ShouldBe(PrintPageProfiles.ProjectTongHop);
    }

    [Fact]
    public void Tong_hop_co_dung_tieu_de_be_rong_va_canh_cua_13_cot()
    {
        AssertColumns(TongHopSheet(Fixture()),
            new("STT", 5, PrintAlign.Center),
            new("Tên vị trí", 30, PrintAlign.Left),
            new("Đơn vị thi công", 24, PrintAlign.Left),
            new("Giá trị HĐ, PL", 16, PrintAlign.Right),
            new("Tạm ứng", 16, PrintAlign.Right),
            new("TT KLHT", 16, PrintAlign.Right),
            new("Thu hồi", 16, PrintAlign.Right),
            new("TT cho nhà thầu", 16, PrintAlign.Right),
            new("Tạm ứng chưa thu hồi", 16, PrintAlign.Right),
            new("GN Dự phòng NSNN", 16, PrintAlign.Right),
            new("GN Quỹ DTNH", 16, PrintAlign.Right),
            new("Tổng đã giải ngân", 16, PrintAlign.Right),
            new("Quyết toán năm", 28, PrintAlign.Left));
    }

    private const string RichYearlyText = "2026: NSNN 5.250.000  |  2027: DTNH 1.500.000";

    [Fact]
    public void Tong_hop_ghi_tien_thanh_SO_that_con_chu_va_nhan_giu_nguyen()
    {
        var sheet = TongHopSheet(RichFixture());

        sheet.Rows.Count.ShouldBe(2);   // một Vị trí + TỔNG CỘNG

        CellAt(sheet, 0, "STT").ShouldBe(PrintCell.OfText("1"));
        CellAt(sheet, 0, "Tên vị trí").ShouldBe(PrintCell.OfText("Vị trí 1 — Nhà điều hành"));
        CellAt(sheet, 0, "Đơn vị thi công").ShouldBe(PrintCell.OfText("Công ty An Phát"));
        CellAt(sheet, 0, "Quyết toán năm").ShouldBe(PrintCell.OfText(RichYearlyText));
        AssertTongHopMoney(sheet, 0);
        sheet.Rows[0].IsBold.ShouldBeFalse();

        // Dòng TỔNG CỘNG: STT và Đơn vị thi công để trống; cùng số với dòng trên vì chỉ có một Vị trí.
        CellAt(sheet, 1, "Tên vị trí").ShouldBe(PrintCell.OfText("TỔNG CỘNG"));
        IsBlank(CellAt(sheet, 1, "STT")).ShouldBeTrue();
        IsBlank(CellAt(sheet, 1, "Đơn vị thi công")).ShouldBeTrue();
        CellAt(sheet, 1, "Quyết toán năm").ShouldBe(PrintCell.OfText(RichYearlyText));
        AssertTongHopMoney(sheet, 1);
        sheet.Rows[1].IsBold.ShouldBeTrue();
    }

    // Dòng TỔNG CỘNG của Tổng hợp để trống STT và Đơn vị thi công dưới dạng PrintCell.OfText(""), KHÔNG phải PrintCell.Empty (bản in
    // lấy thẳng chuỗi rỗng của màn hình). FilledHeaders phải coi hai ô đó là trống, tức nó đi qua IsBlank chứ không so với
    // PrintCell.Empty. Đây là chỗ duy nhất chạy FilledHeaders trên ô chữ rỗng thật: các test sổ Vị trí đều dùng RichFixture,
    // đủ mọi trường, nên so với PrintCell.Empty vẫn đúng ở đó và che mất lỗi.
    [Fact]
    public void Dong_TONG_CONG_cua_Tong_hop_chi_ghi_o_co_chu()
    {
        var sheet = TongHopSheet(RichFixture());

        FilledHeaders(sheet, 1).ShouldBe(new[]
        {
            "Tên vị trí", "Giá trị HĐ, PL", "Tạm ứng", "TT KLHT", "Thu hồi", "TT cho nhà thầu",
            "Tạm ứng chưa thu hồi", "GN Dự phòng NSNN", "GN Quỹ DTNH", "Tổng đã giải ngân", "Quyết toán năm",
        });
    }

    /// Chín cột tiền của Tổng hợp là SỐ thật (kiểu Money) chứ không phải chuỗi "5.250.000" của màn
    /// hình, để chọn một vùng trong Excel là thấy tổng. "TT cho nhà thầu" và "Tổng đã giải ngân" cùng
    /// lấy từ một số (TotalDisbursed) — đúng như màn hình (SummaryDisplayRow.From); bản in giữ nguyên.
    private static void AssertTongHopMoney(PrintTableSheet sheet, int row)
    {
        CellAt(sheet, row, "Giá trị HĐ, PL").ShouldBe(PrintCell.OfMoney(86_000_000m));
        CellAt(sheet, row, "Tạm ứng").ShouldBe(PrintCell.OfMoney(4_750_000m));
        CellAt(sheet, row, "TT KLHT").ShouldBe(PrintCell.OfMoney(2_800_000m));
        CellAt(sheet, row, "Thu hồi").ShouldBe(PrintCell.OfMoney(800_000m));
        CellAt(sheet, row, "TT cho nhà thầu").ShouldBe(PrintCell.OfMoney(6_750_000m));
        CellAt(sheet, row, "Tạm ứng chưa thu hồi").ShouldBe(PrintCell.OfMoney(3_950_000m));
        CellAt(sheet, row, "GN Dự phòng NSNN").ShouldBe(PrintCell.OfMoney(5_250_000m));
        CellAt(sheet, row, "GN Quỹ DTNH").ShouldBe(PrintCell.OfMoney(1_500_000m));
        CellAt(sheet, row, "Tổng đã giải ngân").ShouldBe(PrintCell.OfMoney(6_750_000m));
    }

    [Fact]
    public void Tong_hop_dong_dung_tay_khong_co_so_nguon_thi_o_tien_de_trong()
    {
        // Dòng không đi qua SummaryDisplayRow.From thì không có Source. Bản in không đọc ngược chuỗi
        // "1.000" thành số, nên ô tiền để trống; nhãn và chữ vẫn in.
        var inner = new ProjectTongHopViewModel();
        inner.Rows.Add(new SummaryDisplayRow { Ordinal = "1", Label = "Dòng dựng tay", Advance = "1.000" });
        var sheet = (PrintTableSheet)new ProjectTongHopPageViewModel(inner).BuildPrintSheet()!;

        CellAt(sheet, 0, "Tên vị trí").ShouldBe(PrintCell.OfText("Dòng dựng tay"));
        CellAt(sheet, 0, "Tạm ứng").ShouldBe(PrintCell.Empty);
    }

    // ===== Quyết toán năm =====

    [Fact]
    public void Quyet_toan_nam_in_ra_bon_cot_va_dung_so_dong()
    {
        var inner = new YearSettlementViewModel();
        inner.Refresh(new[] { Fixture() });
        var sheet = (PrintTableSheet)new YearSettlementPageViewModel(inner).BuildPrintSheet()!;

        sheet.SheetName.ShouldBe("Quyết toán năm");
        sheet.Columns.Count.ShouldBe(4);
        sheet.Rows.Count.ShouldBe(inner.Rows.Count);
        sheet.Page.ShouldBe(PrintPageProfiles.YearSettlement);
    }

    [Fact]
    public void Quyet_toan_nam_co_dung_tieu_de_be_rong_va_canh_cua_4_cot()
    {
        AssertColumns(QuyetToanSheet(Fixture()),
            new("Năm", 12, PrintAlign.Center),
            new("NSNN", 18, PrintAlign.Right),
            new("Ngoại hối", 18, PrintAlign.Right),
            new("Tổng", 18, PrintAlign.Right));
    }

    [Fact]
    public void Quyet_toan_nam_ghi_tien_thanh_SO_that_con_nhan_nam_giu_nguyen()
    {
        var sheet = QuyetToanSheet(RichFixture());

        sheet.Rows.Count.ShouldBe(3);   // hai năm tài chính + TỔNG CỘNG

        CellAt(sheet, 0, "Năm").ShouldBe(PrintCell.OfText("Quyết toán năm 2026"));
        CellAt(sheet, 0, "NSNN").ShouldBe(PrintCell.OfMoney(5_250_000m));
        CellAt(sheet, 0, "Ngoại hối").ShouldBe(PrintCell.Empty);          // 0 ra ô trống
        CellAt(sheet, 0, "Tổng").ShouldBe(PrintCell.OfMoney(5_250_000m));

        CellAt(sheet, 1, "Năm").ShouldBe(PrintCell.OfText("Quyết toán năm 2027"));
        CellAt(sheet, 1, "NSNN").ShouldBe(PrintCell.Empty);
        CellAt(sheet, 1, "Ngoại hối").ShouldBe(PrintCell.OfMoney(1_500_000m));
        CellAt(sheet, 1, "Tổng").ShouldBe(PrintCell.OfMoney(1_500_000m));

        CellAt(sheet, 2, "Năm").ShouldBe(PrintCell.OfText("TỔNG CỘNG"));
        CellAt(sheet, 2, "NSNN").ShouldBe(PrintCell.OfMoney(5_250_000m));
        CellAt(sheet, 2, "Ngoại hối").ShouldBe(PrintCell.OfMoney(1_500_000m));
        CellAt(sheet, 2, "Tổng").ShouldBe(PrintCell.OfMoney(6_750_000m));

        sheet.Rows.Select(r => r.IsBold).ShouldBe(new[] { false, false, true });
    }

    [Fact]
    public void Quyet_toan_nam_dong_dung_tay_khong_co_so_thi_o_tien_de_trong()
    {
        // Như Tổng hợp: dòng dựng tay chỉ có chuỗi "1.000", không có số để ghi.
        var inner = new YearSettlementViewModel();
        inner.Rows.Add(new YearSettlementRow { YearLabel = "Dòng dựng tay", NSNN = "1.000", Total = "1.000" });
        var sheet = (PrintTableSheet)new YearSettlementPageViewModel(inner).BuildPrintSheet()!;

        CellAt(sheet, 0, "Năm").ShouldBe(PrintCell.OfText("Dòng dựng tay"));
        foreach (var header in new[] { "NSNN", "Ngoại hối", "Tổng" })
            CellAt(sheet, 0, header).ShouldBe(PrintCell.Empty, header);
    }

    // ===== Cả ba sheet =====

    [Fact]
    public void Tien_bang_0_ra_o_trong_chu_khong_phai_so_0_o_ca_ba_sheet()
    {
        // Fixture(): mọi ô tiền đều 0, nên mọi cột tiền của mọi dòng phải là ô trống (đúng quy ước
        // MoneyText.Format(0m) trên màn hình), không phải ô số 0.
        AssertMoneyColumnsAreEmpty(LedgerSheet(Fixture()),
            "Giá trị HĐ", "Phụ lục", "Tạm ứng", "KLHT", "Thu hồi",
            "GN NSNN", "GN Ngoại hối", "Số dư TU", "Tổng đã GN");
        AssertMoneyColumnsAreEmpty(TongHopSheet(Fixture()),
            "Giá trị HĐ, PL", "Tạm ứng", "TT KLHT", "Thu hồi", "TT cho nhà thầu",
            "Tạm ứng chưa thu hồi", "GN Dự phòng NSNN", "GN Quỹ DTNH", "Tổng đã giải ngân");
        AssertMoneyColumnsAreEmpty(QuyetToanSheet(Fixture()), "NSNN", "Ngoại hối", "Tổng");
    }

    private static void AssertMoneyColumnsAreEmpty(PrintTableSheet sheet, params string[] moneyColumns)
    {
        sheet.Rows.Count.ShouldBeGreaterThan(0);
        for (var row = 0; row < sheet.Rows.Count; row++)
            foreach (var header in moneyColumns)
                CellAt(sheet, row, header).ShouldBe(PrintCell.Empty, $"{sheet.SheetName}, dòng {row}, cột {header}");
    }

    // ===== Chỗ chạm với bộ ghi =====

    [Fact]
    public void Sheet_cua_ba_man_ghi_duoc_bang_bo_ghi_that_ke_ca_dot_trong_Noi_dung()
    {
        var viTri = RichFixture();
        // Một đợt người dùng vừa thêm và chưa gõ gì: Nội dung và Số CT cùng trống, ô chữ duy nhất còn lại
        // ("NSNN") nằm ở cột căn giữa mà dòng vẫn mang bậc thụt 2. Bộ ghi phải bỏ qua bậc đó chứ không
        // ném ArgumentException làm hỏng cả workbook.
        viTri.CostGroups[0].Packages[0].Dots.Add(
            new Dot { SortOrder = 2, Date = new DateOnly(2026, 5, 1), Fund = Fund.NSNN });

        var tongHop = new ProjectTongHopViewModel();
        tongHop.Refresh(new[] { viTri });
        var quyetToan = new YearSettlementViewModel();
        quyetToan.Refresh(new[] { viTri });
        var sheets = new[]
        {
            new ProjectTongHopPageViewModel(tongHop).BuildPrintSheet()!,
            new ViTriPageViewModel(new VitriLedgerViewModel(viTri)).BuildPrintSheet()!,
            new YearSettlementPageViewModel(quyetToan).BuildPrintSheet()!,
        };

        using var stream = new MemoryStream();
        new PrintWorkbookWriter().Write(sheets, stream, new PrintFooterInfo("Dự án thử", null));
        stream.Position = 0;
        using var workbook = new XLWorkbook(stream);

        workbook.Worksheets.Select(w => w.Name)
            .ShouldBe(new[] { "Tổng hợp", "Vị trí 1 — Nhà điều hành", "Quyết toán năm" });

        // Ở cả ba sheet tiền ra tệp là số thật, để chọn một vùng trong Excel là thấy tổng.
        AssertNumber(workbook, "Tổng hợp", row: 2, column: 4, expected: 86_000_000);                   // Giá trị HĐ, PL
        AssertNumber(workbook, "Vị trí 1 — Nhà điều hành", row: 2, column: 14, expected: 6_750_000);   // TỔNG CỘNG · Tổng đã GN
        AssertNumber(workbook, "Quyết toán năm", row: 2, column: 2, expected: 5_250_000);              // năm 2026 · NSNN
    }

    private static void AssertNumber(XLWorkbook workbook, string sheet, int row, int column, double expected)
    {
        var cell = workbook.Worksheet(sheet).Cell(row, column);
        cell.DataType.ShouldBe(XLDataType.Number, $"{sheet}, dòng {row}, cột {column}");
        cell.GetDouble().ShouldBe(expected, $"{sheet}, dòng {row}, cột {column}");
    }
}
