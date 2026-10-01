using ClosedXML.Excel;
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Printing;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Desktop.ViewModels.Finance.Forms;
using ConstructionFinance.Domain.Finance;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

/// Workbook gom sheet cho "In toàn bộ" và ủy quyền "In sheet này" cho tab đang mở, cùng ba trang biểu mẫu
/// khai báo nội dung in qua IPrintablePage. Workbook dựng bằng đường nạp thật của app (LoadAsync), không
/// chọc thẳng vào ViTriPages.
public class PrintWorkbookTests
{
    private static readonly string[] AllSheetNames =
    {
        "Tổng hợp", "Vị trí 1", "Vị trí 2", "Quyết toán năm",
        "Thông tri", "KS chi", "GĐNTT",
    };

    // ===== Dữ liệu mẫu =====

    /// Một nhóm / một gói / hai đợt, mọi ô tiền đều 0: đợt 1 NSNN, đợt 2 Ngoại hối. Cùng hình dạng với
    /// Fixture() của PrintablePageTests, thêm đợt Ngoại hối vì tab GĐNTT chỉ in được khi chọn được một đợt
    /// Ngoại hối. Ngày đặt cố định để năm tài chính của hai đợt đoán trước được.
    private static ViTri Fixture(string name, int sortOrder)
    {
        var viTri = new ViTri { Name = name, SortOrder = sortOrder };
        var group = new CostGroup { Name = "CHI PHÍ XÂY DỰNG", SortOrder = 0 };
        var package = new Package { Name = "Gói thầu số 01", SortOrder = 0 };
        package.Dots.Add(new Dot { Content = "Đợt 1", SortOrder = 0, Fund = Fund.NSNN, Date = new DateOnly(2026, 3, 15) });
        package.Dots.Add(new Dot { Content = "Đợt 2", SortOrder = 1, Fund = Fund.NgoaiHoi, Date = new DateOnly(2026, 3, 20) });
        group.Packages.Add(package);
        viTri.CostGroups.Add(group);
        return viTri;
    }

    private static Project ProjectWithTwoViTri(string? code = "DA-01") => new()
    {
        Id = 7,
        Code = code,
        Name = "Dự án A",
        ViTris = { Fixture("Vị trí 1", 0), Fixture("Vị trí 2", 1) },
    };

    /// Workbook với sáu service giả. tree = null nghĩa là CSDL chưa có dự án nào.
    private static FinanceWorkbookViewModel CreateWorkbook(Project? tree)
    {
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.GetAllSummariesAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(Array.Empty<FinanceProjectSummary>());
        service.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(tree is null
                   ? new List<Project>()
                   : new List<Project> { new() { Id = tree.Id, Code = tree.Code, Name = tree.Name } });
        if (tree is not null)
            service.Setup(s => s.GetTreeAsync(tree.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tree);

        var summaryVm = new FinanceSummaryViewModel(
            service.Object, Mock.Of<IFinanceImportService>(),
            Mock.Of<IFinanceExportService>(), Mock.Of<IDialogService>());

        var companyInfo = new Mock<ICompanyInfoService>();
        companyInfo.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new CompanyInfoDto(null, null, null, null, null, null, null, null,
                       DonViCapTren: "Sở Tài chính", DonViCoSo: "Ban QLDA số 1"));

        return new FinanceWorkbookViewModel(
            service.Object, new SummaryPageViewModel(summaryVm), Mock.Of<IDialogService>(),
            new ServiceCollection().BuildServiceProvider(),
            Mock.Of<IFormExcelExporter>(), companyInfo.Object);
    }

    /// Nạp dự án bằng đúng đường app dùng: LoadAsync chọn dự án đầu rồi nạp cây và dựng tab. Mọi mock trả
    /// Task đã hoàn tất nên cả chuỗi (kể cả phần fire-and-forget trong OnSelectedProjectChanged) chạy đồng
    /// bộ tới cùng, nên chờ ở đây không có gì để chặn.
    private static FinanceWorkbookViewModel LoadWorkbook(Project? tree)
    {
        var workbook = CreateWorkbook(tree);
        workbook.LoadAsync().GetAwaiter().GetResult();
        return workbook;
    }

    /// Dự án hai Vị trí ("Vị trí 1", "Vị trí 2"); tab GĐNTT đã chọn một gói thầu (đợt Ngoại hối của Vị trí 1)
    /// nên in được.
    private static FinanceWorkbookViewModel BuildWorkbookWithTwoViTri()
    {
        var workbook = LoadWorkbook(ProjectWithTwoViTri());
        var gdntt = workbook.ViTriPages.OfType<GdnttFormPageViewModel>().Single();
        gdntt.Inner.Picker.Items[0].IsSelected = true;
        gdntt.Inner.CanExport.ShouldBeTrue("tiền đề của helper: tab GĐNTT đã có gói thầu để in");
        return workbook;
    }

