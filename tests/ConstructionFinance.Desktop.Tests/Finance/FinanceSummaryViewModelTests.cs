using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Finance;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class FinanceSummaryViewModelTests
{
    private static FinanceProjectSummary S(int id, string name, decimal advance, decimal nsnn) =>
        new(id, null, name, null, 1_000m,
            ProjectCalculator.Aggregate(new List<LineItemInput>
            {
                new(new System.DateOnly(2026, 1, 1), null, advance, 0m, 0m, nsnn, 0m),
            }));

    private static FinanceSummaryViewModel Create(out Mock<IFinanceProjectService> svc,
        out Mock<IFinanceImportService> importer, out Mock<IFinanceExportService> exporter, out FakeDialogService dlg)
    {
        svc = new Mock<IFinanceProjectService>(MockBehavior.Strict);
        svc.Setup(s => s.GetAllSummariesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FinanceProjectSummary> { S(1, "Cầu A12", 500m, 500m), S(2, "Đường B3", 0m, 300m) });
        importer = new Mock<IFinanceImportService>(MockBehavior.Strict);
        exporter = new Mock<IFinanceExportService>(MockBehavior.Strict);
        dlg = new FakeDialogService();
        return new FinanceSummaryViewModel(svc.Object, importer.Object, exporter.Object, dlg);
    }

    [Fact]
    public async Task LoadAsync_shows_all_rows_and_grand_total()
    {
        var vm = Create(out _, out _, out _, out _);
        await vm.LoadAsync();

        vm.Rows.Count.ShouldBe(2);
        vm.GrandSumAdvance.ShouldBe(500m);
        vm.GrandSumNSNN.ShouldBe(800m);            // 500 + 300
        vm.GrandTotalDisbursed.ShouldBe(800m);
    }

    [Fact]
    public async Task FilterText_narrows_rows_and_grand_total_instantly()
    {
        var vm = Create(out _, out _, out _, out _);
        await vm.LoadAsync();

        vm.FilterText = "cầu";                      // case-insensitive substring
        vm.Rows.Count.ShouldBe(1);
        vm.Rows[0].Name.ShouldBe("Cầu A12");
        vm.GrandSumNSNN.ShouldBe(500m);            // only the matching project
    }

    [Fact]
    public async Task ImportCommand_parses_saves_and_reloads()
    {
        var vm = Create(out var svc, out var importer, out _, out var dlg);
        dlg.PickOpenFileResult = "/tmp/legacy.xlsx";
        dlg.ConfirmResult = true;
        importer.Setup(i => i.ParseWorkbookAsync("/tmp/legacy.xlsx", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConstructionFinance.Application.Import.FinanceImportResult(
                new List<ConstructionFinance.Domain.Finance.Project> { new() { Name = "DA1" } },
                new List<string>()));
        svc.Setup(s => s.SaveImportedAsync(It.IsAny<IReadOnlyList<ConstructionFinance.Domain.Finance.Project>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await vm.LoadAsync(); // initial load
        await vm.ImportCommand.ExecuteAsync(null);

        importer.Verify(i => i.ParseWorkbookAsync("/tmp/legacy.xlsx", It.IsAny<CancellationToken>()), Times.Once);
        svc.Verify(s => s.SaveImportedAsync(It.Is<IReadOnlyList<ConstructionFinance.Domain.Finance.Project>>(l => l.Count == 1), It.IsAny<CancellationToken>()), Times.Once);
        svc.Verify(s => s.GetAllSummariesAsync(It.IsAny<CancellationToken>()), Times.AtLeast(2)); // initial + reload
    }

    [Fact]
    public async Task ImportCommand_noop_when_no_file_picked()
    {
        var vm = Create(out var svc, out var importer, out _, out var dlg);
        dlg.PickOpenFileResult = null; // user cancelled

        await vm.ImportCommand.ExecuteAsync(null);

        importer.Verify(i => i.ParseWorkbookAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExportCommand_gathers_displayed_projects_and_exports()
    {
        var vm = Create(out var svc, out _, out var exporter, out var dlg);
        await vm.LoadAsync(); // Rows = 2 projects
        dlg.PickSaveFileResult = "/tmp/out.xlsx";
        svc.Setup(s => s.GetWithLinesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => new ConstructionFinance.Domain.Finance.Project { Id = id, Name = $"P{id}" });
        exporter.Setup(e => e.ExportAsync(It.IsAny<IReadOnlyList<ConstructionFinance.Domain.Finance.Project>>(), "/tmp/out.xlsx", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await vm.ExportCommand.ExecuteAsync(null);

        exporter.Verify(e => e.ExportAsync(It.Is<IReadOnlyList<ConstructionFinance.Domain.Finance.Project>>(l => l.Count == 2), "/tmp/out.xlsx", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExportCommand_noop_when_save_cancelled()
    {
        var vm = Create(out var svc, out _, out var exporter, out var dlg);
        await vm.LoadAsync();
        dlg.PickSaveFileResult = null; // cancelled

        await vm.ExportCommand.ExecuteAsync(null);

        exporter.Verify(e => e.ExportAsync(It.IsAny<IReadOnlyList<ConstructionFinance.Domain.Finance.Project>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ActivateProject_raises_the_activation_event_with_the_project_id()
    {
        var sut = Create(out _, out _, out _, out _);
        var raised = 0;
        sut.ProjectActivationRequested += (_, id) => raised = id;

        sut.ActivateProject(42);

        raised.ShouldBe(42);
    }
}
