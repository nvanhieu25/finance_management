using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// Excel-like workbook: a pinned TH page plus one page per project.
public partial class FinanceWorkbookViewModel : ViewModelBase
{
    private readonly IFinanceProjectService _service;
    private readonly IDialogService _dialog;
    private readonly IServiceProvider _services;

    public FinanceWorkbookViewModel(
        IFinanceProjectService service,
        SummaryPageViewModel summaryPage,
        IDialogService dialog,
        IServiceProvider services)
    {
        _service = service;
        _dialog = dialog;
        _services = services;
        SummaryPage = summaryPage;
    }

    public ObservableCollection<WorkbookPageViewModel> Pages { get; } = new();

    public SummaryPageViewModel SummaryPage { get; }

    [ObservableProperty] private WorkbookPageViewModel? _selectedPage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            Pages.Clear();
            Pages.Add(SummaryPage);

            var projects = await _service.ListAsync();
            foreach (var project in projects)
            {
                try
                {
                    Pages.Add(await CreateProjectPageAsync(project.Id));
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Skipped project {ProjectId} while building the workbook", project.Id);
                }
            }

            SelectedPage = SummaryPage;
            await SummaryPage.Summary.LoadAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Loading the finance workbook failed");
            ErrorMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }

    /// Jump to a project's tab — used by the TH page's double-click.
    public Task GoToProjectAsync(int projectId)
    {
        var page = Pages.OfType<ProjectPageViewModel>()
                        .FirstOrDefault(p => p.ProjectId == projectId);
        if (page is not null) SelectedPage = page;
        return Task.CompletedTask;
    }

    private async Task<ProjectPageViewModel> CreateProjectPageAsync(int projectId)
    {
        var grid = _services.GetRequiredService<FinanceProjectGridViewModel>();
        await grid.LoadProjectAsync(projectId);

        // The grid swallows its own load failures (missing project / DB error),
        // leaving CurrentProjectId unset. Surface that as a throw so the caller's
        // per-project catch skips this ONE project instead of adding a phantom
        // "(dự án mới)" tab that was never really loaded.
        if (grid.CurrentProjectId != projectId)
            throw new InvalidOperationException(
                grid.ErrorMessage ?? $"Project {projectId} could not be loaded.");

        return new ProjectPageViewModel(grid);
    }
}