    /// Cùng dự án nhưng tab GĐNTT chưa chọn gói thầu nào, tức GdnttFormViewModel.CanExport == false.
    private static FinanceWorkbookViewModel BuildWorkbookWhereGdnttHasNoPackage()
    {
        var workbook = LoadWorkbook(ProjectWithTwoViTri());
        var gdntt = workbook.ViTriPages.OfType<GdnttFormPageViewModel>().Single();
        gdntt.Inner.CanExport.ShouldBeFalse("tiền đề của helper: tab GĐNTT có mặt nhưng chưa chọn gói thầu");
        return workbook;
    }

    /// Ô nằm dưới cột có tiêu đề `header` — đọc như đọc tờ giấy in, không phụ thuộc số thứ tự cột.
    private static PrintCell CellAt(PrintTableSheet sheet, int row, string header)
    {
        var column = sheet.Columns.Select((c, i) => (c, i)).Single(x => x.c.Header == header).i;
        return sheet.Rows[row].Cells[column];
    }

    // ===== "In toàn bộ" gom sheet =====

    [Fact]
    public void In_toan_bo_ra_dung_thu_tu_sheet_nhu_Mau()
    {
        var workbook = BuildWorkbookWithTwoViTri();          // helper riêng của tệp test này

        var names = workbook.BuildAllSheets().Select(s => s.SheetName).ToList();

        names.ShouldBe(new[]
        {
            "Tổng hợp", "Vị trí 1", "Vị trí 2", "Quyết toán năm",
            "Thông tri", "KS chi", "GĐNTT",
        });
    }

    [Fact]
    public void In_sheet_nay_uy_quyen_cho_tab_dang_mo()
    {
        var workbook = BuildWorkbookWithTwoViTri();
        workbook.SelectedViTriPage = workbook.ViTriPages.OfType<ViTriPageViewModel>().Last();

        ((IPrintablePage)workbook).PrintSheetName.ShouldBe("Vị trí 2");
    }

    [Fact]
    public void Doi_tab_thi_bao_PrintSheetName_da_doi()
    {
        var workbook = BuildWorkbookWithTwoViTri();
        var changed = new List<string>();
        workbook.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");

        workbook.SelectedViTriPage = workbook.ViTriPages.OfType<ViTriPageViewModel>().Last();

        // Không có tín hiệu này thì nút In ở khung chung không cập nhật được trạng thái.
        changed.ShouldContain(nameof(IPrintablePage.PrintSheetName));
    }

    [Fact]
    public void GDNTT_chua_chon_goi_thau_thi_bi_bo_qua_khi_in_toan_bo()
    {
        var workbook = BuildWorkbookWhereGdnttHasNoPackage();

        var names = workbook.BuildAllSheets().Select(s => s.SheetName).ToList();

        names.ShouldNotContain("GĐNTT");
        names.ShouldContain("Thông tri");     // các sheet khác vẫn ra bình thường
    }

    [Fact]
    public void In_sheet_nay_in_dung_sheet_cua_tab_dang_mo()
    {
        var workbook = BuildWorkbookWithTwoViTri();
        workbook.SelectedViTriPage = workbook.ViTriPages.OfType<ViTriPageViewModel>().Last();

        var sheet = ((IPrintablePage)workbook).BuildPrintSheet();

        sheet.ShouldNotBeNull();
        sheet.SheetName.ShouldBe("Vị trí 2");
    }

    /// Một trang không in được đứng GIỮA dải tab. GĐNTT cũng trả null nhưng đứng cuối, nên chỉ riêng nó thì
    /// không thử được chỗ này: một BuildAllSheets dừng ở trang null đầu tiên vẫn qua được test GĐNTT.
    private sealed class TrangKhongIn : WorkbookPageViewModel
    {
        public override object Content => new object();
    }

    [Fact]
    public void Trang_giua_dai_tab_khong_in_duoc_thi_bi_bo_qua_con_cac_trang_sau_van_ra()
    {
        var workbook = BuildWorkbookWithTwoViTri();
        workbook.ViTriPages.Insert(1, new TrangKhongIn());

        var names = workbook.BuildAllSheets().Select(s => s.SheetName).ToList();

        names.ShouldBe(AllSheetNames);
    }

