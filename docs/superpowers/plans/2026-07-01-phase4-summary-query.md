# Phase 4 — Tổng hợp + truy vấn theo tên Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A new project-centric **Tổng hợp** screen: an instant **"Gõ tên dự án…"** filter shows that project's live-computed aggregate row (empty filter → all projects + a grand-total row), every figure computed live by `ProjectCalculator`; clicking a row opens that project's entry grid (Phase 3).

**Architecture:** Clean Architecture + MVVM. A pure `ProjectCalculator.Combine` folds many `ProjectAggregate`s into a grand total (single source of formulas, extended). A new `IFinanceProjectService.GetAllSummariesAsync` maps every project (with its lines) through `ProjectCalculator.Aggregate` into a `FinanceProjectSummary` DTO. The Desktop `FinanceSummaryViewModel` loads those, filters by name instantly, recomputes the grand total over the displayed rows via `Combine`, and navigates to the Phase-3 entry grid on row-open. The View is a **read-only `DataGrid`** (display/query — not Excel entry) with a filter box and a pinned grand-total row.

**Tech Stack:** .NET 8, Avalonia 11.2 + FluentAvalonia, CommunityToolkit.Mvvm, EF Core 8 + SQLite, xUnit + Shouldly + Moq.

## Global Constraints

- Free-license only — no commercial controls, no new package. Read-only `DataGrid` (already used by the legacy Tổng hợp / Decisions screens) is fine here.
- Money is `decimal`. Derived figures are **never stored** — computed live from raw cells via `ProjectCalculator` (the single source of every formula, including the new `Combine`).
- Conventions: `camelCase` locals/params, `_camelCase` private fields, `PascalCase` types/members. All UI strings Vietnamese.
- Strangler: keep the legacy "Bảng tổng hợp" screen untouched; **add** a new "Tổng hợp dự án" nav item beside it (user-confirmed). New finance UI lives under `…/Finance/`; new model in `ConstructionFinance.Domain.Finance`.
- Reuse Phase 3: row-open calls the existing `FinanceProjectGridViewModel.LoadProjectAsync(int)` then navigates to it.

---

## File Structure

**Data (Task 1):**
- Modify `src/ConstructionFinance.Domain/Abstractions/IFinanceProjectRepository.cs` — add `ListWithLinesAsync`.
- Modify `src/ConstructionFinance.Data/Repositories/FinanceProjectRepository.cs`.

**Application calc + service (Tasks 2–3):**
- Modify `src/ConstructionFinance.Application/Calc/CalcModels.cs` — add `FinanceProjectSummary` record.
- Modify `src/ConstructionFinance.Application/Calc/ProjectCalculator.cs` — add `Combine`.
- Modify `src/ConstructionFinance.Application/Services/Interfaces/IFinanceProjectService.cs` — add `GetAllSummariesAsync`.
- Modify `src/ConstructionFinance.Application/Services/Implementations/FinanceProjectService.cs`.

**Desktop (Tasks 4–6):**
- Create `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceSummaryRowVm.cs` — flat display row.
- Create `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceSummaryViewModel.cs` — load + filter + grand total + open-project.
- Create `src/ConstructionFinance.Desktop/Views/Finance/FinanceSummaryView.axaml` (+ `.axaml.cs`).

**Wiring (Task 7):**
- Modify `src/ConstructionFinance.Desktop/Common/HostBuilder.cs` — register `FinanceSummaryViewModel`.
- Modify `src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs` — `NavigateToAsync` case `"FinanceSummary"`.
- Modify `src/ConstructionFinance.Desktop/Views/MainWindow.axaml` — nav menu item.

**Tests:**
- `tests/ConstructionFinance.Data.Tests/Finance/FinanceProjectRepositoryTests.cs` (extend)
- `tests/ConstructionFinance.Application.Tests/Calc/ProjectCalculatorTests.cs` (extend — `Combine`)
- `tests/ConstructionFinance.Application.Tests/Finance/FinanceProjectServiceTests.cs` (extend — summaries)
- `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceSummaryRowVmTests.cs`
- `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceSummaryViewModelTests.cs`

**Summary grid columns (spec §6):** Tên dự án · Nhà thầu · Giá trị HĐ · Σ Tạm ứng · Σ Thu hồi · TU chưa thu hồi · GN NSNN · GN Ngoại hối · Tổng giải ngân · Quyết toán theo năm.

---

## Task 1: Repository `ListWithLinesAsync`

**Files:**
- Modify: `src/ConstructionFinance.Domain/Abstractions/IFinanceProjectRepository.cs`
- Modify: `src/ConstructionFinance.Data/Repositories/FinanceProjectRepository.cs`
- Test: `tests/ConstructionFinance.Data.Tests/Finance/FinanceProjectRepositoryTests.cs`

**Interfaces:**
- Consumes: `AppDbContext.FinanceProjects`, `Project`, `ProjectLineItem` (EF `Include`).
- Produces: `Task<IReadOnlyList<Project>> IFinanceProjectRepository.ListWithLinesAsync(CancellationToken ct = default)` — all projects, each with its `Lines` eager-loaded, ordered by `Name`. Used by Task 3.

- [ ] **Step 1: Write the failing test**

Add to `tests/ConstructionFinance.Data.Tests/Finance/FinanceProjectRepositoryTests.cs`:

```csharp
    [Fact]
    public async Task ListWithLinesAsync_returns_all_projects_with_their_lines()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new ConstructionFinance.Data.Repositories.FinanceProjectRepository(ctx);

        var a = new Project { Name = "Beta", ContractValue = 100m };
        a.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 1, 1), Advance = 50m, DisbursedNSNN = 50m });
        var b = new Project { Name = "Alpha", ContractValue = 200m };
        b.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 2, 1), PaymentKLHT = 80m, DisbursedNSNN = 80m });
        b.Lines.Add(new ProjectLineItem { SortOrder = 2, Date = new DateOnly(2026, 3, 1), DisbursedNgoaiHoi = 20m });
        await repo.AddAsync(a);
        await repo.AddAsync(b);
        ctx.ChangeTracker.Clear();

        var all = await repo.ListWithLinesAsync();
        all.Count.ShouldBe(2);
        all[0].Name.ShouldBe("Alpha");           // ordered by Name
        all[0].Lines.Count.ShouldBe(2);
        all[1].Name.ShouldBe("Beta");
        all[1].Lines.Single().Advance.ShouldBe(50m);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Data.Tests --filter "FullyQualifiedName~ListWithLinesAsync_returns_all"`
Expected: FAIL — `IFinanceProjectRepository` has no `ListWithLinesAsync` (compile error).

- [ ] **Step 3: Add the interface method**

In `src/ConstructionFinance.Domain/Abstractions/IFinanceProjectRepository.cs`, add after `ListAsync`:

```csharp
    Task<IReadOnlyList<Project>> ListWithLinesAsync(CancellationToken ct = default);
```

- [ ] **Step 4: Implement it**

In `src/ConstructionFinance.Data/Repositories/FinanceProjectRepository.cs`, add after `ListAsync`:

```csharp
    public async Task<IReadOnlyList<Project>> ListWithLinesAsync(CancellationToken ct = default) =>
        await _context.FinanceProjects
            .Include(p => p.Lines)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Data.Tests --filter "FullyQualifiedName~ListWithLinesAsync_returns_all"`
Expected: PASS.

- [ ] **Step 6: Run the full Data suite**

Run: `dotnet test tests/ConstructionFinance.Data.Tests`
Expected: PASS (all green).

- [ ] **Step 7: Commit**

```bash
git add src/ConstructionFinance.Domain/Abstractions/IFinanceProjectRepository.cs \
        src/ConstructionFinance.Data/Repositories/FinanceProjectRepository.cs \
        tests/ConstructionFinance.Data.Tests/Finance/FinanceProjectRepositoryTests.cs
git commit -m "feat(finance): FinanceProjectRepository.ListWithLinesAsync"
```

---

## Task 2: Calc — `FinanceProjectSummary` record + `ProjectCalculator.Combine`

**Files:**
- Modify: `src/ConstructionFinance.Application/Calc/CalcModels.cs`
- Modify: `src/ConstructionFinance.Application/Calc/ProjectCalculator.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Calc/ProjectCalculatorTests.cs`

**Interfaces:**
- Consumes: `ProjectAggregate`, `YearSettlement` (`ConstructionFinance.Application.Calc`).
- Produces:
  - `record FinanceProjectSummary(int ProjectId, string? Code, string Name, string? ContractorName, decimal ContractValue, ProjectAggregate Aggregate)` — one project's identity + its computed aggregate. Used by Tasks 3, 4, 5.
  - `static ProjectAggregate ProjectCalculator.Combine(IReadOnlyList<ProjectAggregate> aggregates)` — field-wise sum of many aggregates; yearly settlements merged by year (sum). Empty input → an all-zero aggregate with no yearly rows. Used by Task 5 (grand total).

- [ ] **Step 1: Write the failing test**

Add to `tests/ConstructionFinance.Application.Tests/Calc/ProjectCalculatorTests.cs`:

```csharp
    [Fact]
    public void Combine_sums_fields_and_merges_yearly_by_year()
    {
        var a = ProjectCalculator.Aggregate(new List<LineItemInput>
        {
            new(new DateOnly(2025, 5, 1), null, 0m, 100m, 0m, 60m, 40m),  // KLHT 100, NSNN 60, DTNH 40
        });
        var b = ProjectCalculator.Aggregate(new List<LineItemInput>
        {
            new(new DateOnly(2025, 6, 1), null, 200m, 0m, 50m, 200m, 0m),  // Advance 200, recovered 50, NSNN 200
            new(new DateOnly(2026, 1, 1), null, 0m, 300m, 0m, 0m, 300m),   // KLHT 300, DTNH 300
        });

        var total = ProjectCalculator.Combine(new[] { a, b });

        total.SumAdvance.ShouldBe(200m);
        total.SumRecovered.ShouldBe(50m);
        total.SumKLHT.ShouldBe(400m);
        total.SumNSNN.ShouldBe(260m);
        total.SumNgoaiHoi.ShouldBe(340m);
        total.UnrecoveredAdvance.ShouldBe(150m);   // 200 − 50
        total.TotalDisbursed.ShouldBe(600m);       // 260 + 340
        total.YearlySettlements.Count.ShouldBe(2); // 2025, 2026
        total.YearlySettlements.Single(y => y.Year == 2025).NSNN.ShouldBe(260m); // 60 + 200
    }

    [Fact]
    public void Combine_of_empty_is_all_zero()
    {
        var total = ProjectCalculator.Combine(System.Array.Empty<ProjectAggregate>());
        total.SumAdvance.ShouldBe(0m);
        total.TotalDisbursed.ShouldBe(0m);
        total.YearlySettlements.ShouldBeEmpty();
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter "FullyQualifiedName~Combine_"`
Expected: FAIL — `ProjectCalculator.Combine` not defined; `FinanceProjectSummary` not yet referenced (this test only needs `Combine`, so the failure is "Combine not defined").

