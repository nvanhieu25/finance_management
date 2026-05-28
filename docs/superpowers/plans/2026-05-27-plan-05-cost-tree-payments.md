# Plan 05: Cost Tree (M6) + Payments (M7)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Triển khai module phức tạp nhất của app — cây Đầu mục Chi phí (M6) và Đợt thanh toán (M7). Thay tab "Chi phí" placeholder trong `ProjectDetailView` (Plan 04) bằng `CostTreeView` thực sự: TreeDataGrid hiển thị cây nhiều cấp, inline edit số lượng/đơn giá, real-time rollup, các lệnh Add/Delete/Indent/Outdent. Khi user chọn 1 leaf, có nút "💰 Thanh toán" mở `PaymentListDialog` để quản lý các đợt thanh toán (CRUD + summary % hoàn thành). Kết thúc plan: user có thể nhập đầy đủ chi phí cho 1 dự án + ghi nhận thanh toán.

**Architecture:**
- `CostTreeViewModel` chủ trì 1 dự án, bind cây qua `HierarchicalTreeDataGridSource<CostTreeNode>` (TreeDataGrid official).
- `CostTreeNode` là wrapper observable của `CostItemDto`, thêm `ObservableCollection<CostTreeNode> Children` + flags hỗ trợ UI.
- Mọi thay đổi đi qua `ICostService` (Plan 02), VM tự refresh tree từ service sau mỗi operation.
- Rollup gọi `ICostService.RecalculateRollupAsync(projectId)` sau insert/update/delete leaf (debounce 300ms).
- Payment quản lý qua `PaymentListDialogViewModel` mở từ tree toolbar khi có leaf chọn — pattern dialog giống Vendor/Decision edit.
- Read-only mode: `IsReadOnly` bool bind theo `Project.Status == Completed`; mọi command disabled.

**Tech Stack:** Bổ sung `Avalonia.Controls.TreeDataGrid` 11.0.10 (đã có trong `Directory.Packages.props` từ Plan 03 nhưng chưa dùng). Phần còn lại đã có (Avalonia, CommunityToolkit.Mvvm, EF Core 8, xUnit, Shouldly, Moq).

**Reference:** [Design doc](../specs/2026-05-19-construction-finance-app-design.md) sections 4.6 (Cây chi phí), 4.7 (Đợt thanh toán). Builds on [Plan 02 services](2026-05-20-plan-02-application-services.md) (`ICostService`, `IPaymentService`) and [Plan 04 ProjectDetailView TabControl](2026-05-26-plan-04-decision-contract-project-ui.md).

**Out of scope** (v1.1+):
- Excel-like copy/paste from clipboard
- Drag-fill / fill down
- Undo/Redo stack
- Import từ file Excel
- Bid package management UI (free-text only for now)
- Vendor picker UI for cost items (free `VendorId` set via separate edit dialog — defer to later)

---

## Key Decisions (flag — phản hồi nếu không đồng ý)

| # | Quyết định | Lý do |
|---|---|---|
| D1 | Dùng `HierarchicalTreeDataGridSource<CostTreeNode>` (Avalonia.Controls.TreeDataGrid) | Plan 03 đã cài package; là lựa chọn duy nhất MIT-free cho tree+grid trên Avalonia. Đã được spec design doc 3.2 chỉ định. |
| D2 | `CostTreeNode` là wrapper observable, KHÔNG sửa `CostItemDto` (record bất biến) | Plan 02 DTOs là record để pure data transfer; tree binding cần `ObservableProperty` + `ObservableCollection<Children>` |
| D3 | Sau MỌI command (add/delete/move/edit), VM **reload toàn bộ tree** từ `ICostService.GetProjectTreeAsync` rồi rebuild `CostTreeNode` cây | Đơn giản, đảm bảo consistency tuyệt đối với DB. Nếu sau này thấy chậm có thể optimize. |
| D4 | Rollup auto-trigger: sau insert/update/delete, debounce 300ms rồi gọi `ICostService.RecalculateRollupAsync` rồi reload | Tránh "chớp" UI khi user nhập liên tục; vẫn cảm giác "real-time" cho mắt thường |
| D5 | Inline editing cell: chỉ cho phép edit trên `ItemName`, `Quantity`, `UnitPrice`, `EstimatedAmount` (khi không phải leaf). Cột khác readonly | Đúng nghiệp vụ — code/path/level/parent là system-managed |
| D6 | Payment UI: nút "💰 Thanh toán" trên toolbar tree, ENABLE khi chọn 1 leaf. Click mở `PaymentListDialog` (1 cửa sổ modal) | Theo design doc 4.7 — dialog đầy đủ với danh sách + summary |
| D7 | Read-only mode khi `Project.Status == Completed`: ẩn toàn bộ toolbar, TreeDataGrid `IsCellEditingEnabled = false` | Đúng spec 4.6 R-CI-4 |
| D8 | Soft-delete cascade subtree: dùng `ICostService.DeleteNodeAsync` (Plan 02 đã làm) — VM chỉ confirm 2 lần với user nếu subtree có nhiều node | Bảo vệ user khỏi xóa nhầm cả nhánh lớn |
| D9 | Indent (Alt+→): node trở thành con của previous sibling. Outdent (Alt+←): node trở thành sibling của parent. Cả hai gọi `ICostService.MoveNodeAsync` | Đúng nghiệp vụ Excel-like. Plan 02 đã có MoveNodeAsync. |

---

## File Structure

```
src/ConstructionFinance.Application/
├── Services/Interfaces/
│   └── ICostUnitTypeService.cs              (NEW — read-only lookup)
├── Services/Implementations/
│   └── CostUnitTypeService.cs               (NEW)
├── DTOs/
│   └── CostUnitTypeDtos.cs                  (NEW — CostUnitTypeDto)
├── Mapping/
│   └── CostUnitTypeMappings.cs              (NEW)
└── DependencyInjection.cs                    (MODIFIED — register CostUnitTypeService)

src/ConstructionFinance.Desktop/
├── Common/HostBuilder.cs                     (MODIFIED — register Cost/Payment VMs)
├── ViewModels/CostItems/
│   ├── CostTreeNode.cs                       (NEW — observable wrapper)
│   ├── CostTreeViewModel.cs                  (NEW — main tree VM)
│   └── CostItemEditDialogViewModel.cs        (NEW — edit non-leaf node name etc.)
├── ViewModels/Payments/
│   ├── PaymentListDialogViewModel.cs         (NEW — list + summary)
│   └── PaymentEditDialogViewModel.cs         (NEW — single payment form)
├── ViewModels/Projects/
│   └── ProjectDetailViewModel.cs             (MODIFIED — host CostTreeViewModel for Chi phí tab)
├── Views/CostItems/
│   ├── CostTreeView.axaml(.cs)               (NEW — main tree view with TreeDataGrid)
│   └── CostItemEditDialog.axaml(.cs)         (NEW)
├── Views/Payments/
│   ├── PaymentListDialog.axaml(.cs)          (NEW)
│   └── PaymentEditDialog.axaml(.cs)          (NEW)
└── Views/Projects/
    └── ProjectDetailView.axaml               (MODIFIED — Chi phí tab content)

tests/ConstructionFinance.Application.Tests/
└── CostUnitTypeServiceTests.cs               (NEW — small read-only service test)

tests/ConstructionFinance.Desktop.Tests/
├── CostItems/
│   ├── CostTreeNodeTests.cs                  (NEW — wrapper behavior)
│   ├── CostTreeViewModelTests.cs             (NEW — load, commands, rollup)
│   └── CostItemEditDialogViewModelTests.cs   (NEW)
└── Payments/
    ├── PaymentListDialogViewModelTests.cs    (NEW)
    └── PaymentEditDialogViewModelTests.cs    (NEW)
```

---

## Pre-flight

- [ ] **Step 0.1: Verify Plan 04 committed and green**

Run from `/home/hieu-nguyen/management/`:
```bash
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
git status --short
dotnet build --configuration Release
dotnet test --configuration Release
```
Expected: Working tree clean (commit Plan 04 first if not). Build 0 errors. 168 tests pass.

- [ ] **Step 0.2: Confirm `Avalonia.Controls.TreeDataGrid` package version in `Directory.Packages.props`**

Run: `grep TreeDataGrid Directory.Packages.props`
Expected: `<PackageVersion Include="Avalonia.Controls.TreeDataGrid" Version="11.0.10" />`. If missing, add it.

---

## Task 1: CostUnitTypeService (read-only lookup)

Cây chi phí cần dropdown đơn vị tính → cần 1 service đơn giản trả về danh sách `CostUnitType`. Plan 02 đã có `CostUnitType` entity + seed data nhưng chưa có service.

**Files:**
- Create: `src/ConstructionFinance.Application/DTOs/CostUnitTypeDtos.cs`
- Create: `src/ConstructionFinance.Application/Mapping/CostUnitTypeMappings.cs`
- Create: `src/ConstructionFinance.Application/Services/Interfaces/ICostUnitTypeService.cs`
- Create: `src/ConstructionFinance.Application/Services/Implementations/CostUnitTypeService.cs`
- Modify: `src/ConstructionFinance.Application/DependencyInjection.cs`
- Test: `tests/ConstructionFinance.Application.Tests/CostUnitTypeServiceTests.cs`

