# Phase 5b — Excel Export (from calc) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Export the finance data to an Excel workbook that reproduces the familiar layout — **one `DA` sheet per project + a `TH` summary sheet** — where every number is a **static value from `ProjectCalculator`** (so the exported figures equal the on-screen figures). Triggered from the Tổng hợp dự án screen; exports the **currently displayed (filtered) projects**.

**Architecture:** Clean Architecture + MVVM. A new `FinanceExportService` (Application, ClosedXML) builds a fresh `XLWorkbook` from `Project` graphs, computing derived cells via `ProjectCalculator` (`ComputeLine` per đợt, `Aggregate` per project, `Combine` for the grand total) and writing them as literal values (no formulas). The Tổng hợp screen gets an "Xuất Excel" action: gather the filtered projects (with lines), save-file picker, export, confirm.

**Tech Stack:** .NET 8, ClosedXML 0.102.2, Avalonia 11.2 + FluentAvalonia, CommunityToolkit.Mvvm, xUnit + Shouldly + Moq.

## Global Constraints

- Free-license only — reuse the already-referenced **ClosedXML 0.102.2**; no new package.
- Money `decimal`. **Single source of formulas:** every derived figure comes from `ProjectCalculator` — the exporter must NOT re-implement any sum/derivation. Cells are written as **static values** (user decision), not Excel formulas.
- Conventions: `camelCase`/`_camelCase`/`PascalCase`. All sheet labels Vietnamese.
- Strangler: legacy `ExcelExportService` (template-fill, ProjectSummaryDto) stays untouched; this is a NEW `FinanceExportService` on the finance model.
- Export scope = the **currently filtered/displayed** projects on the Tổng hợp screen (user decision), fetched with their line items.

---

## Sheet layout (reproduces the sample, values-based)

**One `DA` sheet per project** (sheet name = project `Name`, sanitized/truncated to ≤31 chars, de-duplicated):

| Row | Content |
|-----|---------|
| 1 | `DỰ ÁN: {Name}` (title, bold) |
| 2 | Headers: A `Ngày` · B `Gói thầu` · C `Hợp đồng` · D `Phụ lục HĐ` · E `Tạm ứng` · F `TT KLHT` · G `Thu hồi` · H `GN NSNN` · I `GN Ngoại hối` · J `Giải ngân` (bold) |
| 3 | Contract row: B `Hợp đồng: {Notes}` · C `ContractValue` · D `ContractAnnexValue` |
| 4..(3+n) | One đợt each (ordered by `SortOrder`): A `Date` (dd/MM/yyyy) · B `PackageName` · E `Advance` · F `PaymentKLHT` · G `RecoveredAdvance` · H `DisbursedNSNN` · I `DisbursedNgoaiHoi` · J `ComputeLine(line).Disbursed` |
| 4+n | `TỔNG CỘNG` (bold): E `SumAdvance` · F `SumKLHT` · G `SumRecovered` · H `SumNSNN` · I `SumNgoaiHoi` · J `TotalDisbursed`; and after it a cell `TU chưa thu hồi: {UnrecoveredAdvance:#,##0}` |

**One `TH` sheet** (first sheet):

| Row | Content |
|-----|---------|
| 1 | `TỔNG HỢP` (title, bold) |
| 2 | Headers: A `STT` · B `Dự án` · C `Nhà thầu` · D `Giá trị HĐ` · E `Σ Tạm ứng` · F `Σ Thu hồi` · G `TU chưa thu hồi` · H `GN NSNN` · I `GN Ngoại hối` · J `Tổng giải ngân` (bold) |
| 3.. | One row per project: STT, `Name`, `ContractorName`, `ContractValue`, `SumAdvance`, `SumRecovered`, `UnrecoveredAdvance`, `SumNSNN`, `SumNgoaiHoi`, `TotalDisbursed` |
| last | `TỔNG CỘNG` (bold): grand totals from `ProjectCalculator.Combine` over all projects' aggregates + `Sum(ContractValue)` |

Money cells: number format `#,##0`, right-aligned by ClosedXML default for numbers. Sheet order: `TH` first, then the DA sheets.

---

## File Structure

**Application (Tasks 1–2):**
- Create `src/ConstructionFinance.Application/Services/Interfaces/IFinanceExportService.cs`.
- Create `src/ConstructionFinance.Application/Services/Implementations/FinanceExportService.cs`.
- Modify `src/ConstructionFinance.Application/DependencyInjection.cs` — register exporter.

