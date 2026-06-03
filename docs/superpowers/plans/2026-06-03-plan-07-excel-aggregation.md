# Plan 07: Excel-Driven Aggregation Workflow

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Refactor the primary workflow around Excel: user uploads a `.xlsx` file following the H.xlsx template, app parses the per-project "TỔNG CỘNG" row of each `DA*` sheet → stores snapshot in DB → renders an aggregated "Tổng hợp" view + "Thông tri" view; user can export the snapshot back into the original template. This becomes the **primary** user flow for projects whose calculations are maintained in Excel. The existing manual-entry flow (Cost Tree, Payment dialogs) remains untouched for small projects.

**Why this shift:** Government accountants already maintain authoritative calculations in Excel files (H.xlsx is the gov template). Re-implementing Excel formula semantics in the app is overkill. Instead the app becomes a **viewer / aggregator** that snapshots Excel totals and produces unified reports.

**Architecture:**
- `ProjectSummary` (Domain) = 1 row per project per import batch; carries the per-project totals.
- `SettlementByYear` (Domain) = 0..N rows per `ProjectSummary`; enables multi-year flexibility (Quyết toán 2025, 2026...).
- `ImportBatch` (Domain) = grouping of snapshots taken from one upload; keeps history.
- `IExcelImportService` (Application, ClosedXML) reads the user's file: detects sheet names matching `DA*` (Vietnamese-aware, case-insensitive), reads the TỔNG CỘNG row, detects "Quyết toán năm YYYY" columns dynamically from headers.
- `IExcelExportService` (Application, ClosedXML) takes an existing template file the user picks AND a `ProjectSummary` set, overwrites the "Tổng hợp" sheet rows with current values, saves to a target path.
- `IProjectSummaryService` (Application) wraps the repository with filtering by batch + cross-year aggregation.
- `TongHopViewModel` + `ThongTriViewModel` + `ImportWizardViewModel` (Desktop) host the new UI.
- Menu is regrouped into 3 sections (Tổng hợp / Dữ liệu nguồn / Hệ thống); a fixed "📥 Import Excel" button appears in the top toolbar of every screen.

**Tech additions:**
- `ClosedXML` (already in the package graph from earlier plans — confirm in `Directory.Packages.props`; add to `Infrastructure` csproj if not present)

**Reference:** [Design doc](../specs/2026-05-19-construction-finance-app-design.md), prior plans, sample files `reference/H.xlsx` (final template) and `reference/Hiệu.xlsx` (real working file — for testing edge cases like `#REF!` cells).

**Out of scope (v1.2+):**
- Payment-row-level import (Plan 08 if needed) — current plan is snapshot only
- Sheet "QĐ" import (approval decisions) — Plan 09 if needed
- Sheet "CT1/CT2" voucher forms — Plan 10 if needed
- Multi-file batch import (only single file per ImportBatch in MVP)
- Diff view between two batches (could be nice-to-have)
- Schema validation that enforces "user template must look like H.xlsx" — instead we tolerate variation and fail-soft (skip bad sheets, warn user)

---

## Key Decisions

| # | Decision | Rationale |
|---|---|---|
| D1 | Snapshot-only import (read TỔNG CỘNG row, not per-payment rows) | User confirmed: Tổng hợp is the only need; payment-row import is overkill |
| D2 | Multi-year flexible via `SettlementByYear` child entity (Year + 4 nullable amount cols) | Each year may have different sub-columns; parser detects "Quyết toán năm YYYY" header regex |
| D3 | Funding source (NSNN/DTNH) encoded via column position, not per-row tag | Matches Excel convention; stored as separate fields on `ProjectSummary` |
| D4 | Existing manual-entry entities (CostItem, Payment) kept; not coupled to `ProjectSummary` | Two parallel flows: snapshot view (Excel-driven) and tree view (manual). Users can choose per-project |
| D5 | `ImportBatch` preserves history; never auto-delete prior batches | Audit trail for gov agency; manual cleanup in Settings if needed |
| D6 | Export rewrites the user's existing template file (they pick target file) | Preserves their custom formatting + non-data sheets (QĐ, CT1, etc.) untouched |
| D7 | Sheet name pattern matching: case-insensitive, accent-insensitive, regex `^(DA|Dự\s*án|Project)\d*` | Tolerates Vietnamese variations |
| D8 | Year detection regex on header row 4 of TH and DA: `Quyết toán\s*(đến\s*)?năm\s*(\d{4})` | Captures both "Quyết toán năm 2025" and "Quyết toán đến năm 2025" |
| D9 | `Trang chủ` placeholder removed; app opens directly on `Tổng hợp` view | Workflow-driven landing |
| D10 | New "📥 Import Excel" button in MainWindow.axaml header; always visible | Primary action; users don't need to navigate to a "Import" menu |

---

## File Structure

