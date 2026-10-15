using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.Common;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Printing;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.ViewModels.Finance.Forms;
using ConstructionFinance.Domain.Finance;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// Sổ Excel-like của dự án: chọn Dự án ở trên, dải tab Vị trí / Tổng hợp / Quyết toán năm / ba biểu mẫu ở dưới.
public partial class FinanceWorkbookViewModel : ViewModelBase, IPrintablePage, IPrintableWorkbook
{
    private readonly IFinanceProjectService _service;
    private readonly IDialogService _dialog;

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

    private readonly IFormExcelExporter _formExporter;
    private readonly ICompanyInfoService _companyInfo;
    private FormOrgInfo _orgInfo = new(null, null);

    public FinanceWorkbookViewModel(
        IFinanceProjectService service,
        IDialogService dialog,
        IFormExcelExporter formExporter,
        ICompanyInfoService companyInfo)
    {
        _service = service;
        _dialog = dialog;
        _formExporter = formExporter;
        _companyInfo = companyInfo;
    }

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    /// Dự án picker.
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

    private FormTabPageViewModel? _thongTriPage;
    private FormTabPageViewModel? _ksChiPage;
    private FormTabPageViewModel? _gdnttPage;

    // Tên đơn vị sửa trên biểu mẫu nhưng chưa lưu vào Cài đặt.
    private bool _orgDirty;

    // Lần nạp cây của dự án đang chọn. OnSelectedProjectChanged không async nên chỉ giữ lại Task để
    // OpenFormTabAsync chờ được.
    private Task _treeLoad = Task.CompletedTask;

    // Ô nhập tay của biểu mẫu bẩn riêng: không ledger nào đánh dấu hộ được.
    private bool _formDeclarationDirty;

    /// True when any Vị trí tab, the tab structure, or a form field has unsaved
    /// edits — drives the toolbar + close guard.
    public bool IsDirty =>
        ViTriPages.OfType<ViTriPageViewModel>().Any(p => p.IsDirty) ||
        _viTriStructureDirty ||
        _formDeclarationDirty;

    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            Projects.Clear();
            ViTriPages.Clear();
            SelectedViTriPage = null;
            _tongHopPage = null;
            _yearPage = null;
            _thongTriPage = null;
            _ksChiPage = null;
            _gdnttPage = null;
            _formDeclarationDirty = false;
            _orgDirty = false;
            _selectedProjectTree = null;
            _viTriStructureDirty = false;

            var info = await _companyInfo.GetAsync();
            _orgInfo = new FormOrgInfo(info.DonViCapTren, info.DonViCoSo);

            var projects = await _service.ListAsync();
            foreach (var project in projects) Projects.Add(project);

            // Auto-select the first Dự án so the Vị trí ledger renders immediately
            // on launch instead of showing an empty picker. Safe with zero projects
            // (FirstOrDefault is null; OnSelectedProjectChanged no-ops on null).
            SelectedProject = Projects.FirstOrDefault();
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
    /// can't be async); the Task is kept in _treeLoad. AddProject sets
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

