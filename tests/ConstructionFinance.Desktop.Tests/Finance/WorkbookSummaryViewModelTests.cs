using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Domain.Finance;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class WorkbookSummaryViewModelTests
{
    private static ViTri ViTriWithAdvance(decimal advance) => new()
    {
        Name = "V", SortOrder = 0,
        CostGroups =
        {
            new CostGroup { Name = "G", SortOrder = 0, Packages =
            {
                new Package { Name = "P", SortOrder = 0, Dots =
                {
                    new Dot { Date = new DateOnly(2026, 3, 1), Fund = Fund.NSNN, Advance = advance, SortOrder = 0 }
                } }
            } }
        }
    };

    private static WorkbookSummaryViewModel CreateSut(params Project[] projects)
    {
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.ListTreesAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(projects);
        return new WorkbookSummaryViewModel(service.Object);
    }

    [Fact]
    public async Task LoadAsync_groups_each_project_then_a_grand_total()
    {
        var sut = CreateSut(
            new Project { Id = 1, Name = "Dự án 1", ViTris = { ViTriWithAdvance(10_000_000m) } },
            new Project { Id = 2, Name = "Dự án 2", ViTris = { ViTriWithAdvance(5_000_000m) } });

        await sut.LoadAsync();

        // I + vị trí, II + vị trí, grand total = 5 rows
        sut.Rows.Count.ShouldBe(5);
        sut.Rows[0].Ordinal.ShouldBe("I");
        sut.Rows[0].Label.ShouldBe("Dự án 1");
        sut.Rows[^1].Label.ShouldBe("Tổng cộng");
        sut.Rows[^1].NSNN.ShouldBe("15.000.000");
    }

    [Fact]
    public async Task Filter_narrows_to_matching_projects_and_recomputes_the_total()
    {
        var sut = CreateSut(
            new Project { Id = 1, Name = "Cầu A", ViTris = { ViTriWithAdvance(10_000_000m) } },
            new Project { Id = 2, Name = "Đường B", ViTris = { ViTriWithAdvance(5_000_000m) } });
        await sut.LoadAsync();

        sut.FilterText = "cầu";

        // Only "Cầu A": I + vị trí + grand total = 3 rows, total = its own 10tr
        sut.Rows.Count.ShouldBe(3);
        sut.Rows[0].Label.ShouldBe("Cầu A");
        sut.Rows[^1].NSNN.ShouldBe("10.000.000");
    }

    [Fact]
    public async Task Activate_raises_the_event_with_the_project_id()
    {
        var sut = CreateSut();
        int? activated = null;
        sut.ProjectActivationRequested += (_, id) => activated = id;

        sut.ActivateProject(42);

        activated.ShouldBe(42);
    }
}
