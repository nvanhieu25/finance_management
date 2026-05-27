using ConstructionFinance.Application.Common.Exceptions;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Projects;
using ConstructionFinance.Domain.Enums;
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
        var vm = new ProjectDetailViewModel(P(status), svc.Object, dlg, nav);
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
    public async Task ChangeStatusAsync_calls_service_and_updates_Project()
    {
        var (vm, svc, _, _) = Create(ProjectStatus.Planning);
        svc.Setup(s => s.UpdateStatusAsync(5, ProjectStatus.InProgress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(P(ProjectStatus.InProgress));

        await vm.ChangeStatusAsync(ProjectStatus.InProgress);

        vm.Project.Status.ShouldBe(ProjectStatus.InProgress);
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
