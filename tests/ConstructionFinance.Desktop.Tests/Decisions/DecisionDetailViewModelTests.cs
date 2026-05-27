using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Contracts;
using ConstructionFinance.Desktop.ViewModels.Decisions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Decisions;

public class DecisionDetailViewModelTests
{
    private static DecisionDto D(int id) =>
        new(id, "1234/QD", "Phê duyệt X", new DateOnly(2026, 3, 15), "UBND", 50_000_000_000m, null);

    [Fact]
    public void Exposes_Decision_property()
    {
        var dto = D(5);
        var sp = BuildServiceProvider();
        var nav = new FakeNavigationService();

        var vm = new DecisionDetailViewModel(dto, nav, sp);

        vm.Decision.ShouldBe(dto);
        vm.Decision.DecisionCode.ShouldBe("1234/QD");
    }

    [Fact]
    public void Exposes_Contracts_child_VM_scoped_to_this_decision()
    {
        var dto = D(5);
        var sp = BuildServiceProvider();
        var nav = new FakeNavigationService();

        var vm = new DecisionDetailViewModel(dto, nav, sp);

        vm.Contracts.ShouldNotBeNull();
        vm.Contracts.DecisionId.ShouldBe(5);
    }

    [Fact]
    public void GoBackCommand_invokes_NavigationService_GoBack()
    {
        var dto = D(5);
        var sp = BuildServiceProvider();
        var nav = new FakeNavigationService();
        nav.NavigateTo("Previous"); // so there's something to go back to
        var vm = new DecisionDetailViewModel(dto, nav, sp);
        nav.NavigateTo(vm);

        vm.GoBackCommand.Execute(null);

        nav.CurrentContent.ShouldBe("Previous");
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var sc = new ServiceCollection();
        var contractSvc = new Mock<IContractService>(MockBehavior.Loose);
        contractSvc.Setup(s => s.GetByDecisionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ContractDto>());
        sc.AddSingleton(contractSvc.Object);
        sc.AddSingleton<ConstructionFinance.Desktop.Services.IDialogService>(new FakeDialogService());
        sc.AddSingleton<ConstructionFinance.Desktop.Services.INavigationService>(new FakeNavigationService());
        sc.AddTransient<ContractListViewModel>();
        return sc.BuildServiceProvider();
    }
}
