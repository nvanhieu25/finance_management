# Phase 6A — Workbook kiểu Excel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Gộp "Tổng hợp dự án" + "Nhập liệu (lưới)" thành **một workbook kiểu Excel** — dải tab dưới đáy, tab `TH` là tổng hợp, mỗi tab `DA` quản lý một dự án — kèm lưu-tất-cả kiểu Excel và import chống trùng. **Không xóa dữ liệu** (teardown là đợt 6B riêng).

**Architecture:** Thêm `FinanceWorkbookViewModel` + lớp trang (`WorkbookPageViewModel` → `SummaryPageViewModel` / `ProjectPageViewModel`) ở tầng Desktop. Trang chỉ **bọc** VM sẵn có; `TabControl.ContentTemplate` bind `Content` → **`ViewLocator` sẵn có tự dựng view cũ** (`FinanceSummaryView`, `FinanceProjectGridView`). `FinanceProjectGridViewModel` được refactor từ master–detail (dropdown) thành **một-instance-một-dự-án**. Không đụng `ProjectCalculator`, Domain, Data.

**Tech Stack:** .NET 8, Avalonia 11.2 + FluentAvalonia, CommunityToolkit.Mvvm, EF Core 8 + SQLite, xUnit + Shouldly + Moq.

**Spec:** `docs/superpowers/specs/2026-07-17-excel-workbook-unified-project-design.md`

## Global Constraints

- Giấy phép **miễn phí** (cơ quan nhà nước) — **không thêm package mới**; chỉ control chuẩn Avalonia/FluentAvalonia.
- Quy ước code: camelCase biến/tham số, `_camelCase` field private, PascalCase type/thành viên.
- Số tiền `decimal`. UI tiếng Việt. Chỉ VND.
- **BÀI HỌC PHASE 3 (bắt buộc):** ô sửa được **KHÔNG** được kết hợp **TwoWay binding + `StringFormat`** trên nguồn kiểu `decimal`/`DateOnly` — Avalonia ghi ngược chuỗi đã format → vòng lặp PropertyChanged vô hạn → StackOverflow/SIGABRT lúc render. Chỉ `TextBlock` chỉ-đọc mới được dùng `StringFormat`.
- `AvaloniaUseCompiledBindingsByDefault=true` → mọi `DataTemplate` phải khai báo `x:DataType`.
- Toàn bộ suite hiện **329 test** phải xanh sau mỗi task.
- Chạy app: `DISPLAY=:1 dotnet run --project src/ConstructionFinance.Desktop` (đừng pipe qua `tail` — pipe đóng sẽ giết app).
- DB dev tách biệt: đặt `CONSTRUCTIONFINANCE_DB` sang file tạm khi test runtime, để `app.db` thật an toàn.

---

### Task 1: Vỏ workbook + xác minh tab dưới đáy

**Mục đích:** Dựng khung workbook chỉ với tab `TH`, để **xác minh sớm** `TabStripPlacement="Bottom"` render đúng trên Avalonia 11.2 + FluentAvalonia (rủi ro chưa biết lớn nhất của đợt này).

**Files:**
- Create: `src/ConstructionFinance.Desktop/ViewModels/Finance/WorkbookPageViewModel.cs`
- Create: `src/ConstructionFinance.Desktop/ViewModels/Finance/SummaryPageViewModel.cs`
- Create: `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceWorkbookViewModel.cs`
- Create: `src/ConstructionFinance.Desktop/Views/Finance/FinanceWorkbookView.axaml`
- Create: `src/ConstructionFinance.Desktop/Views/Finance/FinanceWorkbookView.axaml.cs`
- Create: `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceWorkbookViewModelTests.cs`
- Modify: `src/ConstructionFinance.Desktop/Common/HostBuilder.cs:50-51`
- Modify: `src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs:36-94`
- Modify: `src/ConstructionFinance.Desktop/Views/MainWindow.axaml:27-33`

**Interfaces:**
- Consumes: `FinanceSummaryViewModel` (đã có), `ViewLocator` (đã có), `IServiceProvider`.
- Produces:
  - `abstract class WorkbookPageViewModel : ViewModelBase` — `string Title`, `bool IsDirty`, `string Header` (computed), `virtual bool IsPinned => false`, `abstract object Content { get; }`
  - `sealed class SummaryPageViewModel : WorkbookPageViewModel` — ctor `(FinanceSummaryViewModel summary)`, prop `FinanceSummaryViewModel Summary`, `IsPinned => true`, `Content => Summary`
  - `partial class FinanceWorkbookViewModel : ViewModelBase` — `ObservableCollection<WorkbookPageViewModel> Pages`, `WorkbookPageViewModel? SelectedPage`, `SummaryPageViewModel SummaryPage`, `Task LoadAsync()`
  - Nav tag `"FinanceWorkbook"`

- [ ] **Step 1: Viết test thất bại**

Tạo `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceWorkbookViewModelTests.cs`:

```csharp
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.ViewModels.Finance;
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
}
```

- [ ] **Step 2: Chạy test để xác nhận nó FAIL**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter FinanceWorkbookViewModelTests`
Expected: FAIL — build error, `WorkbookPageViewModel`/`SummaryPageViewModel`/`FinanceWorkbookViewModel` chưa tồn tại.

- [ ] **Step 3: Tạo lớp trang cơ sở**

Tạo `src/ConstructionFinance.Desktop/ViewModels/Finance/WorkbookPageViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// One page (tab) of the finance workbook. The tab strip binds Header; the tab
/// body binds Content, which the app-wide ViewLocator resolves to the existing
/// view of the wrapped view-model — so the workbook reuses the Phase-3/4 views
/// as-is instead of re-implementing them.
public abstract partial class WorkbookPageViewModel : ViewModelBase
{
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private bool _isDirty;

    /// Tab caption; unsaved pages get an Excel-style bullet.
    public string Header => IsDirty ? $"• {Title}" : Title;

    /// The TH page is pinned: always first, never closed or renamed.
    public virtual bool IsPinned => false;

    /// Inner view-model rendered in the tab body (resolved by ViewLocator).
    public abstract object Content { get; }

    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(Header));
    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(Header));
}
```

- [ ] **Step 4: Tạo trang TH**

Tạo `src/ConstructionFinance.Desktop/ViewModels/Finance/SummaryPageViewModel.cs`:

```csharp
namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// The pinned "TH" page — wraps the existing Tổng hợp view-model unchanged.
public sealed class SummaryPageViewModel : WorkbookPageViewModel
{
    public SummaryPageViewModel(FinanceSummaryViewModel summary)
    {
        Summary = summary;
        Title = "TH";
    }

    public FinanceSummaryViewModel Summary { get; }

    public override bool IsPinned => true;

    public override object Content => Summary;
}
```

- [ ] **Step 5: Tạo VM workbook (tối thiểu)**

Tạo `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceWorkbookViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

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
            SelectedPage = SummaryPage;
            await SummaryPage.Summary.LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }
}
```

- [ ] **Step 6: Chạy test để xác nhận PASS**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter FinanceWorkbookViewModelTests`
Expected: PASS — 3 test.

- [ ] **Step 7: Tạo View workbook**

Tạo `src/ConstructionFinance.Desktop/Views/Finance/FinanceWorkbookView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Finance"
             x:Class="ConstructionFinance.Desktop.Views.Finance.FinanceWorkbookView"
             x:DataType="vm:FinanceWorkbookViewModel">
    <DockPanel>
        <StackPanel DockPanel.Dock="Top" Spacing="2" Margin="0,0,0,12">
            <TextBlock Classes="pageTitle" Text="Tài chính dự án"/>
            <TextBlock Classes="pageSubtitle" Text="Mỗi trang là một dự án — trang TH là bảng tổng hợp"/>
        </StackPanel>

        <TabControl ItemsSource="{Binding Pages}"
                    SelectedItem="{Binding SelectedPage}"
                    TabStripPlacement="Bottom">
            <TabControl.ItemTemplate>
                <DataTemplate x:DataType="vm:WorkbookPageViewModel">
                    <TextBlock Text="{Binding Header}"/>
                </DataTemplate>
            </TabControl.ItemTemplate>
            <TabControl.ContentTemplate>
                <DataTemplate x:DataType="vm:WorkbookPageViewModel">
                    <ContentControl Content="{Binding Content}"/>
                </DataTemplate>
            </TabControl.ContentTemplate>
        </TabControl>
    </DockPanel>
</UserControl>
```

