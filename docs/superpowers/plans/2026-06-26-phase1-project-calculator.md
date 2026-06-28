# Phase 1 — ProjectCalculator (lõi công thức) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a pure, fully-tested calculation layer (`ProjectCalculator`) that turns a project's raw line-item inputs into computed per-line and per-project aggregates, mirroring the Excel formulas.

**Architecture:** Pure value-records + a static calculator in the Application layer. No EF, no UI, no dependency on existing entities — so it cannot break the running app and is 100% unit-testable. Later phases (persistence, grid UI, export) consume this layer as the single source of formulas.

**Tech Stack:** .NET 8, C# records, xUnit + Shouldly.

## Global Constraints
- Free-license only; offline desktop; single user.
- Naming: camelCase locals/params, `_camelCase` private fields, PascalCase types/members.
- Money is `decimal`.
- Source of truth for formulas: spec `docs/superpowers/specs/2026-06-26-excel-like-project-finance-redesign-design.md` §4.

---

## File Structure

- Create: `src/ConstructionFinance.Application/Calc/CalcModels.cs` — input/output records + `LineItemKind` enum.
- Create: `src/ConstructionFinance.Application/Calc/ProjectCalculator.cs` — the pure calculator.
- Create: `tests/ConstructionFinance.Application.Tests/Calc/ProjectCalculatorTests.cs` — all tests.

Namespace for all: `ConstructionFinance.Application.Calc`.

---

### Task 1: Per-line computation (`ComputeLine`)

**Files:**
- Create: `src/ConstructionFinance.Application/Calc/CalcModels.cs`
- Create: `src/ConstructionFinance.Application/Calc/ProjectCalculator.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Calc/ProjectCalculatorTests.cs`

**Interfaces:**
- Produces:
  - `enum LineItemKind { Advance, Payment }`
  - `record LineItemInput(DateOnly Date, string? PackageName, decimal Advance, decimal PaymentKLHT, decimal RecoveredAdvance, decimal DisbursedNSNN, decimal DisbursedNgoaiHoi)`
  - `record LineItemComputed(LineItemKind Kind, decimal Disbursed, decimal Expected, bool IsBalanced)`
  - `static LineItemComputed ProjectCalculator.ComputeLine(LineItemInput x)`
  - `static LineItemKind ProjectCalculator.InferKind(LineItemInput x)`

- [ ] **Step 1: Write the failing test**

Create `tests/ConstructionFinance.Application.Tests/Calc/ProjectCalculatorTests.cs`:
```csharp
using ConstructionFinance.Application.Calc;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Calc;

public class ProjectCalculatorTests
{
    private static LineItemInput Advance(decimal amount, decimal nsnn, decimal ngoaiHoi) =>
        new(new DateOnly(2026, 1, 5), "Ứng", amount, 0m, 0m, nsnn, ngoaiHoi);

    private static LineItemInput Payment(decimal klht, decimal recovered, decimal nsnn, decimal ngoaiHoi) =>
        new(new DateOnly(2026, 2, 20), "TT KLHT", 0m, klht, recovered, nsnn, ngoaiHoi);

    [Fact]
    public void ComputeLine_advance_disbursed_is_nsnn_plus_ngoaihoi_expected_is_advance()
    {
        var r = ProjectCalculator.ComputeLine(Advance(amount: 500m, nsnn: 500m, ngoaiHoi: 0m));
        r.Kind.ShouldBe(LineItemKind.Advance);
        r.Disbursed.ShouldBe(500m);
        r.Expected.ShouldBe(500m);
        r.IsBalanced.ShouldBeTrue();
    }

    [Fact]
    public void ComputeLine_payment_expected_is_klht_minus_recovered()
    {
        // KLHT 800 − thu hồi 200 = 600; split NSNN 420 + Ngoại hối 180 = 600 → balanced.
        var r = ProjectCalculator.ComputeLine(Payment(klht: 800m, recovered: 200m, nsnn: 420m, ngoaiHoi: 180m));
        r.Kind.ShouldBe(LineItemKind.Payment);
        r.Disbursed.ShouldBe(600m);
        r.Expected.ShouldBe(600m);
        r.IsBalanced.ShouldBeTrue();
    }

    [Fact]
    public void ComputeLine_flags_imbalance_when_split_does_not_match_klht_minus_recovered()
    {
        // KLHT 300 − thu hồi 100 = 200 expected, but split sums to 150 → not balanced.
        var r = ProjectCalculator.ComputeLine(Payment(klht: 300m, recovered: 100m, nsnn: 150m, ngoaiHoi: 0m));
        r.Expected.ShouldBe(200m);
        r.Disbursed.ShouldBe(150m);
        r.IsBalanced.ShouldBeFalse();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ConstructionFinance.Application.Tests.csproj --filter "FullyQualifiedName~ProjectCalculatorTests"`
