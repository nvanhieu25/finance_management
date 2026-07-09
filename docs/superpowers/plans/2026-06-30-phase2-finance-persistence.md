# Phase 2 — Finance persistence model (additive) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist the new project-centric flat model (Project + line items + decision links) alongside the legacy schema, and wire it to the Phase-1 `ProjectCalculator` so a saved project yields its computed aggregate.

**Architecture:** New entities live in a **separate namespace `ConstructionFinance.Domain.Finance`** so they do not clash with the legacy `ConstructionFinance.Domain.Entities.Project`. The migration is **additive** (new tables only; legacy tables untouched), so the app keeps building and running on the old UI throughout. Tests use in-memory SQLite (`EnsureCreated`) so the real `app.db` is never touched. Legacy entities/UI are removed in a final cleanup phase, not here.

**Tech Stack:** .NET 8, EF Core 8 + SQLite, xUnit + Shouldly.

## Global Constraints
- Free-license; offline desktop; single user.
- Naming: camelCase locals/params, `_camelCase` private fields, PascalCase types/members. Money `decimal`, `HasPrecision(18,2)`.
- **Additive only** — do NOT modify or delete any legacy entity, config, DbSet, repository, view, or viewmodel in this phase.
- New entities namespace: `ConstructionFinance.Domain.Finance`. New tables prefixed `finance_`.
- Source of truth: spec `docs/superpowers/specs/2026-06-26-excel-like-project-finance-redesign-design.md` §3.
- Consumes Phase-1 calc types from `ConstructionFinance.Application.Calc`: `LineItemInput`, `ProjectAggregate`, `ProjectCalculator.Aggregate`.

---

## File Structure

- Create: `src/ConstructionFinance.Domain/Finance/Project.cs`, `ProjectLineItem.cs`, `ProjectDecision.cs`
- Create: `src/ConstructionFinance.Application/Calc/LineItemMapping.cs` (entity → calc DTO)
- Create: `src/ConstructionFinance.Data/Configurations/Finance/FinanceProjectConfiguration.cs`, `FinanceProjectLineItemConfiguration.cs`, `FinanceProjectDecisionConfiguration.cs`
- Create: `src/ConstructionFinance.Data/DesignTimeDbContextFactory.cs` (so `dotnet ef` can build the context)
- Modify: `src/ConstructionFinance.Data/AppDbContext.cs` (add 3 DbSets via alias)
- Modify: `src/ConstructionFinance.Desktop/Common/AppPaths.cs` (env-var DB override)
- Create: `src/ConstructionFinance.Domain/Abstractions/IFinanceProjectRepository.cs` + `src/ConstructionFinance.Data/Repositories/FinanceProjectRepository.cs`
- Create: `src/ConstructionFinance.Application/Services/Interfaces/IFinanceProjectService.cs` + `src/ConstructionFinance.Application/Services/Implementations/FinanceProjectService.cs`
- Migration: generated under `src/ConstructionFinance.Data/Migrations/`
- Tests: `tests/ConstructionFinance.Application.Tests/Calc/LineItemMappingTests.cs`, `tests/ConstructionFinance.Data.Tests/Finance/FinanceProjectRepositoryTests.cs`, `tests/ConstructionFinance.Application.Tests/Finance/FinanceProjectServiceTests.cs`, `tests/ConstructionFinance.Desktop.Tests/Common/AppPathsTests.cs`

---

### Task 1: Dev-DB env override in AppPaths

**Files:**
- Modify: `src/ConstructionFinance.Desktop/Common/AppPaths.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Common/AppPathsTests.cs`

**Interfaces:**
- Produces: `AppPaths.DatabaseFile` honours env var `CONSTRUCTIONFINANCE_DB` when set (absolute path to a db file); otherwise the default `LocalAppDataRoot/app.db`.

- [ ] **Step 1: Write the failing test**

