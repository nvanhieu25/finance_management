using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.TongHop;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.TongHop;

public class ImportWizardViewModelTests
{
    private static (ImportWizardViewModel vm, Mock<IExcelImportService> import, Mock<IProjectSummaryService> summaries, FakeDialogService dlg)
        Create()
    {
        var import = new Mock<IExcelImportService>(MockBehavior.Strict);
        var summaries = new Mock<IProjectSummaryService>(MockBehavior.Strict);
        var dlg = new FakeDialogService();
        var vm = new ImportWizardViewModel(import.Object, summaries.Object, dlg);
        return (vm, import, summaries, dlg);
    }

    [Fact]
    public async Task BrowseCommand_opens_file_picker_and_parses()
    {
        var (vm, import, _, dlg) = Create();
        var path = Path.Combine(Path.GetTempPath(), "test.xlsx");
        dlg.PickOpenFileResult = path;
        import.Setup(s => s.ParseAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportResultDto("test.xlsx", "h", 1,
                new[] { new ProjectSummaryDto("DA1", "A", null, 0, 0, 0, 0, 0, 0, 0, 0, 0, Array.Empty<SettlementByYearDto>()) },
                Array.Empty<string>()));

        await vm.BrowseCommand.ExecuteAsync(null);

        vm.Preview.Count.ShouldBe(1);
        import.Verify(s => s.ParseAsync(path, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveAndGetBatchIdAsync_returns_null_when_no_file_loaded()
    {
        var (vm, _, summaries, _) = Create();
        var id = await vm.SaveAndGetBatchIdAsync();
        id.ShouldBeNull();
        summaries.Verify(s => s.SaveBatchAsync(It.IsAny<ImportResultDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveAndGetBatchIdAsync_calls_SaveBatchAsync_when_no_existing_batch()
    {
        var (vm, import, summaries, _) = Create();
        var path = "/tmp/f.xlsx";
        import.Setup(s => s.ParseAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportResultDto("f.xlsx", "h", 1,
                new[] { new ProjectSummaryDto("DA1", "A", null, 0, 0, 0, 0, 0, 0, 0, 0, 0, Array.Empty<SettlementByYearDto>()) },
                Array.Empty<string>()));
        summaries.Setup(s => s.FindBatchByFileNameAsync("f.xlsx", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ImportBatchDto?)null);
        summaries.Setup(s => s.SaveBatchAsync(It.IsAny<ImportResultDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(42);

        await vm.LoadFileAsync(path);
        var id = await vm.SaveAndGetBatchIdAsync();

        id.ShouldBe(42);
        summaries.Verify(s => s.ReplaceBatchAsync(It.IsAny<int>(), It.IsAny<ImportResultDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveAndGetBatchIdAsync_replaces_existing_batch_when_same_file_and_confirmed()
    {
        var (vm, import, summaries, dlg) = Create();
        dlg.ConfirmResult = true;
        var path = "/tmp/f.xlsx";
        import.Setup(s => s.ParseAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportResultDto("f.xlsx", "h", 1,
                new[] { new ProjectSummaryDto("DA1", "A", null, 0, 0, 0, 0, 0, 0, 0, 0, 0, Array.Empty<SettlementByYearDto>()) },
                Array.Empty<string>()));
        summaries.Setup(s => s.FindBatchByFileNameAsync("f.xlsx", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportBatchDto(7, "f.xlsx", DateTime.UtcNow, 1, 1, null, Array.Empty<string>()));
        summaries.Setup(s => s.ReplaceBatchAsync(7, It.IsAny<ImportResultDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(7);

        await vm.LoadFileAsync(path);
        var id = await vm.SaveAndGetBatchIdAsync();

        id.ShouldBe(7);
        summaries.Verify(s => s.ReplaceBatchAsync(7, It.IsAny<ImportResultDto>(), It.IsAny<CancellationToken>()), Times.Once);
        summaries.Verify(s => s.SaveBatchAsync(It.IsAny<ImportResultDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveAndGetBatchIdAsync_aborts_when_overwrite_declined()
    {
        var (vm, import, summaries, dlg) = Create();
        dlg.ConfirmResult = false;
        var path = "/tmp/f.xlsx";
        import.Setup(s => s.ParseAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportResultDto("f.xlsx", "h", 1,
                new[] { new ProjectSummaryDto("DA1", "A", null, 0, 0, 0, 0, 0, 0, 0, 0, 0, Array.Empty<SettlementByYearDto>()) },
                Array.Empty<string>()));
        summaries.Setup(s => s.FindBatchByFileNameAsync("f.xlsx", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportBatchDto(7, "f.xlsx", DateTime.UtcNow, 1, 1, null, Array.Empty<string>()));

        await vm.LoadFileAsync(path);
        var id = await vm.SaveAndGetBatchIdAsync();

        id.ShouldBeNull();
        summaries.Verify(s => s.ReplaceBatchAsync(It.IsAny<int>(), It.IsAny<ImportResultDto>(), It.IsAny<CancellationToken>()), Times.Never);
        summaries.Verify(s => s.SaveBatchAsync(It.IsAny<ImportResultDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
