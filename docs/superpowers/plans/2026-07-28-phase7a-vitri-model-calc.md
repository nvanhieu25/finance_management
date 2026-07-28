# Phase 7A — Vị trí hierarchy: model + calc foundation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the data model, EF persistence, and pure calc layer for the 6-level tree (Dự án → Vị trí → Chi phí → Gói thầu → Đợt) with fiscal-year settlement — no UI yet.

**Architecture:** Additive strangler. New entities (`ViTri`, `CostGroup`, `Package`, `Dot`) and a new pure calculator (`TreeCalculator`) are added **alongside** the existing flat `Domain.Finance.Project`/`ProjectLineItem` + `ProjectCalculator`, which stay untouched so the 6A workbook keeps compiling and running. The old flat types are deleted in Phase 7E. `Project` gains one additive `ViTris` navigation; nothing existing is reshaped or removed in 7A.

**Tech Stack:** .NET 8, C# 12, EF Core 8 + SQLite, xUnit + Shouldly.

## Global Constraints

- 100% free-license deps only (MIT/Apache/BSD). No Syncfusion/QuestPDF/EPPlus v5+/FluentAssertions v8+.
- Namespace for new domain types: `ConstructionFinance.Domain.Finance`. Calc types: `ConstructionFinance.Application.Calc`.
- Naming: camelCase locals, PascalCase members/types, `_camelCase` private fields.
- Money is `decimal` đồng. Dates are `DateOnly`.
- Entities derive from `ConstructionFinance.Domain.Common.BaseEntity` (`Id`, `CreatedAt`, `UpdatedAt`).
- **Additive only:** do not modify or delete existing `ProjectLineItem`, `ProjectCalculator`, old EF config, or old migrations in this phase. The only edit to an existing entity is adding `List<ViTri> ViTris` to `Finance.Project`.
- **No reset:** the EF migration ADDS tables (`finance_vitris`, `finance_cost_groups`, `finance_packages`, `finance_dots`); it never drops `finance_*`. Existing data is untouched.
- Fiscal year rule: a đợt dated `d` belongs to fiscal year `N` where `1/2/N ≤ d ≤ 31/1/(N+1)`. Concretely `FiscalYear(d) = d.Month >= 2 ? d.Year : d.Year - 1`.
- Disbursement rule: `disbursed(đợt) = Advance` if it is an advance (Advance>0 && PaymentKLHT==0), else `PaymentKLHT − RecoveredAdvance`; routed entirely to NSNN or Ngoại hối by the đợt's `Fund`.

---

### Task 1: New domain entities + `Fund` enum

**Files:**
- Create: `src/ConstructionFinance.Domain/Finance/Fund.cs`
- Create: `src/ConstructionFinance.Domain/Finance/ViTri.cs`
- Create: `src/ConstructionFinance.Domain/Finance/CostGroup.cs`
- Create: `src/ConstructionFinance.Domain/Finance/Package.cs`
- Create: `src/ConstructionFinance.Domain/Finance/Dot.cs`
- Modify: `src/ConstructionFinance.Domain/Finance/Project.cs` (add `ViTris`)
- Test: `tests/ConstructionFinance.Domain.Tests/Finance/TreeEntitiesTests.cs`

**Interfaces:**
- Consumes: `BaseEntity` from `Domain.Common`.
- Produces:
  - `enum Fund { NSNN, NgoaiHoi }`
  - `ViTri { int ProjectId; Project Project; string Name; string? NguonVon; int SortOrder; List<CostGroup> CostGroups }`
  - `CostGroup { int ViTriId; ViTri ViTri; string Name; int SortOrder; List<Package> Packages }`
  - `Package { int CostGroupId; CostGroup CostGroup; string Name; string? ContractorName; string? BankAccount; decimal ContractValue; decimal ContractAnnexValue; int SortOrder; List<Dot> Dots }`
  - `Dot { int PackageId; Package Package; int SortOrder; DateOnly Date; string? Content; string? DocumentNo; Fund Fund; decimal Advance; decimal PaymentKLHT; decimal RecoveredAdvance }`
  - `Project.ViTris : List<ViTri>`

- [ ] **Step 1: Write the failing test**

```csharp
// tests/ConstructionFinance.Domain.Tests/Finance/TreeEntitiesTests.cs
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Domain.Tests.Finance;

public class TreeEntitiesTests
{
    [Fact]
    public void Tree_navigations_default_to_empty_and_wire_up()
    {
        var project = new Project { Name = "DA1" };
        var viTri = new ViTri { Name = "Vị trí 1" };
        var group = new CostGroup { Name = "CHI PHÍ XÂY DỰNG" };
        var package = new Package { Name = "Gói 1", ContractValue = 50_000_000_000m };
        var dot = new Dot { Date = new DateOnly(2026, 3, 1), Fund = Fund.NgoaiHoi, Advance = 10_000_000m };

        project.ViTris.Add(viTri);
        viTri.CostGroups.Add(group);
        group.Packages.Add(package);
        package.Dots.Add(dot);

        project.ViTris.ShouldHaveSingleItem();
        viTri.CostGroups[0].Packages[0].Dots[0].Fund.ShouldBe(Fund.NgoaiHoi);
        new Package().Dots.ShouldBeEmpty();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Domain.Tests --filter TreeEntitiesTests`