        _treeLoad = LoadSelectedProjectTreeAsync(value);
    }

    private async Task LoadSelectedProjectTreeAsync(Project? project)
    {
        ViTriPages.Clear();
        SelectedViTriPage = null;
        _tongHopPage = null;
        _yearPage = null;
        _thongTriPage = null;
        _ksChiPage = null;
        _gdnttPage = null;
        _formDeclarationDirty = false;
        _orgDirty = false;
        _selectedProjectTree = null;
        _viTriStructureDirty = false;
        OnPropertyChanged(nameof(IsDirty));

        if (project is null) return;

        IsBusy = true;
        try
        {
            // Cài đặt (Đơn vị cấp trên/cơ sở) can change while the workbook is open, and
            // RefreshFormTabs runs synchronously from several UI-driven paths (ledger edits,
            // tab selection), so it can't await a settings read itself. Re-reading here, on
            // every project switch, keeps the form tabs from showing a snapshot frozen at
            // workbook-open time without having to make the whole refresh chain async.
            var info = await _companyInfo.GetAsync();
            _orgInfo = new FormOrgInfo(info.DonViCapTren, info.DonViCoSo);

            var tree = await _service.GetTreeAsync(project.Id);
            if (tree is null) return;

            _selectedProjectTree = tree;

            _tongHopPage = new ProjectTongHopPageViewModel(new ProjectTongHopViewModel());
            _yearPage = new YearSettlementPageViewModel(new YearSettlementViewModel());

            ViTriPages.Add(_tongHopPage);
            foreach (var viTri in tree.ViTris.OrderBy(v => v.SortOrder))
                AddViTriPage(new VitriLedgerViewModel(viTri));
            ViTriPages.Add(_yearPage);

            CreateFormPages();

            foreach (var formPage in FormPages()) ViTriPages.Add(formPage);

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

        var ledgerPages = ViTriPages.OfType<ViTriPageViewModel>().ToList();

        // Gán SortOrder theo đúng vị trí tab TRƯỚC khi trải phẳng, để thứ tự đợt mà
        // FormRowFlattener sinh ra khớp một-một với danh sách dòng lưới bên dưới —
        // picker dựa vào sự khớp đó để giữ lựa chọn theo đối tượng dòng.
        var viTris = new List<ViTri>();
        for (var i = 0; i < ledgerPages.Count; i++)
        {
            var viTri = ledgerPages[i].Ledger.ToViTri();
            viTri.SortOrder = i;
            viTris.Add(viTri);
        }

        _tongHopPage?.Inner.Refresh(viTris);
        _yearPage?.Inner.Refresh(viTris);
        RefreshFormTabs(viTris, ledgerPages);
    }

    /// Ba tab biểu mẫu ăn cùng một cây với hai tab tổng hợp, kể cả phần chưa lưu.
    private void RefreshFormTabs(IReadOnlyList<ViTri> viTris, IReadOnlyList<ViTriPageViewModel> ledgerPages)
    {
        if (_selectedProjectTree is null) return;
        if (_thongTriPage is null || _ksChiPage is null || _gdnttPage is null) return;

        var rows = FormRowFlattener.Flatten(viTris);
        var sources = ledgerPages
            .SelectMany(p => p.Ledger.Rows.Where(r => r.Kind == RowKind.Entry))
            .ToList();

        // Bản khai sống ngay trên cây đã nạp, nên Save-All ghi kèm mà không phải
        // ghép lại thủ công.
        var projectName = _selectedProjectTree.Name;
        _thongTriPage.Inner.Refresh(rows, sources, _orgInfo, projectName);
        _ksChiPage.Inner.Refresh(rows, sources, _orgInfo, projectName);
        _gdnttPage.Inner.Refresh(rows, sources, _orgInfo, projectName);
    }

    /// Dựng ba tab biểu mẫu cho dự án đang chọn (cây đã nằm ở _selectedProjectTree): nạp chữ đã gõ từ cây và nghe
    /// thay đổi của từng tab.
    private void CreateFormPages()
    {
        _thongTriPage = NewFormPage(FormDefinitions.ThongTri);
        _ksChiPage = NewFormPage(FormDefinitions.KsChi);
        _gdnttPage = NewFormPage(FormDefinitions.Gdntt);
    }

    private FormTabPageViewModel NewFormPage(FormDefinition definition)
    {
        var page = new FormTabPageViewModel(new FormPageViewModel(definition, _formExporter, _dialog));
        page.Inner.InputsChanged += OnFormDeclarationChanged;
        page.Inner.OrgEdited += OnOrgEdited;
        if (_selectedProjectTree is not null) page.Inner.LoadInputs(_selectedProjectTree.FormInputs);
        return page;
    }

    private IEnumerable<FormTabPageViewModel> FormPages() =>
        new[] { _thongTriPage, _ksChiPage, _gdnttPage }.OfType<FormTabPageViewModel>();

    /// Tên đơn vị sửa trên MỘT biểu mẫu phải hiện ngay trên mọi biểu mẫu và ở Cài đặt (lưu cùng Ctrl+S).
    private void OnOrgEdited(object? sender, OrgEditedEventArgs e)
    {
        _orgInfo = _orgInfo.With(e.Key, e.Value);
        _orgDirty = true;
        _formDeclarationDirty = true;
        OnPropertyChanged(nameof(IsDirty));
        RefreshAggregateTabs();
    }

    /// Mở thẳng một tab biểu mẫu ("thongtri" | "kschi" | "gdntt") của dự án đang chọn. Chờ nạp cây xong rồi mới chọn.
    public async Task OpenFormTabAsync(string formId)
    {
        await _treeLoad;
        var page = formId switch
        {
            "thongtri" => _thongTriPage,
            "kschi" => _ksChiPage,
            "gdntt" => _gdnttPage,
            _ => null,
        };
        if (page is not null) SelectedViTriPage = page;
    }

    private void OnFormDeclarationChanged(object? sender, EventArgs e)
    {
        _formDeclarationDirty = true;
        OnPropertyChanged(nameof(IsDirty));
    }

    // Một handler duy nhất, thứ tự viết rõ ra: không dựa vào việc CommunityToolkit gọi overload nào trước (hai
    // overload cùng được gọi sau khi trường đã gán, nhưng thứ tự giữa chúng không phải điều thư viện hứa).
    partial void OnSelectedViTriPageChanged(WorkbookPageViewModel? oldValue, WorkbookPageViewModel? newValue)
    {
        // 1. Gỡ handler khỏi trang vừa rời: workbook chỉ nghe trang ĐANG MỞ, vì PrintSheetName của nó là của trang đó,
        //    và trang đã rời không được báo thay cho workbook trong lúc làm mới bên dưới (cũng không bị giữ lại).
        if (oldValue is not null) oldValue.PropertyChanged -= OnOpenPagePropertyChanged;

        // 2. Làm mới tab tổng hợp/biểu mẫu vừa được mở, để nó hiện số mới nhất.
        if (newValue is ProjectTongHopPageViewModel or YearSettlementPageViewModel
            or FormTabPageViewModel)
            RefreshAggregateTabs();

        // 3. Nghe trang mới (sau lần làm mới, để lần làm mới không tự gây ra thông báo chuyển tiếp thừa).
        if (newValue is not null) newValue.PropertyChanged += OnOpenPagePropertyChanged;

        // 4. Báo CUỐI CÙNG: đổi tab là đổi PrintSheetName (ủy quyền cho tab đang mở) và có thể đổi cả CanPrintAll, nên báo
        //    khi mọi thứ ở trên đã xong để người nghe (nút In ở khung chung) đọc giá trị đã cập nhật, từ đúng trang mới.
        RaisePrintStateChanged();
        DeleteViTriCommand.NotifyCanExecuteChanged();
    }

    // Trang đang mở tự đổi PrintSheetName mà không đổi tab (đổi tên Vị trí; hoặc GĐNTT vừa được tích gói thầu nên chuyển
    // từ không in được sang in được): chuyển tiếp, nếu không nút In hiện tên/trạng thái lệch cho tới khi người dùng đổi
    // tab. Chỉ chuyển tiếp đúng PrintSheetName — trang đang mở đổi tiêu đề hay đánh dấu bẩn thì nút In không cần biết.
    // Không so giá trị ở đây: báo thừa thì được (xem hợp đồng ở IPrintablePage), bỏ sót mới là lỗi.
    private void OnOpenPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IPrintablePage.PrintSheetName))
            RaisePrintStateChanged();
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
        var name = UniqueName.NextFree("Dự án mới", taken);

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

        CreateFormPages();
        foreach (var formPage in FormPages()) ViTriPages.Add(formPage);

        RefreshAggregateTabs();
        SelectedViTriPage = ViTriPages.OfType<ViTriPageViewModel>().First();  // land on the ledger to type

        _skipNextSelectionReload = true;
        SelectedProject = project;

        _viTriStructureDirty = true;
        OnPropertyChanged(nameof(IsDirty));
    }

    /// Delete the selected project (after confirming). A saved project (Id>0) is
    /// deleted from the DB immediately — nothing deferred to Save-All. An unsaved
    /// (Id 0) project is simply dropped from the list.
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
        }

        Projects.Remove(project);
        SelectedProject = Projects.FirstOrDefault();
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

    /// Select a project in the Dự án picker by id (used when jumping in from the
    /// all-projects Tổng hợp screen). No-op if the id isn't loaded.
    public void SelectProjectById(int projectId)
    {
        var match = Projects.FirstOrDefault(p => p.Id == projectId);
        if (match is not null) SelectedProject = match;
    }

    /// Excel-style Save-All: persist the selected project's Vị trí tree (ledgers, tab
    /// structure, form inputs, org names) if anything changed. A failed save leaves
    /// everything dirty so edits are never silently dropped.
    [RelayCommand]
    private async Task SaveAllAsync()
    {
        var saveViTriTree = SelectedProject is not null && _selectedProjectTree is not null &&
                             (ViTriPages.OfType<ViTriPageViewModel>().Any(p => p.IsDirty) ||
                              _viTriStructureDirty || _formDeclarationDirty);

        if (!saveViTriTree) return;

        IsBusy = true;
        var failed = new List<string>();
        try
        {
            var ok = await SaveViTriTreeAsync();
            if (!ok) failed.Add(_selectedProjectTree!.Name);

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

        foreach (var page in FormPages())
            foreach (var input in page.Inner.ToFormInputs())
                toSave.FormInputs.Add(input);

        try
        {
            if (_orgDirty)
            {
                // Tên đơn vị lưu ở Cài đặt: đọc-sửa-ghi bản ghi công ty, chỉ đổi hai trường này.
                var info = await _companyInfo.GetAsync();
                await _companyInfo.SaveAsync(info with { DonViCapTren = _orgInfo.DonViCapTren, DonViCoSo = _orgInfo.DonViCoSo });
                _orgDirty = false;
            }
            await _service.ReplaceTreeAsync(toSave);
            foreach (var page in ViTriPages.OfType<ViTriPageViewModel>())
                page.Ledger.IsDirty = false;
            if (_thongTriPage is not null) _thongTriPage.IsDirty = false;
            if (_ksChiPage is not null) _ksChiPage.IsDirty = false;
            if (_gdnttPage is not null) _gdnttPage.IsDirty = false;
            _viTriStructureDirty = false;
            _formDeclarationDirty = false;
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Saving the Vị trí tree for project {ProjectId} failed", loaded.Id);
            ErrorMessage = ex.Message;
            return false;
        }
    }

    // ===== In =====

    // Ủy quyền cho tab đang mở: khung chung chỉ biết một interface, không cần biết đến khái niệm tab.
    public string? PrintSheetName => SelectedViTriPage?.PrintSheetName;
    public PrintSheet? BuildPrintSheet() => SelectedViTriPage?.BuildPrintSheet();

    /// Tên dùng đặt tên tệp in: mã dự án nếu có, không thì tên dự án.
    public string PrintWorkbookName => SelectedProjectCode is { Length: > 0 } code
        ? code
        : SelectedProjectName;

    /// Tên dự án in ở chân trang: TÊN dự án, không phải mã (PrintWorkbookName ưu tiên mã vì nó đặt tên tệp). Người nhận
    /// bản in không biết mã nội bộ nên chân trang phải đọc được mà không cần biết mã.
    public string PrintFooterProjectName => SelectedProjectName;

    /// Có gì để In toàn bộ không: ít nhất một trang báo tên sheet in được. Nút In hỏi cái này mỗi lần cập nhật
    /// bật/mờ nên chỉ ĐỌC tên (rẻ): tuyệt đối không dựng sheet, không làm mới tab nào. Việc đó là của BuildAllSheets,
    /// chạy một lần mỗi lần in thật.
    public bool CanPrintAll => ViTriPages.Any(p => p.PrintSheetName is { Length: > 0 });

    /// Đường HÀNH ĐỘNG của "In toàn bộ", chạy một lần mỗi lần in thật (còn bật/mờ nút thì hỏi CanPrintAll, không hỏi
    /// hàm này). Thứ tự sheet theo Mẫu.xlsx: Tổng hợp → Vị trí 1..N → Quyết toán năm → 3 biểu mẫu — đúng thứ tự dải
    /// tab ViTriPages. Trang nào trả null (ví dụ GĐNTT chưa chọn gói thầu) thì không chặn cả lô nhưng cũng KHÔNG bị bỏ
    /// lặng lẽ: nó được liệt kê trong Skipped (tên tab + lý do) để người gọi nói với người dùng bộ tệp này thiếu gì.
    public PrintAllResult BuildAllSheets()
    {
        // Làm mới các tab Tổng hợp, Quyết toán năm và biểu mẫu TRƯỚC khi gom. Chúng chỉ tự làm mới khi được
        // chọn và ở lần sửa ĐẦU TIÊN của mỗi sổ Vị trí (lúc IsDirty lật false→true, xem AddViTriPage). Đứng ở
        // tab Vị trí, sửa từ lần thứ hai trở đi rồi bấm In toàn bộ: không làm mới ở đây thì các sheet đó mang
        // số cũ mà không có gì cảnh báo.
        RefreshAggregateTabs();

        var sheets = new List<PrintSheet>();
        var skipped = new List<SkippedPrintPage>();
        foreach (var page in ViTriPages)
        {
            if (page.BuildPrintSheet() is { } sheet)
                sheets.Add(sheet);
            else
                // Gọi trang bằng Title (chữ trên tab): PrintSheetName của nó là null nên không có tên sheet nào để gọi.
                skipped.Add(new SkippedPrintPage(page.Title, page.PrintUnavailableReason));
        }
        return new PrintAllResult(sheets, skipped);
    }

    /// Trạng thái in của workbook gồm PrintSheetName (của tab đang mở) và CanPrintAll: hai thứ nút In đọc. Một trong
    /// hai có thể đã đổi thì báo cả hai; báo thừa chỉ làm người nghe đọc lại một giá trị rẻ.
    private void RaisePrintStateChanged()
    {
        OnPropertyChanged(nameof(PrintSheetName));
        OnPropertyChanged(nameof(CanPrintAll));
    }
}