**Desktop (Task 3):**
- Modify `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceSummaryViewModel.cs` — `ExportCommand` + ctor dep.
- Modify `src/ConstructionFinance.Desktop/Views/Finance/FinanceSummaryView.axaml` — "Xuất Excel" button.

**Tests:**
- `tests/ConstructionFinance.Application.Tests/Finance/FinanceExportServiceTests.cs` (reopen-and-assert values).
- `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceSummaryViewModelTests.cs` (export command).

---

## Task 1: `FinanceExportService.ExportAsync` — DA sheets

**Files:**
- Create: `src/ConstructionFinance.Application/Services/Interfaces/IFinanceExportService.cs`
- Create: `src/ConstructionFinance.Application/Services/Implementations/FinanceExportService.cs`
- Modify: `src/ConstructionFinance.Application/DependencyInjection.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Finance/FinanceExportServiceTests.cs`

**Interfaces:**
- Consumes: ClosedXML; `Project`, `ProjectLineItem`; `ProjectCalculator.ComputeLine`/`Aggregate` (`ConstructionFinance.Application.Calc`); `LineItemMapping.ToCalcInput`.
- Produces: `interface IFinanceExportService { Task ExportAsync(IReadOnlyList<Project> projects, string targetPath, CancellationToken ct = default); }` and `FinanceExportService` writing one `DA` sheet per project (this task) with all figures from calc as static values. Registered in DI. TH sheet added in Task 2. Used by Task 3.

- [ ] **Step 1: Write the failing test**

Create `tests/ConstructionFinance.Application.Tests/Finance/FinanceExportServiceTests.cs`:

```csharp
using ClosedXML.Excel;
using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Finance;

public class FinanceExportServiceTests
{
    private static Project SampleProject()
    {
        var p = new Project { Name = "DA1", ContractValue = 2_000_000_000m, ContractAnnexValue = 2_200_000_000m, Notes = "Hợp đồng số 01" };
        p.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 1, 5), PackageName = "Đợt ứng", Advance = 500_000_000m, DisbursedNSNN = 500_000_000m });
        p.Lines.Add(new ProjectLineItem { SortOrder = 2, Date = new DateOnly(2026, 2, 1), PaymentKLHT = 800_000_000m, RecoveredAdvance = 200_000_000m, DisbursedNSNN = 420_000_000m, DisbursedNgoaiHoi = 180_000_000m });
        return p;
    }

    [Fact]
    public async Task ExportAsync_writes_a_DA_sheet_with_dot_values_and_totals()
    {
        var target = Path.Combine(Path.GetTempPath(), $"fin-exp-{Guid.NewGuid():N}.xlsx");
        try
        {
            await new FinanceExportService().ExportAsync(new[] { SampleProject() }, target);

            using var wb = new XLWorkbook(target);
            var da = wb.Worksheet("DA1");

            // contract row (row 3): C = ContractValue, D = ContractAnnexValue
            da.Cell(3, 3).GetValue<decimal>().ShouldBe(2_000_000_000m);
            da.Cell(3, 4).GetValue<decimal>().ShouldBe(2_200_000_000m);

            // đợt row 1 (row 4): E Advance, H NSNN, J Giải ngân (= H + I)
            da.Cell(4, 5).GetValue<decimal>().ShouldBe(500_000_000m);
            da.Cell(4, 8).GetValue<decimal>().ShouldBe(500_000_000m);
            da.Cell(4, 10).GetValue<decimal>().ShouldBe(500_000_000m);

            // đợt row 2 (row 5): J Giải ngân = 420M + 180M = 600M
            da.Cell(5, 10).GetValue<decimal>().ShouldBe(600_000_000m);

            // TỔNG CỘNG row (row 6): E Σadvance=500M, H Σnsnn=920M, J TotalDisbursed=1_100M
            da.Cell(6, 1).GetString().ShouldContain("TỔNG");
            da.Cell(6, 5).GetValue<decimal>().ShouldBe(500_000_000m);
            da.Cell(6, 8).GetValue<decimal>().ShouldBe(920_000_000m);
            da.Cell(6, 10).GetValue<decimal>().ShouldBe(1_100_000_000m);
        }
        finally { if (File.Exists(target)) File.Delete(target); }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter "FullyQualifiedName~FinanceExportServiceTests"`
