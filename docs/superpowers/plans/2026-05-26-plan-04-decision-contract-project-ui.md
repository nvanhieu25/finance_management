# Plan 04: Decision / Contract / Project CRUD UIs

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Hoàn thiện CRUD UI cho 3 thực thể trung tâm của cây nghiệp vụ: **Quyết định phê duyệt → Hợp đồng → Dự án**. Người dùng có thể drill-down qua từng cấp: mở danh sách Quyết định → chọn 1 → xem chi tiết + danh sách Hợp đồng bên dưới → chọn 1 → xem chi tiết + danh sách Dự án bên dưới → chọn 1 → xem chi tiết Dự án. Mỗi cấp có CRUD đầy đủ (thêm/sửa/xóa). ProjectDetailView có tab "Chi phí" với placeholder — Plan 05 sẽ thay bằng cost tree.

**Architecture:** Lặp lại pattern Vendor từ Plan 03 cho 3 entity, cộng thêm **`INavigationService`** để chuyển view (`MainContent`) trong shell và giữ back-stack đơn giản. Mỗi entity có:
- `XxxListViewModel` (CRUD trên flat list) — dùng cho menu trực tiếp
- `XxxEditDialogViewModel` (form thêm/sửa) — dùng qua `IDialogService`
- `XxxDetailViewModel` (xem chi tiết + danh sách entity con bên dưới) — chỉ Decision và Contract
- `ProjectDetailViewModel` (TabControl: tab Info hoạt động, tab Chi phí placeholder để Plan 05 thay)

**Tech Stack:** Đã có từ Plan 03 — Avalonia 11, FluentAvalonia, CommunityToolkit.Mvvm, EF Core 8. Không thêm package mới.

**Reference:** [Design doc](../specs/2026-05-19-construction-finance-app-design.md) sections 4.3 (Decision module), 4.4 (Contract module), 4.5 (Project module), [Plan 03](2026-05-25-plan-03-avalonia-shell-vendor-ui.md) (Vendor pattern lập sẵn).

**Out of scope** (Plan 05+):
- Cost tree UI (tab "Chi phí" trong ProjectDetailView) — Plan 05
- Payment UI (nested trong cost item) — Plan 05
- Tab "Báo cáo" và "Tài liệu" trong ProjectDetailView — Plan 06+
- Breadcrumb navigation (chỉ Back button trong Plan này)
- Sidebar "Cây Dự án" (tree view xuyên QĐ→HĐ→DA) — sau MVP
- Trạng thái dropdown trong ProjectDetailView dùng menu nhanh thay vì dialog

---

## Key Decisions (flag — phản hồi nếu không đồng ý)

| # | Quyết định | Lý do |
|---|---|---|
| D1 | Master-detail drill-down: Decision list → Decision detail (chứa Contract list nested) → Contract detail (chứa Project list nested) → Project detail | Khớp design doc 4.3-4.5; tự nhiên với user; tránh phải vẽ breadcrumb phức tạp ở plan này |
| D2 | `INavigationService` đơn giản: `NavigateTo(viewModel)` + `GoBack()` với 1 back-stack `Stack<object>` | Đủ cho drill-down 4 cấp; phức tạp hơn (route, deep link) chưa cần |
| D3 | Detail view nhúng list view con — Decision detail nhúng `ContractListViewModel(decisionId)`, v.v. | DRY: tái dùng list logic, tránh duplicate code |
| D4 | Project status update qua dropdown `ComboBox` trong ProjectDetailView, gọi `IProjectService.UpdateStatusAsync` — bắt `BusinessRuleException` cho transition không hợp lệ | Đơn giản, không cần dialog riêng cho status |
| D5 | ProjectDetailView dùng `TabControl` 4 tab: Info (đầy đủ), Chi phí (placeholder text), Báo cáo (placeholder), Tài liệu (placeholder) | Đúng design 4.5; Plan 05 thay tab Chi phí bằng cost tree |
| D6 | Delete confirm dialog dùng `IDialogService.ConfirmAsync` (đã có) với thông điệp tiếng Việt | Như pattern Vendor; nhất quán UX |
| D7 | Error từ service (`NotFoundException`, `BusinessRuleException`, `ValidationException`) → `IDialogService.ShowMessageAsync("Lỗi", ex.Message)` | Đơn giản; sau này có thể dùng InfoBar inline nhưng chưa cần |
| D8 | Project list trong ContractDetailView KHÔNG show ngay full project info, chỉ list grid → click row vào detail | Tách responsibility rõ; nhất quán pattern Decision→Contract |

---

## File Structure

```
src/ConstructionFinance.Desktop/
├── Services/
│   ├── INavigationService.cs               (NEW)
│   └── NavigationService.cs                (NEW)
├── ViewModels/
│   ├── MainWindowViewModel.cs              (MODIFIED — accept INavigationService, expose CurrentContent via service)
│   ├── Decisions/
│   │   ├── DecisionListViewModel.cs        (NEW)
│   │   ├── DecisionEditDialogViewModel.cs  (NEW)
│   │   └── DecisionDetailViewModel.cs      (NEW)
│   ├── Contracts/
│   │   ├── ContractListViewModel.cs        (NEW — filtered by decisionId)
│   │   ├── ContractEditDialogViewModel.cs  (NEW)
│   │   └── ContractDetailViewModel.cs      (NEW)
│   └── Projects/
│       ├── ProjectListViewModel.cs         (NEW — filtered by contractId)
│       ├── ProjectEditDialogViewModel.cs   (NEW)
│       └── ProjectDetailViewModel.cs       (NEW — tabbed; placeholders for non-Info tabs)
├── Views/
│   ├── Decisions/
│   │   ├── DecisionListView.axaml(.cs)     (NEW)
│   │   ├── DecisionEditDialog.axaml(.cs)   (NEW)
│   │   └── DecisionDetailView.axaml(.cs)   (NEW)
│   ├── Contracts/
│   │   ├── ContractEditDialog.axaml(.cs)   (NEW)
│   │   └── ContractDetailView.axaml(.cs)   (NEW)
│   └── Projects/
│       ├── ProjectEditDialog.axaml(.cs)    (NEW)
│       └── ProjectDetailView.axaml(.cs)    (NEW — TabControl)
└── Common/
    └── HostBuilder.cs                      (MODIFIED — register all new VMs + INavigationService)

tests/ConstructionFinance.Desktop.Tests/
├── Common/
│   └── FakeNavigationService.cs            (NEW)
├── Decisions/
│   ├── DecisionListViewModelTests.cs       (NEW)
│   ├── DecisionEditDialogViewModelTests.cs (NEW)
│   └── DecisionDetailViewModelTests.cs     (NEW)
├── Contracts/
│   ├── ContractListViewModelTests.cs       (NEW)
│   ├── ContractEditDialogViewModelTests.cs (NEW)
│   └── ContractDetailViewModelTests.cs     (NEW)
└── Projects/
    ├── ProjectListViewModelTests.cs        (NEW)
    ├── ProjectEditDialogViewModelTests.cs  (NEW)
    └── ProjectDetailViewModelTests.cs      (NEW)
```

---

## Pre-flight

- [ ] **Step 0.1: Verify Plan 03 committed and green**

Run from `/home/hieu-nguyen/management/`:
```bash
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
git status --short
dotnet build --configuration Release
dotnet test --configuration Release
```
Expected: Clean working tree (commit Plan 03 first). Build 0 errors. All 126 tests pass.

If working tree dirty: commit Plan 03 BEFORE starting Plan 04.

---

## Task 1: `INavigationService` + `NavigationService` + `FakeNavigationService`

**Files:**
- Create: `src/ConstructionFinance.Desktop/Services/INavigationService.cs`
- Create: `src/ConstructionFinance.Desktop/Services/NavigationService.cs`
- Create: `tests/ConstructionFinance.Desktop.Tests/Common/FakeNavigationService.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Common/NavigationServiceTests.cs`

- [ ] **Step 1.1: Write failing tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Desktop.Tests/Common/NavigationServiceTests.cs`:
```csharp
using ConstructionFinance.Desktop.Services;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Common;

public class NavigationServiceTests
{
    [Fact]
    public void NavigateTo_sets_CurrentContent()
    {
        var nav = new NavigationService();
        var vm = new object();

        nav.NavigateTo(vm);

        nav.CurrentContent.ShouldBe(vm);
    }

    [Fact]
    public void NavigateTo_raises_event()
    {
        var nav = new NavigationService();
        object? captured = null;
        nav.CurrentContentChanged += (_, c) => captured = c;

        nav.NavigateTo("X");

        captured.ShouldBe("X");
    }

    [Fact]
    public void GoBack_restores_previous_content()
    {
        var nav = new NavigationService();
        nav.NavigateTo("A");
        nav.NavigateTo("B");

        nav.GoBack();

        nav.CurrentContent.ShouldBe("A");
    }

    [Fact]
    public void GoBack_when_empty_stack_does_nothing()
    {
        var nav = new NavigationService();
        nav.NavigateTo("A");

        nav.GoBack();      // no previous → current stays
        nav.GoBack();      // still empty

        nav.CurrentContent.ShouldBe("A");
    }

    [Fact]
    public void CanGoBack_true_after_two_navigates()
    {
        var nav = new NavigationService();
        nav.CanGoBack.ShouldBeFalse();
        nav.NavigateTo("A");
        nav.CanGoBack.ShouldBeFalse();
        nav.NavigateTo("B");
        nav.CanGoBack.ShouldBeTrue();
    }
}
```

- [ ] **Step 1.2: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~NavigationServiceTests"`
Expected: Type not found.

- [ ] **Step 1.3: Implement `INavigationService`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Services/INavigationService.cs`:
```csharp
namespace ConstructionFinance.Desktop.Services;

public interface INavigationService
{
    object? CurrentContent { get; }
    bool CanGoBack { get; }
    event EventHandler<object?>? CurrentContentChanged;

    void NavigateTo(object viewModel);
    void GoBack();
    void Reset();
}
```

- [ ] **Step 1.4: Implement `NavigationService`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Services/NavigationService.cs`:
```csharp
namespace ConstructionFinance.Desktop.Services;

public class NavigationService : INavigationService
{
    private readonly Stack<object> _back = new();

    public object? CurrentContent { get; private set; }
    public bool CanGoBack => _back.Count > 0;
    public event EventHandler<object?>? CurrentContentChanged;

    public void NavigateTo(object viewModel)
    {
        if (CurrentContent is not null)
            _back.Push(CurrentContent);
        CurrentContent = viewModel;
        CurrentContentChanged?.Invoke(this, CurrentContent);
    }

    public void GoBack()
    {
        if (_back.Count == 0) return;
        CurrentContent = _back.Pop();
        CurrentContentChanged?.Invoke(this, CurrentContent);
    }

    public void Reset()
    {
        _back.Clear();
        CurrentContent = null;
        CurrentContentChanged?.Invoke(this, null);
    }
}
```

- [ ] **Step 1.5: Implement `FakeNavigationService` for VM tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Desktop.Tests/Common/FakeNavigationService.cs`:
```csharp
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.Tests.Common;

public class FakeNavigationService : INavigationService
{
    public object? CurrentContent { get; private set; }
    public bool CanGoBack => NavigationHistory.Count > 0;
    public event EventHandler<object?>? CurrentContentChanged;

    public List<object> NavigationHistory { get; } = new();

    public void NavigateTo(object viewModel)
    {
        NavigationHistory.Add(viewModel);
        CurrentContent = viewModel;
        CurrentContentChanged?.Invoke(this, viewModel);
    }

    public void GoBack()
    {
        if (NavigationHistory.Count == 0) return;
        NavigationHistory.RemoveAt(NavigationHistory.Count - 1);
        CurrentContent = NavigationHistory.LastOrDefault();
        CurrentContentChanged?.Invoke(this, CurrentContent);
    }

    public void Reset()
    {
        NavigationHistory.Clear();
        CurrentContent = null;
        CurrentContentChanged?.Invoke(this, null);
    }
}
```

- [ ] **Step 1.6: Register in HostBuilder**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Common/HostBuilder.cs`. Inside `ConfigureServices`, after the `IDialogService` registration line, add:
```csharp
                services.AddSingleton<Services.INavigationService, Services.NavigationService>();
```

- [ ] **Step 1.7: Verify + commit**

Run:
```bash
dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~NavigationServiceTests"
git add . && git commit -m "feat(desktop): INavigationService + NavigationService with back-stack"
```
Expected: 5 tests pass.

---

## Task 2: DecisionListViewModel — TDD

