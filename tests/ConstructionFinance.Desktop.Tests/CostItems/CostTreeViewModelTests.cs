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

    [Fact]
    public async Task AddRootCommand_calls_service_with_correct_dto()
    {
        var (vm, cost, _, _, _) = Create();
        cost.Setup(s => s.CreateNodeAsync(It.IsAny<CreateCostItemDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Dto(99, null, "Mới", 0, "/99/", isLeaf: false));
        cost.Setup(s => s.RecalculateRollupAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await vm.LoadAsync();
        await vm.AddRootCommand.ExecuteAsync(null);

        cost.Verify(s => s.CreateNodeAsync(
            It.Is<CreateCostItemDto>(d => d.ProjectId == 7 && d.ParentId == null && !d.IsLeaf),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddChildCommand_passes_selected_node_as_parent_and_marks_it_non_leaf()
    {
        var (vm, cost, _, _, _) = Create(new[]
        {
            Dto(1, null, "Root", 0, "/1/", isLeaf: true, estimated: 100m)
        });
        cost.Setup(s => s.CreateNodeAsync(It.IsAny<CreateCostItemDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Dto(2, 1, "Con", 1, "/1/2/", isLeaf: true));
        cost.Setup(s => s.RecalculateRollupAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await vm.LoadAsync();
        vm.SelectedNode = vm.RootNodes[0];

        await vm.AddChildCommand.ExecuteAsync(null);

        cost.Verify(s => s.CreateNodeAsync(
            It.Is<CreateCostItemDto>(d => d.ParentId == 1 && d.IsLeaf),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddChildCommand_disabled_when_no_selection()
    {
        var (vm, _, _, _, _) = Create();
        await vm.LoadAsync();
        vm.SelectedNode = null;
        vm.AddChildCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task AddSiblingCommand_uses_selected_node_ParentId_and_IsLeaf()
    {
        var (vm, cost, _, _, _) = Create(new[]
        {
            Dto(1, null, "P", 0, "/1/", isLeaf: false),
            Dto(2, 1,    "A", 1, "/1/2/", isLeaf: true)
        });
        cost.Setup(s => s.CreateNodeAsync(It.IsAny<CreateCostItemDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Dto(3, 1, "B", 1, "/1/3/", isLeaf: true));
        cost.Setup(s => s.RecalculateRollupAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await vm.LoadAsync();
        vm.SelectedNode = vm.RootNodes[0].Children[0];

        await vm.AddSiblingCommand.ExecuteAsync(null);

        cost.Verify(s => s.CreateNodeAsync(
            It.Is<CreateCostItemDto>(d => d.ParentId == 1 && d.IsLeaf),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCommand_calls_service_when_user_confirms()
    {
        var (vm, cost, _, _, dlg) = Create(new[]
        {
            Dto(1, null, "Root", 0, "/1/", isLeaf: true)
        });
        cost.Setup(s => s.DeleteNodeAsync(1, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        cost.Setup(s => s.RecalculateRollupAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        dlg.ConfirmResult = true;

        await vm.LoadAsync();
        vm.SelectedNode = vm.RootNodes[0];

        await vm.DeleteCommand.ExecuteAsync(null);

        cost.Verify(s => s.DeleteNodeAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCommand_skipped_when_user_declines()
    {
        var (vm, cost, _, _, dlg) = Create(new[]
        {
            Dto(1, null, "Root", 0, "/1/", isLeaf: true)
        });
        dlg.ConfirmResult = false;
        await vm.LoadAsync();
        vm.SelectedNode = vm.RootNodes[0];

        await vm.DeleteCommand.ExecuteAsync(null);

        cost.Verify(s => s.DeleteNodeAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Commands_disabled_when_IsReadOnly()
    {
        var (vm, _, _, _, _) = Create();
        vm.SetProject(projectId: 7, status: ProjectStatus.Completed);
        await vm.LoadAsync();

        vm.AddRootCommand.CanExecute(null).ShouldBeFalse();

        var node = new CostTreeNode(Dto(1, null, "X", 0, "/1/", isLeaf: true));
        vm.SelectedNode = node;
        vm.AddChildCommand.CanExecute(null).ShouldBeFalse();
        vm.AddSiblingCommand.CanExecute(null).ShouldBeFalse();
        vm.DeleteCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task IndentCommand_moves_node_under_previous_sibling()
    {
        // root has children A, B. Selecting B + Indent → B becomes child of A.
        var data = new[]
        {
            Dto(1, null, "root", 0, "/1/", isLeaf: false),
            Dto(2, 1, "A", 1, "/1/2/", isLeaf: true),
            Dto(3, 1, "B", 1, "/1/3/", isLeaf: true)
        };
        var (vm, cost, _, _, _) = Create(data);
        cost.Setup(s => s.MoveNodeAsync(3, 2, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        cost.Setup(s => s.RecalculateRollupAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await vm.LoadAsync();
        vm.SelectedNode = vm.RootNodes[0].Children.Single(c => c.ItemName == "B");

        await vm.IndentCommand.ExecuteAsync(null);

        cost.Verify(s => s.MoveNodeAsync(3, 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IndentCommand_disabled_when_node_has_no_previous_sibling()
    {
        var data = new[]
        {
            Dto(1, null, "root", 0, "/1/", isLeaf: false),
            Dto(2, 1, "A", 1, "/1/2/", isLeaf: true)
        };
        var (vm, _, _, _, _) = Create(data);
        await vm.LoadAsync();
        vm.SelectedNode = vm.RootNodes[0].Children[0];

        vm.IndentCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task OutdentCommand_moves_node_to_grandparent_as_sibling()
    {
        var data = new[]
        {
            Dto(1, null, "root", 0, "/1/", isLeaf: false),
            Dto(2, 1, "A", 1, "/1/2/", isLeaf: false),
            Dto(3, 2, "A1", 2, "/1/2/3/", isLeaf: true)
        };
        var (vm, cost, _, _, _) = Create(data);
        cost.Setup(s => s.MoveNodeAsync(3, 1, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        cost.Setup(s => s.RecalculateRollupAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await vm.LoadAsync();
        vm.SelectedNode = vm.RootNodes[0].Children[0].Children[0];

        await vm.OutdentCommand.ExecuteAsync(null);

        cost.Verify(s => s.MoveNodeAsync(3, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OutdentCommand_disabled_when_node_already_at_root_level()
    {
        var data = new[]
        {
            Dto(1, null, "root", 0, "/1/", isLeaf: true)
        };
        var (vm, _, _, _, _) = Create(data);
        await vm.LoadAsync();
        vm.SelectedNode = vm.RootNodes[0];

        vm.OutdentCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task ApplyEditAsync_calls_UpdateNodeAsync_and_rollup()
    {
        var data = new[]
        {
            Dto(1, null, "X", 0, "/1/", isLeaf: true, estimated: 100m)
        };
        var (vm, cost, _, _, _) = Create(data);
        cost.Setup(s => s.UpdateNodeAsync(It.IsAny<UpdateCostItemDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Dto(1, null, "Y", 0, "/1/", isLeaf: true, estimated: 200m));
        cost.Setup(s => s.RecalculateRollupAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await vm.LoadAsync();
        var node = vm.RootNodes[0];
        node.ItemName = "Y";
        node.Quantity = 10m;
        node.UnitPrice = 20m;

        await vm.ApplyEditAsync(node);

        cost.Verify(s => s.UpdateNodeAsync(
            It.Is<UpdateCostItemDto>(d => d.Id == 1 && d.ItemName == "Y" && d.Quantity == 10m && d.UnitPrice == 20m),
            It.IsAny<CancellationToken>()), Times.Once);
        cost.Verify(s => s.RecalculateRollupAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ApplyEditAsync_does_nothing_when_IsReadOnly()
    {
        var (vm, cost, _, _, _) = Create(new[] { Dto(1, null, "X", 0, "/1/", isLeaf: true) });
        await vm.LoadAsync();
        // Switch to Completed AFTER load so the node exists in RootNodes.
        vm.SetProject(7, ProjectStatus.Completed);
        var node = vm.RootNodes[0];
        node.ItemName = "Y";

        await vm.ApplyEditAsync(node);

        cost.Verify(s => s.UpdateNodeAsync(It.IsAny<UpdateCostItemDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
