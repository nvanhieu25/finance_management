# Phase 7C — Tổng hợp + Quyết toán năm Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the three aggregation surfaces of the Vị trí workbook — a per-project "Tổng hợp" tab, a per-project "Quyết toán năm" tab, and an all-projects "Tổng hợp" nav screen — all live-computed from the Vị trí tree via the existing `TreeCalculator`.

**Architecture:** A new pure `WorkbookSummaryBuilder` turns Vị trí trees into flat, ordered summary rows (Project subtotal → Vị trí rows → grand total, mirroring the `Mẫu.xlsx` "Tổng hợp" sheet). Two read-only tab view-models (`ProjectTongHopViewModel`, `YearSettlementViewModel`) recompute live from the in-memory Vị trí tabs and are folded into the existing workbook's bottom tab strip as `[Tổng hợp | Vị trí… | Quyết toán năm]`. A separate `WorkbookSummaryViewModel` + View + nav item renders the multi-project screen with an instant name filter. No new calc math — `TreeCalculator.AggregateViTri`/`Roll` and `TreeAggregate.Yearly` already exist from 7A.

**Tech Stack:** .NET 8, Avalonia 11 (FluentAvalonia NavigationView), EF Core 8 + SQLite, CommunityToolkit.Mvvm, ClosedXML; tests xUnit + Moq + Shouldly. 100% free-license.

## Global Constraints

- **Free-license only.** No Syncfusion / QuestPDF / EPPlus v5+ / FluentAssertions v8+. Allowed: Avalonia (MIT), EF Core, SQLite, ClosedXML, Shouldly, Moq, xUnit.
- **No editable TwoWay + StringFormat on typed (decimal/DateOnly) sources** (6A StackOverflow trap). These 7C surfaces are **read-only**, so they use a **string-facade row VM** (money pre-formatted via `MoneyText.Format`) rather than binding raw decimals with `StringFormat`.
- **Money display:** Vietnamese "." thousand grouping, `0 → ""` (blank), via `ConstructionFinance.Desktop.Common.MoneyText.Format`.
- **.NET naming:** `camelCase` locals, `PascalCase` members/types, `_camelCase` private fields.
- **Fiscal year:** 1/2/N → 31/1/(N+1). Already encoded in `TreeCalculator.FiscalYear` / `TreeAggregate.Yearly` — do not re-derive.
- **Strangler:** the old flat 6A summary (`FinanceSummaryViewModel`, the pinned "TH" tab) stays untouched as dead code, removed in 7E. 7C adds new surfaces alongside it.
- **UI is Vietnamese, VND only, single-user.**
- **Verify UI** by running on `DISPLAY=:1` and screenshotting via `mss` (see Task 6) — subagents cannot see the UI; the phase completion gate is the user comparing numbers to `Mẫu.xlsx`.

## Reference: `Mẫu.xlsx` "Tổng hợp" sheet columns

Grouped table: a **Project** subtotal row (roman ordinal `I, II…`) followed by its **Vị trí** rows (arabic `1, 2…`), then a grand **Tổng cộng** row. Column → `TreeAggregate` field:

| Col | Header | Source |
|---|---|---|
| A | Số TT | ordinal (roman for project, arabic for vị trí) |
| B | Tên dự án / vị trí | label |
| C | Đơn vị thi công xây lắp | first package's `ContractorName` |
| D | Giá trị HĐ, PLHĐ Xây lắp | Σ `Package.ContractValue + ContractAnnexValue` |
| E | Tạm ứng | `SumAdvance` |
| F | TT Khối lượng hoàn thành | `SumKLHT` |
| G | Thu hồi tạm ứng | `SumRecovered` |
| H | TT tiền cho nhà thầu | `TotalDisbursed` (= E + F − G) |
| I | Tạm ứng chưa thu hồi | `UnrecoveredAdvance` |
| J | Giải ngân từ Dự phòng NSNN | `DisbursedNSNN` |
| K | Giải ngân từ Quỹ dự trữ ngoại hối | `DisbursedNgoaiHoi` |
| L | Tổng giá trị đã giải ngân | `TotalDisbursed` |
| M/N, O/P… | Quyết toán năm YYYY (NSNN / DTNH) | `Yearly` — rendered as **one pre-formatted text column** here (dynamic year count); the dedicated "Quyết toán năm" tab shows the per-year breakdown. |

The **"Quyết toán năm" sheet in `Mẫu.xlsx` is empty** — no template. Design (chosen): rows = fiscal years, columns `Năm | DPNSNN | Quỹ DTNH | Tổng` + a `TỔNG CỘNG` row.

---

## Task 1: `WorkbookSummaryBuilder` — pure summary rows

**Files:**
- Create: `src/ConstructionFinance.Application/Calc/WorkbookSummary.cs`
- Create: `src/ConstructionFinance.Application/Calc/WorkbookSummaryBuilder.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Calc/WorkbookSummaryBuilderTests.cs`

**Interfaces:**
- Consumes: `TreeCalculator.AggregateViTri(ViTri)`, `TreeCalculator.Roll(IReadOnlyList<TreeAggregate>)`, `TreeAggregate` (from 7A); entities `Project`, `ViTri`, `CostGroup`, `Package` (`ConstructionFinance.Domain.Finance`).
- Produces:
  - `enum SummaryRowKind { Project, ViTri, GrandTotal }`
  - `record WorkbookSummaryRow(SummaryRowKind Kind, string Ordinal, string Label, int ProjectId, string? Contractor, decimal ContractValue, TreeAggregate Aggregate)`
  - `static (IReadOnlyList<WorkbookSummaryRow> Rows, WorkbookSummaryRow Total) WorkbookSummaryBuilder.BuildViTriRows(IReadOnlyList<ViTri> viTris)` — one `ViTri` row per vị trí (arabic ordinal) + a `GrandTotal` row labelled `"TỔNG CỘNG"`. Used by the per-project tab (Task 2).
  - `static IReadOnlyList<WorkbookSummaryRow> WorkbookSummaryBuilder.BuildAll(IReadOnlyList<Project> projects)` — for each project a `Project` row (roman ordinal) then its `ViTri` rows; a final `GrandTotal` row labelled `"Tổng cộng"`. Used by the all-projects screen (Task 5).

- [ ] **Step 1: Write the failing test**

```csharp
// tests/ConstructionFinance.Application.Tests/Calc/WorkbookSummaryBuilderTests.cs
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Calc;

public class WorkbookSummaryBuilderTests
{
    // One advance đợt (NSNN) so aggregates are non-zero and predictable.
    private static ViTri ViTriWithAdvance(string name, int sort, decimal advance, decimal contract) =>
        new()
        {
            Name = name,
            SortOrder = sort,
            CostGroups =
            {
                new CostGroup
                {
                    Name = "CHI PHÍ XÂY DỰNG",
                    SortOrder = 0,
                    Packages =
                    {
                        new Package
                        {
                            Name = "Gói A",
                            ContractorName = "Cty A",
                            ContractValue = contract,
                            ContractAnnexValue = 0m,
                            SortOrder = 0,
                            Dots =
                            {
                                new Dot
                                {
                                    Date = new DateOnly(2026, 3, 1),
                                    Fund = Fund.NSNN,
                                    Advance = advance,
                                    PaymentKLHT = 0m,
                                    RecoveredAdvance = 0m,
                                    SortOrder = 0
                                }
                            }
                        }
                    }
                }
            }
        };

    [Fact]
    public void BuildViTriRows_returns_one_row_per_vitri_plus_a_total()
    {
        var v1 = ViTriWithAdvance("Vị trí 1", 0, 10_000_000m, 50_000_000_000m);
        var v2 = ViTriWithAdvance("Vị trí 2", 1, 20_000_000m, 0m);

        var (rows, total) = WorkbookSummaryBuilder.BuildViTriRows(new[] { v1, v2 });

        rows.Count.ShouldBe(2);
        rows[0].Kind.ShouldBe(SummaryRowKind.ViTri);
        rows[0].Ordinal.ShouldBe("1");
        rows[0].Label.ShouldBe("Vị trí 1");
        rows[0].Contractor.ShouldBe("Cty A");
        rows[0].ContractValue.ShouldBe(50_000_000_000m);
        rows[0].Aggregate.DisbursedNSNN.ShouldBe(10_000_000m);
        rows[1].Ordinal.ShouldBe("2");

        total.Kind.ShouldBe(SummaryRowKind.GrandTotal);
        total.Label.ShouldBe("TỔNG CỘNG");
        total.Aggregate.DisbursedNSNN.ShouldBe(30_000_000m);
        total.ContractValue.ShouldBe(50_000_000_000m);
    }

    [Fact]
    public void BuildAll_groups_each_project_with_roman_ordinal_then_a_grand_total()
    {
        var p1 = new Project { Id = 1, Name = "Dự án 1", ViTris = { ViTriWithAdvance("Vị trí 1", 0, 10_000_000m, 0m) } };
        var p2 = new Project { Id = 2, Name = "Dự án 2", ViTris = { ViTriWithAdvance("Vị trí 1", 0, 5_000_000m, 0m) } };

        var rows = WorkbookSummaryBuilder.BuildAll(new[] { p1, p2 });

        // I(project) + 1 vị trí, II(project) + 1 vị trí, grand total = 5 rows
        rows.Count.ShouldBe(5);
        rows[0].Kind.ShouldBe(SummaryRowKind.Project);
        rows[0].Ordinal.ShouldBe("I");
        rows[0].Label.ShouldBe("Dự án 1");
        rows[0].ProjectId.ShouldBe(1);
        rows[0].Aggregate.DisbursedNSNN.ShouldBe(10_000_000m);
        rows[1].Kind.ShouldBe(SummaryRowKind.ViTri);
        rows[2].Ordinal.ShouldBe("II");
        rows[2].ProjectId.ShouldBe(2);
        rows[^1].Kind.ShouldBe(SummaryRowKind.GrandTotal);
        rows[^1].Label.ShouldBe("Tổng cộng");
        rows[^1].Aggregate.DisbursedNSNN.ShouldBe(15_000_000m);
    }

    [Fact]
    public void BuildAll_on_empty_input_is_just_a_zero_grand_total()
    {
        var rows = WorkbookSummaryBuilder.BuildAll(Array.Empty<Project>());

        rows.Count.ShouldBe(1);
        rows[0].Kind.ShouldBe(SummaryRowKind.GrandTotal);
        rows[0].Aggregate.TotalDisbursed.ShouldBe(0m);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter WorkbookSummaryBuilderTests`