- [ ] **Step 1.1: Failing test**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Application.Tests/CostUnitTypeServiceTests.cs`:
```csharp
using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Data.Seed;
using ConstructionFinance.Domain.Entities;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class CostUnitTypeServiceTests
{
    [Fact]
    public async Task GetAllAsync_returns_seeded_units_sorted_by_code()
    {
        using var h = new ServiceTestHarness();
        CostUnitTypeSeeder.Seed(h.Context);
        ICostUnitTypeService svc = new CostUnitTypeService(new Repository<CostUnitType>(h.Context));

        var list = await svc.GetAllAsync();

        list.Count.ShouldBeGreaterThanOrEqualTo(10);
        list.ShouldContain(u => u.Code == "m2");
        list.ShouldContain(u => u.Code == "m3");
        list[0].Code.CompareTo(list[^1].Code).ShouldBeLessThanOrEqualTo(0); // sorted
    }
}
```

- [ ] **Step 1.2: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~CostUnitTypeServiceTests"`
Expected: types missing.

- [ ] **Step 1.3: DTO + Mapping**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/DTOs/CostUnitTypeDtos.cs`:
```csharp
namespace ConstructionFinance.Application.DTOs;

public record CostUnitTypeDto(int Id, string Code, string Name, string? Description);
```

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Mapping/CostUnitTypeMappings.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Application.Mapping;

public static class CostUnitTypeMappings
{
    public static CostUnitTypeDto ToDto(this CostUnitType e) =>
        new(e.Id, e.Code, e.Name, e.Description);
}
```

- [ ] **Step 1.4: ICostUnitTypeService + impl**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Services/Interfaces/ICostUnitTypeService.cs`:
```csharp
using ConstructionFinance.Application.DTOs;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface ICostUnitTypeService
{
    Task<IReadOnlyList<CostUnitTypeDto>> GetAllAsync(CancellationToken ct = default);
}
```

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Services/Implementations/CostUnitTypeService.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Mapping;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Application.Services.Implementations;

public class CostUnitTypeService : ICostUnitTypeService
{
    private readonly IRepository<CostUnitType> _units;

    public CostUnitTypeService(IRepository<CostUnitType> units) => _units = units;

    public async Task<IReadOnlyList<CostUnitTypeDto>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _units.ListAsync(ct);
        return list.OrderBy(u => u.Code).Select(u => u.ToDto()).ToList();
    }
}
```

- [ ] **Step 1.5: Register in DI**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Application/DependencyInjection.cs`. Inside `AddApplicationServices`, add to the Services group:
```csharp
        services.AddScoped<ICostUnitTypeService, CostUnitTypeService>();
```

- [ ] **Step 1.6: Verify + commit**

```bash
dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~CostUnitTypeServiceTests"
git add . && git commit -m "feat(app): CostUnitTypeService (read-only lookup) + DTO/mapping"
```
Expected: test passes.

---

## Task 2: `CostTreeNode` — observable wrapper

`CostItemDto` is a record (immutable). For TreeDataGrid binding we need an observable class with `ObservableCollection<CostTreeNode> Children` + property change notifications.

**Files:**
- Create: `src/ConstructionFinance.Desktop/ViewModels/CostItems/CostTreeNode.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/CostItems/CostTreeNodeTests.cs`

- [ ] **Step 2.1: Failing test**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Desktop.Tests/CostItems/CostTreeNodeTests.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.ViewModels.CostItems;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.CostItems;

public class CostTreeNodeTests
{
    private static CostItemDto Dto(int id, int? parentId, string name, bool isLeaf, decimal qty = 0, decimal unitPrice = 0, decimal estimated = 0) =>
        new(id, 1, parentId, null, null, null, name, parentId is null ? 0 : 1, "/", 0, isLeaf, qty, unitPrice, estimated, null, null);

    [Fact]
    public void Constructor_copies_dto_fields()
    {
        var dto = Dto(5, null, "Chi phí xây dựng", false, estimated: 1_000_000m);
        var node = new CostTreeNode(dto);

        node.Id.ShouldBe(5);
        node.ItemName.ShouldBe("Chi phí xây dựng");
        node.IsLeaf.ShouldBeFalse();
        node.EstimatedAmount.ShouldBe(1_000_000m);
        node.Children.ShouldNotBeNull();
        node.Children.Count.ShouldBe(0);
    }

    [Fact]
    public void ItemName_setter_raises_PropertyChanged()
    {
        var node = new CostTreeNode(Dto(1, null, "old", true));
        string? changed = null;
        node.PropertyChanged += (_, e) => changed = e.PropertyName;

        node.ItemName = "new";

        changed.ShouldBe(nameof(CostTreeNode.ItemName));
    }

    [Fact]
    public void Quantity_and_UnitPrice_setters_raise_PropertyChanged()
    {
        var node = new CostTreeNode(Dto(1, null, "X", true));
        var changes = new List<string>();
        node.PropertyChanged += (_, e) => changes.Add(e.PropertyName ?? "");

        node.Quantity = 10m;
        node.UnitPrice = 1000m;

        changes.ShouldContain(nameof(CostTreeNode.Quantity));
        changes.ShouldContain(nameof(CostTreeNode.UnitPrice));
    }
}
```

- [ ] **Step 2.2: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~CostTreeNodeTests"`

- [ ] **Step 2.3: Implement**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/CostItems/CostTreeNode.cs`:
```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.DTOs;

namespace ConstructionFinance.Desktop.ViewModels.CostItems;

public partial class CostTreeNode : ObservableObject
{
    public int Id { get; }
    public int ProjectId { get; }
    public int? ParentId { get; }
    public int Level { get; }
    public string Path { get; }

    [ObservableProperty] private string? _itemCode;
    [ObservableProperty] private string _itemName;
    [ObservableProperty] private bool _isLeaf;
    [ObservableProperty] private int? _unitTypeId;
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private decimal _unitPrice;
    [ObservableProperty] private decimal _estimatedAmount;
    [ObservableProperty] private string? _bidPackage;
    [ObservableProperty] private string? _notes;
    [ObservableProperty] private int? _vendorId;
    [ObservableProperty] private bool _isExpanded = true;

    public ObservableCollection<CostTreeNode> Children { get; } = new();

    public CostTreeNode(CostItemDto dto)
    {
        Id = dto.Id;
        ProjectId = dto.ProjectId;
        ParentId = dto.ParentId;
        Level = dto.Level;
        Path = dto.Path;
        _itemCode = dto.ItemCode;
        _itemName = dto.ItemName;
        _isLeaf = dto.IsLeaf;
        _unitTypeId = dto.UnitTypeId;
        _quantity = dto.Quantity;
        _unitPrice = dto.UnitPrice;
        _estimatedAmount = dto.EstimatedAmount;
        _bidPackage = dto.BidPackage;
        _notes = dto.Notes;
        _vendorId = dto.VendorId;
    }
}
```

- [ ] **Step 2.4: Verify + commit**

```bash
dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~CostTreeNodeTests"
git add . && git commit -m "feat(desktop): CostTreeNode observable wrapper for tree binding"
```

---

## Task 3: `CostTreeViewModel` — load + flat tree → hierarchical

**Files:**
- Create: `src/ConstructionFinance.Desktop/ViewModels/CostItems/CostTreeViewModel.cs` (initial — load only; commands added in later tasks)
- Test: `tests/ConstructionFinance.Desktop.Tests/CostItems/CostTreeViewModelTests.cs`

- [ ] **Step 3.1: Failing test — load**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Desktop.Tests/CostItems/CostTreeViewModelTests.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.CostItems;
using ConstructionFinance.Domain.Enums;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.CostItems;

public class CostTreeViewModelTests
{
    private static CostItemDto Dto(int id, int? parentId, string name, int level, string path, bool isLeaf = false, decimal estimated = 0) =>
        new(id, 1, parentId, null, null, null, name, level, path, 0, isLeaf, 0, 0, estimated, null, null);

    private static (CostTreeViewModel vm, Mock<ICostService> cost, Mock<IPaymentService> pay, Mock<ICostUnitTypeService> units, FakeDialogService dlg)
        Create(IEnumerable<CostItemDto>? initial = null)
    {
        var cost = new Mock<ICostService>(MockBehavior.Strict);
        cost.Setup(s => s.GetProjectTreeAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((initial ?? Array.Empty<CostItemDto>()).ToList());
        var pay = new Mock<IPaymentService>(MockBehavior.Loose);
        var units = new Mock<ICostUnitTypeService>(MockBehavior.Loose);
        units.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<CostUnitTypeDto>());
        var dlg = new FakeDialogService();
        var vm = new CostTreeViewModel(cost.Object, pay.Object, units.Object, dlg);
        vm.SetProject(projectId: 7, status: ProjectStatus.InProgress);
        return (vm, cost, pay, units, dlg);
    }

    [Fact]
    public async Task LoadAsync_builds_hierarchy_from_flat_list()
    {
        // Tree shape:
        //   root1 (id=1, leaf=false)
        //     child1a (id=2, parent=1, leaf=true)
        //     child1b (id=3, parent=1, leaf=false)
        //       grandchild (id=4, parent=3, leaf=true)
        //   root2 (id=5, leaf=true)
        var data = new[]
        {
            Dto(1, null, "Xây dựng", 0, "/1/", isLeaf: false, estimated: 10_000_000m),
            Dto(2, 1,    "Móng",     1, "/1/2/", isLeaf: true,  estimated: 5_000_000m),
            Dto(3, 1,    "Cột",      1, "/1/3/", isLeaf: false),
            Dto(4, 3,    "Cột tầng 1", 2, "/1/3/4/", isLeaf: true, estimated: 2_000_000m),
            Dto(5, null, "Thiết bị", 0, "/5/", isLeaf: true,  estimated: 8_000_000m)
        };
        var (vm, _, _, _, _) = Create(data);

        await vm.LoadAsync();

        vm.RootNodes.Count.ShouldBe(2);
        var root1 = vm.RootNodes[0];
        root1.ItemName.ShouldBe("Xây dựng");
        root1.Children.Count.ShouldBe(2);
        var child1b = root1.Children.Single(c => c.ItemName == "Cột");
        child1b.Children.Count.ShouldBe(1);
        child1b.Children[0].ItemName.ShouldBe("Cột tầng 1");
        vm.RootNodes[1].ItemName.ShouldBe("Thiết bị");
    }

    [Fact]
    public async Task LoadAsync_with_empty_returns_empty_RootNodes()
    {
        var (vm, _, _, _, _) = Create();
        await vm.LoadAsync();
        vm.RootNodes.ShouldBeEmpty();
    }

    [Fact]
    public void IsReadOnly_true_when_project_Completed()
    {
        var (vm, _, _, _, _) = Create();
        vm.SetProject(projectId: 7, status: ProjectStatus.Completed);
        vm.IsReadOnly.ShouldBeTrue();
    }

    [Fact]
    public void IsReadOnly_false_when_project_InProgress()
    {
        var (vm, _, _, _, _) = Create();
        vm.IsReadOnly.ShouldBeFalse();
    }
}
```

