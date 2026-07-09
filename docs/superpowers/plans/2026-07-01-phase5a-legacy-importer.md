# Phase 5a — Legacy Excel Importer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A one-time importer that reads a legacy Excel workbook, maps **each `DA*` sheet → one finance `Project` + its `ProjectLineItem` đợt** (reading raw input cells, using cached formula VALUES for computed cells), captures both contract figures (Hợp đồng C + Phụ lục HĐ D) as two fields, persists them, and surfaces the result in the app.

**Architecture:** Clean Architecture + MVVM. A pure-parse `FinanceImportService` (Application, ClosedXML) turns a workbook into `Project` graphs + warnings — no DB. Persistence goes through the existing finance service. The entry grid gains the new `ContractAnnexValue` field; the Tổng hợp dự án screen gets a "Nhập Excel cũ" action (file picker → parse → confirm → save → refresh). Numbers stay a single source: imported đợt store raw cells; everything derived is recomputed by `ProjectCalculator`.

**Tech Stack:** .NET 8, ClosedXML 0.102.2, Avalonia 11.2 + FluentAvalonia, CommunityToolkit.Mvvm, EF Core 8 + SQLite, xUnit + Shouldly + Moq.

## Global Constraints

- Free-license only — reuse the already-referenced **ClosedXML 0.102.2** (Application project); no new package.
- Money is `decimal`, `HasPrecision(18,2)`. Derived figures are **never stored** — the importer reads only raw input cells (Ngày, Gói thầu, Tạm ứng, TT KLHT, Thu ứng, GN NSNN, GN Ngoại hối); `ProjectCalculator` recomputes the rest live.
- **Read cached formula VALUES, not formulas** — legacy `H`/`K`/`L` cells are formulas (`=E6`, `=F7-G7`); use `cell.GetDouble()` (returns the computed value) with the defensive empty/error guard.
- Conventions: `camelCase` locals/params, `_camelCase` private fields, `PascalCase` types/members. Vietnamese UI strings and warnings.
- Strangler: legacy import (`ExcelImportService`, ImportBatch/ProjectSummary) stays untouched. New importer + model live in the finance namespaces (`ConstructionFinance.Application.*`, `ConstructionFinance.Domain.Finance`).
- Additive migration only (add one column) — no drop of legacy tables in this phase.

---

## Legacy DA-sheet layout (from `reference/Hieu_sample.xlsx`, sheets DA1..DA5)

Row 2 = headers, row 3 = sub-headers, **row 4 = "Tổng cộng:"** (SUM formulas — the data boundary marker), data rows 5..16.

| Col | Row-5 (contract row) | Rows 6+ (đợt) |
|-----|----------------------|---------------|
| A | date | **Ngày** (date) |
| B | contract name ("Hợp đồng số 01/2026…") | **Gói thầu** |
| C | **Hợp đồng** value (e.g. 1,000,000,000) | — |
| D | **Phụ lục HĐ** value (e.g. 1,100,000,000) | — |
| E | — | **Tạm ứng** (Advance) |
| F | — | **TT KLHT** (PaymentKLHT) |
| G | — | **Thu ứng** (RecoveredAdvance) |
| H | — | **NSNN** (DisbursedNSNN — usually a formula `=E6` / `=F7−G7`; read its VALUE) |
| I | — | **Ngoại hối** (DisbursedNgoaiHoi) |

The **contract row** is the data row with a value in C or D and no đợt amounts (E/F/G empty). Project name = sheet name (no "Tên dự án:" label in the sample). Contractor name (optional) = from the `TH` sheet: the row whose col B equals this sheet name, col C.

---

## File Structure

**Domain / Data (Task 1):**
- Modify `src/ConstructionFinance.Domain/Finance/Project.cs` — add `ContractAnnexValue`.
- Modify `src/ConstructionFinance.Data/Configurations/Finance/FinanceProjectConfiguration.cs` — precision.
- New EF migration under `src/ConstructionFinance.Data/Migrations/`.

**Application — parse (Task 2) + persist (Task 3):**
- Create `src/ConstructionFinance.Application/Import/FinanceImportModels.cs` — `FinanceImportResult`.
- Create `src/ConstructionFinance.Application/Services/Interfaces/IFinanceImportService.cs`.
- Create `src/ConstructionFinance.Application/Services/Implementations/FinanceImportService.cs`.
- Modify `IFinanceProjectService` + `FinanceProjectService` — `SaveImportedAsync`.
- Modify `src/ConstructionFinance.Application/DependencyInjection.cs` — register importer.

**Desktop (Tasks 4–5):**
- Modify `FinanceProjectGridViewModel` + `FinanceProjectGridView.axaml` — surface `ContractAnnexValue`.
- Modify `FinanceSummaryViewModel` + `FinanceSummaryView.axaml` — "Nhập Excel cũ" action.