```
src/ConstructionFinance.Domain/
├── Entities/
│   ├── ProjectSummary.cs                       (NEW)
│   ├── SettlementByYear.cs                     (NEW)
│   └── ImportBatch.cs                          (NEW)
└── Abstractions/
    └── IProjectSummaryRepository.cs            (NEW)

src/ConstructionFinance.Data/
├── Configurations/
│   ├── ProjectSummaryConfiguration.cs          (NEW)
│   ├── SettlementByYearConfiguration.cs        (NEW)
│   └── ImportBatchConfiguration.cs             (NEW)
├── Repositories/
│   └── ProjectSummaryRepository.cs             (NEW)
├── Migrations/
│   └── 20260603_AddProjectSummary.cs           (NEW — EF gen)
└── AppDbContext.cs                              (MODIFIED — DbSets + register configs)

src/ConstructionFinance.Application/
├── DTOs/
│   ├── ProjectSummaryDto.cs                    (NEW)
│   ├── SettlementByYearDto.cs                  (NEW)
│   ├── ImportBatchDto.cs                       (NEW)
│   ├── ImportResultDto.cs                      (NEW — parse output + warnings)
│   └── TongHopAggregateDto.cs                  (NEW — totals across projects)
├── Services/Interfaces/
│   ├── IExcelImportService.cs                  (NEW)
│   ├── IExcelExportService.cs                  (NEW)
│   └── IProjectSummaryService.cs               (NEW)
├── Services/Implementations/
│   ├── ExcelImportService.cs                   (NEW — ClosedXML)
│   ├── ExcelExportService.cs                   (NEW — ClosedXML)
│   └── ProjectSummaryService.cs                (NEW)
└── DependencyInjection.cs                      (MODIFIED — register 3 services)

src/ConstructionFinance.Desktop/
├── ViewModels/
│   ├── TongHop/
│   │   ├── TongHopViewModel.cs                 (NEW)
│   │   └── ImportWizardViewModel.cs            (NEW)
│   └── ThongTri/
│       └── ThongTriViewModel.cs                (NEW)
├── Views/
│   ├── TongHop/
│   │   ├── TongHopView.axaml(.cs)              (NEW)
│   │   └── ImportWizardDialog.axaml(.cs)       (NEW)
│   └── ThongTri/
│       └── ThongTriView.axaml(.cs)             (NEW)
├── Common/HostBuilder.cs                        (MODIFIED — register new VMs)
├── ViewModels/MainWindowViewModel.cs            (MODIFIED — add Tổng hợp/Thông tri/Import nav)
└── Views/MainWindow.axaml                       (MODIFIED — regrouped menu + Import toolbar btn)

tests/ConstructionFinance.Application.Tests/
├── ExcelImportServiceTests.cs                  (NEW — uses reference/H.xlsx fixture)
└── ProjectSummaryServiceTests.cs               (NEW)

tests/ConstructionFinance.Desktop.Tests/
├── TongHop/
│   ├── TongHopViewModelTests.cs                (NEW)
│   └── ImportWizardViewModelTests.cs           (NEW)
└── ThongTri/
    └── ThongTriViewModelTests.cs               (NEW)

reference/
├── H.xlsx                                       (already present — template)
└── Hiệu.xlsx                                    (already present — sample data)

docs/
└── excel-template-format.md                    (NEW — user-facing format spec)
```

---

## Pre-flight

- [ ] **Step 0.1: Verify Plan 06 + recent fixes green**

```bash
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
git status --short
dotnet build --configuration Release
dotnet test --configuration Release --nologo --verbosity minimal
```

Expected: clean tree, 232 tests pass.

- [ ] **Step 0.2: Confirm ClosedXML is available**

```bash
grep -i "ClosedXML" /home/hieu-nguyen/management/Directory.Packages.props
```

If missing, add `<PackageVersion Include="ClosedXML" Version="0.102.2" />`. Plan 07 Task 2 will reference it from `Infrastructure.csproj`.

---

## Task 1: Domain entities + Data configurations + migration

**Files:**
- New: `src/ConstructionFinance.Domain/Entities/ProjectSummary.cs`
- New: `src/ConstructionFinance.Domain/Entities/SettlementByYear.cs`
- New: `src/ConstructionFinance.Domain/Entities/ImportBatch.cs`
- New: `src/ConstructionFinance.Domain/Abstractions/IProjectSummaryRepository.cs`
- New: `src/ConstructionFinance.Data/Configurations/ProjectSummaryConfiguration.cs`
- New: `src/ConstructionFinance.Data/Configurations/SettlementByYearConfiguration.cs`
- New: `src/ConstructionFinance.Data/Configurations/ImportBatchConfiguration.cs`
- New: `src/ConstructionFinance.Data/Repositories/ProjectSummaryRepository.cs`
- Modify: `src/ConstructionFinance.Data/AppDbContext.cs`
- Modify: `src/ConstructionFinance.Data/DependencyInjection.cs`

### Step 1.1: Entity — ImportBatch

```csharp
using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Domain.Entities;

public class ImportBatch : BaseEntity
{
    public required string SourceFileName { get; set; }
    public string? SourceFileHash { get; set; }   // SHA256 hex — for de-dup
    public DateTime ImportedAtUtc { get; set; }
    public int SheetsFound { get; set; }
    public int ProjectsImported { get; set; }
    public string? Description { get; set; }       // optional user note
    public string? WarningsJson { get; set; }       // JSON array of parse warnings

    public List<ProjectSummary> Summaries { get; set; } = new();
}
```

### Step 1.2: Entity — ProjectSummary

```csharp
using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Domain.Entities;

public class ProjectSummary : BaseEntity
{
    public int ImportBatchId { get; set; }
    public ImportBatch? Batch { get; set; }

    public required string SheetName { get; set; }
    public required string ProjectName { get; set; }
    public string? ContractorName { get; set; }

    public decimal ContractValue { get; set; }
    public decimal Advance { get; set; }
    public decimal PaymentKLHT { get; set; }
    public decimal RecoveredAdvance { get; set; }
    public decimal PaymentToContractor { get; set; }
    public decimal UnrecoveredAdvance { get; set; }
    public decimal DisbursedNSNN { get; set; }
    public decimal DisbursedDTNH { get; set; }
    public decimal TotalDisbursed { get; set; }

    public List<SettlementByYear> YearlySettlements { get; set; } = new();
}
```

### Step 1.3: Entity — SettlementByYear

```csharp
using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Domain.Entities;

public class SettlementByYear : BaseEntity
{
    public int ProjectSummaryId { get; set; }
    public ProjectSummary? ProjectSummary { get; set; }

    public int Year { get; set; }
    public decimal? KLHT { get; set; }
    public decimal? Advance { get; set; }
    public decimal? NSNN { get; set; }
    public decimal? DTNH { get; set; }
}
```

### Step 1.4: Repository abstraction

```csharp
using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Domain.Abstractions;

public interface IProjectSummaryRepository
{
    Task<ImportBatch?> GetBatchByIdAsync(int batchId, CancellationToken ct = default);
    Task<IReadOnlyList<ImportBatch>> ListBatchesAsync(CancellationToken ct = default);
    Task<ImportBatch?> GetLatestBatchAsync(CancellationToken ct = default);
    Task AddBatchAsync(ImportBatch batch, CancellationToken ct = default);
    Task DeleteBatchAsync(int batchId, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectSummary>> ListByBatchAsync(int batchId, CancellationToken ct = default);
}
```

### Step 1.5: EF Core configurations