Create `tests/ConstructionFinance.Desktop.Tests/Common/AppPathsTests.cs`:
```csharp
using ConstructionFinance.Desktop.Common;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Common;

public class AppPathsTests
{
    [Fact]
    public void DatabaseFile_uses_env_override_when_set()
    {
        var prev = Environment.GetEnvironmentVariable("CONSTRUCTIONFINANCE_DB");
        try
        {
            Environment.SetEnvironmentVariable("CONSTRUCTIONFINANCE_DB", "/tmp/app-dev.db");
            AppPaths.DatabaseFile.ShouldBe("/tmp/app-dev.db");
            AppPaths.ConnectionString.ShouldContain("/tmp/app-dev.db");
        }
        finally { Environment.SetEnvironmentVariable("CONSTRUCTIONFINANCE_DB", prev); }
    }

    [Fact]
    public void DatabaseFile_defaults_under_localappdata_when_env_absent()
    {
        var prev = Environment.GetEnvironmentVariable("CONSTRUCTIONFINANCE_DB");
        try
        {
            Environment.SetEnvironmentVariable("CONSTRUCTIONFINANCE_DB", null);
            AppPaths.DatabaseFile.ShouldEndWith("app.db");
        }
        finally { Environment.SetEnvironmentVariable("CONSTRUCTIONFINANCE_DB", prev); }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/ConstructionFinance.Desktop.Tests.csproj --filter "FullyQualifiedName~AppPathsTests"`
Expected: FAIL — `DatabaseFile` ignores the env var (returns the LocalAppData path).

- [ ] **Step 3: Implement the override**

In `src/ConstructionFinance.Desktop/Common/AppPaths.cs`, replace the `DatabaseFile` property:
```csharp
    public static string DatabaseFile =>
        Environment.GetEnvironmentVariable("CONSTRUCTIONFINANCE_DB") is { Length: > 0 } overridePath
            ? overridePath
            : Path.Combine(LocalAppDataRoot, "app.db");
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests/ConstructionFinance.Desktop.Tests.csproj --filter "FullyQualifiedName~AppPathsTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ConstructionFinance.Desktop/Common/AppPaths.cs tests/ConstructionFinance.Desktop.Tests/Common/AppPathsTests.cs
git commit -m "feat(paths): CONSTRUCTIONFINANCE_DB env override for a dev database"
```

---

### Task 2: Finance entities + line-item → calc mapper

**Files:**
- Create: `src/ConstructionFinance.Domain/Finance/Project.cs`, `ProjectLineItem.cs`, `ProjectDecision.cs`
- Create: `src/ConstructionFinance.Application/Calc/LineItemMapping.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Calc/LineItemMappingTests.cs`

**Interfaces:**
- Produces:
  - `ConstructionFinance.Domain.Finance.Project { int Id; string? Code; string Name; string? ContractorName; decimal ContractValue; string? Notes; List<ProjectLineItem> Lines; List<ProjectDecision> Decisions }` (extends `BaseEntity`)
  - `ConstructionFinance.Domain.Finance.ProjectLineItem { int Id; int ProjectId; int SortOrder; DateOnly Date; string? PackageName; string? ContractRef; decimal Advance; decimal PaymentKLHT; decimal RecoveredAdvance; decimal DisbursedNSNN; decimal DisbursedNgoaiHoi }` (extends `BaseEntity`)
  - `ConstructionFinance.Domain.Finance.ProjectDecision { int ProjectId; int ApprovalDecisionId }`
  - `static LineItemInput LineItemMapping.ToCalcInput(this ProjectLineItem x)`

- [ ] **Step 1: Write the failing test**

Create `tests/ConstructionFinance.Application.Tests/Calc/LineItemMappingTests.cs`:
```csharp
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Calc;

public class LineItemMappingTests
{
    [Fact]
    public void ToCalcInput_copies_all_raw_fields()
    {
        var line = new ProjectLineItem
        {
            Date = new DateOnly(2026, 2, 20),
            PackageName = "TT KLHT",
            Advance = 0m,
            PaymentKLHT = 800m,
            RecoveredAdvance = 200m,
            DisbursedNSNN = 420m,
            DisbursedNgoaiHoi = 180m,
        };

        var input = line.ToCalcInput();

        input.Date.ShouldBe(new DateOnly(2026, 2, 20));
        input.PackageName.ShouldBe("TT KLHT");
        input.PaymentKLHT.ShouldBe(800m);
        input.RecoveredAdvance.ShouldBe(200m);
        input.DisbursedNSNN.ShouldBe(420m);
        input.DisbursedNgoaiHoi.ShouldBe(180m);
        // and the calc layer agrees on the disbursed total
        ProjectCalculator.ComputeLine(input).Disbursed.ShouldBe(600m);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ConstructionFinance.Application.Tests.csproj --filter "FullyQualifiedName~LineItemMappingTests"`