**Files:**
- Create: `src/ConstructionFinance.Desktop/ViewModels/Decisions/DecisionListViewModel.cs`
- Create: stub `src/ConstructionFinance.Desktop/ViewModels/Decisions/DecisionEditDialogViewModel.cs` (full impl Task 3)
- Create: stub `src/ConstructionFinance.Desktop/ViewModels/Decisions/DecisionDetailViewModel.cs` (full impl Task 4)
- Test: `tests/ConstructionFinance.Desktop.Tests/Decisions/DecisionListViewModelTests.cs`

- [ ] **Step 2.1: Write failing tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Desktop.Tests/Decisions/DecisionListViewModelTests.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Decisions;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Decisions;

public class DecisionListViewModelTests
{
    private static DecisionDto D(int id, string code) =>
        new(id, code, "Tên QĐ", new DateOnly(2026, 1, 1), "UBND", 1_000_000m, null);

    private static (DecisionListViewModel vm, Mock<IDecisionService> svc, FakeDialogService dlg, FakeNavigationService nav) Create(IEnumerable<DecisionDto>? initial = null)
    {
        var svc = new Mock<IDecisionService>(MockBehavior.Strict);
        svc.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((initial ?? Array.Empty<DecisionDto>()).ToList());
        var dlg = new FakeDialogService();
        var nav = new FakeNavigationService();
        var sp = new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider();
        var vm = new DecisionListViewModel(svc.Object, dlg, nav, sp);
        return (vm, svc, dlg, nav);
    }

    [Fact]
    public async Task LoadAsync_populates_Decisions()
    {
        var (vm, _, _, _) = Create(new[] { D(1, "1234/QD"), D(2, "5678/QD") });
        await vm.LoadAsync();
        vm.Decisions.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AddCommand_opens_dialog_and_creates_on_confirm()
    {
        var (vm, svc, dlg, _) = Create();
        svc.Setup(s => s.CreateAsync(It.IsAny<CreateDecisionDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(D(99, "NEW"));
        dlg.DialogResult = true;
        await vm.LoadAsync();

        await vm.AddCommand.ExecuteAsync(null);

        svc.Verify(s => s.CreateAsync(It.IsAny<CreateDecisionDto>(), It.IsAny<CancellationToken>()), Times.Once);
        vm.Decisions.ShouldContain(d => d.DecisionCode == "NEW");
    }

    [Fact]
    public async Task DeleteCommand_calls_service_when_confirmed()
    {
        var (vm, svc, dlg, _) = Create(new[] { D(1, "X") });
        svc.Setup(s => s.DeleteAsync(1, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        dlg.ConfirmResult = true;
        await vm.LoadAsync();
        vm.SelectedDecision = vm.Decisions[0];

        await vm.DeleteCommand.ExecuteAsync(null);

        svc.Verify(s => s.DeleteAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        vm.Decisions.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteCommand_shows_error_message_on_BusinessRuleException()
    {
        var (vm, svc, dlg, _) = Create(new[] { D(1, "X") });
        svc.Setup(s => s.DeleteAsync(1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConstructionFinance.Application.Common.Exceptions.BusinessRuleException("Còn HĐ"));
        dlg.ConfirmResult = true;
        await vm.LoadAsync();
        vm.SelectedDecision = vm.Decisions[0];

        await vm.DeleteCommand.ExecuteAsync(null);

        dlg.ShownMessages.ShouldContain(m => m.Contains("Còn HĐ"));
        vm.Decisions.Count.ShouldBe(1); // not removed
    }

    [Fact]
    public async Task OpenDetailCommand_navigates_when_selection_set()
    {
        var (vm, _, _, nav) = Create(new[] { D(1, "X") });
        await vm.LoadAsync();
        vm.SelectedDecision = vm.Decisions[0];

        await vm.OpenDetailCommand.ExecuteAsync(null);

        nav.NavigationHistory.Count.ShouldBe(1);
        nav.NavigationHistory[0].ShouldBeOfType<DecisionDetailViewModel>();
    }
}
```

- [ ] **Step 2.2: Create stub `DecisionEditDialogViewModel`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Decisions/DecisionEditDialogViewModel.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.ViewModels.Decisions;

// Stub — full implementation in Task 3.
public partial class DecisionEditDialogViewModel : ViewModelBase
{
    public DecisionEditDialogViewModel(DecisionDto? initial) { }
    public CreateDecisionDto ToCreateDto() =>
        new("stub", "stub", new DateOnly(2026, 1, 1), "stub", 0m, null);
    public UpdateDecisionDto ToUpdateDto(int id) =>
        new(id, "stub", "stub", new DateOnly(2026, 1, 1), "stub", 0m, null);
}
```

- [ ] **Step 2.3: Create stub `DecisionDetailViewModel`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Decisions/DecisionDetailViewModel.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.ViewModels.Decisions;

// Stub — full implementation in Task 4.
public partial class DecisionDetailViewModel : ViewModelBase
{
    public DecisionDetailViewModel(DecisionDto decision) { Decision = decision; }
    public DecisionDto Decision { get; }
}
```

- [ ] **Step 2.4: Implement `DecisionListViewModel`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Decisions/DecisionListViewModel.cs`:
```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructionFinance.Desktop.ViewModels.Decisions;

public partial class DecisionListViewModel : ViewModelBase
{
    private readonly IDecisionService _service;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;
    private readonly IServiceProvider _services;

    public DecisionListViewModel(
        IDecisionService service,
        IDialogService dialog,
        INavigationService nav,
        IServiceProvider services)
    {
        _service = service;
        _dialog = dialog;
        _nav = nav;
        _services = services;
    }

    public ObservableCollection<DecisionDto> Decisions { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenDetailCommand))]
    private DecisionDto? _selectedDecision;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var list = await _service.GetAllAsync();
            Decisions.Clear();
            foreach (var d in list) Decisions.Add(d);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var editVm = new DecisionEditDialogViewModel(initial: null);
        var ok = await _dialog.ShowDialogAsync(editVm);
        if (ok != true) return;
        try
        {
            var created = await _service.CreateAsync(editVm.ToCreateDto());
            Decisions.Add(created);
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync()
    {
        if (SelectedDecision is null) return;
        var editVm = new DecisionEditDialogViewModel(initial: SelectedDecision);
        var ok = await _dialog.ShowDialogAsync(editVm);
        if (ok != true) return;
        try
        {
            var updated = await _service.UpdateAsync(editVm.ToUpdateDto(SelectedDecision.Id));
            var idx = Decisions.IndexOf(SelectedDecision);
            if (idx >= 0) Decisions[idx] = updated;
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        if (SelectedDecision is null) return;
        var ok = await _dialog.ConfirmAsync("Xóa Quyết định",
            $"Bạn có chắc muốn xóa Quyết định \"{SelectedDecision.DecisionCode}\"? Có thể khôi phục trong 30 ngày.");
        if (!ok) return;
        try
        {
            await _service.DeleteAsync(SelectedDecision.Id);
            Decisions.Remove(SelectedDecision);
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task OpenDetailAsync()
    {
        if (SelectedDecision is null) return Task.CompletedTask;
        var detail = new DecisionDetailViewModel(SelectedDecision);
        _nav.NavigateTo(detail);
        return Task.CompletedTask;
    }

    private bool HasSelection() => SelectedDecision is not null;
}
```

- [ ] **Step 2.5: Run tests — pass**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~DecisionListViewModelTests"`
Expected: 5 tests pass.

- [ ] **Step 2.6: Commit**

```bash
git add . && git commit -m "feat(desktop): DecisionListViewModel with CRUD + drill-down navigation"
```

---

## Task 3: `DecisionEditDialogViewModel` — TDD (replace stub)

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Decisions/DecisionEditDialogViewModel.cs` (replace stub)
- Test: `tests/ConstructionFinance.Desktop.Tests/Decisions/DecisionEditDialogViewModelTests.cs`

- [ ] **Step 3.1: Write failing tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Desktop.Tests/Decisions/DecisionEditDialogViewModelTests.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.ViewModels.Decisions;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Decisions;

public class DecisionEditDialogViewModelTests
{
    [Fact]
    public void New_dialog_has_empty_fields()
    {
        var vm = new DecisionEditDialogViewModel(initial: null);
        vm.IsEditMode.ShouldBeFalse();
        vm.DialogTitle.ShouldBe("Thêm Quyết định phê duyệt");
        vm.DecisionCode.ShouldBeNullOrEmpty();
    }

    [Fact]
    public void Edit_dialog_pre_fills_from_dto()
    {
        var dto = new DecisionDto(5, "1234/QD", "Tên QĐ", new DateOnly(2026, 3, 15), "UBND Tỉnh ABC", 50_000_000_000m, "Note");
        var vm = new DecisionEditDialogViewModel(initial: dto);

        vm.IsEditMode.ShouldBeTrue();
        vm.DialogTitle.ShouldBe("Sửa Quyết định phê duyệt");
        vm.DecisionCode.ShouldBe("1234/QD");
        vm.IssuingAuthority.ShouldBe("UBND Tỉnh ABC");
        vm.TotalApprovedAmount.ShouldBe(50_000_000_000m);
    }

    [Fact]
    public void ToCreateDto_maps_all_fields()
    {
        var vm = new DecisionEditDialogViewModel(initial: null)
        {
            DecisionCode = "1234/QD",
            DecisionTitle = "Phê duyệt",
            DecisionDate = new DateOnly(2026, 3, 15),
            IssuingAuthority = "UBND",
            TotalApprovedAmount = 100_000m
        };

        var dto = vm.ToCreateDto();
        dto.DecisionCode.ShouldBe("1234/QD");
        dto.TotalApprovedAmount.ShouldBe(100_000m);
    }

    [Fact]
    public void Validation_DecisionCode_required()
    {
        var vm = new DecisionEditDialogViewModel(initial: null) { DecisionCode = "" };
        vm.DecisionCodeError.ShouldNotBeNullOrEmpty();
        vm.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validation_TotalApprovedAmount_negative_invalid()
    {
        var vm = new DecisionEditDialogViewModel(initial: null)
        {
            DecisionCode = "X",
            DecisionTitle = "Y",
            IssuingAuthority = "Z",
            TotalApprovedAmount = -1
        };
        vm.TotalApprovedAmountError.ShouldNotBeNullOrEmpty();
        vm.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void IsValid_true_when_all_required_filled()
    {
        var vm = new DecisionEditDialogViewModel(initial: null)
        {
            DecisionCode = "X",
            DecisionTitle = "Y",
            IssuingAuthority = "Z",
            TotalApprovedAmount = 100m
        };
        vm.IsValid.ShouldBeTrue();
    }
}
```

- [ ] **Step 3.2: Run — fails (stub doesn't have these members)**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~DecisionEditDialogViewModelTests"`
Expected: Multiple failures.

- [ ] **Step 3.3: Replace stub with full implementation**

Replace `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Decisions/DecisionEditDialogViewModel.cs` with:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.ViewModels.Decisions;

public partial class DecisionEditDialogViewModel : ViewModelBase, IHasDialogTitle
{
    public bool IsEditMode { get; }
    public string DialogTitle => IsEditMode ? "Sửa Quyết định phê duyệt" : "Thêm Quyết định phê duyệt";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DecisionCodeError), nameof(IsValid))]
    private string _decisionCode = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DecisionTitleError), nameof(IsValid))]
    private string _decisionTitle = "";

    [ObservableProperty] private DateOnly _decisionDate = DateOnly.FromDateTime(DateTime.Today);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IssuingAuthorityError), nameof(IsValid))]
    private string _issuingAuthority = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalApprovedAmountError), nameof(IsValid))]
    private decimal _totalApprovedAmount;

    [ObservableProperty] private string? _notes;

    public DecisionEditDialogViewModel(DecisionDto? initial)
    {
        IsEditMode = initial is not null;
        if (initial is not null)
        {
            DecisionCode = initial.DecisionCode;
            DecisionTitle = initial.DecisionTitle;
            DecisionDate = initial.DecisionDate;
            IssuingAuthority = initial.IssuingAuthority;
            TotalApprovedAmount = initial.TotalApprovedAmount;
            Notes = initial.Notes;
        }
    }

    public string? DecisionCodeError =>
        string.IsNullOrWhiteSpace(DecisionCode) ? "Mã Quyết định là bắt buộc." : null;

    public string? DecisionTitleError =>
        string.IsNullOrWhiteSpace(DecisionTitle) ? "Trích yếu là bắt buộc." : null;

    public string? IssuingAuthorityError =>
        string.IsNullOrWhiteSpace(IssuingAuthority) ? "Cơ quan ban hành là bắt buộc." : null;

    public string? TotalApprovedAmountError =>
        TotalApprovedAmount < 0 ? "Tổng giá trị không thể âm." : null;

    public bool IsValid =>
        DecisionCodeError is null &&
        DecisionTitleError is null &&
        IssuingAuthorityError is null &&
        TotalApprovedAmountError is null;

    public CreateDecisionDto ToCreateDto() =>
        new(DecisionCode, DecisionTitle, DecisionDate, IssuingAuthority, TotalApprovedAmount, Notes);

    public UpdateDecisionDto ToUpdateDto(int id) =>
        new(id, DecisionCode, DecisionTitle, DecisionDate, IssuingAuthority, TotalApprovedAmount, Notes);
}
```

- [ ] **Step 3.4: Run tests — pass**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~DecisionEditDialogViewModelTests"`
Expected: 6 tests pass.