Tạo `src/ConstructionFinance.Desktop/Views/Finance/FinanceWorkbookView.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop.Views.Finance;

public partial class FinanceWorkbookView : UserControl
{
    public FinanceWorkbookView() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 8: Đăng ký DI**

Trong `src/ConstructionFinance.Desktop/Common/HostBuilder.cs`, ngay sau dòng `services.AddTransient<ViewModels.Finance.FinanceSummaryViewModel>();` thêm:

```csharp
                services.AddTransient<ViewModels.Finance.SummaryPageViewModel>();
                services.AddTransient<ViewModels.Finance.FinanceWorkbookViewModel>();
```

- [ ] **Step 9: Thêm nav item + route**

Trong `src/ConstructionFinance.Desktop/Views/MainWindow.axaml`, ngay TRƯỚC `<ui:NavigationViewItem Content="Bảng tổng hợp" Tag="TongHop">` thêm:

```xml
                <ui:NavigationViewItem Content="Tài chính dự án" Tag="FinanceWorkbook">
                    <ui:NavigationViewItem.IconSource><ui:SymbolIconSource Symbol="{x:Static ui:Symbol.Calculator}"/></ui:NavigationViewItem.IconSource>
                </ui:NavigationViewItem>
```

Trong `src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs`, thêm case vào `NavigateToAsync` ngay trước `case "Settings":`:

```csharp
            case "FinanceWorkbook":
                var fwvm = _services.GetRequiredService<ViewModels.Finance.FinanceWorkbookViewModel>();
                _nav.Reset();
                _nav.NavigateTo(fwvm);
                await fwvm.LoadAsync();
                break;
```

Và đổi `InitializeAsync` để mở workbook làm màn mặc định:

```csharp
    public async Task InitializeAsync()
    {
        try { await NavigateToAsync("FinanceWorkbook"); }
        catch (Exception ex) { Log.Warning(ex, "Initial navigation to FinanceWorkbook failed"); }
    }
```

- [ ] **Step 10: Build + chạy thật để XÁC MINH tab dưới đáy**

Run: `dotnet build 2>&1 | grep -E "error|Warning.*AVLN" ; DISPLAY=:1 dotnet run --project src/ConstructionFinance.Desktop`
Expected: app mở, nav có "Tài chính dự án", **dải tab nằm DƯỚI ĐÁY** với tab `TH` hiện bảng tổng hợp; log không có exception.

**NẾU tab không nằm dưới đáy** (theme FluentAvalonia ghi đè): fallback là thêm style vào `Styles/Theme.axaml` nhắm `TabControl` → không đổi sang control tự vẽ. Ghi lại phát hiện vào ledger.

- [ ] **Step 11: Chạy toàn bộ suite**

Run: `dotnet test 2>&1 | grep -E "Passed!|Failed!"`
Expected: tất cả xanh (329 + 3 mới = 332).

- [ ] **Step 12: Commit**

```bash
git add src/ConstructionFinance.Desktop/ViewModels/Finance/WorkbookPageViewModel.cs \
        src/ConstructionFinance.Desktop/ViewModels/Finance/SummaryPageViewModel.cs \
        src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceWorkbookViewModel.cs \
        src/ConstructionFinance.Desktop/Views/Finance/FinanceWorkbookView.axaml \
        src/ConstructionFinance.Desktop/Views/Finance/FinanceWorkbookView.axaml.cs \
        src/ConstructionFinance.Desktop/Common/HostBuilder.cs \
        src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs \
        src/ConstructionFinance.Desktop/Views/MainWindow.axaml \
        tests/ConstructionFinance.Desktop.Tests/Finance/FinanceWorkbookViewModelTests.cs
git commit -m "feat(finance): workbook shell with bottom tab strip + pinned TH page"
```

---

### Task 2: Refactor lưới → một-instance-một-dự-án + IsDirty

**Mục đích:** Bỏ dropdown chọn dự án khỏi `FinanceProjectGridViewModel` (tab chính là phép chọn) và thêm theo dõi "chưa lưu".

⚠️ **Task rủi ro hồi quy cao nhất của đợt** — `FinanceProjectGridViewModelTests` hiện có phủ VM này và sẽ phải sửa cùng lúc.

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceProjectGridViewModel.cs` (toàn bộ)
- Modify: `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceProjectGridViewModelTests.cs`
- Modify: `src/ConstructionFinance.Desktop/Views/Finance/FinanceProjectGridView.axaml` (gỡ dropdown + nút dự án)

**Interfaces:**
- Consumes: `IFinanceProjectService`, `IDialogService`, `LineItemRowViewModel` (đã có).
- Produces: `FinanceProjectGridViewModel` với
  - `bool IsDirty`, `event EventHandler? DirtyChanged`
  - `void InitializeNew()` — dự án trắng, 1 dòng, `IsDirty = true`
  - `Task LoadProjectAsync(int id)` — nạp dự án, `IsDirty = false`
  - `Task<bool> SaveAsync()` — trả `true` nếu lưu thành công; `IsDirty = false` sau khi lưu
  - **ĐÃ BỎ:** `Projects`, `SelectedProject`, `LoadAsync()`, `NewProjectCommand`, `DeleteProjectCommand`

- [ ] **Step 1: Viết test thất bại**

Thêm vào `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceProjectGridViewModelTests.cs`:

```csharp
    [Fact]
    public void InitializeNew_starts_dirty_with_one_blank_row()
    {
        var sut = new FinanceProjectGridViewModel(Mock.Of<IFinanceProjectService>(), Mock.Of<IDialogService>());

        sut.InitializeNew();

        sut.CurrentProjectId.ShouldBe(0);
        sut.Lines.Count.ShouldBe(1);
        sut.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public async Task LoadProjectAsync_leaves_the_grid_clean()
    {
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.GetWithLinesAsync(7, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Project { Id = 7, Name = "DA1" });
        var sut = new FinanceProjectGridViewModel(service.Object, Mock.Of<IDialogService>());

        await sut.LoadProjectAsync(7);

        sut.ProjectName.ShouldBe("DA1");
        sut.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public async Task Editing_a_header_field_marks_the_grid_dirty()
    {
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.GetWithLinesAsync(7, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Project { Id = 7, Name = "DA1" });
        var sut = new FinanceProjectGridViewModel(service.Object, Mock.Of<IDialogService>());
        await sut.LoadProjectAsync(7);

        sut.ContractorName = "Nhà thầu X";

        sut.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public async Task Editing_a_row_marks_the_grid_dirty()
    {
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.GetWithLinesAsync(7, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Project
               {
                   Id = 7,
                   Name = "DA1",
                   Lines = { new ProjectLineItem { SortOrder = 1, Advance = 10m } }
               });
        var sut = new FinanceProjectGridViewModel(service.Object, Mock.Of<IDialogService>());
        await sut.LoadProjectAsync(7);

        sut.Lines[0].PaymentKLHT = 50m;

        sut.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveAsync_clears_dirty_and_returns_true()
    {
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.CreateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(42);
        var sut = new FinanceProjectGridViewModel(service.Object, Mock.Of<IDialogService>());
        sut.InitializeNew();
        sut.ProjectName = "DA moi";

        var ok = await sut.SaveAsync();

        ok.ShouldBeTrue();
        sut.CurrentProjectId.ShouldBe(42);
        sut.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveAsync_refuses_a_blank_name_and_stays_dirty()
    {
        var sut = new FinanceProjectGridViewModel(Mock.Of<IFinanceProjectService>(), Mock.Of<IDialogService>());
        sut.InitializeNew();
        sut.ProjectName = "   ";

        var ok = await sut.SaveAsync();

        ok.ShouldBeFalse();
        sut.IsDirty.ShouldBeTrue();
    }
```

Xóa mọi test cũ tham chiếu `Projects`, `SelectedProject`, `LoadAsync()`, `NewProjectCommand`, `DeleteProjectCommand` trong file này.

- [ ] **Step 2: Chạy test để xác nhận FAIL**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter FinanceProjectGridViewModelTests`
Expected: FAIL — `InitializeNew`/`IsDirty` chưa tồn tại; `SaveAsync` chưa trả `bool`.

- [ ] **Step 3: Refactor VM**

Thay toàn bộ `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceProjectGridViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// Excel-like entry grid for exactly ONE project — the workbook tab that hosts
/// this instance IS the project selection, so there is no project picker here.
/// Totals are computed live by ProjectCalculator; nothing derived is persisted.
public partial class FinanceProjectGridViewModel : ViewModelBase
{
    private readonly IFinanceProjectService _service;
    private readonly IDialogService _dialog;
    private bool _loading;