- [ ] **Step 3: Add the `FinanceProjectSummary` record**

In `src/ConstructionFinance.Application/Calc/CalcModels.cs`, append:

```csharp
/// One project's identity plus its live-computed aggregate (for the Tổng hợp screen).
public record FinanceProjectSummary(
    int ProjectId,
    string? Code,
    string Name,
    string? ContractorName,
    decimal ContractValue,
    ProjectAggregate Aggregate);
```

- [ ] **Step 4: Implement `Combine`**

In `src/ConstructionFinance.Application/Calc/ProjectCalculator.cs`, add this method to the class (after `Aggregate`):

```csharp
    /// Fold many project aggregates into a grand total (the "Tổng cộng" row).
    /// Field-wise sum; yearly settlements merged by year.
    public static ProjectAggregate Combine(IReadOnlyList<ProjectAggregate> aggregates)
    {
        var yearly = aggregates
            .SelectMany(a => a.YearlySettlements)
            .GroupBy(y => y.Year)
            .OrderBy(g => g.Key)
            .Select(g => new YearSettlement(
                Year: g.Key,
                NSNN: g.Sum(y => y.NSNN),
                NgoaiHoi: g.Sum(y => y.NgoaiHoi),
                KLHT: g.Sum(y => y.KLHT),
                Advance: g.Sum(y => y.Advance)))
            .ToList();

        return new ProjectAggregate(
            SumAdvance: aggregates.Sum(a => a.SumAdvance),
            SumRecovered: aggregates.Sum(a => a.SumRecovered),
            SumKLHT: aggregates.Sum(a => a.SumKLHT),
            SumNSNN: aggregates.Sum(a => a.SumNSNN),
            SumNgoaiHoi: aggregates.Sum(a => a.SumNgoaiHoi),
            UnrecoveredAdvance: aggregates.Sum(a => a.UnrecoveredAdvance),
            TotalDisbursed: aggregates.Sum(a => a.TotalDisbursed),
            YearlySettlements: yearly);
    }
```

(`System.Linq` is already in scope in this file via the existing `Aggregate`/`BuildYearly` LINQ usage.)

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter "FullyQualifiedName~Combine_"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/ConstructionFinance.Application/Calc/CalcModels.cs \
        src/ConstructionFinance.Application/Calc/ProjectCalculator.cs \
        tests/ConstructionFinance.Application.Tests/Calc/ProjectCalculatorTests.cs
git commit -m "feat(finance): ProjectCalculator.Combine + FinanceProjectSummary record"
```

---

## Task 3: Service `GetAllSummariesAsync`

**Files:**
- Modify: `src/ConstructionFinance.Application/Services/Interfaces/IFinanceProjectService.cs`
- Modify: `src/ConstructionFinance.Application/Services/Implementations/FinanceProjectService.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Finance/FinanceProjectServiceTests.cs`

**Interfaces:**
- Consumes: `IFinanceProjectRepository.ListWithLinesAsync` (Task 1); `ProjectCalculator.Aggregate`; `FinanceProjectSummary` (Task 2); `LineItemMapping.ToCalcInput`.
- Produces: `Task<IReadOnlyList<FinanceProjectSummary>> IFinanceProjectService.GetAllSummariesAsync(CancellationToken ct = default)` — one `FinanceProjectSummary` per project, aggregate computed from its ordered lines. Used by Task 5.

- [ ] **Step 1: Write the failing test**

Add to `tests/ConstructionFinance.Application.Tests/Finance/FinanceProjectServiceTests.cs`:

```csharp
    [Fact]
    public async Task GetAllSummariesAsync_returns_one_computed_summary_per_project()
    {
        using var h = new ServiceTestHarness();
        var svc = new FinanceProjectService(new FinanceProjectRepository(h.Context));

        var p1 = new Project { Name = "DA1", ContractValue = 1_000m, ContractorName = "Nhà thầu A" };
        p1.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 1, 5), Advance = 500m, DisbursedNSNN = 500m });
        p1.Lines.Add(new ProjectLineItem { SortOrder = 2, Date = new DateOnly(2026, 2, 1), PaymentKLHT = 800m, RecoveredAdvance = 200m, DisbursedNSNN = 420m, DisbursedNgoaiHoi = 180m });
        var p2 = new Project { Name = "DA2", ContractValue = 2_000m };
        await svc.CreateAsync(p1);
        await svc.CreateAsync(p2);
        h.Context.ChangeTracker.Clear();

        var summaries = await svc.GetAllSummariesAsync();

        summaries.Count.ShouldBe(2);
        var s1 = summaries.Single(s => s.Name == "DA1");
        s1.ContractValue.ShouldBe(1_000m);
        s1.ContractorName.ShouldBe("Nhà thầu A");
        s1.Aggregate.UnrecoveredAdvance.ShouldBe(300m);   // 500 − 200
        s1.Aggregate.TotalDisbursed.ShouldBe(1_100m);     // (500) + (420+180)
        var s2 = summaries.Single(s => s.Name == "DA2");
        s2.Aggregate.TotalDisbursed.ShouldBe(0m);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter "FullyQualifiedName~GetAllSummariesAsync"`
Expected: FAIL — `IFinanceProjectService` has no `GetAllSummariesAsync` (compile error).

- [ ] **Step 3: Add the interface method**

In `src/ConstructionFinance.Application/Services/Interfaces/IFinanceProjectService.cs`, add after `GetAggregateAsync`:

```csharp
    Task<IReadOnlyList<FinanceProjectSummary>> GetAllSummariesAsync(CancellationToken ct = default);