- [ ] **Step 3.5: Commit**

```bash
git add . && git commit -m "feat(desktop): DecisionEditDialogViewModel with inline validation"
```

---

## Task 4: `DecisionDetailViewModel` — TDD (replace stub)

`DecisionDetailViewModel` shows decision info AND embeds a nested `ContractListViewModel(decisionId)`. So this task also needs a stub for `ContractListViewModel`.

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Decisions/DecisionDetailViewModel.cs` (replace stub)
- Create stub: `src/ConstructionFinance.Desktop/ViewModels/Contracts/ContractListViewModel.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Decisions/DecisionDetailViewModelTests.cs`

- [ ] **Step 4.1: Write failing tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Desktop.Tests/Decisions/DecisionDetailViewModelTests.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Contracts;
using ConstructionFinance.Desktop.ViewModels.Decisions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Decisions;

public class DecisionDetailViewModelTests
{
    private static DecisionDto D(int id) =>
        new(id, "1234/QD", "Phê duyệt X", new DateOnly(2026, 3, 15), "UBND", 50_000_000_000m, null);

    [Fact]
    public void Exposes_Decision_property()
    {
        var dto = D(5);
        var sp = BuildServiceProvider();
        var nav = new FakeNavigationService();

        var vm = new DecisionDetailViewModel(dto, nav, sp);

        vm.Decision.ShouldBe(dto);
        vm.Decision.DecisionCode.ShouldBe("1234/QD");
    }

    [Fact]
    public void Exposes_Contracts_child_VM_scoped_to_this_decision()
    {
        var dto = D(5);
        var sp = BuildServiceProvider();
        var nav = new FakeNavigationService();

        var vm = new DecisionDetailViewModel(dto, nav, sp);

        vm.Contracts.ShouldNotBeNull();
        vm.Contracts.DecisionId.ShouldBe(5);
    }

    [Fact]
    public async Task GoBackCommand_invokes_NavigationService_GoBack()
    {
        var dto = D(5);
        var sp = BuildServiceProvider();
        var nav = new FakeNavigationService();
        nav.NavigateTo("Previous"); // so there's something to go back to
        var vm = new DecisionDetailViewModel(dto, nav, sp);
        nav.NavigateTo(vm);

        await vm.GoBackCommand.ExecuteAsync(null);

        nav.CurrentContent.ShouldBe("Previous");
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var sc = new ServiceCollection();
        var contractSvc = new Mock<IContractService>(MockBehavior.Loose);
        contractSvc.Setup(s => s.GetByDecisionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ContractDto>());
        sc.AddSingleton(contractSvc.Object);
        sc.AddSingleton<Services.IDialogService>(new FakeDialogService());
        sc.AddSingleton<Services.INavigationService>(new FakeNavigationService());
        sc.AddTransient<ContractListViewModel>();
        return sc.BuildServiceProvider();
    }
}
```

Note the namespace alias: `using Services = ConstructionFinance.Desktop.Services;` — add this to the test file's using block if `IDialogService`/`INavigationService` are ambiguous. If not ambiguous, can use directly: `ConstructionFinance.Desktop.Services.IDialogService`. Adjust based on actual conflicts.

For clarity, replace the `BuildServiceProvider` method's lines to use fully-qualified types:
```csharp
        sc.AddSingleton<ConstructionFinance.Desktop.Services.IDialogService>(new FakeDialogService());
        sc.AddSingleton<ConstructionFinance.Desktop.Services.INavigationService>(new FakeNavigationService());
```

- [ ] **Step 4.2: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~DecisionDetailViewModelTests"`
Expected: Types missing.

- [ ] **Step 4.3: Create stub `ContractListViewModel`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Contracts/ContractListViewModel.cs`:
```csharp
using System.Collections.ObjectModel;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.ViewModels.Contracts;

// Stub — full implementation in Task 6.
public partial class ContractListViewModel : ViewModelBase
{
    public ContractListViewModel(IContractService service, IDialogService dialog, INavigationService nav, IServiceProvider services)
    {
        // service/dialog/nav/services parameters reserved for full impl in Task 6
        _ = service; _ = dialog; _ = nav; _ = services;
    }

    public int DecisionId { get; set; }
    public ObservableCollection<ContractDto> Contracts { get; } = new();

    public void SetDecisionId(int decisionId) => DecisionId = decisionId;

    public Task LoadAsync() => Task.CompletedTask;
}
```

- [ ] **Step 4.4: Implement `DecisionDetailViewModel`**

Replace `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Decisions/DecisionDetailViewModel.cs` with:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.ViewModels.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructionFinance.Desktop.ViewModels.Decisions;

public partial class DecisionDetailViewModel : ViewModelBase
{
    private readonly INavigationService _nav;

    public DecisionDetailViewModel(DecisionDto decision, INavigationService nav, IServiceProvider services)
    {
        _nav = nav;
        Decision = decision;
        Contracts = services.GetRequiredService<ContractListViewModel>();
        Contracts.SetDecisionId(decision.Id);
    }

    public DecisionDto Decision { get; }
    public ContractListViewModel Contracts { get; }

    public Task InitializeAsync() => Contracts.LoadAsync();

    [RelayCommand]
    private void GoBack() => _nav.GoBack();
}
```

- [ ] **Step 4.5: Run tests — pass**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~DecisionDetailViewModelTests"`
Expected: 3 tests pass.

- [ ] **Step 4.6: Update `DecisionListViewModel.OpenDetailAsync` to construct with new signature**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Decisions/DecisionListViewModel.cs`. Replace the `OpenDetailAsync` method body:
```csharp
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task OpenDetailAsync()
    {
        if (SelectedDecision is null) return Task.CompletedTask;
        var detail = new DecisionDetailViewModel(SelectedDecision, _nav, _services);
        _nav.NavigateTo(detail);
        return detail.InitializeAsync();
    }
```

Also update the `OpenDetailCommand_navigates_when_selection_set` test — it asserts on `DecisionDetailViewModel` so should still pass.

- [ ] **Step 4.7: Run all tests so far**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/`
Expected: All pass (Decision tests + previous Vendor tests + Navigation tests).

- [ ] **Step 4.8: Commit**

```bash
git add . && git commit -m "feat(desktop): DecisionDetailViewModel with nested ContractListViewModel"
```

---

## Task 5: Decision XAML views

**Files:**
- Create: `src/ConstructionFinance.Desktop/Views/Decisions/DecisionListView.axaml(.cs)`
- Create: `src/ConstructionFinance.Desktop/Views/Decisions/DecisionEditDialog.axaml(.cs)`
- Create: `src/ConstructionFinance.Desktop/Views/Decisions/DecisionDetailView.axaml(.cs)`

- [ ] **Step 5.1: Create `DecisionListView.axaml`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Decisions/DecisionListView.axaml`:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Decisions"
             x:Class="ConstructionFinance.Desktop.Views.Decisions.DecisionListView"
             x:DataType="vm:DecisionListViewModel">
    <DockPanel>
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Spacing="8" Margin="0,0,0,12">
            <Button Content="+ Thêm" Command="{Binding AddCommand}"/>
            <Button Content="Sửa" Command="{Binding EditCommand}"/>
            <Button Content="Xóa" Command="{Binding DeleteCommand}"/>
            <Button Content="Xem chi tiết →" Command="{Binding OpenDetailCommand}"/>
            <ProgressBar IsIndeterminate="True" IsVisible="{Binding IsBusy}" Width="100" VerticalAlignment="Center"/>
            <TextBlock Text="{Binding ErrorMessage}" Foreground="Red" VerticalAlignment="Center"/>
        </StackPanel>
        <DataGrid ItemsSource="{Binding Decisions}"
                  SelectedItem="{Binding SelectedDecision}"
                  IsReadOnly="True"
                  GridLinesVisibility="Horizontal"
                  HeadersVisibility="Column">
            <DataGrid.Columns>
                <DataGridTextColumn Header="Mã QĐ" Binding="{Binding DecisionCode}" Width="180"/>
                <DataGridTextColumn Header="Trích yếu" Binding="{Binding DecisionTitle}" Width="*"/>
                <DataGridTextColumn Header="Ngày" Binding="{Binding DecisionDate}" Width="110"/>
                <DataGridTextColumn Header="Cơ quan" Binding="{Binding IssuingAuthority}" Width="220"/>
                <DataGridTextColumn Header="Tổng giá trị" Binding="{Binding TotalApprovedAmount, StringFormat=#\\,##0}" Width="160"/>
            </DataGrid.Columns>
        </DataGrid>
    </DockPanel>
</UserControl>
```

- [ ] **Step 5.2: Create `DecisionListView.axaml.cs`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Decisions/DecisionListView.axaml.cs`:
```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop.Views.Decisions;

public partial class DecisionListView : UserControl
{
    public DecisionListView() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 5.3: Create `DecisionEditDialog.axaml`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Decisions/DecisionEditDialog.axaml`:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Decisions"
             x:Class="ConstructionFinance.Desktop.Views.Decisions.DecisionEditDialog"
             x:DataType="vm:DecisionEditDialogViewModel"
             MinWidth="520">
    <Grid ColumnDefinitions="170,*" RowDefinitions="Auto,Auto,Auto,Auto,Auto,Auto" RowSpacing="8" ColumnSpacing="8">
        <TextBlock Grid.Row="0" Grid.Column="0" Text="Mã Quyết định *" VerticalAlignment="Center"/>
        <StackPanel Grid.Row="0" Grid.Column="1">
            <TextBox Text="{Binding DecisionCode}"/>
            <TextBlock Text="{Binding DecisionCodeError}" Foreground="Red" FontSize="11"
                       IsVisible="{Binding DecisionCodeError, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"/>
        </StackPanel>

        <TextBlock Grid.Row="1" Grid.Column="0" Text="Trích yếu *" VerticalAlignment="Center"/>
        <StackPanel Grid.Row="1" Grid.Column="1">
            <TextBox Text="{Binding DecisionTitle}" AcceptsReturn="True" Height="60" TextWrapping="Wrap"/>
            <TextBlock Text="{Binding DecisionTitleError}" Foreground="Red" FontSize="11"
                       IsVisible="{Binding DecisionTitleError, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"/>
        </StackPanel>

        <TextBlock Grid.Row="2" Grid.Column="0" Text="Ngày ban hành" VerticalAlignment="Center"/>
        <DatePicker Grid.Row="2" Grid.Column="1" SelectedDate="{Binding DecisionDate, Mode=TwoWay, Converter={x:Static DateTimeOffsetConverters.DateOnlyToDateTimeOffset}}"/>

        <TextBlock Grid.Row="3" Grid.Column="0" Text="Cơ quan ban hành *" VerticalAlignment="Center"/>
        <StackPanel Grid.Row="3" Grid.Column="1">
            <TextBox Text="{Binding IssuingAuthority}"/>
            <TextBlock Text="{Binding IssuingAuthorityError}" Foreground="Red" FontSize="11"
                       IsVisible="{Binding IssuingAuthorityError, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"/>
        </StackPanel>

        <TextBlock Grid.Row="4" Grid.Column="0" Text="Tổng giá trị (VND) *" VerticalAlignment="Center"/>
        <StackPanel Grid.Row="4" Grid.Column="1">
            <NumericUpDown Value="{Binding TotalApprovedAmount}" Minimum="0" FormatString="#,##0"/>
            <TextBlock Text="{Binding TotalApprovedAmountError}" Foreground="Red" FontSize="11"
                       IsVisible="{Binding TotalApprovedAmountError, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"/>
        </StackPanel>

        <TextBlock Grid.Row="5" Grid.Column="0" Text="Ghi chú" VerticalAlignment="Top"/>
        <TextBox Grid.Row="5" Grid.Column="1" Text="{Binding Notes}" AcceptsReturn="True" Height="80" TextWrapping="Wrap"/>
    </Grid>
</UserControl>
```

> **Note about `DateOnlyToDateTimeOffset`**: Avalonia's `DatePicker` works with `DateTimeOffset?`. We need a small converter to bridge `DateOnly`. The static class `DateTimeOffsetConverters` doesn't exist by default — we create it in Step 5.4.

- [ ] **Step 5.4: Create `DateOnlyToDateTimeOffset` value converter**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Common/DateTimeOffsetConverters.cs`:
```csharp
using System.Globalization;
using Avalonia.Data.Converters;

namespace ConstructionFinance.Desktop.Common;

public static class DateTimeOffsetConverters
{
    public static readonly IValueConverter DateOnlyToDateTimeOffset = new FuncValueConverter<DateOnly, DateTimeOffset?>(
        d => new DateTimeOffset(d.Year, d.Month, d.Day, 0, 0, 0, TimeSpan.Zero));

