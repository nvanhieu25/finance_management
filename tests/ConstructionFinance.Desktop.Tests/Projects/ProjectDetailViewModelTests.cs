using ConstructionFinance.Application.Common.Exceptions;
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

public class ProjectDetailViewModelTests
{
    private static ProjectDto P(ProjectStatus status = ProjectStatus.Planning) =>
        new(5, 7, "DA-1", "Nhà máy X", null, null, null, null, status, 1_000_000m, 0m, null);

    private static (ProjectDetailViewModel vm, Mock<IProjectService> svc, FakeDialogService dlg, FakeNavigationService nav) Create(ProjectStatus status = ProjectStatus.Planning)
    {
        var svc = new Mock<IProjectService>(MockBehavior.Strict);
        var dlg = new FakeDialogService();
        var nav = new FakeNavigationService();

        var sc = new ServiceCollection();
        var cost = new Mock<ICostService>(MockBehavior.Loose);
        cost.Setup(s => s.GetProjectTreeAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CostItemDto>());
        sc.AddSingleton(cost.Object);
        var pay = new Mock<IPaymentService>(MockBehavior.Loose);
        sc.AddSingleton(pay.Object);
        var units = new Mock<ICostUnitTypeService>(MockBehavior.Loose);
        units.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<CostUnitTypeDto>());
        sc.AddSingleton(units.Object);
        var vendors = new Mock<IVendorService>(MockBehavior.Loose);
        vendors.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<VendorDto>());
        sc.AddSingleton(vendors.Object);
        sc.AddSingleton<ConstructionFinance.Desktop.Services.IDialogService>(dlg);
        sc.AddTransient<ConstructionFinance.Desktop.ViewModels.CostItems.CostTreeViewModel>();
        var sp = sc.BuildServiceProvider();

        var vm = new ProjectDetailViewModel(P(status), svc.Object, dlg, nav, sp);
        return (vm, svc, dlg, nav);
    }

    [Fact]
    public void Exposes_Project_and_AvailableStatuses()
    {
        var (vm, _, _, _) = Create();
        vm.Project.ShouldNotBeNull();
        vm.AvailableStatuses.ShouldContain(ProjectStatus.InProgress);
    }

    [Fact]
    public void Exposes_CostTree_initialized_with_project_id_and_status()
    {
        var (vm, _, _, _) = Create(ProjectStatus.InProgress);
        vm.CostTree.ShouldNotBeNull();
        vm.CostTree.ProjectId.ShouldBe(5);
        vm.CostTree.IsReadOnly.ShouldBeFalse();
    }

    [Fact]
    public async Task ChangeStatusAsync_calls_service_and_updates_Project()
    {
        var (vm, svc, _, _) = Create(ProjectStatus.Planning);
        svc.Setup(s => s.UpdateStatusAsync(5, ProjectStatus.InProgress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(P(ProjectStatus.InProgress));

        await vm.ChangeStatusAsync(ProjectStatus.InProgress);

        vm.Project.Status.ShouldBe(ProjectStatus.InProgress);
    }

    [Fact]
    public async Task ChangeStatusAsync_to_Completed_makes_CostTree_read_only()
    {
        var (vm, svc, _, _) = Create(ProjectStatus.InProgress);
        svc.Setup(s => s.UpdateStatusAsync(5, ProjectStatus.Completed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(P(ProjectStatus.Completed));

        await vm.ChangeStatusAsync(ProjectStatus.Completed);

        vm.CostTree.IsReadOnly.ShouldBeTrue();
    }

    [Fact]
    public async Task ChangeStatusAsync_shows_error_on_BusinessRuleException()
    {
        var (vm, svc, dlg, _) = Create(ProjectStatus.Planning);
        svc.Setup(s => s.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<ProjectStatus>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessRuleException("Không hợp lệ"));

        await vm.ChangeStatusAsync(ProjectStatus.Completed);

        dlg.ShownMessages.ShouldContain(m => m.Contains("Không hợp lệ"));
        vm.Project.Status.ShouldBe(ProjectStatus.Planning);
    }

    [Fact]
    public void GoBackCommand_invokes_GoBack()
    {
        var (vm, _, _, nav) = Create();
        nav.NavigateTo("PREV");
        nav.NavigateTo(vm);

        vm.GoBackCommand.Execute(null);

        nav.CurrentContent.ShouldBe("PREV");
    }
}