Expected: FAIL — build error, types `ViTri`/`CostGroup`/`Package`/`Dot`/`Fund` do not exist.

- [ ] **Step 3: Write the entities**

```csharp
// Fund.cs
namespace ConstructionFinance.Domain.Finance;
public enum Fund { NSNN, NgoaiHoi }
```
```csharp
// ViTri.cs
using ConstructionFinance.Domain.Common;
namespace ConstructionFinance.Domain.Finance;
public class ViTri : BaseEntity
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public required string Name { get; set; }
    public string? NguonVon { get; set; }
    public int SortOrder { get; set; }
    public List<CostGroup> CostGroups { get; set; } = new();
}
```
```csharp
// CostGroup.cs
using ConstructionFinance.Domain.Common;
namespace ConstructionFinance.Domain.Finance;
public class CostGroup : BaseEntity
{
    public int ViTriId { get; set; }
    public ViTri ViTri { get; set; } = null!;
    public required string Name { get; set; }
    public int SortOrder { get; set; }
    public List<Package> Packages { get; set; } = new();
}
```
```csharp
// Package.cs
using ConstructionFinance.Domain.Common;
namespace ConstructionFinance.Domain.Finance;
public class Package : BaseEntity
{
    public int CostGroupId { get; set; }
    public CostGroup CostGroup { get; set; } = null!;
    public required string Name { get; set; }
    public string? ContractorName { get; set; }
    public string? BankAccount { get; set; }
    public decimal ContractValue { get; set; }
    public decimal ContractAnnexValue { get; set; }
    public int SortOrder { get; set; }
    public List<Dot> Dots { get; set; } = new();
}
```
```csharp
// Dot.cs
using ConstructionFinance.Domain.Common;
namespace ConstructionFinance.Domain.Finance;
public class Dot : BaseEntity
{
    public int PackageId { get; set; }
    public Package Package { get; set; } = null!;
    public int SortOrder { get; set; }
    public DateOnly Date { get; set; }
    public string? Content { get; set; }
    public string? DocumentNo { get; set; }
    public Fund Fund { get; set; }
    public decimal Advance { get; set; }
    public decimal PaymentKLHT { get; set; }
    public decimal RecoveredAdvance { get; set; }
}
```
Add to `Project.cs` (leave all existing members intact):
```csharp
    public List<ViTri> ViTris { get; set; } = new();
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Domain.Tests --filter TreeEntitiesTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ConstructionFinance.Domain/Finance tests/ConstructionFinance.Domain.Tests/Finance/TreeEntitiesTests.cs
git commit -m "feat(finance): Vị trí→Chi phí→Gói thầu→Đợt domain entities + Fund enum"
```

---

### Task 2: Calc models + `FiscalYear` helper

**Files:**
- Create: `src/ConstructionFinance.Application/Calc/TreeModels.cs`
- Create: `src/ConstructionFinance.Application/Calc/TreeCalculator.cs` (helper only in this task)
- Test: `tests/ConstructionFinance.Application.Tests/Calc/FiscalYearTests.cs`

**Interfaces:**
- Produces:
  - `record DotInput(DateOnly Date, Fund Fund, decimal Advance, decimal PaymentKLHT, decimal RecoveredAdvance)`
  - `record FiscalYearSettlement(int FiscalYear, decimal NSNN, decimal NgoaiHoi)`
  - `record TreeAggregate(decimal SumAdvance, decimal SumKLHT, decimal SumRecovered, decimal DisbursedNSNN, decimal DisbursedNgoaiHoi, decimal UnrecoveredAdvance, decimal TotalDisbursed, IReadOnlyList<FiscalYearSettlement> Yearly)`
  - `static int TreeCalculator.FiscalYear(DateOnly d)`

- [ ] **Step 1: Write the failing test**

```csharp
// FiscalYearTests.cs
using ConstructionFinance.Application.Calc;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Calc;

public class FiscalYearTests
{
    [Theory]
    [InlineData(2026, 2, 1, 2026)]   // 1/2/2026 → FY 2026 (start of window)
    [InlineData(2026, 12, 31, 2026)] // still FY 2026
    [InlineData(2027, 1, 31, 2026)]  // 31/1/2027 → still FY 2026 (end of window)
    [InlineData(2027, 2, 1, 2027)]   // rolls to FY 2027
    [InlineData(2026, 1, 15, 2025)]  // Jan belongs to the previous fiscal year
    public void FiscalYear_maps_by_feb_to_jan_window(int y, int m, int d, int expected)
    {
        TreeCalculator.FiscalYear(new DateOnly(y, m, d)).ShouldBe(expected);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter FiscalYearTests`
Expected: FAIL — `TreeModels`/`TreeCalculator` do not exist.

