# Phase 3 — Excel-like Entry Grid Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a master–detail screen where the user picks a finance Project and edits its đợt (line items) in a hand-built, Excel-like grid (gridlines, keyboard cell-nav, paste-block from Excel/TSV, frozen header + frozen TOTAL row), with live totals computed by `ProjectCalculator` and persistence via the finance service.

**Architecture:** Custom grid (no DataGrid/TreeDataGrid) per design decision. A `LineItemRowViewModel` (observable, one per đợt) recomputes its own derived cells via the pure `ProjectCalculator.ComputeLine` and raises an event; `FinanceProjectGridViewModel` (master–detail) owns the project picker, the `ObservableCollection<LineItemRowViewModel>`, the live TOTAL figures (via `ProjectCalculator.Aggregate`), and save/delete. The View renders a frozen header `Grid`, a scrollable `ItemsControl` body of per-row `Grid`s, and a frozen TOTAL `Grid`; code-behind wires keyboard navigation and Ctrl+V paste (parsed by a pure `ClipboardGridParser`). MVVM with CommunityToolkit.Mvvm; Clean Architecture layering preserved.

**Tech Stack:** .NET 8, Avalonia 11.2 + FluentAvalonia, CommunityToolkit.Mvvm, EF Core 8 + SQLite, xUnit + Shouldly + Moq.

## Global Constraints

- Giấy phép **miễn phí** (cơ quan nhà nước) — no commercial controls, no new UI package; build the grid from stock Avalonia primitives.
- Desktop offline, single user.
- Code conventions: `camelCase` locals/params, `_camelCase` private fields, `PascalCase` types/members.
- Money is `decimal`; persistence already uses `HasPrecision(18,2)`.
- Strangler: legacy schema/UI (`Entities.Project`, Contract, CostItem, Payment, ImportBatch) stays untouched; the new model lives in `ConstructionFinance.Domain.Finance` and the new UI under `…/Finance/`.
- Derived figures are **not stored** — computed live from raw cells via `ProjectCalculator` (single source of formulas).
- All UI strings in Vietnamese, matching existing screens.

---

## File Structure

**Domain / Data (Task 1):**
- Modify `src/ConstructionFinance.Domain/Abstractions/IFinanceProjectRepository.cs` — add `UpdateAsync`.
- Modify `src/ConstructionFinance.Data/Repositories/FinanceProjectRepository.cs` — implement replace-lines `UpdateAsync`.

**Application service (Task 2):**
- Modify `src/ConstructionFinance.Application/Services/Interfaces/IFinanceProjectService.cs` — add `ListAsync`, `GetWithLinesAsync`, `UpdateAsync`, `DeleteAsync`.
- Modify `src/ConstructionFinance.Application/Services/Implementations/FinanceProjectService.cs`.

**Desktop ViewModels / helpers (Tasks 3–5):**
- Create `src/ConstructionFinance.Desktop/Common/FinanceCellParsing.cs` — pure money/date cell parsers.
- Create `src/ConstructionFinance.Desktop/ViewModels/Finance/LineItemRowViewModel.cs` — observable row + live calc.
- Create `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceProjectGridViewModel.cs` — master–detail + totals + persistence + paste apply.
- Create `src/ConstructionFinance.Desktop/Common/ClipboardGridParser.cs` — pure TSV → `string[][]`.

**Desktop View (Tasks 6–7):**
- Create `src/ConstructionFinance.Desktop/Views/Finance/FinanceProjectGridView.axaml` (+ `.axaml.cs`) — custom grid + keyboard/paste wiring.

**Wiring (Task 8):**
- Modify `src/ConstructionFinance.Desktop/Common/HostBuilder.cs` — register `FinanceProjectGridViewModel`.
- Modify `src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs` — `NavigateToAsync` case.
- Modify `src/ConstructionFinance.Desktop/Views/MainWindow.axaml` — nav menu item.

**Tests:**
- `tests/ConstructionFinance.Data.Tests/Finance/FinanceProjectRepositoryTests.cs` (extend)
- `tests/ConstructionFinance.Application.Tests/Finance/FinanceProjectServiceTests.cs` (extend)
- `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceCellParsingTests.cs`
- `tests/ConstructionFinance.Desktop.Tests/Finance/LineItemRowViewModelTests.cs`
- `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceProjectGridViewModelTests.cs`
- `tests/ConstructionFinance.Desktop.Tests/Finance/ClipboardGridParserTests.cs`

**Grid column order (the canonical index used by parsing, paste, and XAML):**
`0` Ngày · `1` Gói thầu · `2` Tạm ứng · `3` TT KLHT · `4` Thu hồi · `5` GN NSNN · `6` GN Ngoại hối · *(derived, not editable:)* Giải ngân · ✓.
Editable columns are indices **0–6**; paste and `SetCell` only touch 0–6.

---

## Task 1: Repository replace-lines `UpdateAsync`

**Files:**
- Modify: `src/ConstructionFinance.Domain/Abstractions/IFinanceProjectRepository.cs`
- Modify: `src/ConstructionFinance.Data/Repositories/FinanceProjectRepository.cs`
- Test: `tests/ConstructionFinance.Data.Tests/Finance/FinanceProjectRepositoryTests.cs`

**Interfaces:**
- Consumes: existing `AppDbContext.FinanceProjects`, `AppDbContext.FinanceProjectLines`, `Project`, `ProjectLineItem`.
- Produces: `Task IFinanceProjectRepository.UpdateAsync(Project project, CancellationToken ct = default)` — updates the project's scalar fields and **replaces all** its line items with `project.Lines` (no-op if the id doesn't exist). Used by Task 2.

- [ ] **Step 1: Write the failing test**

Add to `tests/ConstructionFinance.Data.Tests/Finance/FinanceProjectRepositoryTests.cs`:

```csharp
    [Fact]
    public async Task UpdateAsync_replaces_lines_and_updates_scalars()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new ConstructionFinance.Data.Repositories.FinanceProjectRepository(ctx);

        var p = new Project { Name = "DA1", ContractValue = 1_000m };
        p.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 1, 5), Advance = 500m, DisbursedNSNN = 500m });
        var id = await repo.AddAsync(p);
        ctx.ChangeTracker.Clear();

        var edited = new Project { Id = id, Name = "DA1 (sửa)", ContractValue = 2_000m };
        edited.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 3, 9), PaymentKLHT = 700m, RecoveredAdvance = 100m, DisbursedNSNN = 600m });
        edited.Lines.Add(new ProjectLineItem { SortOrder = 2, Date = new DateOnly(2026, 4, 1), DisbursedNgoaiHoi = 50m });
        await repo.UpdateAsync(edited);
        ctx.ChangeTracker.Clear();

        var loaded = await repo.GetWithLinesAsync(id);
        loaded.ShouldNotBeNull();
        loaded!.Name.ShouldBe("DA1 (sửa)");
        loaded.ContractValue.ShouldBe(2_000m);
        loaded.Lines.Count.ShouldBe(2);
        loaded.Lines.OrderBy(l => l.SortOrder).First().PaymentKLHT.ShouldBe(700m);

        // old lines fully gone — only the 2 new rows remain in the table
        ctx.FinanceProjectLines.Count(l => l.ProjectId == id).ShouldBe(2);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Data.Tests --filter "FullyQualifiedName~UpdateAsync_replaces_lines"`
Expected: FAIL — `IFinanceProjectRepository` does not contain a definition for `UpdateAsync` (compile error).

- [ ] **Step 3: Add the interface method**

In `src/ConstructionFinance.Domain/Abstractions/IFinanceProjectRepository.cs`, add inside the interface (after `DeleteAsync`):

```csharp
    Task UpdateAsync(Project project, CancellationToken ct = default);
```

- [ ] **Step 4: Implement replace-lines update**

In `src/ConstructionFinance.Data/Repositories/FinanceProjectRepository.cs`, add this method to the class (after `GetWithLinesAsync`):