Expected: FAIL to compile — `ProjectCalculator`/`LineItemInput` do not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ConstructionFinance.Application/Calc/CalcModels.cs`:
```csharp
namespace ConstructionFinance.Application.Calc;

public enum LineItemKind { Advance, Payment }

/// Raw inputs for one đợt (line item). All money is decimal đồng.
public record LineItemInput(
    DateOnly Date,
    string? PackageName,
    decimal Advance,
    decimal PaymentKLHT,
    decimal RecoveredAdvance,
    decimal DisbursedNSNN,
    decimal DisbursedNgoaiHoi);

/// Computed view of one đợt.
public record LineItemComputed(
    LineItemKind Kind,
    decimal Disbursed,
    decimal Expected,
    bool IsBalanced);
```

Create `src/ConstructionFinance.Application/Calc/ProjectCalculator.cs`:
```csharp
namespace ConstructionFinance.Application.Calc;

/// Single source of the Excel formulas. Pure; no DB/UI dependency.
public static class ProjectCalculator
{
    /// A đợt is an advance only when it fills Tạm ứng and no TT KLHT; otherwise a payment (default).
    public static LineItemKind InferKind(LineItemInput x) =>
        x.Advance > 0m && x.PaymentKLHT == 0m ? LineItemKind.Advance : LineItemKind.Payment;