Write `src/ConstructionFinance.Data/Configurations/ImportBatchConfiguration.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations;

public class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> b)
    {
        b.ToTable("import_batches");
        b.HasKey(x => x.Id);
        b.Property(x => x.SourceFileName).IsRequired().HasMaxLength(260);
        b.Property(x => x.SourceFileHash).HasMaxLength(64);
        b.Property(x => x.ImportedAtUtc).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasMany(x => x.Summaries).WithOne(s => s.Batch!).HasForeignKey(s => s.ImportBatchId).OnDelete(DeleteBehavior.Cascade);
    }
}
```

Write similar for `ProjectSummaryConfiguration` (table `project_summaries`, FK on `ImportBatchId`, `decimal(18,2)` for amounts, index on `(ImportBatchId, SheetName)`) and `SettlementByYearConfiguration` (table `settlement_by_years`, FK on `ProjectSummaryId`, index on `(ProjectSummaryId, Year)`).

### Step 1.6: Repository implementation

```csharp
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConstructionFinance.Data.Repositories;

public class ProjectSummaryRepository : IProjectSummaryRepository
{
    private readonly AppDbContext _ctx;

    public ProjectSummaryRepository(AppDbContext ctx) => _ctx = ctx;

    public async Task<ImportBatch?> GetBatchByIdAsync(int batchId, CancellationToken ct = default) =>
        await _ctx.ImportBatches
            .Include(b => b.Summaries).ThenInclude(s => s.YearlySettlements)
            .FirstOrDefaultAsync(b => b.Id == batchId, ct);

    public async Task<IReadOnlyList<ImportBatch>> ListBatchesAsync(CancellationToken ct = default) =>
        await _ctx.ImportBatches.OrderByDescending(b => b.ImportedAtUtc).ToListAsync(ct);

    public async Task<ImportBatch?> GetLatestBatchAsync(CancellationToken ct = default) =>
        await _ctx.ImportBatches
            .Include(b => b.Summaries).ThenInclude(s => s.YearlySettlements)
            .OrderByDescending(b => b.ImportedAtUtc)
            .FirstOrDefaultAsync(ct);

    public async Task AddBatchAsync(ImportBatch batch, CancellationToken ct = default) =>
        await _ctx.ImportBatches.AddAsync(batch, ct);

    public async Task DeleteBatchAsync(int batchId, CancellationToken ct = default)
    {
        var batch = await _ctx.ImportBatches.FindAsync(new object[] { batchId }, ct);
        if (batch is not null) _ctx.ImportBatches.Remove(batch);
    }

    public async Task<IReadOnlyList<ProjectSummary>> ListByBatchAsync(int batchId, CancellationToken ct = default) =>
        await _ctx.ProjectSummaries
            .Include(s => s.YearlySettlements)
            .Where(s => s.ImportBatchId == batchId)
            .OrderBy(s => s.SheetName)
            .ToListAsync(ct);
}
```

### Step 1.7: DbContext + DI

Edit `AppDbContext.cs`:
```csharp
public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
public DbSet<ProjectSummary> ProjectSummaries => Set<ProjectSummary>();
public DbSet<SettlementByYear> SettlementByYears => Set<SettlementByYear>();
```
Register configurations in `OnModelCreating`.

Edit `Data/DependencyInjection.cs`:
```csharp
services.AddScoped<IProjectSummaryRepository, ProjectSummaryRepository>();
```

### Step 1.8: Generate migration

```bash
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
cd /home/hieu-nguyen/management
dotnet ef migrations add AddProjectSummary \
  --project src/ConstructionFinance.Data \
  --startup-project src/ConstructionFinance.Desktop \
  --output-dir Migrations
```

### Step 1.9: Build + tests (no new test logic yet — just compile)

```bash
dotnet build --configuration Release
dotnet test --configuration Release --nologo --verbosity minimal
```

Expected: 0 errors, 232 tests still pass (no behavioral change yet).

### Step 1.10: Commit

```bash
git add . && git commit -m "feat(domain,data): ProjectSummary + ImportBatch + SettlementByYear entities + migration"
```

---

## Task 2: `IExcelImportService` + impl

This is the parser core. Reads a `.xlsx` via ClosedXML, scans sheets, extracts TỔNG CỘNG values + multi-year settlement cols.

**Files:**
- Modify: `src/ConstructionFinance.Application/ConstructionFinance.Application.csproj` (add ClosedXML PackageRef)
- New: `src/ConstructionFinance.Application/DTOs/ImportResultDto.cs`
- New: `src/ConstructionFinance.Application/DTOs/ProjectSummaryDto.cs` (record matching entity)
- New: `src/ConstructionFinance.Application/DTOs/SettlementByYearDto.cs`
- New: `src/ConstructionFinance.Application/Services/Interfaces/IExcelImportService.cs`
- New: `src/ConstructionFinance.Application/Services/Implementations/ExcelImportService.cs`
- Test: `tests/ConstructionFinance.Application.Tests/ExcelImportServiceTests.cs`

### Step 2.1: Add ClosedXML to Application csproj

```xml
<ItemGroup>
  <PackageReference Include="ClosedXML" />
</ItemGroup>
```

If `Directory.Packages.props` lacks ClosedXML, add `<PackageVersion Include="ClosedXML" Version="0.102.2" />`.

### Step 2.2: DTOs

```csharp
// ProjectSummaryDto.cs
public record ProjectSummaryDto(
    string SheetName, string ProjectName, string? ContractorName,
    decimal ContractValue, decimal Advance, decimal PaymentKLHT, decimal RecoveredAdvance,
    decimal PaymentToContractor, decimal UnrecoveredAdvance, decimal DisbursedNSNN,
    decimal DisbursedDTNH, decimal TotalDisbursed,
    IReadOnlyList<SettlementByYearDto> YearlySettlements);

public record SettlementByYearDto(int Year, decimal? KLHT, decimal? Advance, decimal? NSNN, decimal? DTNH);

// ImportResultDto.cs
public record ImportResultDto(
    string SourceFileName, string SourceFileHash, int SheetsFound,
    IReadOnlyList<ProjectSummaryDto> Summaries,
    IReadOnlyList<string> Warnings);
```

### Step 2.3: Interface

```csharp
namespace ConstructionFinance.Application.Services.Interfaces;

public interface IExcelImportService
{
    /// Parses a .xlsx file and returns parsed summaries + warnings. Does NOT persist.
    Task<ImportResultDto> ParseAsync(string filePath, CancellationToken ct = default);
}
```

