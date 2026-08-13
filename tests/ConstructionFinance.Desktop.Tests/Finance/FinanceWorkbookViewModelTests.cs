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

    private static FinanceWorkbookViewModel CreateSut(params FinanceProjectSummary[] summaries)
    {
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.GetAllSummariesAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(summaries);
        service.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(Array.Empty<Project>());

        var summaryVm = new FinanceSummaryViewModel(
            service.Object,
            Mock.Of<IFinanceImportService>(),
            Mock.Of<IFinanceExportService>(),
            Mock.Of<IDialogService>());

        return new FinanceWorkbookViewModel(
            service.Object,
            new SummaryPageViewModel(summaryVm),
            Mock.Of<IDialogService>(),
            new ServiceCollection().BuildServiceProvider(),
            Mock.Of<IFormExcelExporter>(),
            CreateCompanyInfo());
    }

    [Fact]
    public async Task LoadAsync_puts_the_pinned_TH_page_first()
    {
        var sut = CreateSut();

        await sut.LoadAsync();

        sut.Pages.ShouldNotBeEmpty();
        sut.Pages[0].ShouldBeOfType<SummaryPageViewModel>();
        sut.Pages[0].IsPinned.ShouldBeTrue();
        sut.Pages[0].Title.ShouldBe("TH");
    }

    [Fact]
    public async Task LoadAsync_selects_the_TH_page()
    {
        var sut = CreateSut();

        await sut.LoadAsync();

        sut.SelectedPage.ShouldBe(sut.Pages[0]);
    }

    [Fact]
    public async Task Header_shows_a_dot_only_when_dirty()
    {
        var sut = CreateSut();
        await sut.LoadAsync();
        var page = sut.Pages[0];
        page.Title = "DA1";

        page.Header.ShouldBe("DA1");

        page.IsDirty = true;
        page.Header.ShouldBe("• DA1");
    }

    private static FinanceWorkbookViewModel CreateSutWithProjects(params (int Id, string Name)[] projects)
    {
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.GetAllSummariesAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(Array.Empty<FinanceProjectSummary>());
        service.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(projects.Select(p => new Project { Id = p.Id, Name = p.Name }).ToList());
        foreach (var p in projects)
        {
            var captured = p;
            service.Setup(s => s.GetWithLinesAsync(captured.Id, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new Project { Id = captured.Id, Name = captured.Name });
        }

        var summaryVm = new FinanceSummaryViewModel(
            service.Object,
            Mock.Of<IFinanceImportService>(),
            Mock.Of<IFinanceExportService>(),
            Mock.Of<IDialogService>());

        var services = new ServiceCollection();
        services.AddTransient(_ => new FinanceProjectGridViewModel(service.Object, Mock.Of<IDialogService>()));

        return new FinanceWorkbookViewModel(
            service.Object,
            new SummaryPageViewModel(summaryVm),
            Mock.Of<IDialogService>(),
            services.BuildServiceProvider(),
            Mock.Of<IFormExcelExporter>(),
            CreateCompanyInfo());
    }

    [Fact]
    public async Task LoadAsync_creates_one_page_per_project_after_TH()
    {
        var sut = CreateSutWithProjects(
            (1, "DA1"),
            (2, "DA2"));

        await sut.LoadAsync();

        sut.Pages.Count.ShouldBe(3);
        sut.Pages[0].ShouldBeOfType<SummaryPageViewModel>();
        sut.Pages[1].Title.ShouldBe("DA1");
        sut.Pages[2].Title.ShouldBe("DA2");
    }

    [Fact]
    public async Task GoToProjectAsync_selects_that_projects_page()
    {
        var sut = CreateSutWithProjects((1, "DA1"), (2, "DA2"));
        await sut.LoadAsync();

        await sut.GoToProjectAsync(2);

        sut.SelectedPage.ShouldBe(sut.Pages[2]);
        sut.SelectedPage!.Title.ShouldBe("DA2");
    }

    [Fact]
    public async Task Activating_a_project_from_the_TH_page_switches_to_its_tab()
    {
        var sut = CreateSutWithProjects((1, "DA1"), (2, "DA2"));
        await sut.LoadAsync();

        sut.SummaryPage.Summary.ActivateProject(2);

        sut.SelectedPage.ShouldBeOfType<ProjectPageViewModel>();
        ((ProjectPageViewModel)sut.SelectedPage!).ProjectId.ShouldBe(2);
    }

    [Fact]
    public async Task GoToProjectAsync_ignores_an_unknown_project()
    {
        var sut = CreateSutWithProjects((1, "DA1"));
        await sut.LoadAsync();
        var before = sut.SelectedPage;

        await sut.GoToProjectAsync(999);

        sut.SelectedPage.ShouldBe(before);
    }

    [Fact]
    public async Task LoadAsync_skips_a_project_that_fails_to_load_and_keeps_the_rest()
    {
        // Project 1 fails to load its lines; project 2 loads fine. The workbook
        // must skip the bad one, keep the good one, and still land on the TH tab.
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.GetAllSummariesAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(Array.Empty<FinanceProjectSummary>());
        service.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<Project>
               {
                   new() { Id = 1, Name = "DA1" },
                   new() { Id = 2, Name = "DA2" }
               });
        service.Setup(s => s.GetWithLinesAsync(1, It.IsAny<CancellationToken>()))
               .ThrowsAsync(new InvalidOperationException("boom"));
        service.Setup(s => s.GetWithLinesAsync(2, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Project { Id = 2, Name = "DA2" });

        var summaryVm = new FinanceSummaryViewModel(
            service.Object,
            Mock.Of<IFinanceImportService>(),
            Mock.Of<IFinanceExportService>(),
            Mock.Of<IDialogService>());

        var services = new ServiceCollection();
        services.AddTransient(_ => new FinanceProjectGridViewModel(service.Object, Mock.Of<IDialogService>()));

        var sut = new FinanceWorkbookViewModel(
            service.Object,
            new SummaryPageViewModel(summaryVm),
            Mock.Of<IDialogService>(),
            services.BuildServiceProvider(),
            Mock.Of<IFormExcelExporter>(),
            CreateCompanyInfo());

        await sut.LoadAsync();

        // TH + only the good project.
        sut.Pages.Count.ShouldBe(2);
        sut.Pages[0].ShouldBeOfType<SummaryPageViewModel>();
        sut.Pages[1].Title.ShouldBe("DA2");
        sut.SelectedPage.ShouldBe(sut.Pages[0]);
    }

    [Fact]
    public async Task AddPage_appends_a_dirty_new_project_page_and_selects_it()
    {
        var sut = CreateSutWithProjects((1, "DA1"));
        await sut.LoadAsync();

        sut.AddPageCommand.Execute(null);

        sut.Pages.Count.ShouldBe(3);
        sut.Pages[2].ShouldBeOfType<ProjectPageViewModel>();
        sut.Pages[2].IsDirty.ShouldBeTrue();
        sut.SelectedPage.ShouldBe(sut.Pages[2]);
    }

    [Fact]
    public async Task ClosePage_never_closes_the_pinned_TH_page()
    {
        var sut = CreateSutWithProjects((1, "DA1"));
        await sut.LoadAsync();

        await sut.ClosePageCommand.ExecuteAsync(sut.Pages[0]);

        sut.Pages[0].ShouldBeOfType<SummaryPageViewModel>();
    }

    /// Builds a SUT whose service/dialog mocks the caller can assert against.
    /// The dialog confirms everything (returns true).
    private static (FinanceWorkbookViewModel Sut, Mock<IFinanceProjectService> Service)
        CreateSutWithConfirmingDialog(params (int Id, string Name)[] projects)
    {
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.GetAllSummariesAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(Array.Empty<FinanceProjectSummary>());
        service.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(projects.Select(p => new Project { Id = p.Id, Name = p.Name }).ToList());
        foreach (var p in projects)
        {
            var captured = p;
            service.Setup(s => s.GetWithLinesAsync(captured.Id, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new Project { Id = captured.Id, Name = captured.Name });
        }

        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>()))
              .ReturnsAsync(true);

        var summaryVm = new FinanceSummaryViewModel(
            service.Object,
            Mock.Of<IFinanceImportService>(),
            Mock.Of<IFinanceExportService>(),
            Mock.Of<IDialogService>());

        var services = new ServiceCollection();
        services.AddTransient(_ => new FinanceProjectGridViewModel(service.Object, Mock.Of<IDialogService>()));

        var sut = new FinanceWorkbookViewModel(
            service.Object,
            new SummaryPageViewModel(summaryVm),
            dialog.Object,
            services.BuildServiceProvider(),
            Mock.Of<IFormExcelExporter>(),
            CreateCompanyInfo());

        return (sut, service);
    }

    [Fact]
    public async Task ClosePage_on_an_unsaved_tab_removes_it_without_calling_delete()
    {
        var (sut, service) = CreateSutWithConfirmingDialog();
        await sut.LoadAsync();
        sut.AddPageCommand.Execute(null);   // an unsaved (id 0) page
        var newPage = sut.Pages[1];

        await sut.ClosePageCommand.ExecuteAsync(newPage);

        sut.Pages.ShouldNotContain(newPage);
        sut.SelectedPage.ShouldBe(sut.SummaryPage);
        service.Verify(s => s.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ClosePage_on_a_saved_tab_deletes_the_project_once()
    {
        var (sut, service) = CreateSutWithConfirmingDialog((7, "DA7"));
        await sut.LoadAsync();
        var savedPage = sut.Pages[1];

        await sut.ClosePageCommand.ExecuteAsync(savedPage);

        sut.Pages.ShouldNotContain(savedPage);
        sut.SelectedPage.ShouldBe(sut.SummaryPage);
        service.Verify(s => s.DeleteAsync(7, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IsDirty_is_true_when_any_project_page_is_dirty()
    {
        var sut = CreateSutWithProjects((1, "DA1"));
        await sut.LoadAsync();
        sut.IsDirty.ShouldBeFalse();

        ((ProjectPageViewModel)sut.Pages[1]).Grid.ContractorName = "X";

        sut.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveAll_saves_only_dirty_pages_and_clears_them()
    {
        var sut = CreateSutWithProjects((1, "DA1"), (2, "DA2"));
        await sut.LoadAsync();
        var dirty = (ProjectPageViewModel)sut.Pages[1];
        dirty.Grid.ContractorName = "X";

        await sut.SaveAllCommand.ExecuteAsync(null);

        dirty.IsDirty.ShouldBeFalse();
        sut.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveAll_keeps_a_failed_page_dirty_and_still_saves_the_others()
    {
        var sut = CreateSutWithProjects((1, "DA1"), (2, "DA2"));
        await sut.LoadAsync();
        var good = (ProjectPageViewModel)sut.Pages[1];
        var bad = (ProjectPageViewModel)sut.Pages[2];
        good.Grid.ContractorName = "X";
        bad.Grid.ProjectName = "   ";   // blank name → SaveAsync returns false

        await sut.SaveAllCommand.ExecuteAsync(null);

        good.IsDirty.ShouldBeFalse();
        bad.IsDirty.ShouldBeTrue();
    }

    // ===== 7B: Dự án picker + Vị trí tabs =====

    /// Builds a SUT whose ListAsync/GetTreeAsync are wired for exactly one project
    /// (with the given Vị trí tree), plus a confirming dialog (for DeleteViTri).
    private static (FinanceWorkbookViewModel Sut, Mock<IFinanceProjectService> Service)
        CreateSutWithProjectTree(int projectId, string projectName, params ViTri[] viTris)
    {
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.GetAllSummariesAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(Array.Empty<FinanceProjectSummary>());
        service.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<Project> { new() { Id = projectId, Name = projectName } });
        service.Setup(s => s.GetWithLinesAsync(projectId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Project { Id = projectId, Name = projectName });
        service.Setup(s => s.GetTreeAsync(projectId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Project { Id = projectId, Name = projectName, ViTris = viTris.ToList() });

        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

        var summaryVm = new FinanceSummaryViewModel(
            service.Object,
            Mock.Of<IFinanceImportService>(),
            Mock.Of<IFinanceExportService>(),
            Mock.Of<IDialogService>());

        var services = new ServiceCollection();
        services.AddTransient(_ => new FinanceProjectGridViewModel(service.Object, Mock.Of<IDialogService>()));

        var sut = new FinanceWorkbookViewModel(
            service.Object,
            new SummaryPageViewModel(summaryVm),
            dialog.Object,
            services.BuildServiceProvider(),
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
        service.Setup(s => s.GetAllSummariesAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(Array.Empty<FinanceProjectSummary>());
        service.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(Array.Empty<Project>());

        var summaryVm = new FinanceSummaryViewModel(
            service.Object,
            Mock.Of<IFinanceImportService>(),
            Mock.Of<IFinanceExportService>(),
            Mock.Of<IDialogService>());

        var sut = new FinanceWorkbookViewModel(
            service.Object,
            new SummaryPageViewModel(summaryVm),
            Mock.Of<IDialogService>(),
            new ServiceCollection().BuildServiceProvider(),
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
        sut.ViTriPages[^3].ShouldBeOfType<ThongTriFormPageViewModel>();
        sut.ViTriPages[^2].ShouldBeOfType<KsChiFormPageViewModel>();
        sut.ViTriPages[^1].ShouldBeOfType<GdnttFormPageViewModel>();
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
        // form tabs from Task 13) — it is the last ViTriPageViewModel in the strip,
        // not necessarily the last page overall.
        sut.ViTriPages.OfType<ViTriPageViewModel>().Last().ShouldBe(sut.SelectedViTriPage);
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
