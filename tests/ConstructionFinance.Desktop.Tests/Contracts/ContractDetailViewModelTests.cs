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

public class ContractDetailViewModelTests
{
    private static ContractDto C(int id) =>
        new(id, 7, null, "HD-1", "Tên HĐ", new DateOnly(2026, 4, 1), null, 1_000_000m, null);

    [Fact]
    public void Exposes_Contract_and_Projects_child_VM()
    {
        var sp = BuildServiceProvider();
        var nav = new FakeNavigationService();

        var vm = new ContractDetailViewModel(C(5), nav, sp);

        vm.Contract.Id.ShouldBe(5);
        vm.Projects.ShouldNotBeNull();
        vm.Projects.ContractId.ShouldBe(5);
    }

    [Fact]
    public void GoBackCommand_invokes_GoBack()
    {
        var sp = BuildServiceProvider();
        var nav = new FakeNavigationService();
        nav.NavigateTo("PREV");
        var vm = new ContractDetailViewModel(C(5), nav, sp);
        nav.NavigateTo(vm);

        vm.GoBackCommand.Execute(null);

        nav.CurrentContent.ShouldBe("PREV");
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var sc = new ServiceCollection();
        var pSvc = new Mock<IProjectService>(MockBehavior.Loose);
        pSvc.Setup(s => s.GetByContractAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProjectDto>());
        sc.AddSingleton(pSvc.Object);
        sc.AddSingleton<ConstructionFinance.Desktop.Services.IDialogService>(new FakeDialogService());
        sc.AddSingleton<ConstructionFinance.Desktop.Services.INavigationService>(new FakeNavigationService());
        sc.AddTransient<ProjectListViewModel>();
        return sc.BuildServiceProvider();
    }
}