- [ ] **Step 3: Write the models + helper**

```csharp
// TreeModels.cs
using ConstructionFinance.Domain.Finance;
namespace ConstructionFinance.Application.Calc;

public record DotInput(DateOnly Date, Fund Fund, decimal Advance, decimal PaymentKLHT, decimal RecoveredAdvance);

public record FiscalYearSettlement(int FiscalYear, decimal NSNN, decimal NgoaiHoi);

public record TreeAggregate(
    decimal SumAdvance,
    decimal SumKLHT,
    decimal SumRecovered,
    decimal DisbursedNSNN,
    decimal DisbursedNgoaiHoi,
    decimal UnrecoveredAdvance,
    decimal TotalDisbursed,
    IReadOnlyList<FiscalYearSettlement> Yearly);
```
```csharp
// TreeCalculator.cs
using ConstructionFinance.Domain.Finance;
namespace ConstructionFinance.Application.Calc;

/// Pure calc for the Vị trí tree. No DB/UI. Single source of the Excel formulas.
public static class TreeCalculator
{
    /// Fiscal year runs 1/2/N → 31/1/(N+1): Feb–Dec map to N, Jan maps to N−1.
    public static int FiscalYear(DateOnly d) => d.Month >= 2 ? d.Year : d.Year - 1;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter FiscalYearTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ConstructionFinance.Application/Calc/TreeModels.cs src/ConstructionFinance.Application/Calc/TreeCalculator.cs tests/ConstructionFinance.Application.Tests/Calc/FiscalYearTests.cs
git commit -m "feat(calc): tree calc models + fiscal-year (1/2→31/1) helper"
```

---

### Task 3: `TreeCalculator.AggregatePackage` (sums, disbursement routing, số dư)

**Files:**
- Modify: `src/ConstructionFinance.Application/Calc/TreeCalculator.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Calc/TreeCalculatorPackageTests.cs`

**Interfaces:**
- Consumes: `DotInput`, `TreeAggregate`, `FiscalYearSettlement`.
- Produces:
  - `static bool TreeCalculator.IsAdvance(DotInput d)`
  - `static decimal TreeCalculator.Disbursed(DotInput d)`
  - `static TreeAggregate TreeCalculator.AggregatePackage(IReadOnlyList<DotInput> dots)`

- [ ] **Step 1: Write the failing test**

```csharp
// TreeCalculatorPackageTests.cs
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Calc;

public class TreeCalculatorPackageTests
{
    private static DotInput Advance(decimal amt, Fund f, DateOnly d) => new(d, f, amt, 0m, 0m);
    private static DotInput Payment(decimal klht, decimal recovered, Fund f, DateOnly d) => new(d, f, 0m, klht, recovered);

    [Fact]
    public void AggregatePackage_routes_disbursement_by_fund_and_derives_so_du_tam_ung()
    {
        var dots = new[]
        {
            Advance(10_000_000m, Fund.NSNN, new DateOnly(2026, 3, 1)),                 // GN NSNN 10tr
            Payment(200_000_000m, 5_000_000m, Fund.NSNN, new DateOnly(2026, 6, 1)),    // GN NSNN 195tr
            Advance(10_000_000m, Fund.NgoaiHoi, new DateOnly(2026, 7, 1)),             // GN Ngoại hối 10tr
        };

        var a = TreeCalculator.AggregatePackage(dots);

        a.SumAdvance.ShouldBe(20_000_000m);
        a.SumKLHT.ShouldBe(200_000_000m);
        a.SumRecovered.ShouldBe(5_000_000m);
        a.DisbursedNSNN.ShouldBe(205_000_000m);      // 10tr + (200tr − 5tr)
        a.DisbursedNgoaiHoi.ShouldBe(10_000_000m);
        a.TotalDisbursed.ShouldBe(215_000_000m);
        a.UnrecoveredAdvance.ShouldBe(15_000_000m);  // ΣAdvance 20tr − ΣRecovered 5tr = số dư tạm ứng
    }

    [Fact]
    public void AggregatePackage_empty_is_all_zero()
    {
        var a = TreeCalculator.AggregatePackage(System.Array.Empty<DotInput>());
        a.TotalDisbursed.ShouldBe(0m);
        a.UnrecoveredAdvance.ShouldBe(0m);
        a.Yearly.ShouldBeEmpty();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter TreeCalculatorPackageTests`
Expected: FAIL — `AggregatePackage` not defined.

- [ ] **Step 3: Implement (add to `TreeCalculator`)**