    public FinanceProjectGridViewModel(IFinanceProjectService service, IDialogService dialog)
    {
        _service = service;
        _dialog = dialog;
        Lines.CollectionChanged += OnLinesCollectionChanged;
    }

    public ObservableCollection<LineItemRowViewModel> Lines { get; } = new();

    [ObservableProperty] private LineItemRowViewModel? _selectedRow;

    [ObservableProperty] private int _currentProjectId;
    [ObservableProperty] private string _projectName = "";
    [ObservableProperty] private string? _projectCode;
    [ObservableProperty] private string? _contractorName;
    [ObservableProperty] private decimal _contractValue;
    [ObservableProperty] private decimal _contractAnnexValue;
    [ObservableProperty] private string? _notes;

    [ObservableProperty] private decimal _sumAdvance;
    [ObservableProperty] private decimal _sumKLHT;
    [ObservableProperty] private decimal _sumRecovered;
    [ObservableProperty] private decimal _sumNSNN;
    [ObservableProperty] private decimal _sumNgoaiHoi;
    [ObservableProperty] private decimal _unrecoveredAdvance;
    [ObservableProperty] private decimal _totalDisbursed;

    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    /// Raised whenever IsDirty flips, so the hosting page can bubble it to the workbook.
    public event EventHandler? DirtyChanged;

    // Header edits mark the project unsaved. Derived Sum* props deliberately do
    // NOT mark dirty — they are recomputed output, not user input.
    partial void OnProjectNameChanged(string value) => MarkDirty();
    partial void OnProjectCodeChanged(string? value) => MarkDirty();
    partial void OnContractorNameChanged(string? value) => MarkDirty();
    partial void OnContractValueChanged(decimal value) => MarkDirty();
    partial void OnContractAnnexValueChanged(decimal value) => MarkDirty();
    partial void OnNotesChanged(string? value) => MarkDirty();

    partial void OnIsDirtyChanged(bool value) => DirtyChanged?.Invoke(this, EventArgs.Empty);

    private void MarkDirty()
    {
        if (_loading) return;
        IsDirty = true;
    }

    /// Blank project with one starter row. Unsaved by definition → starts dirty.
    public void InitializeNew()
    {
        _loading = true;
        try
        {
            CurrentProjectId = 0;
            ProjectName = "";
            ProjectCode = null;
            ContractorName = null;
            ContractValue = 0m;
            ContractAnnexValue = 0m;
            Notes = null;
            ClearLines();
            Lines.Add(new LineItemRowViewModel());
            RecomputeTotals();
        }
        finally { _loading = false; }
        IsDirty = true;
    }

    public async Task LoadProjectAsync(int id)
    {
        IsBusy = true;
        ErrorMessage = null;
        _loading = true;
        try
        {
            var project = await _service.GetWithLinesAsync(id);
            if (project is null) return;

            CurrentProjectId = project.Id;
            ProjectName = project.Name;
            ProjectCode = project.Code;
            ContractorName = project.ContractorName;
            ContractValue = project.ContractValue;
            ContractAnnexValue = project.ContractAnnexValue;
            Notes = project.Notes;

            ClearLines();
            foreach (var line in project.Lines.OrderBy(l => l.SortOrder))
                Lines.Add(new LineItemRowViewModel(line));
            RecomputeTotals();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally
        {
            _loading = false;
            IsBusy = false;
        }
        IsDirty = false;
    }

    [RelayCommand]
    private void AddRow()
    {
        Lines.Add(new LineItemRowViewModel());
        RecomputeTotals();
    }

    [RelayCommand]
    private void DeleteRow()
    {
        if (SelectedRow is null) return;
        Lines.Remove(SelectedRow);
        RecomputeTotals();
    }

    // The toolbar binds SaveCommand; the workbook calls SaveAsync directly for
    // its bool result. [RelayCommand] cannot wrap a Task<bool> method, so the
    // command is a thin void-ish wrapper over the real one.
    [RelayCommand]
    private async Task Save() => await SaveAsync();

    /// Persist this project. Returns true on success. The workbook's Save-All
    /// relies on the bool: a failed page must stay dirty so it is not lost.
    public async Task<bool> SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            await _dialog.ShowMessageAsync("Thiếu thông tin", "Tên dự án không được để trống.");
            return false;
        }

        var project = new Project
        {
            Id = CurrentProjectId,
            Name = ProjectName.Trim(),
            Code = ProjectCode,
            ContractorName = ContractorName,
            ContractValue = ContractValue,
            ContractAnnexValue = ContractAnnexValue,
            Notes = Notes
        };
        for (var i = 0; i < Lines.Count; i++)
        {
            Lines[i].SortOrder = i + 1;
            project.Lines.Add(Lines[i].ToEntity());
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (CurrentProjectId == 0)
                CurrentProjectId = await _service.CreateAsync(project);
            else
                await _service.UpdateAsync(project);

            IsDirty = false;
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return false;
        }
        finally { IsBusy = false; }
    }

    /// Paste a TSV block (rows of cells) starting at the given cell; new rows are
    /// appended as needed. Only editable columns (0–6) are written.
    public void ApplyPaste(int startRow, int startCol, string[][] block)
    {
        for (var r = 0; r < block.Length; r++)
        {
            var targetIndex = startRow + r;
            while (Lines.Count <= targetIndex)
                Lines.Add(new LineItemRowViewModel());

            var cells = block[r];
            for (var c = 0; c < cells.Length; c++)
                Lines[targetIndex].SetCell(startCol + c, cells[c]);
        }
        RecomputeTotals();
    }

    public void RecomputeTotals()
    {
        var inputs = Lines.Select(l => l.ToInput()).ToList();
        var agg = ProjectCalculator.Aggregate(inputs);
        SumAdvance = agg.SumAdvance;
        SumKLHT = agg.SumKLHT;
        SumRecovered = agg.SumRecovered;
        SumNSNN = agg.SumNSNN;
        SumNgoaiHoi = agg.SumNgoaiHoi;
        UnrecoveredAdvance = agg.UnrecoveredAdvance;
        TotalDisbursed = agg.TotalDisbursed;
    }

    private void OnLinesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (LineItemRowViewModel row in e.OldItems) row.InputChanged -= OnRowInputChanged;
        if (e.NewItems is not null)
            foreach (LineItemRowViewModel row in e.NewItems) row.InputChanged += OnRowInputChanged;
        MarkDirty();
    }

    private void OnRowInputChanged(object? sender, EventArgs e)
    {
        RecomputeTotals();
        MarkDirty();
    }

    private void ClearLines()
    {
        foreach (var row in Lines) row.InputChanged -= OnRowInputChanged;
        Lines.Clear();
    }
}
```

- [ ] **Step 4: Gỡ dropdown + nút dự án khỏi View**

Trong `src/ConstructionFinance.Desktop/Views/Finance/FinanceProjectGridView.axaml`, xóa:
- `ComboBox` bind `Projects`/`SelectedProject`
- `Button` bind `NewProjectCommand`
- `Button` bind `DeleteProjectCommand`

Giữ nguyên: nút `AddRowCommand`, `DeleteRowCommand`, `SaveCommand`, toàn bộ lưới, ô header dự án (`ProjectName`, `ContractorName`, `ContractValue`, `ContractAnnexValue` — **bind decimal thô, KHÔNG StringFormat**).

- [ ] **Step 5: Chạy test để xác nhận PASS**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter FinanceProjectGridViewModelTests`
Expected: PASS.

- [ ] **Step 6: Chạy toàn bộ suite**

Run: `dotnet test 2>&1 | grep -E "Passed!|Failed!"`
Expected: tất cả xanh. `FinanceSummaryViewModel.OpenProjectAsync` gọi `grid.LoadProjectAsync` — vẫn còn, chưa vỡ. Nếu có lỗi build ở chỗ gọi `LoadAsync()`/`NewProject()` của grid, sửa tại chỗ.

- [ ] **Step 7: Commit**

```bash
git add src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceProjectGridViewModel.cs \
        src/ConstructionFinance.Desktop/Views/Finance/FinanceProjectGridView.axaml \
        tests/ConstructionFinance.Desktop.Tests/Finance/FinanceProjectGridViewModelTests.cs
git commit -m "refactor(finance): entry grid serves one project + tracks unsaved state"
```

---

### Task 3: Trang dự án + nạp tab từ DB

**Files:**
- Create: `src/ConstructionFinance.Desktop/ViewModels/Finance/ProjectPageViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceWorkbookViewModel.cs`
- Modify: `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceWorkbookViewModelTests.cs`

