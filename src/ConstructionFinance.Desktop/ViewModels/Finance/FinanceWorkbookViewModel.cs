using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.Import;
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

    // AddProject builds a new project's ledger tabs directly in-memory and then sets
    // SelectedProject to it — but the normal OnSelectedProjectChanged path would try to
    // GetTreeAsync a project that was never saved (Id 0), find nothing, and wipe out the
    // tabs we just built. This tells that one transition to skip the DB reload.
    private bool _skipNextSelectionReload;

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

    /// Read/write facade over SelectedProject.Name for the "Tên dự án" rename field.
    /// Project is a plain domain entity (not observable), so two things need explicit
    /// handling that a normal [ObservableProperty] would give for free: (1) the Dự án
    /// ComboBox's displayed text for the selected item won't refresh on its own —
    /// RefreshSelectedProjectDisplay() forces that; (2) SelectedProject (from Projects,
    /// loaded via the lightweight ListAsync) and _selectedProjectTree (loaded separately
    /// via GetTreeAsync, used by Save-All) are two DIFFERENT object instances for the
    /// same DB row once a project is selected, so both must be updated or Save-All would
    /// silently persist the pre-rename name.
    public string SelectedProjectName
    {
        get => SelectedProject?.Name ?? "";
        set
        {
            if (SelectedProject is null) return;
            var trimmed = value ?? "";
            if (SelectedProject.Name == trimmed) return;

            SelectedProject.Name = trimmed;
            if (_selectedProjectTree is not null) _selectedProjectTree.Name = trimmed;

            OnPropertyChanged(nameof(SelectedProjectName));
            RefreshSelectedProjectDisplay();
            _viTriStructureDirty = true;
            OnPropertyChanged(nameof(IsDirty));
        }
    }

    /// Read/write facade over SelectedProject.Code ("Mã dự án") — same dual-instance
    /// reasoning as SelectedProjectName, minus the ComboBox refresh (Code isn't shown
    /// there).
    public string? SelectedProjectCode
    {
        get => SelectedProject?.Code;
        set
        {
            if (SelectedProject is null) return;
            if (SelectedProject.Code == value) return;

            SelectedProject.Code = value;
            if (_selectedProjectTree is not null) _selectedProjectTree.Code = value;

            OnPropertyChanged(nameof(SelectedProjectCode));
            _viTriStructureDirty = true;
            OnPropertyChanged(nameof(IsDirty));
        }
    }

    // Heterogeneous: [Tổng hợp][one per Vị trí][Quyết toán năm].
    public ObservableCollection<WorkbookPageViewModel> ViTriPages { get; } = new();
    [ObservableProperty] private WorkbookPageViewModel? _selectedViTriPage;

    // The two read-only aggregate tabs for the selected project (rebuilt per project).
    private ProjectTongHopPageViewModel? _tongHopPage;
    private YearSettlementPageViewModel? _yearPage;

    /// True when any project page (old flow) OR any Vị trí tab (new flow) has
    /// unsaved edits — drives the toolbar + close guard.
    public bool IsDirty =>
        Pages.OfType<ProjectPageViewModel>().Any(p => p.IsDirty) ||
        ViTriPages.OfType<ViTriPageViewModel>().Any(p => p.IsDirty) ||
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
            _tongHopPage = null;
            _yearPage = null;
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
            // Auto-select the first Dự án so the Vị trí ledger renders immediately
            // on launch instead of showing an empty picker. Safe with zero projects
            // (FirstOrDefault is null; OnSelectedProjectChanged no-ops on null).
            SelectedProject = Projects.FirstOrDefault();
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
    /// SummaryPage's ProjectActivationRequested handler above. AddProject sets
    /// _skipNextSelectionReload first since it already built the tabs itself in-memory.
    partial void OnSelectedProjectChanged(Project? value)
    {
        OnPropertyChanged(nameof(SelectedProjectName));
        OnPropertyChanged(nameof(SelectedProjectCode));

        if (_skipNextSelectionReload)
        {
            _skipNextSelectionReload = false;
            return;
        }

        _ = LoadSelectedProjectTreeAsync(value);
    }

    private async Task LoadSelectedProjectTreeAsync(Project? project)
    {
        ViTriPages.Clear();
        SelectedViTriPage = null;
        _tongHopPage = null;
        _yearPage = null;
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

            _tongHopPage = new ProjectTongHopPageViewModel(new ProjectTongHopViewModel());
            _yearPage = new YearSettlementPageViewModel(new YearSettlementViewModel());

            ViTriPages.Add(_tongHopPage);
            foreach (var viTri in tree.ViTris.OrderBy(v => v.SortOrder))
                AddViTriPage(new VitriLedgerViewModel(viTri));
            ViTriPages.Add(_yearPage);

            RefreshAggregateTabs();
            SelectedViTriPage = ViTriPages.FirstOrDefault();   // the Tổng hợp tab
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
        // IsDirty only flips false→true once per ledger, so this catches that one
        // dirty transition (useful when the aggregate tab is already the one showing).
        // It is not the authoritative refresh path — that happens on selecting an
        // aggregate tab, via OnSelectedViTriPageChanged above.
        ledger.DirtyChanged += (_, _) => RefreshAggregateTabs();
        InsertViTriPage(page);
    }

    /// Insert a Vị trí tab before the trailing "Quyết toán năm" tab (or at the end
    /// if the aggregate tabs aren't present, e.g. the AddProject in-memory path).
    private void InsertViTriPage(ViTriPageViewModel page)
    {
        var yearIndex = _yearPage is null ? -1 : ViTriPages.IndexOf(_yearPage);
        if (yearIndex >= 0) ViTriPages.Insert(yearIndex, page);
        else ViTriPages.Add(page);
    }

    /// Recompute the read-only Tổng hợp + Quyết toán năm tabs from the current
    /// (possibly-unsaved) Vị trí ledgers.
    private void RefreshAggregateTabs()
    {
        if (_tongHopPage is null && _yearPage is null) return;

        var viTris = ViTriPages.OfType<ViTriPageViewModel>()
                               .Select(p => p.Ledger.ToViTri())
                               .ToList();
        _tongHopPage?.Inner.Refresh(viTris);
        _yearPage?.Inner.Refresh(viTris);
    }

    partial void OnSelectedViTriPageChanged(WorkbookPageViewModel? value)
    {
        if (value is ProjectTongHopPageViewModel or YearSettlementPageViewModel)
            RefreshAggregateTabs();

        DeleteViTriCommand.NotifyCanExecuteChanged();
    }

    /// New blank Vị trí tab under the selected project. No-op with no project selected.
    [RelayCommand]
    private void AddViTri()
    {
        if (SelectedProject is null) return;

        var count = ViTriPages.OfType<ViTriPageViewModel>().Count();
        var ledger = new VitriLedgerViewModel(new ViTri { Name = "Vị trí mới", SortOrder = count });
        AddViTriPage(ledger);
        SelectedViTriPage = ViTriPages.OfType<ViTriPageViewModel>().Last();

        _viTriStructureDirty = true;
        OnPropertyChanged(nameof(IsDirty));
        RefreshAggregateTabs();
    }

    /// Remove a Vị trí tab (after confirming) — it is only dropped from the DB once
    /// Save-All persists the rebuilt tree.
    [RelayCommand(CanExecute = nameof(CanDeleteViTri))]
    private async Task DeleteViTriAsync(WorkbookPageViewModel? page)
    {
        if (page is not ViTriPageViewModel viTriPage) return;

        var ok = await _dialog.ConfirmAsync("Xóa vị trí",
            $"Bạn có chắc muốn xóa vị trí \"{viTriPage.Title}\" và toàn bộ nhóm chi phí/gói thầu/đợt của nó?");
        if (!ok) return;

        ViTriPages.Remove(viTriPage);
        SelectedViTriPage = ViTriPages.OfType<ViTriPageViewModel>().FirstOrDefault()
                            ?? (WorkbookPageViewModel?)_tongHopPage;

        _viTriStructureDirty = true;
        OnPropertyChanged(nameof(IsDirty));
        RefreshAggregateTabs();
    }

    /// The "Xóa Vị trí" button only makes sense on a ledger tab — greyed out while
    /// the read-only Tổng hợp / Quyết toán năm aggregate tabs are selected.
    private bool CanDeleteViTri(WorkbookPageViewModel? page) => page is ViTriPageViewModel;

    /// New project for the Vị trí workbook path: Id 0, a free default name, and ONE
    /// empty Vị trí so the ledger is ready to enter immediately. Builds the ledger tab
    /// directly (see _skipNextSelectionReload) instead of relying on the normal
    /// DB-backed selection-change path, since this project has nothing in the DB yet.
    /// Unsaved until Save-All's ReplaceTreeAsync inserts it (Id 0 → insert) and it gets
    /// a real Id.
    [RelayCommand]
    private void AddProject()
    {
        var taken = new HashSet<string>(Projects.Select(p => p.Name.Trim()), StringComparer.OrdinalIgnoreCase);
        var name = ImportMerge.NextFreeName("Dự án mới", taken);

        var viTri = new ViTri { Name = "Vị trí 1", SortOrder = 0 };
        var project = new Project { Id = 0, Name = name, ViTris = { viTri } };

        Projects.Add(project);

        _selectedProjectTree = project;
        ViTriPages.Clear();
        _tongHopPage = new ProjectTongHopPageViewModel(new ProjectTongHopViewModel());
        _yearPage = new YearSettlementPageViewModel(new YearSettlementViewModel());
        ViTriPages.Add(_tongHopPage);
        AddViTriPage(new VitriLedgerViewModel(viTri));
        ViTriPages.Add(_yearPage);
        RefreshAggregateTabs();
        SelectedViTriPage = ViTriPages.OfType<ViTriPageViewModel>().First();  // land on the ledger to type

        _skipNextSelectionReload = true;
        SelectedProject = project;

        _viTriStructureDirty = true;
        OnPropertyChanged(nameof(IsDirty));
    }

    /// Delete the selected project (after confirming). A saved project (Id>0) is
    /// deleted from the DB immediately, same as the old flow's ClosePageAsync — nothing
    /// deferred to Save-All. An unsaved (Id 0) project is simply dropped from the list.
    /// Also drops the matching old-flow Pages tab, if any, so it doesn't linger pointing
    /// at a project that no longer exists.
    [RelayCommand]
    private async Task DeleteProjectAsync()
    {
        var project = SelectedProject;
        if (project is null) return;

        var ok = await _dialog.ConfirmAsync("Xóa dự án",
            $"Bạn có chắc muốn xóa dự án \"{project.Name}\" và toàn bộ Vị trí/nhóm chi phí/gói thầu/đợt của nó?");
        if (!ok) return;

        if (project.Id != 0)
        {
            try
            {
                await _service.DeleteAsync(project.Id);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                await _dialog.ShowMessageAsync("Lỗi", ex.Message);
                return;
            }

            var stalePage = Pages.OfType<ProjectPageViewModel>().FirstOrDefault(p => p.ProjectId == project.Id);
            if (stalePage is not null) Pages.Remove(stalePage);
        }

        Projects.Remove(project);
        SelectedProject = Projects.FirstOrDefault();

        await SummaryPage.Summary.LoadAsync();
    }

    /// Project is a plain domain entity (not observable), so mutating its Name directly
    /// (SelectedProjectName's setter) wouldn't make the Dự án ComboBox's displayed text
    /// refresh on its own. Replacing the SAME instance at its own index in the
    /// ObservableCollection raises a Replace notification, which is enough to make the
    /// ComboBox re-evaluate/redraw that item's DataTemplate.
    private void RefreshSelectedProjectDisplay()
    {
        var project = SelectedProject;
        if (project is null) return;
        var index = Projects.IndexOf(project);
        if (index >= 0) Projects[index] = project;
    }

    /// Jump to a project's tab — used by the TH page's double-click.
    public Task GoToProjectAsync(int projectId)
    {
        var page = Pages.OfType<ProjectPageViewModel>()
                        .FirstOrDefault(p => p.ProjectId == projectId);
        if (page is not null) SelectedPage = page;
        return Task.CompletedTask;
    }

    /// Select a project in the Dự án picker by id (used when jumping in from the
    /// all-projects Tổng hợp screen). No-op if the id isn't loaded.
    public void SelectProjectById(int projectId)
    {
        var match = Projects.FirstOrDefault(p => p.Id == projectId);
        if (match is not null) SelectedProject = match;
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
                             (ViTriPages.OfType<ViTriPageViewModel>().Any(p => p.IsDirty) || _viTriStructureDirty);

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

        var ledgerPages = ViTriPages.OfType<ViTriPageViewModel>().ToList();
        for (var i = 0; i < ledgerPages.Count; i++)
        {
            var viTri = ledgerPages[i].Ledger.ToViTri();
            viTri.SortOrder = i;
            toSave.ViTris.Add(viTri);
        }

        try
        {
            await _service.ReplaceTreeAsync(toSave);
            foreach (var page in ViTriPages.OfType<ViTriPageViewModel>())
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