Expected: FAIL — `FinanceExportService` does not exist (compile error).

- [ ] **Step 3: Create the interface**

Create `src/ConstructionFinance.Application/Services/Interfaces/IFinanceExportService.cs`:

```csharp
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IFinanceExportService
{
    /// Write a workbook (one DA sheet per project + a TH summary) with all figures
    /// computed by ProjectCalculator, as static values.
    Task ExportAsync(IReadOnlyList<Project> projects, string targetPath, CancellationToken ct = default);
}
```

- [ ] **Step 4: Implement the DA-sheet writer**

Create `src/ConstructionFinance.Application/Services/Implementations/FinanceExportService.cs`:

```csharp
using ClosedXML.Excel;
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Services.Implementations;

public class FinanceExportService : IFinanceExportService
{
    private const string MoneyFormat = "#,##0";

    public Task ExportAsync(IReadOnlyList<Project> projects, string targetPath, CancellationToken ct = default) =>
        Task.Run(() => Write(projects, targetPath), ct);

    private static void Write(IReadOnlyList<Project> projects, string targetPath)
    {
        using var wb = new XLWorkbook();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in projects)
            WriteDaSheet(wb, p, used);
        wb.SaveAs(targetPath);
    }

    private static void WriteDaSheet(XLWorkbook wb, Project project, HashSet<string> usedNames)
    {
        var ws = wb.AddWorksheet(UniqueSheetName(project.Name, usedNames));
        var lines = project.Lines.OrderBy(l => l.SortOrder).ToList();
        var agg = ProjectCalculator.Aggregate(lines.Select(l => l.ToCalcInput()).ToList());

        ws.Cell(1, 1).Value = $"DỰ ÁN: {project.Name}";
        ws.Cell(1, 1).Style.Font.Bold = true;

        var headers = new[] { "Ngày", "Gói thầu", "Hợp đồng", "Phụ lục HĐ", "Tạm ứng", "TT KLHT", "Thu hồi", "GN NSNN", "GN Ngoại hối", "Giải ngân" };
        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(2, c + 1).Value = headers[c];
            ws.Cell(2, c + 1).Style.Font.Bold = true;
        }

        // contract row (row 3)
        ws.Cell(3, 2).Value = string.IsNullOrWhiteSpace(project.Notes) ? "Hợp đồng" : $"Hợp đồng: {project.Notes}";
        SetMoney(ws.Cell(3, 3), project.ContractValue);
        SetMoney(ws.Cell(3, 4), project.ContractAnnexValue);

        // đợt rows (row 4+)
        var r = 4;
        foreach (var line in lines)
        {
            var computed = ProjectCalculator.ComputeLine(line.ToCalcInput());
            ws.Cell(r, 1).Value = line.Date.ToString("dd/MM/yyyy");
            ws.Cell(r, 2).Value = line.PackageName ?? "";
            SetMoney(ws.Cell(r, 5), line.Advance);
            SetMoney(ws.Cell(r, 6), line.PaymentKLHT);
            SetMoney(ws.Cell(r, 7), line.RecoveredAdvance);
            SetMoney(ws.Cell(r, 8), line.DisbursedNSNN);
            SetMoney(ws.Cell(r, 9), line.DisbursedNgoaiHoi);
            SetMoney(ws.Cell(r, 10), computed.Disbursed);
            r++;
        }

        // TỔNG CỘNG row
        ws.Cell(r, 1).Value = "TỔNG CỘNG";
        ws.Cell(r, 1).Style.Font.Bold = true;
        SetMoney(ws.Cell(r, 5), agg.SumAdvance);
        SetMoney(ws.Cell(r, 6), agg.SumKLHT);
        SetMoney(ws.Cell(r, 7), agg.SumRecovered);
        SetMoney(ws.Cell(r, 8), agg.SumNSNN);
        SetMoney(ws.Cell(r, 9), agg.SumNgoaiHoi);
        SetMoney(ws.Cell(r, 10), agg.TotalDisbursed);
        ws.Row(r).Style.Font.Bold = true;
        ws.Cell(r + 1, 2).Value = $"TU chưa thu hồi: {agg.UnrecoveredAdvance:#,##0}";

        ws.Columns().AdjustToContents();
    }

    private static void SetMoney(IXLCell cell, decimal value)
    {
        cell.Value = value;
        cell.Style.NumberFormat.Format = MoneyFormat;
    }

    /// Excel sheet names ≤31 chars, no []:*?/\, unique within the workbook.
    private static string UniqueSheetName(string raw, HashSet<string> used)
    {
        var cleaned = new string((raw ?? "DA").Where(ch => !"[]:*?/\\".Contains(ch)).ToArray()).Trim();
        if (cleaned.Length == 0) cleaned = "DA";
        if (cleaned.Length > 31) cleaned = cleaned[..31];
        var name = cleaned;
        var i = 2;
        while (!used.Add(name))
        {
            var suffix = $" ({i++})";
            name = cleaned.Length + suffix.Length > 31 ? cleaned[..(31 - suffix.Length)] + suffix : cleaned + suffix;
        }
        return name;
    }
}
```