**Interfaces:**
- Consumes: `FinanceProjectGridViewModel` (Task 2), `WorkbookPageViewModel` (Task 1), `IFinanceProjectService.ListWithLines` qua `GetAllSummariesAsync` + `GetWithLinesAsync`.
- Produces:
  - `sealed class ProjectPageViewModel : WorkbookPageViewModel` — ctor `(FinanceProjectGridViewModel grid)`, prop `FinanceProjectGridViewModel Grid`, `int ProjectId => Grid.CurrentProjectId`, `Content => Grid`
  - `FinanceWorkbookViewModel.LoadAsync()` dựng 1 `ProjectPageViewModel` cho mỗi dự án
  - `FinanceWorkbookViewModel.GoToProjectAsync(int projectId)`

- [ ] **Step 1: Viết test thất bại**

Thêm vào `FinanceWorkbookViewModelTests.cs`:

```csharp
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
```

Thêm helper vào cùng file:

```csharp
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
```

Thêm `using ConstructionFinance.Domain.Finance;` vào đầu file test.

- [ ] **Step 2: Chạy test để xác nhận FAIL**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter FinanceWorkbookViewModelTests`
Expected: FAIL — `ProjectPageViewModel` và `GoToProjectAsync` chưa tồn tại.

- [ ] **Step 3: Tạo trang dự án**

Tạo `src/ConstructionFinance.Desktop/ViewModels/Finance/ProjectPageViewModel.cs`:

```csharp
using System.ComponentModel;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// One "DA" page — wraps the single-project entry grid. The tab caption follows
/// the project name, and the page mirrors the grid's unsaved state so the
/// workbook can show the bullet and drive Save-All.
public sealed class ProjectPageViewModel : WorkbookPageViewModel
{
    public ProjectPageViewModel(FinanceProjectGridViewModel grid)
    {
        Grid = grid;
        Title = DisplayName(grid.ProjectName);
        IsDirty = grid.IsDirty;

        Grid.DirtyChanged += (_, _) => IsDirty = Grid.IsDirty;
        Grid.PropertyChanged += OnGridPropertyChanged;
    }

    public FinanceProjectGridViewModel Grid { get; }

    public int ProjectId => Grid.CurrentProjectId;

    public override object Content => Grid;

    private void OnGridPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FinanceProjectGridViewModel.ProjectName))
            Title = DisplayName(Grid.ProjectName);
    }

    private static string DisplayName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "(dự án mới)" : name.Trim();
}
```

- [ ] **Step 4: Nạp trang dự án trong workbook**

Trong `FinanceWorkbookViewModel.cs`, thay `LoadAsync` và thêm `GoToProjectAsync`:

```csharp
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
                Pages.Add(await CreateProjectPageAsync(project.Id));

            SelectedPage = SummaryPage;
            await SummaryPage.Summary.LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
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
        return new ProjectPageViewModel(grid);
    }
```

Thêm `using Microsoft.Extensions.DependencyInjection;` vào đầu `FinanceWorkbookViewModel.cs`.

- [ ] **Step 5: Chạy test để xác nhận PASS**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter FinanceWorkbookViewModelTests`
Expected: PASS — 6 test.

- [ ] **Step 6: Chạy toàn bộ suite + chạy app**

Run: `dotnet test 2>&1 | grep -E "Passed!|Failed!"` rồi `DISPLAY=:1 dotnet run --project src/ConstructionFinance.Desktop`
Expected: suite xanh; app hiện tab `TH` + một tab cho mỗi dự án trong DB; bấm tab dự án thấy lưới nhập của đúng dự án đó.

- [ ] **Step 7: Commit**

```bash
git add src/ConstructionFinance.Desktop/ViewModels/Finance/ProjectPageViewModel.cs \
        src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceWorkbookViewModel.cs \
        tests/ConstructionFinance.Desktop.Tests/Finance/FinanceWorkbookViewModelTests.cs
git commit -m "feat(finance): one workbook tab per project + jump-to-project"
```

---

### Task 4: Thêm / Xóa / Đổi tên trang

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceWorkbookViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/Views/Finance/FinanceWorkbookView.axaml`
- Modify: `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceWorkbookViewModelTests.cs`

**Interfaces:**
- Produces: `AddPageCommand`, `ClosePageCommand(WorkbookPageViewModel)`, `RenamePageCommand(WorkbookPageViewModel)` trên `FinanceWorkbookViewModel`.

- [ ] **Step 1: Viết test thất bại**

Thêm vào `FinanceWorkbookViewModelTests.cs`:

```csharp
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
```

- [ ] **Step 2: Chạy test để xác nhận FAIL**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter FinanceWorkbookViewModelTests`
Expected: FAIL — `AddPageCommand`/`ClosePageCommand` chưa tồn tại.

- [ ] **Step 3: Thêm command**

Thêm vào `FinanceWorkbookViewModel.cs`:

```csharp
    /// New blank project page. It is unsaved until Save-All writes it.
    [RelayCommand]
    private void AddPage()
    {
        var grid = _services.GetRequiredService<FinanceProjectGridViewModel>();
        grid.InitializeNew();
        var page = new ProjectPageViewModel(grid);
        Pages.Add(page);
        SelectedPage = page;
    }

    /// Delete the project behind a page, then close the tab. TH is pinned.
    [RelayCommand]
    private async Task ClosePageAsync(WorkbookPageViewModel? page)
    {
        if (page is not ProjectPageViewModel project || project.IsPinned) return;

        var ok = await _dialog.ConfirmAsync("Xóa dự án",
            $"Bạn có chắc muốn xóa dự án \"{project.Title}\" và toàn bộ đợt của nó?");
        if (!ok) return;

        try
        {
            if (project.ProjectId != 0)
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
```

Thêm `using CommunityToolkit.Mvvm.Input;` vào đầu file.

- [ ] **Step 4: Chạy test để xác nhận PASS**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter FinanceWorkbookViewModelTests`
Expected: PASS — 8 test.

- [ ] **Step 5: Thêm nút `+` và menu chuột phải vào View**

Trong `FinanceWorkbookView.axaml`, thay khối `TabControl.ItemTemplate` thành (thêm context menu) và thêm nút `+` phía trên dải tab:

```xml
            <TabControl.ItemTemplate>
                <DataTemplate x:DataType="vm:WorkbookPageViewModel">
                    <TextBlock Text="{Binding Header}">
                        <TextBlock.ContextMenu>
                            <ContextMenu>
                                <MenuItem Header="Đổi tên"
                                          Command="{Binding $parent[TabControl].((vm:FinanceWorkbookViewModel)DataContext).RenamePageCommand}"
                                          CommandParameter="{Binding}"/>
                                <MenuItem Header="Xóa dự án"
                                          Command="{Binding $parent[TabControl].((vm:FinanceWorkbookViewModel)DataContext).ClosePageCommand}"
                                          CommandParameter="{Binding}"/>
                            </ContextMenu>
                        </TextBlock.ContextMenu>
                    </TextBlock>
                </DataTemplate>
            </TabControl.ItemTemplate>
```

Và ngay dưới `<StackPanel DockPanel.Dock="Top" …>` thêm thanh công cụ:

```xml
        <Border DockPanel.Dock="Top" Classes="toolbarStrip" Margin="0,0,0,8">
            <StackPanel Orientation="Horizontal" Spacing="8">
                <Button Classes="toolbar" Command="{Binding AddPageCommand}" Content="+ Thêm dự án"/>
            </StackPanel>
        </Border>
```

- [ ] **Step 6: Build + chạy app**

Run: `dotnet build 2>&1 | grep -E "error" ; DISPLAY=:1 dotnet run --project src/ConstructionFinance.Desktop`
Expected: bấm "+ Thêm dự án" → tab mới `(dự án mới)` xuất hiện và được chọn; chuột phải tab dự án → menu "Đổi tên"/"Xóa dự án"; chuột phải tab TH → không xóa được.

- [ ] **Step 7: Chạy toàn bộ suite + commit**

```bash
dotnet test 2>&1 | grep -E "Passed!|Failed!"
git add src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceWorkbookViewModel.cs \
        src/ConstructionFinance.Desktop/Views/Finance/FinanceWorkbookView.axaml \
        tests/ConstructionFinance.Desktop.Tests/Finance/FinanceWorkbookViewModelTests.cs
git commit -m "feat(finance): add/close/rename workbook project pages"
```

---

### Task 5: Lưu tất cả kiểu Excel + Ctrl+S

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceWorkbookViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/Views/Finance/FinanceWorkbookView.axaml`
- Modify: `src/ConstructionFinance.Desktop/Views/Finance/FinanceWorkbookView.axaml.cs`
- Modify: `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceWorkbookViewModelTests.cs`