Expected: FAIL — `WorkbookSummaryBuilder` / `WorkbookSummaryRow` do not exist (compile error).

- [ ] **Step 3: Create the models**

```csharp
// src/ConstructionFinance.Application/Calc/WorkbookSummary.cs
namespace ConstructionFinance.Application.Calc;

public enum SummaryRowKind { Project, ViTri, GrandTotal }

/// One flat row of a Tổng hợp table. Ordinal is roman for a project subtotal,
/// arabic for a vị trí; ContractValue is Σ (Giá trị HĐ + Phụ lục HĐ) of the
/// packages under it; Aggregate carries every money column (and Yearly).
public record WorkbookSummaryRow(
    SummaryRowKind Kind,
    string Ordinal,
    string Label,
    int ProjectId,
    string? Contractor,
    decimal ContractValue,
    TreeAggregate Aggregate);
```

- [ ] **Step 4: Implement the builder**

```csharp
// src/ConstructionFinance.Application/Calc/WorkbookSummaryBuilder.cs
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Calc;

/// Pure: turns Vị trí trees into ordered Tổng hợp rows (Project subtotal → its
/// Vị trí rows → grand total), mirroring the Mẫu.xlsx "Tổng hợp" sheet. No DB/UI.
public static class WorkbookSummaryBuilder
{
    /// One row per vị trí (arabic ordinal) plus a "TỔNG CỘNG" grand-total row.
    public static (IReadOnlyList<WorkbookSummaryRow> Rows, WorkbookSummaryRow Total)
        BuildViTriRows(IReadOnlyList<ViTri> viTris)
    {
        var ordered = viTris.OrderBy(v => v.SortOrder).ToList();
        var rows = new List<WorkbookSummaryRow>();
        var aggregates = new List<TreeAggregate>();

        for (var i = 0; i < ordered.Count; i++)
        {
            var v = ordered[i];
            var agg = TreeCalculator.AggregateViTri(v);
            aggregates.Add(agg);
            rows.Add(new WorkbookSummaryRow(
                Kind: SummaryRowKind.ViTri,
                Ordinal: (i + 1).ToString(),
                Label: v.Name,
                ProjectId: v.ProjectId,
                Contractor: FirstContractor(v),
                ContractValue: ContractValueOf(v),
                Aggregate: agg));
        }

        var total = new WorkbookSummaryRow(
            Kind: SummaryRowKind.GrandTotal,
            Ordinal: "",
            Label: "TỔNG CỘNG",
            ProjectId: 0,
            Contractor: null,
            ContractValue: rows.Sum(r => r.ContractValue),
            Aggregate: TreeCalculator.Roll(aggregates));

        return (rows, total);
    }

    /// For each project: a roman-ordinal Project subtotal row then its vị trí
    /// rows; finally a "Tổng cộng" grand total across all projects.
    public static IReadOnlyList<WorkbookSummaryRow> BuildAll(IReadOnlyList<Project> projects)
    {
        var rows = new List<WorkbookSummaryRow>();
        var projectAggregates = new List<TreeAggregate>();
        var grandContract = 0m;

        var index = 0;
        foreach (var p in projects)
        {
            index++;
            var (viTriRows, viTriTotal) = BuildViTriRows(p.ViTris);
            projectAggregates.Add(viTriTotal.Aggregate);
            grandContract += viTriTotal.ContractValue;

            rows.Add(new WorkbookSummaryRow(
                Kind: SummaryRowKind.Project,
                Ordinal: Roman(index),
                Label: p.Name,
                ProjectId: p.Id,
                Contractor: null,
                ContractValue: viTriTotal.ContractValue,
                Aggregate: viTriTotal.Aggregate));
            rows.AddRange(viTriRows);
        }

        rows.Add(new WorkbookSummaryRow(
            Kind: SummaryRowKind.GrandTotal,
            Ordinal: "",
            Label: "Tổng cộng",
            ProjectId: 0,
            Contractor: null,
            ContractValue: grandContract,
            Aggregate: TreeCalculator.Roll(projectAggregates)));

        return rows;
    }

    private static decimal ContractValueOf(ViTri v) =>
        v.CostGroups.SelectMany(g => g.Packages).Sum(p => p.ContractValue + p.ContractAnnexValue);

    private static string? FirstContractor(ViTri v) =>
        v.CostGroups.OrderBy(g => g.SortOrder)
            .SelectMany(g => g.Packages.OrderBy(p => p.SortOrder))
            .Select(p => p.ContractorName)
            .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));

    // Enough for the practical project counts here (1..~30).
    private static string Roman(int n)
    {
        var map = new (int V, string S)[]
        {
            (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"),
            (90, "XC"), (50, "L"), (40, "XL"), (10, "X"),
            (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
        };
        var sb = new System.Text.StringBuilder();
        foreach (var (v, s) in map)
            while (n >= v) { sb.Append(s); n -= v; }
        return sb.ToString();
    }
}
```

Note: `TreeCalculator.Roll([])` returns an all-zero aggregate with an empty `Yearly` — that is what makes the empty-input grand total work.

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter WorkbookSummaryBuilderTests`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add src/ConstructionFinance.Application/Calc/WorkbookSummary.cs \
        src/ConstructionFinance.Application/Calc/WorkbookSummaryBuilder.cs \
        tests/ConstructionFinance.Application.Tests/Calc/WorkbookSummaryBuilderTests.cs
git commit -m "feat(app): WorkbookSummaryBuilder — Tổng hợp rows from Vị trí trees"
```

---

## Task 2: `SummaryDisplayRow` + `ProjectTongHopViewModel` (per-project Tổng hợp tab)

**Files:**
- Create: `src/ConstructionFinance.Desktop/ViewModels/Finance/SummaryDisplayRow.cs`
- Create: `src/ConstructionFinance.Desktop/ViewModels/Finance/ProjectTongHopViewModel.cs`
- Create: `src/ConstructionFinance.Desktop/ViewModels/Finance/ProjectTongHopPageViewModel.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Finance/ProjectTongHopViewModelTests.cs`