    [Fact]
    public void Chua_co_du_an_thi_khong_in_duoc_gi()
    {
        var workbook = LoadWorkbook(null);
        var printable = (IPrintablePage)workbook;

        printable.PrintSheetName.ShouldBeNull();
        printable.BuildPrintSheet().ShouldBeNull();
        workbook.BuildAllSheets().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("DA-01", "DA-01")]
    [InlineData(null, "Dự án A")]
    [InlineData("", "Dự án A")]
    public void Ten_workbook_la_ma_du_an_neu_co_khong_thi_la_ten_du_an(string? code, string expected)
    {
        var workbook = LoadWorkbook(ProjectWithTwoViTri(code));

        ((IPrintableWorkbook)workbook).PrintWorkbookName.ShouldBe(expected);
    }

    [Fact]
    public void In_toan_bo_ghi_duoc_thanh_mot_tep_xlsx_du_bay_sheet()
    {
        var workbook = BuildWorkbookWithTwoViTri();

        // Qua bộ ghi thật: một sheet biểu mẫu có FormPayload rỗng (hoặc hai thành viên) sẽ làm bộ ghi ném
        // ArgumentException, hỏng cả workbook. Đây là chỗ canh ba trang biểu mẫu trao đúng model cho bộ ghi.
        using var stream = new MemoryStream();
        new PrintWorkbookWriter().Write(workbook.BuildAllSheets(), stream, new PrintFooterInfo(workbook.PrintWorkbookName, null));
        stream.Position = 0;
        using var xlsx = new XLWorkbook(stream);

        xlsx.Worksheets.Select(w => w.Name).ShouldBe(AllSheetNames);
    }

    // ===== "In toàn bộ" phải in số MỚI NHẤT =====
    //
    // Các tab Tổng hợp, Quyết toán năm và ba biểu mẫu chỉ tự làm mới khi được CHỌN và ở lần sửa ĐẦU TIÊN của một
    // sổ (lúc IsDirty lật false→true). Đứng ở tab Vị trí, sửa lần thứ hai trở đi thì không có gì làm mới chúng,
    // nên "In toàn bộ" phải tự làm mới trước khi gom sheet.
    //
    // Các test này CỐ Ý không gọi hàm làm mới nào: hễ gọi là chính test tự cấp cái bảo đảm mà app không có, và
    // test chứng minh số không. Chúng đi đúng đường của người dùng (gõ vào ô → sổ Vị trí → workbook).

    /// Mở dự án, đứng ở tab Vị trí 1, sửa cùng một ô Tạm ứng hai lần (1.000.000 rồi 2.500.000) rồi bấm "In toàn bộ".
    /// Thông tri được tích sẵn đợt NSNN để có số mà in.
    private static IReadOnlyList<PrintSheet> PrintAllAfterEditingTheSameCellTwice()
    {
        var workbook = BuildWorkbookWithTwoViTri();
        var viTri1 = workbook.ViTriPages.OfType<ViTriPageViewModel>().First();
        workbook.SelectedViTriPage = viTri1;
        workbook.ViTriPages.OfType<ThongTriFormPageViewModel>().Single().Inner.Picker.Items[0].IsSelected = true;

        var dot = viTri1.Ledger.Rows.Single(r => r.Kind == RowKind.Entry && r.Fund == Fund.NSNN);
        dot.AdvanceText = "1.000.000";
        dot.AdvanceText = "2.500.000";

        return workbook.BuildAllSheets();
    }

    [Fact]
    public void In_toan_bo_Tong_hop_mang_so_cua_lan_sua_thu_hai()
    {
        var tongHop = PrintAllAfterEditingTheSameCellTwice()
            .OfType<PrintTableSheet>().Single(s => s.SheetName == "Tổng hợp");

        tongHop.Rows.Count.ShouldBe(3);                                         // Vị trí 1, Vị trí 2, TỔNG CỘNG
        CellAt(tongHop, 0, "Tạm ứng").ShouldBe(PrintCell.OfMoney(2_500_000m), "dòng Vị trí 1");
        CellAt(tongHop, 2, "Tên vị trí").ShouldBe(PrintCell.OfText("TỔNG CỘNG"));
        CellAt(tongHop, 2, "Tạm ứng").ShouldBe(PrintCell.OfMoney(2_500_000m), "dòng TỔNG CỘNG");
    }