**Interfaces:**
- Produces: `bool IsDirty` (gộp), `SaveAllCommand` trên `FinanceWorkbookViewModel`.

- [ ] **Step 1: Viết test thất bại**

Thêm vào `FinanceWorkbookViewModelTests.cs`:

```csharp
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
```

- [ ] **Step 2: Chạy test để xác nhận FAIL**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter FinanceWorkbookViewModelTests`
Expected: FAIL — `IsDirty`/`SaveAllCommand` chưa tồn tại trên workbook.

- [ ] **Step 3: Cài IsDirty gộp + SaveAll**

Thêm vào `FinanceWorkbookViewModel.cs`:

```csharp
    /// True when any project page has unsaved edits (drives the toolbar + close guard).
    public bool IsDirty => Pages.OfType<ProjectPageViewModel>().Any(p => p.IsDirty);

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
```

Trong `AddPage` và `CreateProjectPageAsync`, sau khi tạo `page`, đăng ký lan truyền dirty:

```csharp
        page.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorkbookPageViewModel.IsDirty))
                OnPropertyChanged(nameof(IsDirty));
        };
```

(đặt ngay trước `Pages.Add(page)` trong `AddPage`, và trước `return new ProjectPageViewModel(grid);` — đổi `CreateProjectPageAsync` thành:

```csharp
    private async Task<ProjectPageViewModel> CreateProjectPageAsync(int projectId)
    {
        var grid = _services.GetRequiredService<FinanceProjectGridViewModel>();
        await grid.LoadProjectAsync(projectId);
        var page = new ProjectPageViewModel(grid);
        page.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorkbookPageViewModel.IsDirty))
                OnPropertyChanged(nameof(IsDirty));
        };
        return page;
    }
```
)

- [ ] **Step 4: Chạy test để xác nhận PASS**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter FinanceWorkbookViewModelTests`
Expected: PASS — 11 test.

- [ ] **Step 5: Thêm nút Lưu tất cả + phím tắt Ctrl+S**

Trong `FinanceWorkbookView.axaml`, thêm nút vào thanh công cụ (cạnh "+ Thêm dự án"):

```xml
                <Button Classes="toolbar accent" Command="{Binding SaveAllCommand}" Content="Lưu tất cả (Ctrl+S)"/>
```

Và thêm KeyBinding vào `UserControl` (ngay sau thẻ mở `<UserControl …>`):

```xml
    <UserControl.KeyBindings>
        <KeyBinding Gesture="Ctrl+S" Command="{Binding SaveAllCommand}"/>
    </UserControl.KeyBindings>
```

- [ ] **Step 6: Hỏi xác nhận khi đóng app còn tab bẩn**

Trong `src/ConstructionFinance.Desktop/Views/MainWindow.axaml.cs`, thêm vào constructor sau `AvaloniaXamlLoader.Load(this)`:

```csharp
        Closing += OnWindowClosing;
```

Và thêm phương thức vào cùng lớp:

```csharp
    private bool _forceClose;

    // Excel-style guard: never let unsaved project pages die silently.
    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_forceClose) return;
        if (DataContext is not ViewModels.MainWindowViewModel vm) return;
        if (vm.CurrentContent is not ViewModels.Finance.FinanceWorkbookViewModel wb || !wb.IsDirty) return;

        e.Cancel = true;
        var leave = await _dialog.ConfirmAsync("Còn thay đổi chưa lưu",
            "Có dự án chưa lưu. Thoát mà không lưu?");
        if (!leave) return;

        _forceClose = true;
        Close();
    }
```

Thêm field ở đầu lớp `MainWindow` (host tĩnh là `App.Host`, đã được gán TRƯỚC khi `MainWindow` được tạo trong `OnFrameworkInitializationCompleted`):

```csharp
    private readonly Services.IDialogService _dialog =
        App.Host!.Services.GetRequiredService<Services.IDialogService>();
```

Thêm using vào đầu `MainWindow.axaml.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
```

- [ ] **Step 7: Build + chạy app + suite**

Run: `dotnet build 2>&1 | grep -E "error" ; dotnet test 2>&1 | grep -E "Passed!|Failed!"` rồi `DISPLAY=:1 dotnet run --project src/ConstructionFinance.Desktop`
Expected: sửa một ô → tab hiện `• DA1`; Ctrl+S → dấu `•` biến mất, TH cập nhật số; sửa rồi đóng cửa sổ → hiện hỏi xác nhận.

- [ ] **Step 8: Commit**

```bash
git add src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceWorkbookViewModel.cs \
        src/ConstructionFinance.Desktop/Views/Finance/FinanceWorkbookView.axaml \
        src/ConstructionFinance.Desktop/Views/MainWindow.axaml.cs \
        tests/ConstructionFinance.Desktop.Tests/Finance/FinanceWorkbookViewModelTests.cs
git commit -m "feat(finance): Excel-style save-all, dirty markers and close guard"
```

---

### Task 6: Nối TH vào workbook

**Mục đích:** Bỏ nút "Mở dự án" (tab đã thay nó) và cho double-click dòng TH nhảy tới tab dự án — **không** để trang tham chiếu vòng ngược workbook.

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceSummaryViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/Views/Finance/FinanceSummaryView.axaml:25-30`
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceWorkbookViewModel.cs`
- Modify: `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceSummaryViewModelTests.cs`

**Interfaces:**
- Produces: `FinanceSummaryViewModel.ProjectActivationRequested` — `event EventHandler<int>?` (mang `ProjectId`). Workbook subscribe → gọi `GoToProjectAsync`.
- Bỏ: `FinanceSummaryViewModel.OpenProjectCommand`, các dependency `INavigationService`/`IServiceProvider` nếu hết dùng.

- [ ] **Step 1: Viết test thất bại**

Thêm vào `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceSummaryViewModelTests.cs`:

```csharp
    [Fact]
    public void ActivateProject_raises_the_activation_event_with_the_project_id()
    {
        var sut = CreateSut();
        var raised = 0;
        sut.ProjectActivationRequested += (_, id) => raised = id;

        sut.ActivateProject(42);

        raised.ShouldBe(42);
    }
```

(dùng helper `CreateSut()` sẵn có trong file; nếu chưa có thì tạo theo mẫu ở `FinanceWorkbookViewModelTests`.)

- [ ] **Step 2: Chạy test để xác nhận FAIL**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter FinanceSummaryViewModelTests`
Expected: FAIL — `ProjectActivationRequested`/`ActivateProject` chưa tồn tại.

- [ ] **Step 3: Thay OpenProject bằng event**

Trong `FinanceSummaryViewModel.cs`: **xóa** toàn bộ `OpenProjectAsync` command và thêm:

```csharp
    /// Raised when the user activates a row (double-click). The workbook listens
    /// and switches to that project's tab — the page never reaches up to its host.
    public event EventHandler<int>? ProjectActivationRequested;

    public void ActivateProject(int projectId) => ProjectActivationRequested?.Invoke(this, projectId);
```

`_nav` và `_services` **chỉ** được dùng trong `OpenProjectAsync` → sau khi xóa chúng thành code chết. **Xóa hẳn** cả field lẫn tham số ctor. Ctor mới còn 4 tham số:

```csharp
    public FinanceSummaryViewModel(
        IFinanceProjectService service,
        IFinanceImportService importer,
        IFinanceExportService exporter,
        IDialogService dialog)
    {
        _service = service;
        _importer = importer;
        _exporter = exporter;
        _dialog = dialog;
    }
```

Xóa luôn `using ConstructionFinance.Desktop.Services;`/`using Microsoft.Extensions.DependencyInjection;` nếu build báo thừa.

**Bắt buộc sửa kèm:** ctor đổi từ 6 → 4 tham số nên **mọi chỗ khởi tạo `FinanceSummaryViewModel` trong test phải bỏ 2 đối số cuối** (`Mock.Of<INavigationService>()`, `new ServiceCollection().BuildServiceProvider()`):
- `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceWorkbookViewModelTests.cs` — cả `CreateSut` (Task 1) và `CreateSutWithProjects` (Task 3)
- `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceSummaryViewModelTests.cs` — helper sẵn có

Build sẽ chỉ đúng chỗ nào còn sai.

- [ ] **Step 4: Sửa View TH**

Trong `FinanceSummaryView.axaml`, xóa khối `<Button Classes="toolbar" Command="{Binding OpenProjectCommand}">…</Button>` (dòng ~25–30).

Trong `src/ConstructionFinance.Desktop/Views/Finance/FinanceSummaryView.axaml.cs`, sửa `OnRowDoubleTapped` để gọi `ActivateProject`:

```csharp
    private void OnRowDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (DataContext is ViewModels.Finance.FinanceSummaryViewModel vm && vm.SelectedRow is { } row)
            vm.ActivateProject(row.ProjectId);
    }
