using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Decisions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Decisions;

public class DecisionListViewModelTests
{
    private static DecisionDto D(int id, string code) =>
        new(id, code, "Tên QĐ", new DateOnly(2026, 1, 1), "UBND", 1_000_000m, null);

    private static (DecisionListViewModel vm, Mock<IDecisionService> svc, FakeDialogService dlg, FakeNavigationService nav) Create(IEnumerable<DecisionDto>? initial = null)
    {
        var svc = new Mock<IDecisionService>(MockBehavior.Strict);
        svc.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((initial ?? Array.Empty<DecisionDto>()).ToList());
        var dlg = new FakeDialogService();
        var nav = new FakeNavigationService();
        var sc = new ServiceCollection();
        var contractSvc = new Mock<ConstructionFinance.Application.Services.Interfaces.IContractService>(MockBehavior.Loose);
        contractSvc.Setup(s => s.GetByDecisionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ContractDto>());
        sc.AddSingleton(contractSvc.Object);
        sc.AddSingleton<ConstructionFinance.Desktop.Services.IDialogService>(dlg);
        sc.AddSingleton<ConstructionFinance.Desktop.Services.INavigationService>(nav);
        sc.AddTransient<ConstructionFinance.Desktop.ViewModels.Contracts.ContractListViewModel>();
        var sp = sc.BuildServiceProvider();
        var vm = new DecisionListViewModel(svc.Object, dlg, nav, sp);
        return (vm, svc, dlg, nav);
    }

    [Fact]
    public async Task LoadAsync_populates_Decisions()
    {
        var (vm, _, _, _) = Create(new[] { D(1, "1234/QD"), D(2, "5678/QD") });
        await vm.LoadAsync();
        vm.Decisions.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AddCommand_opens_dialog_and_creates_on_confirm()
    {
        var (vm, svc, dlg, _) = Create();
        svc.Setup(s => s.CreateAsync(It.IsAny<CreateDecisionDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(D(99, "NEW"));
        dlg.DialogResult = true;
        await vm.LoadAsync();

        await vm.AddCommand.ExecuteAsync(null);

        svc.Verify(s => s.CreateAsync(It.IsAny<CreateDecisionDto>(), It.IsAny<CancellationToken>()), Times.Once);
        vm.Decisions.ShouldContain(d => d.DecisionCode == "NEW");
    }

    [Fact]
    public async Task DeleteCommand_calls_service_when_confirmed()
    {
        var (vm, svc, dlg, _) = Create(new[] { D(1, "X") });
        svc.Setup(s => s.DeleteAsync(1, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        dlg.ConfirmResult = true;
        await vm.LoadAsync();
        vm.SelectedDecision = vm.Decisions[0];

        await vm.DeleteCommand.ExecuteAsync(null);

        svc.Verify(s => s.DeleteAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        vm.Decisions.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteCommand_shows_error_message_on_BusinessRuleException()
    {
        var (vm, svc, dlg, _) = Create(new[] { D(1, "X") });
        svc.Setup(s => s.DeleteAsync(1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConstructionFinance.Application.Common.Exceptions.BusinessRuleException("Còn HĐ"));
        dlg.ConfirmResult = true;
        await vm.LoadAsync();
        vm.SelectedDecision = vm.Decisions[0];

        await vm.DeleteCommand.ExecuteAsync(null);

        dlg.ShownMessages.ShouldContain(m => m.Contains("Còn HĐ"));
        vm.Decisions.Count.ShouldBe(1); // not removed
    }

    [Fact]
    public async Task OpenDetailCommand_navigates_when_selection_set()
    {
        var (vm, _, _, nav) = Create(new[] { D(1, "X") });
        await vm.LoadAsync();
        vm.SelectedDecision = vm.Decisions[0];

        await vm.OpenDetailCommand.ExecuteAsync(null);

        nav.NavigationHistory.Count.ShouldBe(1);
        nav.NavigationHistory[0].ShouldBeOfType<DecisionDetailViewModel>();
    }
}