```csharp
    public async Task UpdateAsync(Project project, CancellationToken ct = default)
    {
        var existing = await _context.FinanceProjects
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == project.Id, ct);
        if (existing is null) return;

        existing.Code = project.Code;
        existing.Name = project.Name;
        existing.ContractorName = project.ContractorName;
        existing.ContractValue = project.ContractValue;
        existing.Notes = project.Notes;

        // Replace strategy: drop all current lines, re-add the incoming ones as
        // fresh rows. Single-user, small data — simplest correct approach and it
        // sidesteps identity-matching of edited rows.
        _context.FinanceProjectLines.RemoveRange(existing.Lines);
        existing.Lines.Clear();
        foreach (var line in project.Lines)
        {
            existing.Lines.Add(new ProjectLineItem
            {
                SortOrder = line.SortOrder,
                Date = line.Date,
                PackageName = line.PackageName,
                ContractRef = line.ContractRef,
                Advance = line.Advance,
                PaymentKLHT = line.PaymentKLHT,
                RecoveredAdvance = line.RecoveredAdvance,
                DisbursedNSNN = line.DisbursedNSNN,
                DisbursedNgoaiHoi = line.DisbursedNgoaiHoi
            });
        }
        await _context.SaveChangesAsync(ct);
    }
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Data.Tests --filter "FullyQualifiedName~UpdateAsync_replaces_lines"`
Expected: PASS.

- [ ] **Step 6: Run the full Data test suite (no regressions)**

Run: `dotnet test tests/ConstructionFinance.Data.Tests`
Expected: PASS (all green).

- [ ] **Step 7: Commit**

```bash
git add src/ConstructionFinance.Domain/Abstractions/IFinanceProjectRepository.cs \
        src/ConstructionFinance.Data/Repositories/FinanceProjectRepository.cs \
        tests/ConstructionFinance.Data.Tests/Finance/FinanceProjectRepositoryTests.cs
git commit -m "feat(finance): FinanceProjectRepository.UpdateAsync (replace-lines)"
```

---

## Task 2: Service surface for the grid (list / get-with-lines / update / delete)

**Files:**
- Modify: `src/ConstructionFinance.Application/Services/Interfaces/IFinanceProjectService.cs`
- Modify: `src/ConstructionFinance.Application/Services/Implementations/FinanceProjectService.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Finance/FinanceProjectServiceTests.cs`

**Interfaces:**
- Consumes: `IFinanceProjectRepository` (`AddAsync`, `GetWithLinesAsync`, `ListAsync`, `DeleteAsync`, and `UpdateAsync` from Task 1).
- Produces (added to `IFinanceProjectService`):
  - `Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default)` — projects without lines, for the picker.
  - `Task<Project?> GetWithLinesAsync(int id, CancellationToken ct = default)` — one project incl. lines, for editing.
  - `Task UpdateAsync(Project project, CancellationToken ct = default)`
  - `Task DeleteAsync(int id, CancellationToken ct = default)`
  - (existing, unchanged: `CreateAsync`, `GetAggregateAsync`)

- [ ] **Step 1: Write the failing test**

Add to `tests/ConstructionFinance.Application.Tests/Finance/FinanceProjectServiceTests.cs`:

```csharp
    [Fact]
    public async Task Update_then_GetWithLines_reflects_changes()
    {
        using var h = new ServiceTestHarness();
        var svc = new FinanceProjectService(new FinanceProjectRepository(h.Context));

        var p = new Project { Name = "DA1", ContractValue = 1_000m };
        p.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 1, 5), Advance = 500m, DisbursedNSNN = 500m });
        var id = await svc.CreateAsync(p);
        h.Context.ChangeTracker.Clear();

        var edited = new Project { Id = id, Name = "DA1b", ContractValue = 3_000m };
        edited.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 2, 2), PaymentKLHT = 900m, DisbursedNSNN = 900m });
        await svc.UpdateAsync(edited);
        h.Context.ChangeTracker.Clear();

        (await svc.ListAsync()).Count.ShouldBe(1);
        var loaded = await svc.GetWithLinesAsync(id);
        loaded.ShouldNotBeNull();
        loaded!.ContractValue.ShouldBe(3_000m);
        loaded.Lines.Single().PaymentKLHT.ShouldBe(900m);

        await svc.DeleteAsync(id);
        h.Context.ChangeTracker.Clear();
        (await svc.ListAsync()).ShouldBeEmpty();
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter "FullyQualifiedName~Update_then_GetWithLines"`
Expected: FAIL — `IFinanceProjectService` has no `ListAsync`/`GetWithLinesAsync`/`UpdateAsync`/`DeleteAsync` (compile error).

- [ ] **Step 3: Extend the interface**

In `src/ConstructionFinance.Application/Services/Interfaces/IFinanceProjectService.cs`, the full file becomes:

```csharp
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IFinanceProjectService
{
    Task<int> CreateAsync(Project project, CancellationToken ct = default);
    Task<ProjectAggregate?> GetAggregateAsync(int projectId, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default);
    Task<Project?> GetWithLinesAsync(int id, CancellationToken ct = default);
    Task UpdateAsync(Project project, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
```

- [ ] **Step 4: Implement the new methods**

In `src/ConstructionFinance.Application/Services/Implementations/FinanceProjectService.cs`, add to the class (after `GetAggregateAsync`):

```csharp
    public Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default) =>
        _repo.ListAsync(ct);

    public Task<Project?> GetWithLinesAsync(int id, CancellationToken ct = default) =>
        _repo.GetWithLinesAsync(id, ct);

    public Task UpdateAsync(Project project, CancellationToken ct = default) =>
        _repo.UpdateAsync(project, ct);

    public Task DeleteAsync(int id, CancellationToken ct = default) =>
        _repo.DeleteAsync(id, ct);
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter "FullyQualifiedName~Update_then_GetWithLines"`
Expected: PASS.

- [ ] **Step 6: Run the full Application test suite**

Run: `dotnet test tests/ConstructionFinance.Application.Tests`
Expected: PASS (all green).

- [ ] **Step 7: Commit**

```bash
git add src/ConstructionFinance.Application/Services/Interfaces/IFinanceProjectService.cs \
        src/ConstructionFinance.Application/Services/Implementations/FinanceProjectService.cs \
        tests/ConstructionFinance.Application.Tests/Finance/FinanceProjectServiceTests.cs
git commit -m "feat(finance): service list/get-with-lines/update/delete for entry grid"
```

---

## Task 3: `LineItemRowViewModel` + cell parsing (observable row, live calc)

**Files:**
- Create: `src/ConstructionFinance.Desktop/Common/FinanceCellParsing.cs`
- Create: `src/ConstructionFinance.Desktop/ViewModels/Finance/LineItemRowViewModel.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceCellParsingTests.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Finance/LineItemRowViewModelTests.cs`