    public static readonly IValueConverter NullableDateOnlyToDateTimeOffset = new FuncValueConverter<DateOnly?, DateTimeOffset?>(
        d => d is null ? null : new DateTimeOffset(d.Value.Year, d.Value.Month, d.Value.Day, 0, 0, 0, TimeSpan.Zero));
}
```

Update the XAML's converter reference to use the namespace import. Replace the line in `DecisionEditDialog.axaml`:
```xml
<DatePicker Grid.Row="2" Grid.Column="1" SelectedDate="{Binding DecisionDate, Mode=TwoWay, Converter={x:Static DateTimeOffsetConverters.DateOnlyToDateTimeOffset}}"/>
```
with:
```xml
<DatePicker Grid.Row="2" Grid.Column="1" SelectedDate="{Binding DecisionDate, Mode=TwoWay, Converter={x:Static common:DateTimeOffsetConverters.DateOnlyToDateTimeOffset}}"/>
```
and add to the root `<UserControl ...>` attributes:
```xml
xmlns:common="using:ConstructionFinance.Desktop.Common"
```

> Note: Two-way binding from `DateTimeOffset?` back to non-nullable `DateOnly` is tricky. The `FuncValueConverter` defined above only supports forward conversion. For full TwoWay, use a class-based converter. Implementation:

Replace `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Common/DateTimeOffsetConverters.cs` content with:
```csharp
using System.Globalization;
using Avalonia.Data.Converters;

namespace ConstructionFinance.Desktop.Common;

public class DateOnlyConverter : IValueConverter
{
    public static readonly DateOnlyConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateOnly d) return new DateTimeOffset(d.Year, d.Month, d.Day, 0, 0, 0, TimeSpan.Zero);
        if (value is DateOnly?) return null;
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTimeOffset dto) return DateOnly.FromDateTime(dto.DateTime);
        return DateOnly.FromDateTime(DateTime.Today);
    }
}

public class NullableDateOnlyConverter : IValueConverter
{
    public static readonly NullableDateOnlyConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateOnly d) return new DateTimeOffset(d.Year, d.Month, d.Day, 0, 0, 0, TimeSpan.Zero);
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTimeOffset dto) return (DateOnly?)DateOnly.FromDateTime(dto.DateTime);
        return null;
    }
}
```

Update the XAML to use `{x:Static common:DateOnlyConverter.Instance}` instead.

- [ ] **Step 5.5: Create `DecisionEditDialog.axaml.cs`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Decisions/DecisionEditDialog.axaml.cs`:
```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop.Views.Decisions;

public partial class DecisionEditDialog : UserControl
{
    public DecisionEditDialog() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 5.6: Create `DecisionDetailView.axaml`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Decisions/DecisionDetailView.axaml`:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Decisions"
             x:Class="ConstructionFinance.Desktop.Views.Decisions.DecisionDetailView"
             x:DataType="vm:DecisionDetailViewModel">
    <DockPanel>
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Spacing="12" Margin="0,0,0,12">
            <Button Content="← Quay lại" Command="{Binding GoBackCommand}"/>
            <TextBlock FontSize="18" FontWeight="SemiBold" VerticalAlignment="Center"
                       Text="{Binding Decision.DecisionCode, StringFormat='Quyết định {0}'}"/>
        </StackPanel>
        <Grid RowDefinitions="Auto,*" RowSpacing="16">
            <Border Grid.Row="0" Background="#F9F9F9" Padding="16" CornerRadius="4">
                <Grid ColumnDefinitions="180,*" RowDefinitions="Auto,Auto,Auto,Auto,Auto" RowSpacing="6">
                    <TextBlock Grid.Row="0" Grid.Column="0" Text="Trích yếu:" FontWeight="SemiBold"/>
                    <TextBlock Grid.Row="0" Grid.Column="1" Text="{Binding Decision.DecisionTitle}" TextWrapping="Wrap"/>

                    <TextBlock Grid.Row="1" Grid.Column="0" Text="Ngày ban hành:" FontWeight="SemiBold"/>
                    <TextBlock Grid.Row="1" Grid.Column="1" Text="{Binding Decision.DecisionDate}"/>

                    <TextBlock Grid.Row="2" Grid.Column="0" Text="Cơ quan:" FontWeight="SemiBold"/>
                    <TextBlock Grid.Row="2" Grid.Column="1" Text="{Binding Decision.IssuingAuthority}"/>

                    <TextBlock Grid.Row="3" Grid.Column="0" Text="Tổng giá trị duyệt:" FontWeight="SemiBold"/>
                    <TextBlock Grid.Row="3" Grid.Column="1" Text="{Binding Decision.TotalApprovedAmount, StringFormat=#\\,##0}"/>

                    <TextBlock Grid.Row="4" Grid.Column="0" Text="Ghi chú:" FontWeight="SemiBold"/>
                    <TextBlock Grid.Row="4" Grid.Column="1" Text="{Binding Decision.Notes}" TextWrapping="Wrap"/>
                </Grid>
            </Border>
            <Border Grid.Row="1" BorderBrush="#DDDDDD" BorderThickness="1" CornerRadius="4">
                <DockPanel>
                    <TextBlock DockPanel.Dock="Top" Text="Hợp đồng thuộc Quyết định này" FontWeight="SemiBold" Margin="12,8" FontSize="14"/>
                    <ContentControl Content="{Binding Contracts}" Margin="8"/>
                </DockPanel>
            </Border>
        </Grid>
    </DockPanel>
</UserControl>
```

- [ ] **Step 5.7: Create `DecisionDetailView.axaml.cs`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Decisions/DecisionDetailView.axaml.cs`:
```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop.Views.Decisions;

public partial class DecisionDetailView : UserControl
{
    public DecisionDetailView() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 5.8: Build**

Run: `dotnet build src/ConstructionFinance.Desktop/`
Expected: 0 errors.

- [ ] **Step 5.9: Commit**

```bash
git add . && git commit -m "feat(desktop): Decision list/edit/detail XAML views + DateOnly converter"
```

---

## Task 6: `ContractListViewModel` — TDD (replace stub)

`ContractListViewModel` is filtered by `DecisionId` (set by parent `DecisionDetailViewModel`). Otherwise same pattern as Vendor list.

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Contracts/ContractListViewModel.cs` (replace stub)
- Create stub: `src/ConstructionFinance.Desktop/ViewModels/Contracts/ContractEditDialogViewModel.cs` (full impl Task 7)
- Create stub: `src/ConstructionFinance.Desktop/ViewModels/Contracts/ContractDetailViewModel.cs` (full impl Task 8)
- Test: `tests/ConstructionFinance.Desktop.Tests/Contracts/ContractListViewModelTests.cs`

- [ ] **Step 6.1: Write failing tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Desktop.Tests/Contracts/ContractListViewModelTests.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Contracts;

public class ContractListViewModelTests
{
    private static ContractDto C(int id, int decisionId, string code) =>
        new(id, decisionId, null, code, "Tên HĐ", new DateOnly(2026, 1, 1), null, 1_000_000m, null);