Expected: FAIL to compile — `Finance.ProjectLineItem`, `LineItemMapping` do not exist.

- [ ] **Step 3: Create entities + mapper**

Create `src/ConstructionFinance.Domain/Finance/Project.cs`:
```csharp
using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Domain.Finance;

/// Project-centric flat model (new). Lives in its own namespace during the
/// strangler transition so it does not clash with Entities.Project (legacy).
public class Project : BaseEntity
{
    public string? Code { get; set; }
    public required string Name { get; set; }
    public string? ContractorName { get; set; }
    public decimal ContractValue { get; set; }
    public string? Notes { get; set; }

    public List<ProjectLineItem> Lines { get; set; } = new();
    public List<ProjectDecision> Decisions { get; set; } = new();
}
```

Create `src/ConstructionFinance.Domain/Finance/ProjectLineItem.cs`:
```csharp
using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Domain.Finance;

/// One đợt (line item). Stores only raw inputs; derived figures are computed
/// live by ProjectCalculator.
public class ProjectLineItem : BaseEntity
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public int SortOrder { get; set; }
    public DateOnly Date { get; set; }
    public string? PackageName { get; set; }
    public string? ContractRef { get; set; }

    public decimal Advance { get; set; }
    public decimal PaymentKLHT { get; set; }
    public decimal RecoveredAdvance { get; set; }
    public decimal DisbursedNSNN { get; set; }
    public decimal DisbursedNgoaiHoi { get; set; }
}
```

Create `src/ConstructionFinance.Domain/Finance/ProjectDecision.cs`:
```csharp
using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Domain.Finance;

/// Many-to-many link between a finance Project and a legacy ApprovalDecision.
public class ProjectDecision
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public int ApprovalDecisionId { get; set; }
    public ApprovalDecision Decision { get; set; } = null!;
}
```

Create `src/ConstructionFinance.Application/Calc/LineItemMapping.cs`:
```csharp
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Calc;

public static class LineItemMapping
{
    public static LineItemInput ToCalcInput(this ProjectLineItem x) =>
        new(x.Date, x.PackageName, x.Advance, x.PaymentKLHT, x.RecoveredAdvance, x.DisbursedNSNN, x.DisbursedNgoaiHoi);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ConstructionFinance.Application.Tests.csproj --filter "FullyQualifiedName~LineItemMappingTests"`
Expected: PASS (1 test).

- [ ] **Step 5: Commit**

```bash
git add src/ConstructionFinance.Domain/Finance/ src/ConstructionFinance.Application/Calc/LineItemMapping.cs tests/ConstructionFinance.Application.Tests/Calc/LineItemMappingTests.cs
git commit -m "feat(finance): new project-centric entities + line-item calc mapper"
```

---

### Task 3: EF configurations, DbSets, design-time factory, migration

**Files:**
- Create: `src/ConstructionFinance.Data/Configurations/Finance/FinanceProjectConfiguration.cs`, `FinanceProjectLineItemConfiguration.cs`, `FinanceProjectDecisionConfiguration.cs`
- Create: `src/ConstructionFinance.Data/DesignTimeDbContextFactory.cs`
- Modify: `src/ConstructionFinance.Data/AppDbContext.cs`
- Test: `tests/ConstructionFinance.Data.Tests/Finance/FinanceProjectRepositoryTests.cs` (round-trip part only this task; repo added in Task 4 — so put the round-trip test here using the context directly)

**Interfaces:**
- Consumes: Task-2 entities.
- Produces: 3 EF configs auto-applied via `ApplyConfigurationsFromAssembly`; `AppDbContext.FinanceProjects`, `.FinanceProjectLines`, `.FinanceProjectDecisions` DbSets; a migration that creates tables `finance_projects`, `finance_project_lines`, `finance_project_decisions` and touches no legacy table.

- [ ] **Step 1: Write the failing test (round-trip via context)**

Create `tests/ConstructionFinance.Data.Tests/Finance/FinanceProjectRepositoryTests.cs`:
```csharp
using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Finance;

public class FinanceProjectRepositoryTests
{
    [Fact]
    public void Context_round_trips_project_with_lines()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();

        var p = new Project { Name = "Cầu A12", ContractValue = 1_000m };
        p.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 1, 5), Advance = 500m, DisbursedNSNN = 500m });
        ctx.FinanceProjects.Add(p);
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();

        var loaded = ctx.FinanceProjects.Include(x => x.Lines).Single();
        loaded.Name.ShouldBe("Cầu A12");
        loaded.ContractValue.ShouldBe(1_000m);
        loaded.Lines.Count.ShouldBe(1);
        loaded.Lines[0].Advance.ShouldBe(500m);
        loaded.Lines[0].DisbursedNSNN.ShouldBe(500m);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Data.Tests/ConstructionFinance.Data.Tests.csproj --filter "FullyQualifiedName~FinanceProjectRepositoryTests"`