```

- [ ] **Step 5: Workbook lắng nghe event**

Trong `FinanceWorkbookViewModel.cs`, thêm vào cuối constructor:

```csharp
        SummaryPage.Summary.ProjectActivationRequested += async (_, projectId) =>
            await GoToProjectAsync(projectId);
```

- [ ] **Step 6: Chạy test + build + app**

Run: `dotnet test 2>&1 | grep -E "Passed!|Failed!"` rồi `DISPLAY=:1 dotnet run --project src/ConstructionFinance.Desktop`
Expected: suite xanh; TH không còn nút "Mở dự án"; double-click một dòng TH → nhảy sang đúng tab dự án đó.

- [ ] **Step 7: Commit**

```bash
git add src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceSummaryViewModel.cs \
        src/ConstructionFinance.Desktop/Views/Finance/FinanceSummaryView.axaml \
        src/ConstructionFinance.Desktop/Views/Finance/FinanceSummaryView.axaml.cs \
        src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceWorkbookViewModel.cs \
        tests/ConstructionFinance.Desktop.Tests/Finance/FinanceSummaryViewModelTests.cs
git commit -m "feat(finance): TH row double-click jumps to the project tab"
```

---

### Task 7: Import chống trùng

**Files:**
- Create: `src/ConstructionFinance.Application/Import/ImportMerge.cs`
- Create: `tests/ConstructionFinance.Application.Tests/Finance/ImportMergeTests.cs`
- Modify: `src/ConstructionFinance.Application/Services/Interfaces/IFinanceProjectService.cs`
- Modify: `src/ConstructionFinance.Application/Services/Implementations/FinanceProjectService.cs`
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceSummaryViewModel.cs`

**Interfaces:**
- Produces:
  - `enum ImportResolution { Overwrite, Skip, AddNew }`
  - `record ImportCandidate(Project Parsed, int? ExistingId, ImportResolution Resolution)`
  - `static class ImportMerge` — `IReadOnlyList<ImportCandidate> Classify(IReadOnlyList<Project> parsed, IReadOnlyList<Project> existing)`; `string NextFreeName(string baseName, ISet<string> taken)`
  - `IFinanceProjectService.SaveMergedAsync(IReadOnlyList<ImportCandidate> candidates, CancellationToken ct = default) → Task<int>`

- [ ] **Step 1: Viết test thất bại**

Tạo `tests/ConstructionFinance.Application.Tests/Finance/ImportMergeTests.cs`:

```csharp
using ConstructionFinance.Application.Import;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Finance;

public class ImportMergeTests
{
    [Fact]
    public void Classify_marks_a_brand_new_project_as_AddNew_with_no_existing_id()
    {
        var parsed = new[] { new Project { Name = "DA9" } };
        var existing = new[] { new Project { Id = 1, Name = "DA1" } };

        var result = ImportMerge.Classify(parsed, existing);

        result.Count.ShouldBe(1);
        result[0].ExistingId.ShouldBeNull();
        result[0].Resolution.ShouldBe(ImportResolution.AddNew);
    }

    [Fact]
    public void Classify_matches_an_existing_name_ignoring_case_and_padding()
    {
        var parsed = new[] { new Project { Name = "  da1 " } };
        var existing = new[] { new Project { Id = 5, Name = "DA1" } };

        var result = ImportMerge.Classify(parsed, existing);

        result[0].ExistingId.ShouldBe(5);
        result[0].Resolution.ShouldBe(ImportResolution.Overwrite); // duplicates default to overwrite
    }

    [Fact]
    public void NextFreeName_appends_2_then_keeps_counting_until_free()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "DA1", "DA1 (2)", "DA1 (3)" };

        ImportMerge.NextFreeName("DA1", taken).ShouldBe("DA1 (4)");
    }

    [Fact]
    public void NextFreeName_returns_the_base_name_when_it_is_free()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "DA2" };

        ImportMerge.NextFreeName("DA1", taken).ShouldBe("DA1");
    }
}
```

- [ ] **Step 2: Chạy test để xác nhận FAIL**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter ImportMergeTests`
Expected: FAIL — namespace `ConstructionFinance.Application.Import.ImportMerge` chưa tồn tại.

- [ ] **Step 3: Viết logic phân loại (hàm thuần)**

Tạo `src/ConstructionFinance.Application/Import/ImportMerge.cs`:

```csharp
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Import;

/// What to do with one parsed project that collides with an existing one.
public enum ImportResolution
{
    Overwrite,
    Skip,
    AddNew
}

/// One parsed project plus the user's decision. ExistingId is null when the
/// project is new (nothing to collide with).
public record ImportCandidate(Project Parsed, int? ExistingId, ImportResolution Resolution);

/// Pure name-matching for legacy-Excel import. Kept free of EF so it can be
/// tested without a database — the old importer's silent duplicate-append is
/// exactly what this replaces.
public static class ImportMerge
{
    /// Pair each parsed project with an existing one by name (case/padding
    /// insensitive). Duplicates default to Overwrite — re-importing a workbook
    /// normally means "take the newer numbers"; the user can change each row.
    public static IReadOnlyList<ImportCandidate> Classify(
        IReadOnlyList<Project> parsed,
        IReadOnlyList<Project> existing)
    {
        var byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in existing)
            byName[Key(project.Name)] = project.Id;

        var candidates = new List<ImportCandidate>(parsed.Count);
        foreach (var project in parsed)
        {
            if (byName.TryGetValue(Key(project.Name), out var id))
                candidates.Add(new ImportCandidate(project, id, ImportResolution.Overwrite));
            else
                candidates.Add(new ImportCandidate(project, null, ImportResolution.AddNew));
        }
        return candidates;
    }

    /// "DA1" → "DA1 (2)" → "DA1 (3)" … until the name is free.
    public static string NextFreeName(string baseName, ISet<string> taken)
    {
        var trimmed = baseName.Trim();
        if (!taken.Contains(trimmed)) return trimmed;

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{trimmed} ({suffix})";
            if (!taken.Contains(candidate)) return candidate;
        }
    }

    private static string Key(string name) => name.Trim();
}
```

- [ ] **Step 4: Chạy test để xác nhận PASS**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter ImportMergeTests`
Expected: PASS — 4 test.

- [ ] **Step 5: Thêm SaveMergedAsync vào service**

Trong `IFinanceProjectService.cs` thêm:

```csharp
    Task<int> SaveMergedAsync(IReadOnlyList<ImportCandidate> candidates, CancellationToken ct = default);
```

(kèm `using ConstructionFinance.Application.Import;`)

Trong `FinanceProjectService.cs` thêm implementation (giữ nguyên phong cách file):

```csharp
    /// Apply the user's per-project import decisions. Skip writes nothing;
    /// Overwrite replaces the existing project's lines wholesale; AddNew inserts
    /// under a free name so an import can never silently duplicate a project.
    public async Task<int> SaveMergedAsync(IReadOnlyList<ImportCandidate> candidates, CancellationToken ct = default)
    {
        var existing = await _repo.ListAsync(ct);
        var taken = new HashSet<string>(existing.Select(p => p.Name.Trim()), StringComparer.OrdinalIgnoreCase);

        var written = 0;
        foreach (var candidate in candidates)
        {
            switch (candidate.Resolution)
            {
                case ImportResolution.Skip:
                    continue;

                case ImportResolution.Overwrite when candidate.ExistingId is { } id:
                    candidate.Parsed.Id = id;
                    await _repo.UpdateAsync(candidate.Parsed, ct);
                    written++;
                    break;

                default:
                    var name = ImportMerge.NextFreeName(candidate.Parsed.Name, taken);
                    candidate.Parsed.Name = name;
                    candidate.Parsed.Id = 0;
                    taken.Add(name);
                    await _repo.AddAsync(candidate.Parsed, ct);
                    written++;
                    break;
            }
        }
        return written;
    }
```

(Field repository trong `FinanceProjectService` tên là `_repo`; `AddAsync`/`UpdateAsync`/`DeleteAsync`/`ListAsync` đều nhận `CancellationToken ct` — khớp với code trên.)

- [ ] **Step 6: Chạy suite + commit**

