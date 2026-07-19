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
            Mock.Of<IDialogService>(),
            Mock.Of<INavigationService>(),
            new ServiceCollection().BuildServiceProvider());

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
            Mock.Of<IDialogService>(),
            Mock.Of<INavigationService>(),
            new ServiceCollection().BuildServiceProvider());

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
            Mock.Of<IDialogService>(),
            Mock.Of<INavigationService>(),
            new ServiceCollection().BuildServiceProvider());

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
}