Expected: FAIL to compile — `AppDbContext.FinanceProjects` does not exist.

- [ ] **Step 3: Add configs, DbSets, design-time factory**

Create `src/ConstructionFinance.Data/Configurations/Finance/FinanceProjectConfiguration.cs`:
```csharp
using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations.Finance;

public class FinanceProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.ToTable("finance_projects");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50);
        b.Property(x => x.Name).IsRequired().HasMaxLength(255);
        b.Property(x => x.ContractorName).HasMaxLength(255);
        b.Property(x => x.ContractValue).HasPrecision(18, 2);

        b.HasMany(x => x.Lines)
            .WithOne(l => l.Project)
            .HasForeignKey(l => l.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Decisions)
            .WithOne(d => d.Project)
            .HasForeignKey(d => d.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

Create `src/ConstructionFinance.Data/Configurations/Finance/FinanceProjectLineItemConfiguration.cs`:
```csharp
using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations.Finance;

public class FinanceProjectLineItemConfiguration : IEntityTypeConfiguration<ProjectLineItem>
{
    public void Configure(EntityTypeBuilder<ProjectLineItem> b)
    {
        b.ToTable("finance_project_lines");
        b.HasKey(x => x.Id);
        b.Property(x => x.PackageName).HasMaxLength(255);
        b.Property(x => x.ContractRef).HasMaxLength(255);
        b.Property(x => x.Advance).HasPrecision(18, 2);
        b.Property(x => x.PaymentKLHT).HasPrecision(18, 2);
        b.Property(x => x.RecoveredAdvance).HasPrecision(18, 2);
        b.Property(x => x.DisbursedNSNN).HasPrecision(18, 2);
        b.Property(x => x.DisbursedNgoaiHoi).HasPrecision(18, 2);
        b.HasIndex(x => new { x.ProjectId, x.SortOrder });
    }
}
```

Create `src/ConstructionFinance.Data/Configurations/Finance/FinanceProjectDecisionConfiguration.cs`:
```csharp
using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations.Finance;