    private static (ContractListViewModel vm, Mock<IContractService> svc, FakeDialogService dlg, FakeNavigationService nav) Create(int decisionId, IEnumerable<ContractDto>? initial = null)
    {
        var svc = new Mock<IContractService>(MockBehavior.Strict);
        svc.Setup(s => s.GetByDecisionAsync(decisionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((initial ?? Array.Empty<ContractDto>()).ToList());
        var dlg = new FakeDialogService();
        var nav = new FakeNavigationService();
        var sp = new ServiceCollection().BuildServiceProvider();
        var vm = new ContractListViewModel(svc.Object, dlg, nav, sp);
        vm.SetDecisionId(decisionId);
        return (vm, svc, dlg, nav);
    }

    [Fact]
    public async Task LoadAsync_uses_GetByDecisionAsync()
    {
        var (vm, svc, _, _) = Create(decisionId: 7, new[] { C(1, 7, "HD-1"), C(2, 7, "HD-2") });
        await vm.LoadAsync();
        vm.Contracts.Count.ShouldBe(2);
        svc.Verify(s => s.GetByDecisionAsync(7, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddCommand_creates_contract_under_current_decision()
    {
        var (vm, svc, dlg, _) = Create(decisionId: 7);
        svc.Setup(s => s.CreateAsync(It.Is<CreateContractDto>(d => d.ApprovalDecisionId == 7), It.IsAny<CancellationToken>()))
            .ReturnsAsync(C(99, 7, "NEW"));
        dlg.DialogResult = true;
        await vm.LoadAsync();

        await vm.AddCommand.ExecuteAsync(null);

        svc.Verify(s => s.CreateAsync(It.Is<CreateContractDto>(d => d.ApprovalDecisionId == 7), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCommand_calls_service_and_removes_from_list()
    {
        var (vm, svc, dlg, _) = Create(decisionId: 7, new[] { C(1, 7, "HD") });
        svc.Setup(s => s.DeleteAsync(1, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        dlg.ConfirmResult = true;
        await vm.LoadAsync();
        vm.SelectedContract = vm.Contracts[0];

        await vm.DeleteCommand.ExecuteAsync(null);

        vm.Contracts.ShouldBeEmpty();
    }

    [Fact]
    public async Task OpenDetailCommand_navigates_to_ContractDetailViewModel()
    {
        var (vm, _, _, nav) = Create(decisionId: 7, new[] { C(1, 7, "HD") });
        await vm.LoadAsync();
        vm.SelectedContract = vm.Contracts[0];

        await vm.OpenDetailCommand.ExecuteAsync(null);

        nav.NavigationHistory.Count.ShouldBe(1);
        nav.NavigationHistory[0].ShouldBeOfType<ContractDetailViewModel>();
    }
}
```

- [ ] **Step 6.2: Create stubs**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Contracts/ContractEditDialogViewModel.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.ViewModels.Contracts;

// Stub — full implementation in Task 7.
public partial class ContractEditDialogViewModel : ViewModelBase
{
    public ContractEditDialogViewModel(ContractDto? initial, int decisionId) { _ = initial; _ = decisionId; }
    public CreateContractDto ToCreateDto() =>
        new(0, null, "stub", "stub", new DateOnly(2026, 1, 1), null, 0m, null);
    public UpdateContractDto ToUpdateDto(int id) =>
        new(id, 0, null, "stub", "stub", new DateOnly(2026, 1, 1), null, 0m, null);
}
```

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Contracts/ContractDetailViewModel.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.ViewModels.Contracts;

// Stub — full implementation in Task 8.
public partial class ContractDetailViewModel : ViewModelBase
{
    public ContractDetailViewModel(ContractDto contract) { Contract = contract; }
    public ContractDto Contract { get; }
}
```

- [ ] **Step 6.3: Implement `ContractListViewModel`**

Replace `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Contracts/ContractListViewModel.cs` with:
```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructionFinance.Desktop.ViewModels.Contracts;

public partial class ContractListViewModel : ViewModelBase
{
    private readonly IContractService _service;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;
    private readonly IServiceProvider _services;

    public ContractListViewModel(
        IContractService service,
        IDialogService dialog,
        INavigationService nav,
        IServiceProvider services)
    {
        _service = service;
        _dialog = dialog;
        _nav = nav;
        _services = services;
    }

    public int DecisionId { get; private set; }
    public ObservableCollection<ContractDto> Contracts { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenDetailCommand))]
    private ContractDto? _selectedContract;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public void SetDecisionId(int decisionId) => DecisionId = decisionId;

    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var list = await _service.GetByDecisionAsync(DecisionId);
            Contracts.Clear();
            foreach (var c in list) Contracts.Add(c);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var editVm = new ContractEditDialogViewModel(initial: null, decisionId: DecisionId);
        var ok = await _dialog.ShowDialogAsync(editVm);
        if (ok != true) return;
        try
        {
            var created = await _service.CreateAsync(editVm.ToCreateDto());
            Contracts.Add(created);
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync()
    {
        if (SelectedContract is null) return;
        var editVm = new ContractEditDialogViewModel(initial: SelectedContract, decisionId: DecisionId);
        var ok = await _dialog.ShowDialogAsync(editVm);
        if (ok != true) return;
        try
        {
            var updated = await _service.UpdateAsync(editVm.ToUpdateDto(SelectedContract.Id));
            var idx = Contracts.IndexOf(SelectedContract);
            if (idx >= 0) Contracts[idx] = updated;
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        if (SelectedContract is null) return;
        var ok = await _dialog.ConfirmAsync("Xóa Hợp đồng",
            $"Bạn có chắc muốn xóa Hợp đồng \"{SelectedContract.ContractCode}\"?");
        if (!ok) return;
        try
        {
            await _service.DeleteAsync(SelectedContract.Id);
            Contracts.Remove(SelectedContract);
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task OpenDetailAsync()
    {
        if (SelectedContract is null) return Task.CompletedTask;
        var detail = new ContractDetailViewModel(SelectedContract);
        _nav.NavigateTo(detail);
        return Task.CompletedTask;
    }

    private bool HasSelection() => SelectedContract is not null;
}
```

- [ ] **Step 6.4: Run tests — pass**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~ContractListViewModelTests"`
Expected: 4 tests pass.

- [ ] **Step 6.5: Commit**

```bash
git add . && git commit -m "feat(desktop): ContractListViewModel filtered by decisionId"
```

---

## Task 7: `ContractEditDialogViewModel` — TDD (replace stub)

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Contracts/ContractEditDialogViewModel.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Contracts/ContractEditDialogViewModelTests.cs`

- [ ] **Step 7.1: Write failing tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Desktop.Tests/Contracts/ContractEditDialogViewModelTests.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.ViewModels.Contracts;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Contracts;

public class ContractEditDialogViewModelTests
{
    [Fact]
    public void New_dialog_carries_decision_id()
    {
        var vm = new ContractEditDialogViewModel(initial: null, decisionId: 7);
        vm.IsEditMode.ShouldBeFalse();
        vm.DecisionId.ShouldBe(7);
        vm.DialogTitle.ShouldBe("Thêm Hợp đồng");
    }

    [Fact]
    public void Edit_dialog_pre_fills_from_dto()
    {
        var dto = new ContractDto(5, 7, null, "HD-001", "Tên HĐ", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 2), 1_000_000m, "Note");
        var vm = new ContractEditDialogViewModel(initial: dto, decisionId: 7);

        vm.IsEditMode.ShouldBeTrue();
        vm.DialogTitle.ShouldBe("Sửa Hợp đồng");
        vm.ContractCode.ShouldBe("HD-001");
        vm.ContractValue.ShouldBe(1_000_000m);
    }

    [Fact]
    public void ToCreateDto_includes_decisionId()
    {
        var vm = new ContractEditDialogViewModel(initial: null, decisionId: 7)
        {
            ContractCode = "HD-1",
            ContractName = "X",
            ContractDate = new DateOnly(2026, 5, 1),
            ContractValue = 1_000_000m
        };

        var dto = vm.ToCreateDto();
        dto.ApprovalDecisionId.ShouldBe(7);
        dto.ContractCode.ShouldBe("HD-1");
    }

    [Fact]
    public void Validation_required_fields()
    {
        var vm = new ContractEditDialogViewModel(initial: null, decisionId: 7);
        vm.IsValid.ShouldBeFalse();
        vm.ContractCodeError.ShouldNotBeNullOrEmpty();
        vm.ContractNameError.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void ContractValue_negative_invalid()
    {
        var vm = new ContractEditDialogViewModel(initial: null, decisionId: 7)
        {
            ContractCode = "X", ContractName = "Y", ContractValue = -1
        };
        vm.ContractValueError.ShouldNotBeNullOrEmpty();
    }
}
```

- [ ] **Step 7.2: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~ContractEditDialogViewModelTests"`
Expected: Multiple failures.

- [ ] **Step 7.3: Replace stub with full implementation**

Replace `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Contracts/ContractEditDialogViewModel.cs` with:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.ViewModels.Contracts;

public partial class ContractEditDialogViewModel : ViewModelBase, IHasDialogTitle
{
    public bool IsEditMode { get; }
    public int DecisionId { get; }
    public string DialogTitle => IsEditMode ? "Sửa Hợp đồng" : "Thêm Hợp đồng";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContractCodeError), nameof(IsValid))]
    private string _contractCode = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContractNameError), nameof(IsValid))]
    private string _contractName = "";

    [ObservableProperty] private DateOnly _contractDate = DateOnly.FromDateTime(DateTime.Today);
    [ObservableProperty] private DateOnly? _signingDate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContractValueError), nameof(IsValid))]
    private decimal _contractValue;

    [ObservableProperty] private int? _contractorVendorId;
    [ObservableProperty] private string? _notes;

    public ContractEditDialogViewModel(ContractDto? initial, int decisionId)
    {
        DecisionId = decisionId;
        IsEditMode = initial is not null;
        if (initial is not null)
        {
            ContractCode = initial.ContractCode;
            ContractName = initial.ContractName;
            ContractDate = initial.ContractDate;
            SigningDate = initial.SigningDate;
            ContractValue = initial.ContractValue;
            ContractorVendorId = initial.ContractorVendorId;
            Notes = initial.Notes;
        }
    }

    public string? ContractCodeError =>
        string.IsNullOrWhiteSpace(ContractCode) ? "Mã Hợp đồng là bắt buộc." : null;

    public string? ContractNameError =>
        string.IsNullOrWhiteSpace(ContractName) ? "Tên Hợp đồng là bắt buộc." : null;

    public string? ContractValueError =>
        ContractValue < 0 ? "Giá trị Hợp đồng không thể âm." : null;

    public bool IsValid =>
        ContractCodeError is null && ContractNameError is null && ContractValueError is null;

    public CreateContractDto ToCreateDto() =>
        new(DecisionId, ContractorVendorId, ContractCode, ContractName, ContractDate, SigningDate, ContractValue, Notes);

    public UpdateContractDto ToUpdateDto(int id) =>
        new(id, DecisionId, ContractorVendorId, ContractCode, ContractName, ContractDate, SigningDate, ContractValue, Notes);
}
```

- [ ] **Step 7.4: Verify + commit**

Run:
```bash
dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~ContractEditDialogViewModelTests"
git add . && git commit -m "feat(desktop): ContractEditDialogViewModel with validation"
```
Expected: 5 tests pass.

---

## Task 8: `ContractDetailViewModel` — TDD (replace stub)

Nested `ProjectListViewModel(contractId)`. Same pattern as DecisionDetailViewModel.

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Contracts/ContractDetailViewModel.cs`
- Create stub: `src/ConstructionFinance.Desktop/ViewModels/Projects/ProjectListViewModel.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Contracts/ContractDetailViewModelTests.cs`

- [ ] **Step 8.1: Write failing tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Desktop.Tests/Contracts/ContractDetailViewModelTests.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Contracts;
using ConstructionFinance.Desktop.ViewModels.Projects;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Contracts;

public class ContractDetailViewModelTests
{
    private static ContractDto C(int id) =>
        new(id, 7, null, "HD-1", "Tên HĐ", new DateOnly(2026, 4, 1), null, 1_000_000m, null);

    [Fact]
    public void Exposes_Contract_and_Projects_child_VM()
    {
        var sp = BuildServiceProvider();
        var nav = new FakeNavigationService();

        var vm = new ContractDetailViewModel(C(5), nav, sp);

        vm.Contract.Id.ShouldBe(5);
        vm.Projects.ShouldNotBeNull();
        vm.Projects.ContractId.ShouldBe(5);
    }

    [Fact]
    public async Task GoBackCommand_invokes_GoBack()
    {
        var sp = BuildServiceProvider();
        var nav = new FakeNavigationService();
        nav.NavigateTo("PREV");
        var vm = new ContractDetailViewModel(C(5), nav, sp);
        nav.NavigateTo(vm);

        await vm.GoBackCommand.ExecuteAsync(null);

        nav.CurrentContent.ShouldBe("PREV");
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var sc = new ServiceCollection();
        var pSvc = new Mock<IProjectService>(MockBehavior.Loose);
        pSvc.Setup(s => s.GetByContractAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProjectDto>());
        sc.AddSingleton(pSvc.Object);
        sc.AddSingleton<ConstructionFinance.Desktop.Services.IDialogService>(new FakeDialogService());
        sc.AddSingleton<ConstructionFinance.Desktop.Services.INavigationService>(new FakeNavigationService());
        sc.AddTransient<ProjectListViewModel>();
        return sc.BuildServiceProvider();
    }
}
```

- [ ] **Step 8.2: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~ContractDetailViewModelTests"`
Expected: `ProjectListViewModel` not found.

- [ ] **Step 8.3: Create stub `ProjectListViewModel`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Projects/ProjectListViewModel.cs`:
```csharp
using System.Collections.ObjectModel;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.ViewModels.Projects;

// Stub — full implementation in Task 9.
public partial class ProjectListViewModel : ViewModelBase
{
    public ProjectListViewModel(IProjectService service, IDialogService dialog, INavigationService nav, IServiceProvider services)
    {
        _ = service; _ = dialog; _ = nav; _ = services;
    }

    public int ContractId { get; set; }
    public ObservableCollection<ProjectDto> Projects { get; } = new();

    public void SetContractId(int contractId) => ContractId = contractId;
    public Task LoadAsync() => Task.CompletedTask;
}
```

- [ ] **Step 8.4: Implement `ContractDetailViewModel`**

Replace `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Contracts/ContractDetailViewModel.cs` with:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.ViewModels.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructionFinance.Desktop.ViewModels.Contracts;

public partial class ContractDetailViewModel : ViewModelBase
{
    private readonly INavigationService _nav;

    public ContractDetailViewModel(ContractDto contract, INavigationService nav, IServiceProvider services)
    {
        _nav = nav;
        Contract = contract;
        Projects = services.GetRequiredService<ProjectListViewModel>();
        Projects.SetContractId(contract.Id);
    }

    public ContractDto Contract { get; }
    public ProjectListViewModel Projects { get; }

    public Task InitializeAsync() => Projects.LoadAsync();

    [RelayCommand]
    private void GoBack() => _nav.GoBack();
}
```

- [ ] **Step 8.5: Update `ContractListViewModel.OpenDetailAsync` to use new signature**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Contracts/ContractListViewModel.cs`. Replace `OpenDetailAsync`:
```csharp
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task OpenDetailAsync()
    {
        if (SelectedContract is null) return Task.CompletedTask;
        var detail = new ContractDetailViewModel(SelectedContract, _nav, _services);
        _nav.NavigateTo(detail);
        return detail.InitializeAsync();
    }
```

- [ ] **Step 8.6: Run tests**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/`
Expected: All Decisions + Contracts tests pass.

- [ ] **Step 8.7: Commit**

```bash
git add . && git commit -m "feat(desktop): ContractDetailViewModel with nested ProjectListViewModel"
```

---

## Task 9: `ProjectListViewModel` — TDD (replace stub)

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Projects/ProjectListViewModel.cs`
- Create stub: `src/ConstructionFinance.Desktop/ViewModels/Projects/ProjectEditDialogViewModel.cs`
- Create stub: `src/ConstructionFinance.Desktop/ViewModels/Projects/ProjectDetailViewModel.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Projects/ProjectListViewModelTests.cs`

- [ ] **Step 9.1: Write failing tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Desktop.Tests/Projects/ProjectListViewModelTests.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Projects;
using ConstructionFinance.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Projects;

public class ProjectListViewModelTests
{
    private static ProjectDto P(int id, int contractId, string code) =>
        new(id, contractId, code, "Nhà máy X", null, null, null, null, ProjectStatus.Planning, 1_000_000m, 0m, null);

    private static (ProjectListViewModel vm, Mock<IProjectService> svc, FakeDialogService dlg, FakeNavigationService nav) Create(int contractId, IEnumerable<ProjectDto>? initial = null)
    {
        var svc = new Mock<IProjectService>(MockBehavior.Strict);
        svc.Setup(s => s.GetByContractAsync(contractId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((initial ?? Array.Empty<ProjectDto>()).ToList());
        var dlg = new FakeDialogService();
        var nav = new FakeNavigationService();
        var sp = new ServiceCollection().BuildServiceProvider();
        var vm = new ProjectListViewModel(svc.Object, dlg, nav, sp);
        vm.SetContractId(contractId);
        return (vm, svc, dlg, nav);
    }

    [Fact]
    public async Task LoadAsync_uses_GetByContractAsync()
    {
        var (vm, svc, _, _) = Create(7, new[] { P(1, 7, "DA-1") });
        await vm.LoadAsync();
        vm.Projects.Count.ShouldBe(1);
        svc.Verify(s => s.GetByContractAsync(7, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddCommand_creates_project_under_current_contract()
    {
        var (vm, svc, dlg, _) = Create(7);
        svc.Setup(s => s.CreateAsync(It.Is<CreateProjectDto>(d => d.ContractId == 7), It.IsAny<CancellationToken>()))
            .ReturnsAsync(P(99, 7, "NEW"));
        dlg.DialogResult = true;
        await vm.LoadAsync();

        await vm.AddCommand.ExecuteAsync(null);

        svc.Verify(s => s.CreateAsync(It.Is<CreateProjectDto>(d => d.ContractId == 7), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCommand_calls_service_when_confirmed()
    {
        var (vm, svc, dlg, _) = Create(7, new[] { P(1, 7, "DA-1") });
        svc.Setup(s => s.DeleteAsync(1, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        dlg.ConfirmResult = true;
        await vm.LoadAsync();
        vm.SelectedProject = vm.Projects[0];

        await vm.DeleteCommand.ExecuteAsync(null);

        vm.Projects.ShouldBeEmpty();
    }

    [Fact]
    public async Task OpenDetailCommand_navigates_to_ProjectDetailViewModel()
    {
        var (vm, _, _, nav) = Create(7, new[] { P(1, 7, "DA-1") });
        await vm.LoadAsync();
        vm.SelectedProject = vm.Projects[0];

        await vm.OpenDetailCommand.ExecuteAsync(null);

        nav.NavigationHistory[0].ShouldBeOfType<ProjectDetailViewModel>();
    }
}
```

- [ ] **Step 9.2: Create stubs**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Projects/ProjectEditDialogViewModel.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.ViewModels.Projects;

// Stub — full implementation in Task 10.
public partial class ProjectEditDialogViewModel : ViewModelBase
{
    public ProjectEditDialogViewModel(ProjectDto? initial, int contractId) { _ = initial; _ = contractId; }
    public CreateProjectDto ToCreateDto() =>
        new(0, "stub", "stub", null, null, null, null, 0m, null);
    public UpdateProjectDto ToUpdateDto(int id) =>
        new(id, 0, "stub", "stub", null, null, null, null, 0m, null);
}
```

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Projects/ProjectDetailViewModel.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.ViewModels.Projects;

// Stub — full implementation in Task 11.
public partial class ProjectDetailViewModel : ViewModelBase
{
    public ProjectDetailViewModel(ProjectDto project) { Project = project; }
    public ProjectDto Project { get; }
}
```

- [ ] **Step 9.3: Implement `ProjectListViewModel`**

Replace `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Projects/ProjectListViewModel.cs` with:
```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructionFinance.Desktop.ViewModels.Projects;

public partial class ProjectListViewModel : ViewModelBase
{
    private readonly IProjectService _service;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;
    private readonly IServiceProvider _services;

    public ProjectListViewModel(IProjectService service, IDialogService dialog, INavigationService nav, IServiceProvider services)
    {
        _service = service;
        _dialog = dialog;
        _nav = nav;
        _services = services;
    }

    public int ContractId { get; private set; }
    public ObservableCollection<ProjectDto> Projects { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenDetailCommand))]
    private ProjectDto? _selectedProject;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public void SetContractId(int contractId) => ContractId = contractId;

    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var list = await _service.GetByContractAsync(ContractId);
            Projects.Clear();
            foreach (var p in list) Projects.Add(p);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var editVm = new ProjectEditDialogViewModel(initial: null, contractId: ContractId);
        var ok = await _dialog.ShowDialogAsync(editVm);
        if (ok != true) return;
        try
        {
            var created = await _service.CreateAsync(editVm.ToCreateDto());
            Projects.Add(created);
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync()
    {
        if (SelectedProject is null) return;
        var editVm = new ProjectEditDialogViewModel(initial: SelectedProject, contractId: ContractId);
        var ok = await _dialog.ShowDialogAsync(editVm);
        if (ok != true) return;
        try
        {
            var updated = await _service.UpdateAsync(editVm.ToUpdateDto(SelectedProject.Id));
            var idx = Projects.IndexOf(SelectedProject);
            if (idx >= 0) Projects[idx] = updated;
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        if (SelectedProject is null) return;
        var ok = await _dialog.ConfirmAsync("Xóa Dự án",
            $"Bạn có chắc muốn xóa Dự án \"{SelectedProject.ProjectCode}\"?");
        if (!ok) return;
        try
        {
            await _service.DeleteAsync(SelectedProject.Id);
            Projects.Remove(SelectedProject);
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task OpenDetailAsync()
    {
        if (SelectedProject is null) return Task.CompletedTask;
        var detail = new ProjectDetailViewModel(SelectedProject);
        _nav.NavigateTo(detail);
        return Task.CompletedTask;
    }

    private bool HasSelection() => SelectedProject is not null;
}
```

- [ ] **Step 9.4: Verify + commit**

Run:
```bash
dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~ProjectListViewModelTests"
git add . && git commit -m "feat(desktop): ProjectListViewModel filtered by contractId"
```
Expected: 4 tests pass.

---

## Task 10: `ProjectEditDialogViewModel` — TDD (replace stub)

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Projects/ProjectEditDialogViewModel.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Projects/ProjectEditDialogViewModelTests.cs`

- [ ] **Step 10.1: Write failing tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Desktop.Tests/Projects/ProjectEditDialogViewModelTests.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.ViewModels.Projects;
using ConstructionFinance.Domain.Enums;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Projects;

public class ProjectEditDialogViewModelTests
{
    [Fact]
    public void New_dialog_for_contract_has_contractId_set()
    {
        var vm = new ProjectEditDialogViewModel(initial: null, contractId: 7);
        vm.IsEditMode.ShouldBeFalse();
        vm.ContractId.ShouldBe(7);
        vm.DialogTitle.ShouldBe("Thêm Dự án");
    }

    [Fact]
    public void Edit_dialog_pre_fills_from_dto()
    {
        var dto = new ProjectDto(5, 7, "DA-1", "Nhà máy X", "Dân dụng", "Hà Nội",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
            ProjectStatus.InProgress, 5_000_000m, 1_000_000m, "Note");
        var vm = new ProjectEditDialogViewModel(initial: dto, contractId: 7);

        vm.IsEditMode.ShouldBeTrue();
        vm.ProjectCode.ShouldBe("DA-1");
        vm.TotalBudget.ShouldBe(5_000_000m);
    }

    [Fact]
    public void ToCreateDto_includes_contractId()
    {
        var vm = new ProjectEditDialogViewModel(initial: null, contractId: 7)
        {
            ProjectCode = "DA-1",
            ProjectName = "X",
            TotalBudget = 1_000_000m
        };
        var dto = vm.ToCreateDto();
        dto.ContractId.ShouldBe(7);
    }

    [Fact]
    public void Validation_required_fields_when_empty()
    {
        var vm = new ProjectEditDialogViewModel(initial: null, contractId: 7);
        vm.IsValid.ShouldBeFalse();
        vm.ProjectCodeError.ShouldNotBeNullOrEmpty();
        vm.ProjectNameError.ShouldNotBeNullOrEmpty();
    }
}
```

- [ ] **Step 10.2: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~ProjectEditDialogViewModelTests"`
Expected: Multiple failures.

- [ ] **Step 10.3: Replace stub**

Replace `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Projects/ProjectEditDialogViewModel.cs` with:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.ViewModels.Projects;

public partial class ProjectEditDialogViewModel : ViewModelBase, IHasDialogTitle
{
    public bool IsEditMode { get; }
    public int ContractId { get; }
    public string DialogTitle => IsEditMode ? "Sửa Dự án" : "Thêm Dự án";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProjectCodeError), nameof(IsValid))]
    private string _projectCode = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProjectNameError), nameof(IsValid))]
    private string _projectName = "";

    [ObservableProperty] private string? _projectType;
    [ObservableProperty] private string? _location;
    [ObservableProperty] private DateOnly? _startDate;
    [ObservableProperty] private DateOnly? _endDate;
    [ObservableProperty] private decimal _totalBudget;
    [ObservableProperty] private string? _notes;

    public ProjectEditDialogViewModel(ProjectDto? initial, int contractId)
    {
        ContractId = contractId;
        IsEditMode = initial is not null;
        if (initial is not null)
        {
            ProjectCode = initial.ProjectCode;
            ProjectName = initial.ProjectName;
            ProjectType = initial.ProjectType;
            Location = initial.Location;
            StartDate = initial.StartDate;
            EndDate = initial.EndDate;
            TotalBudget = initial.TotalBudget;
            Notes = initial.Notes;
        }
    }

    public string? ProjectCodeError =>
        string.IsNullOrWhiteSpace(ProjectCode) ? "Mã Dự án là bắt buộc." : null;

    public string? ProjectNameError =>
        string.IsNullOrWhiteSpace(ProjectName) ? "Tên Dự án là bắt buộc." : null;

    public bool IsValid => ProjectCodeError is null && ProjectNameError is null;

    public CreateProjectDto ToCreateDto() =>
        new(ContractId, ProjectCode, ProjectName, ProjectType, Location, StartDate, EndDate, TotalBudget, Notes);

    public UpdateProjectDto ToUpdateDto(int id) =>
        new(id, ContractId, ProjectCode, ProjectName, ProjectType, Location, StartDate, EndDate, TotalBudget, Notes);
}
```

- [ ] **Step 10.4: Verify + commit**

Run:
```bash
dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~ProjectEditDialogViewModelTests"
git add . && git commit -m "feat(desktop): ProjectEditDialogViewModel with validation"
```

---

## Task 11: `ProjectDetailViewModel` — TDD (replace stub, status update support)

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Projects/ProjectDetailViewModel.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Projects/ProjectDetailViewModelTests.cs`

