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

public class ThongTriFormViewModelTests
{
    private static FormDotRow Row(int dotIndex, decimal advance, string viTri = "Vị trí 1") =>
        new(new FormDotKey(dotIndex, 0, 0, 0), viTri, "CHI PHÍ XÂY DỰNG", "Gói A",
            "Cty XL A", "123456789", 50_000_000_000m, 0m, new DateOnly(2026, 3, 1),
            $"Tạm ứng lần {dotIndex + 1}", "CT01", Fund.NSNN, advance, 0m, 0m, advance);

    private static (ThongTriFormViewModel vm, Mock<IFormExcelExporter> exporter, FakeDialogService dialog) Create()
    {
        var exporter = new Mock<IFormExcelExporter>();
        var dialog = new FakeDialogService();
        return (new ThongTriFormViewModel(exporter.Object, dialog), exporter, dialog);
    }

    private static void Load(ThongTriFormViewModel vm, params FormDotRow[] rows) =>
        vm.Refresh(rows, Array.Empty<LedgerRowViewModel>(), new ThongTriDeclaration(),
                   new FormOrgInfo("Sở Tài chính", "Ban QLDA số 1"), "Dự án A");

    [Fact]
    public void Refresh_with_no_selection_shows_only_the_zero_project_line()
    {
        var (vm, _, _) = Create();

        Load(vm, Row(0, 10_000_000m));

        vm.Lines.Count.ShouldBe(1);
        vm.Lines[0].Content.ShouldBe("Dự án A");
        vm.TotalText.ShouldBe("");                   // MoneyText hiển thị 0 là ô trống
        vm.TotalInWords.ShouldBe("Không đồng.");
    }

    [Fact]
    public void Ticking_a_dot_rebuilds_the_preview_live()
    {
        var (vm, _, _) = Create();
        Load(vm, Row(0, 10_000_000m), Row(1, 5_000_000m, "Vị trí 2"));

        vm.Picker.Items[0].IsSelected = true;

        vm.Lines.Count.ShouldBe(3);                  // dự án + vị trí + đợt
        vm.Lines[2].Content.ShouldBe("Tạm ứng lần 1");
        vm.Lines[2].AmountText.ShouldBe("10.000.000");
        vm.TotalText.ShouldBe("10.000.000");
        vm.TotalInWords.ShouldBe("Mười triệu đồng.");
    }

    [Fact]
    public void Project_line_is_bold_and_dot_lines_are_indented()
    {
        var (vm, _, _) = Create();
        Load(vm, Row(0, 10_000_000m));
        vm.Picker.SelectAllCommand.Execute(null);

        vm.Lines[0].IsBold.ShouldBeTrue();
        vm.Lines[0].Indent.ShouldBe(0);
        vm.Lines[1].Indent.ShouldBe(1);
        vm.Lines[2].Indent.ShouldBe(2);
    }

    [Fact]
    public void Editing_a_manual_field_writes_through_and_announces_the_change()
    {
        var (vm, _, _) = Create();
        Load(vm, Row(0, 10_000_000m));
        var announced = 0;
        vm.DeclarationChanged += (_, _) => announced++;

        vm.SoThongTri = "12/TT";
        vm.Khoan = "11";

        announced.ShouldBe(2);
        vm.ToDeclaration().SoThongTri.ShouldBe("12/TT");
        vm.ToDeclaration().Khoan.ShouldBe("11");
    }

    [Fact]
    public void Refresh_loads_the_stored_declaration_into_the_boxes()
    {
        var (vm, _, _) = Create();

        vm.Refresh(new[] { Row(0, 10_000_000m) }, Array.Empty<LedgerRowViewModel>(),
            new ThongTriDeclaration { SoThongTri = "9/TT", TieuMuc = "9301" },
            new FormOrgInfo(null, null), "Dự án A");

        vm.SoThongTri.ShouldBe("9/TT");
        vm.TieuMuc.ShouldBe("9301");
    }

    [Fact]
    public async Task Export_writes_the_file_the_user_picked()
    {
        var (vm, exporter, dialog) = Create();
        Load(vm, Row(0, 10_000_000m));
        vm.Picker.SelectAllCommand.Execute(null);
        dialog.PickSaveFileResult = "/tmp/tt.xlsx";

        await vm.ExportCommand.ExecuteAsync(null);

        exporter.Verify(e => e.ExportThongTriAsync(
            It.Is<ThongTriForm>(f => f.Total == 10_000_000m && f.SoThongTri == null),
            "/tmp/tt.xlsx", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Export_does_nothing_when_the_user_cancels_the_save_dialog()
    {
        var (vm, exporter, dialog) = Create();
        Load(vm, Row(0, 10_000_000m));
        dialog.PickSaveFileResult = null;

        await vm.ExportCommand.ExecuteAsync(null);

        exporter.Verify(e => e.ExportThongTriAsync(
            It.IsAny<ThongTriForm>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Export_surfaces_a_failure_as_a_message()
    {
        var (vm, exporter, dialog) = Create();
        Load(vm, Row(0, 10_000_000m));
        dialog.PickSaveFileResult = "/tmp/tt.xlsx";
        exporter.Setup(e => e.ExportThongTriAsync(
                   It.IsAny<ThongTriForm>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new IOException("ổ đĩa đầy"));

        await vm.ExportCommand.ExecuteAsync(null);

        dialog.ShownMessages.ShouldContain(m => m.Contains("ổ đĩa đầy"));
    }
}