**Tests:**
- `tests/ConstructionFinance.Data.Tests/Finance/FinanceProjectRepositoryTests.cs` (annex round-trip)
- `tests/ConstructionFinance.Application.Tests/Finance/FinanceImportServiceTests.cs` (parse — in-test fixture + real sample)
- `tests/ConstructionFinance.Application.Tests/Finance/FinanceProjectServiceTests.cs` (SaveImported)
- `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceProjectGridViewModelTests.cs` (annex load/save)
- `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceSummaryViewModelTests.cs` (import command)

---

## Task 1: Domain `ContractAnnexValue` + migration

**Files:**
- Modify: `src/ConstructionFinance.Domain/Finance/Project.cs`
- Modify: `src/ConstructionFinance.Data/Configurations/Finance/FinanceProjectConfiguration.cs`
- Create: EF migration (generated)
- Test: `tests/ConstructionFinance.Data.Tests/Finance/FinanceProjectRepositoryTests.cs`

**Interfaces:**
- Produces: `decimal Project.ContractAnnexValue { get; set; }` (Phụ lục HĐ, `HasPrecision(18,2)`), persisted in `finance_projects` via an additive migration. Consumed by Tasks 2–5.

- [ ] **Step 1: Write the failing test**

Add to `tests/ConstructionFinance.Data.Tests/Finance/FinanceProjectRepositoryTests.cs`:

```csharp
    [Fact]
    public async Task ContractAnnexValue_round_trips()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new ConstructionFinance.Data.Repositories.FinanceProjectRepository(ctx);

        var id = await repo.AddAsync(new Project { Name = "DA1", ContractValue = 1_000m, ContractAnnexValue = 1_100m });
        ctx.ChangeTracker.Clear();

        var loaded = await repo.GetWithLinesAsync(id);
        loaded!.ContractValue.ShouldBe(1_000m);
        loaded.ContractAnnexValue.ShouldBe(1_100m);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Data.Tests --filter "FullyQualifiedName~ContractAnnexValue_round_trips"`
Expected: FAIL — `Project` has no `ContractAnnexValue` (compile error).

- [ ] **Step 3: Add the property**

In `src/ConstructionFinance.Domain/Finance/Project.cs`, add after `ContractValue`:

```csharp
    public decimal ContractAnnexValue { get; set; }
```

- [ ] **Step 4: Configure precision**

In `src/ConstructionFinance.Data/Configurations/Finance/FinanceProjectConfiguration.cs`, add after the `ContractValue` precision line:

```csharp
        b.Property(x => x.ContractAnnexValue).HasPrecision(18, 2);
```

- [ ] **Step 5: Generate the additive migration**

Run:
```bash
dotnet ef migrations add AddContractAnnexValue \
  --project src/ConstructionFinance.Data \
  --startup-project src/ConstructionFinance.Desktop \
  --output-dir Migrations
```
Expected: creates `Migrations/<timestamp>_AddContractAnnexValue.cs` whose `Up` does a single `AddColumn<decimal>(name: "ContractAnnexValue", table: "finance_projects", ...)` with a `0m` default, and `Down` drops it. Open the file and confirm it is exactly that one additive column (no other table changes). If `dotnet ef` fails to run, STOP and report — do not hand-write the snapshot.

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Data.Tests --filter "FullyQualifiedName~ContractAnnexValue_round_trips"`
Expected: PASS (the SQLite in-memory `EnsureCreated` picks up the new column from the model).

- [ ] **Step 7: Run the full Data suite**

Run: `dotnet test tests/ConstructionFinance.Data.Tests`
Expected: PASS (all green).

- [ ] **Step 8: Commit**

```bash
git add src/ConstructionFinance.Domain/Finance/Project.cs \
        src/ConstructionFinance.Data/Configurations/Finance/FinanceProjectConfiguration.cs \
        src/ConstructionFinance.Data/Migrations/ \
        tests/ConstructionFinance.Data.Tests/Finance/FinanceProjectRepositoryTests.cs
git commit -m "feat(finance): Project.ContractAnnexValue (Phụ lục HĐ) + additive migration"
```

---

## Task 2: `FinanceImportService.ParseWorkbookAsync` (DA sheets → Projects)

**Files:**
- Create: `src/ConstructionFinance.Application/Import/FinanceImportModels.cs`
- Create: `src/ConstructionFinance.Application/Services/Interfaces/IFinanceImportService.cs`
- Create: `src/ConstructionFinance.Application/Services/Implementations/FinanceImportService.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Finance/FinanceImportServiceTests.cs`

**Interfaces:**
- Consumes: ClosedXML; `Project`, `ProjectLineItem` (`ConstructionFinance.Domain.Finance`).
- Produces:
  - `record FinanceImportResult(IReadOnlyList<Project> Projects, IReadOnlyList<string> Warnings)`.
  - `interface IFinanceImportService { Task<FinanceImportResult> ParseWorkbookAsync(string filePath, CancellationToken ct = default); }` — parse only, NO DB. One `Project` (with `Lines`) per `DA*` sheet.
  - Used by Tasks 3, 5.

**Mapping rules (implement exactly):** find the total row (rows 2..8 with "Tổng cộng" in col A or B); data range = rows (totalRow+1)..min(lastRowUsed, totalRow+40). The **contract row** = first data row with a non-empty C or D and no amount in E/F/G → its C→`ContractValue`, D→`ContractAnnexValue`, B→`Notes` (contract reference). Every other data row that has a date in A OR any amount in E/F/G/H/I becomes a đợt (`SortOrder` 1..n in row order): A→`Date`, B→`PackageName`, E→`Advance`, F→`PaymentKLHT`, G→`RecoveredAdvance`, H→`DisbursedNSNN` (cached value), I→`DisbursedNgoaiHoi`. `Project.Name` = sheet name; `ContractorName` = TH-sheet lookup (row where col B == sheet name → col C) or null.

- [ ] **Step 1: Write the failing tests**

Create `tests/ConstructionFinance.Application.Tests/Finance/FinanceImportServiceTests.cs`:

```csharp
using ClosedXML.Excel;
using ConstructionFinance.Application.Services.Implementations;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Finance;

