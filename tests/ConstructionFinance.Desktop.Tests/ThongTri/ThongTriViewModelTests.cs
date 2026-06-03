using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.ViewModels.ThongTri;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.ThongTri;

public class ThongTriViewModelTests
{
    private static ProjectSummaryDto P(string name) =>
        new("DA1", name, "NT", 1000m, 100m, 200m, 50m, 150m, 50m, 800m, 0m, 800m, Array.Empty<SettlementByYearDto>());

    [Fact]
    public async Task LoadAsync_populates_project_list_from_latest_batch()
    {
        var svc = new Mock<IProjectSummaryService>(MockBehavior.Strict);
        svc.Setup(s => s.GetLatestBatchAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportBatchDto(1, "H.xlsx", DateTime.UtcNow, 6, 2, null, Array.Empty<string>()));
        svc.Setup(s => s.ListByBatchAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { P("A"), P("B") });

        var vm = new ThongTriViewModel(svc.Object);
        await vm.LoadAsync();

        vm.Projects.Count.ShouldBe(2);
    }

    [Fact]
    public void SelectProject_populates_detail_fields_from_snapshot()
    {
        var vm = new ThongTriViewModel(Mock.Of<IProjectSummaryService>());
        var p = P("Dự án X");
        vm.SelectedProject = p;

        vm.SelectedProject!.ProjectName.ShouldBe("Dự án X");
        vm.StatusMessage.ShouldContain("Dự án X");
    }
}
