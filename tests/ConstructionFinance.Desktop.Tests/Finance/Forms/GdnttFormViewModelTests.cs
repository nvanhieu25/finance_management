using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Desktop.ViewModels.Finance.Forms;
using ConstructionFinance.Domain.Finance;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance.Forms;

public class GdnttFormViewModelTests
{
    private static FormDotRow Row(FormDotKey key, decimal advance, string packageName = "Gói A") =>
        new(key, "Vị trí 1", "CHI PHÍ XÂY DỰNG", packageName, "Cty XL A", "123456789",
            50_000_000_000m, 2_000_000_000m, new DateOnly(2026, 3, 1), "Đợt", "CT01",
            Fund.NgoaiHoi, advance, 0m, 0m, advance);

    private static (GdnttFormViewModel vm, Mock<IFormExcelExporter> exporter, FakeDialogService dialog) Create()
    {
        var exporter = new Mock<IFormExcelExporter>();
        var dialog = new FakeDialogService();
        return (new GdnttFormViewModel(exporter.Object, dialog), exporter, dialog);
    }

    private static void Load(GdnttFormViewModel vm, params FormDotRow[] rows) =>
        vm.Refresh(rows, Array.Empty<LedgerRowViewModel>(), new GdnttDeclaration(),
                   new FormOrgInfo("Sở Tài chính", "Ban QLDA số 1"), "Dự án A");

    [Fact]
    public void Selecting_one_package_fills_the_amount_block()
    {
        var (vm, _, _) = Create();
        Load(vm, Row(new FormDotKey(0, 0, 0, 0), 10_000_000m), Row(new FormDotKey(0, 0, 0, 1), 30_000_000m));

        vm.Picker.Items[1].IsSelected = true;

        vm.PackageName.ShouldBe("Gói A");
        vm.ContractorName.ShouldBe("Cty XL A");
        vm.ContractTotalText.ShouldBe("52.000.000.000");
        vm.CumulativeBeforeText.ShouldBe("10.000.000");
        vm.RequestedText.ShouldBe("30.000.000");
        vm.RequestedInWords.ShouldBe("Ba mươi triệu đồng.");
        vm.ErrorMessage.ShouldBeNull();
        vm.CanExport.ShouldBeTrue();
    }

    [Fact]
    public void Selecting_dots_from_two_packages_shows_a_warning_and_blocks_export()
    {
        var (vm, _, _) = Create();
        Load(vm, Row(new FormDotKey(0, 0, 0, 0), 10_000_000m),
                 Row(new FormDotKey(0, 0, 1, 0), 20_000_000m, "Gói B"));

        vm.Picker.SelectAllCommand.Execute(null);

        vm.ErrorMessage.ShouldNotBeNull();
        vm.ErrorMessage!.ShouldContain("một gói thầu");
        vm.CanExport.ShouldBeFalse();
    }

    [Fact]
    public void Nothing_selected_shows_the_prompt_and_blocks_export()
    {
        var (vm, _, _) = Create();
        Load(vm, Row(new FormDotKey(0, 0, 0, 0), 10_000_000m));

        vm.ErrorMessage.ShouldNotBeNull();
        vm.CanExport.ShouldBeFalse();
        vm.RequestedText.ShouldBe("");
    }

    [Fact]
    public async Task Export_is_refused_while_the_selection_is_invalid()
    {
        var (vm, exporter, dialog) = Create();
        Load(vm, Row(new FormDotKey(0, 0, 0, 0), 10_000_000m),
                 Row(new FormDotKey(0, 0, 1, 0), 20_000_000m, "Gói B"));
        vm.Picker.SelectAllCommand.Execute(null);
        dialog.PickSaveFileResult = "/tmp/gd.xlsx";

        await vm.ExportCommand.ExecuteAsync(null);

        exporter.Verify(e => e.ExportGdnttAsync(
            It.IsAny<GdnttForm>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        dialog.ShownMessages.ShouldContain(m => m.Contains("một gói thầu"));
    }

    [Fact]
    public async Task Export_hands_the_built_form_to_the_exporter()
    {
        var (vm, exporter, dialog) = Create();
        Load(vm, Row(new FormDotKey(0, 0, 0, 0), 30_000_000m));
        vm.Picker.SelectAllCommand.Execute(null);
        dialog.PickSaveFileResult = "/tmp/gd.xlsx";

        await vm.ExportCommand.ExecuteAsync(null);

        exporter.Verify(e => e.ExportGdnttAsync(
            It.Is<GdnttForm>(f => f.RequestedThisPeriod == 30_000_000m && f.PackageName == "Gói A"),
            "/tmp/gd.xlsx", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Manual_money_boxes_feed_the_contractor_payment()
    {
        var (vm, _, _) = Create();
        Load(vm, Row(new FormDotKey(0, 0, 0, 0), 30_000_000m));
        vm.Picker.SelectAllCommand.Execute(null);

        vm.ChuyenTienBaoHanhText = "2.000.000";
        vm.ChuyenTienTamGiuText = "1.000.000";

        vm.PaidToContractorText.ShouldBe("27.000.000");
        vm.ToDeclaration().ChuyenTienBaoHanh.ShouldBe(2_000_000m);
    }

    [Fact]
    public void Refresh_exposes_the_two_agency_names_from_org_info()
    {
        var (vm, _, _) = Create();

        Load(vm, Row(new FormDotKey(0, 0, 0, 0), 10_000_000m));

        vm.DonViCapTrenText.ShouldBe("Sở Tài chính");
        vm.DonViCoSoText.ShouldBe("Ban QLDA số 1");
    }

    [Fact]
    public void Refresh_loading_stored_values_does_not_announce_a_change()
    {
        var (vm, _, _) = Create();
        var announced = 0;
        vm.DeclarationChanged += (_, _) => announced++;

        vm.Refresh(new[] { Row(new FormDotKey(0, 0, 0, 0), 30_000_000m) },
            Array.Empty<LedgerRowViewModel>(),
            new GdnttDeclaration { TaiNganHang = "Vietcombank" },
            new FormOrgInfo("Sở Tài chính", "Ban QLDA số 1"), "Dự án A");

        vm.TaiNganHang.ShouldBe("Vietcombank");
        announced.ShouldBe(0);
    }
}