public class FinanceProjectDecisionConfiguration : IEntityTypeConfiguration<ProjectDecision>
{
    public void Configure(EntityTypeBuilder<ProjectDecision> b)
    {
        b.ToTable("finance_project_decisions");
        b.HasKey(x => new { x.ProjectId, x.ApprovalDecisionId });
        b.HasOne(x => x.Decision)
            .WithMany()
            .HasForeignKey(x => x.ApprovalDecisionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

Ensure the EF design package is referenced in the Data project (needed for `IDesignTimeDbContextFactory` and `dotnet ef`):
```bash
dotnet add src/ConstructionFinance.Data package Microsoft.EntityFrameworkCore.Design
```
(If it is already referenced, this is a no-op.)

Create `src/ConstructionFinance.Data/DesignTimeDbContextFactory.cs` (lets `dotnet ef` build the context without the Avalonia host):
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ConstructionFinance.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=design-time.db")
            .Options;
        return new AppDbContext(options);
    }
}
```

In `src/ConstructionFinance.Data/AppDbContext.cs`, add an alias `using` at the top and the 3 DbSets. Add after the existing `using` lines:
```csharp
using FinanceProject = ConstructionFinance.Domain.Finance.Project;
using FinanceProjectLineItem = ConstructionFinance.Domain.Finance.ProjectLineItem;
using FinanceProjectDecision = ConstructionFinance.Domain.Finance.ProjectDecision;
```
Add these DbSet properties alongside the existing ones (after `SettlementByYears`):
```csharp
    public DbSet<FinanceProject> FinanceProjects => Set<FinanceProject>();
    public DbSet<FinanceProjectLineItem> FinanceProjectLines => Set<FinanceProjectLineItem>();
    public DbSet<FinanceProjectDecision> FinanceProjectDecisions => Set<FinanceProjectDecision>();
```

- [ ] **Step 4: Run the round-trip test (in-memory EnsureCreated builds the schema)**

Run: `dotnet test tests/ConstructionFinance.Data.Tests/ConstructionFinance.Data.Tests.csproj --filter "FullyQualifiedName~FinanceProjectRepositoryTests"`
Expected: PASS (1 test).

- [ ] **Step 5: Generate the additive migration**

Run from repo root:
```bash
dotnet ef migrations add AddFinanceModel \
  --project src/ConstructionFinance.Data \
  --startup-project src/ConstructionFinance.Data
```
Expected: a new migration under `src/ConstructionFinance.Data/Migrations/`. **Open the generated `*_AddFinanceModel.cs` and confirm `Up()` only calls `CreateTable` for `finance_projects`, `finance_project_lines`, `finance_project_decisions` — it must NOT `DropTable`/`AlterColumn` any legacy table.** If the diff touches a legacy table, STOP and report (model drift), do not commit.

**Fallback (environment):** if `dotnet ef` is not installed or cannot run in this environment, do NOT block — the in-memory tests (Step 4) already validate the model via `EnsureCreated`. Report status `DONE_WITH_CONCERNS` noting "migration deferred to Phase 3 (dotnet ef unavailable)"; the migration will be generated when the app is first run on the new schema. Still commit the configs/DbSets/factory.

- [ ] **Step 6: Build + run the full suite**

Run: `dotnet build ConstructionFinance.sln && dotnet test ConstructionFinance.sln`
Expected: build succeeds; all tests green.

- [ ] **Step 7: Commit**

```bash
git add src/ConstructionFinance.Data/ tests/ConstructionFinance.Data.Tests/Finance/
git commit -m "feat(finance): EF config + DbSets + additive migration for finance model"
```

---

### Task 4: FinanceProjectRepository (CRUD + get-with-lines)

**Files:**
- Create: `src/ConstructionFinance.Domain/Abstractions/IFinanceProjectRepository.cs`
- Create: `src/ConstructionFinance.Data/Repositories/FinanceProjectRepository.cs`
- Modify: `src/ConstructionFinance.Data/DependencyInjection.cs` (register the repo)
- Test: `tests/ConstructionFinance.Data.Tests/Finance/FinanceProjectRepositoryTests.cs` (append)

**Interfaces:**
- Consumes: Task-2 entities, `AppDbContext` DbSets (Task 3).
- Produces `IFinanceProjectRepository`:
  - `Task<int> AddAsync(Project project, CancellationToken ct = default)`
  - `Task<Project?> GetWithLinesAsync(int id, CancellationToken ct = default)`
  - `Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default)`
  - `Task DeleteAsync(int id, CancellationToken ct = default)`

- [ ] **Step 1: Write the failing test**

Append to `tests/ConstructionFinance.Data.Tests/Finance/FinanceProjectRepositoryTests.cs`:
```csharp
    [Fact]
    public async Task Repository_adds_lists_and_loads_with_lines()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new ConstructionFinance.Data.Repositories.FinanceProjectRepository(ctx);

        var p = new Project { Name = "DA1", ContractValue = 1_000m };
        p.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 1, 5), PaymentKLHT = 800m, RecoveredAdvance = 200m, DisbursedNSNN = 600m });
        var id = await repo.AddAsync(p);
        ctx.ChangeTracker.Clear();

        (await repo.ListAsync()).Count.ShouldBe(1);
        var loaded = await repo.GetWithLinesAsync(id);
        loaded.ShouldNotBeNull();
        loaded!.Lines.Count.ShouldBe(1);

        await repo.DeleteAsync(id);
        ctx.ChangeTracker.Clear();
        (await repo.ListAsync()).ShouldBeEmpty();
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Data.Tests/ConstructionFinance.Data.Tests.csproj --filter "FullyQualifiedName~FinanceProjectRepositoryTests"`
Expected: FAIL to compile — `FinanceProjectRepository` does not exist.

- [ ] **Step 3: Implement repo + interface + DI**

Create `src/ConstructionFinance.Domain/Abstractions/IFinanceProjectRepository.cs`:
```csharp
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Domain.Abstractions;

public interface IFinanceProjectRepository
{
    Task<int> AddAsync(Project project, CancellationToken ct = default);
    Task<Project?> GetWithLinesAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
```

Create `src/ConstructionFinance.Data/Repositories/FinanceProjectRepository.cs`:
```csharp
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ConstructionFinance.Data.Repositories;

public class FinanceProjectRepository : IFinanceProjectRepository
{
    private readonly AppDbContext _context;

