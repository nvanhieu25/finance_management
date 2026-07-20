using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Domain.Finance;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class FinanceWorkbookViewModelTests
{
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
            new ServiceCollection().BuildServiceProvider());
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
            services.BuildServiceProvider());
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
            services.BuildServiceProvider());

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
            services.BuildServiceProvider());

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
}