### Step 2.4: ExcelImportService skeleton

Write `src/ConstructionFinance.Application/Services/Implementations/ExcelImportService.cs`:

```csharp
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;

namespace ConstructionFinance.Application.Services.Implementations;

public class ExcelImportService : IExcelImportService
{
    private static readonly Regex DaSheetPattern =
        new(@"^(da|dự\s*án|project)\s*\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex YearPattern =
        new(@"quyết\s*toán\s*(đến\s*)?năm\s*(\d{4})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public async Task<ImportResultDto> ParseAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Excel file not found", filePath);

        var hash = await ComputeSha256Async(filePath, ct);
        var fileName = Path.GetFileName(filePath);

        using var wb = new XLWorkbook(filePath);
        var warnings = new List<string>();
        var summaries = new List<ProjectSummaryDto>();
        var daSheets = wb.Worksheets.Where(w => DaSheetPattern.IsMatch(w.Name.Trim())).ToList();

        foreach (var sheet in daSheets)
        {
            try
            {
                var summary = ParseDaSheet(sheet, warnings);
                if (summary is not null) summaries.Add(summary);
            }
            catch (Exception ex)
            {
                warnings.Add($"Sheet '{sheet.Name}': {ex.Message}");
            }
        }

        return new ImportResultDto(
            SourceFileName: fileName,
            SourceFileHash: hash,
            SheetsFound: wb.Worksheets.Count,
            Summaries: summaries,
            Warnings: warnings);
    }

    private ProjectSummaryDto? ParseDaSheet(IXLWorksheet sheet, List<string> warnings)
    {
        // H.xlsx layout: header rows 4-5; TỔNG CỘNG row 6.
        var totalRowIdx = FindTotalRow(sheet);
        if (totalRowIdx is null)
        {
            warnings.Add($"Sheet '{sheet.Name}': không tìm thấy dòng TỔNG CỘNG.");
            return null;
        }
        var row = sheet.Row(totalRowIdx.Value);

        // Column mapping per H.xlsx DA sheet:
        // D = Giá trị HĐ; F = Tạm ứng; G = TT KLHT; H = Thu hồi tạm ứng;
        // I = TT từ DP NSNN (treated as PaymentToContractor in our schema);
        // J = Tạm ứng chưa thu hồi; K = Tổng giá trị đã giải ngân.
        // L..onward = year settlement columns (parsed dynamically).
        decimal Cell(string col) => GetDecimal(row.Cell(col), warnings, sheet.Name);

        var contractValue = Cell("D");
        var advance = Cell("F");
        var paymentKLHT = Cell("G");
        var recoveredAdvance = Cell("H");
        var paymentToContractor = Cell("I");
        var unrecoveredAdvance = Cell("J");
        var totalDisbursed = Cell("K");

        // Funding source split — H.xlsx DA sheet doesn't have direct NSNN/DTNH cols at TỔNG CỘNG.
        // For now, attribute everything to NSNN (refine via Tổng hợp sheet read if needed in Plan 08).
        var disbursedNSNN = totalDisbursed;
        var disbursedDTNH = 0m;

        // Detect year columns from header rows 4-5.
        var yearCols = DetectYearColumns(sheet, warnings);
        var yearlySettlements = new List<SettlementByYearDto>();
        foreach (var (year, cols) in yearCols)
        {
            yearlySettlements.Add(new SettlementByYearDto(
                Year: year,
                KLHT: cols.TryGetValue("klht", out var k) ? Cell(k) : (decimal?)null,
                Advance: cols.TryGetValue("ứng", out var a) ? Cell(a) : (decimal?)null,
                NSNN: cols.TryGetValue("nsnn", out var n) ? Cell(n) : (decimal?)null,
                DTNH: cols.TryGetValue("dtnh", out var d) ? Cell(d) : (decimal?)null));
        }

        // Project name: try cell A2 ("Tên dự án: ..."), else fallback to sheet name.
        var projectName = TryReadProjectName(sheet) ?? sheet.Name;
        var contractorName = TryReadContractorName(sheet);

        return new ProjectSummaryDto(
            SheetName: sheet.Name,
            ProjectName: projectName,
            ContractorName: contractorName,
            ContractValue: contractValue,
            Advance: advance,
            PaymentKLHT: paymentKLHT,
            RecoveredAdvance: recoveredAdvance,
            PaymentToContractor: paymentToContractor,
            UnrecoveredAdvance: unrecoveredAdvance,
            DisbursedNSNN: disbursedNSNN,
            DisbursedDTNH: disbursedDTNH,
            TotalDisbursed: totalDisbursed,
            YearlySettlements: yearlySettlements);
    }

    private static int? FindTotalRow(IXLWorksheet sheet)
    {
        // Scan rows 4-10 looking for cell A or B containing "TỔNG CỘNG" / "Tổng cộng".
        for (int r = 4; r <= 10; r++)
        {
            var a = sheet.Cell(r, 1).GetString().Trim();
            var b = sheet.Cell(r, 2).GetString().Trim();
            if (a.Equals("TỔNG CỘNG", StringComparison.OrdinalIgnoreCase) ||
                b.Equals("Tổng cộng", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("Tổng cộng", StringComparison.OrdinalIgnoreCase))
                return r;
        }
        return null;
    }

    /// Returns dictionary: year → {sub-col-name → column-letter}
    private Dictionary<int, Dictionary<string, string>> DetectYearColumns(IXLWorksheet sheet, List<string> warnings)
    {
        var result = new Dictionary<int, Dictionary<string, string>>();
        // Row 4 holds the year group headers; row 5 holds sub-col labels.
        var row4 = sheet.Row(4);
        var row5 = sheet.Row(5);
        var lastCol = Math.Min(sheet.LastColumnUsed()?.ColumnNumber() ?? 0, 30);

        for (int c = 1; c <= lastCol; c++)
        {
            var header4 = row4.Cell(c).GetString().Trim();
            var match = YearPattern.Match(header4);
            if (!match.Success) continue;
            var year = int.Parse(match.Groups[2].Value);

            // Find span: how many merged cells does this header cover?
            int spanStart = c, spanEnd = c;
            var merged = row4.Cell(c).MergedRange();
            if (merged is not null) spanEnd = merged.LastColumn().ColumnNumber();

            var subCols = new Dictionary<string, string>();
            for (int cc = spanStart; cc <= spanEnd; cc++)
            {
                var label = row5.Cell(cc).GetString().Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(label)) continue;
                string? key = label switch
                {
                    var s when s.Contains("klht") || s.Contains("khối lượng") => "klht",
                    var s when s.Contains("ứng") || s.Contains("tạm ứng") => "ứng",
                    var s when s.Contains("nsnn") || s.Contains("dự phòng") => "nsnn",
                    var s when s.Contains("dtnh") || s.Contains("ngoại hối") || s.Contains("qdtnh") => "dtnh",
                    _ => null
                };
                if (key is not null) subCols[key] = XLHelper.GetColumnLetterFromNumber(cc);
            }

            if (subCols.Count > 0) result[year] = subCols;
            else warnings.Add($"Sheet '{sheet.Name}': năm {year} không tìm thấy cột phụ.");

            c = spanEnd; // skip span
        }

        return result;
    }

    private static decimal GetDecimal(IXLCell cell, List<string> warnings, string sheetName)
    {
        if (cell.IsEmpty()) return 0m;
        if (cell.HasFormula && cell.Value.IsError)
        {
            warnings.Add($"Sheet '{sheetName}' ô {cell.Address}: công thức lỗi #REF! — giá trị tính 0.");
            return 0m;
        }
        try
        {
            return (decimal)cell.GetDouble();
        }
        catch
        {
            warnings.Add($"Sheet '{sheetName}' ô {cell.Address}: không đọc được số — giá trị tính 0.");
            return 0m;
        }
    }

    private static string? TryReadProjectName(IXLWorksheet sheet)
    {
        // H.xlsx puts the human project name typically in A2 or in a merged title row.
        // For MVP we keep null; the wizard lets user override.
        return null;
    }

    private static string? TryReadContractorName(IXLWorksheet sheet)
    {
        // H.xlsx DA sheet: column C in row 7+ holds "Đơn vị Thi công". Take first non-empty.
        for (int r = 7; r <= 30; r++)
        {
            var v = sheet.Cell(r, 3).GetString().Trim();
            if (!string.IsNullOrWhiteSpace(v) && !v.Equals("Đơn vị Thi công (Nhà thầu)", StringComparison.OrdinalIgnoreCase))
                return v;
        }
        return null;
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        await using var fs = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(fs, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
```

