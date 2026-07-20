using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    /// True when any project page has unsaved edits (drives the toolbar + close guard).
    public bool IsDirty => Pages.OfType<ProjectPageViewModel>().Any(p => p.IsDirty);

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

    /// New blank project page. It is unsaved until Save-All writes it.
    [RelayCommand]
    private void AddPage()
    {
        var grid = _services.GetRequiredService<FinanceProjectGridViewModel>();
        grid.InitializeNew();
        var page = new ProjectPageViewModel(grid);
        page.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorkbookPageViewModel.IsDirty))
                OnPropertyChanged(nameof(IsDirty));
        };
        Pages.Add(page);
        SelectedPage = page;
    }

    /// Excel-style Save-All: write every dirty page, then refresh TH. A page that
    /// fails stays dirty so its edits are never silently dropped.
    [RelayCommand]
    private async Task SaveAllAsync()
    {
        var dirtyPages = Pages.OfType<ProjectPageViewModel>().Where(p => p.IsDirty).ToList();
        if (dirtyPages.Count == 0) return;

        IsBusy = true;
        var failed = new List<string>();
        try
        {
            foreach (var page in dirtyPages)
            {
                var ok = await page.Grid.SaveAsync();
                if (!ok) failed.Add(page.Title);
            }

            await SummaryPage.Summary.LoadAsync();
            OnPropertyChanged(nameof(IsDirty));
        }
        finally { IsBusy = false; }

        if (failed.Count > 0)
            await _dialog.ShowMessageAsync("Lưu chưa xong",
                "Chưa lưu được các dự án sau (kiểm tra lại tên dự án):\n- " + string.Join("\n- ", failed));
    }

    /// Close a project page. TH is pinned (never a ProjectPageViewModel).
    /// An unsaved (id 0) page just gets a light "discard" confirm — there's
    /// nothing in the DB to delete. A saved page gets the delete-warning
    /// confirm and is removed from the database.
    [RelayCommand]
    private async Task ClosePageAsync(WorkbookPageViewModel? page)
    {
        if (page is not ProjectPageViewModel project) return;

        if (project.ProjectId == 0)
        {
            var discard = await _dialog.ConfirmAsync("Đóng dự án",
                "Đóng dự án chưa lưu? Dữ liệu chưa lưu sẽ mất.");
            if (!discard) return;

            Pages.Remove(project);
            SelectedPage = SummaryPage;
            return;
        }

        var ok = await _dialog.ConfirmAsync("Xóa dự án",
            $"Bạn có chắc muốn xóa dự án \"{project.Title}\" và toàn bộ đợt của nó?");
        if (!ok) return;

        try
        {
            await _service.DeleteAsync(project.ProjectId);
            Pages.Remove(project);
            SelectedPage = SummaryPage;
            await SummaryPage.Summary.LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            await _dialog.ShowMessageAsync("Lỗi", ex.Message);
        }
    }

    /// Rename = focus the project-name box on that page; the grid owns the field.
    [RelayCommand]
    private void RenamePage(WorkbookPageViewModel? page)
    {
        if (page is not ProjectPageViewModel project) return;
        SelectedPage = project;
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

        var page = new ProjectPageViewModel(grid);
        page.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorkbookPageViewModel.IsDirty))
                OnPropertyChanged(nameof(IsDirty));
        };
        return page;
    }
}
