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
        Project? tree = null) => CreateSut(tree, out _);

    private static (FinanceWorkbookViewModel vm, Mock<IFinanceProjectService> service) CreateSut(
        Project? tree, out Mock<ICompanyInfoService> companyInfo)
    {
        var project = tree ?? new Project { Id = 7, Name = "Dự án A", ViTris = { ViTriWithDots() } };

        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<Project> { new() { Id = project.Id, Name = project.Name } });
        service.Setup(s => s.GetTreeAsync(project.Id, It.IsAny<CancellationToken>()))
               .ReturnsAsync(project);

        companyInfo = new Mock<ICompanyInfoService>();
        companyInfo.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new CompanyInfoDto(null, null, null, null, null, null, null, null,
                       DonViCapTren: "Sở Tài chính", DonViCoSo: "Ban QLDA số 1"));

        var vm = new FinanceWorkbookViewModel(
            service.Object, Mock.Of<IDialogService>(),
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

        var thongTri = vm.ViTriPages.FormTab("thongtri").Inner;
        var gdntt = vm.ViTriPages.FormTab("gdntt").Inner;
        var ksChi = vm.ViTriPages.FormTab("kschi").Inner;

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

        vm.ViTriPages.FormTab("thongtri").Inner.SetInput("SoThongTri", "12/TT");

        vm.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public async Task Editing_a_form_field_shows_the_dirty_bullet_on_its_own_tab_and_clears_after_save()
    {
        var (vm, service) = CreateSut();
        service.Setup(s => s.ReplaceTreeAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        await vm.LoadAsync();
        var thongTriPage = vm.ViTriPages.FormTab("thongtri");
        thongTriPage.IsDirty.ShouldBeFalse();
        thongTriPage.Header.ShouldBe("Thông tri");

        thongTriPage.Inner.SetInput("SoThongTri", "12/TT");

        thongTriPage.IsDirty.ShouldBeTrue();
        thongTriPage.Header.ShouldBe("• Thông tri");

        await vm.SaveAllCommand.ExecuteAsync(null);

        thongTriPage.IsDirty.ShouldBeFalse();
        thongTriPage.Header.ShouldBe("Thông tri");
    }

    [Fact]
    public async Task SaveAll_persists_the_typed_text_as_FormInputs()
    {
        var (vm, service) = CreateSut();
        Project? saved = null;
        service.Setup(s => s.ReplaceTreeAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
               .Callback<Project, CancellationToken>((p, _) => saved = p)
               .Returns(Task.CompletedTask);
        await vm.LoadAsync();

        vm.ViTriPages.FormTab("thongtri").Inner.SetInput("SoThongTri", "12/TT");
        vm.ViTriPages.FormTab("kschi").Inner.SetInput("ThueGTGT", "2.000.000");
        vm.ViTriPages.FormTab("gdntt").Inner.SetInput("TaiNganHang", "Vietcombank");
        await vm.SaveAllCommand.ExecuteAsync(null);

        saved.ShouldNotBeNull();
        saved!.FormInputs.Select(i => (i.Form, i.Key, i.Value)).OrderBy(t => t.Key).ShouldBe(new (string, string, string?)[]
        {
            ("thongtri", "SoThongTri", "12/TT"),
            ("gdntt", "TaiNganHang", "Vietcombank"),
            ("kschi", "ThueGTGT", "2000000"),
        }.OrderBy(t => t.Item2));
        vm.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public async Task Stored_inputs_load_into_the_form_tabs()
    {
        var project = new Project
        {
            Id = 7, Name = "Dự án A",
            ViTris = { ViTriWithDots() },
            FormInputs = { new FormInput { Form = "thongtri", Key = "SoThongTri", Value = "9/TT" } }
        };
        var (vm, _) = CreateSut(project);

        await vm.LoadAsync();

        vm.ViTriPages.FormTab("thongtri").Inner.Sheet.Cells.SelectMany(c => c.Segments)
            .First(x => x.Key == "SoThongTri").Value.ShouldBe("9/TT");
        vm.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public async Task Adding_a_dot_in_the_ledger_shows_up_in_the_form_picker()
    {
        var (vm, _) = CreateSut();
        await vm.LoadAsync();
        var ledgerPage = vm.ViTriPages.OfType<ViTriPageViewModel>().Single();
        var ksChi = vm.ViTriPages.FormTab("kschi");

        var packageRow = ledgerPage.Ledger.Rows.First(r => r.Kind == RowKind.Package);
        ledgerPage.Ledger.AddEntryCommand.Execute(packageRow);
        vm.SelectedViTriPage = ksChi;      // chọn tab form → làm mới

        ksChi.Inner.Picker.Items.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Editing_the_org_on_one_form_shows_on_the_others_and_is_saved_to_company_settings()
    {
        var (vm, service) = CreateSut(null, out var companyInfo);
        service.Setup(s => s.ReplaceTreeAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        await vm.LoadAsync();

        vm.ViTriPages.FormTab("thongtri").Inner.SetInput("don_vi_co_so", "Ban mới");

        vm.ViTriPages.FormTab("gdntt").Inner.Sheet.Cells.Single(c => c.Row == 2 && c.Col == 1)
            .DisplayText.ShouldBe("Ban mới");
        vm.IsDirty.ShouldBeTrue();
        await vm.SaveAllCommand.ExecuteAsync(null);
        companyInfo.Verify(c => c.SaveAsync(
            It.Is<CompanyInfoDto>(i => i.DonViCoSo == "Ban mới" && i.DonViCapTren == "Sở Tài chính"),
            It.IsAny<CancellationToken>()), Times.Once);
        vm.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public async Task OpenFormTabAsync_waits_for_the_tree_and_selects_the_tab()
    {
        var (vm, _) = CreateSut();
        await vm.LoadAsync();

        await vm.OpenFormTabAsync("kschi");

        vm.SelectedViTriPage.ShouldBeSameAs(vm.ViTriPages.FormTab("kschi"));
    }

    [Fact]
    public async Task A_new_project_gets_three_empty_form_tabs()
    {
        var (vm, _) = CreateSut();
        await vm.LoadAsync();

        vm.AddProjectCommand.Execute(null);

        vm.ViTriPages.OfType<FormTabPageViewModel>().Count().ShouldBe(3);
        vm.ViTriPages.FormTab("thongtri").Inner.ToFormInputs().ShouldBeEmpty();
    }
}