### Step 2.5: Failing tests

Write `tests/ConstructionFinance.Application.Tests/ExcelImportServiceTests.cs`:

```csharp
using ConstructionFinance.Application.Services.Implementations;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class ExcelImportServiceTests
{
    private const string FixturePath = "../../../../../reference/H.xlsx";

    [Fact]
    public async Task ParseAsync_finds_DA_sheets_from_H_xlsx()
    {
        var svc = new ExcelImportService();
        var result = await svc.ParseAsync(FixturePath);

        result.Summaries.Count.ShouldBeGreaterThan(0);
        result.Summaries.ShouldAllBe(s => s.SheetName.StartsWith("DA", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ParseAsync_collects_warnings_for_ref_errors()
    {
        var svc = new ExcelImportService();
        var result = await svc.ParseAsync(FixturePath);

        // H.xlsx DA1 has #REF! cells in row 6; expect at least one warning.
        result.Warnings.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ParseAsync_detects_year_columns_dynamically()
    {
        var svc = new ExcelImportService();
        var result = await svc.ParseAsync(FixturePath);

        var anyYears = result.Summaries.SelectMany(s => s.YearlySettlements).Select(y => y.Year).Distinct();
        anyYears.ShouldContain(y => y >= 2024 && y <= 2030);
    }

    [Fact]
    public async Task ParseAsync_throws_when_file_missing()
    {
        var svc = new ExcelImportService();
        await Should.ThrowAsync<FileNotFoundException>(() => svc.ParseAsync("/tmp/nonexistent.xlsx"));
    }
}
```

Adjust `FixturePath` relative path as needed; alternative is to copy `reference/H.xlsx` into the test project's `TestData/` folder + use `[assembly: AssemblyKeyFile]` or `<None CopyToOutputDirectory="PreserveNewest">` in the csproj.

### Step 2.6: Register DI

Edit `Application/DependencyInjection.cs`:
```csharp
services.AddScoped<IExcelImportService, ExcelImportService>();
```

### Step 2.7: Verify + commit

```bash
dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~ExcelImportServiceTests"
git add . && git commit -m "feat(app): ExcelImportService — parse H.xlsx DA sheets to ProjectSummary snapshots"
```

Expected: 4 new tests pass. Total: 236.

---

## Task 3: `IExcelExportService` + impl

Takes a template `.xlsx` (the user's file), overwrites the "Tổng hợp" sheet data rows with current snapshot values, saves to a target path. Leaves QĐ, DA*, CT1/CT2 sheets untouched (the user's authoritative data lives there).

**Files:**
- New: `src/ConstructionFinance.Application/Services/Interfaces/IExcelExportService.cs`
- New: `src/ConstructionFinance.Application/Services/Implementations/ExcelExportService.cs`
- Test: append to `ExcelImportServiceTests.cs` or new `ExcelExportServiceTests.cs`

### Step 3.1: Interface

```csharp
public interface IExcelExportService
{
    /// Opens the template file, overwrites the "Tổng hợp" sheet data rows with the given summaries,
    /// saves to <paramref name="targetPath"/>. Returns the target path.
    Task<string> ExportTongHopAsync(
        string templatePath,
        string targetPath,
        IReadOnlyList<ProjectSummaryDto> summaries,
        CancellationToken ct = default);
}
```

### Step 3.2: Implementation skeleton