```csharp
    public static bool IsAdvance(DotInput d) => d.Advance > 0m && d.PaymentKLHT == 0m;

    public static decimal Disbursed(DotInput d) =>
        IsAdvance(d) ? d.Advance : d.PaymentKLHT - d.RecoveredAdvance;

    public static TreeAggregate AggregatePackage(IReadOnlyList<DotInput> dots)
    {
        var nsnn = dots.Where(d => d.Fund == Fund.NSNN).Sum(Disbursed);
        var ngoaiHoi = dots.Where(d => d.Fund == Fund.NgoaiHoi).Sum(Disbursed);
        var sumAdvance = dots.Sum(d => d.Advance);
        var sumRecovered = dots.Sum(d => d.RecoveredAdvance);

        return new TreeAggregate(
            SumAdvance: sumAdvance,
            SumKLHT: dots.Sum(d => d.PaymentKLHT),
            SumRecovered: sumRecovered,
            DisbursedNSNN: nsnn,
            DisbursedNgoaiHoi: ngoaiHoi,
            UnrecoveredAdvance: sumAdvance - sumRecovered,
            TotalDisbursed: nsnn + ngoaiHoi,
            Yearly: BuildYearly(dots));
    }

    private static IReadOnlyList<FiscalYearSettlement> BuildYearly(IReadOnlyList<DotInput> dots) =>
        dots
            .GroupBy(d => FiscalYear(d.Date))
            .OrderBy(g => g.Key)
            .Select(g => new FiscalYearSettlement(
                FiscalYear: g.Key,
                NSNN: g.Where(d => d.Fund == Fund.NSNN).Sum(Disbursed),
                NgoaiHoi: g.Where(d => d.Fund == Fund.NgoaiHoi).Sum(Disbursed)))
            .ToList();
```
Add `using System.Linq;` if not implicitly available.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter TreeCalculatorPackageTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ConstructionFinance.Application/Calc/TreeCalculator.cs tests/ConstructionFinance.Application.Tests/Calc/TreeCalculatorPackageTests.cs
git commit -m "feat(calc): AggregatePackage — fund routing, số dư tạm ứng, yearly split"
```

---

### Task 4: `TreeCalculator.Roll` (roll-up for Chi phí / Vị trí / Dự án)

**Files:**
- Modify: `src/ConstructionFinance.Application/Calc/TreeCalculator.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Calc/TreeCalculatorRollTests.cs`

**Interfaces:**
- Produces: `static TreeAggregate TreeCalculator.Roll(IReadOnlyList<TreeAggregate> children)` — field-wise sum with `Yearly` merged by `FiscalYear`. Used identically for CostGroup (Σ packages), ViTri (Σ groups), Project (Σ vị trí), and the Tổng hợp grand total (Σ projects).

- [ ] **Step 1: Write the failing test**

```csharp
// TreeCalculatorRollTests.cs
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Calc;

public class TreeCalculatorRollTests
{
    [Fact]
    public void Roll_sums_fields_and_merges_yearly_by_fiscal_year()
    {
        var p1 = TreeCalculator.AggregatePackage(new[]
        {
            new DotInput(new DateOnly(2026, 3, 1), Fund.NSNN, 10_000_000m, 0m, 0m),
        });
        var p2 = TreeCalculator.AggregatePackage(new[]
        {
            new DotInput(new DateOnly(2026, 5, 1), Fund.NSNN, 0m, 200_000_000m, 5_000_000m),
            new DotInput(new DateOnly(2027, 4, 1), Fund.NgoaiHoi, 0m, 30_000_000m, 0m), // FY 2027
        });

        var group = TreeCalculator.Roll(new[] { p1, p2 });

        group.DisbursedNSNN.ShouldBe(205_000_000m);
        group.DisbursedNgoaiHoi.ShouldBe(30_000_000m);
        group.UnrecoveredAdvance.ShouldBe(5_000_000m); // 10tr − 5tr
        group.Yearly.Count.ShouldBe(2);                 // FY2026 and FY2027
        group.Yearly.Single(y => y.FiscalYear == 2026).NSNN.ShouldBe(205_000_000m);
        group.Yearly.Single(y => y.FiscalYear == 2027).NgoaiHoi.ShouldBe(30_000_000m);
    }