    public static LineItemComputed ComputeLine(LineItemInput x)
    {
        var kind = InferKind(x);
        var disbursed = x.DisbursedNSNN + x.DisbursedNgoaiHoi;
        var expected = kind == LineItemKind.Advance
            ? x.Advance
            : x.PaymentKLHT - x.RecoveredAdvance;
        return new LineItemComputed(kind, disbursed, expected, disbursed == expected);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ConstructionFinance.Application.Tests.csproj --filter "FullyQualifiedName~ProjectCalculatorTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ConstructionFinance.Application/Calc/CalcModels.cs src/ConstructionFinance.Application/Calc/ProjectCalculator.cs tests/ConstructionFinance.Application.Tests/Calc/ProjectCalculatorTests.cs
git commit -m "feat(calc): per-line disbursement computation with cross-check"
```

---

### Task 2: Per-project aggregate (`Aggregate`)

**Files:**
- Modify: `src/ConstructionFinance.Application/Calc/CalcModels.cs` (add `ProjectAggregate`, `YearSettlement`)
- Modify: `src/ConstructionFinance.Application/Calc/ProjectCalculator.cs` (add `Aggregate`)
- Test: `tests/ConstructionFinance.Application.Tests/Calc/ProjectCalculatorTests.cs`

**Interfaces:**
- Consumes: `LineItemInput`, `ComputeLine` (Task 1).
- Produces:
  - `record YearSettlement(int Year, decimal NSNN, decimal NgoaiHoi, decimal KLHT, decimal Advance)`
  - `record ProjectAggregate(decimal SumAdvance, decimal SumRecovered, decimal SumKLHT, decimal SumNSNN, decimal SumNgoaiHoi, decimal UnrecoveredAdvance, decimal TotalDisbursed, IReadOnlyList<YearSettlement> YearlySettlements)`
  - `static ProjectAggregate ProjectCalculator.Aggregate(IReadOnlyList<LineItemInput> items)`

- [ ] **Step 1: Write the failing test**

Append to `ProjectCalculatorTests.cs`:
```csharp
    [Fact]
    public void Aggregate_sums_columns_and_derives_unrecovered_and_total()
    {
        var items = new[]
        {
            Advance(amount: 500m, nsnn: 500m, ngoaiHoi: 0m),         // ứng 500
            Payment(klht: 800m, recovered: 200m, nsnn: 420m, ngoaiHoi: 180m), // giải ngân 600
        };

        var a = ProjectCalculator.Aggregate(items);

        a.SumAdvance.ShouldBe(500m);
        a.SumRecovered.ShouldBe(200m);
        a.SumKLHT.ShouldBe(800m);
        a.SumNSNN.ShouldBe(920m);            // 500 + 420
        a.SumNgoaiHoi.ShouldBe(180m);        // 0 + 180
        a.UnrecoveredAdvance.ShouldBe(300m); // 500 − 200
        a.TotalDisbursed.ShouldBe(1100m);    // 920 + 180
    }

    [Fact]
    public void Aggregate_empty_returns_zeros()
    {
        var a = ProjectCalculator.Aggregate(System.Array.Empty<LineItemInput>());
        a.SumAdvance.ShouldBe(0m);
        a.TotalDisbursed.ShouldBe(0m);
        a.YearlySettlements.ShouldBeEmpty();
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ConstructionFinance.Application.Tests.csproj --filter "FullyQualifiedName~ProjectCalculatorTests"`
Expected: FAIL to compile — `Aggregate`/`ProjectAggregate` do not exist.

- [ ] **Step 3: Write minimal implementation**

Append to `CalcModels.cs`:
```csharp
/// Per-year settlement breakdown (Quyết toán theo năm).
public record YearSettlement(int Year, decimal NSNN, decimal NgoaiHoi, decimal KLHT, decimal Advance);

/// Computed totals for one project (the DA "Tổng cộng" row).
public record ProjectAggregate(
    decimal SumAdvance,
    decimal SumRecovered,
    decimal SumKLHT,
    decimal SumNSNN,
    decimal SumNgoaiHoi,
    decimal UnrecoveredAdvance,
    decimal TotalDisbursed,
    IReadOnlyList<YearSettlement> YearlySettlements);
```

Append the method inside `ProjectCalculator`:
```csharp
    public static ProjectAggregate Aggregate(IReadOnlyList<LineItemInput> items)
    {
        var sumAdvance = items.Sum(i => i.Advance);
        var sumRecovered = items.Sum(i => i.RecoveredAdvance);
        var sumKlht = items.Sum(i => i.PaymentKLHT);
        var sumNsnn = items.Sum(i => i.DisbursedNSNN);
        var sumNgoaiHoi = items.Sum(i => i.DisbursedNgoaiHoi);

        return new ProjectAggregate(
            SumAdvance: sumAdvance,
            SumRecovered: sumRecovered,
            SumKLHT: sumKlht,
            SumNSNN: sumNsnn,
            SumNgoaiHoi: sumNgoaiHoi,
            UnrecoveredAdvance: sumAdvance - sumRecovered,
            TotalDisbursed: sumNsnn + sumNgoaiHoi,
            YearlySettlements: BuildYearly(items));
    }

    private static IReadOnlyList<YearSettlement> BuildYearly(IReadOnlyList<LineItemInput> items) =>
        items
            .GroupBy(i => i.Date.Year)
            .OrderBy(g => g.Key)
            .Select(g => new YearSettlement(
                Year: g.Key,
                NSNN: g.Sum(i => i.DisbursedNSNN),
                NgoaiHoi: g.Sum(i => i.DisbursedNgoaiHoi),
                KLHT: g.Sum(i => i.PaymentKLHT),
                Advance: g.Sum(i => i.Advance)))
            .ToList();
```

Add `using System.Collections.Generic;` and `using System.Linq;` at the top of `ProjectCalculator.cs` if not already present.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ConstructionFinance.Application.Tests.csproj --filter "FullyQualifiedName~ProjectCalculatorTests"`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ConstructionFinance.Application/Calc/CalcModels.cs src/ConstructionFinance.Application/Calc/ProjectCalculator.cs tests/ConstructionFinance.Application.Tests/Calc/ProjectCalculatorTests.cs
git commit -m "feat(calc): per-project aggregate (sums, unrecovered advance, total disbursed)"
```

---

### Task 3: Yearly settlement grouping across multiple years

**Files:**
- Test: `tests/ConstructionFinance.Application.Tests/Calc/ProjectCalculatorTests.cs`

**Interfaces:**
- Consumes: `Aggregate`, `YearSettlement` (Task 2). No new production code expected — `BuildYearly` already groups by year; this task proves multi-year behavior and ordering.

- [ ] **Step 1: Write the failing test**

Append to `ProjectCalculatorTests.cs`:
```csharp
    [Fact]
    public void Aggregate_groups_yearly_settlements_by_year_ordered()
    {
        var items = new[]
        {
            new LineItemInput(new DateOnly(2026, 3, 1), "p", 0m, 100m, 0m, 100m, 0m),
            new LineItemInput(new DateOnly(2025, 6, 1), "p", 0m, 50m, 0m, 30m, 20m),
            new LineItemInput(new DateOnly(2026, 9, 1), "p", 0m, 0m, 0m, 40m, 10m),
        };

        var years = ProjectCalculator.Aggregate(items).YearlySettlements;

        years.Count.ShouldBe(2);
        years[0].Year.ShouldBe(2025);            // ordered ascending
        years[0].NSNN.ShouldBe(30m);
        years[0].NgoaiHoi.ShouldBe(20m);
        years[1].Year.ShouldBe(2026);
        years[1].NSNN.ShouldBe(140m);            // 100 + 40
        years[1].NgoaiHoi.ShouldBe(10m);
        years[1].KLHT.ShouldBe(100m);
    }
```

- [ ] **Step 2: Run test to verify it fails (or passes immediately)**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ConstructionFinance.Application.Tests.csproj --filter "FullyQualifiedName~ProjectCalculatorTests"`
Expected: PASS immediately (Task 2's `BuildYearly` already implements this). If it FAILS, fix `BuildYearly` ordering/sums to match. This task locks the behavior with a regression test.

- [ ] **Step 3: (only if Step 2 failed) Fix `BuildYearly`**

No change expected. If failing, ensure `.OrderBy(g => g.Key)` and the per-year sums match the test.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ConstructionFinance.Application.Tests.csproj --filter "FullyQualifiedName~ProjectCalculatorTests"`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add tests/ConstructionFinance.Application.Tests/Calc/ProjectCalculatorTests.cs
git commit -m "test(calc): lock multi-year settlement grouping + ordering"
```

---

### Task 4: End-to-end fidelity test (worked example mirroring the Excel)

**Files:**
- Test: `tests/ConstructionFinance.Application.Tests/Calc/ProjectCalculatorTests.cs`

**Interfaces:**
- Consumes: `ComputeLine`, `Aggregate` (Tasks 1–2). Asserts the full worked example from the spec: HĐ 1 tỷ → ứng 500tr → KLHT 800tr thu hồi 200tr → giải ngân 600tr.

- [ ] **Step 1: Write the failing test**

Append to `ProjectCalculatorTests.cs`:
```csharp
    [Fact]
    public void WorkedExample_matches_excel_DA_sheet_totals()
    {
        // Mirrors spec §1 worked example + Hiêu_sample DA totals.
        var items = new[]
        {
            // Đợt 1: ứng 500tr (giải ngân 500tr, toàn bộ NSNN)
            new LineItemInput(new DateOnly(2026, 1, 5), "Ứng đợt 1", 500m, 0m, 0m, 500m, 0m),
            // Đợt 2: KLHT 800tr, thu hồi 200tr → giải ngân 600tr (NSNN 420 + Ngoại hối 180)
            new LineItemInput(new DateOnly(2026, 2, 20), "TT KLHT đợt 1", 0m, 800m, 200m, 420m, 180m),
        };

        // Per-line cross-check both balanced.
        ProjectCalculator.ComputeLine(items[0]).IsBalanced.ShouldBeTrue();
        ProjectCalculator.ComputeLine(items[1]).Disbursed.ShouldBe(600m);

        var a = ProjectCalculator.Aggregate(items);
        a.UnrecoveredAdvance.ShouldBe(300m);   // 500 − 200 (Tạm ứng chưa thu hồi)
        a.TotalDisbursed.ShouldBe(1100m);      // 500 + 600
        a.SumNSNN.ShouldBe(920m);
        a.SumNgoaiHoi.ShouldBe(180m);
        a.YearlySettlements.ShouldHaveSingleItem().Year.ShouldBe(2026);
    }
```

- [ ] **Step 2: Run test to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ConstructionFinance.Application.Tests.csproj --filter "FullyQualifiedName~ProjectCalculatorTests"`
Expected: PASS (7 tests) — this is a fidelity check over existing code; it should pass without new production code.

- [ ] **Step 3: Run the full suite to confirm no regressions**

Run: `dotnet test ConstructionFinance.sln`
Expected: all green (existing 268 + 7 new calc tests).

- [ ] **Step 4: Commit**

```bash
git add tests/ConstructionFinance.Application.Tests/Calc/ProjectCalculatorTests.cs
git commit -m "test(calc): end-to-end worked-example fidelity against Excel totals"
```

---

## Self-Review

**Spec coverage (§4):** ComputeLine covers §4.1 (Disbursed, Expected, IsBalanced, Kind). Aggregate covers §4.2 (all sums, UnrecoveredAdvance, TotalDisbursed, YearlySettlements). Tasks 3–4 lock yearly grouping + worked example. ContractValue (§4.2) is a Project field for Phase 2, not the calculator — out of scope here, correctly. TH/global aggregation (§4.3) is just "one ProjectAggregate per project" — delivered by `Aggregate`, consumed in a later phase.

**Placeholder scan:** none — every step has full code/commands.

**Type consistency:** `LineItemInput`, `LineItemComputed`, `LineItemKind`, `ProjectAggregate`, `YearSettlement`, `ProjectCalculator.ComputeLine/Aggregate/InferKind` are used identically across Tasks 1–4. Property names (`DisbursedNSNN`, `DisbursedNgoaiHoi`, `UnrecoveredAdvance`, `TotalDisbursed`) are stable.

**Out of scope (later phases):** EF entities + persistence (Phase 2), Excel-like grid (Phase 3), query screen (Phase 4), export + legacy importer (Phase 5), Thông tri (Phase 6 / separate spec).