**KHÔNG đụng UI trong task này.** `FinanceSummaryViewModel.ImportAsync` giữ nguyên (vẫn gọi `SaveImportedAsync`); toàn bộ việc nối UI + chuyển sang `SaveMergedAsync` nằm ở Task 7b, để task này không sinh ra code phải xóa ngay sau đó. Kết quả của Task 7 là **logic phân loại + service method đã có test**, dùng được ngay ở 7b.

```bash
dotnet test 2>&1 | grep -E "Passed!|Failed!"
git add src/ConstructionFinance.Application/Import/ImportMerge.cs \
        src/ConstructionFinance.Application/Services/Interfaces/IFinanceProjectService.cs \
        src/ConstructionFinance.Application/Services/Implementations/FinanceProjectService.cs \
        tests/ConstructionFinance.Application.Tests/Finance/ImportMergeTests.cs
git commit -m "feat(finance): name-matching import merge logic + SaveMergedAsync"
```

---

### Task 7b: Dialog chọn xử lý từng dự án trùng

**Mục đích:** Cài đúng spec §6 — người dùng chọn **từng dự án**: Ghi đè / Bỏ qua / Thêm bản mới. Thay luồng `ConfirmAsync` tạm ở Task 7.

**Files:**
- Create: `src/ConstructionFinance.Desktop/ViewModels/Finance/ImportResolutionDialogViewModel.cs`
- Create: `src/ConstructionFinance.Desktop/Views/Finance/ImportResolutionDialog.axaml`
- Create: `src/ConstructionFinance.Desktop/Views/Finance/ImportResolutionDialog.axaml.cs`
- Create: `tests/ConstructionFinance.Desktop.Tests/Finance/ImportResolutionDialogViewModelTests.cs`
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceSummaryViewModel.cs`

**Không** đăng ký DI: dialog VM nhận `IReadOnlyList<ImportCandidate>` ở ctor nên được `new` trực tiếp trong luồng import (giống các dialog VM khác của app).

**Interfaces:**
- Consumes: `ImportCandidate`, `ImportResolution`, `ImportMerge` (Task 7); `IDialogService.ShowDialogAsync<T>` (đã có — `ViewLocator` map `ViewModels.Finance.ImportResolutionDialogViewModel` → `Views.Finance.ImportResolutionDialog` bằng luật candidate-2 sẵn có).
- Produces:
  - `partial class ImportResolutionRowVm : ObservableObject` — `string Name`, `bool IsDuplicate`, `string StatusText`, `ImportResolution Resolution`
  - `partial class ImportResolutionDialogViewModel : ViewModelBase` — ctor `(IReadOnlyList<ImportCandidate> candidates)`, `ObservableCollection<ImportResolutionRowVm> Rows`, `IReadOnlyList<ImportCandidate> ToCandidates()`, `int DuplicateCount`, `int NewCount`

- [ ] **Step 1: Viết test thất bại**

Tạo `tests/ConstructionFinance.Desktop.Tests/Finance/ImportResolutionDialogViewModelTests.cs`:

```csharp
using ConstructionFinance.Application.Import;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class ImportResolutionDialogViewModelTests
{
    private static ImportResolutionDialogViewModel CreateSut() =>
        new(new[]
        {
            new ImportCandidate(new Project { Name = "DA1" }, 5, ImportResolution.Overwrite),
            new ImportCandidate(new Project { Name = "DA9" }, null, ImportResolution.AddNew)
        });

    [Fact]
    public void Duplicates_default_to_overwrite_and_new_projects_to_add()
    {
        var sut = CreateSut();

        sut.Rows.Count.ShouldBe(2);
        sut.Rows[0].IsDuplicate.ShouldBeTrue();
        sut.Rows[0].Resolution.ShouldBe(ImportResolution.Overwrite);
        sut.Rows[1].IsDuplicate.ShouldBeFalse();
        sut.Rows[1].Resolution.ShouldBe(ImportResolution.AddNew);
    }

    [Fact]
    public void Counts_split_duplicates_from_new_projects()
    {
        var sut = CreateSut();

        sut.DuplicateCount.ShouldBe(1);
        sut.NewCount.ShouldBe(1);
    }

    [Fact]
    public void ToCandidates_carries_the_users_per_row_choice_back()
    {
        var sut = CreateSut();
        sut.Rows[0].Resolution = ImportResolution.Skip;

        var result = sut.ToCandidates();

        result[0].Resolution.ShouldBe(ImportResolution.Skip);
        result[0].ExistingId.ShouldBe(5);
        result[0].Parsed.Name.ShouldBe("DA1");
        result[1].Resolution.ShouldBe(ImportResolution.AddNew);
    }

    [Fact]
    public void A_new_project_row_cannot_be_switched_to_overwrite()
    {
        var sut = CreateSut();

        sut.Rows[1].Resolution = ImportResolution.Overwrite;

        // Nothing to overwrite → ToCandidates must keep ExistingId null so the
        // service falls through to the AddNew branch instead of updating id 0.
        sut.ToCandidates()[1].ExistingId.ShouldBeNull();
    }
}
```

- [ ] **Step 2: Chạy test để xác nhận FAIL**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter ImportResolutionDialogViewModelTests`
Expected: FAIL — `ImportResolutionDialogViewModel` chưa tồn tại.

- [ ] **Step 3: Viết VM dialog**

Tạo `src/ConstructionFinance.Desktop/ViewModels/Finance/ImportResolutionDialogViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.Import;
using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// One line of the import review list.
public partial class ImportResolutionRowVm : ObservableObject
{
    public ImportResolutionRowVm(ImportCandidate candidate)
    {
        Candidate = candidate;
        Name = candidate.Parsed.Name;
        IsDuplicate = candidate.ExistingId is not null;
        _resolution = candidate.Resolution;
    }

    public ImportCandidate Candidate { get; }
    public string Name { get; }
    public bool IsDuplicate { get; }

    public string StatusText => IsDuplicate ? "Đã có trong app" : "Dự án mới";

    [ObservableProperty] private ImportResolution _resolution;
}

/// Lets the user decide what happens to each imported project before anything is
/// written — the old importer appended blindly, which is how the legacy table
/// ended up holding the same five projects three times.
public partial class ImportResolutionDialogViewModel : ViewModelBase
{
    public ImportResolutionDialogViewModel(IReadOnlyList<ImportCandidate> candidates)
    {
        foreach (var candidate in candidates)
            Rows.Add(new ImportResolutionRowVm(candidate));
    }

    public ObservableCollection<ImportResolutionRowVm> Rows { get; } = new();

    public int DuplicateCount => Rows.Count(r => r.IsDuplicate);
    public int NewCount => Rows.Count(r => !r.IsDuplicate);

    public string Headline =>
        $"Tìm thấy {Rows.Count} dự án: {NewCount} mới, {DuplicateCount} trùng tên.";

    /// Fold the per-row choices back into candidates for the service. A row with
    /// nothing to overwrite keeps ExistingId null so the service adds it instead.
    public IReadOnlyList<ImportCandidate> ToCandidates() =>
        Rows.Select(r => r.Candidate with { Resolution = r.Resolution }).ToList();
}
```

- [ ] **Step 4: Chạy test để xác nhận PASS**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter ImportResolutionDialogViewModelTests`
Expected: PASS — 4 test.

- [ ] **Step 5: Viết View dialog**

Avalonia **không** có `ObjectDataProvider` và không liệt kê enum trực tiếp trong XAML → danh sách lựa chọn phải do VM cung cấp. Thêm vào `ImportResolutionRowVm` (file ở Step 3):

```csharp
    /// Only duplicates get a choice; a new project has nothing to overwrite.
    public IReadOnlyList<ImportResolution> Options { get; } =
        new[] { ImportResolution.Overwrite, ImportResolution.Skip, ImportResolution.AddNew };