    public FinanceProjectRepository(AppDbContext context) => _context = context;

    public async Task<int> AddAsync(Project project, CancellationToken ct = default)
    {
        await _context.FinanceProjects.AddAsync(project, ct);
        await _context.SaveChangesAsync(ct);
        return project.Id;
    }

    public async Task<Project?> GetWithLinesAsync(int id, CancellationToken ct = default) =>
        await _context.FinanceProjects
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default) =>
        await _context.FinanceProjects.OrderBy(p => p.Name).ToListAsync(ct);

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var p = await _context.FinanceProjects.FindAsync(new object[] { id }, ct);
        if (p is not null)
        {
            _context.FinanceProjects.Remove(p);
            await _context.SaveChangesAsync(ct);
        }
    }
}
```

In `src/ConstructionFinance.Data/DependencyInjection.cs`, inside `AddDataServices`, add (after the existing `AddScoped<IProjectSummaryRepository, ...>` line):
```csharp
        services.AddScoped<IFinanceProjectRepository, FinanceProjectRepository>();
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Data.Tests/ConstructionFinance.Data.Tests.csproj --filter "FullyQualifiedName~FinanceProjectRepositoryTests"`
Expected: PASS (2 tests in the file).

- [ ] **Step 5: Commit**

```bash
git add src/ConstructionFinance.Domain/Abstractions/IFinanceProjectRepository.cs src/ConstructionFinance.Data/Repositories/FinanceProjectRepository.cs src/ConstructionFinance.Data/DependencyInjection.cs tests/ConstructionFinance.Data.Tests/Finance/FinanceProjectRepositoryTests.cs
git commit -m "feat(finance): FinanceProjectRepository CRUD + DI registration"
```

---

### Task 5: FinanceProjectService (CRUD + computed aggregate)

**Files:**
- Create: `src/ConstructionFinance.Application/Services/Interfaces/IFinanceProjectService.cs`
- Create: `src/ConstructionFinance.Application/Services/Implementations/FinanceProjectService.cs`
- Modify: `src/ConstructionFinance.Application/DependencyInjection.cs` (register the service)
- Test: `tests/ConstructionFinance.Application.Tests/Finance/FinanceProjectServiceTests.cs`

**Interfaces:**
- Consumes: `IFinanceProjectRepository` (Task 4), `ProjectCalculator.Aggregate` + `LineItemMapping.ToCalcInput` (Phase 1 / Task 2).
- Produces `IFinanceProjectService`:
  - `Task<int> CreateAsync(Project project, CancellationToken ct = default)`
  - `Task<ProjectAggregate?> GetAggregateAsync(int projectId, CancellationToken ct = default)` — returns null if the project does not exist.

- [ ] **Step 1: Write the failing test**

Create `tests/ConstructionFinance.Application.Tests/Finance/FinanceProjectServiceTests.cs`:
```csharp
using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Finance;

public class FinanceProjectServiceTests
{
    [Fact]
    public async Task GetAggregateAsync_computes_from_saved_lines()
    {
        using var h = new ServiceTestHarness();
        var repo = new FinanceProjectRepository(h.Context);
        var svc = new FinanceProjectService(repo);

        var p = new Project { Name = "DA1", ContractValue = 1_000m };
        p.Lines.Add(new ProjectLineItem { SortOrder = 1, Date = new DateOnly(2026, 1, 5), Advance = 500m, DisbursedNSNN = 500m });
        p.Lines.Add(new ProjectLineItem { SortOrder = 2, Date = new DateOnly(2026, 2, 20), PaymentKLHT = 800m, RecoveredAdvance = 200m, DisbursedNSNN = 420m, DisbursedNgoaiHoi = 180m });
        var id = await svc.CreateAsync(p);
        h.Context.ChangeTracker.Clear();

        var agg = await svc.GetAggregateAsync(id);

        agg.ShouldNotBeNull();
        agg!.UnrecoveredAdvance.ShouldBe(300m);  // 500 − 200
        agg.TotalDisbursed.ShouldBe(1_100m);     // 500 + 600
        agg.SumNSNN.ShouldBe(920m);
        agg.SumNgoaiHoi.ShouldBe(180m);
    }

