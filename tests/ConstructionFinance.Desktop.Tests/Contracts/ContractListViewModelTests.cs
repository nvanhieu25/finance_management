using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Contracts;
using ConstructionFinance.Desktop.ViewModels.Projects;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Contracts;

public class ContractListViewModelTests
{
    private static ContractDto C(int id, int decisionId, string code) =>
        new(id, decisionId, null, code, "Tên HĐ", new DateOnly(2026, 1, 1), null, 1_000_000m, null);

    private static (ContractListViewModel vm, Mock<IContractService> svc, FakeDialogService dlg, FakeNavigationService nav) Create(int decisionId, IEnumerable<ContractDto>? initial = null)
    {
        var svc = new Mock<IContractService>(MockBehavior.Strict);
        svc.Setup(s => s.GetByDecisionAsync(decisionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((initial ?? Array.Empty<ContractDto>()).ToList());
        var dlg = new FakeDialogService();
        var nav = new FakeNavigationService();
        var sc = new ServiceCollection();
        var projectSvc = new Mock<IProjectService>(MockBehavior.Loose);
        projectSvc.Setup(s => s.GetByContractAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProjectDto>());
        sc.AddSingleton(projectSvc.Object);
        sc.AddSingleton<ConstructionFinance.Desktop.Services.IDialogService>(dlg);
        sc.AddSingleton<ConstructionFinance.Desktop.Services.INavigationService>(nav);
        sc.AddTransient<ProjectListViewModel>();
        var sp = sc.BuildServiceProvider();
        var vm = new ContractListViewModel(svc.Object, dlg, nav, sp);
        vm.SetDecisionId(decisionId);
        return (vm, svc, dlg, nav);
    }

    [Fact]
    public async Task LoadAsync_uses_GetByDecisionAsync()
    {
        var (vm, svc, _, _) = Create(decisionId: 7, new[] { C(1, 7, "HD-1"), C(2, 7, "HD-2") });
        await vm.LoadAsync();
        vm.Contracts.Count.ShouldBe(2);
        svc.Verify(s => s.GetByDecisionAsync(7, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddCommand_creates_contract_under_current_decision()
    {
        var (vm, svc, dlg, _) = Create(decisionId: 7);
        svc.Setup(s => s.CreateAsync(It.Is<CreateContractDto>(d => d.ApprovalDecisionId == 7), It.IsAny<CancellationToken>()))
            .ReturnsAsync(C(99, 7, "NEW"));
        dlg.DialogResult = true;
        await vm.LoadAsync();

        await vm.AddCommand.ExecuteAsync(null);

        svc.Verify(s => s.CreateAsync(It.Is<CreateContractDto>(d => d.ApprovalDecisionId == 7), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCommand_calls_service_and_removes_from_list()
    {
        var (vm, svc, dlg, _) = Create(decisionId: 7, new[] { C(1, 7, "HD") });
        svc.Setup(s => s.DeleteAsync(1, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        dlg.ConfirmResult = true;
        await vm.LoadAsync();
        vm.SelectedContract = vm.Contracts[0];

        await vm.DeleteCommand.ExecuteAsync(null);

        vm.Contracts.ShouldBeEmpty();
    }

    [Fact]
    public async Task OpenDetailCommand_navigates_to_ContractDetailViewModel()
    {
        var (vm, _, _, nav) = Create(decisionId: 7, new[] { C(1, 7, "HD") });
        await vm.LoadAsync();
        vm.SelectedContract = vm.Contracts[0];

        await vm.OpenDetailCommand.ExecuteAsync(null);

        nav.NavigationHistory.Count.ShouldBe(1);
        nav.NavigationHistory[0].ShouldBeOfType<ContractDetailViewModel>();
    }
}