public class FinanceImportServiceTests
{
    private static string HieuFixture => Path.Combine(AppContext.BaseDirectory, "TestData", "Hieu.xlsx");

    // Builds one DA sheet in the legacy layout: total row 4, contract row 5, two đợt (rows 6-7).
    private static string BuildDaWorkbook()
    {
        var path = Path.Combine(Path.GetTempPath(), $"da-{Guid.NewGuid():N}.xlsx");
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("DA1");
        ws.Cell("A2").Value = "Ngày tháng"; ws.Cell("B2").Value = "Tên gói thầu";
        ws.Cell("C2").Value = "Hợp đồng"; ws.Cell("D2").Value = "Phụ lục HĐ";
        ws.Cell("B4").Value = "Tổng cộng:";
        // contract row (row 5): C/D set, no đợt amounts
        ws.Cell("A5").Value = new DateTime(2026, 1, 1);
        ws.Cell("B5").Value = "Hợp đồng số 01/2026";
        ws.Cell("C5").Value = 2_000_000_000d;
        ws.Cell("D5").Value = 2_200_000_000d;
        // đợt 1 (row 6): advance
        ws.Cell("A6").Value = new DateTime(2026, 1, 5);
        ws.Cell("B6").Value = "Đợt ứng";
        ws.Cell("E6").Value = 500_000_000d;
        ws.Cell("H6").Value = 500_000_000d;
        // đợt 2 (row 7): KLHT payment
        ws.Cell("A7").Value = new DateTime(2026, 2, 1);
        ws.Cell("F7").Value = 800_000_000d;
        ws.Cell("G7").Value = 200_000_000d;
        ws.Cell("H7").Value = 600_000_000d;
        ws.Cell("I7").Value = 100_000_000d;
        wb.SaveAs(path);
        return path;
    }