    [Fact]
    public async Task GetAggregateAsync_returns_null_for_missing_project()
    {
        using var h = new ServiceTestHarness();
        var svc = new FinanceProjectService(new FinanceProjectRepository(h.Context));
        (await svc.GetAggregateAsync(999)).ShouldBeNull();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ConstructionFinance.Application.Tests.csproj --filter "FullyQualifiedName~FinanceProjectServiceTests"`
Expected: FAIL to compile — `FinanceProjectService` does not exist.

- [ ] **Step 3: Implement service + interface + DI**

Create `src/ConstructionFinance.Application/Services/Interfaces/IFinanceProjectService.cs`:
```csharp
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IFinanceProjectService
{
    Task<int> CreateAsync(Project project, CancellationToken ct = default);
    Task<ProjectAggregate?> GetAggregateAsync(int projectId, CancellationToken ct = default);
}
```

Create `src/ConstructionFinance.Application/Services/Implementations/FinanceProjectService.cs`:
```csharp
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Services.Implementations;

public class FinanceProjectService : IFinanceProjectService
{
    private readonly IFinanceProjectRepository _repo;

    public FinanceProjectService(IFinanceProjectRepository repo) => _repo = repo;

    public Task<int> CreateAsync(Project project, CancellationToken ct = default) =>
        _repo.AddAsync(project, ct);

    public async Task<ProjectAggregate?> GetAggregateAsync(int projectId, CancellationToken ct = default)
    {
        var project = await _repo.GetWithLinesAsync(projectId, ct);
        if (project is null) return null;

        var inputs = project.Lines
            .OrderBy(l => l.SortOrder)
            .Select(l => l.ToCalcInput())
            .ToList();
        return ProjectCalculator.Aggregate(inputs);
    }
}
```

In `src/ConstructionFinance.Application/DependencyInjection.cs`, inside `AddApplicationServices`, add (next to the other `AddScoped` service registrations):
```csharp
        services.AddScoped<Services.Interfaces.IFinanceProjectService, Services.Implementations.FinanceProjectService>();
```
(If the file uses fully-qualified or `using`-shortened names, match the existing style; the key is registering `IFinanceProjectService` → `FinanceProjectService` as scoped.)

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ConstructionFinance.Application.Tests.csproj --filter "FullyQualifiedName~FinanceProjectServiceTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Run the full suite + commit**

Run: `dotnet test ConstructionFinance.sln`
Expected: all green.
```bash
git add src/ConstructionFinance.Application/Services/Interfaces/IFinanceProjectService.cs src/ConstructionFinance.Application/Services/Implementations/FinanceProjectService.cs src/ConstructionFinance.Application/DependencyInjection.cs tests/ConstructionFinance.Application.Tests/Finance/FinanceProjectServiceTests.cs
git commit -m "feat(finance): FinanceProjectService — CRUD + computed aggregate"
```

---

## Self-Review

**Spec coverage (§3, §7):** New entities Project/ProjectLineItem/ProjectDecision (Task 2) + Vendor/Decision reused (ProjectDecision links ApprovalDecision). Persistence + migration additive (Task 3). Repository CRUD (Task 4). Service wiring calc → aggregate (Task 5). Dev-DB safety (Task 1). Legacy removal is explicitly deferred to the final cleanup phase — NOT this plan (Global Constraints: additive only).

**Placeholder scan:** none — every step has full code/commands. Task 5 Step 3 DI line says "match existing style" because the exact `using` aliases in `DependencyInjection.cs` are not pinned here; the requirement (register `IFinanceProjectService`→`FinanceProjectService` scoped) is explicit.

**Type consistency:** `Finance.Project`, `Finance.ProjectLineItem`, `Finance.ProjectDecision`; `LineItemMapping.ToCalcInput`; `IFinanceProjectRepository.{AddAsync,GetWithLinesAsync,ListAsync,DeleteAsync}`; `IFinanceProjectService.{CreateAsync,GetAggregateAsync}`; DbSets `FinanceProjects/FinanceProjectLines/FinanceProjectDecisions`. Names are used identically across tasks. Consumes Phase-1 `LineItemInput`, `ProjectAggregate`, `ProjectCalculator.Aggregate` unchanged.

**Additive guarantee:** no task modifies a legacy entity/config/DbSet/repo/UI. Task 3 Step 5 explicitly verifies the migration touches no legacy table.
