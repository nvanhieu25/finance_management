using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Desktop.ViewModels.Finance.Forms;
using ConstructionFinance.Domain.Finance;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class FinanceWorkbookFormTabsTests
{
    private static ViTri ViTriWithDots() => new()
    {
        Name = "Vị trí 1", SortOrder = 0,
        CostGroups =
        {
            new CostGroup { Name = "CHI PHÍ XÂY DỰNG", SortOrder = 0, Packages =
            {
                new Package { Name = "Gói A", ContractorName = "Cty XL A", SortOrder = 0, Dots =
                {
                    new Dot { Date = new DateOnly(2026, 3, 1), Fund = Fund.NSNN,
                              Advance = 10_000_000m, Content = "Tạm ứng lần 1", SortOrder = 0 },
                    new Dot { Date = new DateOnly(2026, 4, 1), Fund = Fund.NgoaiHoi,
                              Advance = 30_000_000m, Content = "Tạm ứng ngoại hối", SortOrder = 1 }
                } }
            } }
        }
    };

    private static (FinanceWorkbookViewModel vm, Mock<IFinanceProjectService> service) CreateSut(
        Project? tree = null)
    {
        var project = tree ?? new Project { Id = 7, Name = "Dự án A", ViTris = { ViTriWithDots() } };

        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.GetAllSummariesAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(Array.Empty<FinanceProjectSummary>());
        service.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<Project> { new() { Id = project.Id, Name = project.Name } });
        service.Setup(s => s.GetTreeAsync(project.Id, It.IsAny<CancellationToken>()))
               .ReturnsAsync(project);

        var summaryVm = new FinanceSummaryViewModel(
            service.Object, Mock.Of<IFinanceImportService>(),
            Mock.Of<IFinanceExportService>(), Mock.Of<IDialogService>());

        var companyInfo = new Mock<ICompanyInfoService>();
        companyInfo.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new CompanyInfoDto(null, null, null, null, null, null, null, null,
                       DonViCapTren: "Sở Tài chính", DonViCoSo: "Ban QLDA số 1"));

        var vm = new FinanceWorkbookViewModel(
            service.Object, new SummaryPageViewModel(summaryVm), Mock.Of<IDialogService>(),
            new ServiceCollection().BuildServiceProvider(),
            Mock.Of<IFormExcelExporter>(), companyInfo.Object);

        return (vm, service);
    }

    [Fact]
    public async Task LoadAsync_appends_the_three_form_tabs_after_quyet_toan_nam()
    {
        var (vm, _) = CreateSut();

        await vm.LoadAsync();

        vm.ViTriPages.Select(p => p.Title).ShouldBe(new[]
        {
            "Tổng hợp", "Vị trí 1", "Quyết toán năm", "Thông tri", "KS chi", "GĐNTT"
        });
    }

    [Fact]
    public async Task Form_tabs_see_the_dots_filtered_by_their_fund()
    {
        var (vm, _) = CreateSut();

        await vm.LoadAsync();

        var thongTri = vm.ViTriPages.OfType<ThongTriFormPageViewModel>().Single().Inner;
        var gdntt = vm.ViTriPages.OfType<GdnttFormPageViewModel>().Single().Inner;
        var ksChi = vm.ViTriPages.OfType<KsChiFormPageViewModel>().Single().Inner;

        thongTri.Picker.Items.Count.ShouldBe(1);      // chỉ đợt NSNN
        gdntt.Picker.Items.Count.ShouldBe(1);         // chỉ đợt ngoại hối
        ksChi.Picker.Items.Count.ShouldBe(2);         // cả hai
    }

    [Fact]
    public async Task Editing_a_form_field_makes_the_workbook_dirty()
    {
        var (vm, _) = CreateSut();
        await vm.LoadAsync();
        vm.IsDirty.ShouldBeFalse();

        vm.ViTriPages.OfType<ThongTriFormPageViewModel>().Single().Inner.SoThongTri = "12/TT";

        vm.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveAll_persists_the_three_declarations_with_the_tree()
    {
        var (vm, service) = CreateSut();
        Project? saved = null;
        service.Setup(s => s.ReplaceTreeAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
               .Callback<Project, CancellationToken>((p, _) => saved = p)
               .Returns(Task.CompletedTask);
        await vm.LoadAsync();

        vm.ViTriPages.OfType<ThongTriFormPageViewModel>().Single().Inner.SoThongTri = "12/TT";
        vm.ViTriPages.OfType<KsChiFormPageViewModel>().Single().Inner.ThueGTGTText = "2.000.000";
        vm.ViTriPages.OfType<GdnttFormPageViewModel>().Single().Inner.TaiNganHang = "Vietcombank";
        await vm.SaveAllCommand.ExecuteAsync(null);

        saved.ShouldNotBeNull();
        saved!.ThongTri!.SoThongTri.ShouldBe("12/TT");
        saved.KsChi!.ThueGTGT.ShouldBe(2_000_000m);
        saved.Gdntt!.TaiNganHang.ShouldBe("Vietcombank");
        vm.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public async Task Stored_declarations_load_into_the_form_tabs()
    {
        var project = new Project
        {
            Id = 7, Name = "Dự án A",
            ViTris = { ViTriWithDots() },
            ThongTri = new ThongTriDeclaration { SoThongTri = "9/TT" }
        };
        var (vm, _) = CreateSut(project);

        await vm.LoadAsync();

        vm.ViTriPages.OfType<ThongTriFormPageViewModel>().Single().Inner.SoThongTri.ShouldBe("9/TT");
        vm.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public async Task Adding_a_dot_in_the_ledger_shows_up_in_the_form_picker()
    {
        var (vm, _) = CreateSut();
        await vm.LoadAsync();
        var ledgerPage = vm.ViTriPages.OfType<ViTriPageViewModel>().Single();
        var ksChi = vm.ViTriPages.OfType<KsChiFormPageViewModel>().Single();

        var packageRow = ledgerPage.Ledger.Rows.First(r => r.Kind == RowKind.Package);
        ledgerPage.Ledger.AddEntryCommand.Execute(packageRow);
        vm.SelectedViTriPage = ksChi;      // chọn tab form → làm mới

        ksChi.Inner.Picker.Items.Count.ShouldBe(3);
    }
}