- [ ] **Step 5: Register in DI**

In `src/ConstructionFinance.Application/DependencyInjection.cs`, next to the finance-import registration, add:

```csharp
        services.AddScoped<IFinanceExportService, FinanceExportService>();
```

- [ ] **Step 6: Run to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter "FullyQualifiedName~FinanceExportServiceTests"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/ConstructionFinance.Application/Services/Interfaces/IFinanceExportService.cs \
        src/ConstructionFinance.Application/Services/Implementations/FinanceExportService.cs \
        src/ConstructionFinance.Application/DependencyInjection.cs \
        tests/ConstructionFinance.Application.Tests/Finance/FinanceExportServiceTests.cs
git commit -m "feat(finance): FinanceExportService — DA sheets with values from calc"
```

---

## Task 2: Add the `TH` summary sheet

**Files:**
- Modify: `src/ConstructionFinance.Application/Services/Implementations/FinanceExportService.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Finance/FinanceExportServiceTests.cs`

**Interfaces:**
- Consumes: `ProjectCalculator.Aggregate`/`Combine`; the existing `WriteDaSheet` helper.
- Produces: `ExportAsync` now also writes a `TH` sheet FIRST (one row per project + a grand-total row via `Combine`). No signature change.

- [ ] **Step 1: Write the failing test**

Add to `FinanceExportServiceTests.cs`:

```csharp
    [Fact]
    public async Task ExportAsync_writes_a_TH_summary_with_per_project_rows_and_grand_total()
    {
        var p2 = new Project { Name = "DA2", ContractValue = 1_000_000_000m, ContractorName = "Nhà thầu B" };
        p2.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 3, 1), DisbursedNSNN = 300_000_000m });

        var target = Path.Combine(Path.GetTempPath(), $"fin-exp-th-{Guid.NewGuid():N}.xlsx");
        try
        {
            await new FinanceExportService().ExportAsync(new[] { SampleProject(), p2 }, target);

            using var wb = new XLWorkbook(target);
            var th = wb.Worksheet("TH");
            th.Position.ShouldBe(1); // TH is the first sheet

            // header row 2, project rows from row 3
            th.Cell(3, 2).GetString().ShouldBe("DA1");
            th.Cell(3, 10).GetValue<decimal>().ShouldBe(1_100_000_000m); // DA1 TotalDisbursed
            th.Cell(4, 2).GetString().ShouldBe("DA2");
            th.Cell(4, 3).GetString().ShouldBe("Nhà thầu B");
            th.Cell(4, 10).GetValue<decimal>().ShouldBe(300_000_000m);

            // grand-total row (row 5): J = 1_100M + 300M = 1_400M
            th.Cell(5, 1).GetString().ShouldContain("TỔNG");
            th.Cell(5, 10).GetValue<decimal>().ShouldBe(1_400_000_000m);
        }
        finally { if (File.Exists(target)) File.Delete(target); }
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter "FullyQualifiedName~ExportAsync_writes_a_TH_summary"`
Expected: FAIL — no `TH` worksheet exists yet (throws / assertion fails).

- [ ] **Step 3: Implement the TH sheet**

In `FinanceExportService.cs`, change `Write` to add the TH sheet FIRST, and add the `WriteThSheet` helper:

Replace the `Write` method body:

```csharp
    private static void Write(IReadOnlyList<Project> projects, string targetPath)
    {
        using var wb = new XLWorkbook();
        WriteThSheet(wb, projects);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "TH" };
        foreach (var p in projects)
            WriteDaSheet(wb, p, used);
        wb.SaveAs(targetPath);
    }

    private static void WriteThSheet(XLWorkbook wb, IReadOnlyList<Project> projects)
    {
        var ws = wb.AddWorksheet("TH");
        ws.Cell(1, 1).Value = "TỔNG HỢP";
        ws.Cell(1, 1).Style.Font.Bold = true;

        var headers = new[] { "STT", "Dự án", "Nhà thầu", "Giá trị HĐ", "Σ Tạm ứng", "Σ Thu hồi", "TU chưa thu hồi", "GN NSNN", "GN Ngoại hối", "Tổng giải ngân" };
        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(2, c + 1).Value = headers[c];
            ws.Cell(2, c + 1).Style.Font.Bold = true;
        }

        var aggregates = new List<ProjectAggregate>();
        var r = 3;
        var stt = 1;
        foreach (var p in projects)
        {
            var agg = ProjectCalculator.Aggregate(p.Lines.OrderBy(l => l.SortOrder).Select(l => l.ToCalcInput()).ToList());
            aggregates.Add(agg);

            ws.Cell(r, 1).Value = stt++;
            ws.Cell(r, 2).Value = p.Name;
            ws.Cell(r, 3).Value = p.ContractorName ?? "";
            SetMoney(ws.Cell(r, 4), p.ContractValue);
            SetMoney(ws.Cell(r, 5), agg.SumAdvance);
            SetMoney(ws.Cell(r, 6), agg.SumRecovered);
            SetMoney(ws.Cell(r, 7), agg.UnrecoveredAdvance);
            SetMoney(ws.Cell(r, 8), agg.SumNSNN);
            SetMoney(ws.Cell(r, 9), agg.SumNgoaiHoi);
            SetMoney(ws.Cell(r, 10), agg.TotalDisbursed);
            r++;
        }

        var total = ProjectCalculator.Combine(aggregates);
        ws.Cell(r, 1).Value = "TỔNG CỘNG";
        SetMoney(ws.Cell(r, 4), projects.Sum(p => p.ContractValue));
        SetMoney(ws.Cell(r, 5), total.SumAdvance);
        SetMoney(ws.Cell(r, 6), total.SumRecovered);
        SetMoney(ws.Cell(r, 7), total.UnrecoveredAdvance);
        SetMoney(ws.Cell(r, 8), total.SumNSNN);
        SetMoney(ws.Cell(r, 9), total.SumNgoaiHoi);
        SetMoney(ws.Cell(r, 10), total.TotalDisbursed);
        ws.Row(r).Style.Font.Bold = true;

        ws.Columns().AdjustToContents();
    }