- [ ] **Step 11.1: Write failing tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Desktop.Tests/Projects/ProjectDetailViewModelTests.cs`:
```csharp
using ConstructionFinance.Application.Common.Exceptions;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Projects;
using ConstructionFinance.Domain.Enums;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Projects;

public class ProjectDetailViewModelTests
{
    private static ProjectDto P(ProjectStatus status = ProjectStatus.Planning) =>
        new(5, 7, "DA-1", "Nhà máy X", null, null, null, null, status, 1_000_000m, 0m, null);

    private static (ProjectDetailViewModel vm, Mock<IProjectService> svc, FakeDialogService dlg, FakeNavigationService nav) Create(ProjectStatus status = ProjectStatus.Planning)
    {
        var svc = new Mock<IProjectService>(MockBehavior.Strict);
        var dlg = new FakeDialogService();
        var nav = new FakeNavigationService();
        var vm = new ProjectDetailViewModel(P(status), svc.Object, dlg, nav);
        return (vm, svc, dlg, nav);
    }

    [Fact]
    public void Exposes_Project_and_AvailableStatuses()
    {
        var (vm, _, _, _) = Create();
        vm.Project.ShouldNotBeNull();
        vm.AvailableStatuses.ShouldContain(ProjectStatus.InProgress);
    }

