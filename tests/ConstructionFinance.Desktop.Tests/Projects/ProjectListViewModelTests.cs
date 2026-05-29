using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Projects;
using ConstructionFinance.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Projects;

public class ProjectListViewModelTests
{
    private static ProjectDto P(int id, int contractId, string code) =>
        new(id, contractId, code, "Nhà máy X", null, null, null, null, ProjectStatus.Planning, 1_000_000m, 0m, null);

    private static (ProjectListViewModel vm, Mock<IProjectService> svc, FakeDialogService dlg, FakeNavigationService nav) Create(int contractId, IEnumerable<ProjectDto>? initial = null)
    {
        var svc = new Mock<IProjectService>(MockBehavior.Strict);
        svc.Setup(s => s.GetByContractAsync(contractId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((initial ?? Array.Empty<ProjectDto>()).ToList());
        var dlg = new FakeDialogService();
        var nav = new FakeNavigationService();

        // OpenDetailCommand resolves CostTreeViewModel via _services. Register a minimal
        // graph so the navigation test does not blow up at resolve time.
        var sc = new ServiceCollection();
        var cost = new Mock<ICostService>(MockBehavior.Loose);
        cost.Setup(s => s.GetProjectTreeAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CostItemDto>());
        sc.AddSingleton(cost.Object);
        sc.AddSingleton(new Mock<IPaymentService>(MockBehavior.Loose).Object);
        var units = new Mock<ICostUnitTypeService>(MockBehavior.Loose);
        units.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<CostUnitTypeDto>());
        sc.AddSingleton(units.Object);
        var vendors = new Mock<IVendorService>(MockBehavior.Loose);
        vendors.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<VendorDto>());
        sc.AddSingleton(vendors.Object);
        sc.AddSingleton<ConstructionFinance.Desktop.Services.IDialogService>(dlg);
        sc.AddTransient<ConstructionFinance.Desktop.ViewModels.CostItems.CostTreeViewModel>();
        var sp = sc.BuildServiceProvider();

        var vm = new ProjectListViewModel(svc.Object, dlg, nav, sp);
        vm.SetContractId(contractId);
        return (vm, svc, dlg, nav);
    }

    [Fact]
    public async Task LoadAsync_uses_GetByContractAsync()
    {
        var (vm, svc, _, _) = Create(7, new[] { P(1, 7, "DA-1") });
        await vm.LoadAsync();
        vm.Projects.Count.ShouldBe(1);
        svc.Verify(s => s.GetByContractAsync(7, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddCommand_creates_project_under_current_contract()
    {
        var (vm, svc, dlg, _) = Create(7);
        svc.Setup(s => s.CreateAsync(It.Is<CreateProjectDto>(d => d.ContractId == 7), It.IsAny<CancellationToken>()))
            .ReturnsAsync(P(99, 7, "NEW"));
        dlg.DialogResult = true;
        await vm.LoadAsync();

        await vm.AddCommand.ExecuteAsync(null);

        svc.Verify(s => s.CreateAsync(It.Is<CreateProjectDto>(d => d.ContractId == 7), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCommand_calls_service_when_confirmed()
    {
        var (vm, svc, dlg, _) = Create(7, new[] { P(1, 7, "DA-1") });
        svc.Setup(s => s.DeleteAsync(1, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        dlg.ConfirmResult = true;
        await vm.LoadAsync();
        vm.SelectedProject = vm.Projects[0];

        await vm.DeleteCommand.ExecuteAsync(null);

        vm.Projects.ShouldBeEmpty();
    }

    [Fact]
    public async Task OpenDetailCommand_navigates_to_ProjectDetailViewModel()
    {
        var (vm, _, _, nav) = Create(7, new[] { P(1, 7, "DA-1") });
        await vm.LoadAsync();
        vm.SelectedProject = vm.Projects[0];

        await vm.OpenDetailCommand.ExecuteAsync(null);

        nav.NavigationHistory[0].ShouldBeOfType<ProjectDetailViewModel>();
    }
}