```

- [ ] **Step 4: Implement it**

In `src/ConstructionFinance.Application/Services/Implementations/FinanceProjectService.cs`, add to the class (after `GetAggregateAsync`):

```csharp
    public async Task<IReadOnlyList<FinanceProjectSummary>> GetAllSummariesAsync(CancellationToken ct = default)
    {
        var projects = await _repo.ListWithLinesAsync(ct);
        return projects
            .Select(p => new FinanceProjectSummary(
                ProjectId: p.Id,
                Code: p.Code,
                Name: p.Name,
                ContractorName: p.ContractorName,
                ContractValue: p.ContractValue,
                Aggregate: ProjectCalculator.Aggregate(
                    p.Lines.OrderBy(l => l.SortOrder).Select(l => l.ToCalcInput()).ToList())))
            .ToList();
    }
```

(`System.Linq` and the `ConstructionFinance.Application.Calc` namespace are already imported in this file.)

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter "FullyQualifiedName~GetAllSummariesAsync"`
Expected: PASS.

- [ ] **Step 6: Run the full Application suite**

Run: `dotnet test tests/ConstructionFinance.Application.Tests`
Expected: PASS (all green).

- [ ] **Step 7: Commit**

```bash
git add src/ConstructionFinance.Application/Services/Interfaces/IFinanceProjectService.cs \
        src/ConstructionFinance.Application/Services/Implementations/FinanceProjectService.cs \
        tests/ConstructionFinance.Application.Tests/Finance/FinanceProjectServiceTests.cs
git commit -m "feat(finance): service GetAllSummariesAsync (live aggregate per project)"
```

---

## Task 4: `FinanceSummaryRowVm` (flat display row)

**Files:**
- Create: `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceSummaryRowVm.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceSummaryRowVmTests.cs`

**Interfaces:**
- Consumes: `FinanceProjectSummary`, `ProjectAggregate`, `YearSettlement` (`ConstructionFinance.Application.Calc`).
- Produces:
  - `class FinanceSummaryRowVm` with init-only props: `int ProjectId`, `string Name`, `string? ContractorName`, `decimal ContractValue`, `decimal SumAdvance`, `decimal SumRecovered`, `decimal UnrecoveredAdvance`, `decimal SumNSNN`, `decimal SumNgoaiHoi`, `decimal TotalDisbursed`, `string YearlySettlementsText`.
  - `static FinanceSummaryRowVm FromSummary(FinanceProjectSummary s)`.
  - `static string FormatYearly(IReadOnlyList<YearSettlement> years)` — `"2025: KLHT 100, NSNN 60 | 2026: …"`, only non-zero parts, years ascending.
  - Used by Tasks 5, 6.

- [ ] **Step 1: Write the failing test**

Create `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceSummaryRowVmTests.cs`:

```csharp
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Desktop.ViewModels.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class FinanceSummaryRowVmTests
{
    private static FinanceProjectSummary Summary() =>
        new(
            ProjectId: 7,
            Code: "DA-07",
            Name: "Cầu A12",
            ContractorName: "Nhà thầu A",
            ContractValue: 1_000m,
            Aggregate: ProjectCalculator.Aggregate(new List<LineItemInput>
            {
                new(new System.DateOnly(2025, 5, 1), null, 500m, 0m, 0m, 500m, 0m),
                new(new System.DateOnly(2026, 2, 1), null, 0m, 800m, 200m, 420m, 180m),
            }));

    [Fact]
    public void FromSummary_flattens_identity_and_aggregate()
    {
        var row = FinanceSummaryRowVm.FromSummary(Summary());

        row.ProjectId.ShouldBe(7);
        row.Name.ShouldBe("Cầu A12");
        row.ContractorName.ShouldBe("Nhà thầu A");
        row.ContractValue.ShouldBe(1_000m);
        row.SumAdvance.ShouldBe(500m);
        row.SumRecovered.ShouldBe(200m);
        row.UnrecoveredAdvance.ShouldBe(300m);
        row.SumNSNN.ShouldBe(920m);
        row.SumNgoaiHoi.ShouldBe(180m);
        row.TotalDisbursed.ShouldBe(1_100m);
    }

    [Fact]
    public void FormatYearly_lists_nonzero_parts_per_year_ascending()
    {
        var text = FinanceSummaryRowVm.FromSummary(Summary()).YearlySettlementsText;
        text.ShouldContain("2025:");
        text.ShouldContain("2026:");
        text.IndexOf("2025").ShouldBeLessThan(text.IndexOf("2026")); // ascending
        text.ShouldContain("Ứng 500");   // 2025 advance
        text.ShouldContain("KLHT 800");  // 2026 KLHT
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter "FullyQualifiedName~FinanceSummaryRowVmTests"`
Expected: FAIL — `FinanceSummaryRowVm` does not exist (compile error).

