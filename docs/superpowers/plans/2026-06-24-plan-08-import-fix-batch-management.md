# Plan 08: Import Fix + Batch Management

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the broken Excel import (ViewLocator mismatch), squash 3 follow-on bugs, then add batch management UI (delete + view warnings) so users can manage imported data.

**Architecture:** Tasks 1-4 are pure bug fixes with no new entities or migrations. Task 5 adds UI-only batch management (delete + warnings) using the existing `DeleteBatchAsync` service method already in `IProjectSummaryService`. No new DB columns or migrations needed.

**Tech Stack:** .NET 8, Avalonia 11 + FluentAvalonia 2.2, CommunityToolkit.Mvvm, xUnit + Shouldly

## Global Constraints

- Free software only (MIT/Apache/BSD) — no new commercial packages
- camelCase local vars/params, `_camelCase` private fields, PascalCase everything else
- No breaking changes to existing tests (258 must still pass after every task)
- ViewLocator convention: `XxxViewModel` → `XxxView`; `XxxDialogViewModel` → `XxxDialog`
- Never add UI comments explaining WHAT; only add WHY comments for non-obvious invariants

---

## File Map

| File | Action | Task |
|------|--------|------|
| `src/ConstructionFinance.Desktop/Views/TongHop/ImportWizardDialog.axaml` | **Rename** → `ImportWizardView.axaml` | 1 |
| `src/ConstructionFinance.Desktop/Views/TongHop/ImportWizardDialog.axaml.cs` | **Rename** → `ImportWizardView.axaml.cs` | 1 |
| `tests/ConstructionFinance.Desktop.Tests/ViewLocatorTests.cs` | Create (new test) | 1 |
| `src/ConstructionFinance.Desktop/Views/TongHop/ImportWizardView.axaml` | Modify — add `ErrorMessage` TextBlock | 2 |
| `src/ConstructionFinance.Application/Services/Implementations/ExcelImportService.cs` | Modify — fix `TryRefine` return for already-split case | 3 |
| `tests/ConstructionFinance.Application.Tests/ExcelImportServiceTests.cs` | Modify — add no-false-warning test | 3 |
| `src/ConstructionFinance.Desktop/ViewModels/TongHop/TongHopViewModel.cs` | Modify — remove redundant `GetLatestBatchAsync` call + add `DeleteBatchCommand` | 4 + 5 |
| `src/ConstructionFinance.Desktop/Views/TongHop/TongHopView.axaml` | Modify — add delete button + warnings button | 5 |

---

## Task 1: C1 — Fix ViewLocator: rename `ImportWizardDialog` → `ImportWizardView`

**Root cause:** `ViewLocator.Build(ImportWizardViewModel)` tries two candidates:
- Candidate1: `Views.TongHop.ImportWizardView` — **file does not exist**
- Candidate2: strips "ViewModel" → `Views.TongHop.ImportWizard` — **file does not exist**

The actual class `ImportWizardDialog` is never reached. The dialog opens but shows `"View not found for ImportWizardViewModel"` instead of the wizard UI. Fix: rename the view to follow the `XxxViewModel → XxxView` convention.

**Files:**
- Rename: `src/ConstructionFinance.Desktop/Views/TongHop/ImportWizardDialog.axaml` → `ImportWizardView.axaml`
- Rename: `src/ConstructionFinance.Desktop/Views/TongHop/ImportWizardDialog.axaml.cs` → `ImportWizardView.axaml.cs`
- Create: `tests/ConstructionFinance.Desktop.Tests/ViewLocatorTests.cs`

**Interfaces:**
- Produces: `ImportWizardView` class resolving for `ImportWizardViewModel` via ViewLocator

- [ ] **Step 1.1: Write failing test**

Create `tests/ConstructionFinance.Desktop.Tests/ViewLocatorTests.cs`:

```csharp
using Avalonia.Controls;
using ConstructionFinance.Desktop;
using ConstructionFinance.Desktop.ViewModels.TongHop;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests;

public class ViewLocatorTests
{
    [Fact]
    public void ViewLocator_resolves_ImportWizardViewModel_to_ImportWizardView()
    {
        var locator = new ViewLocator();
        var vm = new FakeImportWizardViewModel();
        var view = locator.Build(vm);
        view.ShouldNotBeNull();
        view.ShouldNotBeOfType<TextBlock>(
            "ViewLocator returned 'View not found' TextBlock — check ImportWizardView.axaml exists and x:Class matches");
        view.GetType().Name.ShouldBe("ImportWizardView");
    }

    // Minimal stub — no real services needed just for ViewLocator resolution
    private sealed class FakeImportWizardViewModel : ImportWizardViewModel
    {
        public FakeImportWizardViewModel() : base(null!, null!, null!) { }
    }
}
```

