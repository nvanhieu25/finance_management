using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Desktop.ViewModels.Finance.Forms;
using ConstructionFinance.Domain.Finance;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class FinanceWorkbookViewModelTests
{
    /// Shared stub for the two Task 13 constructor params these pre-existing tests
    /// don't otherwise care about.
    private static ICompanyInfoService CreateCompanyInfo()
    {
        var mock = new Mock<ICompanyInfoService>();
        mock.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyInfoDto(null, null, null, null, null, null, null, null));
        return mock.Object;
    }

    private static FinanceWorkbookViewModel CreateSutWithProjects(params (int Id, string Name)[] projects) =>
        CreateSutWithConfirmingDialog(projects).Sut;

    /// Builds a SUT whose service/dialog mocks the caller can assert against.
    /// The dialog confirms everything (returns true).
    private static (FinanceWorkbookViewModel Sut, Mock<IFinanceProjectService> Service)
        CreateSutWithConfirmingDialog(params (int Id, string Name)[] projects)
    {
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(projects.Select(p => new Project { Id = p.Id, Name = p.Name }).ToList());
        foreach (var p in projects)
        {
            var captured = p;
            service.Setup(s => s.GetTreeAsync(captured.Id, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new Project { Id = captured.Id, Name = captured.Name });
        }

        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>()))
              .ReturnsAsync(true);

        var sut = new FinanceWorkbookViewModel(
            service.Object,
            dialog.Object,
            Mock.Of<IFormExcelExporter>(),
            CreateCompanyInfo());

        return (sut, service);
    }

    // ===== 7B: Dự án picker + Vị trí tabs =====

    /// Builds a SUT whose ListAsync/GetTreeAsync are wired for exactly one project
    /// (with the given Vị trí tree), plus a confirming dialog (for DeleteViTri).
    private static (FinanceWorkbookViewModel Sut, Mock<IFinanceProjectService> Service)
        CreateSutWithProjectTree(int projectId, string projectName, params ViTri[] viTris)
    {
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<Project> { new() { Id = projectId, Name = projectName } });
        service.Setup(s => s.GetTreeAsync(projectId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Project { Id = projectId, Name = projectName, ViTris = viTris.ToList() });

        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        var sut = new FinanceWorkbookViewModel(
            service.Object,
            dialog.Object,
            Mock.Of<IFormExcelExporter>(),
            CreateCompanyInfo());

        return (sut, service);
    }

    [Fact]
    public async Task LoadAsync_populates_the_Projects_picker_and_auto_selects_the_first_project()
    {
        var (sut, _) = CreateSutWithProjectTree(1, "DA1");

        await sut.LoadAsync();

        sut.Projects.Count.ShouldBe(1);
        sut.Projects[0].Name.ShouldBe("DA1");
        // Auto-selected so the Vị trí ledger renders immediately on launch instead
        // of an empty picker (no manual pick required for the common single/first
        // project case).
        sut.SelectedProject.ShouldBe(sut.Projects[0]);
    }

    [Fact]
    public async Task LoadAsync_with_no_projects_leaves_the_picker_unselected()
    {
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(Array.Empty<Project>());

        var sut = new FinanceWorkbookViewModel(
            service.Object,
            Mock.Of<IDialogService>(),
            Mock.Of<IFormExcelExporter>(),
            CreateCompanyInfo());

        await sut.LoadAsync();

        sut.Projects.ShouldBeEmpty();
        sut.SelectedProject.ShouldBeNull();
        sut.ViTriPages.ShouldBeEmpty();
    }

    [Fact]
    public async Task Selecting_a_project_builds_one_ViTriPage_per_ViTri()
    {
        var (sut, _) = CreateSutWithProjectTree(1, "DA1",
            new ViTri { Name = "V1", SortOrder = 0 },
            new ViTri { Name = "V2", SortOrder = 1 });
        await sut.LoadAsync();

        sut.SelectedProject = sut.Projects.Single();

        // [Tổng hợp][V1][V2][Quyết toán năm][Thông tri][KS chi][GĐNTT] — the strip now
        // ends with three fixed form tabs after Quyết toán năm (Task 13).
        sut.ViTriPages.Count.ShouldBe(7);
        sut.ViTriPages[0].ShouldBeOfType<ProjectTongHopPageViewModel>();
        sut.ViTriPages[0].Title.ShouldBe("Tổng hợp");
        var ledgers = sut.ViTriPages.OfType<ViTriPageViewModel>().ToList();
        ledgers.Count.ShouldBe(2);
        ledgers[0].Title.ShouldBe("V1");
        ledgers[1].Title.ShouldBe("V2");
        sut.ViTriPages[^4].ShouldBeOfType<YearSettlementPageViewModel>();
        sut.ViTriPages[^4].Title.ShouldBe("Quyết toán năm");
        sut.ViTriPages[^3].ShouldBeOfType<FormTabPageViewModel>();
        sut.ViTriPages[^2].ShouldBeOfType<FormTabPageViewModel>();
        sut.ViTriPages[^1].ShouldBeOfType<FormTabPageViewModel>();
        sut.SelectedViTriPage.ShouldBe(sut.ViTriPages[0]);   // lands on Tổng hợp
    }

    [Fact]
    public async Task Deselecting_the_project_clears_the_ViTri_tabs()
    {
        var (sut, _) = CreateSutWithProjectTree(1, "DA1", new ViTri { Name = "V1" });
        await sut.LoadAsync();
        sut.SelectedProject = sut.Projects.Single();
        sut.ViTriPages.ShouldNotBeEmpty();

        sut.SelectedProject = null;

        sut.ViTriPages.ShouldBeEmpty();
        sut.SelectedViTriPage.ShouldBeNull();
    }

    [Fact]
    public async Task AddViTri_appends_a_new_tab_and_marks_the_workbook_dirty()
    {
        var (sut, _) = CreateSutWithProjectTree(1, "DA1");
        await sut.LoadAsync();
        sut.SelectedProject = sut.Projects.Single();
        sut.IsDirty.ShouldBeFalse();

        sut.AddViTriCommand.Execute(null);

        sut.ViTriPages.OfType<ViTriPageViewModel>().Count().ShouldBe(1);
        // new Vị trí sits before the trailing fixed tabs (Quyết toán năm + the three
        // form tabs from Task 13): the page right after it must be Quyết toán năm,
        // not one of the form tabs or the end of the strip.
        var addedIndex = sut.ViTriPages.IndexOf(sut.SelectedViTriPage!);
        sut.ViTriPages[addedIndex + 1].ShouldBeOfType<YearSettlementPageViewModel>();
        sut.SelectedViTriPage.ShouldBeOfType<ViTriPageViewModel>();
        sut.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public async Task AddViTri_without_a_selected_project_is_a_no_op()
    {
        var (sut, _) = CreateSutWithProjectTree(1, "DA1");
        await sut.LoadAsync();
        sut.SelectedProject = null; // LoadAsync auto-selects the first project; force the no-project state

        sut.AddViTriCommand.Execute(null);

        sut.ViTriPages.ShouldBeEmpty();
        sut.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteViTri_after_confirm_removes_the_tab_and_marks_the_workbook_dirty()
    {
        var (sut, _) = CreateSutWithProjectTree(1, "DA1",
            new ViTri { Name = "V1" }, new ViTri { Name = "V2" });
        await sut.LoadAsync();
        sut.SelectedProject = sut.Projects.Single();
        var toDelete = sut.ViTriPages.OfType<ViTriPageViewModel>().First();

        await sut.DeleteViTriCommand.ExecuteAsync(toDelete);

        var remaining = sut.ViTriPages.OfType<ViTriPageViewModel>().ToList();
        remaining.Count.ShouldBe(1);
        remaining[0].Title.ShouldBe("V2");
        sut.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteViTri_CanExecute_is_false_on_an_aggregate_tab_and_true_on_a_ViTri_ledger_tab()
    {
        var (sut, _) = CreateSutWithProjectTree(1, "DA1", new ViTri { Name = "V1" });
        await sut.LoadAsync();
        sut.SelectedProject = sut.Projects.Single();

        // LoadSelectedProjectTreeAsync lands on the Tổng hợp tab by default.
        sut.SelectedViTriPage.ShouldBeOfType<ProjectTongHopPageViewModel>();
        sut.DeleteViTriCommand.CanExecute(sut.SelectedViTriPage).ShouldBeFalse();

        sut.SelectedViTriPage = sut.ViTriPages.OfType<ViTriPageViewModel>().First();

        sut.DeleteViTriCommand.CanExecute(sut.SelectedViTriPage).ShouldBeTrue();
    }

    [Fact]
    public async Task IsDirty_is_true_when_a_ViTri_ledgers_row_edit_marks_it_dirty()
    {
        var (sut, _) = CreateSutWithProjectTree(1, "DA1", new ViTri { Name = "V1" });
        await sut.LoadAsync();
        sut.SelectedProject = sut.Projects.Single();
        sut.IsDirty.ShouldBeFalse();

        sut.ViTriPages.OfType<ViTriPageViewModel>().First().Ledger.AddGroupCommand.Execute(null);

        sut.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveAll_persists_the_selected_projects_ViTri_tree_via_ReplaceTreeAsync()
    {
        var (sut, service) = CreateSutWithProjectTree(7, "DA7", new ViTri { Name = "V1" });
        await sut.LoadAsync();
        sut.SelectedProject = sut.Projects.Single();
        sut.ViTriPages.OfType<ViTriPageViewModel>().First().Ledger.AddGroupCommand.Execute(null); // a real edit -> dirty
        sut.IsDirty.ShouldBeTrue();

        Project? saved = null;
        service.Setup(s => s.ReplaceTreeAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
               .Callback<Project, CancellationToken>((p, _) => saved = p)
               .Returns(Task.CompletedTask);

        await sut.SaveAllCommand.ExecuteAsync(null);

        saved.ShouldNotBeNull();
        saved!.Id.ShouldBe(7);
        saved.Name.ShouldBe("DA7");
        saved.ViTris.Count.ShouldBe(1);
        saved.ViTris[0].Name.ShouldBe("V1");
        saved.ViTris[0].CostGroups.Count.ShouldBe(1);
        sut.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveAll_is_a_no_op_when_nothing_is_dirty()
    {
        var (sut, service) = CreateSutWithProjectTree(1, "DA1", new ViTri { Name = "V1" });
        await sut.LoadAsync();
        sut.SelectedProject = sut.Projects.Single();

        await sut.SaveAllCommand.ExecuteAsync(null);

        service.Verify(s => s.ReplaceTreeAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SelectProjectById_moves_selection_to_the_matching_project()
    {
        var sut = CreateSutWithProjects((1, "DA1"), (2, "DA2"));
        await sut.LoadAsync();
        sut.SelectedProject!.Id.ShouldBe(1);   // LoadAsync auto-selects the first

        sut.SelectProjectById(2);

        sut.SelectedProject!.Id.ShouldBe(2);   // now discriminates a real move from a no-op
    }

    // ===== Add / rename / delete Dự án (workflow gap fix) =====

    [Fact]
    public async Task AddProject_adds_and_selects_a_new_project_with_one_empty_ViTri_and_marks_dirty()
    {
        var (sut, _) = CreateSutWithProjectTree(1, "DA1");
        await sut.LoadAsync();
        sut.IsDirty.ShouldBeFalse();

        sut.AddProjectCommand.Execute(null);

        sut.Projects.Count.ShouldBe(2);
        var newProject = sut.Projects.Last();
        newProject.Id.ShouldBe(0);
        newProject.Name.ShouldBe("Dự án mới");
        sut.SelectedProject.ShouldBe(newProject);
        sut.ViTriPages.OfType<ViTriPageViewModel>().Count().ShouldBe(1);
        sut.ViTriPages.OfType<ViTriPageViewModel>().First().Ledger.Name.ShouldBe("Vị trí 1");
        sut.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public async Task AddProject_appends_a_number_when_the_default_name_is_taken()
    {
        var (sut, _) = CreateSutWithProjectTree(1, "Dự án mới");
        await sut.LoadAsync();

        sut.AddProjectCommand.Execute(null);

        sut.Projects.Last().Name.ShouldBe("Dự án mới (2)");
    }

    [Fact]
    public async Task SelectedProjectName_setter_updates_the_selected_projects_Name()
    {
        var (sut, _) = CreateSutWithProjectTree(1, "DA1");
        await sut.LoadAsync();
        sut.SelectedProject = sut.Projects.Single();

        sut.SelectedProjectName = "DA1 renamed";

        sut.SelectedProject!.Name.ShouldBe("DA1 renamed");
        sut.SelectedProjectName.ShouldBe("DA1 renamed");
        sut.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteProject_after_confirm_removes_a_saved_project_and_reselects()
    {
        var (sut, service) = CreateSutWithConfirmingDialog((1, "DA1"), (2, "DA2"));
        await sut.LoadAsync();
        sut.SelectedProject = sut.Projects.Single(p => p.Id == 1);

        await sut.DeleteProjectCommand.ExecuteAsync(null);

        sut.Projects.Count.ShouldBe(1);
        sut.Projects[0].Id.ShouldBe(2);
        sut.SelectedProject!.Id.ShouldBe(2);
        service.Verify(s => s.DeleteAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteProject_on_an_unsaved_new_project_just_removes_it_without_calling_DeleteAsync()
    {
        var (sut, service) = CreateSutWithConfirmingDialog((1, "DA1"));
        await sut.LoadAsync();
        sut.AddProjectCommand.Execute(null);
        var newProject = sut.SelectedProject!;
        newProject.Id.ShouldBe(0);

        await sut.DeleteProjectCommand.ExecuteAsync(null);

        sut.Projects.ShouldNotContain(newProject);
        service.Verify(s => s.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveAll_persists_a_newly_added_and_renamed_project_via_ReplaceTreeAsync()
    {
        var (sut, service) = CreateSutWithProjectTree(1, "DA1");
        await sut.LoadAsync();

        sut.AddProjectCommand.Execute(null);
        sut.SelectedProjectName = "Dự án A";

        Project? saved = null;
        service.Setup(s => s.ReplaceTreeAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
               .Callback<Project, CancellationToken>((p, _) => saved = p)
               .Returns(Task.CompletedTask);

        await sut.SaveAllCommand.ExecuteAsync(null);

        saved.ShouldNotBeNull();
        saved!.Id.ShouldBe(0);
        saved.Name.ShouldBe("Dự án A");
        saved.ViTris.Count.ShouldBe(1);
        saved.ViTris[0].Name.ShouldBe("Vị trí 1");
        sut.IsDirty.ShouldBeFalse();
    }
}