```csharp
using ClosedXML.Excel;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;

namespace ConstructionFinance.Application.Services.Implementations;

public class ExcelExportService : IExcelExportService
{
    public async Task<string> ExportTongHopAsync(string templatePath, string targetPath,
        IReadOnlyList<ProjectSummaryDto> summaries, CancellationToken ct = default)
    {
        // 1. Copy template to target (preserves all sheets, formatting, formulas in other sheets).
        File.Copy(templatePath, targetPath, overwrite: true);

        // 2. Open target, find "Tổng hợp" sheet.
        using var wb = new XLWorkbook(targetPath);
        var sheet = wb.Worksheets.FirstOrDefault(w =>
            w.Name.Equals("Tổng hợp", StringComparison.OrdinalIgnoreCase) ||
            w.Name.Equals("TH", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Template không có sheet 'Tổng hợp' hoặc 'TH'.");

        // 3. Locate first data row (after header rows 7-8).
        int firstDataRow = 9;
        int rowIdx = firstDataRow;

        // 4. Clear existing data rows (up to TỔNG CỘNG row).
        var totalRow = sheet.RowsUsed()
            .Where(r => r.Cell(2).GetString().Trim().Equals("Tổng cộng", StringComparison.OrdinalIgnoreCase))
            .Select(r => r.RowNumber())
            .FirstOrDefault();
        if (totalRow > firstDataRow)
        {
            for (int r = firstDataRow; r < totalRow; r++)
            {
                for (int c = 1; c <= 17; c++) sheet.Cell(r, c).Clear(XLClearOptions.Contents);
            }
        }

        // 5. Write summaries.
        foreach (var s in summaries)
        {
            sheet.Cell(rowIdx, 1).Value = rowIdx - firstDataRow + 1; // STT
            sheet.Cell(rowIdx, 2).Value = s.ProjectName;
            sheet.Cell(rowIdx, 3).Value = s.ContractorName ?? "";
            sheet.Cell(rowIdx, 4).Value = s.ContractValue;
            sheet.Cell(rowIdx, 5).Value = s.Advance;
            sheet.Cell(rowIdx, 6).Value = s.PaymentKLHT;
            sheet.Cell(rowIdx, 7).Value = s.RecoveredAdvance;
            sheet.Cell(rowIdx, 8).Value = s.PaymentToContractor;
            sheet.Cell(rowIdx, 9).Value = s.UnrecoveredAdvance;
            sheet.Cell(rowIdx, 10).Value = s.DisbursedNSNN;
            sheet.Cell(rowIdx, 11).Value = s.DisbursedDTNH;
            sheet.Cell(rowIdx, 12).Value = s.TotalDisbursed;

            // Year settlement columns: write to detected positions per template. For MVP we assume
            // M-O = year1 (3 cols: KLHT, Advance, DTNH), P-Q = year2 (2 cols: NSNN, DTNH).
            var years = s.YearlySettlements.OrderBy(y => y.Year).ToList();
            if (years.Count >= 1)
            {
                var y1 = years[0];
                if (y1.KLHT.HasValue) sheet.Cell(rowIdx, 13).Value = y1.KLHT.Value;
                if (y1.Advance.HasValue) sheet.Cell(rowIdx, 14).Value = y1.Advance.Value;
                if (y1.DTNH.HasValue) sheet.Cell(rowIdx, 15).Value = y1.DTNH.Value;
            }
            if (years.Count >= 2)
            {
                var y2 = years[1];
                if (y2.NSNN.HasValue) sheet.Cell(rowIdx, 16).Value = y2.NSNN.Value;
                if (y2.DTNH.HasValue) sheet.Cell(rowIdx, 17).Value = y2.DTNH.Value;
            }
            rowIdx++;
        }

        await Task.Run(() => wb.Save(), ct);
        return targetPath;
    }
}
```

### Step 3.3: Register DI + test (basic round-trip)

```csharp
[Fact]
public async Task ExportTongHopAsync_creates_target_file_with_TH_sheet()
{
    var importer = new ExcelImportService();
    var exporter = new ExcelExportService();
    var parsed = await importer.ParseAsync(FixturePath);

    var target = Path.Combine(Path.GetTempPath(), $"export-{Guid.NewGuid():N}.xlsx");
    try
    {
        var result = await exporter.ExportTongHopAsync(FixturePath, target, parsed.Summaries);
        File.Exists(result).ShouldBeTrue();
        using var wb = new ClosedXML.Excel.XLWorkbook(result);
        wb.Worksheets.Any(w => w.Name.Equals("Tổng hợp", StringComparison.OrdinalIgnoreCase) ||
                                w.Name.Equals("TH", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
    }
    finally { if (File.Exists(target)) File.Delete(target); }
}
```

### Step 3.4: Commit

```bash
git add . && git commit -m "feat(app): ExcelExportService — write Tổng hợp values back to template"
```

Expected: +1 test. Total: 237.

---

## Task 4: `IProjectSummaryService` + impl

**Files:**
- New: `src/ConstructionFinance.Application/Services/Interfaces/IProjectSummaryService.cs`
- New: `src/ConstructionFinance.Application/Services/Implementations/ProjectSummaryService.cs`
- Test: `tests/ConstructionFinance.Application.Tests/ProjectSummaryServiceTests.cs`

### Step 4.1: Interface

```csharp
namespace ConstructionFinance.Application.Services.Interfaces;

public interface IProjectSummaryService
{
    Task<int> SaveBatchAsync(ImportResultDto parsed, CancellationToken ct = default);
    Task<IReadOnlyList<ImportBatchDto>> ListBatchesAsync(CancellationToken ct = default);
    Task<ImportBatchDto?> GetLatestBatchAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ProjectSummaryDto>> ListByBatchAsync(int batchId, CancellationToken ct = default);
    Task DeleteBatchAsync(int batchId, CancellationToken ct = default);
    Task<TongHopAggregateDto> AggregateAsync(int batchId, CancellationToken ct = default);
}
```

### Step 4.2: Implementation

Map between DTOs and entities; save via repository + IUnitOfWork. Aggregate sums each numeric field across all summaries in batch + groups year settlements by year.

### Step 4.3: Tests

3 tests minimum:
- `SaveBatchAsync_persists_batch_with_summaries_and_yearlySettlements`
- `GetLatestBatchAsync_returns_most_recent_by_ImportedAtUtc`
- `AggregateAsync_sums_columns_across_summaries`

### Step 4.4: DI + commit

```bash
git add . && git commit -m "feat(app): ProjectSummaryService — persist parsed Excel + aggregate Tổng hợp"
```

Expected: +3 tests. Total: 240.

---

## Task 5: `ImportWizardViewModel` + dialog view

Modal dialog. Steps:
1. User picks file via `IDialogService.OpenFilePickerAsync` (need to add this method if missing).
2. App calls `IExcelImportService.ParseAsync` → shows preview DataGrid + warnings panel.
3. User reviews; can cancel or confirm.
4. On confirm: `IProjectSummaryService.SaveBatchAsync` → close dialog with new BatchId.