    [Fact]
    public async Task ChangeStatusAsync_calls_service_and_updates_Project()
    {
        var (vm, svc, _, _) = Create(ProjectStatus.Planning);
        svc.Setup(s => s.UpdateStatusAsync(5, ProjectStatus.InProgress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(P(ProjectStatus.InProgress));

        await vm.ChangeStatusAsync(ProjectStatus.InProgress);

        vm.Project.Status.ShouldBe(ProjectStatus.InProgress);
    }

    [Fact]
    public async Task ChangeStatusAsync_shows_error_on_BusinessRuleException()
    {
        var (vm, svc, dlg, _) = Create(ProjectStatus.Planning);
        svc.Setup(s => s.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<ProjectStatus>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessRuleException("Không hợp lệ"));

        await vm.ChangeStatusAsync(ProjectStatus.Completed);

        dlg.ShownMessages.ShouldContain(m => m.Contains("Không hợp lệ"));
        vm.Project.Status.ShouldBe(ProjectStatus.Planning);
    }

    [Fact]
    public async Task GoBackCommand_invokes_GoBack()
    {
        var (vm, _, _, nav) = Create();
        nav.NavigateTo("PREV");
        nav.NavigateTo(vm);

        await vm.GoBackCommand.ExecuteAsync(null);

        nav.CurrentContent.ShouldBe("PREV");
    }
}
```

- [ ] **Step 11.2: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~ProjectDetailViewModelTests"`
Expected: Multiple failures.

- [ ] **Step 11.3: Replace stub**

Replace `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Projects/ProjectDetailViewModel.cs` with:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Desktop.ViewModels.Projects;

public partial class ProjectDetailViewModel : ViewModelBase
{
    private readonly IProjectService _service;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;

    public ProjectDetailViewModel(ProjectDto project, IProjectService service, IDialogService dialog, INavigationService nav)
    {
        _service = service;
        _dialog = dialog;
        _nav = nav;
        Project = project;
    }

    [ObservableProperty] private ProjectDto _project;

    public IReadOnlyList<ProjectStatus> AvailableStatuses { get; } =
        Enum.GetValues<ProjectStatus>().ToList();

    public async Task ChangeStatusAsync(ProjectStatus newStatus)
    {
        try
        {
            Project = await _service.UpdateStatusAsync(Project.Id, newStatus);
        }
        catch (Exception ex)
        {
            await _dialog.ShowMessageAsync("Lỗi", ex.Message);
        }
    }

    [RelayCommand]
    private void GoBack() => _nav.GoBack();
}
```

- [ ] **Step 11.4: Update `ProjectListViewModel.OpenDetailAsync` to resolve `IProjectService` and pass into `ProjectDetailViewModel`**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Projects/ProjectListViewModel.cs`. Replace `OpenDetailAsync`:
```csharp
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task OpenDetailAsync()
    {
        if (SelectedProject is null) return Task.CompletedTask;
        var detail = new ProjectDetailViewModel(SelectedProject, _service, _dialog, _nav);
        _nav.NavigateTo(detail);
        return Task.CompletedTask;
    }
```

- [ ] **Step 11.5: Verify + commit**

Run:
```bash
dotnet test tests/ConstructionFinance.Desktop.Tests/
git add . && git commit -m "feat(desktop): ProjectDetailViewModel with status state transitions + back nav"
```
Expected: All Desktop tests pass.

---

## Task 12: XAML views for Contract + Project + edit dialogs

**Files:**
- Create: `src/ConstructionFinance.Desktop/Views/Contracts/ContractListView.axaml(.cs)` (used inside DecisionDetailView)
- Create: `src/ConstructionFinance.Desktop/Views/Contracts/ContractEditDialog.axaml(.cs)`
- Create: `src/ConstructionFinance.Desktop/Views/Contracts/ContractDetailView.axaml(.cs)`
- Create: `src/ConstructionFinance.Desktop/Views/Projects/ProjectListView.axaml(.cs)` (used inside ContractDetailView)
- Create: `src/ConstructionFinance.Desktop/Views/Projects/ProjectEditDialog.axaml(.cs)`
- Create: `src/ConstructionFinance.Desktop/Views/Projects/ProjectDetailView.axaml(.cs)`

These views follow the exact same patterns as the Vendor views from Plan 03 and Decision views from Task 5. Each list view = DataGrid + toolbar; each edit dialog = form Grid with validation TextBlocks; each detail view = top info card + GoBack button + nested child list view.

- [ ] **Step 12.1: Create `ContractListView.axaml`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Contracts/ContractListView.axaml`:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Contracts"
             x:Class="ConstructionFinance.Desktop.Views.Contracts.ContractListView"
             x:DataType="vm:ContractListViewModel">
    <DockPanel>
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Spacing="8" Margin="0,0,0,8">
            <Button Content="+ Thêm" Command="{Binding AddCommand}"/>
            <Button Content="Sửa" Command="{Binding EditCommand}"/>
            <Button Content="Xóa" Command="{Binding DeleteCommand}"/>
            <Button Content="Xem chi tiết →" Command="{Binding OpenDetailCommand}"/>
            <TextBlock Text="{Binding ErrorMessage}" Foreground="Red" VerticalAlignment="Center"/>
        </StackPanel>
        <DataGrid ItemsSource="{Binding Contracts}"
                  SelectedItem="{Binding SelectedContract}"
                  IsReadOnly="True"
                  GridLinesVisibility="Horizontal"
                  HeadersVisibility="Column">
            <DataGrid.Columns>
                <DataGridTextColumn Header="Mã HĐ" Binding="{Binding ContractCode}" Width="150"/>
                <DataGridTextColumn Header="Tên HĐ" Binding="{Binding ContractName}" Width="*"/>
                <DataGridTextColumn Header="Ngày HĐ" Binding="{Binding ContractDate}" Width="110"/>
                <DataGridTextColumn Header="Giá trị" Binding="{Binding ContractValue, StringFormat=#\\,##0}" Width="160"/>
            </DataGrid.Columns>
        </DataGrid>
    </DockPanel>
</UserControl>
```

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Contracts/ContractListView.axaml.cs`:
```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop.Views.Contracts;

public partial class ContractListView : UserControl
{
    public ContractListView() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 12.2: Create `ContractEditDialog.axaml`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Contracts/ContractEditDialog.axaml`:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Contracts"
             xmlns:common="using:ConstructionFinance.Desktop.Common"
             x:Class="ConstructionFinance.Desktop.Views.Contracts.ContractEditDialog"
             x:DataType="vm:ContractEditDialogViewModel"
             MinWidth="520">
    <Grid ColumnDefinitions="170,*" RowDefinitions="Auto,Auto,Auto,Auto,Auto,Auto" RowSpacing="8" ColumnSpacing="8">
        <TextBlock Grid.Row="0" Grid.Column="0" Text="Mã Hợp đồng *" VerticalAlignment="Center"/>
        <StackPanel Grid.Row="0" Grid.Column="1">
            <TextBox Text="{Binding ContractCode}"/>
            <TextBlock Text="{Binding ContractCodeError}" Foreground="Red" FontSize="11"
                       IsVisible="{Binding ContractCodeError, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"/>
        </StackPanel>

        <TextBlock Grid.Row="1" Grid.Column="0" Text="Tên Hợp đồng *" VerticalAlignment="Center"/>
        <StackPanel Grid.Row="1" Grid.Column="1">
            <TextBox Text="{Binding ContractName}" AcceptsReturn="True" Height="50" TextWrapping="Wrap"/>
            <TextBlock Text="{Binding ContractNameError}" Foreground="Red" FontSize="11"
                       IsVisible="{Binding ContractNameError, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"/>
        </StackPanel>

        <TextBlock Grid.Row="2" Grid.Column="0" Text="Ngày HĐ" VerticalAlignment="Center"/>
        <DatePicker Grid.Row="2" Grid.Column="1"
                    SelectedDate="{Binding ContractDate, Mode=TwoWay, Converter={x:Static common:DateOnlyConverter.Instance}}"/>

        <TextBlock Grid.Row="3" Grid.Column="0" Text="Ngày ký" VerticalAlignment="Center"/>
        <DatePicker Grid.Row="3" Grid.Column="1"
                    SelectedDate="{Binding SigningDate, Mode=TwoWay, Converter={x:Static common:NullableDateOnlyConverter.Instance}}"/>

        <TextBlock Grid.Row="4" Grid.Column="0" Text="Giá trị HĐ *" VerticalAlignment="Center"/>
        <StackPanel Grid.Row="4" Grid.Column="1">
            <NumericUpDown Value="{Binding ContractValue}" Minimum="0" FormatString="#,##0"/>
            <TextBlock Text="{Binding ContractValueError}" Foreground="Red" FontSize="11"
                       IsVisible="{Binding ContractValueError, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"/>
        </StackPanel>

        <TextBlock Grid.Row="5" Grid.Column="0" Text="Ghi chú" VerticalAlignment="Top"/>
        <TextBox Grid.Row="5" Grid.Column="1" Text="{Binding Notes}" AcceptsReturn="True" Height="80" TextWrapping="Wrap"/>
    </Grid>
</UserControl>
```

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Contracts/ContractEditDialog.axaml.cs`:
```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop.Views.Contracts;

public partial class ContractEditDialog : UserControl
{
    public ContractEditDialog() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 12.3: Create `ContractDetailView.axaml`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Contracts/ContractDetailView.axaml`:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Contracts"
             x:Class="ConstructionFinance.Desktop.Views.Contracts.ContractDetailView"
             x:DataType="vm:ContractDetailViewModel">
    <DockPanel>
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Spacing="12" Margin="0,0,0,12">
            <Button Content="← Quay lại" Command="{Binding GoBackCommand}"/>
            <TextBlock FontSize="18" FontWeight="SemiBold" VerticalAlignment="Center"
                       Text="{Binding Contract.ContractCode, StringFormat='Hợp đồng {0}'}"/>
        </StackPanel>
        <Grid RowDefinitions="Auto,*" RowSpacing="16">
            <Border Grid.Row="0" Background="#F9F9F9" Padding="16" CornerRadius="4">
                <Grid ColumnDefinitions="170,*" RowDefinitions="Auto,Auto,Auto,Auto" RowSpacing="6">
                    <TextBlock Grid.Row="0" Grid.Column="0" Text="Tên HĐ:" FontWeight="SemiBold"/>
                    <TextBlock Grid.Row="0" Grid.Column="1" Text="{Binding Contract.ContractName}" TextWrapping="Wrap"/>

                    <TextBlock Grid.Row="1" Grid.Column="0" Text="Ngày HĐ:" FontWeight="SemiBold"/>
                    <TextBlock Grid.Row="1" Grid.Column="1" Text="{Binding Contract.ContractDate}"/>

                    <TextBlock Grid.Row="2" Grid.Column="0" Text="Giá trị HĐ:" FontWeight="SemiBold"/>
                    <TextBlock Grid.Row="2" Grid.Column="1" Text="{Binding Contract.ContractValue, StringFormat=#\\,##0}"/>

                    <TextBlock Grid.Row="3" Grid.Column="0" Text="Ghi chú:" FontWeight="SemiBold"/>
                    <TextBlock Grid.Row="3" Grid.Column="1" Text="{Binding Contract.Notes}" TextWrapping="Wrap"/>
                </Grid>
            </Border>
            <Border Grid.Row="1" BorderBrush="#DDDDDD" BorderThickness="1" CornerRadius="4">
                <DockPanel>
                    <TextBlock DockPanel.Dock="Top" Text="Dự án thuộc Hợp đồng này" FontWeight="SemiBold" Margin="12,8" FontSize="14"/>
                    <ContentControl Content="{Binding Projects}" Margin="8"/>
                </DockPanel>
            </Border>
        </Grid>
    </DockPanel>
</UserControl>
```

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Contracts/ContractDetailView.axaml.cs`:
```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop.Views.Contracts;

public partial class ContractDetailView : UserControl
{
    public ContractDetailView() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 12.4: Create `ProjectListView.axaml`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Projects/ProjectListView.axaml`:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Projects"
             x:Class="ConstructionFinance.Desktop.Views.Projects.ProjectListView"
             x:DataType="vm:ProjectListViewModel">
    <DockPanel>
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Spacing="8" Margin="0,0,0,8">
            <Button Content="+ Thêm" Command="{Binding AddCommand}"/>
            <Button Content="Sửa" Command="{Binding EditCommand}"/>
            <Button Content="Xóa" Command="{Binding DeleteCommand}"/>
            <Button Content="Xem chi tiết →" Command="{Binding OpenDetailCommand}"/>
            <TextBlock Text="{Binding ErrorMessage}" Foreground="Red" VerticalAlignment="Center"/>
        </StackPanel>
        <DataGrid ItemsSource="{Binding Projects}"
                  SelectedItem="{Binding SelectedProject}"
                  IsReadOnly="True"
                  GridLinesVisibility="Horizontal"
                  HeadersVisibility="Column">
            <DataGrid.Columns>
                <DataGridTextColumn Header="Mã DA" Binding="{Binding ProjectCode}" Width="120"/>
                <DataGridTextColumn Header="Tên Dự án" Binding="{Binding ProjectName}" Width="*"/>
                <DataGridTextColumn Header="Trạng thái" Binding="{Binding Status}" Width="120"/>
                <DataGridTextColumn Header="Dự toán" Binding="{Binding TotalBudget, StringFormat=#\\,##0}" Width="160"/>
            </DataGrid.Columns>
        </DataGrid>
    </DockPanel>
</UserControl>
```

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Projects/ProjectListView.axaml.cs`:
```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop.Views.Projects;

public partial class ProjectListView : UserControl
{
    public ProjectListView() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 12.5: Create `ProjectEditDialog.axaml`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Projects/ProjectEditDialog.axaml`:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Projects"
             xmlns:common="using:ConstructionFinance.Desktop.Common"
             x:Class="ConstructionFinance.Desktop.Views.Projects.ProjectEditDialog"
             x:DataType="vm:ProjectEditDialogViewModel"
             MinWidth="540">
    <Grid ColumnDefinitions="170,*" RowDefinitions="Auto,Auto,Auto,Auto,Auto,Auto,Auto" RowSpacing="8" ColumnSpacing="8">
        <TextBlock Grid.Row="0" Grid.Column="0" Text="Mã Dự án *" VerticalAlignment="Center"/>
        <StackPanel Grid.Row="0" Grid.Column="1">
            <TextBox Text="{Binding ProjectCode}"/>
            <TextBlock Text="{Binding ProjectCodeError}" Foreground="Red" FontSize="11"
                       IsVisible="{Binding ProjectCodeError, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"/>
        </StackPanel>

        <TextBlock Grid.Row="1" Grid.Column="0" Text="Tên Dự án *" VerticalAlignment="Center"/>
        <StackPanel Grid.Row="1" Grid.Column="1">
            <TextBox Text="{Binding ProjectName}"/>
            <TextBlock Text="{Binding ProjectNameError}" Foreground="Red" FontSize="11"
                       IsVisible="{Binding ProjectNameError, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"/>
        </StackPanel>

        <TextBlock Grid.Row="2" Grid.Column="0" Text="Loại CT" VerticalAlignment="Center"/>
        <TextBox Grid.Row="2" Grid.Column="1" Text="{Binding ProjectType}"/>

        <TextBlock Grid.Row="3" Grid.Column="0" Text="Địa điểm" VerticalAlignment="Center"/>
        <TextBox Grid.Row="3" Grid.Column="1" Text="{Binding Location}"/>

        <TextBlock Grid.Row="4" Grid.Column="0" Text="Ngày bắt đầu" VerticalAlignment="Center"/>
        <DatePicker Grid.Row="4" Grid.Column="1"
                    SelectedDate="{Binding StartDate, Mode=TwoWay, Converter={x:Static common:NullableDateOnlyConverter.Instance}}"/>

        <TextBlock Grid.Row="5" Grid.Column="0" Text="Ngày kết thúc" VerticalAlignment="Center"/>
        <DatePicker Grid.Row="5" Grid.Column="1"
                    SelectedDate="{Binding EndDate, Mode=TwoWay, Converter={x:Static common:NullableDateOnlyConverter.Instance}}"/>

        <TextBlock Grid.Row="6" Grid.Column="0" Text="Dự toán (VND)" VerticalAlignment="Center"/>
        <NumericUpDown Grid.Row="6" Grid.Column="1" Value="{Binding TotalBudget}" Minimum="0" FormatString="#,##0"/>
    </Grid>
</UserControl>
```

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Projects/ProjectEditDialog.axaml.cs`:
```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop.Views.Projects;

public partial class ProjectEditDialog : UserControl
{
    public ProjectEditDialog() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 12.6: Create `ProjectDetailView.axaml` (TabControl with 4 tabs; only Info functional)**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Projects/ProjectDetailView.axaml`:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Projects"
             x:Class="ConstructionFinance.Desktop.Views.Projects.ProjectDetailView"
             x:DataType="vm:ProjectDetailViewModel">
    <DockPanel>
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Spacing="12" Margin="0,0,0,12">
            <Button Content="← Quay lại" Command="{Binding GoBackCommand}"/>
            <TextBlock FontSize="18" FontWeight="SemiBold" VerticalAlignment="Center"
                       Text="{Binding Project.ProjectCode, StringFormat='Dự án {0}'}"/>
        </StackPanel>
        <TabControl>
            <TabItem Header="Thông tin">
                <Grid ColumnDefinitions="200,*" RowDefinitions="Auto,Auto,Auto,Auto,Auto,Auto" RowSpacing="8" Margin="16">
                    <TextBlock Grid.Row="0" Grid.Column="0" Text="Tên Dự án:" FontWeight="SemiBold"/>
                    <TextBlock Grid.Row="0" Grid.Column="1" Text="{Binding Project.ProjectName}" TextWrapping="Wrap"/>

                    <TextBlock Grid.Row="1" Grid.Column="0" Text="Loại CT:" FontWeight="SemiBold"/>
                    <TextBlock Grid.Row="1" Grid.Column="1" Text="{Binding Project.ProjectType}"/>

                    <TextBlock Grid.Row="2" Grid.Column="0" Text="Địa điểm:" FontWeight="SemiBold"/>
                    <TextBlock Grid.Row="2" Grid.Column="1" Text="{Binding Project.Location}"/>

                    <TextBlock Grid.Row="3" Grid.Column="0" Text="Thời gian:" FontWeight="SemiBold"/>
                    <StackPanel Grid.Row="3" Grid.Column="1" Orientation="Horizontal" Spacing="8">
                        <TextBlock Text="{Binding Project.StartDate}"/>
                        <TextBlock Text="→"/>
                        <TextBlock Text="{Binding Project.EndDate}"/>
                    </StackPanel>

                    <TextBlock Grid.Row="4" Grid.Column="0" Text="Trạng thái:" FontWeight="SemiBold"/>
                    <ComboBox Grid.Row="4" Grid.Column="1" ItemsSource="{Binding AvailableStatuses}"
                              SelectedItem="{Binding Project.Status, Mode=OneWay}"
                              SelectionChanged="OnStatusChanged" Width="200"/>

                    <TextBlock Grid.Row="5" Grid.Column="0" Text="Tổng dự toán:" FontWeight="SemiBold"/>
                    <TextBlock Grid.Row="5" Grid.Column="1" Text="{Binding Project.TotalBudget, StringFormat=#\\,##0}"/>
                </Grid>
            </TabItem>
            <TabItem Header="Chi phí">
                <TextBlock Margin="16" Foreground="Gray"
                           Text="Cây đầu mục chi phí — sẽ được triển khai ở Plan 05."/>
            </TabItem>
            <TabItem Header="Báo cáo">
                <TextBlock Margin="16" Foreground="Gray"
                           Text="Báo cáo dự án — sẽ được triển khai ở plan sau."/>
            </TabItem>
            <TabItem Header="Tài liệu">
                <TextBlock Margin="16" Foreground="Gray"
                           Text="Tài liệu đính kèm — sẽ được triển khai ở plan sau."/>
            </TabItem>
        </TabControl>
    </DockPanel>
</UserControl>
```

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Projects/ProjectDetailView.axaml.cs`:
```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ConstructionFinance.Desktop.ViewModels.Projects;
using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Desktop.Views.Projects;

public partial class ProjectDetailView : UserControl
{
    public ProjectDetailView() => AvaloniaXamlLoader.Load(this);

    private async void OnStatusChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ProjectDetailViewModel vm && sender is ComboBox cb
            && cb.SelectedItem is ProjectStatus newStatus
            && newStatus != vm.Project.Status)
        {
            await vm.ChangeStatusAsync(newStatus);
        }
    }
}
```

- [ ] **Step 12.7: Build**

Run: `dotnet build src/ConstructionFinance.Desktop/`
Expected: 0 errors. (Warnings about unused code-behind methods OK.)

- [ ] **Step 12.8: Commit**

```bash
git add . && git commit -m "feat(desktop): Contract + Project XAML views (list, edit dialog, detail with TabControl)"
```

---

## Task 13: Wire menu "Quyết định" into MainWindow + register all VMs in DI

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/Views/MainWindow.axaml.cs`
- Modify: `src/ConstructionFinance.Desktop/Common/HostBuilder.cs`

- [ ] **Step 13.1: Register new VMs in HostBuilder**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Common/HostBuilder.cs`. Inside `ConfigureServices`, after the existing `VendorListViewModel` line, add:
```csharp
                services.AddTransient<ViewModels.Decisions.DecisionListViewModel>();
                services.AddTransient<ViewModels.Contracts.ContractListViewModel>();
                services.AddTransient<ViewModels.Projects.ProjectListViewModel>();
                services.AddSingleton<IServiceProvider>(sp => sp);
```

(The last line allows `IServiceProvider` itself to be injected — needed by detail VMs that resolve children.)

- [ ] **Step 13.2: Update `MainWindowViewModel` to consume `INavigationService`**

Replace `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs` with:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.ViewModels.Decisions;
using ConstructionFinance.Desktop.ViewModels.Vendors;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructionFinance.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;
    private readonly INavigationService _nav;

    public MainWindowViewModel(IServiceProvider services, INavigationService nav)
    {
        _services = services;
        _nav = nav;
        _nav.CurrentContentChanged += (_, content) => CurrentContent = content;
    }

    [ObservableProperty] private string _title = "Quản lý Tài chính Công trình";
    [ObservableProperty] private string _statusText = $"DB: {AppPaths.DatabaseFile} | Sẵn sàng";
    [ObservableProperty] private object? _currentContent;

    public async Task NavigateToAsync(string tag)
    {
        switch (tag)
        {
            case "Decisions":
                var dvm = _services.GetRequiredService<DecisionListViewModel>();
                _nav.Reset();
                _nav.NavigateTo(dvm);
                await dvm.LoadAsync();
                break;
            case "Vendors":
                var vvm = _services.GetRequiredService<VendorListViewModel>();
                _nav.Reset();
                _nav.NavigateTo(vvm);
                await vvm.LoadAsync();
                break;
            default:
                _nav.Reset();
                _nav.NavigateTo(new PlaceholderViewModel { Tag = tag });
                break;
        }
    }
}

public class PlaceholderViewModel : ViewModelBase
{
    public string Tag { get; set; } = "";
}
```

- [ ] **Step 13.3: Build**

Run: `dotnet build src/ConstructionFinance.Desktop/`
Expected: 0 errors.

- [ ] **Step 13.4: Run full test suite**

Run: `dotnet test --configuration Release`
Expected: All tests across 4 projects pass (Domain 37 + Data 38 + Application 39 + Desktop ~40+).

- [ ] **Step 13.5: Manual smoke test (Linux/desktop)**

Run:
```bash
dotnet run --project src/ConstructionFinance.Desktop/ &
APP_PID=$!
sleep 4
kill $APP_PID 2>/dev/null || true
```
Manual checklist (on a desktop machine):
- [ ] App opens, sidebar visible
- [ ] Click "Quyết định" → empty list with `+ Thêm` button
- [ ] Add a decision → appears in list
- [ ] Click "Xem chi tiết →" → DecisionDetailView opens, sees contracts panel
- [ ] Add a contract → appears in nested list
- [ ] Click "Xem chi tiết →" on a contract → ContractDetailView opens with projects panel
- [ ] Add a project → appears in nested list
- [ ] Click on the project → ProjectDetailView with TabControl opens
- [ ] Change project status from Planning → InProgress via dropdown → service called, UI reflects new status
- [ ] Try Completed from Planning → error dialog shows "Không hợp lệ"
- [ ] Click "← Quay lại" navigates back step by step

- [ ] **Step 13.6: Commit**

```bash
git add . && git commit -m "feat(desktop): wire Decisions menu + nested drill-down navigation"
```

---

## Final verification

- [ ] **Step F.1: Clean build + test**

Run:
```bash
cd /home/hieu-nguyen/management/
dotnet clean
dotnet build --configuration Release
dotnet test --configuration Release --verbosity minimal
```
Expected: 0 errors, all tests pass.

- [ ] **Step F.2: Confirm git log shows ~13 commits for Plan 04**

Run: `git log --oneline | head -16`

- [ ] **Step F.3: Verify nested CRUD works end-to-end (manual)**

Per Step 13.5 checklist.

---

## Deliverables

After this plan:

1. ✅ `INavigationService` with back-stack (5 tests) — drives the master-detail drill-down across the app
2. ✅ Decision CRUD UI: list + edit dialog + detail (info + nested contracts) — 14 tests across 3 VMs
3. ✅ Contract CRUD UI: list (within decision detail) + edit + detail (info + nested projects) — 11 tests
4. ✅ Project CRUD UI: list (within contract detail) + edit + detail with TabControl + status state machine — 12 tests
5. ✅ `DateOnlyConverter`/`NullableDateOnlyConverter` for `DateOnly` ↔ `DateTimeOffset` binding
6. ✅ Full drill-down: Decision list → Decision detail → Contract list → Contract detail → Project list → Project detail
7. ✅ Project status update via dropdown with `BusinessRuleException` handling
8. ✅ "Quyết định" menu in MainWindow wired end-to-end

**What's NOT in this plan** (Plan 05+):
- "Chi phí" tab in ProjectDetailView is a placeholder — Plan 05 implements TreeDataGrid cost tree
- "Báo cáo" + "Tài liệu" tabs are placeholders — later plans
- Payments UI (nested within cost item) — Plan 05
- Breadcrumb navigation (only Back button for now)
- Sidebar "Cây Dự án" tree view — later
- Excel/PDF export — Plan 06
- Backup/Restore + Installer — Plan 06