- [ ] **Step 3: Implement `FinanceSummaryRowVm`**

Create `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceSummaryRowVm.cs`:

```csharp
using ConstructionFinance.Application.Calc;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// Flat, read-only row for the Tổng hợp DataGrid. Built from a live-computed
/// FinanceProjectSummary; yearly settlements are pre-formatted to a text column.
public class FinanceSummaryRowVm
{
    public int ProjectId { get; init; }
    public string Name { get; init; } = "";
    public string? ContractorName { get; init; }
    public decimal ContractValue { get; init; }
    public decimal SumAdvance { get; init; }
    public decimal SumRecovered { get; init; }
    public decimal UnrecoveredAdvance { get; init; }
    public decimal SumNSNN { get; init; }
    public decimal SumNgoaiHoi { get; init; }
    public decimal TotalDisbursed { get; init; }
    public string YearlySettlementsText { get; init; } = "";

    public static FinanceSummaryRowVm FromSummary(FinanceProjectSummary s) => new()
    {
        ProjectId = s.ProjectId,
        Name = s.Name,
        ContractorName = s.ContractorName,
        ContractValue = s.ContractValue,
        SumAdvance = s.Aggregate.SumAdvance,
        SumRecovered = s.Aggregate.SumRecovered,
        UnrecoveredAdvance = s.Aggregate.UnrecoveredAdvance,
        SumNSNN = s.Aggregate.SumNSNN,
        SumNgoaiHoi = s.Aggregate.SumNgoaiHoi,
        TotalDisbursed = s.Aggregate.TotalDisbursed,
        YearlySettlementsText = FormatYearly(s.Aggregate.YearlySettlements)
    };

    public static string FormatYearly(IReadOnlyList<YearSettlement> years) =>
        string.Join(" | ", years.OrderBy(y => y.Year).Select(y =>
        {
            var parts = new List<string>();
            if (y.KLHT > 0) parts.Add($"KLHT {y.KLHT:#,##0}");
            if (y.Advance > 0) parts.Add($"Ứng {y.Advance:#,##0}");
            if (y.NSNN > 0) parts.Add($"NSNN {y.NSNN:#,##0}");
            if (y.NgoaiHoi > 0) parts.Add($"NgoaiHoi {y.NgoaiHoi:#,##0}");
            return $"{y.Year}: {string.Join(", ", parts)}";
        }));
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter "FullyQualifiedName~FinanceSummaryRowVmTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceSummaryRowVm.cs \
        tests/ConstructionFinance.Desktop.Tests/Finance/FinanceSummaryRowVmTests.cs
git commit -m "feat(finance): FinanceSummaryRowVm flat display row"
```

---

## Task 5: `FinanceSummaryViewModel` (load + instant filter + grand total + open-project)

**Files:**
- Create: `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceSummaryViewModel.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceSummaryViewModelTests.cs`

**Interfaces:**
- Consumes: `IFinanceProjectService.GetAllSummariesAsync` (Task 3); `ProjectCalculator.Combine` (Task 2); `FinanceSummaryRowVm` (Task 4); `INavigationService`; `IServiceProvider`; `FinanceProjectGridViewModel.LoadProjectAsync` (Phase 3).
- Produces:
  - `class FinanceSummaryViewModel : ViewModelBase` with:
    - `ObservableCollection<FinanceSummaryRowVm> Rows` (filtered view).
    - `string FilterText` (observable) — re-filters instantly on change.
    - `FinanceSummaryRowVm? SelectedRow` (observable).
    - grand-total observable props: `decimal GrandContractValue`, `GrandSumAdvance`, `GrandSumRecovered`, `GrandUnrecovered`, `GrandSumNSNN`, `GrandSumNgoaiHoi`, `GrandTotalDisbursed`.
    - `bool IsBusy`, `string? ErrorMessage`.
    - `Task LoadAsync()` — fetches all summaries, caches them, applies the (empty) filter.
    - commands: `OpenProjectCommand` (opens the entry grid for `SelectedRow`), `RefreshCommand`.
  - Used by Tasks 6, 7.

- [ ] **Step 1: Write the failing test**

Create `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceSummaryViewModelTests.cs`:

```csharp
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Finance;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class FinanceSummaryViewModelTests
{
    private static FinanceProjectSummary S(int id, string name, decimal advance, decimal nsnn) =>
        new(id, null, name, null, 1_000m,
            ProjectCalculator.Aggregate(new List<LineItemInput>
            {
                new(new System.DateOnly(2026, 1, 1), null, advance, 0m, 0m, nsnn, 0m),
            }));

    private static FinanceSummaryViewModel Create(out Mock<IFinanceProjectService> svc)
    {
        svc = new Mock<IFinanceProjectService>(MockBehavior.Strict);
        svc.Setup(s => s.GetAllSummariesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FinanceProjectSummary> { S(1, "Cầu A12", 500m, 500m), S(2, "Đường B3", 0m, 300m) });
        var nav = new FakeNavigationService();
        var services = new ServiceCollection().BuildServiceProvider();
        return new FinanceSummaryViewModel(svc.Object, nav, services);
    }

    [Fact]
    public async Task LoadAsync_shows_all_rows_and_grand_total()
    {
        var vm = Create(out _);
        await vm.LoadAsync();

        vm.Rows.Count.ShouldBe(2);
        vm.GrandSumAdvance.ShouldBe(500m);
        vm.GrandSumNSNN.ShouldBe(800m);            // 500 + 300
        vm.GrandTotalDisbursed.ShouldBe(800m);
    }

    [Fact]
    public async Task FilterText_narrows_rows_and_grand_total_instantly()
    {
        var vm = Create(out _);
        await vm.LoadAsync();

        vm.FilterText = "cầu";                      // case-insensitive substring
        vm.Rows.Count.ShouldBe(1);
        vm.Rows[0].Name.ShouldBe("Cầu A12");
        vm.GrandSumNSNN.ShouldBe(500m);            // only the matching project
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter "FullyQualifiedName~FinanceSummaryViewModelTests"`
Expected: FAIL — `FinanceSummaryViewModel` does not exist (compile error).