**Interfaces:**
- Consumes: `WorkbookSummaryBuilder.BuildViTriRows` (Task 1); `WorkbookSummaryRow`, `SummaryRowKind`, `TreeAggregate`; `MoneyText.Format`; `FiscalYearSettlement`; `ViTri`; `WorkbookPageViewModel` base.
- Produces (used by Task 4 & Task 5):
  - `class SummaryDisplayRow` — read-only string-facade row with: `string Ordinal, Label, Contractor, ContractValue, Advance, KLHT, Recovered, DisbursedToContractor, Unrecovered, NSNN, NgoaiHoi, TotalDisbursed, YearlyText; bool IsBold;` and `static SummaryDisplayRow From(WorkbookSummaryRow row)`, `static string FormatYearly(IReadOnlyList<FiscalYearSettlement> years)`.
  - `class ProjectTongHopViewModel : ViewModelBase` with `ObservableCollection<SummaryDisplayRow> Rows` and `void Refresh(IReadOnlyList<ViTri> viTris)`.
  - `class ProjectTongHopPageViewModel : WorkbookPageViewModel` wrapping it (`Title = "Tổng hợp"`, `Content` = the VM), plus a `public ProjectTongHopViewModel Inner { get; }` accessor.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/ConstructionFinance.Desktop.Tests/Finance/ProjectTongHopViewModelTests.cs
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class ProjectTongHopViewModelTests
{
    private static ViTri ViTriWithAdvance(string name, int sort, decimal advance) => new()
    {
        Name = name,
        SortOrder = sort,
        CostGroups =
        {
            new CostGroup
            {
                Name = "CHI PHÍ XÂY DỰNG", SortOrder = 0,
                Packages =
                {
                    new Package
                    {
                        Name = "Gói A", ContractorName = "Cty A",
                        ContractValue = 50_000_000_000m, SortOrder = 0,
                        Dots =
                        {
                            new Dot { Date = new DateOnly(2026, 3, 1), Fund = Fund.NSNN,
                                      Advance = advance, SortOrder = 0 }
                        }
                    }
                }
            }
        }
    };

    [Fact]
    public void Refresh_lists_a_row_per_vitri_and_a_bold_total()
    {
        var vm = new ProjectTongHopViewModel();

        vm.Refresh(new[]
        {
            ViTriWithAdvance("Vị trí 1", 0, 10_000_000m),
            ViTriWithAdvance("Vị trí 2", 1, 20_000_000m)
        });

        vm.Rows.Count.ShouldBe(3);                 // 2 vị trí + total
        vm.Rows[0].Label.ShouldBe("Vị trí 1");
        vm.Rows[0].IsBold.ShouldBeFalse();
        vm.Rows[0].NSNN.ShouldBe("10.000.000");    // MoneyText "." grouping
        vm.Rows[2].Label.ShouldBe("TỔNG CỘNG");
        vm.Rows[2].IsBold.ShouldBeTrue();
        vm.Rows[2].TotalDisbursed.ShouldBe("30.000.000");
    }

    [Fact]
    public void Refresh_replaces_previous_rows()
    {
        var vm = new ProjectTongHopViewModel();
        vm.Refresh(new[] { ViTriWithAdvance("Vị trí 1", 0, 10_000_000m) });
        vm.Refresh(new[] { ViTriWithAdvance("Vị trí X", 0, 1_000_000m) });

        vm.Rows.Count.ShouldBe(2);                 // 1 vị trí + total (not appended)
        vm.Rows[0].Label.ShouldBe("Vị trí X");
    }

    [Fact]
    public void Zero_money_renders_blank()
    {
        var vm = new ProjectTongHopViewModel();
        vm.Refresh(new[] { ViTriWithAdvance("Vị trí 1", 0, 0m) });

        vm.Rows[0].Advance.ShouldBe("");           // 0 → blank (Excel-like)
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter ProjectTongHopViewModelTests`
Expected: FAIL — types do not exist.

- [ ] **Step 3: Implement `SummaryDisplayRow`**

```csharp
// src/ConstructionFinance.Desktop/ViewModels/Finance/SummaryDisplayRow.cs
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// Read-only, pre-formatted row shared by the per-project Tổng hợp tab and the
/// all-projects Tổng hợp screen. Money is formatted with MoneyText ("." grouping,
/// 0 → blank); Project/GrandTotal rows are bold. String facade — never bound TwoWay.
public class SummaryDisplayRow
{
    public int ProjectId { get; init; }
    public string Ordinal { get; init; } = "";
    public string Label { get; init; } = "";
    public string Contractor { get; init; } = "";
    public string ContractValue { get; init; } = "";
    public string Advance { get; init; } = "";
    public string KLHT { get; init; } = "";
    public string Recovered { get; init; } = "";
    public string DisbursedToContractor { get; init; } = "";
    public string Unrecovered { get; init; } = "";
    public string NSNN { get; init; } = "";
    public string NgoaiHoi { get; init; } = "";
    public string TotalDisbursed { get; init; } = "";
    public string YearlyText { get; init; } = "";
    public bool IsBold { get; init; }

    public static SummaryDisplayRow From(WorkbookSummaryRow row)
    {
        var a = row.Aggregate;
        return new SummaryDisplayRow
        {
            ProjectId = row.ProjectId,
            Ordinal = row.Ordinal,
            Label = row.Label,
            Contractor = row.Contractor ?? "",
            ContractValue = MoneyText.Format(row.ContractValue),
            Advance = MoneyText.Format(a.SumAdvance),
            KLHT = MoneyText.Format(a.SumKLHT),
            Recovered = MoneyText.Format(a.SumRecovered),
            DisbursedToContractor = MoneyText.Format(a.TotalDisbursed),
            Unrecovered = MoneyText.Format(a.UnrecoveredAdvance),
            NSNN = MoneyText.Format(a.DisbursedNSNN),
            NgoaiHoi = MoneyText.Format(a.DisbursedNgoaiHoi),
            TotalDisbursed = MoneyText.Format(a.TotalDisbursed),
            YearlyText = FormatYearly(a.Yearly),
            IsBold = row.Kind != SummaryRowKind.ViTri
        };
    }

    public static string FormatYearly(IReadOnlyList<FiscalYearSettlement> years) =>
        string.Join("  |  ", years
            .Where(y => y.NSNN != 0m || y.NgoaiHoi != 0m)
            .OrderBy(y => y.FiscalYear)
            .Select(y =>
            {
                var parts = new List<string>();
                if (y.NSNN != 0m) parts.Add($"NSNN {MoneyText.Format(y.NSNN)}");
                if (y.NgoaiHoi != 0m) parts.Add($"DTNH {MoneyText.Format(y.NgoaiHoi)}");
                return $"{y.FiscalYear}: {string.Join(", ", parts)}";
            }));
}
```

- [ ] **Step 4: Implement `ProjectTongHopViewModel` and its page wrapper**

```csharp
// src/ConstructionFinance.Desktop/ViewModels/Finance/ProjectTongHopViewModel.cs
using System.Collections.ObjectModel;
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// Read-only "Tổng hợp" tab for one project: a row per Vị trí + a TỔNG CỘNG row,
/// recomputed live from the in-memory Vị trí tabs via Refresh().
public class ProjectTongHopViewModel : ViewModelBase
{
    public ObservableCollection<SummaryDisplayRow> Rows { get; } = new();

    public void Refresh(IReadOnlyList<ViTri> viTris)
    {
        var (rows, total) = WorkbookSummaryBuilder.BuildViTriRows(viTris);
        Rows.Clear();
        foreach (var r in rows) Rows.Add(SummaryDisplayRow.From(r));
        Rows.Add(SummaryDisplayRow.From(total));
    }
}
```

```csharp
// src/ConstructionFinance.Desktop/ViewModels/Finance/ProjectTongHopPageViewModel.cs
namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// The pinned per-project "Tổng hợp" tab (first in the Vị trí tab strip).
public sealed class ProjectTongHopPageViewModel : WorkbookPageViewModel
{
    public ProjectTongHopPageViewModel(ProjectTongHopViewModel inner)
    {
        Inner = inner;
        Title = "Tổng hợp";
    }

    public ProjectTongHopViewModel Inner { get; }
    public override object Content => Inner;
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter ProjectTongHopViewModelTests`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add src/ConstructionFinance.Desktop/ViewModels/Finance/SummaryDisplayRow.cs \
        src/ConstructionFinance.Desktop/ViewModels/Finance/ProjectTongHopViewModel.cs \
        src/ConstructionFinance.Desktop/ViewModels/Finance/ProjectTongHopPageViewModel.cs \
        tests/ConstructionFinance.Desktop.Tests/Finance/ProjectTongHopViewModelTests.cs
git commit -m "feat(desktop): ProjectTongHopViewModel — per-project Tổng hợp tab (live)"
```

---

## Task 3: `YearSettlementViewModel` (per-project Quyết toán năm tab)

**Files:**
- Create: `src/ConstructionFinance.Desktop/ViewModels/Finance/YearSettlementRow.cs`
- Create: `src/ConstructionFinance.Desktop/ViewModels/Finance/YearSettlementViewModel.cs`
- Create: `src/ConstructionFinance.Desktop/ViewModels/Finance/YearSettlementPageViewModel.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Finance/YearSettlementViewModelTests.cs`

**Interfaces:**
- Consumes: `TreeCalculator.AggregateViTri`, `TreeCalculator.Roll`, `TreeAggregate.Yearly` (`FiscalYearSettlement`), `MoneyText.Format`, `ViTri`, `WorkbookPageViewModel`.
- Produces (used by Task 4):
  - `class YearSettlementRow` — `string YearLabel, NSNN, NgoaiHoi, Total; bool IsBold`.
  - `class YearSettlementViewModel : ViewModelBase` — `ObservableCollection<YearSettlementRow> Rows`, `void Refresh(IReadOnlyList<ViTri> viTris)`.
  - `class YearSettlementPageViewModel : WorkbookPageViewModel` (`Title = "Quyết toán năm"`, `public YearSettlementViewModel Inner`).

- [ ] **Step 1: Write the failing test**

```csharp
// tests/ConstructionFinance.Desktop.Tests/Finance/YearSettlementViewModelTests.cs
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class YearSettlementViewModelTests
{
    private static ViTri ViTriWithDot(DateOnly date, Fund fund, decimal advance) => new()
    {
        Name = "V", SortOrder = 0,
        CostGroups =
        {
            new CostGroup
            {
                Name = "G", SortOrder = 0,
                Packages =
                {
                    new Package
                    {
                        Name = "P", SortOrder = 0,
                        Dots = { new Dot { Date = date, Fund = fund, Advance = advance, SortOrder = 0 } }
                    }
                }
            }
        }
    };

    [Fact]
    public void Refresh_makes_a_row_per_fiscal_year_plus_a_total()
    {
        var vm = new YearSettlementViewModel();

        vm.Refresh(new[]
        {
            ViTriWithDot(new DateOnly(2026, 3, 1), Fund.NSNN, 10_000_000m),      // FY 2026
            ViTriWithDot(new DateOnly(2027, 1, 15), Fund.NgoaiHoi, 4_000_000m)   // Jan → FY 2026
        });

        // Both đợt land in fiscal year 2026 (1/2/2026 → 31/1/2027).
        vm.Rows.Count.ShouldBe(2);                          // FY 2026 + total
        vm.Rows[0].YearLabel.ShouldBe("Quyết toán năm 2026");
        vm.Rows[0].NSNN.ShouldBe("10.000.000");
        vm.Rows[0].NgoaiHoi.ShouldBe("4.000.000");
        vm.Rows[0].Total.ShouldBe("14.000.000");
        vm.Rows[1].YearLabel.ShouldBe("TỔNG CỘNG");
        vm.Rows[1].IsBold.ShouldBeTrue();
        vm.Rows[1].Total.ShouldBe("14.000.000");
    }

    [Fact]
    public void Two_distinct_fiscal_years_get_two_rows()
    {
        var vm = new YearSettlementViewModel();

        vm.Refresh(new[]
        {
            ViTriWithDot(new DateOnly(2026, 3, 1), Fund.NSNN, 10_000_000m),      // FY 2026
            ViTriWithDot(new DateOnly(2027, 3, 1), Fund.NSNN, 5_000_000m)        // FY 2027
        });

        vm.Rows.Count.ShouldBe(3);                          // 2026, 2027, total
        vm.Rows[0].YearLabel.ShouldBe("Quyết toán năm 2026");
        vm.Rows[1].YearLabel.ShouldBe("Quyết toán năm 2027");
        vm.Rows[2].Total.ShouldBe("15.000.000");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter YearSettlementViewModelTests`
Expected: FAIL — types do not exist.

- [ ] **Step 3: Implement the row + view-model + page**

```csharp
// src/ConstructionFinance.Desktop/ViewModels/Finance/YearSettlementRow.cs
namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// One fiscal-year settlement line (or the bold TỔNG CỘNG). String facade.
public class YearSettlementRow
{
    public string YearLabel { get; init; } = "";
    public string NSNN { get; init; } = "";
    public string NgoaiHoi { get; init; } = "";
    public string Total { get; init; } = "";
    public bool IsBold { get; init; }
}
```

```csharp
// src/ConstructionFinance.Desktop/ViewModels/Finance/YearSettlementViewModel.cs
using System.Collections.ObjectModel;
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// Read-only "Quyết toán năm" tab: one row per fiscal year (1/2→31/1), split
/// DPNSNN / Quỹ DTNH, + a TỔNG CỘNG row. Live via Refresh() from the Vị trí tabs.
public class YearSettlementViewModel : ViewModelBase
{
    public ObservableCollection<YearSettlementRow> Rows { get; } = new();

    public void Refresh(IReadOnlyList<ViTri> viTris)
    {
        var agg = TreeCalculator.Roll(viTris.Select(TreeCalculator.AggregateViTri).ToList());

        Rows.Clear();
        foreach (var y in agg.Yearly.OrderBy(y => y.FiscalYear))
            Rows.Add(new YearSettlementRow
            {
                YearLabel = $"Quyết toán năm {y.FiscalYear}",
                NSNN = MoneyText.Format(y.NSNN),
                NgoaiHoi = MoneyText.Format(y.NgoaiHoi),
                Total = MoneyText.Format(y.NSNN + y.NgoaiHoi)
            });

        if (Rows.Count == 0) return;   // no đợt → no year rows, no total

        var totalNsnn = agg.Yearly.Sum(y => y.NSNN);
        var totalNgoaiHoi = agg.Yearly.Sum(y => y.NgoaiHoi);
        Rows.Add(new YearSettlementRow
        {
            YearLabel = "TỔNG CỘNG",
            NSNN = MoneyText.Format(totalNsnn),
            NgoaiHoi = MoneyText.Format(totalNgoaiHoi),
            Total = MoneyText.Format(totalNsnn + totalNgoaiHoi),
            IsBold = true
        });
    }
}
```

```csharp
// src/ConstructionFinance.Desktop/ViewModels/Finance/YearSettlementPageViewModel.cs
namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// The per-project "Quyết toán năm" tab (last in the Vị trí tab strip).
public sealed class YearSettlementPageViewModel : WorkbookPageViewModel
{
    public YearSettlementPageViewModel(YearSettlementViewModel inner)
    {
        Inner = inner;
        Title = "Quyết toán năm";
    }

    public YearSettlementViewModel Inner { get; }
    public override object Content => Inner;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter YearSettlementViewModelTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ConstructionFinance.Desktop/ViewModels/Finance/YearSettlementRow.cs \
        src/ConstructionFinance.Desktop/ViewModels/Finance/YearSettlementViewModel.cs \
        src/ConstructionFinance.Desktop/ViewModels/Finance/YearSettlementPageViewModel.cs \
        tests/ConstructionFinance.Desktop.Tests/Finance/YearSettlementViewModelTests.cs
git commit -m "feat(desktop): YearSettlementViewModel — Quyết toán năm tab (live)"
```

---

## Task 4: Fold Tổng hợp + Quyết toán năm into the workbook tab strip

Turn the Vị trí-only tab strip into a heterogeneous strip `[Tổng hợp | Vị trí 1 | Vị trí 2 … | Quyết toán năm]`, keeping all existing add/rename/delete/save-all behavior working for the Vị trí tabs only. The two aggregate tabs recompute whenever a ledger changes or the tab is selected.

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceWorkbookViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/Views/Finance/FinanceWorkbookView.axaml`
- Create: `src/ConstructionFinance.Desktop/Views/Finance/ProjectTongHopView.axaml` (+ `.axaml.cs`)
- Create: `src/ConstructionFinance.Desktop/Views/Finance/YearSettlementView.axaml` (+ `.axaml.cs`)
- Modify (update existing tests): `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceWorkbookViewModelTests.cs`
- Test (new): `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceWorkbookTongHopTabsTests.cs`

**Interfaces:**
- Consumes: `ProjectTongHopPageViewModel`/`ProjectTongHopViewModel` (Task 2), `YearSettlementPageViewModel`/`YearSettlementViewModel` (Task 3), `WorkbookPageViewModel`, `ViTriPageViewModel`, `VitriLedgerViewModel.ToViTri()`, `VitriLedgerViewModel.DirtyChanged`.
- Produces: `FinanceWorkbookViewModel.ViTriPages` retyped to `ObservableCollection<WorkbookPageViewModel>`; `SelectedViTriPage` retyped to `WorkbookPageViewModel?`. New internal `RefreshAggregateTabs()`.

### 4a — View-model changes

- [ ] **Step 1: Retype the tab collection and selection**

In `FinanceWorkbookViewModel.cs`:

Change
```csharp
public ObservableCollection<ViTriPageViewModel> ViTriPages { get; } = new();
[ObservableProperty] private ViTriPageViewModel? _selectedViTriPage;
```
to
```csharp
// Heterogeneous: [Tổng hợp][one per Vị trí][Quyết toán năm].
public ObservableCollection<WorkbookPageViewModel> ViTriPages { get; } = new();
[ObservableProperty] private WorkbookPageViewModel? _selectedViTriPage;

// The two read-only aggregate tabs for the selected project (rebuilt per project).
private ProjectTongHopPageViewModel? _tongHopPage;
private YearSettlementPageViewModel? _yearPage;
```

- [ ] **Step 2: Filter the dirty check to Vị trí tabs**

In the `IsDirty` getter, change `ViTriPages.Any(p => p.IsDirty)` to:
```csharp
ViTriPages.OfType<ViTriPageViewModel>().Any(p => p.IsDirty) ||
```
(The aggregate tabs are read-only and never dirty, but filtering makes intent explicit and avoids future footguns.)

- [ ] **Step 3: Build the heterogeneous strip when a project is selected**

Replace the tab-building block inside `LoadSelectedProjectTreeAsync` (the `foreach … AddViTriPage …; SelectedViTriPage = ViTriPages.FirstOrDefault();`) with:

```csharp
_selectedProjectTree = tree;

_tongHopPage = new ProjectTongHopPageViewModel(new ProjectTongHopViewModel());
_yearPage = new YearSettlementPageViewModel(new YearSettlementViewModel());

ViTriPages.Add(_tongHopPage);
foreach (var viTri in tree.ViTris.OrderBy(v => v.SortOrder))
    AddViTriPage(new VitriLedgerViewModel(viTri));
ViTriPages.Add(_yearPage);

RefreshAggregateTabs();
SelectedViTriPage = ViTriPages.FirstOrDefault();   // the Tổng hợp tab
```

Also, at the top of `LoadSelectedProjectTreeAsync` where it clears state, null the two page fields:
```csharp
ViTriPages.Clear();
SelectedViTriPage = null;
_tongHopPage = null;
_yearPage = null;
_selectedProjectTree = null;
```
And in `LoadAsync`'s reset block do the same (`_tongHopPage = null; _yearPage = null;`) right after `SelectedViTriPage = null;`.

- [ ] **Step 4: Recompute aggregates on ledger edits and tab selection**

Add a helper and wire it. In `AddViTriPage`, after creating `page`, also subscribe to ledger changes:

```csharp
private void AddViTriPage(VitriLedgerViewModel ledger)
{
    var page = new ViTriPageViewModel(ledger);
    page.PropertyChanged += (_, e) =>
    {
        if (e.PropertyName == nameof(WorkbookPageViewModel.IsDirty))
            OnPropertyChanged(nameof(IsDirty));
    };
    // Any ledger edit (subtotals recompute live) should refresh the aggregate tabs.
    ledger.DirtyChanged += (_, _) => RefreshAggregateTabs();
    InsertViTriPage(page);
}
```

Replace the previous `ViTriPages.Add(page);` with a helper that keeps the Year tab last:

```csharp
/// Insert a Vị trí tab before the trailing "Quyết toán năm" tab (or at the end
/// if the aggregate tabs aren't present, e.g. the AddProject in-memory path).
private void InsertViTriPage(ViTriPageViewModel page)
{
    var yearIndex = _yearPage is null ? -1 : ViTriPages.IndexOf(_yearPage);
    if (yearIndex >= 0) ViTriPages.Insert(yearIndex, page);
    else ViTriPages.Add(page);
}
```

Add the refresh helper:

```csharp
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
```

Refresh when the user switches to an aggregate tab (covers edits made while another tab was focused). Add:

```csharp
partial void OnSelectedViTriPageChanged(WorkbookPageViewModel? value)
{
    if (value is ProjectTongHopPageViewModel or YearSettlementPageViewModel)
        RefreshAggregateTabs();
}
```

- [ ] **Step 5: Keep add/delete/save scoped to Vị trí tabs**

`AddViTri`: after building the ledger, use `InsertViTriPage` (already used via `AddViTriPage`) and select the newly inserted page. Update its body so the new tab is selected and aggregates refresh:
```csharp
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
```

`DeleteViTriAsync`: guard to Vị trí tabs and reselect a Vị trí tab (fall back to the Tổng hợp tab):
```csharp
[RelayCommand]
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
```

`AddProject` builds its tabs in-memory. Update its tab-building block (currently `ViTriPages.Clear(); AddViTriPage(...); SelectedViTriPage = ViTriPages.FirstOrDefault();`) to include the aggregate tabs:
```csharp
_selectedProjectTree = project;
ViTriPages.Clear();
_tongHopPage = new ProjectTongHopPageViewModel(new ProjectTongHopViewModel());
_yearPage = new YearSettlementPageViewModel(new YearSettlementViewModel());
ViTriPages.Add(_tongHopPage);
AddViTriPage(new VitriLedgerViewModel(viTri));
ViTriPages.Add(_yearPage);
RefreshAggregateTabs();
SelectedViTriPage = ViTriPages.OfType<ViTriPageViewModel>().First();  // land on the ledger to type
```

`SaveViTriTreeAsync`: change the tab loop to Vị trí tabs only:
```csharp
var ledgerPages = ViTriPages.OfType<ViTriPageViewModel>().ToList();
for (var i = 0; i < ledgerPages.Count; i++)
{
    var viTri = ledgerPages[i].Ledger.ToViTri();
    viTri.SortOrder = i;
    toSave.ViTris.Add(viTri);
}
```
and its success block:
```csharp
foreach (var page in ViTriPages.OfType<ViTriPageViewModel>())
    page.Ledger.IsDirty = false;
```

`SaveAllAsync`: the `saveViTriTree` guard uses `ViTriPages.Any(p => p.IsDirty)` — change to `ViTriPages.OfType<ViTriPageViewModel>().Any(p => p.IsDirty)`.

- [ ] **Step 6: Update the existing workbook tests for the new tab layout**

The Vị trí strip now always carries the Tổng hợp (index 0) and Quyết toán năm (last) tabs. Update `FinanceWorkbookViewModelTests.cs`:

- `Selecting_a_project_builds_one_ViTriPage_per_ViTri` — replace the assertions:
```csharp
sut.SelectedProject = sut.Projects.Single();

// [Tổng hợp][V1][V2][Quyết toán năm]
sut.ViTriPages.Count.ShouldBe(4);
sut.ViTriPages[0].ShouldBeOfType<ProjectTongHopPageViewModel>();
sut.ViTriPages[0].Title.ShouldBe("Tổng hợp");
var ledgers = sut.ViTriPages.OfType<ViTriPageViewModel>().ToList();
ledgers[0].Title.ShouldBe("V1");
ledgers[1].Title.ShouldBe("V2");
sut.ViTriPages[^1].ShouldBeOfType<YearSettlementPageViewModel>();
sut.ViTriPages[^1].Title.ShouldBe("Quyết toán năm");
sut.SelectedViTriPage.ShouldBe(sut.ViTriPages[0]);   // lands on Tổng hợp
```

- `Deselecting_the_project_clears_the_ViTri_tabs` — unchanged (`ViTriPages.ShouldBeEmpty()` after `SelectedProject = null`) — still valid; keep as is.

- `AddViTri_appends_a_new_tab_and_marks_the_workbook_dirty`:
```csharp
sut.AddViTriCommand.Execute(null);

sut.ViTriPages.OfType<ViTriPageViewModel>().Count().ShouldBe(1);
// new Vị trí sits before the trailing Quyết toán năm tab
sut.ViTriPages[^1].ShouldBeOfType<YearSettlementPageViewModel>();
sut.SelectedViTriPage.ShouldBeOfType<ViTriPageViewModel>();
sut.IsDirty.ShouldBeTrue();
```

- `AddViTri_without_a_selected_project_is_a_no_op` — after `SelectedProject = null`, `ViTriPages` is empty, so `sut.ViTriPages.ShouldBeEmpty();` stays valid. Keep.

- `DeleteViTri_after_confirm_removes_the_tab_and_marks_the_workbook_dirty`:
```csharp
var toDelete = sut.ViTriPages.OfType<ViTriPageViewModel>().First();
await sut.DeleteViTriCommand.ExecuteAsync(toDelete);

var remaining = sut.ViTriPages.OfType<ViTriPageViewModel>().ToList();
remaining.Count.ShouldBe(1);
remaining[0].Title.ShouldBe("V2");
sut.IsDirty.ShouldBeTrue();
```

- `IsDirty_is_true_when_a_ViTri_ledgers_row_edit_marks_it_dirty`:
```csharp
sut.ViTriPages.OfType<ViTriPageViewModel>().First().Ledger.AddGroupCommand.Execute(null);
sut.IsDirty.ShouldBeTrue();
```

- `SaveAll_persists_the_selected_projects_ViTri_tree_via_ReplaceTreeAsync`:
```csharp
sut.ViTriPages.OfType<ViTriPageViewModel>().First().Ledger.AddGroupCommand.Execute(null);
```
(the `saved.ViTris.Count.ShouldBe(1)` etc. assertions stay — only the Vị trí tabs are saved.)

- `AddProject_adds_and_selects_a_new_project_with_one_empty_ViTri_and_marks_dirty`:
```csharp
sut.AddProjectCommand.Execute(null);
...
sut.ViTriPages.OfType<ViTriPageViewModel>().Count().ShouldBe(1);
sut.ViTriPages.OfType<ViTriPageViewModel>().First().Ledger.Name.ShouldBe("Vị trí 1");
```

- `SaveAll_persists_a_newly_added_and_renamed_project_via_ReplaceTreeAsync` — the `saved.ViTris.Count.ShouldBe(1)` assertion stays valid (aggregate tabs excluded from save). No change needed beyond confirming it compiles with the retyped collection.

- [ ] **Step 7: Run existing workbook tests to verify they pass**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter FinanceWorkbookViewModelTests`
Expected: PASS (all, with the updated assertions).

- [ ] **Step 8: Write the new tab-integration test**

```csharp
// tests/ConstructionFinance.Desktop.Tests/Finance/FinanceWorkbookTongHopTabsTests.cs
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.Import;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Domain.Finance;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class FinanceWorkbookTongHopTabsTests
{
    private static FinanceWorkbookViewModel CreateSut(int projectId, string name, params ViTri[] viTris)
    {
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.GetAllSummariesAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(Array.Empty<FinanceProjectSummary>());
        service.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<Project> { new() { Id = projectId, Name = name } });
        service.Setup(s => s.GetTreeAsync(projectId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Project { Id = projectId, Name = name, ViTris = viTris.ToList() });

        var summaryVm = new FinanceSummaryViewModel(
            service.Object, Mock.Of<IFinanceImportService>(),
            Mock.Of<IFinanceExportService>(), Mock.Of<IDialogService>());

        return new FinanceWorkbookViewModel(
            service.Object, new SummaryPageViewModel(summaryVm),
            Mock.Of<IDialogService>(), new ServiceCollection().BuildServiceProvider());
    }

    private static ViTri ViTriWithAdvance(string name, decimal advance) => new()
    {
        Name = name, SortOrder = 0,
        CostGroups =
        {
            new CostGroup { Name = "G", SortOrder = 0, Packages =
            {
                new Package { Name = "P", SortOrder = 0, Dots =
                {
                    new Dot { Date = new DateOnly(2026, 3, 1), Fund = Fund.NSNN, Advance = advance, SortOrder = 0 }
                } }
            } }
        }
    };

    [Fact]
    public async Task Selecting_a_project_frames_the_strip_with_TongHop_and_QuyetToanNam()
    {
        var sut = CreateSut(1, "DA1", ViTriWithAdvance("V1", 10_000_000m));
        await sut.LoadAsync();

        sut.ViTriPages[0].ShouldBeOfType<ProjectTongHopPageViewModel>();
        sut.ViTriPages[^1].ShouldBeOfType<YearSettlementPageViewModel>();
    }

    [Fact]
    public async Task The_TongHop_tab_reflects_the_ledger_numbers()
    {
        var sut = CreateSut(1, "DA1", ViTriWithAdvance("V1", 10_000_000m));
        await sut.LoadAsync();

        var tongHop = sut.ViTriPages.OfType<ProjectTongHopPageViewModel>().Single();
        tongHop.Inner.Rows[^1].NSNN.ShouldBe("10.000.000");   // TỔNG CỘNG row
    }

    [Fact]
    public async Task The_QuyetToanNam_tab_buckets_by_fiscal_year()
    {
        var sut = CreateSut(1, "DA1", ViTriWithAdvance("V1", 10_000_000m));
        await sut.LoadAsync();

        var year = sut.ViTriPages.OfType<YearSettlementPageViewModel>().Single();
        year.Inner.Rows[0].YearLabel.ShouldBe("Quyết toán năm 2026");
        year.Inner.Rows[0].NSNN.ShouldBe("10.000.000");
    }
}
```

- [ ] **Step 9: Run the new test**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter FinanceWorkbookTongHopTabsTests`
Expected: PASS (3 tests).

### 4b — Views (needs the user's visual review — Task 6)

- [ ] **Step 10: Create the Tổng hợp tab view**

```xml
<!-- src/ConstructionFinance.Desktop/Views/Finance/ProjectTongHopView.axaml -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Finance"
             x:Class="ConstructionFinance.Desktop.Views.Finance.ProjectTongHopView"
             x:DataType="vm:ProjectTongHopViewModel">
    <DataGrid ItemsSource="{Binding Rows}" IsReadOnly="True"
              GridLinesVisibility="All" HeadersVisibility="All"
              CanUserResizeColumns="True" CanUserSortColumns="False">
        <DataGrid.Columns>
            <DataGridTextColumn Header="TT" Binding="{Binding Ordinal}" Width="48"/>
            <DataGridTextColumn Header="Tên vị trí" Binding="{Binding Label}" Width="200"/>
            <DataGridTextColumn Header="Đơn vị thi công" Binding="{Binding Contractor}" Width="150"/>
            <DataGridTextColumn Header="Giá trị HĐ, PLHĐ" Binding="{Binding ContractValue}" Width="130"/>
            <DataGridTextColumn Header="Tạm ứng" Binding="{Binding Advance}" Width="120"/>
            <DataGridTextColumn Header="TT KLHT" Binding="{Binding KLHT}" Width="120"/>
            <DataGridTextColumn Header="Thu hồi" Binding="{Binding Recovered}" Width="120"/>
            <DataGridTextColumn Header="TT cho nhà thầu" Binding="{Binding DisbursedToContractor}" Width="130"/>
            <DataGridTextColumn Header="Tạm ứng chưa thu hồi" Binding="{Binding Unrecovered}" Width="140"/>
            <DataGridTextColumn Header="GN Dự phòng NSNN" Binding="{Binding NSNN}" Width="140"/>
            <DataGridTextColumn Header="GN Quỹ DTNH" Binding="{Binding NgoaiHoi}" Width="130"/>
            <DataGridTextColumn Header="Tổng đã giải ngân" Binding="{Binding TotalDisbursed}" Width="140"/>
            <DataGridTextColumn Header="Quyết toán năm" Binding="{Binding YearlyText}" Width="260"/>
        </DataGrid.Columns>
    </DataGrid>
</UserControl>
```

```csharp
// src/ConstructionFinance.Desktop/Views/Finance/ProjectTongHopView.axaml.cs
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop.Views.Finance;

public partial class ProjectTongHopView : UserControl
{
    public ProjectTongHopView() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 11: Create the Quyết toán năm tab view**

```xml
<!-- src/ConstructionFinance.Desktop/Views/Finance/YearSettlementView.axaml -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Finance"
             x:Class="ConstructionFinance.Desktop.Views.Finance.YearSettlementView"
             x:DataType="vm:YearSettlementViewModel">
    <StackPanel Margin="0,4,0,0">
        <TextBlock Classes="pageSubtitle" Margin="4,0,0,8"
                   Text="Quyết toán theo năm tài chính (1/2 → 31/1)"/>
        <DataGrid ItemsSource="{Binding Rows}" IsReadOnly="True"
                  GridLinesVisibility="All" HeadersVisibility="All"
                  CanUserResizeColumns="True" CanUserSortColumns="False">
            <DataGrid.Columns>
                <DataGridTextColumn Header="Năm" Binding="{Binding YearLabel}" Width="220"/>
                <DataGridTextColumn Header="Dự phòng NSNN" Binding="{Binding NSNN}" Width="180"/>
                <DataGridTextColumn Header="Quỹ dự trữ ngoại hối" Binding="{Binding NgoaiHoi}" Width="200"/>
                <DataGridTextColumn Header="Tổng" Binding="{Binding Total}" Width="180"/>
            </DataGrid.Columns>
        </DataGrid>
    </StackPanel>
</UserControl>
```

```csharp
// src/ConstructionFinance.Desktop/Views/Finance/YearSettlementView.axaml.cs
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop.Views.Finance;

public partial class YearSettlementView : UserControl
{
    public YearSettlementView() => AvaloniaXamlLoader.Load(this);
}
```

The `ViewLocator` resolves `ProjectTongHopViewModel → ProjectTongHopView` and `YearSettlementViewModel → YearSettlementView` by naming convention (no registration needed).

- [ ] **Step 12: Point the workbook's Vị trí `TabControl` at the base page type**

In `FinanceWorkbookView.axaml`, the `ViTriTabs` `TabControl` templates currently use `x:DataType="vm:ViTriPageViewModel"`. Since the collection is now `WorkbookPageViewModel`, change both templates to bind the base type (both `Header` and `Content` live on the base):

```xml
<TabControl x:Name="ViTriTabs"
            ItemsSource="{Binding ViTriPages}"
            SelectedItem="{Binding SelectedViTriPage}"
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
```

- [ ] **Step 13: Build to verify XAML compiles**

Run: `dotnet build src/ConstructionFinance.Desktop`
Expected: Build succeeded (0 errors).

- [ ] **Step 14: Commit**

```bash
git add src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceWorkbookViewModel.cs \
        src/ConstructionFinance.Desktop/Views/Finance/FinanceWorkbookView.axaml \
        src/ConstructionFinance.Desktop/Views/Finance/ProjectTongHopView.axaml \
        src/ConstructionFinance.Desktop/Views/Finance/ProjectTongHopView.axaml.cs \
        src/ConstructionFinance.Desktop/Views/Finance/YearSettlementView.axaml \
        src/ConstructionFinance.Desktop/Views/Finance/YearSettlementView.axaml.cs \
        tests/ConstructionFinance.Desktop.Tests/Finance/FinanceWorkbookViewModelTests.cs \
        tests/ConstructionFinance.Desktop.Tests/Finance/FinanceWorkbookTongHopTabsTests.cs
git commit -m "feat(desktop): Tổng hợp + Quyết toán năm tabs in the Vị trí workbook strip"
```

---

## Task 5: All-projects "Tổng hợp" nav screen (tree-based)

A new left-nav item "Tổng hợp" showing every project's Vị trí tree aggregated, grouped Project→Vị trí, with an instant name filter and a grand-total row; double-click a project row to jump to its workbook tab. The old flat `FinanceSummaryViewModel` and its "TH" tab are left untouched (7E teardown).

**Files:**
- Create: `src/ConstructionFinance.Desktop/ViewModels/Finance/WorkbookSummaryViewModel.cs`
- Create: `src/ConstructionFinance.Desktop/Views/Finance/WorkbookSummaryView.axaml` (+ `.axaml.cs`)
- Modify: `src/ConstructionFinance.Desktop/Common/HostBuilder.cs` (DI)
- Modify: `src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs` (nav case)
- Modify: `src/ConstructionFinance.Desktop/Views/MainWindow.axaml` (nav item)
- Test: `tests/ConstructionFinance.Desktop.Tests/Finance/WorkbookSummaryViewModelTests.cs`

**Interfaces:**
- Consumes: `IFinanceProjectService.ListTreesAsync` (returns projects with full Vị trí trees), `WorkbookSummaryBuilder.BuildAll` (Task 1), `SummaryDisplayRow` (Task 2).
- Produces: `WorkbookSummaryViewModel` with `ObservableCollection<SummaryDisplayRow> Rows`, `string FilterText`, `Task LoadAsync()`, `event EventHandler<int>? ProjectActivationRequested`, `void ActivateProject(int)`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/ConstructionFinance.Desktop.Tests/Finance/WorkbookSummaryViewModelTests.cs
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Domain.Finance;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class WorkbookSummaryViewModelTests
{
    private static ViTri ViTriWithAdvance(decimal advance) => new()
    {
        Name = "V", SortOrder = 0,
        CostGroups =
        {
            new CostGroup { Name = "G", SortOrder = 0, Packages =
            {
                new Package { Name = "P", SortOrder = 0, Dots =
                {
                    new Dot { Date = new DateOnly(2026, 3, 1), Fund = Fund.NSNN, Advance = advance, SortOrder = 0 }
                } }
            } }
        }
    };

    private static WorkbookSummaryViewModel CreateSut(params Project[] projects)
    {
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.ListTreesAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(projects);
        return new WorkbookSummaryViewModel(service.Object);
    }

    [Fact]
    public async Task LoadAsync_groups_each_project_then_a_grand_total()
    {
        var sut = CreateSut(
            new Project { Id = 1, Name = "Dự án 1", ViTris = { ViTriWithAdvance(10_000_000m) } },
            new Project { Id = 2, Name = "Dự án 2", ViTris = { ViTriWithAdvance(5_000_000m) } });

        await sut.LoadAsync();

        // I + vị trí, II + vị trí, grand total = 5 rows
        sut.Rows.Count.ShouldBe(5);
        sut.Rows[0].Ordinal.ShouldBe("I");
        sut.Rows[0].Label.ShouldBe("Dự án 1");
        sut.Rows[^1].Label.ShouldBe("Tổng cộng");
        sut.Rows[^1].NSNN.ShouldBe("15.000.000");
    }

    [Fact]
    public async Task Filter_narrows_to_matching_projects_and_recomputes_the_total()
    {
        var sut = CreateSut(
            new Project { Id = 1, Name = "Cầu A", ViTris = { ViTriWithAdvance(10_000_000m) } },
            new Project { Id = 2, Name = "Đường B", ViTris = { ViTriWithAdvance(5_000_000m) } });
        await sut.LoadAsync();

        sut.FilterText = "cầu";

        // Only "Cầu A": I + vị trí + grand total = 3 rows, total = its own 10tr
        sut.Rows.Count.ShouldBe(3);
        sut.Rows[0].Label.ShouldBe("Cầu A");
        sut.Rows[^1].NSNN.ShouldBe("10.000.000");
    }

    [Fact]
    public async Task Activate_raises_the_event_with_the_project_id()
    {
        var sut = CreateSut();
        int? activated = null;
        sut.ProjectActivationRequested += (_, id) => activated = id;

        sut.ActivateProject(42);

        activated.ShouldBe(42);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter WorkbookSummaryViewModelTests`
Expected: FAIL — `WorkbookSummaryViewModel` does not exist.

- [ ] **Step 3: Implement the view-model**

```csharp
// src/ConstructionFinance.Desktop/ViewModels/Finance/WorkbookSummaryViewModel.cs
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// All-projects Tổng hợp (tree model): every project's Vị trí aggregated, grouped
/// Project → Vị trí → grand total, with an instant name filter. Double-click a
/// project row to open its workbook tab.
public partial class WorkbookSummaryViewModel : ViewModelBase
{
    private readonly IFinanceProjectService _service;
    private List<Project> _all = new();

    public WorkbookSummaryViewModel(IFinanceProjectService service) => _service = service;

    public ObservableCollection<SummaryDisplayRow> Rows { get; } = new();

    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private SummaryDisplayRow? _selectedRow;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            _all = (await _service.ListTreesAsync()).ToList();
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
            : _all.Where(p => p.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)).ToList();

        Rows.Clear();
        foreach (var row in WorkbookSummaryBuilder.BuildAll(matches))
            Rows.Add(SummaryDisplayRow.From(row));
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    /// Raised on double-click; the host workbook switches to the project's tab.
    public event EventHandler<int>? ProjectActivationRequested;

    public void ActivateProject(int projectId) => ProjectActivationRequested?.Invoke(this, projectId);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter WorkbookSummaryViewModelTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Create the view**

```xml
<!-- src/ConstructionFinance.Desktop/Views/Finance/WorkbookSummaryView.axaml -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Finance"
             x:Class="ConstructionFinance.Desktop.Views.Finance.WorkbookSummaryView"
             x:DataType="vm:WorkbookSummaryViewModel">
    <DockPanel>
        <StackPanel DockPanel.Dock="Top" Spacing="2" Margin="0,0,0,12">
            <TextBlock Classes="pageTitle" Text="Tổng hợp toàn bộ dự án"/>
            <TextBlock Classes="pageSubtitle"
                       Text="Gộp mọi dự án theo Vị trí — nhấp đúp dòng dự án để mở sổ theo dõi"/>
        </StackPanel>

        <Border DockPanel.Dock="Top" Classes="toolbarStrip" Margin="0,0,0,8">
            <StackPanel Orientation="Horizontal" Spacing="8">
                <TextBlock Text="Lọc theo tên:" Classes="formLabel" VerticalAlignment="Center"/>
                <TextBox MinWidth="240" Text="{Binding FilterText}"/>
                <Button Classes="toolbar" Command="{Binding RefreshCommand}" Content="Làm mới"/>
            </StackPanel>
        </Border>

        <DataGrid ItemsSource="{Binding Rows}" SelectedItem="{Binding SelectedRow}"
                  IsReadOnly="True" GridLinesVisibility="All" HeadersVisibility="All"
                  CanUserResizeColumns="True" CanUserSortColumns="False"
                  DoubleTapped="OnRowDoubleTapped">
            <DataGrid.Columns>
                <DataGridTextColumn Header="TT" Binding="{Binding Ordinal}" Width="48"/>
                <DataGridTextColumn Header="Tên dự án / vị trí" Binding="{Binding Label}" Width="220"/>
                <DataGridTextColumn Header="Đơn vị thi công" Binding="{Binding Contractor}" Width="150"/>
                <DataGridTextColumn Header="Giá trị HĐ, PLHĐ" Binding="{Binding ContractValue}" Width="130"/>
                <DataGridTextColumn Header="Tạm ứng" Binding="{Binding Advance}" Width="120"/>
                <DataGridTextColumn Header="TT KLHT" Binding="{Binding KLHT}" Width="120"/>
                <DataGridTextColumn Header="Thu hồi" Binding="{Binding Recovered}" Width="120"/>
                <DataGridTextColumn Header="TT cho nhà thầu" Binding="{Binding DisbursedToContractor}" Width="130"/>
                <DataGridTextColumn Header="Tạm ứng chưa thu hồi" Binding="{Binding Unrecovered}" Width="140"/>
                <DataGridTextColumn Header="GN Dự phòng NSNN" Binding="{Binding NSNN}" Width="140"/>
                <DataGridTextColumn Header="GN Quỹ DTNH" Binding="{Binding NgoaiHoi}" Width="130"/>
                <DataGridTextColumn Header="Tổng đã giải ngân" Binding="{Binding TotalDisbursed}" Width="140"/>
                <DataGridTextColumn Header="Quyết toán năm" Binding="{Binding YearlyText}" Width="260"/>
            </DataGrid.Columns>
        </DataGrid>
    </DockPanel>
</UserControl>
```

```csharp
// src/ConstructionFinance.Desktop/Views/Finance/WorkbookSummaryView.axaml.cs
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using ConstructionFinance.Desktop.ViewModels.Finance;

namespace ConstructionFinance.Desktop.Views.Finance;

public partial class WorkbookSummaryView : UserControl
{
    public WorkbookSummaryView() => AvaloniaXamlLoader.Load(this);

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is WorkbookSummaryViewModel vm && vm.SelectedRow is { ProjectId: > 0 } row)
            vm.ActivateProject(row.ProjectId);
    }
}
```

- [ ] **Step 6: Register in DI**

In `src/ConstructionFinance.Desktop/Common/HostBuilder.cs`, next to the other Finance registrations, add:
```csharp
services.AddTransient<ViewModels.Finance.WorkbookSummaryViewModel>();
```

- [ ] **Step 7: Add the nav case**

In `src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs`, inside `NavigateToAsync`'s `switch`, add before `default:`:
```csharp
case "WorkbookSummary":
    var wsvm = _services.GetRequiredService<ViewModels.Finance.WorkbookSummaryViewModel>();
    _nav.Reset();
    _nav.NavigateTo(wsvm);
    await wsvm.LoadAsync();
    break;
```

- [ ] **Step 8: Add the nav item**

In `src/ConstructionFinance.Desktop/Views/MainWindow.axaml`, under the `Nghiệp vụ` header, after the `NavFinanceWorkbook` item, add:
```xml
<ui:NavigationViewItem Content="Tổng hợp" Tag="WorkbookSummary">
    <ui:NavigationViewItem.IconSource><ui:SymbolIconSource Symbol="{x:Static ui:Symbol.ViewAll}"/></ui:NavigationViewItem.IconSource>
</ui:NavigationViewItem>
```
(If `ui:Symbol.ViewAll` is not available in this FluentAvalonia version, use `ui:Symbol.List`.)

- [ ] **Step 9: Build + run the full desktop test suite**

Run: `dotnet build src/ConstructionFinance.Desktop && dotnet test tests/ConstructionFinance.Desktop.Tests`
Expected: Build succeeded; all tests PASS.

- [ ] **Step 10: Commit**

```bash
git add src/ConstructionFinance.Desktop/ViewModels/Finance/WorkbookSummaryViewModel.cs \
        src/ConstructionFinance.Desktop/Views/Finance/WorkbookSummaryView.axaml \
        src/ConstructionFinance.Desktop/Views/Finance/WorkbookSummaryView.axaml.cs \
        src/ConstructionFinance.Desktop/Common/HostBuilder.cs \
        src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs \
        src/ConstructionFinance.Desktop/Views/MainWindow.axaml \
        tests/ConstructionFinance.Desktop.Tests/Finance/WorkbookSummaryViewModelTests.cs
git commit -m "feat(desktop): all-projects Tổng hợp nav screen (tree model)"
```

---

## Task 6: Full-suite gate + manual visual verification (user)

**Files:** none (verification only).

- [ ] **Step 1: Run the whole solution test suite**

Run: `dotnet test`
Expected: every test project PASS (461 baseline + the ~14 new/updated 7C tests, all green). If any fail, fix before proceeding — do not claim completion on red.

- [ ] **Step 2: Launch the app on an isolated DB with seeded tree data**

Kill stale instances first, then run in the background:
```bash
pkill -9 -f "ConstructionFinance.Desktop/bin/Debug" || true
CONSTRUCTIONFINANCE_DB=/tmp/claude-1000/-home-hieu-nguyen-management/f740667c-9541-4fac-85c8-fc2853733a91/scratchpad/phase7c.db \
DISPLAY=:1 dotnet run --project src/ConstructionFinance.Desktop
```
Seed a project → Vị trí → CostGroup → Package → a few đợt (advance + payment, both funds, dates across 2026/2027 fiscal years) either through the UI or by inserting into the temp DB (money stored as TEXT, Date as ISO TEXT, `Fund` int 0=NSNN/1=NgoaiHoi). Self-screenshot per `reference_screenshot_capability` (mss on `DISPLAY=:1`, `_NET_WM_STATE_ABOVE`).

- [ ] **Step 3: User visually confirms the numbers match `Mẫu.xlsx`**

Check, against the `Mẫu.xlsx` "Tổng hợp" and "Vị trí 1" sheets:
- The workbook's **Tổng hợp** tab shows a row per Vị trí + a bold TỔNG CỘNG, columns matching the Mẫu (E–L) and the TỔNG CỘNG equal to the ledger's TỔNG CỘNG row.
- The **Quyết toán năm** tab buckets đợt by fiscal year (1/2→31/1), NSNN/DTNH split, with a total.
- The all-projects **Tổng hợp** nav screen groups each project (roman) with its Vị trí rows and a grand Tổng cộng; the name filter narrows instantly; double-click opens the project's workbook tab.
- "." thousand grouping everywhere; empty money cells are blank.

**Completion gate:** user sign-off that the figures match `Mẫu.xlsx`. This is the phase's definition of done (spec §7, 7C row).

---

## Self-Review (performed against spec §4, §5.1, §5.3)

- **§4 WorkbookSummary (Σ ProjectAggregate, live grand total on the filtered set):** Task 1 `BuildAll` + Task 5 `ApplyFilter` (filters then rebuilds incl. grand total). ✅
- **§4 Quyết toán năm (bucket đợt by fiscal year 1/2→31/1, split NSNN/DTNH, auto from dates):** Task 3 uses `TreeAggregate.Yearly` (already fiscal-year bucketed in 7A). ✅
- **§5.1(1) per-project Tổng hợp tab (read-only, pulled from ledgers):** Task 2 + Task 4 (tab, live refresh). ✅
- **§5.1(3) Quyết toán năm tab:** Task 3 + Task 4. ✅
- **§5.1 unified bottom tab strip [Tổng hợp | Vị trí… | Quyết toán năm]:** Task 4. ✅ (User-confirmed layout choice.)
- **§5.3 all-projects Tổng hợp screen (name filter, live total, jump to project):** Task 5. ✅
- **Money "." grouping + blank-on-zero:** `SummaryDisplayRow`/`YearSettlementRow` via `MoneyText.Format`. ✅
- **Strangler (old flat summary untouched):** confirmed — no edits to `FinanceSummaryViewModel`/`SummaryPageViewModel`/`FinanceSummaryView`. ✅
- **Ledger per-year columns:** intentionally **out of 7C** per the user's decision (kept in the dedicated tab); noted so a reviewer doesn't flag it as a gap. ✅
- **Type consistency:** `SummaryDisplayRow` (Task 2) reused verbatim by Tasks 4 & 5; `ProjectTongHopViewModel.Refresh(IReadOnlyList<ViTri>)` / `YearSettlementViewModel.Refresh(IReadOnlyList<ViTri>)` signatures match their callers in Task 4; `ViTriPages`/`SelectedViTriPage` retype propagated to every existing test touched in Task 4 Step 6. ✅
- **Placeholder scan:** no TBD/TODO; every code step carries full code. ✅