**Files:**
- New: `src/ConstructionFinance.Desktop/ViewModels/TongHop/ImportWizardViewModel.cs`
- New: `src/ConstructionFinance.Desktop/Views/TongHop/ImportWizardDialog.axaml(.cs)`
- Test: `tests/ConstructionFinance.Desktop.Tests/TongHop/ImportWizardViewModelTests.cs`

### Step 5.1: Failing tests
3 tests:
- `BrowseCommand_opens_file_picker_and_parses`
- `ConfirmCommand_disabled_when_no_file_loaded`
- `ConfirmCommand_calls_SaveBatchAsync_and_returns_batch_id`

### Step 5.2: ViewModel + view skeleton (form fields for source path, preview list, warnings, Confirm/Cancel buttons).

### Step 5.3: Commit
```bash
git commit -m "feat(desktop): ImportWizard — pick file, preview parsed summaries, confirm save"
```

Expected: +3 tests.

---

## Task 6: `TongHopViewModel` + view

Displays the latest batch's summaries in a multi-column DataGrid with sub-header groups (Quyết toán năm X). Toolbar:
- Dropdown: select batch
- Filter textbox: filter projects by name/contractor
- Button: "📥 Import" (opens ImportWizardDialog) — also in MainWindow toolbar
- Button: "💾 Xuất Excel" → file picker for template + target → call ExcelExportService

**Files:**
- New: `src/ConstructionFinance.Desktop/ViewModels/TongHop/TongHopViewModel.cs`
- New: `src/ConstructionFinance.Desktop/Views/TongHop/TongHopView.axaml(.cs)`
- Test: `TongHopViewModelTests.cs`

### Step 6.1: Failing tests
3 tests:
- `LoadAsync_loads_latest_batch_summaries`
- `BatchChangedCommand_reloads_summaries_for_selected_batch`
- `ExportCommand_calls_ExportTongHopAsync_with_current_summaries`

### Step 6.2: View — multi-column DataGrid with header groups