    [Fact]
    public void Roll_of_empty_is_zero()
    {
        var r = TreeCalculator.Roll(System.Array.Empty<TreeAggregate>());
        r.TotalDisbursed.ShouldBe(0m);
        r.Yearly.ShouldBeEmpty();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter TreeCalculatorRollTests`
Expected: FAIL — `Roll` not defined.

- [ ] **Step 3: Implement (add to `TreeCalculator`)**

```csharp
    public static TreeAggregate Roll(IReadOnlyList<TreeAggregate> children)
    {
        var yearly = children
            .SelectMany(c => c.Yearly)
            .GroupBy(y => y.FiscalYear)
            .OrderBy(g => g.Key)
            .Select(g => new FiscalYearSettlement(
                FiscalYear: g.Key,
                NSNN: g.Sum(y => y.NSNN),
                NgoaiHoi: g.Sum(y => y.NgoaiHoi)))
            .ToList();

        return new TreeAggregate(
            SumAdvance: children.Sum(c => c.SumAdvance),
            SumKLHT: children.Sum(c => c.SumKLHT),
            SumRecovered: children.Sum(c => c.SumRecovered),
            DisbursedNSNN: children.Sum(c => c.DisbursedNSNN),
            DisbursedNgoaiHoi: children.Sum(c => c.DisbursedNgoaiHoi),
            UnrecoveredAdvance: children.Sum(c => c.UnrecoveredAdvance),
            TotalDisbursed: children.Sum(c => c.TotalDisbursed),
            Yearly: yearly);
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter TreeCalculatorRollTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ConstructionFinance.Application/Calc/TreeCalculator.cs tests/ConstructionFinance.Application.Tests/Calc/TreeCalculatorRollTests.cs
git commit -m "feat(calc): Roll — roll-up aggregate for group/vị trí/dự án/tổng hợp"
```

---

### Task 5: Mapping `Dot → DotInput` + `AggregateTree` façade over a Project

**Files:**
- Create: `src/ConstructionFinance.Application/Calc/DotMapping.cs`
- Modify: `src/ConstructionFinance.Application/Calc/TreeCalculator.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Calc/AggregateTreeTests.cs`

**Interfaces:**
- Consumes: `Domain.Finance.Project` (with `ViTris → CostGroups → Packages → Dots`).
- Produces:
  - `static DotInput DotMapping.ToDotInput(this Dot d)`
  - `static TreeAggregate TreeCalculator.AggregateTree(Project project)` — sorts children by `SortOrder`, aggregates each package, rolls groups→vị trí→project.

- [ ] **Step 1: Write the failing test**

```csharp
// AggregateTreeTests.cs
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Calc;

public class AggregateTreeTests
{
    [Fact]
    public void AggregateTree_rolls_whole_project()
    {
        var project = new Project
        {
            Name = "DA1",
            ViTris =
            {
                new ViTri
                {
                    Name = "Vị trí 1",
                    CostGroups =
                    {
                        new CostGroup
                        {
                            Name = "XÂY DỰNG",
                            Packages =
                            {
                                new Package
                                {
                                    Name = "Gói 1",
                                    Dots =
                                    {
                                        new Dot { Date = new DateOnly(2026,3,1), Fund = Fund.NSNN, Advance = 10_000_000m },
                                        new Dot { Date = new DateOnly(2026,6,1), Fund = Fund.NSNN, PaymentKLHT = 200_000_000m, RecoveredAdvance = 5_000_000m },
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var a = TreeCalculator.AggregateTree(project);

        a.DisbursedNSNN.ShouldBe(205_000_000m);
        a.TotalDisbursed.ShouldBe(205_000_000m);
        a.UnrecoveredAdvance.ShouldBe(5_000_000m);
        a.Yearly.ShouldHaveSingleItem();
        a.Yearly[0].FiscalYear.ShouldBe(2026);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter AggregateTreeTests`
Expected: FAIL — `ToDotInput`/`AggregateTree` not defined.

- [ ] **Step 3: Implement**

```csharp
// DotMapping.cs
using ConstructionFinance.Domain.Finance;
namespace ConstructionFinance.Application.Calc;

public static class DotMapping
{
    public static DotInput ToDotInput(this Dot d) =>
        new(d.Date, d.Fund, d.Advance, d.PaymentKLHT, d.RecoveredAdvance);
}
```
Add to `TreeCalculator`:
```csharp
    public static TreeAggregate AggregatePackageEntity(Package p) =>
        AggregatePackage(p.Dots.OrderBy(d => d.SortOrder).Select(d => d.ToDotInput()).ToList());

    public static TreeAggregate AggregateGroup(CostGroup g) =>
        Roll(g.Packages.OrderBy(p => p.SortOrder).Select(AggregatePackageEntity).ToList());

    public static TreeAggregate AggregateViTri(ViTri v) =>
        Roll(v.CostGroups.OrderBy(g => g.SortOrder).Select(AggregateGroup).ToList());

    public static TreeAggregate AggregateTree(Project project) =>
        Roll(project.ViTris.OrderBy(v => v.SortOrder).Select(AggregateViTri).ToList());
```
Add `using ConstructionFinance.Application.Calc;` is same namespace; ensure `System.Linq` in scope.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter AggregateTreeTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ConstructionFinance.Application/Calc/DotMapping.cs src/ConstructionFinance.Application/Calc/TreeCalculator.cs tests/ConstructionFinance.Application.Tests/Calc/AggregateTreeTests.cs
git commit -m "feat(calc): AggregateTree — Dot→DotInput mapping + per-level entity roll-up"
```

---

### Task 6: EF configuration for the four new entities

**Files:**
- Create: `src/ConstructionFinance.Data/Configurations/ViTriConfiguration.cs`
- Create: `src/ConstructionFinance.Data/Configurations/CostGroupConfiguration.cs`
- Create: `src/ConstructionFinance.Data/Configurations/PackageConfiguration.cs`
- Create: `src/ConstructionFinance.Data/Configurations/DotConfiguration.cs`
- Modify: `src/ConstructionFinance.Data/AppDbContext.cs` (add three DbSets + `using` aliases)
- Test: `tests/ConstructionFinance.Data.Tests/Finance/TreePersistenceTests.cs`

**Interfaces:**
- Consumes: entities from Task 1.
- Produces: `AppDbContext.ViTris`, `AppDbContext.CostGroups`, `AppDbContext.Packages`, `AppDbContext.Dots` DbSets; tables `finance_vitris`, `finance_cost_groups`, `finance_packages`, `finance_dots`; cascade-delete from `Project`→`ViTri`→`CostGroup`→`Package`→`Dot`; `Dot.Fund` stored as `int`.

Look first at an existing config (e.g. `Configurations/` folder) to copy the `IEntityTypeConfiguration<T>` + `ToTable("finance_*")` pattern used for `FinanceProjectLineItem`.

- [ ] **Step 1: Write the failing test**

```csharp
// TreePersistenceTests.cs
using ConstructionFinance.Data.Tests;
using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Finance;

public class TreePersistenceTests
{
    [Fact]
    public async Task Save_and_load_full_tree_roundtrips()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var project = new Project
        {
            Name = "DA1",
            ViTris = { new ViTri { Name = "Vị trí 1", SortOrder = 0,
                CostGroups = { new CostGroup { Name = "XÂY DỰNG",
                    Packages = { new Package { Name = "Gói 1", ContractValue = 50_000_000_000m,
                        Dots = { new Dot { Date = new DateOnly(2026,3,1), Fund = Fund.NgoaiHoi, Advance = 10_000_000m } } } } } } } }
        };
        ctx.FinanceProjects.Add(project);
        await ctx.SaveChangesAsync();

        using var ctx2 = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(ctx.Database.GetDbConnection()).Options);
        var loaded = await ctx2.FinanceProjects
            .Include(p => p.ViTris).ThenInclude(v => v.CostGroups)
            .ThenInclude(g => g.Packages).ThenInclude(pk => pk.Dots)
            .SingleAsync();

        loaded.ViTris[0].CostGroups[0].Packages[0].Dots[0].Fund.ShouldBe(Fund.NgoaiHoi);
        loaded.ViTris[0].CostGroups[0].Packages[0].ContractValue.ShouldBe(50_000_000_000m);
    }

    [Fact]
    public async Task Deleting_project_cascades_to_dots()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var project = new Project
        {
            Name = "DA1",
            ViTris = { new ViTri { Name = "V1", CostGroups = { new CostGroup { Name = "G",
                Packages = { new Package { Name = "P", Dots = { new Dot { Date = new DateOnly(2026,3,1), Fund = Fund.NSNN, Advance = 1m } } } } } } } }
        };
        ctx.FinanceProjects.Add(project);
        await ctx.SaveChangesAsync();

        ctx.FinanceProjects.Remove(project);
        await ctx.SaveChangesAsync();

        (await ctx.Dots.CountAsync()).ShouldBe(0);
        (await ctx.Packages.CountAsync()).ShouldBe(0);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Data.Tests --filter TreePersistenceTests`
Expected: FAIL — `ctx.Dots`/`ctx.Packages` DbSets do not exist.

- [ ] **Step 3: Write the DbSets + configurations**

In `AppDbContext.cs` add aliases + DbSets (keep existing lines):
```csharp
using FinanceViTri = ConstructionFinance.Domain.Finance.ViTri;
using FinanceCostGroup = ConstructionFinance.Domain.Finance.CostGroup;
using FinancePackage = ConstructionFinance.Domain.Finance.Package;
using FinanceDot = ConstructionFinance.Domain.Finance.Dot;
// ...
public DbSet<FinanceViTri> ViTris => Set<FinanceViTri>();
public DbSet<FinanceCostGroup> CostGroups => Set<FinanceCostGroup>();
public DbSet<FinancePackage> Packages => Set<FinancePackage>();
public DbSet<FinanceDot> Dots => Set<FinanceDot>();
```
Each configuration (matching the existing `finance_*` style), e.g.:
```csharp
// ViTriConfiguration.cs
using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace ConstructionFinance.Data.Configurations;

public class ViTriConfiguration : IEntityTypeConfiguration<ViTri>
{
    public void Configure(EntityTypeBuilder<ViTri> b)
    {
        b.ToTable("finance_vitris");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired();
        b.HasOne(x => x.Project).WithMany(p => p.ViTris)
            .HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
```
`CostGroupConfiguration` (`finance_cost_groups`, FK→ViTri cascade), `PackageConfiguration` (`finance_packages`, FK→CostGroup cascade, money `decimal`), `DotConfiguration` (`finance_dots`, FK→Package cascade, `b.Property(x => x.Fund).HasConversion<int>()`). Follow the money-precision convention already used by `FinanceProjectLineItem`'s config (copy its decimal config).

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Data.Tests --filter TreePersistenceTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ConstructionFinance.Data/Configurations tests/ConstructionFinance.Data.Tests/Finance/TreePersistenceTests.cs src/ConstructionFinance.Data/AppDbContext.cs
git commit -m "feat(data): EF config + DbSets + cascade for Vị trí tree"
```

---

### Task 7: EF migration `AddViTriHierarchy`

**Files:**
- Create: `src/ConstructionFinance.Data/Migrations/*_AddViTriHierarchy.cs` (generated)
- Modify: `src/ConstructionFinance.Data/Migrations/AppDbContextModelSnapshot.cs` (generated)

**Interfaces:** none new — this makes the real `app.db` schema match the model added in Task 6. Additive: only `CreateTable` for the four `finance_*` tables; no existing table touched.

- [ ] **Step 1: Generate the migration**

Run:
```bash
dotnet ef migrations add AddViTriHierarchy \
  -p src/ConstructionFinance.Data -s src/ConstructionFinance.Desktop
```
Expected: new migration file created; build succeeds.

- [ ] **Step 2: Inspect the migration**

Open the generated `*_AddViTriHierarchy.cs`. Verify: it only calls `migrationBuilder.CreateTable` for `finance_vitris`, `finance_cost_groups`, `finance_packages`, `finance_dots` with the cascade FKs — and does **not** drop or alter any existing `finance_*` table. If it contains any `DropTable`/`DropColumn`, stop and fix the model (something non-additive slipped in).

- [ ] **Step 3: Verify it applies cleanly**

Run:
```bash
dotnet build src/ConstructionFinance.Desktop
CONSTRUCTIONFINANCE_DB="$(mktemp -d)/mig.db" dotnet run --project src/ConstructionFinance.Desktop -- --migrate-only 2>/dev/null || true
```
(If no `--migrate-only` switch exists, instead assert via a Data.Tests that calls `ctx.Database.Migrate()` on a fresh SQLite file and confirms the four tables exist. Add that as `MigrationAppliesTests` if the switch is absent.)

- [ ] **Step 4: Full suite green**

Run: `dotnet test`
Expected: all existing tests + new tree tests PASS (no regressions in the 366 baseline).

- [ ] **Step 5: Commit**

```bash
git add src/ConstructionFinance.Data/Migrations
git commit -m "feat(data): migration AddViTriHierarchy (additive — 4 new finance_* tables)"
```

---

### Task 8: Repository — load the tree

**Files:**
- Modify: `src/ConstructionFinance.Domain/Abstractions/IFinanceProjectRepository.cs`
- Modify: `src/ConstructionFinance.Data/Repositories/FinanceProjectRepository.cs`
- Test: `tests/ConstructionFinance.Data.Tests/Finance/FinanceProjectRepositoryTreeTests.cs`

**Interfaces:**
- Produces (new methods on `IFinanceProjectRepository`, existing methods unchanged):
  - `Task<Project?> GetTreeAsync(int id, CancellationToken ct = default)` — Includes `ViTris→CostGroups→Packages→Dots`.
  - `Task<IReadOnlyList<Project>> ListTreesAsync(CancellationToken ct = default)` — same includes, ordered by `Name`.

- [ ] **Step 1: Write the failing test**

```csharp
// FinanceProjectRepositoryTreeTests.cs
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Data.Tests;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Finance;

public class FinanceProjectRepositoryTreeTests
{
    [Fact]
    public async Task GetTreeAsync_loads_all_four_levels()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new FinanceProjectRepository(ctx);
        var id = await repo.AddAsync(new Project
        {
            Name = "DA1",
            ViTris = { new ViTri { Name = "V1", CostGroups = { new CostGroup { Name = "G",
                Packages = { new Package { Name = "P",
                    Dots = { new Dot { Date = new DateOnly(2026,3,1), Fund = Fund.NSNN, Advance = 7m } } } } } } } }
        });

        var tree = await repo.GetTreeAsync(id);

        tree.ShouldNotBeNull();
        tree!.ViTris[0].CostGroups[0].Packages[0].Dots[0].Advance.ShouldBe(7m);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Data.Tests --filter FinanceProjectRepositoryTreeTests`
Expected: FAIL — `GetTreeAsync` not defined.

- [ ] **Step 3: Implement**

Add to the interface:
```csharp
    Task<Project?> GetTreeAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> ListTreesAsync(CancellationToken ct = default);
```
Add to `FinanceProjectRepository` (leave existing methods intact):
```csharp
    public async Task<Project?> GetTreeAsync(int id, CancellationToken ct = default) =>
        await _context.FinanceProjects
            .Include(p => p.ViTris).ThenInclude(v => v.CostGroups)
                .ThenInclude(g => g.Packages).ThenInclude(pk => pk.Dots)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Project>> ListTreesAsync(CancellationToken ct = default) =>
        await _context.FinanceProjects
            .Include(p => p.ViTris).ThenInclude(v => v.CostGroups)
                .ThenInclude(g => g.Packages).ThenInclude(pk => pk.Dots)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Data.Tests --filter FinanceProjectRepositoryTreeTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ConstructionFinance.Domain/Abstractions/IFinanceProjectRepository.cs src/ConstructionFinance.Data/Repositories/FinanceProjectRepository.cs tests/ConstructionFinance.Data.Tests/Finance/FinanceProjectRepositoryTreeTests.cs
git commit -m "feat(data): repository GetTreeAsync/ListTreesAsync for Vị trí tree"
```

---

### Task 9: Service — live tree aggregate

**Files:**
- Modify: `src/ConstructionFinance.Application/Services/Interfaces/IFinanceProjectService.cs`
- Modify: `src/ConstructionFinance.Application/Services/Implementations/FinanceProjectService.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Services/FinanceProjectServiceTreeTests.cs` (create folder if absent)

**Interfaces:**
- Consumes: `IFinanceProjectRepository.GetTreeAsync`, `TreeCalculator.AggregateTree`.
- Produces (new method on `IFinanceProjectService`):
  - `Task<TreeAggregate?> GetTreeAggregateAsync(int projectId, CancellationToken ct = default)`

Use a fake/stub `IFinanceProjectRepository` (return a hand-built `Project` tree) so the test stays in the Application layer without a DB — match how other Application service tests fake their repos.

- [ ] **Step 1: Write the failing test**

```csharp
// FinanceProjectServiceTreeTests.cs
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Services;

public class FinanceProjectServiceTreeTests
{
    private sealed class FakeRepo : IFinanceProjectRepository
    {
        private readonly Project _tree;
        public FakeRepo(Project tree) => _tree = tree;
        public Task<Project?> GetTreeAsync(int id, CancellationToken ct = default) => Task.FromResult<Project?>(_tree);
        // Remaining members throw NotImplementedException — unused by this test.
        public Task<int> AddAsync(Project p, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Project?> GetWithLinesAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Project>> ListWithLinesAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Project>> ListTreesAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateAsync(Project p, CancellationToken ct = default) => throw new NotImplementedException();
    }

    [Fact]
    public async Task GetTreeAggregateAsync_returns_rolled_up_totals()
    {
        var tree = new Project
        {
            Name = "DA1",
            ViTris = { new ViTri { Name = "V1", CostGroups = { new CostGroup { Name = "G",
                Packages = { new Package { Name = "P",
                    Dots = { new Dot { Date = new DateOnly(2026,3,1), Fund = Fund.NSNN, PaymentKLHT = 200_000_000m, RecoveredAdvance = 5_000_000m } } } } } } } }
        };
        var svc = new FinanceProjectService(new FakeRepo(tree));

        var a = await svc.GetTreeAggregateAsync(1);

        a.ShouldNotBeNull();
        a!.DisbursedNSNN.ShouldBe(195_000_000m);
        a.SumRecovered.ShouldBe(5_000_000m);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter FinanceProjectServiceTreeTests`
Expected: FAIL — `GetTreeAggregateAsync` not defined (and `FakeRepo` won't compile until interface has `GetTreeAsync`/`ListTreesAsync` from Task 8).

- [ ] **Step 3: Implement**

Add to `IFinanceProjectService`:
```csharp
    Task<TreeAggregate?> GetTreeAggregateAsync(int projectId, CancellationToken ct = default);
```
Add to `FinanceProjectService`:
```csharp
    public async Task<TreeAggregate?> GetTreeAggregateAsync(int projectId, CancellationToken ct = default)
    {
        var project = await _repo.GetTreeAsync(projectId, ct);
        return project is null ? null : TreeCalculator.AggregateTree(project);
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter FinanceProjectServiceTreeTests`
Expected: PASS.

- [ ] **Step 5: Full suite + commit**

```bash
dotnet test
git add src/ConstructionFinance.Application/Services tests/ConstructionFinance.Application.Tests/Services/FinanceProjectServiceTreeTests.cs
git commit -m "feat(app): FinanceProjectService.GetTreeAggregateAsync (live tree aggregate)"
```

---

## Phase 7A Done — exit criteria

- `dotnet test` green (366 baseline + new tree tests), app still builds/runs the 6A workbook (old flat model untouched).
- New: `ViTri`/`CostGroup`/`Package`/`Dot`/`Fund` entities + EF tables + additive migration; `TreeCalculator` (package/roll/tree aggregates + fiscal-year 1/2→31/1); repo `GetTreeAsync`/`ListTreesAsync`; service `GetTreeAggregateAsync`.
- No UI yet — that is Phase 7B. Old flat types remain as dead-ish code until Phase 7E teardown.

## Self-review notes (author)

- Spec §3 (model) → Tasks 1, 6, 7. §4 (calc: aggregates, số dư, fiscal year) → Tasks 2–5, 9. Persistence → Tasks 6–8. UI §5, forms §6 → later phases (7B–7D), correctly out of this plan.
- Deviation from spec §7: **additive migration, no `finance_*` reset.** Rationale: keeps the app runnable and every task green during a multi-phase refactor; old tables/data are dropped in 7E instead. Flagged to the user.
- Type consistency: `TreeAggregate`/`FiscalYearSettlement`/`DotInput` names identical across Tasks 2–9; repo methods `GetTreeAsync`/`ListTreesAsync` match between Tasks 8 and 9's fake.
