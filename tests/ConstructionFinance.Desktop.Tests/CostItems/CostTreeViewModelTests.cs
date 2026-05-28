using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.CostItems;
using ConstructionFinance.Domain.Enums;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.CostItems;

public class CostTreeViewModelTests
{
    private static CostItemDto Dto(int id, int? parentId, string name, int level, string path, bool isLeaf = false, decimal estimated = 0) =>
        new(id, 1, parentId, null, null, null, name, level, path, 0, isLeaf, 0, 0, estimated, null, null);

    private static (CostTreeViewModel vm, Mock<ICostService> cost, Mock<IPaymentService> pay, Mock<ICostUnitTypeService> units, FakeDialogService dlg)
        Create(IEnumerable<CostItemDto>? initial = null)
    {
        var cost = new Mock<ICostService>(MockBehavior.Strict);
        cost.Setup(s => s.GetProjectTreeAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((initial ?? Array.Empty<CostItemDto>()).ToList());
        var pay = new Mock<IPaymentService>(MockBehavior.Loose);
        var units = new Mock<ICostUnitTypeService>(MockBehavior.Loose);
        units.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<CostUnitTypeDto>());
        var dlg = new FakeDialogService();
        var vm = new CostTreeViewModel(cost.Object, pay.Object, units.Object, dlg);
        vm.SetProject(projectId: 7, status: ProjectStatus.InProgress);
        return (vm, cost, pay, units, dlg);
    }

    [Fact]
    public async Task LoadAsync_builds_hierarchy_from_flat_list()
    {
        // Tree shape:
        //   root1 (id=1, leaf=false)
        //     child1a (id=2, parent=1, leaf=true)
        //     child1b (id=3, parent=1, leaf=false)
        //       grandchild (id=4, parent=3, leaf=true)
        //   root2 (id=5, leaf=true)
        var data = new[]
        {
            Dto(1, null, "Xây dựng", 0, "/1/", isLeaf: false, estimated: 10_000_000m),
            Dto(2, 1,    "Móng",     1, "/1/2/", isLeaf: true,  estimated: 5_000_000m),
            Dto(3, 1,    "Cột",      1, "/1/3/", isLeaf: false),
            Dto(4, 3,    "Cột tầng 1", 2, "/1/3/4/", isLeaf: true, estimated: 2_000_000m),
            Dto(5, null, "Thiết bị", 0, "/5/", isLeaf: true,  estimated: 8_000_000m)
        };
        var (vm, _, _, _, _) = Create(data);

        await vm.LoadAsync();

        vm.RootNodes.Count.ShouldBe(2);
        var root1 = vm.RootNodes[0];
        root1.ItemName.ShouldBe("Xây dựng");
        root1.Children.Count.ShouldBe(2);
        var child1b = root1.Children.Single(c => c.ItemName == "Cột");
        child1b.Children.Count.ShouldBe(1);
        child1b.Children[0].ItemName.ShouldBe("Cột tầng 1");
        vm.RootNodes[1].ItemName.ShouldBe("Thiết bị");
    }

    [Fact]
    public async Task LoadAsync_with_empty_returns_empty_RootNodes()
    {
        var (vm, _, _, _, _) = Create();
        await vm.LoadAsync();
        vm.RootNodes.ShouldBeEmpty();
    }

    [Fact]
    public void IsReadOnly_true_when_project_Completed()
    {
        var (vm, _, _, _, _) = Create();
        vm.SetProject(projectId: 7, status: ProjectStatus.Completed);
        vm.IsReadOnly.ShouldBeTrue();
    }

    [Fact]
    public void IsReadOnly_false_when_project_InProgress()
    {
        var (vm, _, _, _, _) = Create();
        vm.IsReadOnly.ShouldBeFalse();
    }

    [Fact]
    public async Task LoadAsync_computes_GrandTotal_from_root_nodes()
    {
        var data = new[]
        {
            Dto(1, null, "A", 0, "/1/", isLeaf: false, estimated: 3_000m),
            Dto(2, null, "B", 0, "/2/", isLeaf: true,  estimated: 2_000m),
            // Child does NOT add to GrandTotal — only roots
            Dto(3, 1, "A1", 1, "/1/3/", isLeaf: true, estimated: 1_000m)
        };
        var (vm, _, _, _, _) = Create(data);

        await vm.LoadAsync();

        vm.GrandTotal.ShouldBe(5_000m);
    }
}