- [ ] **Step 3: Implement `FinanceSummaryViewModel`**

Create `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceSummaryViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// Project-centric Tổng hợp: instant name filter → live aggregate rows + grand
/// total; open a row to edit that project in the Phase-3 entry grid.
public partial class FinanceSummaryViewModel : ViewModelBase
{
    private readonly IFinanceProjectService _service;
    private readonly INavigationService _nav;
    private readonly IServiceProvider _services;
    private List<FinanceProjectSummary> _all = new();

    public FinanceSummaryViewModel(IFinanceProjectService service, INavigationService nav, IServiceProvider services)
    {
        _service = service;
        _nav = nav;
        _services = services;
    }

    public ObservableCollection<FinanceSummaryRowVm> Rows { get; } = new();

    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private FinanceSummaryRowVm? _selectedRow;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    [ObservableProperty] private decimal _grandContractValue;
    [ObservableProperty] private decimal _grandSumAdvance;
    [ObservableProperty] private decimal _grandSumRecovered;
    [ObservableProperty] private decimal _grandUnrecovered;
    [ObservableProperty] private decimal _grandSumNSNN;
    [ObservableProperty] private decimal _grandSumNgoaiHoi;
    [ObservableProperty] private decimal _grandTotalDisbursed;

    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            _all = (await _service.GetAllSummariesAsync()).ToList();
            ApplyFilter();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var needle = FilterText?.Trim() ?? "";
        var matches = string.IsNullOrEmpty(needle)
            ? _all
            : _all.Where(s => s.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)).ToList();

        Rows.Clear();
        foreach (var s in matches) Rows.Add(FinanceSummaryRowVm.FromSummary(s));

        var total = ProjectCalculator.Combine(matches.Select(s => s.Aggregate).ToList());
        GrandContractValue = matches.Sum(s => s.ContractValue);
        GrandSumAdvance = total.SumAdvance;
        GrandSumRecovered = total.SumRecovered;
        GrandUnrecovered = total.UnrecoveredAdvance;
        GrandSumNSNN = total.SumNSNN;
        GrandSumNgoaiHoi = total.SumNgoaiHoi;
        GrandTotalDisbursed = total.TotalDisbursed;
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        if (SelectedRow is null) return;
        var grid = _services.GetRequiredService<FinanceProjectGridViewModel>();
        await grid.LoadProjectAsync(SelectedRow.ProjectId);
        _nav.NavigateTo(grid);
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter "FullyQualifiedName~FinanceSummaryViewModelTests"`
Expected: PASS.

- [ ] **Step 5: Run the full Desktop suite**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests`
Expected: PASS (all green).

- [ ] **Step 6: Commit**

```bash
git add src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceSummaryViewModel.cs \
        tests/ConstructionFinance.Desktop.Tests/Finance/FinanceSummaryViewModelTests.cs