Use `DataGridTemplateColumn` to emulate sub-header groups (DataGrid doesn't support nested headers natively in Avalonia). Layout sketch:

```xml
<DataGrid ItemsSource="{Binding Summaries}">
  <DataGrid.Columns>
    <DataGridTextColumn Header="STT" Binding="{Binding StT}"/>
    <DataGridTextColumn Header="Tên dự án" Binding="{Binding ProjectName}"/>
    <!-- ... other single cols ... -->

    <!-- Quyết toán 2025 / 2026 dynamic — bind from a flattened YearColumnHeaders collection -->
    <DataGridTemplateColumn Header="Quyết toán 2025 (KLHT)">
      <DataGridTemplateColumn.CellTemplate>
        <DataTemplate><TextBlock Text="{Binding Year2025KLHT, StringFormat=N0}"/></DataTemplate>
      </DataGridTemplateColumn.CellTemplate>
    </DataGridTemplateColumn>
    <!-- ... -->
  </DataGrid.Columns>
</DataGrid>
```

For dynamic year columns: pre-compute up to 3 settlement years in the VM (`Year2025*`, `Year2026*`, `Year2027*` properties on a flat row DTO).

### Step 6.3: Commit
```bash
git commit -m "feat(desktop): TongHop view — render batch summaries with year-aware columns"
```

Expected: +3 tests.

---

## Task 7: `ThongTriViewModel` + view

Picker for project (from latest batch) → renders a Thông tri-like card with the chosen project's snapshot fields formatted as a form/voucher.

**Files:**
- New: `src/ConstructionFinance.Desktop/ViewModels/ThongTri/ThongTriViewModel.cs`
- New: `src/ConstructionFinance.Desktop/Views/ThongTri/ThongTriView.axaml(.cs)`
- Test: `ThongTriViewModelTests.cs`

### Step 7.1: Test
2 tests:
- `LoadAsync_populates_project_list_from_latest_batch`
- `SelectProject_populates_detail_fields_from_snapshot`

### Step 7.2: View — form-card style

Show all 12 snapshot fields + each year's settlements; format amounts in Vietnamese number style. Add a "📄 In Thông tri" button (placeholder for v1.1 PDF export — current MVP just shows on screen).

### Step 7.3: Commit
```bash
git commit -m "feat(desktop): ThongTri view — per-project snapshot card"
```

Expected: +2 tests.

---

## Task 8: Menu restructure + toolbar Import button

This task changes the app shell. Touches MainWindow + MainWindowViewModel + HostBuilder.

**Files:**
- Modify: `src/ConstructionFinance.Desktop/Views/MainWindow.axaml`
- Modify: `src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/Common/HostBuilder.cs`

### Step 8.1: New menu structure in MainWindow.axaml

Replace the flat `NavigationView` items with sections (use `NavigationViewItemHeader` for group titles):

```xml
<ui:NavigationView ...>
  <ui:NavigationView.MenuItems>
    <ui:NavigationViewItemHeader Content="Tổng hợp"/>
    <ui:NavigationViewItem Content="Bảng tổng hợp" Tag="TongHop" IconSource="Library"/>
    <ui:NavigationViewItem Content="Thông tri" Tag="ThongTri" IconSource="Document"/>
    <ui:NavigationViewItem Content="Lịch sử Import" Tag="ImportHistory" IconSource="History"/>

    <ui:NavigationViewItemHeader Content="Dữ liệu nguồn"/>
    <ui:NavigationViewItem Content="Quyết định" Tag="Decisions" IconSource="Document"/>
    <ui:NavigationViewItem Content="Dự án" Tag="Projects" IconSource="GridView"/>
    <ui:NavigationViewItem Content="Nhà cung cấp" Tag="Vendors" IconSource="People"/>

    <ui:NavigationViewItemHeader Content="Hệ thống"/>
    <ui:NavigationViewItem Content="Cài đặt" Tag="Settings" IconSource="Settings"/>
  </ui:NavigationView.MenuItems>

  <ui:NavigationView.PaneCustomContent>
    <Button Content="📥 Import Excel" Command="{Binding OpenImportWizardCommand}"
            HorizontalAlignment="Stretch" Margin="8"/>
  </ui:NavigationView.PaneCustomContent>
</ui:NavigationView>
```

(Adjust FluentAvalonia control names as needed; PaneCustomContent or PaneFooter may differ.)

### Step 8.2: MainWindowViewModel — new cases

```csharp
case "TongHop":
    var tvm = _services.GetRequiredService<TongHopViewModel>();
    _nav.Reset();
    _nav.NavigateTo(tvm);
    await tvm.LoadAsync();
    break;
case "ThongTri":
    var ttvm = _services.GetRequiredService<ThongTriViewModel>();
    _nav.Reset();
    _nav.NavigateTo(ttvm);
    await ttvm.LoadAsync();
    break;
case "ImportHistory":
    // Reuse ImportBatch list view — same VM as TongHop with different mode? For MVP, defer to v1.2.
    _nav.NavigateTo(new PlaceholderViewModel { Tag = tag });
    break;
```

Add new `[RelayCommand] OpenImportWizardAsync` that opens the ImportWizardDialog and triggers a Tổng hợp reload on confirm.

### Step 8.3: HostBuilder — register VMs

```csharp
services.AddTransient<ViewModels.TongHop.TongHopViewModel>();
services.AddTransient<ViewModels.TongHop.ImportWizardViewModel>();
services.AddTransient<ViewModels.ThongTri.ThongTriViewModel>();
```

### Step 8.4: Default landing screen

In `MainWindowViewModel` constructor (or `InitializeAsync` on first navigation), trigger `NavigateToAsync("TongHop")` so app opens directly on Tổng hợp.

### Step 8.5: Build + commit

```bash
dotnet build --configuration Release
git add . && git commit -m "feat(desktop): menu restructure (3 groups) + Import Excel toolbar button"
```

---

## Task 9: Tests — end-to-end import H.xlsx round-trip

Tests confirm a full flow: parse `reference/H.xlsx` → save batch → re-query → values match.

**File:** `tests/ConstructionFinance.Application.Tests/ExcelImportRoundTripTests.cs`

2 tests:
- `RoundTrip_parses_H_xlsx_persists_and_reloads_with_matching_values`
- `RoundTrip_export_after_import_produces_valid_xlsx`

```bash
git add . && git commit -m "test(app): end-to-end round-trip H.xlsx → DB → re-query → export"
```

Expected: +2 tests. Total ~252.

---

## Task 10: Docs — Excel template format spec

**File:** `docs/excel-template-format.md`

User-facing markdown explaining:
- Required sheet names: "Tổng hợp" (or "TH"), "DA1", "DA2", ...
- DA sheet layout: rows 4-5 headers, row 6 TỔNG CỘNG, columns D/F/G/H/I/J/K, year columns L+ with "Quyết toán năm YYYY" headers
- Tổng hợp sheet layout: rows 7-8 headers, row 9+ data, row N TỔNG CỘNG
- Tolerated variations + known limitations (sub-columns must be in expected order)
- Common errors + how to fix (e.g., `#REF!` cells → app warns but doesn't crash; user should fix in Excel)

Also include a cell mapping table:

| App field | DA sheet cell | Tổng hợp sheet cell |
|---|---|---|
| ProjectName | (sheet name) | B (row data) |
| ContractValue | D6 | D |
| Advance | F6 | E |
| ... | ... | ... |

### Commit

```bash
git add docs/excel-template-format.md
git commit -m "docs: Excel template format spec for end users"
```

---

## Final verification

- [ ] **Step F.1: Clean build + full test suite**

```bash
dotnet clean
dotnet build --configuration Release
dotnet test --configuration Release --nologo --verbosity minimal
```

Expected: 0 errors. Approximately **252+ tests pass** (232 baseline + 4 ExcelImport + 1 ExcelExport + 3 ProjectSummaryService + 3 ImportWizard + 3 TongHop + 2 ThongTri + 2 RoundTrip = ~250).

- [ ] **Step F.2: Manual smoke test**

```bash
dotnet run --project src/ConstructionFinance.Desktop/
```

Checklist:
- [ ] App opens on "Bảng tổng hợp" screen (no longer "Trang chủ")
- [ ] Menu shows 3 sections (Tổng hợp / Dữ liệu nguồn / Hệ thống)
- [ ] "📥 Import Excel" button visible in nav pane
- [ ] Click Import → file picker → choose `reference/H.xlsx` → preview shows parsed projects + warnings → Confirm → Tổng hợp view populates
- [ ] Switch to Thông tri → pick project → see snapshot fields
- [ ] Click "Xuất Excel" → pick template + target → new file is generated with Tổng hợp sheet populated
- [ ] Open the exported file in LibreOffice/Excel — verify Tổng hợp sheet has the values; DA sheets are unchanged

- [ ] **Step F.3: Confirm commits**

```bash
git log --oneline | head -16
```

Expected: ~10 Plan 07 commits.

---

## Deliverables

After Plan 07:

1. ✅ `ProjectSummary` + `SettlementByYear` + `ImportBatch` entities with multi-year support
2. ✅ `ExcelImportService` — parses H.xlsx-style files, fail-soft on `#REF!`, dynamic year detection
3. ✅ `ExcelExportService` — preserves user's template, writes only "Tổng hợp" sheet
4. ✅ `ProjectSummaryService` — CRUD + aggregate
5. ✅ Import wizard dialog (file pick, preview, confirm)
6. ✅ Tổng hợp view (multi-column DataGrid with year groups)
7. ✅ Thông tri view (per-project snapshot card)
8. ✅ Menu restructured into 3 sections; "Import Excel" toolbar button
9. ✅ Default landing screen = Tổng hợp
10. ✅ Round-trip tests on `reference/H.xlsx`

**What's NOT in MVP** (v1.2+ roadmap):

- Payment-row-level import (drill-down per đợt thanh toán) → Plan 08
- Approval Decision (QĐ) sheet import → Plan 09
- CT1 / CT2 / T.Tri voucher form generation/printing → Plan 10
- PDF export of Thông tri → Plan 11
- Diff view between two import batches → Plan 12
- Multi-file batch import → Plan 13
- "Import History" view (full audit of batches) → Plan 14

**Next plan (v1.2):** Most likely Plan 08 (payment rows) if users want to drill down inside the app instead of jumping back to Excel.
