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
    public async Task SaveAndGetBatchIdAsync_calls_SaveBatchAsync_when_parsed()
    {
        var (vm, import, summaries, _) = Create();
        var path = "/tmp/f.xlsx";
        import.Setup(s => s.ParseAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportResultDto("f.xlsx", "h", 1,
                new[] { new ProjectSummaryDto("DA1", "A", null, 0, 0, 0, 0, 0, 0, 0, 0, 0, Array.Empty<SettlementByYearDto>()) },
                Array.Empty<string>()));
        summaries.Setup(s => s.SaveBatchAsync(It.IsAny<ImportResultDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(42);

        await vm.LoadFileAsync(path);
        var id = await vm.SaveAndGetBatchIdAsync();

        id.ShouldBe(42);
    }
}
