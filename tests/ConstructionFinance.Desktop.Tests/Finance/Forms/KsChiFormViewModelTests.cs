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

public class KsChiFormViewModelTests
{
    private static FormDotRow Row(int dotIndex, Fund fund, decimal advance, decimal klht, decimal recovered) =>
        new(new FormDotKey(0, 0, 0, dotIndex), "Vị trí 1", "CHI PHÍ XÂY DỰNG", "Gói A",
            "Cty XL A", "123456789", 50_000_000_000m, 0m, new DateOnly(2026, 3, 1), "Đợt", "CT01",
            fund, advance, klht, recovered,
            // Same formula as TreeCalculator.Disbursed: a đợt may carry both Tạm ứng and KLHT.
            Disbursed: advance + klht - recovered);

    private static (KsChiFormViewModel vm, Mock<IFormExcelExporter> exporter, FakeDialogService dialog) Create()
    {
        var exporter = new Mock<IFormExcelExporter>();
        var dialog = new FakeDialogService();
        return (new KsChiFormViewModel(exporter.Object, dialog), exporter, dialog);
    }

    [Fact]
    public void Selecting_dots_fills_the_three_fund_columns()
    {
        var (vm, _, _) = Create();
        vm.Refresh(new[]
        {
            Row(0, Fund.NSNN, 10_000_000m, 0m, 0m),
            Row(1, Fund.NgoaiHoi, 0m, 200_000_000m, 5_000_000m)
        }, Array.Empty<LedgerRowViewModel>(), new KsChiDeclaration(), "Dự án A");

        vm.Picker.SelectAllCommand.Execute(null);

        vm.AcceptedTotalText.ShouldBe("210.000.000");
        vm.AcceptedNsnnText.ShouldBe("10.000.000");
        vm.AcceptedNgoaiHoiText.ShouldBe("200.000.000");
        vm.RecoveredTotalText.ShouldBe("5.000.000");
        vm.PaidTotalText.ShouldBe("205.000.000");
        vm.PaidInWords.ShouldBe("Hai trăm linh năm triệu đồng.");
    }

    [Fact]
    public void Manual_money_boxes_parse_dotted_input_and_rebuild()
    {
        var (vm, _, _) = Create();
        vm.Refresh(new[] { Row(0, Fund.NSNN, 10_000_000m, 0m, 0m) },
            Array.Empty<LedgerRowViewModel>(), new KsChiDeclaration(), "Dự án A");

        vm.ThueGTGTText = "2.000.000";
        vm.SoTuChoiText = "500.000";

        vm.ToDeclaration().ThueGTGT.ShouldBe(2_000_000m);
        vm.ToDeclaration().SoTuChoi.ShouldBe(500_000m);
    }

    [Fact]
    public void Ghi_chu_falls_back_to_the_first_selected_dot()
    {
        var (vm, _, _) = Create();
        vm.Refresh(new[] { Row(0, Fund.NSNN, 10_000_000m, 0m, 0m) },
            Array.Empty<LedgerRowViewModel>(), new KsChiDeclaration(), "Dự án A");

        vm.Picker.SelectAllCommand.Execute(null);

        vm.GhiChuText.ShouldBe("Đợt");
    }

    [Fact]
    public async Task Export_hands_the_built_form_to_the_exporter()
    {
        var (vm, exporter, dialog) = Create();
        vm.Refresh(new[] { Row(0, Fund.NSNN, 10_000_000m, 0m, 0m) },
            Array.Empty<LedgerRowViewModel>(), new KsChiDeclaration(), "Dự án A");
        vm.Picker.SelectAllCommand.Execute(null);
        dialog.PickSaveFileResult = "/tmp/ks.xlsx";

        await vm.ExportCommand.ExecuteAsync(null);

        exporter.Verify(e => e.ExportKsChiAsync(
            It.Is<KsChiForm>(f => f.Accepted.Total == 10_000_000m),
            "/tmp/ks.xlsx", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Refresh_loading_stored_values_does_not_announce_a_change()
    {
        var (vm, _, _) = Create();
        var announced = 0;
        vm.DeclarationChanged += (_, _) => announced++;

        vm.Refresh(new[] { Row(0, Fund.NSNN, 10_000_000m, 0m, 0m) },
            Array.Empty<LedgerRowViewModel>(),
            new KsChiDeclaration { ThueGTGT = 2_000_000m, LyDoTuChoi = "x" }, "Dự án A");

        vm.ThueGTGTText.ShouldBe("2.000.000");
        vm.LyDoTuChoi.ShouldBe("x");
        announced.ShouldBe(0);
    }
}
