using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.TongHop;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.TongHop;

public class TongHopViewModelTests
{
    private static (TongHopViewModel vm, Mock<IProjectSummaryService> summaries, Mock<IExcelExportService> export, FakeDialogService dlg)
        Create(IReadOnlyList<ProjectSummaryDto>? data = null, ImportBatchDto? batch = null)
    {
        var summaries = new Mock<IProjectSummaryService>(MockBehavior.Strict);
        var export = new Mock<IExcelExportService>(MockBehavior.Loose);
        var dlg = new FakeDialogService();

        var b = batch ?? new ImportBatchDto(1, "H.xlsx", DateTime.UtcNow, 6, data?.Count ?? 0, null, Array.Empty<string>());
        summaries.Setup(s => s.ListBatchesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<ImportBatchDto> { b });
        summaries.Setup(s => s.GetLatestBatchAsync(It.IsAny<CancellationToken>())).ReturnsAsync(b);
        summaries.Setup(s => s.ListByBatchAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(data ?? Array.Empty<ProjectSummaryDto>());
        summaries.Setup(s => s.AggregateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TongHopAggregateDto(1, data?.Count ?? 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, Array.Empty<YearAggregateDto>()));

        var sc = new ServiceCollection();
        sc.AddSingleton(summaries.Object);
        sc.AddSingleton(export.Object);
        sc.AddSingleton<IDialogService>(dlg);
        sc.AddTransient<ImportWizardViewModel>();
        var sp = sc.BuildServiceProvider();

        var vm = new TongHopViewModel(summaries.Object, export.Object, dlg, sp);
        return (vm, summaries, export, dlg);
    }

    private static ProjectSummaryDto P(string name) =>
        new("DA1", name, "NT", 1000m, 0, 0, 0, 0, 0, 800m, 0m, 800m, Array.Empty<SettlementByYearDto>());

    [Fact]
    public async Task LoadAsync_loads_latest_batch_summaries()
    {
        var (vm, _, _, _) = Create(new[] { P("A"), P("B") });
        await vm.LoadAsync();
        vm.Rows.Count.ShouldBe(2);
    }

    [Fact]
    public async Task BatchChangedCommand_reloads_summaries_for_selected_batch()
    {
        var (vm, summaries, _, _) = Create(new[] { P("A") });
        await vm.LoadAsync();
        summaries.Setup(s => s.ListByBatchAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { P("Z") });
        summaries.Setup(s => s.AggregateAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TongHopAggregateDto(2, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, Array.Empty<YearAggregateDto>()));

        vm.SelectedBatch = new ImportBatchDto(2, "other.xlsx", DateTime.UtcNow, 1, 1, null, Array.Empty<string>());
        await Task.Delay(50);
        vm.Rows.Count.ShouldBe(1);
        vm.Rows[0].ProjectName.ShouldBe("Z");
    }

    [Fact]
    public async Task ExportCommand_calls_ExportTongHopAsync_with_current_summaries()
    {
        var data = new[] { P("A") };
        var (vm, _, export, dlg) = Create(data);
        await vm.LoadAsync();
        dlg.PickOpenFileResult = "/tmp/template.xlsx";
        dlg.PickSaveFileResult = "/tmp/out.xlsx";
        export.Setup(s => s.ExportTongHopAsync("/tmp/template.xlsx", "/tmp/out.xlsx", data, It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/out.xlsx");

        await vm.ExportCommand.ExecuteAsync(null);

        export.Verify(s => s.ExportTongHopAsync("/tmp/template.xlsx", "/tmp/out.xlsx", data, It.IsAny<CancellationToken>()), Times.Once);
    }
}