git commit -m "feat(finance): FinanceSummaryViewModel — instant name filter + live grand total"
```

---

## Task 6: `FinanceSummaryView` (filter box + read-only DataGrid + grand-total row)

**Files:**
- Create: `src/ConstructionFinance.Desktop/Views/Finance/FinanceSummaryView.axaml`
- Create: `src/ConstructionFinance.Desktop/Views/Finance/FinanceSummaryView.axaml.cs`

**Interfaces:**
- Consumes: `FinanceSummaryViewModel` (Task 5) as `x:DataType`.
- Produces: a `UserControl` named `FinanceSummaryView`. Deliverable is a clean build; visual behavior is reviewed in the manual run (Task 7). Double-clicking a row, or the "Mở dự án" button, invokes `OpenProjectCommand`.

- [ ] **Step 1: Create the View XAML**

Create `src/ConstructionFinance.Desktop/Views/Finance/FinanceSummaryView.axaml`:

```xaml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:ui="using:FluentAvalonia.UI.Controls"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Finance"
             x:Class="ConstructionFinance.Desktop.Views.Finance.FinanceSummaryView"
             x:DataType="vm:FinanceSummaryViewModel">
    <DockPanel>
        <StackPanel DockPanel.Dock="Top" Spacing="2" Margin="0,0,0,12">
            <TextBlock Classes="pageTitle" Text="Tổng hợp dự án"/>
            <TextBlock Classes="pageSubtitle" Text="Gõ tên dự án để lọc — số tổng hợp tính sống"/>
        </StackPanel>

        <Border Classes="tableCard">
            <DockPanel>
                <!-- Filter + actions -->
                <Border DockPanel.Dock="Top" Classes="toolbarStrip">
                    <Grid ColumnDefinitions="Auto,*,Auto,Auto">
                        <TextBlock Grid.Column="0" Text="Gõ tên dự án…" Classes="muted"
                                   VerticalAlignment="Center" Margin="0,0,8,0"/>
                        <TextBox Grid.Column="1" Text="{Binding FilterText}" MaxWidth="360"
                                 HorizontalAlignment="Left" Watermark="Tên dự án"/>
                        <ProgressBar Grid.Column="2" IsIndeterminate="True" IsVisible="{Binding IsBusy}"
                                     Width="100" VerticalAlignment="Center" Margin="8,0"/>
                        <Button Grid.Column="3" Classes="toolbar" Command="{Binding OpenProjectCommand}">
                            <StackPanel Orientation="Horizontal" Spacing="6">
                                <ui:SymbolIcon Symbol="{x:Static ui:Symbol.OpenLocal}"/>
                                <TextBlock Text="Mở dự án"/>
                            </StackPanel>
                        </Button>
                    </Grid>
                </Border>

                <!-- Grand-total row pinned at the bottom -->
                <Border DockPanel.Dock="Bottom" Background="{DynamicResource GridHeaderBrush}"
                        BorderBrush="{DynamicResource CardBorderBrush}" BorderThickness="0,1,0,0"
                        Padding="12,8">
                    <Grid ColumnDefinitions="*,Auto,Auto,Auto,Auto,Auto,Auto">
                        <TextBlock Grid.Column="0" Text="TỔNG CỘNG" FontWeight="Bold" VerticalAlignment="Center"/>
                        <TextBlock Grid.Column="1" Text="{Binding GrandContractValue, StringFormat=HĐ #\,##0}" Margin="16,0" FontWeight="SemiBold"/>
                        <TextBlock Grid.Column="2" Text="{Binding GrandSumAdvance, StringFormat=Ứng #\,##0}" Margin="16,0"/>
                        <TextBlock Grid.Column="3" Text="{Binding GrandUnrecovered, StringFormat=TU chưa TH #\,##0}" Margin="16,0"/>
                        <TextBlock Grid.Column="4" Text="{Binding GrandSumNSNN, StringFormat=NSNN #\,##0}" Margin="16,0"/>
                        <TextBlock Grid.Column="5" Text="{Binding GrandSumNgoaiHoi, StringFormat=NgoaiHoi #\,##0}" Margin="16,0"/>
                        <TextBlock Grid.Column="6" Text="{Binding GrandTotalDisbursed, StringFormat=Giải ngân #\,##0}" Margin="16,0" FontWeight="Bold"/>
                    </Grid>
                </Border>

                <!-- The data grid (read-only) -->
                <DataGrid ItemsSource="{Binding Rows}"
                          SelectedItem="{Binding SelectedRow}"
                          IsReadOnly="True"
                          HeadersVisibility="Column"
                          DoubleTapped="OnRowDoubleTapped">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="Tên dự án" Binding="{Binding Name}" Width="220"/>
                        <DataGridTextColumn Header="Nhà thầu" Binding="{Binding ContractorName}" Width="160"/>
                        <DataGridTextColumn Header="Giá trị HĐ" Binding="{Binding ContractValue, StringFormat=#\,##0}" Width="130"/>
                        <DataGridTextColumn Header="Σ Tạm ứng" Binding="{Binding SumAdvance, StringFormat=#\,##0}" Width="120"/>
                        <DataGridTextColumn Header="Σ Thu hồi" Binding="{Binding SumRecovered, StringFormat=#\,##0}" Width="120"/>
                        <DataGridTextColumn Header="TU chưa thu hồi" Binding="{Binding UnrecoveredAdvance, StringFormat=#\,##0}" Width="140"/>
                        <DataGridTextColumn Header="GN NSNN" Binding="{Binding SumNSNN, StringFormat=#\,##0}" Width="120"/>
                        <DataGridTextColumn Header="GN Ngoại hối" Binding="{Binding SumNgoaiHoi, StringFormat=#\,##0}" Width="120"/>
                        <DataGridTextColumn Header="Tổng giải ngân" Binding="{Binding TotalDisbursed, StringFormat=#\,##0}" Width="140"/>
                        <DataGridTextColumn Header="Quyết toán theo năm" Binding="{Binding YearlySettlementsText}" Width="*"/>
                    </DataGrid.Columns>
                </DataGrid>
            </DockPanel>
        </Border>
    </DockPanel>
</UserControl>
```

- [ ] **Step 2: Create the code-behind (double-tap opens the project)**

Create `src/ConstructionFinance.Desktop/Views/Finance/FinanceSummaryView.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ConstructionFinance.Desktop.ViewModels.Finance;

namespace ConstructionFinance.Desktop.Views.Finance;

public partial class FinanceSummaryView : UserControl
{
    public FinanceSummaryView() => AvaloniaXamlLoader.Load(this);

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is FinanceSummaryViewModel vm && vm.OpenProjectCommand.CanExecute(null))
            vm.OpenProjectCommand.Execute(null);
    }
}
```

- [ ] **Step 3: Build the Desktop project**

Run: `dotnet build src/ConstructionFinance.Desktop`
Expected: Build succeeded (0 errors). Resolve any compiled-binding error before proceeding. (If `DoubleTapped`/`TappedEventArgs` differ in the pinned Avalonia version, adapt to the available double-click event; the intent is "double-click a row → OpenProjectCommand".)

- [ ] **Step 4: Commit**

```bash
git add src/ConstructionFinance.Desktop/Views/Finance/FinanceSummaryView.axaml \
        src/ConstructionFinance.Desktop/Views/Finance/FinanceSummaryView.axaml.cs
