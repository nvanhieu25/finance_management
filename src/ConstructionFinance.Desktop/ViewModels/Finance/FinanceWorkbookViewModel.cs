using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Domain.Finance;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// Excel-like workbook: a pinned TH page plus one page per project.
public partial class FinanceWorkbookViewModel : ViewModelBase
{
    private readonly IFinanceProjectService _service;
    private readonly IDialogService _dialog;
    private readonly IServiceProvider _services;

    // The full tree as loaded for the selected project (Code/Notes are preserved
    // on Save-All since the Vị trí picker never edits them). Null until a project
    // with a successfully-loaded tree is selected.
    private Project? _selectedProjectTree;

    // Set by AddViTri/DeleteViTri: those structural edits have no single ledger to
    // flag dirty, so IsDirty tracks them separately until the next successful save.
    private bool _viTriStructureDirty;

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

        SummaryPage.Summary.ProjectActivationRequested += async (_, projectId) =>
            await GoToProjectAsync(projectId);
    }

    public ObservableCollection<WorkbookPageViewModel> Pages { get; } = new();

    public SummaryPageViewModel SummaryPage { get; }

    [ObservableProperty] private WorkbookPageViewModel? _selectedPage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    /// Dự án picker (new Vị trí workbook path — coexists with the old flat-grid Pages).
    public ObservableCollection<Project> Projects { get; } = new();

    [ObservableProperty] private Project? _selectedProject;

    /// One tab per Vị trí of the currently-selected project.
    public ObservableCollection<ViTriPageViewModel> ViTriPages { get; } = new();

    [ObservableProperty] private ViTriPageViewModel? _selectedViTriPage;

    /// True when any project page (old flow) OR any Vị trí tab (new flow) has
    /// unsaved edits — drives the toolbar + close guard.
    public bool IsDirty =>
        Pages.OfType<ProjectPageViewModel>().Any(p => p.IsDirty) ||
        ViTriPages.Any(p => p.IsDirty) ||
        _viTriStructureDirty;

    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            Pages.Clear();
            Pages.Add(SummaryPage);
            Projects.Clear();
            ViTriPages.Clear();
            SelectedViTriPage = null;
            _selectedProjectTree = null;
            _viTriStructureDirty = false;

            var projects = await _service.ListAsync();
            foreach (var project in projects)
            {
                Projects.Add(project);
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
            // Deliberately no auto-selected Dự án — the picker starts empty; the
            // user chooses a project to see its Vị trí tabs.
            await SummaryPage.Summary.LoadAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Loading the finance workbook failed");
            ErrorMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }

    /// Loads the selected project's Vị trí tree and rebuilds one tab per Vị trí.
    /// Fire-and-forget from the property-changed hook (ObservableProperty setters
    /// can't be async) — mirrors the fire-and-forget pattern already used for
    /// SummaryPage's ProjectActivationRequested handler above.
    partial void OnSelectedProjectChanged(Project? value) => _ = LoadSelectedProjectTreeAsync(value);

    private async Task LoadSelectedProjectTreeAsync(Project? project)
    {
        ViTriPages.Clear();
        SelectedViTriPage = null;
        _selectedProjectTree = null;
        _viTriStructureDirty = false;
        OnPropertyChanged(nameof(IsDirty));

        if (project is null) return;

        IsBusy = true;
        try
        {
            var tree = await _service.GetTreeAsync(project.Id);
            if (tree is null) return;

            _selectedProjectTree = tree;
            foreach (var viTri in tree.ViTris.OrderBy(v => v.SortOrder))
                AddViTriPage(new VitriLedgerViewModel(viTri));

            SelectedViTriPage = ViTriPages.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Loading the Vị trí tree for project {ProjectId} failed", project.Id);
            ErrorMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }

    private void AddViTriPage(VitriLedgerViewModel ledger)
    {
        var page = new ViTriPageViewModel(ledger);
        page.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorkbookPageViewModel.IsDirty))
                OnPropertyChanged(nameof(IsDirty));
        };
        ViTriPages.Add(page);
    }

    /// New blank Vị trí tab under the selected project. No-op with no project selected.
    [RelayCommand]
    private void AddViTri()
    {
        if (SelectedProject is null) return;

        var ledger = new VitriLedgerViewModel(new ViTri { Name = "Vị trí mới", SortOrder = ViTriPages.Count });
        AddViTriPage(ledger);
        SelectedViTriPage = ViTriPages[^1];

        _viTriStructureDirty = true;
        OnPropertyChanged(nameof(IsDirty));
    }

    /// Remove a Vị trí tab (after confirming) — it is only dropped from the DB once
    /// Save-All persists the rebuilt tree.
    [RelayCommand]
    private async Task DeleteViTriAsync(ViTriPageViewModel? page)
    {
        if (page is null) return;

        var ok = await _dialog.ConfirmAsync("Xóa vị trí",
            $"Bạn có chắc muốn xóa vị trí \"{page.Title}\" và toàn bộ nhóm chi phí/gói thầu/đợt của nó?");
        if (!ok) return;

        ViTriPages.Remove(page);
        SelectedViTriPage = ViTriPages.FirstOrDefault();

        _viTriStructureDirty = true;
        OnPropertyChanged(nameof(IsDirty));
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

    /// Excel-style Save-All: write every dirty page, persist the selected project's
    /// Vị trí tree if it changed, then refresh TH. A page that fails stays dirty so
    /// its edits are never silently dropped.
    [RelayCommand]
    private async Task SaveAllAsync()
    {
        var dirtyPages = Pages.OfType<ProjectPageViewModel>().Where(p => p.IsDirty).ToList();
        var saveViTriTree = SelectedProject is not null && _selectedProjectTree is not null &&
                             (ViTriPages.Any(p => p.IsDirty) || _viTriStructureDirty);

        if (dirtyPages.Count == 0 && !saveViTriTree) return;

        IsBusy = true;
        var failed = new List<string>();
        try
        {
            foreach (var page in dirtyPages)
            {
                var ok = await page.Grid.SaveAsync();
                if (!ok) failed.Add(page.Title);
            }

            if (saveViTriTree)
            {
                var ok = await SaveViTriTreeAsync();
                if (!ok) failed.Add(_selectedProjectTree!.Name);
            }

            await SummaryPage.Summary.LoadAsync();
            OnPropertyChanged(nameof(IsDirty));
        }
        finally { IsBusy = false; }

        if (failed.Count > 0)
            await _dialog.ShowMessageAsync("Lưu chưa xong",
                "Chưa lưu được các dự án sau (kiểm tra lại tên dự án):\n- " + string.Join("\n- ", failed));
    }

    /// Rebuilds the selected project's Project (preserving Code/Name/Notes from the
    /// loaded tree — this picker never edits them) with ViTris from the current tabs,
    /// and persists it wholesale via ReplaceTreeAsync.
    private async Task<bool> SaveViTriTreeAsync()
    {
        var loaded = _selectedProjectTree!;
        var toSave = new Project
        {
            Id = loaded.Id,
            Code = loaded.Code,
            Name = loaded.Name,
            Notes = loaded.Notes
        };

        for (var i = 0; i < ViTriPages.Count; i++)
        {
            var viTri = ViTriPages[i].Ledger.ToViTri();
            viTri.SortOrder = i;
            toSave.ViTris.Add(viTri);
        }

        try
        {
            await _service.ReplaceTreeAsync(toSave);
            foreach (var page in ViTriPages)
                page.Ledger.IsDirty = false;
            _viTriStructureDirty = false;
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Saving the Vị trí tree for project {ProjectId} failed", loaded.Id);
            ErrorMessage = ex.Message;
            return false;
        }
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