```

(`ProjectAggregate` is in `ConstructionFinance.Application.Calc`, already imported.)

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter "FullyQualifiedName~FinanceExportServiceTests"`
Expected: PASS (both DA and TH tests).

- [ ] **Step 5: Run the full Application suite**

Run: `dotnet test tests/ConstructionFinance.Application.Tests`
Expected: PASS (all green).

- [ ] **Step 6: Commit**

```bash
git add src/ConstructionFinance.Application/Services/Implementations/FinanceExportService.cs \
        tests/ConstructionFinance.Application.Tests/Finance/FinanceExportServiceTests.cs
git commit -m "feat(finance): export TH summary sheet (grand total via Combine)"
```

---

## Task 3: "Xuất Excel" action on the Tổng hợp screen

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceSummaryViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/Views/Finance/FinanceSummaryView.axaml`
- Modify: `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceSummaryViewModelTests.cs`

**Interfaces:**
- Consumes: `IFinanceExportService.ExportAsync`; `IFinanceProjectService.GetWithLinesAsync`; `IDialogService.PickSaveFileAsync`/`ShowMessageAsync`; the displayed `Rows` (each has `ProjectId`).
- Produces: `FinanceSummaryViewModel` gains an `ExportCommand` (gather the displayed projects with lines → save picker → export → confirm) and one new ctor dep (`IFinanceExportService`); an "Xuất Excel" button in the View toolbar.

- [ ] **Step 1: Update the tests for the new ctor + add an export test**

In `FinanceSummaryViewModelTests.cs`, update the `Create` helper to add the exporter mock, and add an export test. Replace the `Create` helper with:

```csharp
    private static FinanceSummaryViewModel Create(out Mock<IFinanceProjectService> svc,
        out Mock<IFinanceImportService> importer, out Mock<IFinanceExportService> exporter, out FakeDialogService dlg)
    {
        svc = new Mock<IFinanceProjectService>(MockBehavior.Strict);
        svc.Setup(s => s.GetAllSummariesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FinanceProjectSummary> { S(1, "Cầu A12", 500m, 500m), S(2, "Đường B3", 0m, 300m) });
        importer = new Mock<IFinanceImportService>(MockBehavior.Strict);
        exporter = new Mock<IFinanceExportService>(MockBehavior.Strict);
        dlg = new FakeDialogService();
        var nav = new FakeNavigationService();
        var services = new ServiceCollection().BuildServiceProvider();
        return new FinanceSummaryViewModel(svc.Object, importer.Object, exporter.Object, dlg, nav, services);
    }