- [ ] **Step 1.2: Run test — verify FAIL**

```bash
dotnet test tests/ConstructionFinance.Desktop.Tests --nologo --verbosity minimal \
  --filter "ViewLocatorTests"
```

Expected: FAIL — `view.GetType().Name` is `"TextBlock"` not `"ImportWizardView"`.

- [ ] **Step 1.3: Rename the XAML file**

```bash
cd src/ConstructionFinance.Desktop/Views/TongHop
mv ImportWizardDialog.axaml ImportWizardView.axaml
mv ImportWizardDialog.axaml.cs ImportWizardView.axaml.cs
```

- [ ] **Step 1.4: Update x:Class in ImportWizardView.axaml**

Open `src/ConstructionFinance.Desktop/Views/TongHop/ImportWizardView.axaml`.
Change line 4:
```xml
x:Class="ConstructionFinance.Desktop.Views.TongHop.ImportWizardDialog"
```
to:
```xml
x:Class="ConstructionFinance.Desktop.Views.TongHop.ImportWizardView"
```

- [ ] **Step 1.5: Update class name in code-behind**

Open `src/ConstructionFinance.Desktop/Views/TongHop/ImportWizardView.axaml.cs`. Replace entire file:

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop.Views.TongHop;

public partial class ImportWizardView : UserControl
{
    public ImportWizardView() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 1.6: Run test — verify PASS**

```bash
dotnet test tests/ConstructionFinance.Desktop.Tests --nologo --verbosity minimal \
  --filter "ViewLocatorTests"
```

Expected: PASS.

- [ ] **Step 1.7: Run full test suite**

```bash
dotnet test --nologo --verbosity minimal
```

Expected: 259/259 pass (258 baseline + 1 new).

- [ ] **Step 1.8: Commit**

```bash
git add src/ConstructionFinance.Desktop/Views/TongHop/ImportWizardView.axaml \
        src/ConstructionFinance.Desktop/Views/TongHop/ImportWizardView.axaml.cs \
        tests/ConstructionFinance.Desktop.Tests/ViewLocatorTests.cs
git commit -m "$(cat <<'EOF'
fix(desktop): rename ImportWizardDialog → ImportWizardView so ViewLocator can resolve it

ViewLocator candidates try XxxView and Xxx, neither matched ImportWizardDialog.
Import dialog was showing 'View not found' instead of wizard UI.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: B2 — Show ErrorMessage in ImportWizardView

**Root cause:** `ImportWizardViewModel.ErrorMessage` is set when `ParseAsync` throws, but `ImportWizardView.axaml` has no binding for it. The user sees no feedback on parse failure.

**Files:**
- Modify: `src/ConstructionFinance.Desktop/Views/TongHop/ImportWizardView.axaml`

- [ ] **Step 2.1: Add ErrorMessage TextBlock to ImportWizardView.axaml**

Open `src/ConstructionFinance.Desktop/Views/TongHop/ImportWizardView.axaml`.

Replace the toolbar `StackPanel` (lines 8-12, the `DockPanel.Dock="Top"` section) with:

```xml
<StackPanel DockPanel.Dock="Top" Spacing="4" Margin="0,0,0,12">
    <StackPanel Orientation="Horizontal" Spacing="8">
        <Button Content="Chọn file..." Command="{Binding BrowseCommand}"/>
        <TextBlock Text="{Binding SelectedFilePath}" VerticalAlignment="Center" TextTrimming="CharacterEllipsis"/>
        <ProgressBar IsIndeterminate="True" IsVisible="{Binding IsBusy}" Width="80"/>
    </StackPanel>
    <TextBlock Text="{Binding ErrorMessage}"
               Foreground="Red"
               TextWrapping="Wrap"
               IsVisible="{Binding ErrorMessage, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"/>
</StackPanel>
```

- [ ] **Step 2.2: Build to verify no XAML errors**

```bash
dotnet build src/ConstructionFinance.Desktop --nologo -q
```

Expected: 0 errors.

- [ ] **Step 2.3: Commit**

```bash
git add src/ConstructionFinance.Desktop/Views/TongHop/ImportWizardView.axaml
git commit -m "$(cat <<'EOF'
fix(desktop): show ErrorMessage in ImportWizardView when file parse fails

Was set on ViewModel but never bound in XAML — user saw no feedback on error.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: B1 — Fix false-positive NSNN/DTNH warning for already-split projects

**Root cause:** `TryRefine` returns `null` for two distinct cases:
1. Project already has split NSNN/DTNH (correct, no warning needed)  
2. Project not found in Tổng hợp lookup (warning IS appropriate)

Caller cannot distinguish them, so warns in both cases. Hiệu.xlsx projects with `DisbursedDTNH != 0` (already split) get spurious "không tìm thấy phân tách" warnings.

**Fix:** Return `s` (the original DTO unchanged) for case 1, return `null` only for case 2. Caller logic stays the same — `null` means "warn + use original".

**Files:**
- Modify: `src/ConstructionFinance.Application/Services/Implementations/ExcelImportService.cs` (lines ~289-300)
- Modify: `tests/ConstructionFinance.Application.Tests/ExcelImportServiceTests.cs`

- [ ] **Step 3.1: Write failing test**

Add to `tests/ConstructionFinance.Application.Tests/ExcelImportServiceTests.cs`:

```csharp
[Fact]
public async Task ParseAsync_Hieu_xlsx_no_false_warning_for_already_split_projects()
{
    // Hiệu.xlsx projects have DisbursedDTNH > 0 — already split.
    // After RefineFundingFromTongHop they must NOT produce "không tìm thấy" warning.
    var svc = new ExcelImportService();
    var result = await svc.ParseAsync(HieuFixturePath);

    var spurious = result.Warnings.Where(w =>
        w.Contains("không tìm thấy phân tách") &&
        w.Contains("NSNN/DTNH")).ToList();
    spurious.ShouldBeEmpty(
        $"False-positive warnings found: {string.Join("; ", spurious)}");
}
```

- [ ] **Step 3.2: Run test — verify FAIL**

```bash
dotnet test tests/ConstructionFinance.Application.Tests --nologo --verbosity minimal \
  --filter "ParseAsync_Hieu_xlsx_no_false_warning"
```

Expected: FAIL — spurious warnings found because already-split check returns null.

- [ ] **Step 3.3: Fix TryRefine in ExcelImportService.cs**

Find the `TryRefine` static method (around line 289) and change the first guard from `return null` to `return s`:

```csharp
private static ProjectSummaryDto? TryRefine(
    ProjectSummaryDto s,
    Dictionary<string, (decimal nsnn, decimal dtnh)> bySheetRef,
    Dictionary<string, (decimal nsnn, decimal dtnh)> byName)
{
    // Project already has both funding sources — leave it as-is, no warning.
    if (s.DisbursedDTNH != 0m || s.DisbursedNSNN != s.TotalDisbursed) return s;
    if (bySheetRef.TryGetValue(s.SheetName, out var a) && a.nsnn + a.dtnh > 0)
        return s with { DisbursedNSNN = a.nsnn, DisbursedDTNH = a.dtnh };
    if (byName.TryGetValue(s.ProjectName, out var b) && b.nsnn + b.dtnh > 0)
        return s with { DisbursedNSNN = b.nsnn, DisbursedDTNH = b.dtnh };
    return null; // genuinely not found → caller should warn
}
```

- [ ] **Step 3.4: Run all Application tests**

```bash
dotnet test tests/ConstructionFinance.Application.Tests --nologo --verbosity minimal
```

Expected: all pass (including existing `ParseAsync_Hieu_xlsx_attributes_NSNN_and_DTNH_separately` — data values must be unchanged).

- [ ] **Step 3.5: Run full suite**

```bash
dotnet test --nologo --verbosity minimal
```

Expected: 260/260 pass (259 + 1 new).

- [ ] **Step 3.6: Commit**

```bash
git add src/ConstructionFinance.Application/Services/Implementations/ExcelImportService.cs \
        tests/ConstructionFinance.Application.Tests/ExcelImportServiceTests.cs
git commit -m "$(cat <<'EOF'
fix(app): suppress false NSNN/DTNH warning for already-split Hiệu projects

TryRefine returned null for both 'already split' and 'not found' cases.
Caller warned for both. Fix: return s (unchanged) when already split so
only genuinely missing projects produce a warning.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: B3 — Remove redundant GetLatestBatchAsync in TongHopViewModel.LoadAsync

**Root cause:** `LoadAsync` calls `ListBatchesAsync()` (returns batches ordered by `ImportedAtUtc DESC`) then also calls `GetLatestBatchAsync()` and checks if it's already in the list. Since `ListBatchesAsync` already includes the latest batch as the first element, the second call is always redundant and adds an extra DB round-trip.

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/TongHop/TongHopViewModel.cs`

- [ ] **Step 4.1: Simplify LoadAsync**

Open `src/ConstructionFinance.Desktop/ViewModels/TongHop/TongHopViewModel.cs`.

Replace the `LoadAsync` method body:

```csharp
public async Task LoadAsync()
{
    IsBusy = true;
    ErrorMessage = null;
    try
    {
        Batches.Clear();
        var list = await _summaries.ListBatchesAsync();
        foreach (var b in list) Batches.Add(b);
        SelectedBatch = Batches.FirstOrDefault();
    }
    catch (Exception ex) { ErrorMessage = ex.Message; }
    finally { IsBusy = false; }
}
```

- [ ] **Step 4.2: Build + full test**

```bash
dotnet build src/ConstructionFinance.Desktop --nologo -q
dotnet test --nologo --verbosity minimal
```

Expected: 260/260 pass, 0 errors.

- [ ] **Step 4.3: Commit**

```bash
git add src/ConstructionFinance.Desktop/ViewModels/TongHop/TongHopViewModel.cs
git commit -m "$(cat <<'EOF'
refactor(desktop): remove redundant GetLatestBatchAsync call in TongHopViewModel.LoadAsync

ListBatchesAsync already returns batches ordered DESC by date so the
first element is the latest. The second DB query was always a no-op.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: F1 — Batch management UI (delete + view warnings)

**Goal:** Let users delete an import batch and inspect its parse warnings from the Tổng hợp screen.

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/TongHop/TongHopViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/Views/TongHop/TongHopView.axaml`

**Interfaces:**
- Consumes: `IProjectSummaryService.DeleteBatchAsync(int batchId)` (already implemented)
- Consumes: `IDialogService.ConfirmAsync(string, string)` (already implemented)
- Consumes: `IDialogService.ShowMessageAsync(string, string)` (already implemented)
- Produces: `TongHopViewModel.DeleteBatchCommand`, `TongHopViewModel.ShowWarningsCommand`

- [ ] **Step 5.1: Add DeleteBatchCommand to TongHopViewModel**

Open `src/ConstructionFinance.Desktop/ViewModels/TongHop/TongHopViewModel.cs`.

Add after the `ExportAsync` method (before the closing brace):

```csharp
[RelayCommand(CanExecute = nameof(HasSelectedBatch))]
private async Task DeleteBatchAsync()
{
    if (SelectedBatch is null) return;
    var confirmed = await _dialog.ConfirmAsync(
        "Xóa lô import",
        $"Xóa lô '{SelectedBatch.SourceFileName}' (nhập {SelectedBatch.ImportedAtUtc:dd/MM/yyyy})?\nDữ liệu quyết toán sẽ bị xóa hoàn toàn.");
    if (!confirmed) return;

    IsBusy = true;
    try
    {
        await _summaries.DeleteBatchAsync(SelectedBatch.Id);
        await LoadAsync();
    }
    catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    finally { IsBusy = false; }
}

[RelayCommand(CanExecute = nameof(HasSelectedBatch))]
private async Task ShowWarningsAsync()
{
    if (SelectedBatch is null) return;
    if (SelectedBatch.Warnings.Count == 0)
    {
        await _dialog.ShowMessageAsync("Cảnh báo import", "Không có cảnh báo nào.");
        return;
    }
    var text = string.Join("\n", SelectedBatch.Warnings.Select((w, i) => $"{i + 1}. {w}"));
    await _dialog.ShowMessageAsync(
        $"Cảnh báo import — {SelectedBatch.SourceFileName}",
        text);
}

private bool HasSelectedBatch() => SelectedBatch is not null;
```

Also add at the end of `partial void OnSelectedBatchChanged`:

```csharp
partial void OnSelectedBatchChanged(ImportBatchDto? value)
{
    _ = ReloadSummariesAsync();
    DeleteBatchCommand.NotifyCanExecuteChanged();
    ShowWarningsCommand.NotifyCanExecuteChanged();
}
```

(Replace the existing single-statement `partial void OnSelectedBatchChanged`.)

- [ ] **Step 5.2: Add buttons to TongHopView.axaml**

Open `src/ConstructionFinance.Desktop/Views/TongHop/TongHopView.axaml`.

Replace the toolbar StackPanel (the `Orientation="Horizontal"` one with the Import/Export buttons):

```xml
<StackPanel Orientation="Horizontal" Spacing="8">
    <TextBlock Text="Lô import:" VerticalAlignment="Center"/>
    <ComboBox ItemsSource="{Binding Batches}"
              SelectedItem="{Binding SelectedBatch}"
              MinWidth="320"
              DisplayMemberBinding="{Binding SourceFileName}"/>
    <Button Content="📥 Import Excel" Command="{Binding ImportCommand}"/>
    <Button Content="💾 Xuất Excel" Command="{Binding ExportCommand}"/>
    <Button Content="⚠ Cảnh báo"
            Command="{Binding ShowWarningsCommand}"
            ToolTip.Tip="Xem cảnh báo của lô import đang chọn"/>
    <Button Content="🗑 Xóa lô"
            Command="{Binding DeleteBatchCommand}"
            Foreground="Red"
            ToolTip.Tip="Xóa lô import đang chọn"/>
    <ProgressBar IsIndeterminate="True" IsVisible="{Binding IsBusy}" Width="80" VerticalAlignment="Center"/>
</StackPanel>
```

- [ ] **Step 5.3: Build to verify**

```bash
dotnet build src/ConstructionFinance.Desktop --nologo -q
```

Expected: 0 errors.

- [ ] **Step 5.4: Run full test suite**

```bash
dotnet test --nologo --verbosity minimal
```

Expected: 260/260 pass.

- [ ] **Step 5.5: Commit**

```bash
git add src/ConstructionFinance.Desktop/ViewModels/TongHop/TongHopViewModel.cs \
        src/ConstructionFinance.Desktop/Views/TongHop/TongHopView.axaml
git commit -m "$(cat <<'EOF'
feat(desktop): add delete batch + view warnings buttons to Tổng hợp screen

Users can now delete an import batch (with confirmation) and inspect
parse warnings for any batch without leaving the screen.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Final Verification

```bash
dotnet clean
dotnet build --configuration Release --nologo
dotnet test --configuration Release --nologo --verbosity minimal
```

Expected: 0 errors, 260 tests pass (258 baseline + 2 new: `ViewLocatorTests` + false-warning test).

**Manual smoke test:**
1. Launch app: `dotnet run --project src/ConstructionFinance.Desktop`
2. Click "📥 Import Excel" in PaneFooter or Tổng hợp screen → dialog opens showing file picker, not "View not found"
3. Select `reference/H.xlsx` → preview populates, no red error text
4. Click "Lưu" → batch saved, Tổng hợp refreshes
5. Click "⚠ Cảnh báo" → warnings shown
6. Click "🗑 Xóa lô" → confirmation appears, then batch deleted

---

## Deliverables

After Plan 08:
1. ✅ Excel import works end-to-end (C1 fixed)
2. ✅ Parse errors visible to user in dialog (B2 fixed)
3. ✅ No spurious NSNN/DTNH warnings for Hiệu.xlsx (B1 fixed)
4. ✅ One fewer DB round-trip on TongHop load (B3 fixed)
5. ✅ Users can delete batches and inspect warnings

## Remaining for Plan 09+

- `ContentDialog.ShowAsync()` without explicit window — works on Linux/X11 currently; harden by passing `desktop.MainWindow` when running on Wayland
- Duplicate import entry point (PaneFooter + TongHop screen): functional but can diverge; consolidate to single path
- Diff view between two import batches
- Multi-file batch import
- Payment-row import (drill-down per đợt thanh toán)
- PDF export of Thông tri