- [ ] **Step 3.2: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~CostTreeViewModelTests"`

- [ ] **Step 3.3: Implement (load + state only — commands come in Tasks 4-7)**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/CostItems/CostTreeViewModel.cs`:
```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Desktop.ViewModels.CostItems;

public partial class CostTreeViewModel : ViewModelBase
{
    private readonly ICostService _cost;
    private readonly IPaymentService _payment;
    private readonly ICostUnitTypeService _units;
    private readonly IDialogService _dialog;

    public CostTreeViewModel(ICostService cost, IPaymentService payment, ICostUnitTypeService units, IDialogService dialog)
    {
        _cost = cost;
        _payment = payment;
        _units = units;
        _dialog = dialog;
    }

    public int ProjectId { get; private set; }
    public ProjectStatus ProjectStatus { get; private set; } = ProjectStatus.Planning;
    public bool IsReadOnly => ProjectStatus == ProjectStatus.Completed;

    public ObservableCollection<CostTreeNode> RootNodes { get; } = new();
    public ObservableCollection<CostUnitTypeDto> Units { get; } = new();

    [ObservableProperty] private CostTreeNode? _selectedNode;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private decimal _grandTotal;

    public void SetProject(int projectId, ProjectStatus status)
    {
        ProjectId = projectId;
        ProjectStatus = status;
        OnPropertyChanged(nameof(IsReadOnly));
    }

    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var flat = await _cost.GetProjectTreeAsync(ProjectId);
            RootNodes.Clear();
            foreach (var root in BuildHierarchy(flat))
                RootNodes.Add(root);

            if (Units.Count == 0)
            {
                var units = await _units.GetAllAsync();
                foreach (var u in units) Units.Add(u);
            }

            GrandTotal = RootNodes.Sum(n => n.EstimatedAmount);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    private static List<CostTreeNode> BuildHierarchy(IEnumerable<CostItemDto> flat)
    {
        var byId = new Dictionary<int, CostTreeNode>();
        var roots = new List<CostTreeNode>();
        foreach (var dto in flat)
        {
            byId[dto.Id] = new CostTreeNode(dto);
        }
        foreach (var dto in flat)
        {
            var node = byId[dto.Id];
            if (dto.ParentId is int pid && byId.TryGetValue(pid, out var parent))
                parent.Children.Add(node);
            else
                roots.Add(node);
        }
        return roots;
    }
}
```

- [ ] **Step 3.4: Verify + commit**

```bash
dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~CostTreeViewModelTests"
git add . && git commit -m "feat(desktop): CostTreeViewModel — load + build hierarchy + read-only state"
```

---

## Task 4: CostTreeViewModel — Add commands (root, child, sibling, delete)

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/CostItems/CostTreeViewModel.cs`
- Modify: `tests/ConstructionFinance.Desktop.Tests/CostItems/CostTreeViewModelTests.cs` (extend)

- [ ] **Step 4.1: Failing tests**

Append to `CostTreeViewModelTests.cs` (inside the same class):
```csharp
    [Fact]
    public async Task AddRootCommand_calls_service_with_correct_dto()
    {
        var (vm, cost, _, _, _) = Create();
        cost.Setup(s => s.CreateNodeAsync(It.IsAny<CreateCostItemDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Dto(99, null, "Mới", 0, "/99/", isLeaf: false));
        cost.Setup(s => s.RecalculateRollupAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await vm.LoadAsync();
        await vm.AddRootCommand.ExecuteAsync(null);

        cost.Verify(s => s.CreateNodeAsync(
            It.Is<CreateCostItemDto>(d => d.ProjectId == 7 && d.ParentId == null && !d.IsLeaf),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddChildCommand_passes_selected_node_as_parent()
    {
        var (vm, cost, _, _, _) = Create(new[]
        {
            Dto(1, null, "Root", 0, "/1/", isLeaf: false, estimated: 100m)
        });
        cost.Setup(s => s.CreateNodeAsync(It.IsAny<CreateCostItemDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Dto(2, 1, "Con", 1, "/1/2/", isLeaf: true));
        cost.Setup(s => s.RecalculateRollupAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await vm.LoadAsync();
        vm.SelectedNode = vm.RootNodes[0];

        await vm.AddChildCommand.ExecuteAsync(null);

        cost.Verify(s => s.CreateNodeAsync(
            It.Is<CreateCostItemDto>(d => d.ParentId == 1 && d.IsLeaf),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddChildCommand_disabled_when_no_selection()
    {
        var (vm, _, _, _, _) = Create();
        await vm.LoadAsync();
        vm.SelectedNode = null;
        vm.AddChildCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteCommand_calls_service_when_user_confirms()
    {
        var (vm, cost, _, _, dlg) = Create(new[]
        {
            Dto(1, null, "Root", 0, "/1/", isLeaf: true)
        });
        cost.Setup(s => s.DeleteNodeAsync(1, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        cost.Setup(s => s.RecalculateRollupAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        dlg.ConfirmResult = true;

        await vm.LoadAsync();
        vm.SelectedNode = vm.RootNodes[0];

        await vm.DeleteCommand.ExecuteAsync(null);

        cost.Verify(s => s.DeleteNodeAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCommand_skipped_when_user_declines()
    {
        var (vm, cost, _, _, dlg) = Create(new[]
        {
            Dto(1, null, "Root", 0, "/1/", isLeaf: true)
        });
        dlg.ConfirmResult = false;
        await vm.LoadAsync();
        vm.SelectedNode = vm.RootNodes[0];

        await vm.DeleteCommand.ExecuteAsync(null);

        cost.Verify(s => s.DeleteNodeAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Commands_disabled_when_IsReadOnly()
    {
        var (vm, _, _, _, _) = Create();
        vm.SetProject(projectId: 7, status: ProjectStatus.Completed);
        await vm.LoadAsync();

        vm.AddRootCommand.CanExecute(null).ShouldBeFalse();
        // AddChild/Delete already gated by selection but also by IsReadOnly
        var node = new CostTreeNode(Dto(1, null, "X", 0, "/1/", isLeaf: true));
        vm.SelectedNode = node;
        vm.AddChildCommand.CanExecute(null).ShouldBeFalse();
        vm.DeleteCommand.CanExecute(null).ShouldBeFalse();
    }
```

- [ ] **Step 4.2: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~CostTreeViewModelTests"`

- [ ] **Step 4.3: Add commands**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/CostItems/CostTreeViewModel.cs`. Add these usings at top:
```csharp
using CommunityToolkit.Mvvm.Input;
```

Update `SelectedNode` property to fire CanExecute changed:
```csharp
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddChildCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddSiblingCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(IndentCommand))]
    [NotifyCanExecuteChangedFor(nameof(OutdentCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenPaymentsCommand))]
    private CostTreeNode? _selectedNode;