    [Fact]
    public void In_toan_bo_Quyet_toan_nam_mang_so_cua_lan_sua_thu_hai()
    {
        var quyetToan = PrintAllAfterEditingTheSameCellTwice()
            .OfType<PrintTableSheet>().Single(s => s.SheetName == "Quyết toán năm");

        quyetToan.Rows.Count.ShouldBe(2);                                       // năm tài chính 2026, TỔNG CỘNG
        CellAt(quyetToan, 0, "NSNN").ShouldBe(PrintCell.OfMoney(2_500_000m), "dòng năm 2026");
        CellAt(quyetToan, 1, "Năm").ShouldBe(PrintCell.OfText("TỔNG CỘNG"));
        CellAt(quyetToan, 1, "Tổng").ShouldBe(PrintCell.OfMoney(2_500_000m), "dòng TỔNG CỘNG");
    }

    [Fact]
    public void In_toan_bo_Thong_tri_mang_so_cua_lan_sua_thu_hai()
    {
        var thongTri = PrintAllAfterEditingTheSameCellTwice()
            .OfType<PrintFormSheet>().Single(s => s.SheetName == "Thông tri");

        thongTri.Form.ThongTri.ShouldNotBeNull().Total.ShouldBe(2_500_000m);
    }

    // ===== Trang GĐNTT: tên sheet và nội dung đi cùng nhau =====
    //
    // Nút In bật theo PrintSheetName. Nên một trang có tên sheet mà BuildPrintSheet() trả null sẽ làm nút sáng
    // lên rồi nhận về null: tên khác null PHẢI nghĩa là có sheet thật.

    private static FormDotRow Row(
        int dotIndex, Fund fund, decimal advance, decimal klht = 0m, decimal recovered = 0m,
        int packageIndex = 0, string packageName = "Gói A") =>
        new(new FormDotKey(0, 0, packageIndex, dotIndex), "Vị trí 1", "CHI PHÍ XÂY DỰNG", packageName,
            "Cty XL A", "123456789", 50_000_000_000m, 2_000_000_000m, new DateOnly(2026, 3, 1), "Đợt", "CT01",
            fund, advance, klht, recovered, Disbursed: advance + klht - recovered);

    private static GdnttFormViewModel NewGdntt(params FormDotRow[] rows)
    {
        var inner = new GdnttFormViewModel(Mock.Of<IFormExcelExporter>(), new FakeDialogService());
        inner.Refresh(rows, Array.Empty<LedgerRowViewModel>(), new GdnttDeclaration(),
            new FormOrgInfo("Sở Tài chính", "Ban QLDA số 1"), "Dự án A");
        return inner;
    }

    [Fact]
    public void Trang_GDNTT_chua_chon_goi_thau_thi_ca_ten_lan_noi_dung_deu_null()
    {
        var inner = NewGdntt(Row(0, Fund.NgoaiHoi, 10_000_000m));
        var page = new GdnttFormPageViewModel(inner);

        inner.CanExport.ShouldBeFalse("tiền đề: chưa tích đợt nào");
        page.PrintSheetName.ShouldBeNull();
        page.BuildPrintSheet().ShouldBeNull();
    }

    [Fact]
    public void Trang_GDNTT_chon_dot_cua_hai_goi_thi_ca_ten_lan_noi_dung_deu_null()
    {
        var inner = NewGdntt(
            Row(0, Fund.NgoaiHoi, 10_000_000m),
            Row(0, Fund.NgoaiHoi, 20_000_000m, packageIndex: 1, packageName: "Gói B"));
        inner.Picker.SelectAllCommand.Execute(null);
        var page = new GdnttFormPageViewModel(inner);

        inner.CanExport.ShouldBeFalse("tiền đề: một giấy đề nghị chỉ dựng cho một gói thầu");
        page.PrintSheetName.ShouldBeNull();
        page.BuildPrintSheet().ShouldBeNull();
    }

    [Fact]
    public void Trang_GDNTT_chon_mot_goi_thi_in_ra_sheet_GDNTT_voi_model_dang_hien()
    {
        var inner = NewGdntt(Row(0, Fund.NgoaiHoi, 10_000_000m), Row(1, Fund.NgoaiHoi, 30_000_000m));
        inner.Picker.Items[1].IsSelected = true;
        var page = new GdnttFormPageViewModel(inner);

        page.PrintSheetName.ShouldBe("GĐNTT");
        var sheet = page.BuildPrintSheet().ShouldBeOfType<PrintFormSheet>();
        sheet.SheetName.ShouldBe("GĐNTT");
        sheet.Page.ShouldBeSameAs(PrintPageProfiles.Gdntt);
        sheet.Form.ThongTri.ShouldBeNull();
        sheet.Form.KsChi.ShouldBeNull();
        var form = sheet.Form.Gdntt.ShouldNotBeNull();
        form.PackageName.ShouldBe("Gói A");
        form.RequestedThisPeriod.ShouldBe(30_000_000m);
    }