```

Update every existing `Create(out …)` call in this file to the new 4-out signature (add `out _` for the exporter slot, keeping whichever outs each test uses). Then add:

```csharp
    [Fact]
    public async Task ExportCommand_gathers_displayed_projects_and_exports()
    {
        var vm = Create(out var svc, out _, out var exporter, out var dlg);
        await vm.LoadAsync(); // Rows = 2 projects
        dlg.PickSaveFileResult = "/tmp/out.xlsx";
        svc.Setup(s => s.GetWithLinesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => new ConstructionFinance.Domain.Finance.Project { Id = id, Name = $"P{id}" });
        exporter.Setup(e => e.ExportAsync(It.IsAny<IReadOnlyList<ConstructionFinance.Domain.Finance.Project>>(), "/tmp/out.xlsx", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await vm.ExportCommand.ExecuteAsync(null);

        exporter.Verify(e => e.ExportAsync(It.Is<IReadOnlyList<ConstructionFinance.Domain.Finance.Project>>(l => l.Count == 2), "/tmp/out.xlsx", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExportCommand_noop_when_save_cancelled()
    {
        var vm = Create(out var svc, out _, out var exporter, out var dlg);
        await vm.LoadAsync();
        dlg.PickSaveFileResult = null; // cancelled

        await vm.ExportCommand.ExecuteAsync(null);

        exporter.Verify(e => e.ExportAsync(It.IsAny<IReadOnlyList<ConstructionFinance.Domain.Finance.Project>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
```

Add `PickSaveFileResult` to the fake if missing — check `FakeDialogService`: it already has `PickSaveFileResult` (string?) returned by `PickSaveFileAsync`. Add `using ConstructionFinance.Application.Services.Interfaces;` if needed.

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter "FullyQualifiedName~FinanceSummaryViewModelTests"`
Expected: FAIL — ctor arity / no `ExportCommand` (compile error).

- [ ] **Step 3: Extend the ViewModel**

In `FinanceSummaryViewModel.cs`, add the exporter field + widen the ctor:

```csharp
    private readonly IFinanceProjectService _service;
    private readonly IFinanceImportService _importer;
    private readonly IFinanceExportService _exporter;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;
    private readonly IServiceProvider _services;
    private List<FinanceProjectSummary> _all = new();

    public FinanceSummaryViewModel(
        IFinanceProjectService service,
        IFinanceImportService importer,
        IFinanceExportService exporter,
        IDialogService dialog,
        INavigationService nav,
        IServiceProvider services)
    {
        _service = service;
        _importer = importer;
        _exporter = exporter;
        _dialog = dialog;
        _nav = nav;
        _services = services;
    }
```

Add the command:

```csharp
    [RelayCommand]
    private async Task ExportAsync()
    {
        if (Rows.Count == 0)
        {
            await _dialog.ShowMessageAsync("Xuất Excel", "Không có dự án nào để xuất.");
            return;
        }

        var path = await _dialog.PickSaveFileAsync("Xuất Excel", "TongHop.xlsx", new[] { ".xlsx" });
        if (string.IsNullOrEmpty(path)) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var projects = new List<ConstructionFinance.Domain.Finance.Project>();
            foreach (var row in Rows)
            {
                var p = await _service.GetWithLinesAsync(row.ProjectId);
                if (p is not null) projects.Add(p);
            }

            await _exporter.ExportAsync(projects, path);
            await _dialog.ShowMessageAsync("Xuất Excel", $"Đã xuất {projects.Count} dự án ra:\n{path}");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            await _dialog.ShowMessageAsync("Lỗi xuất Excel", ex.Message);
        }
        finally { IsBusy = false; }
    }
```

- [ ] **Step 4: Add the toolbar button to the View**

In `src/ConstructionFinance.Desktop/Views/Finance/FinanceSummaryView.axaml`, add inside the toolbar's right-hand button `StackPanel` (next to "Nhập Excel cũ"):

```xaml
                            <Button Classes="toolbar" Command="{Binding ExportCommand}">
                                <StackPanel Orientation="Horizontal" Spacing="6">
                                    <ui:SymbolIcon Symbol="{x:Static ui:Symbol.SaveAs}"/>
                                    <TextBlock Text="Xuất Excel"/>
                                </StackPanel>
                            </Button>
```

- [ ] **Step 5: Build + run the tests**

Run: `dotnet build src/ConstructionFinance.Desktop` → 0 errors.
Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter "FullyQualifiedName~FinanceSummaryViewModelTests"`
Expected: PASS (updated existing tests + 2 new export tests).

- [ ] **Step 6: Commit**

```bash
git add src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceSummaryViewModel.cs \
        src/ConstructionFinance.Desktop/Views/Finance/FinanceSummaryView.axaml \
        tests/ConstructionFinance.Desktop.Tests/Finance/FinanceSummaryViewModelTests.cs
git commit -m "feat(finance): export displayed projects to Excel from Tổng hợp screen"
```

---

## Task 4: Full suite + manual export/round-trip run

**Files:** none (verification).

- [ ] **Step 1: Build the whole solution**

Run: `dotnet build`
Expected: 0 errors.

- [ ] **Step 2: Run the entire test suite**

Run: `dotnet test`
Expected: PASS — all green (prior baseline + the new Phase 5b tests).

- [ ] **Step 3: Manual export run (controller-run; no display for subagents)**

Run: `DISPLAY=:1 dotnet run --project src/ConstructionFinance.Desktop`
Verify (confirm with the user):
1. Nav → "Tổng hợp dự án" → import the sample first (Nhập Excel cũ) so there is data → **Xuất Excel** → save to a path → success message.
2. Open the produced file: it has a `TH` sheet (per-project rows + TỔNG CỘNG) and one `DA` sheet per project; the numbers match the on-screen figures.
3. **Round-trip:** the exported figures equal what the Tổng hợp screen shows for each project (export == screen — spec §8).
4. Typing a name filter, then Xuất Excel, produces a workbook of ONLY the filtered projects (export scope = displayed).

- [ ] **Step 4: Commit (only if a fix was needed)**

```bash
git add -A && git commit -m "fix(finance): <describe> for Excel export"
```

---

## Self-Review Notes (author checklist, already applied)

- **Spec §8 coverage:** export keeps the fill-template style (DA sheets + TH), and every number sources from `ProjectCalculator` (`ComputeLine`/`Aggregate`/`Combine`) → exported figures equal screen figures. Written as static values (user decision).
- **User decisions honored:** values (not formulas); scope = currently displayed/filtered projects (Task 3 gathers `Rows`).
- **Single-source / no re-implementation:** the exporter never re-derives a sum in code — DA totals use `Aggregate`, per-line Giải ngân uses `ComputeLine(line).Disbursed`, TH grand total uses `Combine`. Contract-value grand total is a raw `Sum` of an input cell (allowed).
- **Test strategy (spec §8 — "không smoke"):** tests reopen the produced workbook and assert exact cell VALUES for đợt cells, DA totals, TH per-project rows, and the TH grand total.
- **Strangler:** new `FinanceExportService` on the finance model; legacy `ExcelExportService`/template-fill untouched. ClosedXML reused (no new package).
- **Type consistency:** `IFinanceExportService.ExportAsync(IReadOnlyList<Project>, string, ct)` stable across Tasks 1–3; `WriteDaSheet`/`WriteThSheet`/`SetMoney`/`UniqueSheetName` internal helpers.
- **Placeholders:** none — every code step carries full content.
- **Out of scope (deferred):** merged group-header cells / exact original formula fidelity (values-based by decision); Thông tri export (Phase 6); dropping legacy tables (final phase). Sheet-name collisions handled by `UniqueSheetName`.
```
