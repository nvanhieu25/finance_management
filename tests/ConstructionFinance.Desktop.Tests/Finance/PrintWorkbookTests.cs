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

    /// Nạp dự án bằng đúng đường app dùng: LoadAsync chọn dự án đầu rồi nạp cây và dựng tab.
    ///
    /// BẤT BIẾN CỦA CẢ TỆP NÀY: mọi mock trong CreateWorkbook trả một Task ĐÃ HOÀN TẤT (ReturnsAsync), nên cả chuỗi,
    /// kể cả phần fire-and-forget LoadSelectedProjectTreeAsync mà OnSelectedProjectChanged tự bắn ra, chạy đồng bộ tới
    /// cùng ngay trong LoadAsync; vì thế chờ bằng GetAwaiter().GetResult() ở đây không chặn gì và khi nó trả về các tab
    /// đã dựng xong. Mọi test trong tệp (kể cả test `await LoadAsync()` trực tiếp) dựa vào điều đó. Một mock trả Task
    /// bất đồng bộ THẬT (Task.Delay, Task.Yield, I/O) sẽ làm phần fire-and-forget chạy tiếp sau khi LoadAsync đã trả:
    /// test sẽ xanh đỏ tùy thời điểm thay vì đỏ rõ ràng, nên có kiểm tra ngay dưới đây. Đừng "sửa" bằng vòng chờ; hãy giữ
    /// mock ở dạng đã hoàn tất.
    private static FinanceWorkbookViewModel LoadWorkbook(Project? tree)
    {
        var workbook = CreateWorkbook(tree);
        workbook.LoadAsync().GetAwaiter().GetResult();

        if (tree is not null)
            workbook.ViTriPages.ShouldNotBeEmpty("bất biến của tệp test: mock phải trả Task đã hoàn tất để cây nạp xong đồng bộ");
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

    /// Mở dự án, đứng ở tab Vị trí 1 và sửa cùng một ô Tạm ứng hai lần (1.000.000 rồi 2.500.000), đúng như người
    /// dùng gõ. Thông tri được tích sẵn đợt NSNN để có số mà in. Chỉ có vậy: chưa in, chưa chọn tab nào khác.
    private static FinanceWorkbookViewModel OpenAndEditTheSameCellTwice()
    {
        var workbook = BuildWorkbookWithTwoViTri();
        var viTri1 = workbook.ViTriPages.OfType<ViTriPageViewModel>().First();
        workbook.SelectedViTriPage = viTri1;
        workbook.ViTriPages.OfType<ThongTriFormPageViewModel>().Single().Inner.Picker.Items[0].IsSelected = true;

        var dot = viTri1.Ledger.Rows.Single(r => r.Kind == RowKind.Entry && r.Fund == Fund.NSNN);
        dot.AdvanceText = "1.000.000";
        dot.AdvanceText = "2.500.000";

        return workbook;
    }

    /// ...rồi bấm "In toàn bộ".
    private static IReadOnlyList<PrintSheet> PrintAllAfterEditingTheSameCellTwice() =>
        OpenAndEditTheSameCellTwice().BuildAllSheets();

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

    // ===== CanPrintAll: câu hỏi "có gì để In toàn bộ không" =====
    //
    // Nút In ở khung chung hỏi CanPrintAll mỗi lần cập nhật trạng thái bật/mờ, nên nó phải rẻ và KHÔNG có tác dụng
    // phụ: không dựng sheet, không làm mới màn nào. Việc làm mới là của BuildAllSheets, chạy một lần mỗi lần in thật.

    /// Trang in được, đếm số lần bị yêu cầu dựng sheet — để thấy CanPrintAll có dựng sheet hay không.
    private sealed class TrangDemLanDung : WorkbookPageViewModel
    {
        public int BuildCalls { get; private set; }

        public override object Content => new object();
        public override string? PrintSheetName => "Trang đếm";

        public override PrintSheet? BuildPrintSheet()
        {
            BuildCalls++;
            return new PrintTableSheet("Trang đếm", PrintPageProfiles.YearSettlement,
                Array.Empty<PrintColumn>(), Array.Empty<PrintRow>());
        }
    }

    /// Số đang hiện trên ba tab tổng hợp/biểu mẫu: đúng ba ô mà các test "mang_so_cua_lan_sua_thu_hai" đọc.
    private sealed record AggregatesShown(PrintCell TongHopTamUng, PrintCell QuyetToanTong, decimal ThongTriTotal);

    /// Đọc thẳng BuildPrintSheet() của từng trang. Cách đọc này KHÔNG làm mới gì (khác workbook.BuildAllSheets()),
    /// nên nhìn được trạng thái hiện tại mà không làm đổi nó.
    private static AggregatesShown AggregatesShownNow(FinanceWorkbookViewModel workbook)
    {
        var tongHop = (PrintTableSheet)workbook.ViTriPages.OfType<ProjectTongHopPageViewModel>().Single().BuildPrintSheet()!;
        var quyetToan = (PrintTableSheet)workbook.ViTriPages.OfType<YearSettlementPageViewModel>().Single().BuildPrintSheet()!;
        var thongTri = (PrintFormSheet)workbook.ViTriPages.OfType<ThongTriFormPageViewModel>().Single().BuildPrintSheet()!;
        return new AggregatesShown(
            CellAt(tongHop, 2, "Tạm ứng"),       // dòng TỔNG CỘNG của Tổng hợp (Vị trí 1, Vị trí 2, TỔNG CỘNG)
            CellAt(quyetToan, 1, "Tổng"),        // dòng TỔNG CỘNG của Quyết toán năm (năm 2026, TỔNG CỘNG)
            thongTri.Form.ThongTri!.Total);
    }

    [Fact]
    public void CanPrintAll_bat_khi_co_man_in_duoc_va_tat_khi_khong_co()
    {
        var loaded = BuildWorkbookWithTwoViTri();
        var noProject = LoadWorkbook(null);
        var onlyUnprintablePage = LoadWorkbook(null);
        onlyUnprintablePage.ViTriPages.Add(new TrangKhongIn());

        // Hỏi nhanh và in thật phải nhất quán: nút sáng thì BuildAllSheets có sheet để in, nút mờ thì không có.
        ((IPrintableWorkbook)loaded).CanPrintAll.ShouldBeTrue("đã nạp dự án");
        loaded.BuildAllSheets().ShouldNotBeEmpty();

        ((IPrintableWorkbook)noProject).CanPrintAll.ShouldBeFalse("chưa có dự án");
        noProject.BuildAllSheets().ShouldBeEmpty();

        // Có trang nhưng không trang nào báo tên sheet: vẫn không in được. "Có trang" không đồng nghĩa "in được".
        ((IPrintableWorkbook)onlyUnprintablePage).CanPrintAll.ShouldBeFalse("có trang nhưng không trang nào in được");
        onlyUnprintablePage.BuildAllSheets().ShouldBeEmpty();
    }

    [Fact]
    public void CanPrintAll_khong_lam_moi_tab_nao_nen_so_van_cu_sau_hai_lan_sua()
    {
        var workbook = OpenAndEditTheSameCellTwice();
        var stale = new AggregatesShown(PrintCell.OfMoney(1_000_000m), PrintCell.OfMoney(1_000_000m), 1_000_000m);

        // Tiền đề: sau hai lần sửa, ba tab tổng hợp còn mang số của lần sửa ĐẦU (chính cái lỗi mà BuildAllSheets vá
        // bằng cách làm mới). Test này dựa vào đó để thấy một lần làm mới; nếu một ngày lỗi gốc được sửa thì dòng này
        // đỏ trước và nói rõ test cần viết lại, chứ không phải CanPrintAll hỏng.
        AggregatesShownNow(workbook).ShouldBe(stale, "tiền đề: ba tab đang mang số cũ của lần sửa đầu");

        var canPrintAll = ((IPrintableWorkbook)workbook).CanPrintAll;

        // Nghịch đảo của ba test "mang_so_cua_lan_sua_thu_hai": BuildAllSheets làm mới nên in ra 2.500.000, còn
        // CanPrintAll mà làm mới thì ba số này cũng nhảy sang 2.500.000. ShouldBeTrue để một CanPrintAll luôn trả
        // false (cũng chẳng làm mới gì) không qua được.
        canPrintAll.ShouldBeTrue();
        AggregatesShownNow(workbook).ShouldBe(stale, "CanPrintAll không được làm mới tab nào");
    }

    [Fact]
    public void CanPrintAll_khong_dung_sheet_nao()
    {
        var workbook = BuildWorkbookWithTwoViTri();
        var spy = new TrangDemLanDung();
        workbook.ViTriPages.Insert(0, spy);     // đứng ĐẦU dải: cách hỏi nào duyệt trang theo thứ tự cũng chạm nó trước

        var canPrintAll = ((IPrintableWorkbook)workbook).CanPrintAll;

        canPrintAll.ShouldBeTrue();
        spy.BuildCalls.ShouldBe(0, "CanPrintAll chỉ đọc tên sheet, không dựng sheet");

        // Đối chứng: in thật thì trang đó BỊ dựng, nên số 0 ở trên có nghĩa chứ không phải bộ đếm hỏng.
        workbook.BuildAllSheets();
        spy.BuildCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Nap_du_an_thi_bao_CanPrintAll_da_doi()
    {
        var workbook = CreateWorkbook(ProjectWithTwoViTri());
        var changed = new List<string>();
        workbook.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");
        ((IPrintableWorkbook)workbook).CanPrintAll.ShouldBeFalse("tiền đề: chưa nạp dự án");

        await workbook.LoadAsync();

        // Không có tín hiệu này thì mục "In toàn bộ" không cập nhật được trạng thái khi dự án vừa nạp xong.
        changed.ShouldContain(nameof(IPrintableWorkbook.CanPrintAll));
        ((IPrintableWorkbook)workbook).CanPrintAll.ShouldBeTrue();
    }

    // ===== Tên sheet GĐNTT đổi theo gói thầu: phải báo cho nút In =====
    //
    // PrintSheetName của trang GĐNTT đổi khi người dùng tích/bỏ tích đợt NGAY TRÊN tab đó, không kèm lần đổi tab nào.
    // Không báo thì nút In ở khung chung mờ lệch cho tới khi người dùng đổi tab.

    [Fact]
    public void Tich_goi_thau_thi_trang_GDNTT_bao_PrintSheetName_da_doi()
    {
        var inner = NewGdntt(Row(0, Fund.NgoaiHoi, 10_000_000m));
        var page = new GdnttFormPageViewModel(inner);
        var changed = new List<string>();
        page.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");

        inner.Picker.Items[0].IsSelected = true;
        changed.Count(n => n == nameof(IPrintablePage.PrintSheetName)).ShouldBe(1, "tích gói thầu: null → \"GĐNTT\"");
        page.PrintSheetName.ShouldBe("GĐNTT");

        inner.Picker.Items[0].IsSelected = false;
        changed.Count(n => n == nameof(IPrintablePage.PrintSheetName)).ShouldBe(2, "bỏ tích: \"GĐNTT\" → null");
        page.PrintSheetName.ShouldBeNull();
    }

    [Fact]
    public void Trang_GDNTT_chi_bao_PrintSheetName_khi_ten_that_su_doi()
    {
        // Hai đợt cùng một gói: tích thêm đợt thứ hai không làm đổi chuyện "in được hay không".
        var inner = NewGdntt(Row(0, Fund.NgoaiHoi, 10_000_000m), Row(1, Fund.NgoaiHoi, 30_000_000m));
        var page = new GdnttFormPageViewModel(inner);
        inner.Picker.Items[0].IsSelected = true;                       // null → "GĐNTT"
        var pageChanged = new List<string>();
        var canExportRaised = 0;
        page.PropertyChanged += (_, e) => pageChanged.Add(e.PropertyName ?? "");
        inner.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GdnttFormViewModel.CanExport)) canExportRaised++;
        };

        inner.Picker.Items[1].IsSelected = true;                       // vẫn một gói: CanExport vẫn true
        inner.TaiNganHang = "Vietcombank";                             // gõ một ô chữ
        inner.ChuyenTienBaoHanhText = "1.000";                         // gõ một ô tiền

        // Tiền đề làm test có nghĩa: Rebuild() của GdnttFormViewModel bắn CanExport ở MỌI lần dựng, kể cả khi giá trị
        // không đổi. Chuyển tiếp nguyên xi thì mỗi lần tích hay gõ đều làm nút In cập nhật lại.
        canExportRaised.ShouldBeGreaterThanOrEqualTo(3, "tiền đề: mỗi lần tích/gõ view-model đều bắn lại CanExport");
        page.PrintSheetName.ShouldBe("GĐNTT", "tiền đề: vẫn in được suốt thời gian đó");
        pageChanged.ShouldNotContain(nameof(IPrintablePage.PrintSheetName));
    }

    [Fact]
    public void Tich_goi_thau_o_tab_GDNTT_dang_mo_thi_workbook_bao_PrintSheetName_va_CanPrintAll()
    {
        var workbook = BuildWorkbookWhereGdnttHasNoPackage();
        var gdntt = workbook.ViTriPages.OfType<GdnttFormPageViewModel>().Single();
        workbook.SelectedViTriPage = gdntt;
        var changed = new List<string>();
        workbook.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");

        gdntt.Inner.Picker.Items[0].IsSelected = true;

        // Không chuyển tiếp thì PrintSheetName đã đổi thành "GĐNTT" mà nút In vẫn mờ cho tới khi đổi tab.
        changed.ShouldContain(nameof(IPrintablePage.PrintSheetName));
        changed.ShouldContain(nameof(IPrintableWorkbook.CanPrintAll));
        ((IPrintablePage)workbook).PrintSheetName.ShouldBe("GĐNTT");
    }

    [Fact]
    public void Tich_goi_thau_o_GDNTT_sau_khi_roi_tab_do_thi_workbook_khong_bao()
    {
        var workbook = BuildWorkbookWhereGdnttHasNoPackage();
        var gdntt = workbook.ViTriPages.OfType<GdnttFormPageViewModel>().Single();
        workbook.SelectedViTriPage = gdntt;                                                      // đã mở GĐNTT: workbook từng nghe nó
        workbook.SelectedViTriPage = workbook.ViTriPages.OfType<ViTriPageViewModel>().First();   // rồi rời sang Vị trí 1
        var changed = new List<string>();
        workbook.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");
        var pageChanged = new List<string>();
        gdntt.PropertyChanged += (_, e) => pageChanged.Add(e.PropertyName ?? "");

        gdntt.Inner.Picker.Items[0].IsSelected = true;

        // PrintSheetName của workbook là của tab đang mở (Vị trí 1, vẫn "Vị trí 1"): GĐNTT đổi gì khi người dùng không
        // xem nó cũng không phải chuyện của nút In. Và workbook phải gỡ handler khỏi trang đã rời.
        gdntt.PrintSheetName.ShouldBe("GĐNTT", "tiền đề: GĐNTT đã đổi thật");
        // Tiền đề thứ hai: chính trang GĐNTT ĐÃ báo. Không có dòng này thì khi trang thôi không báo nữa, workbook "im lặng"
        // cũng xanh test — im vì không có gì để chuyển tiếp chứ không phải vì đã gỡ handler.
        pageChanged.ShouldContain(nameof(IPrintablePage.PrintSheetName), "tiền đề: trang GĐNTT đã bắn PrintSheetName");
        changed.ShouldNotContain(nameof(IPrintablePage.PrintSheetName));
        ((IPrintablePage)workbook).PrintSheetName.ShouldBe("Vị trí 1");
    }

    [Fact]
    public void Trang_dang_mo_doi_thu_khac_PrintSheetName_thi_workbook_khong_bao()
    {
        var workbook = BuildWorkbookWithTwoViTri();
        var viTri1 = workbook.ViTriPages.OfType<ViTriPageViewModel>().First();
        workbook.SelectedViTriPage = viTri1;
        var changed = new List<string>();
        workbook.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");
        var pageChanged = new List<string>();
        viTri1.PropertyChanged += (_, e) => pageChanged.Add(e.PropertyName ?? "");

        viTri1.Title = "Nhà điều hành";      // trang đang mở bắn Title và Header
        viTri1.IsDirty = true;               // và IsDirty

        // Tiền đề: trang đang mở ĐÃ bắn thông báo (Title, Header, IsDirty), chỉ không phải PrintSheetName. Không có hai
        // dòng này thì khi hai lệnh gán trên không còn bắn gì, "workbook không chuyển tiếp" xanh vì không có gì để chuyển.
        pageChanged.ShouldNotBeEmpty("tiền đề: trang đang mở đã bắn thông báo");
        pageChanged.ShouldNotContain(nameof(IPrintablePage.PrintSheetName), "tiền đề: nhưng đó không phải PrintSheetName");

        // Chỉ PrintSheetName của trang mới được chuyển tiếp thành PrintSheetName của workbook; không thì mỗi lần trang
        // đang mở đổi bất cứ gì (đánh dấu bẩn, đổi tiêu đề) nút In lại bị hỏi lại.
        changed.ShouldNotContain(nameof(IPrintablePage.PrintSheetName));
    }

    // ===== Đổi tên Vị trí là đổi tên sheet in =====

    [Fact]
    public void Doi_ten_Vi_tri_thi_trang_bao_PrintSheetName_da_doi()
    {
        var page = new ViTriPageViewModel(new VitriLedgerViewModel(Fixture("Vị trí 1", 0)));
        var changed = new List<string>();
        page.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");

        page.Ledger.Name = "Nhà điều hành";      // người dùng gõ vào ô tên Vị trí

        changed.ShouldContain(nameof(IPrintablePage.PrintSheetName));
        page.PrintSheetName.ShouldBe("Nhà điều hành");
    }

    [Fact]
    public void Doi_ten_Vi_tri_dang_mo_thi_workbook_bao_PrintSheetName_da_doi()
    {
        var workbook = BuildWorkbookWithTwoViTri();
        var viTri1 = workbook.ViTriPages.OfType<ViTriPageViewModel>().First();
        workbook.SelectedViTriPage = viTri1;
        var changed = new List<string>();
        workbook.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");

        viTri1.Ledger.Name = "Nhà điều hành";

        // Tên sheet là tên Vị trí; nút In ở khung chung có thể hiện tên đó (nhãn, tooltip), nên nó phải theo kịp ngay
        // chứ không đợi tới lần đổi tab sau.
        changed.ShouldContain(nameof(IPrintablePage.PrintSheetName));
        ((IPrintablePage)workbook).PrintSheetName.ShouldBe("Nhà điều hành");
    }

    // ===== Đổi tab: làm mới trước, báo sau =====

    [Fact]
    public void Doi_sang_tab_tong_hop_thi_chi_bao_PrintSheetName_sau_khi_da_lam_moi_tab_do()
    {
        var workbook = OpenAndEditTheSameCellTwice();        // đang ở Vị trí 1; Tổng hợp còn mang số cũ 1.000.000
        var tongHop = workbook.ViTriPages.OfType<ProjectTongHopPageViewModel>().Single();
        PrintCell TongHopTamUngDangHien() => CellAt((PrintTableSheet)tongHop.BuildPrintSheet()!, 2, "Tạm ứng");
        TongHopTamUngDangHien().ShouldBe(PrintCell.OfMoney(1_000_000m), "tiền đề: Tổng hợp đang mang số cũ");

        var seen = new List<(string? Name, PrintCell TongHopTamUng)>();
        workbook.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IPrintablePage.PrintSheetName))
                seen.Add((workbook.PrintSheetName, TongHopTamUngDangHien()));
        };

        workbook.SelectedViTriPage = tongHop;

        // Người nghe (nút In) đọc NGAY lúc được báo: phải thấy đúng trang vừa mở và số đã làm mới. Tức là báo SAU lần
        // làm mới và SAU khi trang đang mở đã đổi, chứ không phải ở một thứ tự do thư viện sinh mã tình cờ gọi ra.
        seen.ShouldNotBeEmpty("tiền đề: đổi tab có báo PrintSheetName");
        foreach (var s in seen)
        {
            s.Name.ShouldBe("Tổng hợp", "tên đọc được lúc được báo phải là của trang vừa mở");
            s.TongHopTamUng.ShouldBe(PrintCell.OfMoney(2_500_000m), "số đọc được lúc được báo phải đã làm mới");
        }
    }

    [Fact]
    public void Doi_tab_thi_bao_lenh_Xoa_Vi_tri_kiem_tra_lai_CanExecute()
    {
        var workbook = BuildWorkbookWithTwoViTri();
        var raised = 0;
        workbook.DeleteViTriCommand.CanExecuteChanged += (_, _) => raised++;

        workbook.SelectedViTriPage = workbook.ViTriPages.OfType<ViTriPageViewModel>().First();

        // Nút "Xóa Vị trí" mờ ở tab tổng hợp và sáng ở tab sổ: nó chỉ biết kiểm tra lại khi được báo.
        raised.ShouldBeGreaterThan(0);
    }
}