git commit -m "feat(finance): Tổng hợp dự án View — name filter + read-only grid + grand total"
```

---

## Task 7: Wiring (DI + nav case + menu item) + run

**Files:**
- Modify: `src/ConstructionFinance.Desktop/Common/HostBuilder.cs`
- Modify: `src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/Views/MainWindow.axaml`

**Interfaces:**
- Consumes: `FinanceSummaryViewModel` (Task 5); `INavigationService`.
- Produces: a "Tổng hợp dự án" nav item (`Tag="FinanceSummary"`) that resolves & loads the screen. ViewLocator maps `ViewModels.Finance.FinanceSummaryViewModel` → `Views.Finance.FinanceSummaryView` automatically.

- [ ] **Step 1: Register the ViewModel**

In `src/ConstructionFinance.Desktop/Common/HostBuilder.cs`, add after the existing `FinanceProjectGridViewModel` registration:

```csharp
                services.AddTransient<ViewModels.Finance.FinanceSummaryViewModel>();
```

- [ ] **Step 2: Add the navigation case**

In `src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs`, add a case inside `NavigateToAsync`'s switch (e.g. after `case "FinanceGrid":`):

```csharp
            case "FinanceSummary":
                var fsvm = _services.GetRequiredService<ViewModels.Finance.FinanceSummaryViewModel>();
                _nav.Reset();
                _nav.NavigateTo(fsvm);
                await fsvm.LoadAsync();
                break;
```

- [ ] **Step 3: Add the nav menu item**

In `src/ConstructionFinance.Desktop/Views/MainWindow.axaml`, add under the "Tổng hợp" header group (next to the legacy "Bảng tổng hợp" item):

```xaml
        <ui:NavigationViewItem Content="Tổng hợp dự án" Tag="FinanceSummary" IconSource="ViewAll"/>
```

- [ ] **Step 4: Build the whole solution**

Run: `dotnet build`
Expected: Build succeeded (0 errors).

- [ ] **Step 5: Run the entire test suite**

Run: `dotnet test`
Expected: PASS — all green (Phase 1–3 baseline + the new Phase 4 tests).

- [ ] **Step 6: Manual review — run the app (controller-run; no display for subagents)**

Run: `DISPLAY=:1 dotnet run --project src/ConstructionFinance.Desktop`
Verify (confirm with the user):
1. Nav → "Tổng hợp dự án" opens the screen; all finance projects appear with their live figures; the **TỔNG CỘNG** row sums them.
2. Typing in **"Gõ tên dự án…"** filters instantly; the grand total reflects only matching rows; clearing shows all again.
3. **Double-click a row** (or select + "Mở dự án") opens that project in the entry grid; editing + saving there, then returning to Tổng hợp + Refresh, shows the updated figures.
4. The legacy "Bảng tổng hợp" item still works unchanged (strangler intact).

- [ ] **Step 7: Commit**

```bash
git add src/ConstructionFinance.Desktop/Common/HostBuilder.cs \
        src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs \
        src/ConstructionFinance.Desktop/Views/MainWindow.axaml
git commit -m "feat(finance): wire Tổng hợp dự án screen into navigation + DI"
```

---

## Self-Review Notes (author checklist, already applied)

- **Spec §6 coverage:** "Gõ tên dự án…" instant filter (Task 5 `OnFilterTextChanged`/`ApplyFilter`); name → that project's live aggregate row, empty → all + Tổng cộng (Task 5 grand total over displayed rows + Task 6 pinned TỔNG CỘNG); columns Giá trị HĐ / Σ Tạm ứng / Σ Thu hồi / TU chưa thu hồi / GN NSNN / GN Ngoại hối / Tổng giải ngân / Quyết toán theo năm (Task 6 DataGrid); click a project → open its entry grid (Task 5 `OpenProjectCommand` reusing Phase-3 `LoadProjectAsync`, Task 6 double-tap).
- **Spec §4.3 (global):** one `ProjectAggregate` per project (Task 3), total = combine of projects (Task 2 `Combine`) — single source of formulas extended, no parallel re-implementation.
- **Single-source / no stored derived:** all figures flow from `ProjectCalculator.Aggregate`/`Combine`; nothing derived is persisted (read-only screen).
- **Strangler / decision:** new nav item beside the legacy "Bảng tổng hợp" (user-confirmed "keep both"); legacy untouched.
- **Type consistency:** `FinanceProjectSummary(ProjectId, Code, Name, ContractorName, ContractValue, Aggregate)` defined in Task 2, produced in Task 3, consumed in Tasks 4–5; `FinanceSummaryRowVm.FromSummary` used in Tasks 4–6; `Combine(IReadOnlyList<ProjectAggregate>)` defined Task 2, used Task 5.
- **Placeholders:** none — every code step carries full content.
- **Carry-forward (c) get-with-decisions accessor:** still **deferred** — spec §6 aggregate has no decision columns; this belongs to the later Export/Thông tri work, not Phase 4.
- **Out of scope (later phases):** Xuất Excel from calc + importer (Phase 5); Thông tri (Phase 6); deleting legacy tables (final phase).
```