```

Add inside the class body, after `LoadAsync` and before `BuildHierarchy`:
```csharp
    [RelayCommand(CanExecute = nameof(CanWrite))]
    private async Task AddRootAsync()
    {
        await CreateNodeAsync(parentId: null, isLeaf: false, defaultName: "Đầu mục mới");
    }

    [RelayCommand(CanExecute = nameof(CanWriteAndHasSelection))]
    private async Task AddChildAsync()
    {
        if (SelectedNode is null) return;
        await CreateNodeAsync(parentId: SelectedNode.Id, isLeaf: true, defaultName: "Mục con");
    }

    [RelayCommand(CanExecute = nameof(CanWriteAndHasSelection))]
    private async Task AddSiblingAsync()
    {
        if (SelectedNode is null) return;
        await CreateNodeAsync(parentId: SelectedNode.ParentId, isLeaf: SelectedNode.IsLeaf, defaultName: "Mục mới");
    }

    [RelayCommand(CanExecute = nameof(CanWriteAndHasSelection))]
    private async Task DeleteAsync()
    {
        if (SelectedNode is null) return;
        var hasChildren = SelectedNode.Children.Count > 0;
        var msg = hasChildren
            ? $"Xóa \"{SelectedNode.ItemName}\" và toàn bộ {CountSubtree(SelectedNode) - 1} đầu mục con? Hành động này có thể khôi phục trong 30 ngày."
            : $"Xóa đầu mục \"{SelectedNode.ItemName}\"?";
        var ok = await _dialog.ConfirmAsync("Xóa đầu mục chi phí", msg);
        if (!ok) return;

        try
        {
            await _cost.DeleteNodeAsync(SelectedNode.Id);
            await _cost.RecalculateRollupAsync(ProjectId);
            await LoadAsync();
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    private async Task CreateNodeAsync(int? parentId, bool isLeaf, string defaultName)
    {
        try
        {
            await _cost.CreateNodeAsync(new CreateCostItemDto(
                ProjectId, parentId, UnitTypeId: null, ItemName: defaultName, IsLeaf: isLeaf,
                VendorId: null, Quantity: 0, UnitPrice: 0, EstimatedAmount: 0, BidPackage: null, Notes: null));
            await _cost.RecalculateRollupAsync(ProjectId);
            await LoadAsync();
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    private static int CountSubtree(CostTreeNode node)
    {
        var count = 1;
        foreach (var c in node.Children) count += CountSubtree(c);
        return count;
    }

    private bool CanWrite() => !IsReadOnly;
    private bool CanWriteAndHasSelection() => !IsReadOnly && SelectedNode is not null;
```

Also notify `AddRootCommand.CanExecute` when `IsReadOnly` changes — update `SetProject`:
```csharp
    public void SetProject(int projectId, ProjectStatus status)
    {
        ProjectId = projectId;
        ProjectStatus = status;
        OnPropertyChanged(nameof(IsReadOnly));
        AddRootCommand.NotifyCanExecuteChanged();
        AddChildCommand.NotifyCanExecuteChanged();
        AddSiblingCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }
```

Note: `IndentCommand`/`OutdentCommand`/`OpenPaymentsCommand` are referenced in `[NotifyCanExecuteChangedFor]` but not yet defined — they'll come in Tasks 5 and 7. The source generator will error if attributes reference non-existent commands. **Resolution: temporarily remove those 3 lines from the `[NotifyCanExecuteChangedFor]` attribute list on `_selectedNode`, and re-add them in Task 5/7 when those commands are added.** For now in Task 4 keep only `AddChildCommand`, `AddSiblingCommand`, `DeleteCommand`.

- [ ] **Step 4.4: Verify + commit**

```bash
dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~CostTreeViewModelTests"
git add . && git commit -m "feat(desktop): CostTreeViewModel — AddRoot/Child/Sibling/Delete commands"
```

---

## Task 5: CostTreeViewModel — Indent / Outdent (move)

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/CostItems/CostTreeViewModel.cs`
- Modify: `tests/ConstructionFinance.Desktop.Tests/CostItems/CostTreeViewModelTests.cs`

- [ ] **Step 5.1: Failing tests**

Append to `CostTreeViewModelTests.cs`:
```csharp
    [Fact]
    public async Task IndentCommand_moves_node_under_previous_sibling()
    {
        // root1 has children: A, B. Selecting B and Indent should move B under A.
        var data = new[]
        {
            Dto(1, null, "root", 0, "/1/", isLeaf: false),
            Dto(2, 1, "A", 1, "/1/2/", isLeaf: true),
            Dto(3, 1, "B", 1, "/1/3/", isLeaf: true)
        };
        var (vm, cost, _, _, _) = Create(data);
        cost.Setup(s => s.MoveNodeAsync(3, 2, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        cost.Setup(s => s.RecalculateRollupAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await vm.LoadAsync();
        vm.SelectedNode = vm.RootNodes[0].Children.Single(c => c.ItemName == "B");

        await vm.IndentCommand.ExecuteAsync(null);

        cost.Verify(s => s.MoveNodeAsync(3, 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IndentCommand_disabled_when_node_has_no_previous_sibling()
    {
        var data = new[]
        {
            Dto(1, null, "root", 0, "/1/", isLeaf: false),
            Dto(2, 1, "A", 1, "/1/2/", isLeaf: true) // only child
        };
        var (vm, _, _, _, _) = Create(data);
        await vm.LoadAsync();
        vm.SelectedNode = vm.RootNodes[0].Children[0];

        vm.IndentCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task OutdentCommand_moves_node_to_grandparent_as_sibling()
    {
        var data = new[]
        {
            Dto(1, null, "root", 0, "/1/", isLeaf: false),
            Dto(2, 1, "A", 1, "/1/2/", isLeaf: false),
            Dto(3, 2, "A1", 2, "/1/2/3/", isLeaf: true)
        };
        var (vm, cost, _, _, _) = Create(data);
        cost.Setup(s => s.MoveNodeAsync(3, 1, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        cost.Setup(s => s.RecalculateRollupAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await vm.LoadAsync();
        vm.SelectedNode = vm.RootNodes[0].Children[0].Children[0]; // A1

        await vm.OutdentCommand.ExecuteAsync(null);

        cost.Verify(s => s.MoveNodeAsync(3, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OutdentCommand_disabled_when_node_already_at_root_level()
    {
        var data = new[]
        {
            Dto(1, null, "root", 0, "/1/", isLeaf: true)
        };
        var (vm, _, _, _, _) = Create(data);
        await vm.LoadAsync();
        vm.SelectedNode = vm.RootNodes[0];

        vm.OutdentCommand.CanExecute(null).ShouldBeFalse();
    }
```

- [ ] **Step 5.2: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~CostTreeViewModelTests"`

- [ ] **Step 5.3: Implement Indent + Outdent**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/CostItems/CostTreeViewModel.cs`:

Add `IndentCommand` and `OutdentCommand` to the `[NotifyCanExecuteChangedFor]` attributes on `_selectedNode`. Update the attribute block to:
```csharp
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddChildCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddSiblingCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(IndentCommand))]
    [NotifyCanExecuteChangedFor(nameof(OutdentCommand))]
    private CostTreeNode? _selectedNode;
```

Add two new commands after `DeleteAsync`:
```csharp
    [RelayCommand(CanExecute = nameof(CanIndent))]
    private async Task IndentAsync()
    {
        if (SelectedNode is null) return;
        var prevSibling = FindPreviousSibling(SelectedNode);
        if (prevSibling is null) return;

        try
        {
            await _cost.MoveNodeAsync(SelectedNode.Id, prevSibling.Id);
            await _cost.RecalculateRollupAsync(ProjectId);
            await LoadAsync();
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(CanOutdent))]
    private async Task OutdentAsync()
    {
        if (SelectedNode is null) return;
        var parent = FindParent(SelectedNode);
        if (parent is null) return; // already at root
        var grandparentId = parent.ParentId; // could be null = move to root

        try
        {
            await _cost.MoveNodeAsync(SelectedNode.Id, grandparentId);
            await _cost.RecalculateRollupAsync(ProjectId);
            await LoadAsync();
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    private bool CanIndent() =>
        !IsReadOnly && SelectedNode is not null && FindPreviousSibling(SelectedNode) is not null;

    private bool CanOutdent() =>
        !IsReadOnly && SelectedNode is not null && FindParent(SelectedNode) is not null;

    private CostTreeNode? FindPreviousSibling(CostTreeNode node)
    {
        var siblings = node.ParentId is int pid
            ? FindNodeById(pid)?.Children
            : RootNodes;
        if (siblings is null) return null;
        var idx = siblings.IndexOf(node);
        return idx > 0 ? siblings[idx - 1] : null;
    }

    private CostTreeNode? FindParent(CostTreeNode node) =>
        node.ParentId is int pid ? FindNodeById(pid) : null;

    private CostTreeNode? FindNodeById(int id)
    {
        foreach (var root in RootNodes)
        {
            var found = FindRecursive(root, id);
            if (found is not null) return found;
        }
        return null;
    }

    private static CostTreeNode? FindRecursive(CostTreeNode node, int id)
    {
        if (node.Id == id) return node;
        foreach (var c in node.Children)
        {
            var found = FindRecursive(c, id);
            if (found is not null) return found;
        }
        return null;
    }
```

- [ ] **Step 5.4: Verify + commit**

```bash
dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~CostTreeViewModelTests"
git add . && git commit -m "feat(desktop): CostTreeViewModel — Indent/Outdent (move via MoveNodeAsync)"
```

---

## Task 6: CostTreeViewModel — Inline edit + debounced rollup

When user edits cell (ItemName, Quantity, UnitPrice, EstimatedAmount) inline, VM must:
1. Call `_cost.UpdateNodeAsync(...)` to persist
2. Trigger rollup (debounced 300ms — multiple rapid edits collapse to one rollup)
3. Reload tree to reflect new estimated amounts

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/CostItems/CostTreeViewModel.cs`
- Modify: `tests/ConstructionFinance.Desktop.Tests/CostItems/CostTreeViewModelTests.cs`

- [ ] **Step 6.1: Failing tests**

Append to `CostTreeViewModelTests.cs`:
```csharp
    [Fact]
    public async Task ApplyEditAsync_calls_UpdateNodeAsync_and_rollup()
    {
        var data = new[]
        {
            Dto(1, null, "X", 0, "/1/", isLeaf: true, estimated: 100m)
        };
        var (vm, cost, _, _, _) = Create(data);
        cost.Setup(s => s.UpdateNodeAsync(It.IsAny<UpdateCostItemDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Dto(1, null, "Y", 0, "/1/", isLeaf: true, estimated: 200m));
        cost.Setup(s => s.RecalculateRollupAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await vm.LoadAsync();
        var node = vm.RootNodes[0];
        node.ItemName = "Y";
        node.Quantity = 10m;
        node.UnitPrice = 20m;

        await vm.ApplyEditAsync(node);

        cost.Verify(s => s.UpdateNodeAsync(
            It.Is<UpdateCostItemDto>(d => d.Id == 1 && d.ItemName == "Y" && d.Quantity == 10m && d.UnitPrice == 20m),
            It.IsAny<CancellationToken>()), Times.Once);
        cost.Verify(s => s.RecalculateRollupAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ApplyEditAsync_does_nothing_when_IsReadOnly()
    {
        var (vm, cost, _, _, _) = Create(new[] { Dto(1, null, "X", 0, "/1/", isLeaf: true) });
        vm.SetProject(7, ProjectStatus.Completed);
        await vm.LoadAsync();
        var node = vm.RootNodes[0];
        node.ItemName = "Y";

        await vm.ApplyEditAsync(node);

        cost.Verify(s => s.UpdateNodeAsync(It.IsAny<UpdateCostItemDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }
```

- [ ] **Step 6.2: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~CostTreeViewModelTests"`

- [ ] **Step 6.3: Implement `ApplyEditAsync`**

Add to `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/CostItems/CostTreeViewModel.cs`:
```csharp
    public async Task ApplyEditAsync(CostTreeNode node)
    {
        if (IsReadOnly) return;
        try
        {
            await _cost.UpdateNodeAsync(new UpdateCostItemDto(
                Id: node.Id,
                UnitTypeId: node.UnitTypeId,
                VendorId: node.VendorId,
                ItemName: node.ItemName,
                IsLeaf: node.IsLeaf,
                BidPackage: node.BidPackage,
                Quantity: node.Quantity,
                UnitPrice: node.UnitPrice,
                EstimatedAmount: node.EstimatedAmount,
                SortOrder: 0,
                Notes: node.Notes));
            await _cost.RecalculateRollupAsync(ProjectId);
            // Refresh the affected node's EstimatedAmount + parent chain by reloading.
            await LoadAsync();
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }
```

Note: in this plan we keep `ApplyEditAsync` synchronous-per-call (no debounce). True debounce belongs in the View layer (TextBox `LostFocus` triggers Apply, not every keystroke). The plan's earlier mention of 300ms debounce is implementation detail deferred to the View — VM just exposes `ApplyEditAsync` to call when commit time.

- [ ] **Step 6.4: Verify + commit**

```bash
dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~CostTreeViewModelTests"
git add . && git commit -m "feat(desktop): CostTreeViewModel — ApplyEditAsync (inline cell commit + rollup)"
```

---

## Task 7: PaymentListDialogViewModel — TDD

**Files:**
- Create: `src/ConstructionFinance.Desktop/ViewModels/Payments/PaymentListDialogViewModel.cs`
- Create: stub `src/ConstructionFinance.Desktop/ViewModels/Payments/PaymentEditDialogViewModel.cs` (full impl Task 8)
- Test: `tests/ConstructionFinance.Desktop.Tests/Payments/PaymentListDialogViewModelTests.cs`

- [ ] **Step 7.1: Failing tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Desktop.Tests/Payments/PaymentListDialogViewModelTests.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Payments;
using ConstructionFinance.Domain.Enums;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Payments;

public class PaymentListDialogViewModelTests
{
    private static PaymentDto P(int id, int costItemId, int no, decimal amount) =>
        new(id, costItemId, no, PaymentType.Installment, new DateOnly(2026, 5, 1), amount, 10m, null, null, null, null);

    private static (PaymentListDialogViewModel vm, Mock<IPaymentService> svc, FakeDialogService dlg) Create(int costItemId = 7, IEnumerable<PaymentDto>? initial = null, PaymentSummaryDto? summary = null)
    {
        var svc = new Mock<IPaymentService>(MockBehavior.Strict);
        svc.Setup(s => s.GetByCostItemAsync(costItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((initial ?? Array.Empty<PaymentDto>()).ToList());
        svc.Setup(s => s.GetSummaryAsync(costItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary ?? new PaymentSummaryDto(costItemId, 1_000m, 0m, 1_000m, 0m));
        var dlg = new FakeDialogService();
        var vm = new PaymentListDialogViewModel(svc.Object, dlg);
        vm.SetCostItem(costItemId, "Móng cọc");
        return (vm, svc, dlg);
    }

    [Fact]
    public async Task LoadAsync_populates_Payments_and_Summary()
    {
        var (vm, _, _) = Create(initial: new[] { P(1, 7, 1, 300m) }, summary: new PaymentSummaryDto(7, 1_000m, 300m, 700m, 30m));
        await vm.LoadAsync();

        vm.Payments.Count.ShouldBe(1);
        vm.Summary.TotalPaid.ShouldBe(300m);
        vm.Summary.CompletionPct.ShouldBe(30m);
    }

    [Fact]
    public async Task AddCommand_opens_dialog_and_calls_AddAsync_on_confirm()
    {
        var (vm, svc, dlg) = Create();
        svc.Setup(s => s.AddAsync(It.IsAny<CreatePaymentDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(P(99, 7, 1, 500m));
        dlg.DialogResult = true;
        await vm.LoadAsync();

        await vm.AddCommand.ExecuteAsync(null);

        svc.Verify(s => s.AddAsync(It.Is<CreatePaymentDto>(d => d.CostItemId == 7), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCommand_calls_DeleteAsync_when_confirmed()
    {
        var (vm, svc, dlg) = Create(initial: new[] { P(1, 7, 1, 300m) });
        svc.Setup(s => s.DeleteAsync(1, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        dlg.ConfirmResult = true;
        await vm.LoadAsync();
        vm.SelectedPayment = vm.Payments[0];

        await vm.DeleteCommand.ExecuteAsync(null);

        svc.Verify(s => s.DeleteAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 7.2: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~PaymentListDialogViewModelTests"`

- [ ] **Step 7.3: Stub `PaymentEditDialogViewModel`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Payments/PaymentEditDialogViewModel.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Desktop.ViewModels.Payments;

// Stub — full implementation in Task 8.
public partial class PaymentEditDialogViewModel : ViewModelBase
{
    public PaymentEditDialogViewModel(PaymentDto? initial, int costItemId, decimal costItemEstimatedAmount)
    {
        _ = initial; _ = costItemEstimatedAmount;
        _costItemId = costItemId;
    }

    private readonly int _costItemId;
    public CreatePaymentDto ToCreateDto() =>
        new(_costItemId, PaymentType.Installment, DateOnly.FromDateTime(DateTime.Today), 0m, null, null, null, null, null);
    public UpdatePaymentDto ToUpdateDto(int id) =>
        new(id, PaymentType.Installment, DateOnly.FromDateTime(DateTime.Today), 0m, null, null, null, null, null);
}
```

- [ ] **Step 7.4: Implement `PaymentListDialogViewModel`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Payments/PaymentListDialogViewModel.cs`:
```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.ViewModels.Payments;

public partial class PaymentListDialogViewModel : ViewModelBase, IHasDialogTitle
{
    private readonly IPaymentService _service;
    private readonly IDialogService _dialog;

    public PaymentListDialogViewModel(IPaymentService service, IDialogService dialog)
    {
        _service = service;
        _dialog = dialog;
    }

    public int CostItemId { get; private set; }
    public string CostItemName { get; private set; } = "";
    public string DialogTitle => $"Đợt thanh toán — {CostItemName}";

    public ObservableCollection<PaymentDto> Payments { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private PaymentDto? _selectedPayment;

    [ObservableProperty] private PaymentSummaryDto _summary = new(0, 0m, 0m, 0m, 0m);
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public void SetCostItem(int costItemId, string costItemName)
    {
        CostItemId = costItemId;
        CostItemName = costItemName;
        OnPropertyChanged(nameof(DialogTitle));
    }

    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var list = await _service.GetByCostItemAsync(CostItemId);
            Payments.Clear();
            foreach (var p in list) Payments.Add(p);
            Summary = await _service.GetSummaryAsync(CostItemId);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var editVm = new PaymentEditDialogViewModel(initial: null, costItemId: CostItemId, costItemEstimatedAmount: Summary.EstimatedAmount);
        var ok = await _dialog.ShowDialogAsync(editVm);
        if (ok != true) return;
        try
        {
            var created = await _service.AddAsync(editVm.ToCreateDto());
            Payments.Add(created);
            Summary = await _service.GetSummaryAsync(CostItemId);
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync()
    {
        if (SelectedPayment is null) return;
        var editVm = new PaymentEditDialogViewModel(initial: SelectedPayment, costItemId: CostItemId, costItemEstimatedAmount: Summary.EstimatedAmount);
        var ok = await _dialog.ShowDialogAsync(editVm);
        if (ok != true) return;
        try
        {
            var updated = await _service.UpdateAsync(editVm.ToUpdateDto(SelectedPayment.Id));
            var idx = Payments.IndexOf(SelectedPayment);
            if (idx >= 0) Payments[idx] = updated;
            Summary = await _service.GetSummaryAsync(CostItemId);
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        if (SelectedPayment is null) return;
        var ok = await _dialog.ConfirmAsync("Xóa đợt thanh toán",
            $"Xóa đợt #{SelectedPayment.PaymentNo} ({SelectedPayment.Amount:#,##0}đ)?");
        if (!ok) return;
        try
        {
            await _service.DeleteAsync(SelectedPayment.Id);
            Payments.Remove(SelectedPayment);
            Summary = await _service.GetSummaryAsync(CostItemId);
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    private bool HasSelection() => SelectedPayment is not null;
}
```

- [ ] **Step 7.5: Verify + commit**

```bash
dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~PaymentListDialogViewModelTests"
git add . && git commit -m "feat(desktop): PaymentListDialogViewModel — CRUD + summary refresh"
```

---

## Task 8: `PaymentEditDialogViewModel` — full impl (replace stub)

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Payments/PaymentEditDialogViewModel.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Payments/PaymentEditDialogViewModelTests.cs`

- [ ] **Step 8.1: Failing tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Desktop.Tests/Payments/PaymentEditDialogViewModelTests.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.ViewModels.Payments;
using ConstructionFinance.Domain.Enums;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Payments;

public class PaymentEditDialogViewModelTests
{
    [Fact]
    public void New_dialog_defaults_today_and_carries_costItemId()
    {
        var vm = new PaymentEditDialogViewModel(initial: null, costItemId: 7, costItemEstimatedAmount: 1_000m);

        vm.IsEditMode.ShouldBeFalse();
        vm.CostItemId.ShouldBe(7);
        vm.PaymentDate.ShouldBe(DateOnly.FromDateTime(DateTime.Today));
    }

    [Fact]
    public void Edit_dialog_pre_fills_from_dto()
    {
        var dto = new PaymentDto(5, 7, 2, PaymentType.Installment, new DateOnly(2026, 5, 15), 300m, 30m, "HD-001", new DateOnly(2026, 5, 14), "Desc", "Notes");
        var vm = new PaymentEditDialogViewModel(initial: dto, costItemId: 7, costItemEstimatedAmount: 1_000m);

        vm.IsEditMode.ShouldBeTrue();
        vm.PaymentType.ShouldBe(PaymentType.Installment);
        vm.Amount.ShouldBe(300m);
        vm.InvoiceNo.ShouldBe("HD-001");
    }

    [Fact]
    public void ToCreateDto_carries_form_fields()
    {
        var vm = new PaymentEditDialogViewModel(initial: null, costItemId: 7, costItemEstimatedAmount: 1_000m)
        {
            PaymentType = PaymentType.Advance,
            Amount = 200m
        };
        var dto = vm.ToCreateDto();
        dto.CostItemId.ShouldBe(7);
        dto.PaymentType.ShouldBe(PaymentType.Advance);
        dto.Amount.ShouldBe(200m);
    }

    [Fact]
    public void Validation_Amount_must_be_positive()
    {
        var vm = new PaymentEditDialogViewModel(initial: null, costItemId: 7, costItemEstimatedAmount: 1_000m) { Amount = 0m };
        vm.AmountError.ShouldNotBeNullOrEmpty();
        vm.IsValid.ShouldBeFalse();
    }
}
```

- [ ] **Step 8.2: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~PaymentEditDialogViewModelTests"`

- [ ] **Step 8.3: Replace stub**

Replace `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Payments/PaymentEditDialogViewModel.cs` with:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Desktop.ViewModels.Payments;

public partial class PaymentEditDialogViewModel : ViewModelBase, IHasDialogTitle
{
    public bool IsEditMode { get; }
    public int CostItemId { get; }
    public decimal CostItemEstimatedAmount { get; }
    public string DialogTitle => IsEditMode ? "Sửa đợt thanh toán" : "Thêm đợt thanh toán";

    public IReadOnlyList<PaymentType> AvailableTypes { get; } = Enum.GetValues<PaymentType>().ToList();

    [ObservableProperty] private PaymentType _paymentType = PaymentType.Installment;
    [ObservableProperty] private DateOnly _paymentDate = DateOnly.FromDateTime(DateTime.Today);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AmountError), nameof(IsValid))]
    private decimal _amount;

    [ObservableProperty] private decimal? _percentage;
    [ObservableProperty] private string? _invoiceNo;
    [ObservableProperty] private DateOnly? _invoiceDate;
    [ObservableProperty] private string? _description;
    [ObservableProperty] private string? _notes;

    public PaymentEditDialogViewModel(PaymentDto? initial, int costItemId, decimal costItemEstimatedAmount)
    {
        CostItemId = costItemId;
        CostItemEstimatedAmount = costItemEstimatedAmount;
        IsEditMode = initial is not null;
        if (initial is not null)
        {
            PaymentType = initial.PaymentType;
            PaymentDate = initial.PaymentDate;
            Amount = initial.Amount;
            Percentage = initial.Percentage;
            InvoiceNo = initial.InvoiceNo;
            InvoiceDate = initial.InvoiceDate;
            Description = initial.Description;
            Notes = initial.Notes;
        }
    }

    public string? AmountError =>
        Amount <= 0 ? "Số tiền phải lớn hơn 0." : null;

    public bool IsValid => AmountError is null;

    public CreatePaymentDto ToCreateDto() =>
        new(CostItemId, PaymentType, PaymentDate, Amount, Percentage, InvoiceNo, InvoiceDate, Description, Notes);

    public UpdatePaymentDto ToUpdateDto(int id) =>
        new(id, PaymentType, PaymentDate, Amount, Percentage, InvoiceNo, InvoiceDate, Description, Notes);
}
```

- [ ] **Step 8.4: Verify + commit**

```bash
dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~PaymentEditDialogViewModelTests"
git add . && git commit -m "feat(desktop): PaymentEditDialogViewModel with validation"
```

---

## Task 9: Add `OpenPayments` command to `CostTreeViewModel`

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/CostItems/CostTreeViewModel.cs`
- Modify: `tests/ConstructionFinance.Desktop.Tests/CostItems/CostTreeViewModelTests.cs`

- [ ] **Step 9.1: Failing test**

Append to `CostTreeViewModelTests.cs`:
```csharp
    [Fact]
    public async Task OpenPaymentsCommand_opens_dialog_when_leaf_selected()
    {
        var (vm, _, _, _, dlg) = Create(new[]
        {
            Dto(1, null, "Móng", 0, "/1/", isLeaf: true, estimated: 1_000m)
        });
        dlg.DialogResult = true;

        await vm.LoadAsync();
        vm.SelectedNode = vm.RootNodes[0];

        await vm.OpenPaymentsCommand.ExecuteAsync(null);

        dlg.OpenedDialogs.Count.ShouldBe(1);
        dlg.OpenedDialogs[0].ShouldBeOfType<PaymentListDialogViewModel>();
    }

    [Fact]
    public async Task OpenPaymentsCommand_disabled_when_no_selection_or_non_leaf()
    {
        var (vm, _, _, _, _) = Create(new[]
        {
            Dto(1, null, "Nhóm", 0, "/1/", isLeaf: false)
        });
        await vm.LoadAsync();

        // No selection
        vm.SelectedNode = null;
        vm.OpenPaymentsCommand.CanExecute(null).ShouldBeFalse();

        // Non-leaf selected
        vm.SelectedNode = vm.RootNodes[0];
        vm.OpenPaymentsCommand.CanExecute(null).ShouldBeFalse();
    }
```

Add `using ConstructionFinance.Desktop.ViewModels.Payments;` to test file imports.

- [ ] **Step 9.2: Run — fails**

- [ ] **Step 9.3: Add `OpenPayments` command**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/CostItems/CostTreeViewModel.cs`:

Add `OpenPaymentsCommand` to `_selectedNode`'s attributes:
```csharp
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddChildCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddSiblingCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(IndentCommand))]
    [NotifyCanExecuteChangedFor(nameof(OutdentCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenPaymentsCommand))]
    private CostTreeNode? _selectedNode;
```

Add `using ConstructionFinance.Desktop.ViewModels.Payments;` to the file.

Add command at the end of the class (before the helper methods):
```csharp
    [RelayCommand(CanExecute = nameof(CanOpenPayments))]
    private async Task OpenPaymentsAsync()
    {
        if (SelectedNode is null || !SelectedNode.IsLeaf) return;
        var dialogVm = new PaymentListDialogViewModel(_payment, _dialog);
        dialogVm.SetCostItem(SelectedNode.Id, SelectedNode.ItemName);
        await dialogVm.LoadAsync();
        await _dialog.ShowDialogAsync(dialogVm);
        // After dialog closes, refresh tree (payments may have changed amounts)
        await LoadAsync();
    }

    private bool CanOpenPayments() => SelectedNode is not null && SelectedNode.IsLeaf;
```

- [ ] **Step 9.4: Verify + commit**

```bash
dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~CostTreeViewModelTests"
git add . && git commit -m "feat(desktop): CostTreeViewModel — OpenPaymentsCommand opens PaymentListDialog"
```

---

## Task 10: CostTreeView XAML — TreeDataGrid setup

**Files:**
- Create: `src/ConstructionFinance.Desktop/Views/CostItems/CostTreeView.axaml` + `.axaml.cs`

- [ ] **Step 10.1: Create `CostTreeView.axaml`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/CostItems/CostTreeView.axaml`:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.CostItems"
             xmlns:tdg="using:Avalonia.Controls"
             x:Class="ConstructionFinance.Desktop.Views.CostItems.CostTreeView"
             x:DataType="vm:CostTreeViewModel">
    <DockPanel>
        <!-- Toolbar -->
        <Border DockPanel.Dock="Top" BorderBrush="#DDDDDD" BorderThickness="0,0,0,1" Padding="0,0,0,8" Margin="0,0,0,8">
            <StackPanel Orientation="Horizontal" Spacing="8">
                <Button Content="+ Đầu mục gốc" Command="{Binding AddRootCommand}"/>
                <Button Content="+ Mục con" Command="{Binding AddChildCommand}"/>
                <Button Content="+ Cùng cấp" Command="{Binding AddSiblingCommand}"/>
                <Separator/>
                <Button Content="← Outdent" Command="{Binding OutdentCommand}" ToolTip.Tip="Alt+←"/>
                <Button Content="→ Indent" Command="{Binding IndentCommand}" ToolTip.Tip="Alt+→"/>
                <Separator/>
                <Button Content="Xóa" Command="{Binding DeleteCommand}"/>
                <Separator/>
                <Button Content="💰 Thanh toán" Command="{Binding OpenPaymentsCommand}"/>
                <ProgressBar IsIndeterminate="True" IsVisible="{Binding IsBusy}" Width="80" VerticalAlignment="Center"/>
                <TextBlock Text="{Binding ErrorMessage}" Foreground="Red" VerticalAlignment="Center"/>
                <TextBlock VerticalAlignment="Center" FontWeight="SemiBold"
                           Text="{Binding GrandTotal, StringFormat='Tổng cộng: {0:#\\,##0}đ'}"/>
            </StackPanel>
        </Border>

        <!-- Tree placeholder — implementing TreeDataGrid binding requires C# source setup in code-behind / VM.
             For now, use a simple TreeView until TreeDataGrid is wired in Task 11. -->
        <TreeView ItemsSource="{Binding RootNodes}"
                  SelectedItem="{Binding SelectedNode}">
            <TreeView.ItemTemplate>
                <TreeDataTemplate ItemsSource="{Binding Children}" DataType="vm:CostTreeNode">
                    <Grid ColumnDefinitions="*,80,100,120,160,140" Margin="0,2">
                        <TextBlock Grid.Column="0" Text="{Binding ItemName}" VerticalAlignment="Center"/>
                        <TextBlock Grid.Column="1" Text="" VerticalAlignment="Center"/>
                        <TextBlock Grid.Column="2" Text="{Binding Quantity, StringFormat=#\\,##0.####}" HorizontalAlignment="Right" VerticalAlignment="Center"/>
                        <TextBlock Grid.Column="3" Text="{Binding UnitPrice, StringFormat=#\\,##0}" HorizontalAlignment="Right" VerticalAlignment="Center"/>
                        <TextBlock Grid.Column="4" Text="{Binding EstimatedAmount, StringFormat=#\\,##0}" FontWeight="SemiBold" HorizontalAlignment="Right" VerticalAlignment="Center"/>
                        <TextBlock Grid.Column="5" Text="{Binding BidPackage}" VerticalAlignment="Center"/>
                    </Grid>
                </TreeDataTemplate>
            </TreeView.ItemTemplate>
        </TreeView>
    </DockPanel>
</UserControl>
```

> **Note**: Full inline-editable TreeDataGrid setup requires building `HierarchicalTreeDataGridSource<CostTreeNode>` in code-behind/VM. For Plan 05 MVP simplicity we use a basic `TreeView` with read-only display. Inline edit happens via a separate edit dialog opened on double-click (CostItemEditDialog — Task 12). Switching to true TreeDataGrid is a v1.1 enhancement.

- [ ] **Step 10.2: Code-behind**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/CostItems/CostTreeView.axaml.cs`:
```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop.Views.CostItems;

public partial class CostTreeView : UserControl
{
    public CostTreeView() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 10.3: Build**

Run: `dotnet build src/ConstructionFinance.Desktop/`
Expected: 0 errors. (RowSpacing/ColumnSpacing already not used.)

- [ ] **Step 10.4: Commit**

```bash
git add . && git commit -m "feat(desktop): CostTreeView XAML (TreeView display + toolbar)"
```

---

## Task 11: PaymentListDialog + PaymentEditDialog XAML

**Files:**
- Create: `src/ConstructionFinance.Desktop/Views/Payments/PaymentListDialog.axaml` + `.axaml.cs`
- Create: `src/ConstructionFinance.Desktop/Views/Payments/PaymentEditDialog.axaml` + `.axaml.cs`

- [ ] **Step 11.1: `PaymentListDialog.axaml`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Payments/PaymentListDialog.axaml`:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Payments"
             x:Class="ConstructionFinance.Desktop.Views.Payments.PaymentListDialog"
             x:DataType="vm:PaymentListDialogViewModel"
             MinWidth="700" MinHeight="450">
    <DockPanel>
        <!-- Summary -->
        <Border DockPanel.Dock="Top" Background="#F9F9F9" Padding="12" Margin="0,0,0,8" CornerRadius="4">
            <Grid ColumnDefinitions="*,*,*,*">
                <StackPanel Grid.Column="0">
                    <TextBlock Text="Dự toán" Foreground="Gray" FontSize="11"/>
                    <TextBlock Text="{Binding Summary.EstimatedAmount, StringFormat=#\\,##0}" FontSize="16" FontWeight="SemiBold"/>
                </StackPanel>
                <StackPanel Grid.Column="1">
                    <TextBlock Text="Đã thanh toán" Foreground="Gray" FontSize="11"/>
                    <TextBlock Text="{Binding Summary.TotalPaid, StringFormat=#\\,##0}" FontSize="16" FontWeight="SemiBold" Foreground="DarkGreen"/>
                </StackPanel>
                <StackPanel Grid.Column="2">
                    <TextBlock Text="Còn lại" Foreground="Gray" FontSize="11"/>
                    <TextBlock Text="{Binding Summary.Remaining, StringFormat=#\\,##0}" FontSize="16" FontWeight="SemiBold"/>
                </StackPanel>
                <StackPanel Grid.Column="3">
                    <TextBlock Text="% Hoàn thành" Foreground="Gray" FontSize="11"/>
                    <TextBlock Text="{Binding Summary.CompletionPct, StringFormat=0.00\\%}" FontSize="16" FontWeight="SemiBold" Foreground="#0078D4"/>
                </StackPanel>
            </Grid>
        </Border>

        <!-- Toolbar -->
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Spacing="8" Margin="0,0,0,8">
            <Button Content="+ Thêm đợt" Command="{Binding AddCommand}"/>
            <Button Content="Sửa" Command="{Binding EditCommand}"/>
            <Button Content="Xóa" Command="{Binding DeleteCommand}"/>
        </StackPanel>

        <DataGrid ItemsSource="{Binding Payments}"
                  SelectedItem="{Binding SelectedPayment}"
                  IsReadOnly="True"
                  GridLinesVisibility="Horizontal"
                  HeadersVisibility="Column">
            <DataGrid.Columns>
                <DataGridTextColumn Header="Đợt" Binding="{Binding PaymentNo}" Width="60"/>
                <DataGridTextColumn Header="Loại" Binding="{Binding PaymentType}" Width="120"/>
                <DataGridTextColumn Header="Ngày" Binding="{Binding PaymentDate}" Width="120"/>
                <DataGridTextColumn Header="Số tiền" Binding="{Binding Amount, StringFormat=#\\,##0}" Width="160"/>
                <DataGridTextColumn Header="%" Binding="{Binding Percentage, StringFormat=0.00}" Width="80"/>
                <DataGridTextColumn Header="Hóa đơn" Binding="{Binding InvoiceNo}" Width="*"/>
            </DataGrid.Columns>
        </DataGrid>
    </DockPanel>
</UserControl>
```

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Payments/PaymentListDialog.axaml.cs`:
```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop.Views.Payments;

public partial class PaymentListDialog : UserControl
{
    public PaymentListDialog() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 11.2: `PaymentEditDialog.axaml`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Payments/PaymentEditDialog.axaml`:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Payments"
             xmlns:common="using:ConstructionFinance.Desktop.Common"
             x:Class="ConstructionFinance.Desktop.Views.Payments.PaymentEditDialog"
             x:DataType="vm:PaymentEditDialogViewModel"
             MinWidth="500">
    <Grid ColumnDefinitions="170,*" RowDefinitions="Auto,Auto,Auto,Auto,Auto,Auto,Auto">
        <TextBlock Grid.Row="0" Grid.Column="0" Text="Loại thanh toán" VerticalAlignment="Center" Margin="0,6"/>
        <ComboBox Grid.Row="0" Grid.Column="1" ItemsSource="{Binding AvailableTypes}" SelectedItem="{Binding PaymentType}" Margin="0,6"/>

        <TextBlock Grid.Row="1" Grid.Column="0" Text="Ngày thanh toán" VerticalAlignment="Center" Margin="0,6"/>
        <DatePicker Grid.Row="1" Grid.Column="1" SelectedDate="{Binding PaymentDate, Mode=TwoWay, Converter={x:Static common:DateOnlyConverter.Instance}}" Margin="0,6"/>

        <TextBlock Grid.Row="2" Grid.Column="0" Text="Số tiền (VND) *" VerticalAlignment="Center" Margin="0,6"/>
        <StackPanel Grid.Row="2" Grid.Column="1" Margin="0,6">
            <NumericUpDown Value="{Binding Amount}" Minimum="0" FormatString="#,##0"/>
            <TextBlock Text="{Binding AmountError}" Foreground="Red" FontSize="11"
                       IsVisible="{Binding AmountError, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"/>
        </StackPanel>

        <TextBlock Grid.Row="3" Grid.Column="0" Text="% so với dự toán" VerticalAlignment="Center" Margin="0,6"/>
        <NumericUpDown Grid.Row="3" Grid.Column="1" Value="{Binding Percentage}" Minimum="0" Maximum="100" FormatString="0.00" Margin="0,6"/>

        <TextBlock Grid.Row="4" Grid.Column="0" Text="Số hóa đơn" VerticalAlignment="Center" Margin="0,6"/>
        <TextBox Grid.Row="4" Grid.Column="1" Text="{Binding InvoiceNo}" Margin="0,6"/>

        <TextBlock Grid.Row="5" Grid.Column="0" Text="Ngày hóa đơn" VerticalAlignment="Center" Margin="0,6"/>
        <DatePicker Grid.Row="5" Grid.Column="1" SelectedDate="{Binding InvoiceDate, Mode=TwoWay, Converter={x:Static common:NullableDateOnlyConverter.Instance}}" Margin="0,6"/>

        <TextBlock Grid.Row="6" Grid.Column="0" Text="Ghi chú" VerticalAlignment="Top" Margin="0,6"/>
        <TextBox Grid.Row="6" Grid.Column="1" Text="{Binding Notes}" AcceptsReturn="True" Height="60" TextWrapping="Wrap" Margin="0,6"/>
    </Grid>
</UserControl>
```

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Payments/PaymentEditDialog.axaml.cs`:
```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop.Views.Payments;

public partial class PaymentEditDialog : UserControl
{
    public PaymentEditDialog() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 11.3: Build + commit**

```bash
dotnet build src/ConstructionFinance.Desktop/
git add . && git commit -m "feat(desktop): PaymentListDialog + PaymentEditDialog XAML views"
```

---

## Task 12: Wire `CostTreeView` into `ProjectDetailView`'s "Chi phí" tab

**Files:**
- Modify: `src/ConstructionFinance.Desktop/Views/Projects/ProjectDetailView.axaml`
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Projects/ProjectDetailViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/Common/HostBuilder.cs`
- Modify: `tests/ConstructionFinance.Desktop.Tests/Projects/ProjectDetailViewModelTests.cs`

- [ ] **Step 12.1: Register `CostTreeViewModel` in DI**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Common/HostBuilder.cs`. Inside `ConfigureServices`, after `ProjectListViewModel` registration, add:
```csharp
                services.AddTransient<ViewModels.CostItems.CostTreeViewModel>();
```

- [ ] **Step 12.2: Update `ProjectDetailViewModel` to host CostTreeViewModel**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Projects/ProjectDetailViewModel.cs`. Update constructor to accept `IServiceProvider` and create `CostTree` property:

Replace the file with:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.ViewModels.CostItems;
using ConstructionFinance.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructionFinance.Desktop.ViewModels.Projects;

public partial class ProjectDetailViewModel : ViewModelBase
{
    private readonly IProjectService _service;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;

    public ProjectDetailViewModel(ProjectDto project, IProjectService service, IDialogService dialog, INavigationService nav, IServiceProvider services)
    {
        _service = service;
        _dialog = dialog;
        _nav = nav;
        Project = project;
        CostTree = services.GetRequiredService<CostTreeViewModel>();
        CostTree.SetProject(project.Id, project.Status);
    }

    [ObservableProperty] private ProjectDto _project;

    public CostTreeViewModel CostTree { get; }

    public IReadOnlyList<ProjectStatus> AvailableStatuses { get; } =
        Enum.GetValues<ProjectStatus>().ToList();

    public Task InitializeAsync() => CostTree.LoadAsync();

    public async Task ChangeStatusAsync(ProjectStatus newStatus)
    {
        try
        {
            Project = await _service.UpdateStatusAsync(Project.Id, newStatus);
            CostTree.SetProject(Project.Id, Project.Status); // refresh read-only state
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

- [ ] **Step 12.3: Update `ProjectListViewModel.OpenDetailAsync`** to pass `IServiceProvider`

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Projects/ProjectListViewModel.cs`. Update `OpenDetailAsync` to use the new 5-arg constructor:
```csharp
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task OpenDetailAsync()
    {
        if (SelectedProject is null) return Task.CompletedTask;
        var detail = new ProjectDetailViewModel(SelectedProject, _service, _dialog, _nav, _services);
        _nav.NavigateTo(detail);
        return detail.InitializeAsync();
    }
```

- [ ] **Step 12.4: Update test factory for ProjectDetailViewModel**

Edit `/home/hieu-nguyen/management/tests/ConstructionFinance.Desktop.Tests/Projects/ProjectDetailViewModelTests.cs`. Update `Create()` helper:
```csharp
    private static (ProjectDetailViewModel vm, Mock<IProjectService> svc, FakeDialogService dlg, FakeNavigationService nav) Create(ProjectStatus status = ProjectStatus.Planning)
    {
        var svc = new Mock<IProjectService>(MockBehavior.Strict);
        var dlg = new FakeDialogService();
        var nav = new FakeNavigationService();
        var sc = new ServiceCollection();
        var cost = new Mock<ICostService>(MockBehavior.Loose);
        cost.Setup(s => s.GetProjectTreeAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CostItemDto>());
        sc.AddSingleton(cost.Object);
        var pay = new Mock<IPaymentService>(MockBehavior.Loose);
        sc.AddSingleton(pay.Object);
        var units = new Mock<ICostUnitTypeService>(MockBehavior.Loose);
        units.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<CostUnitTypeDto>());
        sc.AddSingleton(units.Object);
        sc.AddSingleton<ConstructionFinance.Desktop.Services.IDialogService>(dlg);
        sc.AddTransient<ConstructionFinance.Desktop.ViewModels.CostItems.CostTreeViewModel>();
        var sp = sc.BuildServiceProvider();
        var vm = new ProjectDetailViewModel(P(status), svc.Object, dlg, nav, sp);
        return (vm, svc, dlg, nav);
    }
```

Add these usings to the test file:
```csharp
using ConstructionFinance.Application.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
```

- [ ] **Step 12.5: Update `ProjectDetailView.axaml` — Chi phí tab content**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Projects/ProjectDetailView.axaml`. Find the "Chi phí" `<TabItem>` and replace its content (currently a `<TextBlock>` placeholder) with:
```xml
            <TabItem Header="Chi phí">
                <ContentControl Content="{Binding CostTree}"/>
            </TabItem>
```

- [ ] **Step 12.6: Build + run all tests**

Run:
```bash
dotnet build --configuration Release
dotnet test --configuration Release
```
Expected: 0 build errors. All tests pass.

- [ ] **Step 12.7: Commit**

```bash
git add . && git commit -m "feat(desktop): wire CostTreeView into ProjectDetailView's Chi phí tab"
```

---

## Final verification

- [ ] **Step F.1: Clean build + full suite**

```bash
cd /home/hieu-nguyen/management/
dotnet clean
dotnet build --configuration Release
dotnet test --configuration Release --verbosity minimal
```
Expected: 0 errors. Approximately **190+ tests pass** (168 baseline + 1 CostUnitTypeService + 3 CostTreeNode + ~15 CostTreeViewModel + 3 PaymentListDialog + 4 PaymentEditDialog).

- [ ] **Step F.2: Confirm commits**

```bash
git log --oneline | head -16
```
Expected: ~12 new commits for Plan 05.

- [ ] **Step F.3: Manual smoke test (desktop only)**

```bash
dotnet run --project src/ConstructionFinance.Desktop/
```

Manual checklist:
- [ ] Open app → Quyết định → drill to Dự án
- [ ] Click tab "Chi phí" → CostTreeView appears with toolbar
- [ ] Click "+ Đầu mục gốc" → new node appears with default name "Đầu mục mới"
- [ ] Select that node → "+ Mục con" enabled → click → child appears
- [ ] Status of project = Completed → all buttons disabled
- [ ] Select a leaf → "💰 Thanh toán" enabled → click → PaymentListDialog opens
- [ ] Add a payment with Amount = 500000 → appears in list → Summary updates

---

## Deliverables

After Plan 05:

1. ✅ `CostUnitTypeService` (read-only) + DI registration
2. ✅ `CostTreeNode` observable wrapper for tree binding
3. ✅ `CostTreeViewModel` with Load + Add (root/child/sibling) + Delete + Indent/Outdent + Inline edit (ApplyEditAsync) + OpenPayments — ~15 tests
4. ✅ `PaymentListDialogViewModel` with CRUD + summary refresh — 3 tests
5. ✅ `PaymentEditDialogViewModel` with validation — 4 tests
6. ✅ `CostTreeView` XAML (basic TreeView, toolbar with all commands)
7. ✅ `PaymentListDialog` + `PaymentEditDialog` XAML
8. ✅ ProjectDetailView "Chi phí" tab swaps placeholder for actual cost tree
9. ✅ Read-only mode tied to `Project.Status == Completed`
10. ✅ End-to-end: tạo đầu mục chi phí cho dự án + ghi nhận thanh toán

**What's NOT in this plan (v1.1+):**
- True inline cell editing in TreeDataGrid (uses basic TreeView for now; double-click → edit dialog as workaround if needed in a follow-up patch)
- Copy/paste from Excel clipboard
- Drag-fill / fill down
- Undo/Redo stack
- Import from Excel
- Keyboard shortcuts (Enter, Tab, Alt+→, Alt+←) — toolbar buttons only for now
- Bid package picker (free-text field for now)
- Vendor picker for cost items

**Next plan (Plan 06):** Settings + Backup/Restore + Inno Setup installer (final MVP plan → ra `.exe`).