**Interfaces:**
- Consumes: `ProjectCalculator.ComputeLine`, `LineItemInput` (`ConstructionFinance.Application.Calc`); `ProjectLineItem` (`ConstructionFinance.Domain.Finance`).
- Produces:
  - `static class FinanceCellParsing` with `static decimal ParseMoney(string? raw)` and `static DateOnly? ParseDate(string? raw)`.
  - `class LineItemRowViewModel : ObservableObject` with:
    - editable observable props: `DateOnly Date`, `string? PackageName`, `string? ContractRef`, `decimal Advance`, `decimal PaymentKLHT`, `decimal RecoveredAdvance`, `decimal DisbursedNSNN`, `decimal DisbursedNgoaiHoi`, `int SortOrder`.
    - derived read-only props: `decimal Disbursed`, `decimal Expected`, `bool IsBalanced`, `string KindLabel`.
    - `event EventHandler? InputChanged;`
    - `void SetCell(int columnIndex, string raw)` — assigns editable column 0–6 from a raw string (ignores other indices).
    - `LineItemInput ToInput()`, `ProjectLineItem ToEntity()`.
    - ctors: `LineItemRowViewModel()` (blank, today's date) and `LineItemRowViewModel(ProjectLineItem entity)`.
  - Used by Tasks 4, 5, 6, 7.

**ParseMoney contract (Vietnamese Excel paste):** strip spaces and `.` (thousands), treat `,` as the decimal separator; empty/blank/unparseable → `0`. Examples: `"1.234.567"→1234567`, `"1234567"→1234567`, `"1.000,50"→1000.50`, `""→0`, `"x"→0`.
**ParseDate contract:** accept `dd/MM/yyyy` and `yyyy-MM-dd`; unparseable/blank → `null`.

- [ ] **Step 1: Write the failing parsing test**

Create `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceCellParsingTests.cs`:

```csharp
using ConstructionFinance.Desktop.Common;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class FinanceCellParsingTests
{
    [Theory]
    [InlineData("1.234.567", 1234567)]
    [InlineData("1234567", 1234567)]
    [InlineData("1.000,50", 1000.50)]
    [InlineData("  2.000 ", 2000)]
    [InlineData("", 0)]
    [InlineData("x", 0)]
    public void ParseMoney_handles_vietnamese_formats(string raw, decimal expected) =>
        FinanceCellParsing.ParseMoney(raw).ShouldBe(expected);

    [Theory]
    [InlineData("05/03/2026", 2026, 3, 5)]
    [InlineData("2026-03-05", 2026, 3, 5)]
    public void ParseDate_accepts_two_formats(string raw, int y, int m, int d) =>
        FinanceCellParsing.ParseDate(raw).ShouldBe(new System.DateOnly(y, m, d));

    [Theory]
    [InlineData("")]
    [InlineData("not a date")]
    public void ParseDate_returns_null_when_unparseable(string raw) =>
        FinanceCellParsing.ParseDate(raw).ShouldBeNull();
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter "FullyQualifiedName~FinanceCellParsingTests"`
Expected: FAIL — `FinanceCellParsing` does not exist (compile error).

- [ ] **Step 3: Implement `FinanceCellParsing`**

Create `src/ConstructionFinance.Desktop/Common/FinanceCellParsing.cs`:

```csharp
using System.Globalization;

namespace ConstructionFinance.Desktop.Common;

/// Pure parsers for Excel-style cell text pasted/typed into the entry grid.
/// Vietnamese convention: '.' groups thousands, ',' is the decimal separator.
public static class FinanceCellParsing
{
    public static decimal ParseMoney(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0m;
        var cleaned = raw.Trim().Replace(" ", "").Replace(".", "").Replace(",", ".");
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0m;
    }

    private static readonly string[] DateFormats = { "dd/MM/yyyy", "yyyy-MM-dd" };

    public static DateOnly? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return DateOnly.TryParseExact(raw.Trim(), DateFormats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)
            ? date
            : null;
    }
}
```

- [ ] **Step 4: Run to verify parsing passes**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter "FullyQualifiedName~FinanceCellParsingTests"`
Expected: PASS.

- [ ] **Step 5: Write the failing row-VM test**

Create `tests/ConstructionFinance.Desktop.Tests/Finance/LineItemRowViewModelTests.cs`:

```csharp
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class LineItemRowViewModelTests
{
    [Fact]
    public void Editing_an_input_recomputes_derived_cells_and_raises_event()
    {
        var row = new LineItemRowViewModel();
        var fired = 0;
        row.InputChanged += (_, _) => fired++;

        row.PaymentKLHT = 800m;
        row.RecoveredAdvance = 200m;
        row.DisbursedNSNN = 600m;

        row.Disbursed.ShouldBe(600m);          // NSNN + NgoaiHoi
        row.Expected.ShouldBe(600m);           // payment: KLHT − RecoveredAdvance
        row.IsBalanced.ShouldBeTrue();
        row.KindLabel.ShouldBe("Thanh toán KLHT");
        fired.ShouldBe(3);                      // one per input change
    }

    [Fact]
    public void Advance_only_line_is_classified_as_ung_tien()
    {
        var row = new LineItemRowViewModel { Advance = 500m, DisbursedNSNN = 500m };
        row.KindLabel.ShouldBe("Ứng tiền");
        row.Expected.ShouldBe(500m);
        row.IsBalanced.ShouldBeTrue();
    }

    [Fact]
    public void Unbalanced_line_flags_IsBalanced_false()
    {
        var row = new LineItemRowViewModel { PaymentKLHT = 800m, RecoveredAdvance = 200m, DisbursedNSNN = 100m };
        row.IsBalanced.ShouldBeFalse();        // disbursed 100 ≠ expected 600
    }

    [Fact]
    public void SetCell_parses_money_and_date_columns()
    {
        var row = new LineItemRowViewModel();
        row.SetCell(0, "05/03/2026");
        row.SetCell(1, "Gói A");
        row.SetCell(2, "1.500.000");
        row.SetCell(5, "1.500.000");

        row.Date.ShouldBe(new System.DateOnly(2026, 3, 5));
        row.PackageName.ShouldBe("Gói A");
        row.Advance.ShouldBe(1_500_000m);
        row.DisbursedNSNN.ShouldBe(1_500_000m);
    }

    [Fact]
    public void Roundtrips_through_entity()
    {
        var entity = new ProjectLineItem { SortOrder = 4, Date = new System.DateOnly(2026, 2, 1), PackageName = "X", PaymentKLHT = 700m, DisbursedNSNN = 700m };
        var row = new LineItemRowViewModel(entity);
        row.PaymentKLHT.ShouldBe(700m);

        var back = row.ToEntity();
        back.SortOrder.ShouldBe(4);
        back.PaymentKLHT.ShouldBe(700m);
        back.PackageName.ShouldBe("X");

        var input = row.ToInput();
        input.ShouldBeOfType<LineItemInput>();
        input.DisbursedNSNN.ShouldBe(700m);
    }
}
```

- [ ] **Step 6: Run to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter "FullyQualifiedName~LineItemRowViewModelTests"`
Expected: FAIL — `LineItemRowViewModel` does not exist (compile error).

- [ ] **Step 7: Implement `LineItemRowViewModel`**

Create `src/ConstructionFinance.Desktop/ViewModels/Finance/LineItemRowViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// One đợt row in the entry grid. Holds only raw input cells; derived cells
/// (Disbursed/Expected/IsBalanced/KindLabel) are recomputed live by ProjectCalculator
/// on every input change. Raises InputChanged so the grid VM can refresh totals.
public partial class LineItemRowViewModel : ObservableObject
{
    [ObservableProperty] private int _sortOrder;
    [ObservableProperty] private DateOnly _date = DateOnly.FromDateTime(DateTime.Today);
    [ObservableProperty] private string? _packageName;
    [ObservableProperty] private string? _contractRef;
    [ObservableProperty] private decimal _advance;
    [ObservableProperty] private decimal _paymentKLHT;
    [ObservableProperty] private decimal _recoveredAdvance;
    [ObservableProperty] private decimal _disbursedNSNN;
    [ObservableProperty] private decimal _disbursedNgoaiHoi;

    [ObservableProperty] private decimal _disbursed;
    [ObservableProperty] private decimal _expected;
    [ObservableProperty] private bool _isBalanced = true;
    [ObservableProperty] private string _kindLabel = "Thanh toán KLHT";

    public event EventHandler? InputChanged;

    public LineItemRowViewModel() => Recompute();

    public LineItemRowViewModel(ProjectLineItem entity)
    {
        _sortOrder = entity.SortOrder;
        _date = entity.Date;
        _packageName = entity.PackageName;
        _contractRef = entity.ContractRef;
        _advance = entity.Advance;
        _paymentKLHT = entity.PaymentKLHT;
        _recoveredAdvance = entity.RecoveredAdvance;
        _disbursedNSNN = entity.DisbursedNSNN;
        _disbursedNgoaiHoi = entity.DisbursedNgoaiHoi;
        Recompute();
    }

    // Any raw input change → recompute derived cells + notify parent.
    partial void OnDateChanged(DateOnly value) => OnInputChanged();
    partial void OnAdvanceChanged(decimal value) => OnInputChanged();
    partial void OnPaymentKLHTChanged(decimal value) => OnInputChanged();
    partial void OnRecoveredAdvanceChanged(decimal value) => OnInputChanged();
    partial void OnDisbursedNSNNChanged(decimal value) => OnInputChanged();
    partial void OnDisbursedNgoaiHoiChanged(decimal value) => OnInputChanged();

    private void OnInputChanged()
    {
        Recompute();
        InputChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Recompute()
    {
        var computed = ProjectCalculator.ComputeLine(ToInput());
        Disbursed = computed.Disbursed;
        Expected = computed.Expected;
        IsBalanced = computed.IsBalanced;
        KindLabel = computed.Kind == LineItemKind.Advance ? "Ứng tiền" : "Thanh toán KLHT";
    }

    /// Assign one editable cell (columns 0–6) from raw clipboard/typed text.
    public void SetCell(int columnIndex, string raw)
    {
        switch (columnIndex)
        {
            case 0: Date = FinanceCellParsing.ParseDate(raw) ?? Date; break;
            case 1: PackageName = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim(); break;
            case 2: Advance = FinanceCellParsing.ParseMoney(raw); break;
            case 3: PaymentKLHT = FinanceCellParsing.ParseMoney(raw); break;
            case 4: RecoveredAdvance = FinanceCellParsing.ParseMoney(raw); break;
            case 5: DisbursedNSNN = FinanceCellParsing.ParseMoney(raw); break;
            case 6: DisbursedNgoaiHoi = FinanceCellParsing.ParseMoney(raw); break;
            // 7+ are derived/read-only — ignore.
        }
    }

    public LineItemInput ToInput() =>
        new(Date, PackageName, Advance, PaymentKLHT, RecoveredAdvance, DisbursedNSNN, DisbursedNgoaiHoi);

    public ProjectLineItem ToEntity() => new()
    {
        SortOrder = SortOrder,
        Date = Date,
        PackageName = PackageName,
        ContractRef = ContractRef,
        Advance = Advance,
        PaymentKLHT = PaymentKLHT,
        RecoveredAdvance = RecoveredAdvance,
        DisbursedNSNN = DisbursedNSNN,
        DisbursedNgoaiHoi = DisbursedNgoaiHoi
    };
}
```

- [ ] **Step 8: Run to verify row-VM tests pass**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter "FullyQualifiedName~LineItemRowViewModelTests"`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add src/ConstructionFinance.Desktop/Common/FinanceCellParsing.cs \
        src/ConstructionFinance.Desktop/ViewModels/Finance/LineItemRowViewModel.cs \
        tests/ConstructionFinance.Desktop.Tests/Finance/FinanceCellParsingTests.cs \
        tests/ConstructionFinance.Desktop.Tests/Finance/LineItemRowViewModelTests.cs
git commit -m "feat(finance): LineItemRowViewModel + cell parsing with live calc"
```

---

## Task 4: `FinanceProjectGridViewModel` (master–detail + live totals + persistence)

**Files:**
- Create: `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceProjectGridViewModel.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceProjectGridViewModelTests.cs`

**Interfaces:**
- Consumes: `IFinanceProjectService` (Task 2), `IDialogService`, `LineItemRowViewModel` (Task 3), `ProjectCalculator.Aggregate`, `Project`.
- Produces:
  - `class FinanceProjectGridViewModel : ViewModelBase` with:
    - `ObservableCollection<Project> Projects` (picker source; `Name` shown).
    - `Project? SelectedProject` (observable) — on change loads that project into the editor.
    - editable header props: `string ProjectName`, `string? ProjectCode`, `string? ContractorName`, `decimal ContractValue`, `string? Notes`.
    - `ObservableCollection<LineItemRowViewModel> Lines`.
    - `LineItemRowViewModel? SelectedRow` (observable).
    - live totals (observable, read-only to UI): `decimal SumAdvance`, `SumKLHT`, `SumRecovered`, `SumNSNN`, `SumNgoaiHoi`, `UnrecoveredAdvance`, `TotalDisbursed`.
    - `bool IsBusy`, `string? ErrorMessage`.
    - `Task LoadAsync()` — fills `Projects`.
    - `int CurrentProjectId` (0 = unsaved new).
    - commands: `NewProjectCommand`, `DeleteProjectCommand`, `SaveCommand`, `AddRowCommand`, `DeleteRowCommand`.
    - `void RecomputeTotals()` and `void ApplyPaste(int startRow, int startCol, string[][] block)` (paste logic lands in Task 5 but the method is declared here).
  - Used by Tasks 5, 6, 7, 8.

- [ ] **Step 1: Write the failing test**

Create `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceProjectGridViewModelTests.cs`:

```csharp
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Domain.Finance;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class FinanceProjectGridViewModelTests
{
    private static (FinanceProjectGridViewModel vm, Mock<IFinanceProjectService> svc) Create()
    {
        var svc = new Mock<IFinanceProjectService>(MockBehavior.Strict);
        svc.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project>());
        var dlg = new FakeDialogService();
        return (new FinanceProjectGridViewModel(svc.Object, dlg), svc);
    }

    [Fact]
    public void AddRow_appends_a_row_and_keeps_totals_live()
    {
        var (vm, _) = Create();
        vm.AddRowCommand.Execute(null);
        vm.Lines.Count.ShouldBe(1);

        vm.Lines[0].Advance = 500m;
        vm.Lines[0].DisbursedNSNN = 500m;

        vm.SumAdvance.ShouldBe(500m);
        vm.TotalDisbursed.ShouldBe(500m);
        vm.UnrecoveredAdvance.ShouldBe(500m);
    }

    [Fact]
    public void DeleteRow_removes_selection_and_updates_totals()
    {
        var (vm, _) = Create();
        vm.AddRowCommand.Execute(null);
        vm.Lines[0].DisbursedNSNN = 300m;
        vm.SelectedRow = vm.Lines[0];

        vm.DeleteRowCommand.Execute(null);

        vm.Lines.ShouldBeEmpty();
        vm.TotalDisbursed.ShouldBe(0m);
    }

    [Fact]
    public async Task Save_new_project_calls_CreateAsync_and_sets_current_id()
    {
        var (vm, svc) = Create();
        svc.Setup(s => s.CreateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);
        svc.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project> { new() { Id = 42, Name = "DA1" } });

        vm.ProjectName = "DA1";
        vm.ContractValue = 1_000m;
        vm.AddRowCommand.Execute(null);
        vm.Lines[0].DisbursedNSNN = 500m;

        await vm.SaveCommand.ExecuteAsync(null);

        svc.Verify(s => s.CreateAsync(It.Is<Project>(p => p.Name == "DA1" && p.Lines.Count == 1), It.IsAny<CancellationToken>()), Times.Once);
        vm.CurrentProjectId.ShouldBe(42);
    }

    [Fact]
    public async Task Save_existing_project_calls_UpdateAsync()
    {
        var (vm, svc) = Create();
        svc.Setup(s => s.GetWithLinesAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Project { Id = 7, Name = "DA7", ContractValue = 2_000m });
        svc.Setup(s => s.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        svc.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project> { new() { Id = 7, Name = "DA7" } });

        await vm.LoadProjectAsync(7);
        vm.CurrentProjectId.ShouldBe(7);

        await vm.SaveCommand.ExecuteAsync(null);
        svc.Verify(s => s.UpdateAsync(It.Is<Project>(p => p.Id == 7), It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter "FullyQualifiedName~FinanceProjectGridViewModelTests"`
Expected: FAIL — `FinanceProjectGridViewModel` does not exist (compile error).

- [ ] **Step 3: Implement `FinanceProjectGridViewModel`**

Create `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceProjectGridViewModel.cs`:

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

/// Master–detail entry grid: pick a project, edit its đợt rows Excel-style,
/// totals computed live by ProjectCalculator, persisted via the finance service.
public partial class FinanceProjectGridViewModel : ViewModelBase
{
    private readonly IFinanceProjectService _service;
    private readonly IDialogService _dialog;
    private bool _suppressSelectionLoad;

    public FinanceProjectGridViewModel(IFinanceProjectService service, IDialogService dialog)
    {
        _service = service;
        _dialog = dialog;
        Lines.CollectionChanged += OnLinesCollectionChanged;
    }

    public ObservableCollection<Project> Projects { get; } = new();
    public ObservableCollection<LineItemRowViewModel> Lines { get; } = new();

    [ObservableProperty] private Project? _selectedProject;
    [ObservableProperty] private LineItemRowViewModel? _selectedRow;

    [ObservableProperty] private int _currentProjectId;
    [ObservableProperty] private string _projectName = "";
    [ObservableProperty] private string? _projectCode;
    [ObservableProperty] private string? _contractorName;
    [ObservableProperty] private decimal _contractValue;
    [ObservableProperty] private string? _notes;

    [ObservableProperty] private decimal _sumAdvance;
    [ObservableProperty] private decimal _sumKLHT;
    [ObservableProperty] private decimal _sumRecovered;
    [ObservableProperty] private decimal _sumNSNN;
    [ObservableProperty] private decimal _sumNgoaiHoi;
    [ObservableProperty] private decimal _unrecoveredAdvance;
    [ObservableProperty] private decimal _totalDisbursed;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var list = await _service.ListAsync();
            Projects.Clear();
            foreach (var p in list) Projects.Add(p);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    partial void OnSelectedProjectChanged(Project? value)
    {
        if (_suppressSelectionLoad || value is null) return;
        _ = LoadProjectAsync(value.Id);
    }

    public async Task LoadProjectAsync(int id)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var project = await _service.GetWithLinesAsync(id);
            if (project is null) return;

            CurrentProjectId = project.Id;
            ProjectName = project.Name;
            ProjectCode = project.Code;
            ContractorName = project.ContractorName;
            ContractValue = project.ContractValue;
            Notes = project.Notes;

            ClearLines();
            foreach (var line in project.Lines.OrderBy(l => l.SortOrder))
                Lines.Add(new LineItemRowViewModel(line));
            RecomputeTotals();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void NewProject()
    {
        CurrentProjectId = 0;
        ProjectName = "";
        ProjectCode = null;
        ContractorName = null;
        ContractValue = 0m;
        Notes = null;
        ClearLines();
        Lines.Add(new LineItemRowViewModel());
        RecomputeTotals();
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

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            await _dialog.ShowMessageAsync("Thiếu thông tin", "Tên dự án không được để trống.");
            return;
        }

        var project = new Project
        {
            Id = CurrentProjectId,
            Name = ProjectName.Trim(),
            Code = ProjectCode,
            ContractorName = ContractorName,
            ContractValue = ContractValue,
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

            await LoadAsync();
            _suppressSelectionLoad = true;
            SelectedProject = Projects.FirstOrDefault(p => p.Id == CurrentProjectId);
            _suppressSelectionLoad = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            await _dialog.ShowMessageAsync("Lỗi", ex.Message);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteProjectAsync()
    {
        if (CurrentProjectId == 0) return;
        var ok = await _dialog.ConfirmAsync("Xóa dự án",
            $"Bạn có chắc muốn xóa dự án \"{ProjectName}\" và toàn bộ đợt của nó?");
        if (!ok) return;
        try
        {
            await _service.DeleteAsync(CurrentProjectId);
            await LoadAsync();
            NewProject();
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    /// Paste a TSV block (rows of cells) starting at the given cell; new rows are
    /// appended as needed. Only editable columns (0–6) are written. (Wired in Task 7.)
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
    }

    private void OnRowInputChanged(object? sender, EventArgs e) => RecomputeTotals();

    private void ClearLines()
    {
        foreach (var row in Lines) row.InputChanged -= OnRowInputChanged;
        Lines.Clear();
    }
}
```

- [ ] **Step 4: Run to verify the grid-VM tests pass**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter "FullyQualifiedName~FinanceProjectGridViewModelTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceProjectGridViewModel.cs \
        tests/ConstructionFinance.Desktop.Tests/Finance/FinanceProjectGridViewModelTests.cs
git commit -m "feat(finance): FinanceProjectGridViewModel — master-detail + live totals + persistence"
```

---

## Task 5: TSV clipboard parser + paste-block tests

**Files:**
- Create: `src/ConstructionFinance.Desktop/Common/ClipboardGridParser.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Finance/ClipboardGridParserTests.cs`

**Interfaces:**
- Consumes: nothing (pure string utility); exercises `FinanceProjectGridViewModel.ApplyPaste` (Task 4).
- Produces: `static class ClipboardGridParser` with `static string[][] Parse(string? raw)` — splits a clipboard TSV block into rows of cells. Splits on `\r\n`/`\n`/`\r`; drops a single trailing empty line; splits each line on `\t`. Empty/null input → empty array. Used by Task 7 code-behind.

- [ ] **Step 1: Write the failing test**

Create `tests/ConstructionFinance.Desktop.Tests/Finance/ClipboardGridParserTests.cs`:

```csharp
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Finance;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class ClipboardGridParserTests
{
    [Fact]
    public void Parse_splits_rows_and_tab_separated_cells()
    {
        var raw = "05/03/2026\tGói A\t1.000\r\n06/03/2026\tGói B\t2.000\r\n";
        var block = ClipboardGridParser.Parse(raw);

        block.Length.ShouldBe(2);
        block[0].ShouldBe(new[] { "05/03/2026", "Gói A", "1.000" });
        block[1][1].ShouldBe("Gói B");
    }

    [Fact]
    public void Parse_handles_single_cell_and_blank()
    {
        ClipboardGridParser.Parse("hello").ShouldBe(new[] { new[] { "hello" } });
        ClipboardGridParser.Parse("").Length.ShouldBe(0);
        ClipboardGridParser.Parse(null).Length.ShouldBe(0);
    }

    [Fact]
    public void ApplyPaste_fills_cells_and_extends_rows()
    {
        var svc = new Mock<IFinanceProjectService>(MockBehavior.Strict);
        svc.Setup(s => s.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Project>());
        var vm = new FinanceProjectGridViewModel(svc.Object, new FakeDialogService());

        // Paste a 2-row block of [Tạm ứng, TT KLHT] starting at row 0, column 2.
        var block = ClipboardGridParser.Parse("1.000\t0\r\n0\t2.000");
        vm.ApplyPaste(startRow: 0, startCol: 2, block);

        vm.Lines.Count.ShouldBe(2);
        vm.Lines[0].Advance.ShouldBe(1_000m);
        vm.Lines[1].PaymentKLHT.ShouldBe(2_000m);
        vm.SumAdvance.ShouldBe(1_000m);
        vm.SumKLHT.ShouldBe(2_000m);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter "FullyQualifiedName~ClipboardGridParserTests"`
Expected: FAIL — `ClipboardGridParser` does not exist (compile error).

- [ ] **Step 3: Implement `ClipboardGridParser`**

Create `src/ConstructionFinance.Desktop/Common/ClipboardGridParser.cs`:

```csharp
namespace ConstructionFinance.Desktop.Common;

/// Parses a clipboard block copied from Excel/Sheets (TSV) into rows of cells.
public static class ClipboardGridParser
{
    public static string[][] Parse(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return Array.Empty<string[]>();

        var lines = raw.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        // Excel appends a trailing newline → drop a single empty final line.
        var count = lines.Length;
        if (count > 0 && lines[count - 1].Length == 0) count--;

        var rows = new string[count][];
        for (var i = 0; i < count; i++)
            rows[i] = lines[i].Split('\t');
        return rows;
    }
}
```

- [ ] **Step 4: Run to verify paste tests pass**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter "FullyQualifiedName~ClipboardGridParserTests"`
Expected: PASS.

- [ ] **Step 5: Run the whole Desktop suite (no regressions across Tasks 3–5)**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests`
Expected: PASS (all green).

- [ ] **Step 6: Commit**

```bash
git add src/ConstructionFinance.Desktop/Common/ClipboardGridParser.cs \
        tests/ConstructionFinance.Desktop.Tests/Finance/ClipboardGridParserTests.cs
git commit -m "feat(finance): ClipboardGridParser (TSV) + paste-block apply"
```

---

## Task 6: Custom grid View — gridlines, frozen header + TOTAL row

**Files:**
- Create: `src/ConstructionFinance.Desktop/Views/Finance/FinanceProjectGridView.axaml`
- Create: `src/ConstructionFinance.Desktop/Views/Finance/FinanceProjectGridView.axaml.cs`

**Interfaces:**
- Consumes: `FinanceProjectGridViewModel` (Task 4) as `x:DataType`.
- Produces: a `UserControl` named `FinanceProjectGridView`. This task delivers a **rendered, manually-reviewed** screen (no automated UI test). The control exposes a `Border x:Name="GridRoot"` wrapping the rows host and an `ItemsControl x:Name="RowsHost"` — Task 7 attaches keyboard/paste handling to these.

> **Column widths (shared by header, rows, TOTAL):** define once as a constant pixel layout so the three Grids line up:
> `Ngày 110 · Gói thầu * · Tạm ứng 130 · TT KLHT 130 · Thu hồi 120 · GN NSNN 130 · GN Ngoại hối 130 · Giải ngân 130 · ✓ 44`.
> Use that exact `ColumnDefinitions` string in all three Grids.

- [ ] **Step 1: Create the View XAML**

Create `src/ConstructionFinance.Desktop/Views/Finance/FinanceProjectGridView.axaml`:

```xaml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:ui="using:FluentAvalonia.UI.Controls"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Finance"
             x:Class="ConstructionFinance.Desktop.Views.Finance.FinanceProjectGridView"
             x:DataType="vm:FinanceProjectGridViewModel">
    <DockPanel>
        <!-- Page header -->
        <StackPanel DockPanel.Dock="Top" Spacing="2" Margin="0,0,0,12">
            <TextBlock Classes="pageTitle" Text="Nhập liệu dự án (lưới)"/>
            <TextBlock Classes="pageSubtitle" Text="Nhập đợt như Excel — số tổng hợp tính sống"/>
        </StackPanel>

        <!-- Project picker + header fields + actions -->
        <Border DockPanel.Dock="Top" Classes="card" Margin="0,0,0,12">
            <StackPanel Spacing="10">
                <Grid ColumnDefinitions="Auto,*,Auto">
                    <StackPanel Grid.Column="0" Spacing="3">
                        <TextBlock Text="DỰ ÁN" Classes="muted" FontSize="11" FontWeight="SemiBold"/>
                        <ComboBox ItemsSource="{Binding Projects}"
                                  SelectedItem="{Binding SelectedProject}"
                                  MinWidth="320"
                                  DisplayMemberBinding="{Binding Name}"/>
                    </StackPanel>
                    <StackPanel Grid.Column="2" Orientation="Horizontal" Spacing="8" VerticalAlignment="Bottom">
                        <Button Classes="toolbar" Command="{Binding NewProjectCommand}">
                            <StackPanel Orientation="Horizontal" Spacing="6">
                                <ui:SymbolIcon Symbol="{x:Static ui:Symbol.Add}"/>
                                <TextBlock Text="Dự án mới"/>
                            </StackPanel>
                        </Button>
                        <Button Classes="accent toolbar" Command="{Binding SaveCommand}">
                            <StackPanel Orientation="Horizontal" Spacing="6">
                                <ui:SymbolIcon Symbol="{x:Static ui:Symbol.Save}"/>
                                <TextBlock Text="Lưu"/>
                            </StackPanel>
                        </Button>
                        <Button Classes="toolbar danger" Command="{Binding DeleteProjectCommand}">
                            <StackPanel Orientation="Horizontal" Spacing="6">
                                <ui:SymbolIcon Symbol="{x:Static ui:Symbol.Delete}"/>
                                <TextBlock Text="Xóa dự án"/>
                            </StackPanel>
                        </Button>
                    </StackPanel>
                </Grid>

                <Grid ColumnDefinitions="2*,*,Auto" RowDefinitions="Auto" ColumnSpacing="12">
                    <StackPanel Grid.Column="0" Spacing="3">
                        <TextBlock Text="Tên dự án" Classes="formLabel"/>
                        <TextBox Text="{Binding ProjectName}"/>
                    </StackPanel>
                    <StackPanel Grid.Column="1" Spacing="3">
                        <TextBlock Text="Nhà thầu" Classes="formLabel"/>
                        <TextBox Text="{Binding ContractorName}"/>
                    </StackPanel>
                    <StackPanel Grid.Column="2" Spacing="3">
                        <TextBlock Text="Giá trị HĐ" Classes="formLabel"/>
                        <TextBox Text="{Binding ContractValue, StringFormat=#\,##0}" MinWidth="160"
                                 TextAlignment="Right"/>
                    </StackPanel>
                </Grid>
            </StackPanel>
        </Border>

        <!-- Row toolbar -->
        <Border DockPanel.Dock="Top" Classes="toolbarStrip" Margin="0,0,0,0">
            <Grid ColumnDefinitions="Auto,*,Auto">
                <StackPanel Grid.Column="0" Orientation="Horizontal" Spacing="8">
                    <Button Classes="toolbar" Command="{Binding AddRowCommand}">
                        <StackPanel Orientation="Horizontal" Spacing="6">
                            <ui:SymbolIcon Symbol="{x:Static ui:Symbol.Add}"/>
                            <TextBlock Text="Thêm đợt"/>
                        </StackPanel>
                    </Button>
                    <Button Classes="toolbar danger" Command="{Binding DeleteRowCommand}">
                        <StackPanel Orientation="Horizontal" Spacing="6">
                            <ui:SymbolIcon Symbol="{x:Static ui:Symbol.Delete}"/>
                            <TextBlock Text="Xóa đợt"/>
                        </StackPanel>
                    </Button>
                    <TextBlock Text="Mẹo: Ctrl+V để dán khối từ Excel" Classes="muted"
                               VerticalAlignment="Center" Margin="8,0,0,0"/>
                </StackPanel>
                <TextBlock Grid.Column="1" Text="{Binding ErrorMessage}"
                           Foreground="{DynamicResource DangerBrush}"
                           VerticalAlignment="Center" HorizontalAlignment="Right" Margin="12,0"/>
                <ProgressBar Grid.Column="2" IsIndeterminate="True" IsVisible="{Binding IsBusy}"
                             Width="100" VerticalAlignment="Center"/>
            </Grid>
        </Border>

        <!-- The grid: frozen header (top), scrollable rows (center), frozen TOTAL (bottom) -->
        <Border Classes="tableCard" x:Name="GridRoot" Focusable="True">
            <DockPanel>
                <!-- Frozen header -->
                <Border DockPanel.Dock="Top" Background="{DynamicResource GridHeaderBrush}"
                        BorderBrush="{DynamicResource CardBorderBrush}" BorderThickness="0,0,0,1">
                    <Grid ColumnDefinitions="110,*,130,130,120,130,130,130,44">
                        <TextBlock Grid.Column="0" Text="Ngày" Classes="gridHeaderCell"/>
                        <TextBlock Grid.Column="1" Text="Gói thầu" Classes="gridHeaderCell"/>
                        <TextBlock Grid.Column="2" Text="Tạm ứng" Classes="gridHeaderCell" TextAlignment="Right"/>
                        <TextBlock Grid.Column="3" Text="TT KLHT" Classes="gridHeaderCell" TextAlignment="Right"/>
                        <TextBlock Grid.Column="4" Text="Thu hồi" Classes="gridHeaderCell" TextAlignment="Right"/>
                        <TextBlock Grid.Column="5" Text="GN NSNN" Classes="gridHeaderCell" TextAlignment="Right"/>
                        <TextBlock Grid.Column="6" Text="GN Ngoại hối" Classes="gridHeaderCell" TextAlignment="Right"/>
                        <TextBlock Grid.Column="7" Text="Giải ngân" Classes="gridHeaderCell" TextAlignment="Right"/>
                        <TextBlock Grid.Column="8" Text="✓" Classes="gridHeaderCell" TextAlignment="Center"/>
                    </Grid>
                </Border>

                <!-- Frozen TOTAL row -->
                <Border DockPanel.Dock="Bottom" Background="{DynamicResource GridHeaderBrush}"
                        BorderBrush="{DynamicResource CardBorderBrush}" BorderThickness="0,1,0,0">
                    <Grid ColumnDefinitions="110,*,130,130,120,130,130,130,44">
                        <TextBlock Grid.Column="0" Text="TỔNG" Classes="gridTotalCell"/>
                        <TextBlock Grid.Column="1" Text="{Binding UnrecoveredAdvance, StringFormat=TU chưa thu hồi: #\,##0}"
                                   Classes="gridTotalCell"/>
                        <TextBlock Grid.Column="2" Text="{Binding SumAdvance, StringFormat=#\,##0}" Classes="gridTotalCell" TextAlignment="Right"/>
                        <TextBlock Grid.Column="3" Text="{Binding SumKLHT, StringFormat=#\,##0}" Classes="gridTotalCell" TextAlignment="Right"/>
                        <TextBlock Grid.Column="4" Text="{Binding SumRecovered, StringFormat=#\,##0}" Classes="gridTotalCell" TextAlignment="Right"/>
                        <TextBlock Grid.Column="5" Text="{Binding SumNSNN, StringFormat=#\,##0}" Classes="gridTotalCell" TextAlignment="Right"/>
                        <TextBlock Grid.Column="6" Text="{Binding SumNgoaiHoi, StringFormat=#\,##0}" Classes="gridTotalCell" TextAlignment="Right"/>
                        <TextBlock Grid.Column="7" Text="{Binding TotalDisbursed, StringFormat=#\,##0}" Classes="gridTotalCell" TextAlignment="Right"/>
                        <TextBlock Grid.Column="8" Text="" Classes="gridTotalCell"/>
                    </Grid>
                </Border>

                <!-- Scrollable rows -->
                <ScrollViewer HorizontalScrollBarVisibility="Disabled" VerticalScrollBarVisibility="Auto">
                    <ItemsControl x:Name="RowsHost" ItemsSource="{Binding Lines}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate x:DataType="vm:LineItemRowViewModel">
                                <Grid ColumnDefinitions="110,*,130,130,120,130,130,130,44"
                                      Classes="gridRow">
                                    <TextBox Grid.Column="0" Classes="gridCell" Text="{Binding Date, StringFormat=dd/MM/yyyy}"/>
                                    <TextBox Grid.Column="1" Classes="gridCell" Text="{Binding PackageName}"/>
                                    <TextBox Grid.Column="2" Classes="gridCell num" Text="{Binding Advance, StringFormat=#\,##0}"/>
                                    <TextBox Grid.Column="3" Classes="gridCell num" Text="{Binding PaymentKLHT, StringFormat=#\,##0}"/>
                                    <TextBox Grid.Column="4" Classes="gridCell num" Text="{Binding RecoveredAdvance, StringFormat=#\,##0}"/>
                                    <TextBox Grid.Column="5" Classes="gridCell num" Text="{Binding DisbursedNSNN, StringFormat=#\,##0}"/>
                                    <TextBox Grid.Column="6" Classes="gridCell num" Text="{Binding DisbursedNgoaiHoi, StringFormat=#\,##0}"/>
                                    <TextBlock Grid.Column="7" Classes="gridCellRO num" Text="{Binding Disbursed, StringFormat=#\,##0}"/>
                                    <TextBlock Grid.Column="8" Classes="gridCellRO" TextAlignment="Center"
                                               Text="{Binding IsBalanced, Converter={x:Static vm:BalancedGlyphConverter.Instance}}"
                                               Foreground="{Binding IsBalanced, Converter={x:Static vm:BalancedBrushConverter.Instance}}"
                                               ToolTip.Tip="{Binding Expected, StringFormat=Cần khớp: #\,##0}"/>
                                </Grid>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </ScrollViewer>
            </DockPanel>
        </Border>
    </DockPanel>
</UserControl>
```

- [ ] **Step 2: Create the converters used by the ✓ cell**

Create `src/ConstructionFinance.Desktop/ViewModels/Finance/BalancedConverters.cs`:

```csharp
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// true → "✓", false → "✕" for the cross-check column.
public class BalancedGlyphConverter : IValueConverter
{
    public static readonly BalancedGlyphConverter Instance = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "✓" : "✕";
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// true → green, false → red, for the cross-check column.
public class BalancedBrushConverter : IValueConverter
{
    public static readonly BalancedBrushConverter Instance = new();
    private static readonly IBrush Ok = new SolidColorBrush(Color.Parse("#2E7D32"));
    private static readonly IBrush Bad = new SolidColorBrush(Color.Parse("#C0392B"));
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Ok : Bad;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

- [ ] **Step 3: Add grid cell styles**

Append to `src/ConstructionFinance.Desktop/Styles/Theme.axaml` (inside the root `<Styles>` element, near the other selectors):

```xaml
    <Style Selector="TextBlock.gridHeaderCell">
        <Setter Property="Padding" Value="8,8"/>
        <Setter Property="FontSize" Value="12"/>
        <Setter Property="FontWeight" Value="SemiBold"/>
        <Setter Property="VerticalAlignment" Value="Center"/>
    </Style>
    <Style Selector="TextBlock.gridTotalCell">
        <Setter Property="Padding" Value="8,8"/>
        <Setter Property="FontSize" Value="12"/>
        <Setter Property="FontWeight" Value="Bold"/>
        <Setter Property="VerticalAlignment" Value="Center"/>
    </Style>
    <Style Selector="Grid.gridRow">
        <Setter Property="MinHeight" Value="34"/>
    </Style>
    <Style Selector="TextBox.gridCell">
        <Setter Property="BorderBrush" Value="{DynamicResource GridLineBrush}"/>
        <Setter Property="BorderThickness" Value="0,0,1,1"/>
        <Setter Property="CornerRadius" Value="0"/>
        <Setter Property="Padding" Value="6,4"/>
        <Setter Property="FontSize" Value="13"/>
        <Setter Property="VerticalContentAlignment" Value="Center"/>
    </Style>
    <Style Selector="TextBox.gridCell.num">
        <Setter Property="TextAlignment" Value="Right"/>
    </Style>
    <Style Selector="TextBox.gridCell:focus">
        <Setter Property="BorderBrush" Value="{DynamicResource BrandPrimaryBrush}"/>
        <Setter Property="BorderThickness" Value="1"/>
    </Style>
    <Style Selector="TextBlock.gridCellRO">
        <Setter Property="Padding" Value="6,4"/>
        <Setter Property="FontSize" Value="13"/>
        <Setter Property="VerticalAlignment" Value="Center"/>
    </Style>
    <Style Selector="TextBlock.gridCellRO.num">
        <Setter Property="TextAlignment" Value="Right"/>
    </Style>
```

- [ ] **Step 4: Create the View code-behind (minimal for now)**

Create `src/ConstructionFinance.Desktop/Views/Finance/FinanceProjectGridView.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop.Views.Finance;

public partial class FinanceProjectGridView : UserControl
{
    public FinanceProjectGridView() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 5: Build the Desktop project**

Run: `dotnet build src/ConstructionFinance.Desktop`
Expected: Build succeeded (0 errors). Resolve any XAML/compiled-binding errors before proceeding.

- [ ] **Step 6: Commit**

```bash
git add src/ConstructionFinance.Desktop/Views/Finance/ \
        src/ConstructionFinance.Desktop/ViewModels/Finance/BalancedConverters.cs \
        src/ConstructionFinance.Desktop/Styles/Theme.axaml
git commit -m "feat(finance): Excel-like grid View — frozen header/total, gridlines, cross-check"
```

> **Manual review checkpoint** (after Task 8 wires navigation): the rendered grid is reviewed by running the app — see Task 8 Step 5.

---

## Task 7: Keyboard navigation + Ctrl+V paste wiring

**Files:**
- Modify: `src/ConstructionFinance.Desktop/Views/Finance/FinanceProjectGridView.axaml.cs`

**Interfaces:**
- Consumes: `FinanceProjectGridViewModel.ApplyPaste`, `ClipboardGridParser.Parse`, the `RowsHost` ItemsControl and `GridRoot` border from Task 6.
- Produces: keyboard behavior on the grid — Enter/↓ move to the cell below (Enter on the last row appends a đợt), ↑ moves up, Tab/Shift+Tab handled natively by focus order; Ctrl+V pastes a TSV block from the active cell. No new public surface.

> The pure logic (TSV parse + ApplyPaste) is already unit-tested (Tasks 4–5). This task is the thin Avalonia event plumbing, verified by manual review in Task 8.

- [ ] **Step 1: Implement keyboard + paste handlers**

Replace `src/ConstructionFinance.Desktop/Views/Finance/FinanceProjectGridView.axaml.cs` with:

```csharp
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.ViewModels.Finance;

namespace ConstructionFinance.Desktop.Views.Finance;

public partial class FinanceProjectGridView : UserControl
{
    public FinanceProjectGridView() => AvaloniaXamlLoader.Load(this);

    private ItemsControl RowsHost => this.FindControl<ItemsControl>("RowsHost")!;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // Tunnel so we react before the TextBox consumes the key.
        AddHandler(KeyDownEvent, OnGridKeyDown, RoutingStrategies.Tunnel);
    }

    private async void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not FinanceProjectGridViewModel vm) return;
        if (e.Source is not Control source) return;

        var (rowIndex, colIndex) = LocateCell(source);
        if (rowIndex < 0) return;

        // Ctrl+V → paste a TSV block from the active cell.
        if (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null) return;
            var text = await clipboard.GetTextAsync();
            if (string.IsNullOrEmpty(text)) return;

            var block = ClipboardGridParser.Parse(text);
            vm.ApplyPaste(rowIndex, colIndex, block);
            e.Handled = true;
            return;
        }

        // Enter / arrows → vertical cell movement (Tab handled by native focus order).
        switch (e.Key)
        {
            case Key.Enter:
            case Key.Down:
                if (e.Key == Key.Enter && rowIndex == vm.Lines.Count - 1)
                    vm.AddRowCommand.Execute(null);
                MoveFocus(rowIndex + 1, colIndex);
                e.Handled = true;
                break;
            case Key.Up:
                MoveFocus(rowIndex - 1, colIndex);
                e.Handled = true;
                break;
        }
    }

    /// Find which (row, column) a focused cell belongs to within RowsHost.
    private (int row, int col) LocateCell(Control source)
    {
        var cell = source as TextBox ?? source.FindAncestorOfType<TextBox>();
        if (cell is null) return (-1, -1);
        var col = Grid.GetColumn(cell);

        var rowGrid = cell.FindAncestorOfType<Grid>();
        if (rowGrid is null) return (-1, -1);
        var container = rowGrid.FindAncestorOfType<ContentPresenter>();
        if (container is null) return (-1, -1);
        var row = RowsHost.IndexFromContainer(container);
        return (row, col);
    }

    /// Focus the cell at (row, col), if it exists and is an editable TextBox.
    private void MoveFocus(int row, int col)
    {
        if (row < 0) return;
        var container = RowsHost.ContainerFromIndex(row);
        if (container is null) return;
        var grid = container.FindDescendantOfType<Grid>();
        if (grid is null) return;
        foreach (var child in grid.Children)
        {
            if (child is TextBox tb && Grid.GetColumn(tb) == col)
            {
                tb.Focus();
                tb.SelectAll();
                return;
            }
        }
    }
}
```

- [ ] **Step 2: Build the Desktop project**

Run: `dotnet build src/ConstructionFinance.Desktop`
Expected: Build succeeded (0 errors). (If `IndexFromContainer`/`ContainerFromIndex` resolve differently in the pinned Avalonia version, adjust to the available `ItemsControl` container API — the intent is index↔container mapping.)

- [ ] **Step 3: Commit**

```bash
git add src/ConstructionFinance.Desktop/Views/Finance/FinanceProjectGridView.axaml.cs
git commit -m "feat(finance): grid keyboard nav (Enter/arrows) + Ctrl+V paste-block"
```

---

## Task 8: Navigation wiring + DI registration + nav menu entry

**Files:**
- Modify: `src/ConstructionFinance.Desktop/Common/HostBuilder.cs`
- Modify: `src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/Views/MainWindow.axaml`

**Interfaces:**
- Consumes: `FinanceProjectGridViewModel` (Task 4), `INavigationService`, `IServiceProvider`.
- Produces: a "Nhập liệu (lưới)" nav item with `Tag="FinanceGrid"` that resolves & loads the grid screen. ViewLocator maps `ViewModels.Finance.FinanceProjectGridViewModel` → `Views.Finance.FinanceProjectGridView` by convention (no change needed).

- [ ] **Step 1: Register the ViewModel in DI**

In `src/ConstructionFinance.Desktop/Common/HostBuilder.cs`, add after the `ThongTriViewModel` registration (line ~49):

```csharp
                services.AddTransient<ViewModels.Finance.FinanceProjectGridViewModel>();
```

- [ ] **Step 2: Add the navigation case**

In `src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs`, add a new `case` inside `NavigateToAsync`'s `switch` (e.g. after the `"Projects"` case):

```csharp
            case "FinanceGrid":
                var fgvm = _services.GetRequiredService<ViewModels.Finance.FinanceProjectGridViewModel>();
                _nav.Reset();
                _nav.NavigateTo(fgvm);
                await fgvm.LoadAsync();
                break;
```

- [ ] **Step 3: Add the nav menu item**

In `src/ConstructionFinance.Desktop/Views/MainWindow.axaml`, add inside `<ui:NavigationView.MenuItems>`, under the "Dữ liệu nguồn" header (next to the existing `Dự án` item):

```xaml
        <ui:NavigationViewItem Content="Nhập liệu (lưới)" Tag="FinanceGrid" IconSource="Edit"/>
```

- [ ] **Step 4: Build the full solution**

Run: `dotnet build`
Expected: Build succeeded (0 errors).

- [ ] **Step 5: Manual review — run the app and exercise the grid**

Run: `dotnet run --project src/ConstructionFinance.Desktop`
Verify (ask the user to confirm each — subagents can't see the UI):
1. Nav → "Nhập liệu (lưới)" opens the screen; the grid shows a frozen header and a frozen TOTAL row with full gridlines.
2. "Dự án mới" → type Tên dự án + Giá trị HĐ; "Thêm đợt" adds a row; type Tạm ứng / TT KLHT / GN NSNN — the **Giải ngân** cell and the **TỔNG** row update live; the ✓ turns green when balanced, red ✕ when not.
3. Keyboard: Enter/↓/↑ move between cells; Enter on the last row appends a new đợt; Tab walks across cells.
4. Copy a block of cells from Excel → click a cell → **Ctrl+V** fills the block, extending rows as needed; totals update.
5. "Lưu" persists; reselect the project from the dropdown → rows reload identically. "Xóa đợt" / "Xóa dự án" work (with confirm).

- [ ] **Step 6: Run the entire test suite (final gate)**

Run: `dotnet test`
Expected: PASS — all green (Phase 1+2 baseline 285, plus the new Phase 3 tests).

- [ ] **Step 7: Commit**

```bash
git add src/ConstructionFinance.Desktop/Common/HostBuilder.cs \
        src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs \
        src/ConstructionFinance.Desktop/Views/MainWindow.axaml
git commit -m "feat(finance): wire entry-grid screen into navigation + DI"
```

---

## Self-Review Notes (author checklist, already applied)

- **Spec §5 coverage:** master–detail picker + `[+ Dự án mới]`/`[Xóa dự án]` (Task 4/6); columns Ngày…✓ + TỔNG row (Task 6); 4 Excel behaviors — gridlines + focus highlight (Task 6 styles), keyboard nav + type-over (Task 7), paste-block TSV (Tasks 5/7), frozen header+totals (Task 6 DockPanel); live calc via `ProjectCalculator` (Tasks 3/4); right-aligned thousands formatting (Task 6 `StringFormat`); blank-last-row → Enter appends (Task 7); Delete-row command (Task 4/6).
- **Spec §4 cross-check (Đ7):** `IsBalanced`/`Expected` surfaced per row with red/green ✓ + tooltip; unbalanced still saves (no save-time validation on balance).
- **Carry-forward items:** (a) `UpdateAsync`/replace-lines — Task 1 ✅; (b) self-commit vs UoW — repo self-commits, consistent with existing `AddAsync`/`DeleteAsync` ✅; (c) get-with-decisions accessor — **out of scope** for Phase 3 (decision linking belongs to Phase 4 Tổng hợp/truy vấn); noted, not built here.
- **Type consistency:** `LineItemRowViewModel` (Date/Advance/PaymentKLHT/RecoveredAdvance/DisbursedNSNN/DisbursedNgoaiHoi, `SetCell`, `ToInput`, `ToEntity`, `InputChanged`) used identically in Tasks 4–7; `ApplyPaste(int,int,string[][])` declared in Task 4, exercised in Task 5, called in Task 7; `ClipboardGridParser.Parse` signature consistent Tasks 5/7.
- **Placeholders:** none — every code step carries full content.
- **Out of scope (later phases):** Tổng hợp + truy-vấn-theo-tên screen (Phase 4); Xuất Excel from calc + importer (Phase 5); Thông tri (Phase 6); deleting legacy tables (final phase).
```