```

Tạo `src/ConstructionFinance.Desktop/Views/Finance/ImportResolutionDialog.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Finance"
             x:Class="ConstructionFinance.Desktop.Views.Finance.ImportResolutionDialog"
             x:DataType="vm:ImportResolutionDialogViewModel"
             Width="640">
    <DockPanel>
        <TextBlock DockPanel.Dock="Top" Text="{Binding Headline}"
                   FontWeight="SemiBold" Margin="0,0,0,4"/>
        <TextBlock DockPanel.Dock="Top" Classes="muted" TextWrapping="Wrap" Margin="0,0,0,12"
                   Text="Chọn cách xử lý cho từng dự án trùng tên. Bỏ qua = giữ nguyên dữ liệu đang có."/>

        <DataGrid ItemsSource="{Binding Rows}" IsReadOnly="False"
                  HeadersVisibility="Column" MaxHeight="360" CanUserSortColumns="False">
            <DataGrid.Columns>
                <DataGridTextColumn Header="Dự án" Binding="{Binding Name}" Width="240" IsReadOnly="True"/>
                <DataGridTextColumn Header="Trạng thái" Binding="{Binding StatusText}" Width="140" IsReadOnly="True"/>
                <DataGridTemplateColumn Header="Xử lý" Width="*">
                    <DataGridTemplateColumn.CellTemplate>
                        <DataTemplate x:DataType="vm:ImportResolutionRowVm">
                            <ComboBox ItemsSource="{Binding Options}"
                                      SelectedItem="{Binding Resolution}"
                                      IsEnabled="{Binding IsDuplicate}"
                                      HorizontalAlignment="Stretch" Margin="4,2"/>
                        </DataTemplate>
                    </DataGridTemplateColumn.CellTemplate>
                </DataGridTemplateColumn>
            </DataGrid.Columns>
        </DataGrid>
    </DockPanel>
</UserControl>
```

> ComboBox hiển thị tên enum tiếng Anh (`Overwrite`/`Skip`/`AddNew`). Nếu lúc chạy thấy khó đọc, thêm `IValueConverter` enum→tiếng Việt vào `BalancedConverters.cs` (file converter đã có) — **chỉ làm nếu người dùng phàn nàn** (YAGNI).

Tạo `src/ConstructionFinance.Desktop/Views/Finance/ImportResolutionDialog.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop.Views.Finance;

public partial class ImportResolutionDialog : UserControl
{
    public ImportResolutionDialog() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 6: Nối dialog vào luồng Import**

Trong `FinanceSummaryViewModel.ImportAsync`, thay đoạn từ `var msg = …` tới `var saved = await _service.SaveImportedAsync(result.Projects);` bằng:

```csharp
            var existing = await _service.ListAsync();
            var candidates = ImportMerge.Classify(result.Projects, existing);

            if (result.Warnings.Count > 0)
                await _dialog.ShowMessageAsync("Cảnh báo khi đọc file",
                    $"{result.Warnings.Count} cảnh báo:\n- " + string.Join("\n- ", result.Warnings.Take(10)));

            var review = new ImportResolutionDialogViewModel(candidates);
            if (await _dialog.ShowDialogAsync(review) != true) return;

            var saved = await _service.SaveMergedAsync(review.ToCandidates());
```

Giữ nguyên 2 dòng sau đó (`await LoadAsync();` và `await _dialog.ShowMessageAsync("Nhập Excel", $"Đã nhập {saved} dự án.");`).

Thêm `using ConstructionFinance.Application.Import;` vào đầu file.

- [ ] **Step 7: Build + chạy suite + chạy app**

Run: `dotnet build 2>&1 | grep -E "error" ; dotnet test 2>&1 | grep -E "Passed!|Failed!"`
Expected: 0 error; toàn bộ test xanh.

Chạy app, import `reference/Hieu_sample.xlsx` **hai lần**:
- Lần 1: dialog hiện 5 dòng "Dự án mới" (ComboBox mờ) → OK → 5 tab.
- Lần 2: dialog hiện 5 dòng "Đã có trong app", mặc định `Overwrite` → đổi một dòng thành `Skip` → OK → **vẫn 5 tab** (không nhân bản), dòng Skip giữ nguyên số cũ.

- [ ] **Step 8: Commit**

```bash
git add src/ConstructionFinance.Desktop/ViewModels/Finance/ImportResolutionDialogViewModel.cs \
        src/ConstructionFinance.Desktop/Views/Finance/ImportResolutionDialog.axaml \
        src/ConstructionFinance.Desktop/Views/Finance/ImportResolutionDialog.axaml.cs \
        src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceSummaryViewModel.cs \
        tests/ConstructionFinance.Desktop.Tests/Finance/ImportResolutionDialogViewModelTests.cs
git commit -m "feat(finance): per-project import review dialog (overwrite/skip/add-new)"
```

---

### Task 8: Dọn nav còn 4 mục + chạy thật + chốt đợt

**Mục đích:** Gỡ các mục cũ khỏi nav (**chưa xóa code** — đó là đợt 6B) và xác minh toàn bộ đợt 6A trên app thật.

**Files:**
- Modify: `src/ConstructionFinance.Desktop/Views/MainWindow.axaml:27-55`
- Modify: `.superpowers/sdd/progress.md`

- [ ] **Step 1: Gỡ mục cũ khỏi nav**

Trong `src/ConstructionFinance.Desktop/Views/MainWindow.axaml`, xóa các `NavigationViewItem` sau:
- `Content="Bảng tổng hợp" Tag="TongHop"`
- `Content="Tổng hợp dự án" Tag="FinanceSummary"`
- `Content="Dự án" Tag="Projects"`
- `Content="Nhập liệu (lưới)" Tag="FinanceGrid"`
- `Content="Nhà cung cấp" Tag="Vendors"`

Và xóa `<ui:NavigationViewItemHeader Content="Dữ liệu nguồn"/>` nếu nhóm chỉ còn "Quyết định" — chuyển "Quyết định" lên nhóm "Tổng hợp" hoặc bỏ header. Nav còn đúng 4 mục: **Tài chính dự án · Thông tri · Quyết định · Cài đặt**.

**KHÔNG** xóa các `case` trong `MainWindowViewModel.NavigateToAsync` và **KHÔNG** xóa VM/View — code chết sẽ dọn ở đợt 6B.

- [ ] **Step 2: Build + suite**

Run: `dotnet build 2>&1 | grep -E "error" ; dotnet test 2>&1 | grep -E "Passed!|Failed!"`
Expected: 0 error; toàn bộ test xanh.

- [ ] **Step 3: Chạy thật trên DB tạm và soát checklist**

```bash
export CONSTRUCTIONFINANCE_DB=/tmp/p6a-check.db
DISPLAY=:1 dotnet run --project src/ConstructionFinance.Desktop
```

Soát từng mục:
- [ ] Nav đúng 4 mục; mở app vào thẳng "Tài chính dự án".
- [ ] Dải tab nằm **dưới đáy**; tab `TH` đầu tiên.
- [ ] "Nhập Excel cũ" → chọn `reference/Hieu_sample.xlsx` → xác nhận → 5 tab DA xuất hiện.
- [ ] Nhập lại đúng file lần nữa → báo "5 trùng tên" và **KHÔNG** sinh thêm tab (chống trùng chạy đúng).
- [ ] Bấm tab DA1 → lưới hiện đúng các đợt; sửa một ô → tab thành `• DA1`, tổng cập nhật sống.
- [ ] Ctrl+S → `•` biến mất; bấm tab TH → số đã đổi theo.
- [ ] "+ Thêm dự án" → tab `(dự án mới)`; gõ tên → tiêu đề tab đổi theo.
- [ ] Chuột phải tab DA → "Xóa dự án" → hỏi xác nhận → tab biến mất.
- [ ] Double-click một dòng ở TH → nhảy đúng tab dự án.
- [ ] Sửa một ô rồi đóng cửa sổ → hiện hỏi xác nhận.
- [ ] Log không có exception / dấu hiệu binding loop (`dotnet run` không exit 134).

```bash
rm -f /tmp/p6a-check.db; unset CONSTRUCTIONFINANCE_DB
```

- [ ] **Step 4: Cập nhật ledger**

Thêm vào cuối `.superpowers/sdd/progress.md` mục `== PHASE 6A — Excel workbook ==` ghi: các commit, kết quả suite, phát hiện về `TabStripPlacement=Bottom`, và dòng:

```
>>> CHỜ NGƯỜI DÙNG: xác nhận đã import lại Excel và số liệu ĐÚNG/ĐỦ trên model mới.
>>> Đợt 6B (teardown + DROP bảng) KHÔNG ĐƯỢC bắt đầu trước xác nhận đó (spec §8).
```

- [ ] **Step 5: Commit**

```bash
git add src/ConstructionFinance.Desktop/Views/MainWindow.axaml .superpowers/sdd/progress.md
git commit -m "feat(finance): nav down to 4 items — workbook replaces the old screens"
```

---

## Sau đợt 6A

**Cổng chặn (spec §8):** trước khi bắt đầu đợt 6B, người dùng phải:
1. Backup `app.db`.
2. Import lại Excel gốc bằng workbook.
3. **Xác nhận số liệu đủ/đúng.**

Chỉ khi có xác nhận đó mới viết plan cho **đợt 6B — teardown** (xóa code chết + migration DROP).