    // ===== Thông tri và KS chi =====

    [Fact]
    public void Thong_tri_in_ra_sheet_Thong_tri_voi_model_dang_hien()
    {
        var inner = new ThongTriFormViewModel(Mock.Of<IFormExcelExporter>(), new FakeDialogService());
        inner.Refresh(
            new[] { Row(0, Fund.NSNN, 10_000_000m), Row(1, Fund.NSNN, 30_000_000m) },
            Array.Empty<LedgerRowViewModel>(), new ThongTriDeclaration { SoThongTri = "12/TT" },
            new FormOrgInfo("Sở Tài chính", "Ban QLDA số 1"), "Dự án A");
        inner.Picker.SelectAllCommand.Execute(null);
        var page = new ThongTriFormPageViewModel(inner);

        page.PrintSheetName.ShouldBe("Thông tri");
        var sheet = page.BuildPrintSheet().ShouldBeOfType<PrintFormSheet>();
        sheet.SheetName.ShouldBe("Thông tri");
        // ShouldBeSameAs chứ không ShouldBe: hồ sơ Thông tri và KS chi bằng nhau theo GIÁ TRỊ (cả hai dọc, ép
        // một trang ngang), nên so giá trị không nhận ra hai trang dùng nhầm hồ sơ của nhau.
        sheet.Page.ShouldBeSameAs(PrintPageProfiles.ThongTri);
        sheet.Form.KsChi.ShouldBeNull();
        sheet.Form.Gdntt.ShouldBeNull();
        var form = sheet.Form.ThongTri.ShouldNotBeNull();
        form.Total.ShouldBe(40_000_000m);
        form.SoThongTri.ShouldBe("12/TT");
    }

    [Fact]
    public void KS_chi_in_ra_sheet_KS_chi_voi_model_dang_hien()
    {
        var inner = new KsChiFormViewModel(Mock.Of<IFormExcelExporter>(), new FakeDialogService());
        inner.Refresh(
            new[] { Row(0, Fund.NSNN, 10_000_000m), Row(1, Fund.NgoaiHoi, 0m, klht: 200_000_000m, recovered: 5_000_000m) },
            Array.Empty<LedgerRowViewModel>(), new KsChiDeclaration(), "Dự án A");
        inner.Picker.SelectAllCommand.Execute(null);
        var page = new KsChiFormPageViewModel(inner);

        page.PrintSheetName.ShouldBe("KS chi");
        var sheet = page.BuildPrintSheet().ShouldBeOfType<PrintFormSheet>();
        sheet.SheetName.ShouldBe("KS chi");
        sheet.Page.ShouldBeSameAs(PrintPageProfiles.KsChi);   // ShouldBeSameAs vì sao: xem test Thông tri
        sheet.Form.ThongTri.ShouldBeNull();
        sheet.Form.Gdntt.ShouldBeNull();
        var form = sheet.Form.KsChi.ShouldNotBeNull();
        form.Accepted.Total.ShouldBe(210_000_000m);
        form.PaidToBeneficiary.Total.ShouldBe(205_000_000m);
    }

    // ===== Trang GĐNTT qua đường ủy quyền của workbook =====

    [Fact]
    public void Tab_GDNTT_chua_chon_goi_thau_thi_workbook_khong_in_duoc_sheet_nay()
    {
        var workbook = BuildWorkbookWhereGdnttHasNoPackage();
        workbook.SelectedViTriPage = workbook.ViTriPages.OfType<GdnttFormPageViewModel>().Single();

        var printable = (IPrintablePage)workbook;

        printable.PrintSheetName.ShouldBeNull();
        printable.BuildPrintSheet().ShouldBeNull();
    }

    [Fact]
    public void Tab_GDNTT_da_chon_goi_thau_thi_workbook_in_duoc_sheet_nay()
    {
        var workbook = BuildWorkbookWithTwoViTri();
        workbook.SelectedViTriPage = workbook.ViTriPages.OfType<GdnttFormPageViewModel>().Single();

        var printable = (IPrintablePage)workbook;

        printable.PrintSheetName.ShouldBe("GĐNTT");
        printable.BuildPrintSheet().ShouldBeOfType<PrintFormSheet>().SheetName.ShouldBe("GĐNTT");
    }
}