    [Fact]
    public async Task ParseWorkbook_maps_contract_row_and_dot_rows()
    {
        var path = BuildDaWorkbook();
        try
        {
            var result = await new FinanceImportService().ParseWorkbookAsync(path);

            var p = result.Projects.ShouldHaveSingleItem();
            p.Name.ShouldBe("DA1");
            p.ContractValue.ShouldBe(2_000_000_000m);
            p.ContractAnnexValue.ShouldBe(2_200_000_000m);

            p.Lines.Count.ShouldBe(2);
            var l1 = p.Lines[0];
            l1.SortOrder.ShouldBe(1);
            l1.Date.ShouldBe(new DateOnly(2026, 1, 5));
            l1.Advance.ShouldBe(500_000_000m);
            l1.DisbursedNSNN.ShouldBe(500_000_000m);
            var l2 = p.Lines[1];
            l2.PaymentKLHT.ShouldBe(800_000_000m);
            l2.RecoveredAdvance.ShouldBe(200_000_000m);
            l2.DisbursedNSNN.ShouldBe(600_000_000m);
            l2.DisbursedNgoaiHoi.ShouldBe(100_000_000m);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task ParseWorkbook_reads_cached_formula_values_from_real_sample()
    {
        File.Exists(HieuFixture).ShouldBeTrue($"Fixture missing: {HieuFixture}");
        var result = await new FinanceImportService().ParseWorkbookAsync(HieuFixture);

        result.Projects.Count.ShouldBe(5); // DA1..DA5
        var da1 = result.Projects.Single(p => p.Name == "DA1");
        da1.ContractValue.ShouldBe(1_000_000_000m);
        da1.ContractAnnexValue.ShouldBe(1_100_000_000m);
        // row 6: Advance 500M, and H6 formula '=E6' → cached value 500M read as DisbursedNSNN
        da1.Lines.ShouldContain(l => l.Advance == 500_000_000m && l.DisbursedNSNN == 500_000_000m);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter "FullyQualifiedName~FinanceImportServiceTests"`
Expected: FAIL — `FinanceImportService` does not exist (compile error).

- [ ] **Step 3: Create the result model + interface**

Create `src/ConstructionFinance.Application/Import/FinanceImportModels.cs`:

```csharp
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Import;

/// Result of parsing a legacy workbook: one Project (with Lines) per DA sheet, plus warnings.
public record FinanceImportResult(IReadOnlyList<Project> Projects, IReadOnlyList<string> Warnings);
```

Create `src/ConstructionFinance.Application/Services/Interfaces/IFinanceImportService.cs`:

```csharp
using ConstructionFinance.Application.Import;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IFinanceImportService
{
    /// Parse a legacy .xlsx into finance Projects (raw cells only). Does NOT persist.
    Task<FinanceImportResult> ParseWorkbookAsync(string filePath, CancellationToken ct = default);
}
```

- [ ] **Step 4: Implement the parser**

Create `src/ConstructionFinance.Application/Services/Implementations/FinanceImportService.cs`:

```csharp
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using ConstructionFinance.Application.Import;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Services.Implementations;

public class FinanceImportService : IFinanceImportService
{
    private static readonly Regex DaSheetPattern =
        new(@"^(da|dự\s*án|project)\s*\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public Task<FinanceImportResult> ParseWorkbookAsync(string filePath, CancellationToken ct = default) =>
        Task.Run(() => Parse(filePath), ct);

    private static FinanceImportResult Parse(string filePath)
    {
        var warnings = new List<string>();
        var projects = new List<Project>();

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var wb = new XLWorkbook(fs);

        var thSheet = wb.Worksheets.FirstOrDefault(w =>
            w.Name.Equals("TH", StringComparison.OrdinalIgnoreCase) ||
            w.Name.Equals("Tổng hợp", StringComparison.OrdinalIgnoreCase));

        var daSheets = wb.Worksheets
            .Where(w => DaSheetPattern.IsMatch(w.Name.Trim()))
            .ToList();
        if (daSheets.Count == 0)
            warnings.Add("Không tìm thấy sheet DA nào trong file.");

        foreach (var sheet in daSheets)
        {
            var totalRow = FindTotalRow(sheet);
            if (totalRow is null)
            {
                warnings.Add($"Sheet '{sheet.Name}': không thấy dòng 'Tổng cộng' — bỏ qua.");
                continue;
            }

            var dataStart = totalRow.Value + 1;
            var lastRow = Math.Min(sheet.LastRowUsed()?.RowNumber() ?? dataStart, totalRow.Value + 40);

            var project = new Project { Name = sheet.Name.Trim() };
            var contractCaptured = false;
            var sort = 0;

            for (var r = dataStart; r <= lastRow; r++)
            {
                var row = sheet.Row(r);
                var c = GetDecimal(row.Cell(3), warnings, sheet.Name);   // C Hợp đồng
                var d = GetDecimal(row.Cell(4), warnings, sheet.Name);   // D Phụ lục HĐ
                var e = GetDecimal(row.Cell(5), warnings, sheet.Name);   // E Tạm ứng
                var f = GetDecimal(row.Cell(6), warnings, sheet.Name);   // F TT KLHT
                var g = GetDecimal(row.Cell(7), warnings, sheet.Name);   // G Thu ứng
                var h = GetDecimal(row.Cell(8), warnings, sheet.Name);   // H NSNN (cached value)
                var i = GetDecimal(row.Cell(9), warnings, sheet.Name);   // I Ngoại hối
                var b = row.Cell(2).GetString().Trim();                  // B name / contract ref
                var date = GetDateOnly(row.Cell(1));                     // A Ngày

                var isContractRow = !contractCaptured && (c != 0m || d != 0m) && e == 0m && f == 0m && g == 0m;
                if (isContractRow)
                {
                    project.ContractValue = c;
                    project.ContractAnnexValue = d;
                    project.Notes = string.IsNullOrWhiteSpace(b) ? null : b;
                    contractCaptured = true;
                    continue;
                }

                var hasAmount = e != 0m || f != 0m || g != 0m || h != 0m || i != 0m;
                if (!hasAmount && date is null) continue; // blank row

                project.Lines.Add(new ProjectLineItem
                {
                    SortOrder = ++sort,
                    Date = date ?? default,
                    PackageName = string.IsNullOrWhiteSpace(b) ? null : b,
                    Advance = e,
                    PaymentKLHT = f,
                    RecoveredAdvance = g,
                    DisbursedNSNN = h,
                    DisbursedNgoaiHoi = i
                });
            }

            project.ContractorName = LookupContractor(thSheet, sheet.Name);
            projects.Add(project);
        }

        return new FinanceImportResult(projects, warnings);
    }

    private static int? FindTotalRow(IXLWorksheet sheet)
    {
        static bool IsTotal(string s)
        {
            var t = s.Trim().TrimEnd(':').Trim();
            return t.Equals("Tổng cộng", StringComparison.OrdinalIgnoreCase);
        }
        for (var r = 2; r <= 8; r++)
            if (IsTotal(sheet.Cell(r, 1).GetString()) || IsTotal(sheet.Cell(r, 2).GetString()))
                return r;
        return null;
    }

    private static string? LookupContractor(IXLWorksheet? th, string daSheetName)
    {
        if (th is null) return null;
        var lastRow = Math.Min(th.LastRowUsed()?.RowNumber() ?? 0, 60);
        for (var r = 2; r <= lastRow; r++)
        {
            if (th.Cell(r, 2).GetString().Trim().Equals(daSheetName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                var c = th.Cell(r, 3).GetString().Trim();
                return string.IsNullOrWhiteSpace(c) ? null : c;
            }
        }
        return null;
    }

    private static DateOnly? GetDateOnly(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;
        try
        {
            if (cell.DataType == XLDataType.DateTime || cell.TryGetValue<DateTime>(out _))
                return DateOnly.FromDateTime(cell.GetDateTime());
        }
        catch { /* not a date */ }
        return null;
    }

    private static decimal GetDecimal(IXLCell cell, List<string> warnings, string sheetName)
    {
        if (cell.IsEmpty()) return 0m;
        if (cell.HasFormula && cell.Value.IsError)
        {
            warnings.Add($"Sheet '{sheetName}' ô {cell.Address}: công thức lỗi — tính 0.");
            return 0m;
        }
        try { return (decimal)cell.GetDouble(); }
        catch { return 0m; } // text/blank cells in a numeric column → 0
    }
}
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter "FullyQualifiedName~FinanceImportServiceTests"`
Expected: PASS (both the in-test fixture and the real-sample test).

- [ ] **Step 6: Commit**

```bash
git add src/ConstructionFinance.Application/Import/FinanceImportModels.cs \
        src/ConstructionFinance.Application/Services/Interfaces/IFinanceImportService.cs \
        src/ConstructionFinance.Application/Services/Implementations/FinanceImportService.cs \
        tests/ConstructionFinance.Application.Tests/Finance/FinanceImportServiceTests.cs
git commit -m "feat(finance): FinanceImportService — parse legacy DA sheets to Projects"
```

---

## Task 3: Persist imported projects (`SaveImportedAsync`) + DI

**Files:**
- Modify: `src/ConstructionFinance.Application/Services/Interfaces/IFinanceProjectService.cs`
- Modify: `src/ConstructionFinance.Application/Services/Implementations/FinanceProjectService.cs`
- Modify: `src/ConstructionFinance.Application/DependencyInjection.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Finance/FinanceProjectServiceTests.cs`

**Interfaces:**
- Consumes: `IFinanceProjectRepository.AddAsync`; `IFinanceImportService` (for DI); `Project`.
- Produces: `Task<int> IFinanceProjectService.SaveImportedAsync(IReadOnlyList<Project> projects, CancellationToken ct = default)` — persists each project (with its lines) via `AddAsync`, returns the count saved. `FinanceImportService` registered in DI. Used by Task 5.

- [ ] **Step 1: Write the failing test**

Add to `tests/ConstructionFinance.Application.Tests/Finance/FinanceProjectServiceTests.cs`:

```csharp
    [Fact]
    public async Task SaveImportedAsync_persists_all_projects_with_lines()
    {
        using var h = new ServiceTestHarness();
        var svc = new FinanceProjectService(new FinanceProjectRepository(h.Context));

        var p1 = new Project { Name = "DA1", ContractValue = 1_000m, ContractAnnexValue = 1_100m };
        p1.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 1, 5), Advance = 500m, DisbursedNSNN = 500m });
        var p2 = new Project { Name = "DA2", ContractValue = 2_000m };

        var count = await svc.SaveImportedAsync(new[] { p1, p2 });
        h.Context.ChangeTracker.Clear();

        count.ShouldBe(2);
        var summaries = await svc.GetAllSummariesAsync();
        summaries.Count.ShouldBe(2);
        summaries.Single(s => s.Name == "DA1").Aggregate.TotalDisbursed.ShouldBe(500m);
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter "FullyQualifiedName~SaveImportedAsync_persists"`
Expected: FAIL — no `SaveImportedAsync` (compile error).

- [ ] **Step 3: Add to the interface**

In `IFinanceProjectService.cs`, add after `DeleteAsync`:

```csharp
    Task<int> SaveImportedAsync(IReadOnlyList<Project> projects, CancellationToken ct = default);
```

- [ ] **Step 4: Implement it**

In `FinanceProjectService.cs`, add:

```csharp
    public async Task<int> SaveImportedAsync(IReadOnlyList<Project> projects, CancellationToken ct = default)
    {
        var saved = 0;
        foreach (var project in projects)
        {
            await _repo.AddAsync(project, ct);
            saved++;
        }
        return saved;
    }
```

- [ ] **Step 5: Register the importer in DI**

In `src/ConstructionFinance.Application/DependencyInjection.cs`, next to the existing finance service registration (`services.AddScoped<IFinanceProjectService, FinanceProjectService>();`), add:

```csharp
        services.AddScoped<IFinanceImportService, FinanceImportService>();
```

- [ ] **Step 6: Run to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter "FullyQualifiedName~SaveImportedAsync_persists"`
Expected: PASS.

- [ ] **Step 7: Run the full Application suite**

Run: `dotnet test tests/ConstructionFinance.Application.Tests`
Expected: PASS (all green).

- [ ] **Step 8: Commit**

```bash
git add src/ConstructionFinance.Application/Services/Interfaces/IFinanceProjectService.cs \
        src/ConstructionFinance.Application/Services/Implementations/FinanceProjectService.cs \
        src/ConstructionFinance.Application/DependencyInjection.cs \
        tests/ConstructionFinance.Application.Tests/Finance/FinanceProjectServiceTests.cs
git commit -m "feat(finance): SaveImportedAsync + register FinanceImportService"
```

---

## Task 4: Surface `ContractAnnexValue` in the entry grid

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceProjectGridViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/Views/Finance/FinanceProjectGridView.axaml`
- Test: `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceProjectGridViewModelTests.cs`

**Interfaces:**
- Consumes: `Project.ContractAnnexValue` (Task 1).
- Produces: `decimal FinanceProjectGridViewModel.ContractAnnexValue` (observable), loaded in `LoadProjectAsync`, written in `SaveAsync`; a "Phụ lục HĐ" TextBox in the header. So an imported/edited annex value is visible and round-trips.

- [ ] **Step 1: Write the failing test**

Add to `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceProjectGridViewModelTests.cs`:

```csharp
    [Fact]
    public async Task Save_new_project_includes_ContractAnnexValue()
    {
        var (vm, svc) = Create();
        svc.Setup(s => s.CreateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>())).ReturnsAsync(5);
        svc.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project> { new() { Id = 5, Name = "DA1" } });

        vm.ProjectName = "DA1";
        vm.ContractValue = 1_000m;
        vm.ContractAnnexValue = 1_100m;
        vm.AddRowCommand.Execute(null);

        await vm.SaveCommand.ExecuteAsync(null);

        svc.Verify(s => s.CreateAsync(It.Is<Project>(p => p.ContractAnnexValue == 1_100m), It.IsAny<CancellationToken>()), Times.Once);
    }
```

(Reuse the existing `Create()` helper in that test class.)

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter "FullyQualifiedName~Save_new_project_includes_ContractAnnexValue"`
Expected: FAIL — `ContractAnnexValue` not a member of the VM (compile error).

- [ ] **Step 3: Add the observable property + load/save mapping**

In `FinanceProjectGridViewModel.cs`, add the property next to `_contractValue`:

```csharp
    [ObservableProperty] private decimal _contractAnnexValue;
```

In `NewProject()`, reset it (after `ContractValue = 0m;`):

```csharp
        ContractAnnexValue = 0m;
```

In `LoadProjectAsync`, set it (after `ContractValue = project.ContractValue;`):

```csharp
            ContractAnnexValue = project.ContractAnnexValue;
```

In `SaveAsync`, include it in the `new Project { … }` initializer (after `ContractValue = ContractValue,`):

```csharp
            ContractAnnexValue = ContractAnnexValue,
```

- [ ] **Step 4: Add the header field to the View**

In `src/ConstructionFinance.Desktop/Views/Finance/FinanceProjectGridView.axaml`, the header-fields `Grid` currently has `ColumnDefinitions="2*,*,Auto"` with Tên dự án / Nhà thầu / Giá trị HĐ. Change it to add a "Phụ lục HĐ" column — replace that grid's opening tag and append the new field:

Change `ColumnDefinitions="2*,*,Auto"` to `ColumnDefinitions="2*,*,Auto,Auto"`, and add after the "Giá trị HĐ" StackPanel (Grid.Column="2"):

```xaml
                    <StackPanel Grid.Column="3" Spacing="3" Margin="6,0,0,0">
                        <TextBlock Text="Phụ lục HĐ" Classes="formLabel"/>
                        <TextBox Text="{Binding ContractAnnexValue}" MinWidth="150" TextAlignment="Right"/>
                    </StackPanel>
```

(Bind the raw `decimal` — no `StringFormat` — per the Phase-3 binding-loop lesson.)

- [ ] **Step 5: Build + run the test**

Run: `dotnet build src/ConstructionFinance.Desktop` → 0 errors.
Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter "FullyQualifiedName~FinanceProjectGridViewModelTests"`
Expected: PASS (new test + existing grid-VM tests green).

- [ ] **Step 6: Commit**

```bash
git add src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceProjectGridViewModel.cs \
        src/ConstructionFinance.Desktop/Views/Finance/FinanceProjectGridView.axaml \
        tests/ConstructionFinance.Desktop.Tests/Finance/FinanceProjectGridViewModelTests.cs
git commit -m "feat(finance): surface Phụ lục HĐ (ContractAnnexValue) in entry grid"
```

---

## Task 5: Import action on the Tổng hợp dự án screen

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceSummaryViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/Views/Finance/FinanceSummaryView.axaml`
- Modify: `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceSummaryViewModelTests.cs`

**Interfaces:**
- Consumes: `IFinanceImportService.ParseWorkbookAsync`; `IFinanceProjectService.SaveImportedAsync`; `IDialogService` (`PickOpenFileAsync`, `ConfirmAsync`, `ShowMessageAsync`).
- Produces: `FinanceSummaryViewModel` gains an `ImportCommand` (pick .xlsx → parse → confirm count/warnings → save → reload) and two new ctor deps (`IFinanceImportService`, `IDialogService`); a "Nhập Excel cũ" button in the View toolbar.

- [ ] **Step 1: Update the existing tests for the new ctor + add an import test**

In `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceSummaryViewModelTests.cs`, update the `Create` helper to pass the two new dependencies, and add an import test. Replace the `Create` helper with:

```csharp
    private static FinanceSummaryViewModel Create(out Mock<IFinanceProjectService> svc,
        out Mock<IFinanceImportService> importer, out FakeDialogService dlg)
    {
        svc = new Mock<IFinanceProjectService>(MockBehavior.Strict);
        svc.Setup(s => s.GetAllSummariesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FinanceProjectSummary> { S(1, "Cầu A12", 500m, 500m), S(2, "Đường B3", 0m, 300m) });
        importer = new Mock<IFinanceImportService>(MockBehavior.Strict);
        dlg = new FakeDialogService();
        var nav = new FakeNavigationService();
        var services = new ServiceCollection().BuildServiceProvider();
        return new FinanceSummaryViewModel(svc.Object, importer.Object, dlg, nav, services);
    }
```

Update the two existing tests to call `Create(out _, out _, out _)` instead of `Create(out _)` / `Create(out var svc)` (keep whichever `out` they actually use). Then add:

```csharp
    [Fact]
    public async Task ImportCommand_parses_saves_and_reloads()
    {
        var vm = Create(out var svc, out var importer, out var dlg);
        dlg.PickOpenFileResult = "/tmp/legacy.xlsx";
        dlg.ConfirmResult = true;
        importer.Setup(i => i.ParseWorkbookAsync("/tmp/legacy.xlsx", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConstructionFinance.Application.Import.FinanceImportResult(
                new List<ConstructionFinance.Domain.Finance.Project> { new() { Name = "DA1" } },
                new List<string>()));
        svc.Setup(s => s.SaveImportedAsync(It.IsAny<IReadOnlyList<ConstructionFinance.Domain.Finance.Project>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await vm.ImportCommand.ExecuteAsync(null);

        importer.Verify(i => i.ParseWorkbookAsync("/tmp/legacy.xlsx", It.IsAny<CancellationToken>()), Times.Once);
        svc.Verify(s => s.SaveImportedAsync(It.Is<IReadOnlyList<ConstructionFinance.Domain.Finance.Project>>(l => l.Count == 1), It.IsAny<CancellationToken>()), Times.Once);
        svc.Verify(s => s.GetAllSummariesAsync(It.IsAny<CancellationToken>()), Times.AtLeast(2)); // initial + reload
    }

    [Fact]
    public async Task ImportCommand_noop_when_no_file_picked()
    {
        var vm = Create(out var svc, out var importer, out var dlg);
        dlg.PickOpenFileResult = null; // user cancelled

        await vm.ImportCommand.ExecuteAsync(null);

        importer.Verify(i => i.ParseWorkbookAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
```

Add `using ConstructionFinance.Application.Services.Interfaces;` if not present.

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter "FullyQualifiedName~FinanceSummaryViewModelTests"`
Expected: FAIL — ctor arity mismatch / no `ImportCommand` (compile error).

- [ ] **Step 3: Extend the ViewModel**

In `FinanceSummaryViewModel.cs`, add the fields + widen the ctor, and add the command. Update the fields block and constructor:

```csharp
    private readonly IFinanceProjectService _service;
    private readonly IFinanceImportService _importer;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;
    private readonly IServiceProvider _services;
    private List<FinanceProjectSummary> _all = new();

    public FinanceSummaryViewModel(
        IFinanceProjectService service,
        IFinanceImportService importer,
        IDialogService dialog,
        INavigationService nav,
        IServiceProvider services)
    {
        _service = service;
        _importer = importer;
        _dialog = dialog;
        _nav = nav;
        _services = services;
    }
```

Add the command (needs `using ConstructionFinance.Application.Services.Interfaces;` — already present via `IFinanceProjectService`):

```csharp
    [RelayCommand]
    private async Task ImportAsync()
    {
        var path = await _dialog.PickOpenFileAsync("Chọn file Excel cũ", new[] { ".xlsx" });
        if (string.IsNullOrEmpty(path)) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var result = await _importer.ParseWorkbookAsync(path);
            if (result.Projects.Count == 0)
            {
                await _dialog.ShowMessageAsync("Nhập Excel", "Không tìm thấy dự án (sheet DA) nào trong file.");
                return;
            }

            var msg = $"Tìm thấy {result.Projects.Count} dự án. Nhập vào ứng dụng?";
            if (result.Warnings.Count > 0)
                msg += $"\n\n{result.Warnings.Count} cảnh báo:\n- " + string.Join("\n- ", result.Warnings.Take(10));
            if (!await _dialog.ConfirmAsync("Nhập Excel", msg)) return;

            var saved = await _service.SaveImportedAsync(result.Projects);
            await LoadAsync();
            await _dialog.ShowMessageAsync("Nhập Excel", $"Đã nhập {saved} dự án.");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            await _dialog.ShowMessageAsync("Lỗi nhập Excel", ex.Message);
        }
        finally { IsBusy = false; }
    }
```

- [ ] **Step 4: Add the toolbar button to the View**

In `src/ConstructionFinance.Desktop/Views/Finance/FinanceSummaryView.axaml`, in the `toolbarStrip` grid, add a button before or after "Mở dự án" (add it to the right group; if it shares the last column, wrap both in a `StackPanel Orientation="Horizontal"`). Add:

```xaml
                        <Button Classes="toolbar" Command="{Binding ImportCommand}">
                            <StackPanel Orientation="Horizontal" Spacing="6">
                                <ui:SymbolIcon Symbol="{x:Static ui:Symbol.Import}"/>
                                <TextBlock Text="Nhập Excel cũ"/>
                            </StackPanel>
                        </Button>
```

If the toolbar's last column already holds the "Mở dự án" button directly, wrap the two buttons in a `<StackPanel Grid.Column="3" Orientation="Horizontal" Spacing="8"> … </StackPanel>` so both show.

- [ ] **Step 5: Build + run the tests**

Run: `dotnet build src/ConstructionFinance.Desktop` → 0 errors.
Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter "FullyQualifiedName~FinanceSummaryViewModelTests"`
Expected: PASS (updated existing tests + 2 new import tests).

- [ ] **Step 6: Commit**

```bash
git add src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceSummaryViewModel.cs \
        src/ConstructionFinance.Desktop/Views/Finance/FinanceSummaryView.axaml \
        tests/ConstructionFinance.Desktop.Tests/Finance/FinanceSummaryViewModelTests.cs
git commit -m "feat(finance): import legacy Excel from Tổng hợp screen"
```

---

## Task 6: DI check, full suite, and manual import run

**Files:** none (verification + any DI gap found).

**Interfaces:** none.

- [ ] **Step 1: Confirm DI resolves the summary VM with its new deps**

The summary VM now needs `IFinanceImportService` (registered in Task 3) and `IDialogService` (already a singleton). Confirm `FinanceSummaryViewModel` is still `AddTransient` in `HostBuilder.cs` and all its constructor deps are registered. If the app throws a DI resolution error at nav time, add the missing registration and note it.

- [ ] **Step 2: Build the whole solution**

Run: `dotnet build`
Expected: 0 errors.

- [ ] **Step 3: Run the entire test suite**

Run: `dotnet test`
Expected: PASS — all green (prior baseline + the new Phase 5a tests).

- [ ] **Step 4: Manual import run (controller-run; no display for subagents)**

Run: `DISPLAY=:1 dotnet run --project src/ConstructionFinance.Desktop`
Verify (confirm with the user):
1. Nav → "Tổng hợp dự án" → **Nhập Excel cũ** → pick `reference/Hieu_sample.xlsx` → confirm dialog shows "Tìm thấy 5 dự án" → after import the grid lists DA1..DA5 with live figures.
2. Open a project (double-click) → the entry grid shows its đợt, Giá trị HĐ and **Phụ lục HĐ** populated; the Giải ngân/✓ compute; totals match.
3. Re-importing the same file adds duplicates (expected — one-time importer; no dedup in scope). Note this to the user.

- [ ] **Step 5: Commit (only if Step 1 required a DI fix)**

```bash
git add -A && git commit -m "fix(finance): register import dependency for Tổng hợp screen"
```

---

## Self-Review Notes (author checklist, already applied)

- **Spec §7 coverage:** one-time importer reads legacy Excel, maps each DA sheet → 1 Project + đợt from raw cells; reads cached formula VALUES for computed cells (`H`). No legacy-table drop (deferred to final phase, per Đ9/strangler).
- **User decisions honored:** contract figures captured as **two fields** — `ContractValue` (C, Hợp đồng) + new `ContractAnnexValue` (D, Phụ lục HĐ) — stored, imported, and surfaced/editable in the entry grid.
- **Single-source / no stored derived:** importer writes only raw input cells; `ProjectCalculator` recomputes derived + totals live (Phases 1/3/4).
- **Test strategy (spec §8):** in-test ClosedXML fixture asserts exact contract-row + đợt mapping; real-sample test (`TestData/Hieu.xlsx`, already copied to test output) asserts 5 projects + cached-formula value read; persist round-trip via `SaveImportedAsync` + `GetAllSummariesAsync`.
- **Type consistency:** `FinanceImportResult(Projects, Warnings)` (Task 2) consumed in Tasks 3/5; `IFinanceImportService.ParseWorkbookAsync` and `IFinanceProjectService.SaveImportedAsync` signatures stable across tasks; `ContractAnnexValue` defined Task 1, used Tasks 2/4.
- **Binding-loop lesson applied:** the new "Phụ lục HĐ" TextBox binds the raw `decimal` (no `StringFormat`), per the Phase-3 fix.
- **Placeholders:** none — every code step carries full content.
- **Out of scope (deferred):** Export from calc (Phase 5b — next plan); dedup on re-import; Thông tri (Phase 6); dropping legacy tables (final phase). Contractor lookup is best-effort (TH sheet by sheet name), null when absent.
```
