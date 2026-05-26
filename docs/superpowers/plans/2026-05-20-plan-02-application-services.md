# Plan 02: Application Services Layer

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Xây dựng tầng Application Services — business logic cho mọi entity (Vendor, Decision, Contract, Project, Payment, CostItem tree, AuditLog). Mỗi service có CRUD + business rules + validation, được unit/integration test đầy đủ với SQLite in-memory. Kết thúc plan: tầng nghiệp vụ hoàn chỉnh sẵn sàng cho UI ở Plan 03, KHÔNG có UI.

**Architecture:** Services trong `ConstructionFinance.Application` phụ thuộc abstractions (`IRepository<T>`, `ICostRepository`, `IUnitOfWork`) trong `ConstructionFinance.Domain.Abstractions` — KHÔNG phụ thuộc trực tiếp `Data`. DTO + hand-written mapping extensions tách Entity khỏi consumer. Lỗi nghiệp vụ ném qua exceptions (`NotFoundException`, `BusinessRuleException`, FluentValidation `ValidationException`). Transaction boundary do service kiểm soát qua `IUnitOfWork`.

**Tech Stack:** .NET 8, FluentValidation (đã có từ Plan 01), xUnit + Shouldly. SQLite in-memory cho integration-style service tests.

**Reference:** [Design doc](../specs/2026-05-19-construction-finance-app-design.md) section 2 (Architecture), 5.2 (Cost tree rules), 4.5 (cascade rules). Builds on [Plan 01](2026-05-19-plan-01-foundation.md).

---

## Key Decisions (flag — phản hồi nếu không đồng ý)

| # | Quyết định | Lý do | Khác design doc? |
|---|---|---|---|
| D1 | Move `IRepository<T>`, `ICostRepository` từ `Data` → `Domain/Abstractions/` | Application chỉ được biết interface, không biết Data (Clean Architecture). Domain đã được cả Data + Application tham chiếu nên không phát sinh dependency mới | Làm rõ design doc 2.2 |
| D2 | Thêm `IUnitOfWork` (Domain) wrap `SaveChangesAsync`, implement trong Data | Service kiểm soát transaction boundary thay vì repo tự save lung tung | Bổ sung |
| D3 | Lỗi qua **exceptions** (`NotFoundException`, `BusinessRuleException`, `ValidationException`) | Đơn giản hơn Result<T> cho desktop single-user; ViewModel (Plan 03) sẽ catch | Phù hợp |
| D4 | **Hand-written mapping extensions** (`.ToDto()`, `.ToEntity()`) thay AutoMapper | Explicit, không reflection magic, dễ test, ít dependency — phù hợp 1 dev | **Khác** — design doc chọn AutoMapper. Recommend bỏ AutoMapper |
| D5 | Fix `AuditInterceptor` Create EntityId=0 bug bằng `SavedChanges` interceptor | Bug phát hiện khi review Plan 01: Id autoincrement chưa có lúc SavingChanges | Sửa bug |
| D6 | Service tests dùng **SQLite in-memory + real repository** (không mock repo) | Integration-style bắt được lỗi EF/query thật, tin cậy hơn mock | Khác design doc (mock) — recommend real |
| D7 | Mở rộng `IRepository<T>` với `FindAsync(predicate)` + `ExistsAsync(predicate)` | Service cần query có filter (vd contracts theo decisionId) + check tồn tại con khi xóa | Bổ sung |

---

## File Structure

```
src/ConstructionFinance.Domain/
├── Abstractions/                                    (NEW — moved + added)
│   ├── IRepository.cs                               (moved from Data, + FindAsync/ExistsAsync)
│   ├── ICostRepository.cs                           (moved from Data)
│   └── IUnitOfWork.cs                               (NEW)

src/ConstructionFinance.Data/
├── Repositories/
│   ├── Repository.cs                                (MODIFIED — namespace using, + FindAsync/ExistsAsync)
│   └── CostRepository.cs                            (MODIFIED — namespace using)
├── UnitOfWork.cs                                    (NEW)
├── Interceptors/
│   └── AuditInterceptor.cs                          (MODIFIED — fix Create EntityId via SavedChanges)

src/ConstructionFinance.Application/
├── ConstructionFinance.Application.csproj           (MODIFIED — add FluentValidation ref)
├── Common/
│   └── Exceptions/
│       ├── NotFoundException.cs                      (NEW)
│       └── BusinessRuleException.cs                  (NEW)
├── DTOs/
│   ├── VendorDtos.cs
│   ├── ApprovalDecisionDtos.cs
│   ├── ContractDtos.cs
│   ├── ProjectDtos.cs
│   ├── PaymentDtos.cs
│   ├── CostItemDtos.cs
│   └── AuditLogDtos.cs
├── Mapping/
│   ├── VendorMappings.cs
│   ├── ApprovalDecisionMappings.cs
│   ├── ContractMappings.cs
│   ├── ProjectMappings.cs
│   ├── PaymentMappings.cs
│   ├── CostItemMappings.cs
│   └── AuditLogMappings.cs
├── Services/
│   ├── Interfaces/
│   │   ├── IVendorService.cs
│   │   ├── IDecisionService.cs
│   │   ├── IContractService.cs
│   │   ├── IProjectService.cs
│   │   ├── IPaymentService.cs
│   │   ├── ICostService.cs
│   │   └── IAuditService.cs
│   └── Implementations/
│       ├── VendorService.cs
│       ├── DecisionService.cs
│       ├── ContractService.cs
│       ├── ProjectService.cs
│       ├── PaymentService.cs
│       ├── CostService.cs
│       └── AuditService.cs
└── DependencyInjection.cs                            (NEW — AddApplicationServices extension)

tests/ConstructionFinance.Application.Tests/         (NEW PROJECT)
├── ConstructionFinance.Application.Tests.csproj
├── ServiceTestHarness.cs                             (SQLite in-memory + real repos + services)
├── VendorServiceTests.cs
├── DecisionServiceTests.cs
├── ContractServiceTests.cs
├── ProjectServiceTests.cs
├── PaymentServiceTests.cs
├── CostServiceTests.cs
└── AuditServiceTests.cs
```

---

## Pre-flight

- [ ] **Step 0.1: Verify Plan 01 is complete and green**

Run from `/home/hieu-nguyen/management/`:
```bash
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
dotnet build --configuration Release
dotnet test --configuration Release
```
Expected: Build 0 errors. All 71 existing tests pass.

---

## Task 1: Relocate repository abstractions to Domain + add IUnitOfWork + extend IRepository

**Files:**
- Create: `src/ConstructionFinance.Domain/Abstractions/IRepository.cs`
- Create: `src/ConstructionFinance.Domain/Abstractions/ICostRepository.cs`
- Create: `src/ConstructionFinance.Domain/Abstractions/IUnitOfWork.cs`
- Delete: `src/ConstructionFinance.Data/Repositories/IRepository.cs`
- Delete: `src/ConstructionFinance.Data/Repositories/ICostRepository.cs`
- Modify: `src/ConstructionFinance.Data/Repositories/Repository.cs`
- Modify: `src/ConstructionFinance.Data/Repositories/CostRepository.cs`
- Create: `src/ConstructionFinance.Data/UnitOfWork.cs`
- Modify: `tests/ConstructionFinance.Data.Tests/Repositories/RepositoryTests.cs` (using)
- Modify: `tests/ConstructionFinance.Data.Tests/Repositories/CostRepositoryTests.cs` (using)
- Test: `tests/ConstructionFinance.Data.Tests/Repositories/RepositoryExtensionsTests.cs`

- [ ] **Step 1.1: Create `IRepository<T>` in Domain with new methods**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Abstractions/IRepository.cs`:
```csharp
using System.Linq.Expressions;
using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Domain.Abstractions;

public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListIncludingDeletedAsync(CancellationToken ct = default);
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    Task SoftDeleteAsync(T entity, CancellationToken ct = default);
    void HardDelete(T entity);
}
```

- [ ] **Step 1.2: Create `ICostRepository` in Domain**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Abstractions/ICostRepository.cs`:
```csharp
using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Domain.Abstractions;

public interface ICostRepository : IRepository<CostItem>
{
    Task<CostItem> AddNodeAsync(int projectId, int? parentId, CostItem newNode, CancellationToken ct = default);
    Task<IReadOnlyList<CostItem>> GetTreeByProjectAsync(int projectId, CancellationToken ct = default);
    Task<IReadOnlyList<CostItem>> GetSubtreeAsync(int nodeId, CancellationToken ct = default);
}
```

- [ ] **Step 1.3: Create `IUnitOfWork` in Domain**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Abstractions/IUnitOfWork.cs`:
```csharp
namespace ConstructionFinance.Domain.Abstractions;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

- [ ] **Step 1.4: Delete old interface files from Data**

Run:
```bash
rm src/ConstructionFinance.Data/Repositories/IRepository.cs
rm src/ConstructionFinance.Data/Repositories/ICostRepository.cs
```

- [ ] **Step 1.5: Update `Repository.cs` — use Domain namespace + implement new methods**

Replace `/home/hieu-nguyen/management/src/ConstructionFinance.Data/Repositories/Repository.cs` with:
```csharp
using System.Linq.Expressions;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace ConstructionFinance.Data.Repositories;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly AppDbContext Context;
    protected readonly DbSet<T> DbSet;

    public Repository(AppDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) =>
        DbSet.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default) =>
        await DbSet.AsNoTracking().ToListAsync(ct);

    public async Task<IReadOnlyList<T>> ListIncludingDeletedAsync(CancellationToken ct = default) =>
        await DbSet.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);

    public async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        await DbSet.Where(predicate).AsNoTracking().ToListAsync(ct);

    public Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        DbSet.AnyAsync(predicate, ct);

    public Task AddAsync(T entity, CancellationToken ct = default) =>
        DbSet.AddAsync(entity, ct).AsTask();

    public void Update(T entity) => DbSet.Update(entity);

    public Task SoftDeleteAsync(T entity, CancellationToken ct = default)
    {
        if (entity is ISoftDeletable sd)
        {
            sd.DeletedAt = DateTime.UtcNow;
            DbSet.Update(entity);
            return Task.CompletedTask;
        }
        DbSet.Remove(entity);
        return Task.CompletedTask;
    }

    public void HardDelete(T entity) => DbSet.Remove(entity);
}
```

- [ ] **Step 1.6: Update `CostRepository.cs` — use Domain namespace for interface**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Data/Repositories/CostRepository.cs`. Change the `using` block at top from:
```csharp
using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
```
to:
```csharp
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
```
(The class body stays identical — `CostRepository : Repository<CostItem>, ICostRepository`.)

- [ ] **Step 1.7: Implement `UnitOfWork` in Data**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Data/UnitOfWork.cs`:
```csharp
using ConstructionFinance.Domain.Abstractions;

namespace ConstructionFinance.Data;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context) => _context = context;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        _context.SaveChangesAsync(ct);
}
```

- [ ] **Step 1.8: Fix `using` in existing repository tests**

Edit `/home/hieu-nguyen/management/tests/ConstructionFinance.Data.Tests/Repositories/RepositoryTests.cs`: add `using ConstructionFinance.Domain.Abstractions;` to the top using block (the test references `IRepository<Vendor>`).

Edit `/home/hieu-nguyen/management/tests/ConstructionFinance.Data.Tests/Repositories/CostRepositoryTests.cs`: add `using ConstructionFinance.Domain.Abstractions;` to the top using block (references `ICostRepository`).

- [ ] **Step 1.9: Write test for new FindAsync/ExistsAsync + UnitOfWork**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Data.Tests/Repositories/RepositoryExtensionsTests.cs`:
```csharp
using ConstructionFinance.Data;
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Entities;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Repositories;

public class RepositoryExtensionsTests
{
    [Fact]
    public async Task FindAsync_filters_by_predicate()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        IRepository<Vendor> repo = new Repository<Vendor>(ctx);
        await repo.AddAsync(new Vendor { VendorName = "Alpha", TaxCode = "111" });
        await repo.AddAsync(new Vendor { VendorName = "Beta", TaxCode = "222" });
        await ctx.SaveChangesAsync();

        var found = await repo.FindAsync(v => v.TaxCode == "222");

        found.Count.ShouldBe(1);
        found[0].VendorName.ShouldBe("Beta");
    }

    [Fact]
    public async Task ExistsAsync_returns_true_when_match()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        IRepository<Vendor> repo = new Repository<Vendor>(ctx);
        await repo.AddAsync(new Vendor { VendorName = "Alpha" });
        await ctx.SaveChangesAsync();

        (await repo.ExistsAsync(v => v.VendorName == "Alpha")).ShouldBeTrue();
        (await repo.ExistsAsync(v => v.VendorName == "Zzz")).ShouldBeFalse();
    }

    [Fact]
    public async Task UnitOfWork_SaveChanges_persists()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        IRepository<Vendor> repo = new Repository<Vendor>(ctx);
        IUnitOfWork uow = new UnitOfWork(ctx);
        await repo.AddAsync(new Vendor { VendorName = "X" });

        var affected = await uow.SaveChangesAsync();

        affected.ShouldBeGreaterThan(0);
        ctx.Vendors.Count().ShouldBe(1);
    }
}
```

- [ ] **Step 1.10: Build + run all Data tests**

Run:
```bash
dotnet build src/ConstructionFinance.Data/
dotnet test tests/ConstructionFinance.Data.Tests/
```
Expected: Build succeeds, all Data tests pass (existing + 3 new).

- [ ] **Step 1.11: Commit**

```bash
git add .
git commit -m "refactor(data): move repo interfaces to Domain.Abstractions, add IUnitOfWork + FindAsync/ExistsAsync"
```

---

## Task 2: Fix AuditInterceptor Create EntityId bug

**Files:**
- Modify: `src/ConstructionFinance.Data/Interceptors/AuditInterceptor.cs`
- Test: `tests/ConstructionFinance.Data.Tests/Interceptors/AuditInterceptorTests.cs` (add a test)

**Problem:** For inserts, `entry.Property("Id").CurrentValue` is 0 during `SavingChanges` (autoincrement not yet assigned). Fix: defer Create-log EntityId resolution to `SavedChanges` (after Id is generated). We stage Create audit entries in `SavingChanges` keeping a reference to the entry, then patch EntityId + insert them in `SavedChanges`.

- [ ] **Step 2.1: Add failing test asserting Create audit log has correct EntityId**

Add to `/home/hieu-nguyen/management/tests/ConstructionFinance.Data.Tests/Interceptors/AuditInterceptorTests.cs` (inside the class):
```csharp
    [Fact]
    public void Insert_audit_log_captures_real_generated_EntityId()
    {
        using var ctx = CreateCtx();
        var v = new Vendor { VendorName = "X" };
        ctx.Vendors.Add(v);
        ctx.SaveChanges();

        v.Id.ShouldBeGreaterThan(0);
        var log = ctx.AuditLogs.Single(l => l.Action == ConstructionFinance.Domain.Enums.AuditAction.Create);
        log.EntityId.ShouldBe(v.Id);
    }
```

- [ ] **Step 2.2: Run — fails (EntityId is 0)**

Run: `dotnet test tests/ConstructionFinance.Data.Tests/ --filter "FullyQualifiedName~AuditInterceptorTests.Insert_audit_log_captures_real_generated_EntityId"`
Expected: FAIL — `log.EntityId` is 0, not `v.Id`.

- [ ] **Step 2.3: Rewrite AuditInterceptor to defer Create EntityId to SavedChanges**

Replace `/home/hieu-nguyen/management/src/ConstructionFinance.Data/Interceptors/AuditInterceptor.cs` with:
```csharp
using System.Text.Json;
using ConstructionFinance.Domain.Common;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ConstructionFinance.Data.Interceptors;

public class AuditInterceptor : SaveChangesInterceptor
{
    // Created entries whose EntityId must be read AFTER SaveChanges assigns the autoincrement Id.
    private readonly List<(EntityEntry Entry, AuditLog Log)> _pendingCreates = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        FlushPendingCreates(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        FlushPendingCreates(eventData.Context);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void Capture(DbContext? ctx)
    {
        if (ctx is null) return;
        var now = DateTime.UtcNow;
        var immediateLogs = new List<AuditLog>();

        foreach (var entry in ctx.ChangeTracker.Entries())
        {
            if (entry.Entity.GetType().GetCustomAttributes(typeof(AuditableAttribute), true).Length == 0)
                continue;

            var entityType = entry.Entity.GetType().Name;

            if (entry.State == EntityState.Added)
            {
                var log = new AuditLog
                {
                    EntityType = entityType,
                    EntityId = 0, // patched in SavedChanges
                    Action = AuditAction.Create,
                    NewValue = SerializeSnapshot(entry),
                    ChangedAt = now
                };
                _pendingCreates.Add((entry, log));
            }
            else if (entry.State == EntityState.Modified)
            {
                var id = GetId(entry);
                if (IsSoftDelete(entry))
                {
                    immediateLogs.Add(new AuditLog { EntityType = entityType, EntityId = id, Action = AuditAction.Delete, OldValue = SerializeSnapshot(entry), ChangedAt = now });
                    continue;
                }
                foreach (var prop in entry.Properties.Where(p => p.IsModified))
                {
                    if (prop.Metadata.Name == nameof(BaseEntity.UpdatedAt)) continue;
                    immediateLogs.Add(new AuditLog
                    {
                        EntityType = entityType,
                        EntityId = id,
                        Action = AuditAction.Update,
                        FieldName = prop.Metadata.Name,
                        OldValue = prop.OriginalValue?.ToString(),
                        NewValue = prop.CurrentValue?.ToString(),
                        ChangedAt = now
                    });
                }
            }
            else if (entry.State == EntityState.Deleted)
            {
                immediateLogs.Add(new AuditLog { EntityType = entityType, EntityId = GetId(entry), Action = AuditAction.Delete, OldValue = SerializeSnapshot(entry), ChangedAt = now });
            }
        }

        if (immediateLogs.Count > 0)
            ctx.Set<AuditLog>().AddRange(immediateLogs);
    }

    private void FlushPendingCreates(DbContext? ctx)
    {
        if (ctx is null || _pendingCreates.Count == 0) return;
        foreach (var (entry, log) in _pendingCreates)
            log.EntityId = GetId(entry);
        ctx.Set<AuditLog>().AddRange(_pendingCreates.Select(p => p.Log));
        _pendingCreates.Clear();
        ctx.SaveChanges(); // persist the Create logs (Id now known)
    }

    private static int GetId(EntityEntry entry) =>
        entry.Property("Id").CurrentValue is int id ? id : 0;

    private static bool IsSoftDelete(EntityEntry entry)
    {
        if (entry.Entity is not ISoftDeletable) return false;
        var deletedAt = entry.Property(nameof(ISoftDeletable.DeletedAt));
        return deletedAt.OriginalValue is null && deletedAt.CurrentValue is not null;
    }

    private static string SerializeSnapshot(EntityEntry entry)
    {
        var dict = entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);
        return JsonSerializer.Serialize(dict);
    }
}
```

> Note: `FlushPendingCreates` calls `ctx.SaveChanges()` once more to persist Create logs after the Id is known. This re-enters the interceptor, but pending list is already cleared and AuditLog has no `[Auditable]` attribute, so it does not recurse.

- [ ] **Step 2.4: Run the new test + full audit test class**

Run: `dotnet test tests/ConstructionFinance.Data.Tests/ --filter "FullyQualifiedName~AuditInterceptorTests"`
Expected: All audit tests pass, including the new EntityId test.

- [ ] **Step 2.5: Run full Data test suite (ensure no regression in smoke tests)**

Run: `dotnet test tests/ConstructionFinance.Data.Tests/`
Expected: All pass.

- [ ] **Step 2.6: Commit**

```bash
git add .
git commit -m "fix(data): AuditInterceptor captures real autoincrement EntityId for inserts"
```

---

## Task 3: Create Application.Tests project + Application exceptions

**Files:**
- Create test project: `tests/ConstructionFinance.Application.Tests/`
- Create: `src/ConstructionFinance.Application/Common/Exceptions/NotFoundException.cs`
- Create: `src/ConstructionFinance.Application/Common/Exceptions/BusinessRuleException.cs`
- Modify: `src/ConstructionFinance.Application/ConstructionFinance.Application.csproj`
- Test: `tests/ConstructionFinance.Application.Tests/ExceptionsTests.cs`

- [ ] **Step 3.1: Create Application.Tests xunit project + references**

Run from `/home/hieu-nguyen/management/`:
```bash
cd tests
dotnet new xunit -n ConstructionFinance.Application.Tests -o ConstructionFinance.Application.Tests
cd ..
rm -f tests/ConstructionFinance.Application.Tests/UnitTest1.cs
dotnet sln add tests/ConstructionFinance.Application.Tests/ConstructionFinance.Application.Tests.csproj
dotnet add tests/ConstructionFinance.Application.Tests/ConstructionFinance.Application.Tests.csproj reference src/ConstructionFinance.Application/ConstructionFinance.Application.csproj
dotnet add tests/ConstructionFinance.Application.Tests/ConstructionFinance.Application.Tests.csproj reference src/ConstructionFinance.Data/ConstructionFinance.Data.csproj
dotnet add tests/ConstructionFinance.Application.Tests/ConstructionFinance.Application.Tests.csproj reference src/ConstructionFinance.Domain/ConstructionFinance.Domain.csproj
```

- [ ] **Step 3.2: Set test packages for Application.Tests**

Replace the `<ItemGroup>` containing test packages in `/home/hieu-nguyen/management/tests/ConstructionFinance.Application.Tests/ConstructionFinance.Application.Tests.csproj` with:
```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Shouldly" />
    <PackageReference Include="Moq" />
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
  </ItemGroup>
```

- [ ] **Step 3.3: Add FluentValidation reference to Application project**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Application/ConstructionFinance.Application.csproj`. Add an `<ItemGroup>` with the package (alongside the existing project reference to Domain):
```xml
  <ItemGroup>
    <PackageReference Include="FluentValidation" />
  </ItemGroup>
```

- [ ] **Step 3.4: Write failing test for exceptions**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Application.Tests/ExceptionsTests.cs`:
```csharp
using ConstructionFinance.Application.Common.Exceptions;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class ExceptionsTests
{
    [Fact]
    public void NotFoundException_message_includes_entity_and_id()
    {
        var ex = new NotFoundException("Vendor", 42);
        ex.Message.ShouldContain("Vendor");
        ex.Message.ShouldContain("42");
    }

    [Fact]
    public void BusinessRuleException_carries_message()
    {
        var ex = new BusinessRuleException("Không thể xóa khi còn hợp đồng con");
        ex.Message.ShouldBe("Không thể xóa khi còn hợp đồng con");
    }
}
```

- [ ] **Step 3.5: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~ExceptionsTests"`
Expected: Types not found.

- [ ] **Step 3.6: Implement NotFoundException**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Common/Exceptions/NotFoundException.cs`:
```csharp
namespace ConstructionFinance.Application.Common.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string entityName, int id)
        : base($"Không tìm thấy {entityName} với Id = {id}") { }
}
```

- [ ] **Step 3.7: Implement BusinessRuleException**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Common/Exceptions/BusinessRuleException.cs`:
```csharp
namespace ConstructionFinance.Application.Common.Exceptions;

public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message) { }
}
```

- [ ] **Step 3.8: Verify + commit**

```bash
dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~ExceptionsTests"
git add . && git commit -m "feat(app): add Application.Tests project + NotFoundException/BusinessRuleException"
```

---

## Task 4: Vendor DTOs + mapping + ServiceTestHarness

**Files:**
- Create: `src/ConstructionFinance.Application/DTOs/VendorDtos.cs`
- Create: `src/ConstructionFinance.Application/Mapping/VendorMappings.cs`
- Create: `tests/ConstructionFinance.Application.Tests/ServiceTestHarness.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Mapping/VendorMappingsTests.cs`

- [ ] **Step 4.1: Write failing test for mapping**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Application.Tests/Mapping/VendorMappingsTests.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Mapping;
using ConstructionFinance.Domain.Entities;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Mapping;

public class VendorMappingsTests
{
    [Fact]
    public void ToDto_maps_all_fields()
    {
        var v = new Vendor { Id = 7, VendorCode = "V1", VendorName = "ABC", TaxCode = "123", Address = "Addr", Phone = "p", Email = "e@x.vn", ContactPerson = "cp", Notes = "n" };
        var dto = v.ToDto();
        dto.Id.ShouldBe(7);
        dto.VendorName.ShouldBe("ABC");
        dto.TaxCode.ShouldBe("123");
        dto.Email.ShouldBe("e@x.vn");
    }

    [Fact]
    public void CreateDto_ToEntity_maps_fields()
    {
        var dto = new CreateVendorDto(VendorCode: "V1", VendorName: "ABC", TaxCode: "123", Address: null, Phone: null, Email: null, ContactPerson: null, Notes: null);
        var e = dto.ToEntity();
        e.VendorName.ShouldBe("ABC");
        e.VendorCode.ShouldBe("V1");
    }

    [Fact]
    public void UpdateDto_Apply_overwrites_entity_fields()
    {
        var e = new Vendor { Id = 5, VendorName = "Old" };
        var dto = new UpdateVendorDto(Id: 5, VendorCode: "V9", VendorName: "New", TaxCode: null, Address: null, Phone: null, Email: null, ContactPerson: null, Notes: null);
        dto.Apply(e);
        e.VendorName.ShouldBe("New");
        e.VendorCode.ShouldBe("V9");
    }
}
```

- [ ] **Step 4.2: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~VendorMappingsTests"`
Expected: Types missing.

- [ ] **Step 4.3: Implement VendorDtos**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/DTOs/VendorDtos.cs`:
```csharp
namespace ConstructionFinance.Application.DTOs;

public record VendorDto(
    int Id,
    string? VendorCode,
    string VendorName,
    string? TaxCode,
    string? Address,
    string? Phone,
    string? Email,
    string? ContactPerson,
    string? Notes);

public record CreateVendorDto(
    string? VendorCode,
    string VendorName,
    string? TaxCode,
    string? Address,
    string? Phone,
    string? Email,
    string? ContactPerson,
    string? Notes);

public record UpdateVendorDto(
    int Id,
    string? VendorCode,
    string VendorName,
    string? TaxCode,
    string? Address,
    string? Phone,
    string? Email,
    string? ContactPerson,
    string? Notes);
```

- [ ] **Step 4.4: Implement VendorMappings**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Mapping/VendorMappings.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Application.Mapping;

public static class VendorMappings
{
    public static VendorDto ToDto(this Vendor e) =>
        new(e.Id, e.VendorCode, e.VendorName, e.TaxCode, e.Address, e.Phone, e.Email, e.ContactPerson, e.Notes);

    public static Vendor ToEntity(this CreateVendorDto d) =>
        new()
        {
            VendorCode = d.VendorCode,
            VendorName = d.VendorName,
            TaxCode = d.TaxCode,
            Address = d.Address,
            Phone = d.Phone,
            Email = d.Email,
            ContactPerson = d.ContactPerson,
            Notes = d.Notes
        };

    public static void Apply(this UpdateVendorDto d, Vendor e)
    {
        e.VendorCode = d.VendorCode;
        e.VendorName = d.VendorName;
        e.TaxCode = d.TaxCode;
        e.Address = d.Address;
        e.Phone = d.Phone;
        e.Email = d.Email;
        e.ContactPerson = d.ContactPerson;
        e.Notes = d.Notes;
    }
}
```

- [ ] **Step 4.5: Implement ServiceTestHarness (shared by all service tests)**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Application.Tests/ServiceTestHarness.cs`:
```csharp
using ConstructionFinance.Data;
using ConstructionFinance.Data.Interceptors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ConstructionFinance.Application.Tests;

/// Spins up a real SQLite in-memory AppDbContext with interceptors,
/// for integration-style service tests (real repositories + UnitOfWork).
public sealed class ServiceTestHarness : IDisposable
{
    private readonly SqliteConnection _connection;
    public AppDbContext Context { get; }

    public ServiceTestHarness()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new TimestampInterceptor(), new AuditInterceptor())
            .Options;
        Context = new AppDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
```

- [ ] **Step 4.6: Verify + commit**

```bash
dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~VendorMappingsTests"
git add . && git commit -m "feat(app): Vendor DTOs + mappings + ServiceTestHarness"
```

---

## Task 5: VendorService (template for all CRUD services)

**Files:**
- Create: `src/ConstructionFinance.Application/Services/Interfaces/IVendorService.cs`
- Create: `src/ConstructionFinance.Application/Services/Implementations/VendorService.cs`
- Test: `tests/ConstructionFinance.Application.Tests/VendorServiceTests.cs`

- [ ] **Step 5.1: Write failing tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Application.Tests/VendorServiceTests.cs`:
```csharp
using ConstructionFinance.Application.Common.Exceptions;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Data;
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Validators;
using FluentValidation;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class VendorServiceTests
{
    private static IVendorService CreateService(AppDbContext ctx)
    {
        var repo = new Repository<Vendor>(ctx);
        var uow = new UnitOfWork(ctx);
        IValidator<Vendor> validator = new VendorValidator();
        return new VendorService(repo, uow, validator);
    }

    [Fact]
    public async Task CreateAsync_persists_and_returns_dto_with_id()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);

        var dto = await svc.CreateAsync(new CreateVendorDto(null, "Công ty ABC", "0123456789", null, null, null, null, null));

        dto.Id.ShouldBeGreaterThan(0);
        dto.VendorName.ShouldBe("Công ty ABC");
        h.Context.Vendors.Count().ShouldBe(1);
    }

    [Fact]
    public async Task CreateAsync_throws_ValidationException_when_name_empty()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);

        await Should.ThrowAsync<ValidationException>(async () =>
            await svc.CreateAsync(new CreateVendorDto(null, "", null, null, null, null, null, null)));
    }

    [Fact]
    public async Task GetByIdAsync_throws_NotFound_when_missing()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);

        await Should.ThrowAsync<NotFoundException>(async () => await svc.GetByIdAsync(999));
    }

    [Fact]
    public async Task UpdateAsync_changes_fields()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);
        var created = await svc.CreateAsync(new CreateVendorDto(null, "Old", null, null, null, null, null, null));

        var updated = await svc.UpdateAsync(new UpdateVendorDto(created.Id, "V9", "New", null, null, null, null, null, null));

        updated.VendorName.ShouldBe("New");
        updated.VendorCode.ShouldBe("V9");
    }

    [Fact]
    public async Task DeleteAsync_soft_deletes_and_hides_from_GetAll()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);
        var created = await svc.CreateAsync(new CreateVendorDto(null, "X", null, null, null, null, null, null));

        await svc.DeleteAsync(created.Id);

        (await svc.GetAllAsync()).ShouldBeEmpty();
    }
}
```

- [ ] **Step 5.2: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~VendorServiceTests"`
Expected: `IVendorService`/`VendorService` not found.

- [ ] **Step 5.3: Implement IVendorService**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Services/Interfaces/IVendorService.cs`:
```csharp
using ConstructionFinance.Application.DTOs;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IVendorService
{
    Task<VendorDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<VendorDto>> GetAllAsync(CancellationToken ct = default);
    Task<VendorDto> CreateAsync(CreateVendorDto dto, CancellationToken ct = default);
    Task<VendorDto> UpdateAsync(UpdateVendorDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
```

- [ ] **Step 5.4: Implement VendorService**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Services/Implementations/VendorService.cs`:
```csharp
using ConstructionFinance.Application.Common.Exceptions;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Mapping;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Entities;
using FluentValidation;

namespace ConstructionFinance.Application.Services.Implementations;

public class VendorService : IVendorService
{
    private readonly IRepository<Vendor> _vendors;
    private readonly IUnitOfWork _uow;
    private readonly IValidator<Vendor> _validator;

    public VendorService(IRepository<Vendor> vendors, IUnitOfWork uow, IValidator<Vendor> validator)
    {
        _vendors = vendors;
        _uow = uow;
        _validator = validator;
    }

    public async Task<VendorDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var entity = await _vendors.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Vendor), id);
        return entity.ToDto();
    }

    public async Task<IReadOnlyList<VendorDto>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _vendors.ListAsync(ct);
        return list.Select(v => v.ToDto()).ToList();
    }

    public async Task<VendorDto> CreateAsync(CreateVendorDto dto, CancellationToken ct = default)
    {
        var entity = dto.ToEntity();
        await _validator.ValidateAndThrowAsync(entity, ct);
        await _vendors.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<VendorDto> UpdateAsync(UpdateVendorDto dto, CancellationToken ct = default)
    {
        var entity = await _vendors.GetByIdAsync(dto.Id, ct) ?? throw new NotFoundException(nameof(Vendor), dto.Id);
        dto.Apply(entity);
        await _validator.ValidateAndThrowAsync(entity, ct);
        _vendors.Update(entity);
        await _uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _vendors.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Vendor), id);
        await _vendors.SoftDeleteAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 5.5: Verify + commit**

```bash
dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~VendorServiceTests"
git add . && git commit -m "feat(app): VendorService with full CRUD + soft delete"
```

---

## Task 6: DecisionService (CRUD + delete guard)

**Files:**
- Create: `src/ConstructionFinance.Application/DTOs/ApprovalDecisionDtos.cs`
- Create: `src/ConstructionFinance.Application/Mapping/ApprovalDecisionMappings.cs`
- Create: `src/ConstructionFinance.Application/Services/Interfaces/IDecisionService.cs`
- Create: `src/ConstructionFinance.Application/Services/Implementations/DecisionService.cs`
- Test: `tests/ConstructionFinance.Application.Tests/DecisionServiceTests.cs`

- [ ] **Step 6.1: Write failing tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Application.Tests/DecisionServiceTests.cs`:
```csharp
using ConstructionFinance.Application.Common.Exceptions;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Data;
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Validators;
using FluentValidation;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class DecisionServiceTests
{
    private static IDecisionService CreateService(AppDbContext ctx) =>
        new DecisionService(
            new Repository<ApprovalDecision>(ctx),
            new Repository<Contract>(ctx),
            new UnitOfWork(ctx),
            new ApprovalDecisionValidator());

    private static CreateDecisionDto ValidCreate(string code = "1234/QD") =>
        new(code, "Phê duyệt dự án", new DateOnly(2026, 5, 1), "UBND Tỉnh", 50_000_000_000m, null);

    [Fact]
    public async Task CreateAsync_persists()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);

        var dto = await svc.CreateAsync(ValidCreate());

        dto.Id.ShouldBeGreaterThan(0);
        dto.DecisionCode.ShouldBe("1234/QD");
    }

    [Fact]
    public async Task CreateAsync_invalid_when_amount_negative()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);

        await Should.ThrowAsync<ValidationException>(async () =>
            await svc.CreateAsync(new CreateDecisionDto("X", "T", new DateOnly(2026, 5, 1), "A", -1m, null)));
    }

    [Fact]
    public async Task DeleteAsync_blocked_when_contracts_exist()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);
        var decision = await svc.CreateAsync(ValidCreate());
        h.Context.Contracts.Add(new Contract { ApprovalDecisionId = decision.Id, ContractCode = "C1", ContractName = "C", ContractDate = new DateOnly(2026, 5, 1), ContractValue = 1m });
        await h.Context.SaveChangesAsync();

        await Should.ThrowAsync<BusinessRuleException>(async () => await svc.DeleteAsync(decision.Id));
    }

    [Fact]
    public async Task DeleteAsync_succeeds_when_no_contracts()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);
        var decision = await svc.CreateAsync(ValidCreate());

        await svc.DeleteAsync(decision.Id);

        (await svc.GetAllAsync()).ShouldBeEmpty();
    }
}
```

- [ ] **Step 6.2: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~DecisionServiceTests"`
Expected: Types missing.

- [ ] **Step 6.3: Implement ApprovalDecisionDtos**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/DTOs/ApprovalDecisionDtos.cs`:
```csharp
namespace ConstructionFinance.Application.DTOs;

public record DecisionDto(
    int Id,
    string DecisionCode,
    string DecisionTitle,
    DateOnly DecisionDate,
    string IssuingAuthority,
    decimal TotalApprovedAmount,
    string? Notes);

public record CreateDecisionDto(
    string DecisionCode,
    string DecisionTitle,
    DateOnly DecisionDate,
    string IssuingAuthority,
    decimal TotalApprovedAmount,
    string? Notes);

public record UpdateDecisionDto(
    int Id,
    string DecisionCode,
    string DecisionTitle,
    DateOnly DecisionDate,
    string IssuingAuthority,
    decimal TotalApprovedAmount,
    string? Notes);
```

- [ ] **Step 6.4: Implement ApprovalDecisionMappings**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Mapping/ApprovalDecisionMappings.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Application.Mapping;

public static class ApprovalDecisionMappings
{
    public static DecisionDto ToDto(this ApprovalDecision e) =>
        new(e.Id, e.DecisionCode, e.DecisionTitle, e.DecisionDate, e.IssuingAuthority, e.TotalApprovedAmount, e.Notes);

    public static ApprovalDecision ToEntity(this CreateDecisionDto d) =>
        new()
        {
            DecisionCode = d.DecisionCode,
            DecisionTitle = d.DecisionTitle,
            DecisionDate = d.DecisionDate,
            IssuingAuthority = d.IssuingAuthority,
            TotalApprovedAmount = d.TotalApprovedAmount,
            Notes = d.Notes
        };

    public static void Apply(this UpdateDecisionDto d, ApprovalDecision e)
    {
        e.DecisionCode = d.DecisionCode;
        e.DecisionTitle = d.DecisionTitle;
        e.DecisionDate = d.DecisionDate;
        e.IssuingAuthority = d.IssuingAuthority;
        e.TotalApprovedAmount = d.TotalApprovedAmount;
        e.Notes = d.Notes;
    }
}
```

- [ ] **Step 6.5: Implement IDecisionService**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Services/Interfaces/IDecisionService.cs`:
```csharp
using ConstructionFinance.Application.DTOs;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IDecisionService
{
    Task<DecisionDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<DecisionDto>> GetAllAsync(CancellationToken ct = default);
    Task<DecisionDto> CreateAsync(CreateDecisionDto dto, CancellationToken ct = default);
    Task<DecisionDto> UpdateAsync(UpdateDecisionDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
```

- [ ] **Step 6.6: Implement DecisionService**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Services/Implementations/DecisionService.cs`:
```csharp
using ConstructionFinance.Application.Common.Exceptions;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Mapping;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Entities;
using FluentValidation;

namespace ConstructionFinance.Application.Services.Implementations;

public class DecisionService : IDecisionService
{
    private readonly IRepository<ApprovalDecision> _decisions;
    private readonly IRepository<Contract> _contracts;
    private readonly IUnitOfWork _uow;
    private readonly IValidator<ApprovalDecision> _validator;

    public DecisionService(
        IRepository<ApprovalDecision> decisions,
        IRepository<Contract> contracts,
        IUnitOfWork uow,
        IValidator<ApprovalDecision> validator)
    {
        _decisions = decisions;
        _contracts = contracts;
        _uow = uow;
        _validator = validator;
    }

    public async Task<DecisionDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var e = await _decisions.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(ApprovalDecision), id);
        return e.ToDto();
    }

    public async Task<IReadOnlyList<DecisionDto>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _decisions.ListAsync(ct);
        return list.Select(x => x.ToDto()).ToList();
    }

    public async Task<DecisionDto> CreateAsync(CreateDecisionDto dto, CancellationToken ct = default)
    {
        var entity = dto.ToEntity();
        await _validator.ValidateAndThrowAsync(entity, ct);
        await _decisions.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<DecisionDto> UpdateAsync(UpdateDecisionDto dto, CancellationToken ct = default)
    {
        var entity = await _decisions.GetByIdAsync(dto.Id, ct) ?? throw new NotFoundException(nameof(ApprovalDecision), dto.Id);
        dto.Apply(entity);
        await _validator.ValidateAndThrowAsync(entity, ct);
        _decisions.Update(entity);
        await _uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _decisions.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(ApprovalDecision), id);
        if (await _contracts.ExistsAsync(c => c.ApprovalDecisionId == id, ct))
            throw new BusinessRuleException("Không thể xóa Quyết định khi còn Hợp đồng trực thuộc. Hãy xóa Hợp đồng trước.");
        await _decisions.SoftDeleteAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 6.7: Verify + commit**

```bash
dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~DecisionServiceTests"
git add . && git commit -m "feat(app): DecisionService with delete guard (RESTRICT when contracts exist)"
```

---

## Task 7: ContractService (parent check + delete guard)

**Files:**
- Create: `src/ConstructionFinance.Application/DTOs/ContractDtos.cs`
- Create: `src/ConstructionFinance.Application/Mapping/ContractMappings.cs`
- Create: `src/ConstructionFinance.Application/Services/Interfaces/IContractService.cs`
- Create: `src/ConstructionFinance.Application/Services/Implementations/ContractService.cs`
- Test: `tests/ConstructionFinance.Application.Tests/ContractServiceTests.cs`

- [ ] **Step 7.1: Write failing tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Application.Tests/ContractServiceTests.cs`:
```csharp
using ConstructionFinance.Application.Common.Exceptions;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Data;
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using ConstructionFinance.Domain.Validators;
using FluentValidation;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class ContractServiceTests
{
    private static IContractService CreateService(AppDbContext ctx) =>
        new ContractService(
            new Repository<Contract>(ctx),
            new Repository<ApprovalDecision>(ctx),
            new Repository<Project>(ctx),
            new UnitOfWork(ctx),
            new ContractValidator());

    private static async Task<int> SeedDecision(AppDbContext ctx, string code = "D1")
    {
        var d = new ApprovalDecision { DecisionCode = code, DecisionTitle = "X", IssuingAuthority = "U", DecisionDate = new DateOnly(2026, 1, 1), TotalApprovedAmount = 1m };
        ctx.ApprovalDecisions.Add(d);
        await ctx.SaveChangesAsync();
        return d.Id;
    }

    [Fact]
    public async Task CreateAsync_succeeds_with_existing_decision()
    {
        using var h = new ServiceTestHarness();
        var decisionId = await SeedDecision(h.Context);
        var svc = CreateService(h.Context);

        var dto = await svc.CreateAsync(new CreateContractDto(decisionId, null, "HD-1", "Hợp đồng A", new DateOnly(2026, 5, 1), null, 1_000_000m, null));

        dto.Id.ShouldBeGreaterThan(0);
        dto.ContractCode.ShouldBe("HD-1");
    }

    [Fact]
    public async Task CreateAsync_throws_NotFound_when_decision_missing()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);

        await Should.ThrowAsync<NotFoundException>(async () =>
            await svc.CreateAsync(new CreateContractDto(999, null, "HD-1", "A", new DateOnly(2026, 5, 1), null, 1m, null)));
    }

    [Fact]
    public async Task DeleteAsync_blocked_when_projects_exist()
    {
        using var h = new ServiceTestHarness();
        var decisionId = await SeedDecision(h.Context);
        var svc = CreateService(h.Context);
        var contract = await svc.CreateAsync(new CreateContractDto(decisionId, null, "HD-1", "A", new DateOnly(2026, 5, 1), null, 1m, null));
        h.Context.Projects.Add(new Project { ContractId = contract.Id, ProjectCode = "P1", ProjectName = "P", Status = ProjectStatus.Planning });
        await h.Context.SaveChangesAsync();

        await Should.ThrowAsync<BusinessRuleException>(async () => await svc.DeleteAsync(contract.Id));
    }

    [Fact]
    public async Task GetByDecisionAsync_returns_only_contracts_of_that_decision()
    {
        using var h = new ServiceTestHarness();
        var d1 = await SeedDecision(h.Context, "D1");
        var d2 = await SeedDecision(h.Context, "D2");
        var svc = CreateService(h.Context);
        await svc.CreateAsync(new CreateContractDto(d1, null, "HD-1", "A", new DateOnly(2026, 5, 1), null, 1m, null));
        await svc.CreateAsync(new CreateContractDto(d1, null, "HD-2", "B", new DateOnly(2026, 5, 1), null, 1m, null));
        await svc.CreateAsync(new CreateContractDto(d2, null, "HD-3", "C", new DateOnly(2026, 5, 1), null, 1m, null));

        var d1Contracts = await svc.GetByDecisionAsync(d1);

        d1Contracts.Count.ShouldBe(2);
    }
}
```

- [ ] **Step 7.2: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~ContractServiceTests"`
Expected: Types missing.

- [ ] **Step 7.3: Implement ContractDtos**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/DTOs/ContractDtos.cs`:
```csharp
namespace ConstructionFinance.Application.DTOs;

public record ContractDto(
    int Id,
    int ApprovalDecisionId,
    int? ContractorVendorId,
    string ContractCode,
    string ContractName,
    DateOnly ContractDate,
    DateOnly? SigningDate,
    decimal ContractValue,
    string? Notes);

public record CreateContractDto(
    int ApprovalDecisionId,
    int? ContractorVendorId,
    string ContractCode,
    string ContractName,
    DateOnly ContractDate,
    DateOnly? SigningDate,
    decimal ContractValue,
    string? Notes);

public record UpdateContractDto(
    int Id,
    int ApprovalDecisionId,
    int? ContractorVendorId,
    string ContractCode,
    string ContractName,
    DateOnly ContractDate,
    DateOnly? SigningDate,
    decimal ContractValue,
    string? Notes);
```

- [ ] **Step 7.4: Implement ContractMappings**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Mapping/ContractMappings.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Application.Mapping;

public static class ContractMappings
{
    public static ContractDto ToDto(this Contract e) =>
        new(e.Id, e.ApprovalDecisionId, e.ContractorVendorId, e.ContractCode, e.ContractName, e.ContractDate, e.SigningDate, e.ContractValue, e.Notes);

    public static Contract ToEntity(this CreateContractDto d) =>
        new()
        {
            ApprovalDecisionId = d.ApprovalDecisionId,
            ContractorVendorId = d.ContractorVendorId,
            ContractCode = d.ContractCode,
            ContractName = d.ContractName,
            ContractDate = d.ContractDate,
            SigningDate = d.SigningDate,
            ContractValue = d.ContractValue,
            Notes = d.Notes
        };

    public static void Apply(this UpdateContractDto d, Contract e)
    {
        e.ApprovalDecisionId = d.ApprovalDecisionId;
        e.ContractorVendorId = d.ContractorVendorId;
        e.ContractCode = d.ContractCode;
        e.ContractName = d.ContractName;
        e.ContractDate = d.ContractDate;
        e.SigningDate = d.SigningDate;
        e.ContractValue = d.ContractValue;
        e.Notes = d.Notes;
    }
}
```

- [ ] **Step 7.5: Implement IContractService**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Services/Interfaces/IContractService.cs`:
```csharp
using ConstructionFinance.Application.DTOs;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IContractService
{
    Task<ContractDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<ContractDto>> GetByDecisionAsync(int decisionId, CancellationToken ct = default);
    Task<ContractDto> CreateAsync(CreateContractDto dto, CancellationToken ct = default);
    Task<ContractDto> UpdateAsync(UpdateContractDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
```

- [ ] **Step 7.6: Implement ContractService**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Services/Implementations/ContractService.cs`:
```csharp
using ConstructionFinance.Application.Common.Exceptions;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Mapping;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Entities;
using FluentValidation;

namespace ConstructionFinance.Application.Services.Implementations;

public class ContractService : IContractService
{
    private readonly IRepository<Contract> _contracts;
    private readonly IRepository<ApprovalDecision> _decisions;
    private readonly IRepository<Project> _projects;
    private readonly IUnitOfWork _uow;
    private readonly IValidator<Contract> _validator;

    public ContractService(
        IRepository<Contract> contracts,
        IRepository<ApprovalDecision> decisions,
        IRepository<Project> projects,
        IUnitOfWork uow,
        IValidator<Contract> validator)
    {
        _contracts = contracts;
        _decisions = decisions;
        _projects = projects;
        _uow = uow;
        _validator = validator;
    }

    public async Task<ContractDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var e = await _contracts.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Contract), id);
        return e.ToDto();
    }

    public async Task<IReadOnlyList<ContractDto>> GetByDecisionAsync(int decisionId, CancellationToken ct = default)
    {
        var list = await _contracts.FindAsync(c => c.ApprovalDecisionId == decisionId, ct);
        return list.Select(x => x.ToDto()).ToList();
    }

    public async Task<ContractDto> CreateAsync(CreateContractDto dto, CancellationToken ct = default)
    {
        if (await _decisions.GetByIdAsync(dto.ApprovalDecisionId, ct) is null)
            throw new NotFoundException(nameof(ApprovalDecision), dto.ApprovalDecisionId);

        var entity = dto.ToEntity();
        await _validator.ValidateAndThrowAsync(entity, ct);
        await _contracts.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<ContractDto> UpdateAsync(UpdateContractDto dto, CancellationToken ct = default)
    {
        var entity = await _contracts.GetByIdAsync(dto.Id, ct) ?? throw new NotFoundException(nameof(Contract), dto.Id);
        dto.Apply(entity);
        await _validator.ValidateAndThrowAsync(entity, ct);
        _contracts.Update(entity);
        await _uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _contracts.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Contract), id);
        if (await _projects.ExistsAsync(p => p.ContractId == id, ct))
            throw new BusinessRuleException("Không thể xóa Hợp đồng khi còn Dự án trực thuộc. Hãy xóa Dự án trước.");
        await _contracts.SoftDeleteAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 7.7: Verify + commit**

```bash
dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~ContractServiceTests"
git add . && git commit -m "feat(app): ContractService with parent-exists check + delete guard"
```

---

## Task 8: ProjectService (CRUD + status state machine + delete guard)

**Files:**
- Create: `src/ConstructionFinance.Application/DTOs/ProjectDtos.cs`
- Create: `src/ConstructionFinance.Application/Mapping/ProjectMappings.cs`
- Create: `src/ConstructionFinance.Application/Services/Interfaces/IProjectService.cs`
- Create: `src/ConstructionFinance.Application/Services/Implementations/ProjectService.cs`
- Test: `tests/ConstructionFinance.Application.Tests/ProjectServiceTests.cs`

State machine (design doc 4.5): `Planning → InProgress → {OnHold↔InProgress, Completed, Cancelled}`, `Planning → Cancelled`. From `Completed`/`Cancelled` → no transitions. Block illegal transitions with `BusinessRuleException`.

- [ ] **Step 8.1: Write failing tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Application.Tests/ProjectServiceTests.cs`:
```csharp
using ConstructionFinance.Application.Common.Exceptions;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Data;
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using ConstructionFinance.Domain.Validators;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class ProjectServiceTests
{
    private static IProjectService CreateService(AppDbContext ctx) =>
        new ProjectService(
            new Repository<Project>(ctx),
            new Repository<Contract>(ctx),
            new Repository<CostItem>(ctx),
            new UnitOfWork(ctx),
            new ProjectValidator());

    private static async Task<int> SeedContract(AppDbContext ctx)
    {
        var d = new ApprovalDecision { DecisionCode = "D1", DecisionTitle = "X", IssuingAuthority = "U", DecisionDate = new DateOnly(2026, 1, 1), TotalApprovedAmount = 1m };
        ctx.ApprovalDecisions.Add(d);
        await ctx.SaveChangesAsync();
        var c = new Contract { ApprovalDecisionId = d.Id, ContractCode = "C1", ContractName = "C", ContractDate = new DateOnly(2026, 1, 1), ContractValue = 1m };
        ctx.Contracts.Add(c);
        await ctx.SaveChangesAsync();
        return c.Id;
    }

    private static CreateProjectDto ValidCreate(int contractId) =>
        new(contractId, "DA-1", "Nhà máy XYZ", "Dân dụng", "Hà Nội", null, null, 1_000_000m, null);

    [Fact]
    public async Task CreateAsync_defaults_status_Planning()
    {
        using var h = new ServiceTestHarness();
        var contractId = await SeedContract(h.Context);
        var svc = CreateService(h.Context);

        var dto = await svc.CreateAsync(ValidCreate(contractId));

        dto.Status.ShouldBe(ProjectStatus.Planning);
    }

    [Fact]
    public async Task UpdateStatus_Planning_to_InProgress_allowed()
    {
        using var h = new ServiceTestHarness();
        var contractId = await SeedContract(h.Context);
        var svc = CreateService(h.Context);
        var p = await svc.CreateAsync(ValidCreate(contractId));

        var updated = await svc.UpdateStatusAsync(p.Id, ProjectStatus.InProgress);

        updated.Status.ShouldBe(ProjectStatus.InProgress);
    }

    [Fact]
    public async Task UpdateStatus_Completed_to_InProgress_blocked()
    {
        using var h = new ServiceTestHarness();
        var contractId = await SeedContract(h.Context);
        var svc = CreateService(h.Context);
        var p = await svc.CreateAsync(ValidCreate(contractId));
        await svc.UpdateStatusAsync(p.Id, ProjectStatus.InProgress);
        await svc.UpdateStatusAsync(p.Id, ProjectStatus.Completed);

        await Should.ThrowAsync<BusinessRuleException>(async () =>
            await svc.UpdateStatusAsync(p.Id, ProjectStatus.InProgress));
    }

    [Fact]
    public async Task UpdateStatus_Planning_to_Completed_blocked()
    {
        using var h = new ServiceTestHarness();
        var contractId = await SeedContract(h.Context);
        var svc = CreateService(h.Context);
        var p = await svc.CreateAsync(ValidCreate(contractId));

        await Should.ThrowAsync<BusinessRuleException>(async () =>
            await svc.UpdateStatusAsync(p.Id, ProjectStatus.Completed));
    }

    [Fact]
    public async Task DeleteAsync_blocked_when_cost_items_exist()
    {
        using var h = new ServiceTestHarness();
        var contractId = await SeedContract(h.Context);
        var svc = CreateService(h.Context);
        var p = await svc.CreateAsync(ValidCreate(contractId));
        h.Context.CostItems.Add(new CostItem { ProjectId = p.Id, ItemName = "X", Path = "/", Level = 0, IsLeaf = true });
        await h.Context.SaveChangesAsync();

        await Should.ThrowAsync<BusinessRuleException>(async () => await svc.DeleteAsync(p.Id));
    }
}
```

- [ ] **Step 8.2: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~ProjectServiceTests"`
Expected: Types missing.

- [ ] **Step 8.3: Implement ProjectDtos**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/DTOs/ProjectDtos.cs`:
```csharp
using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Application.DTOs;

public record ProjectDto(
    int Id,
    int ContractId,
    string ProjectCode,
    string ProjectName,
    string? ProjectType,
    string? Location,
    DateOnly? StartDate,
    DateOnly? EndDate,
    ProjectStatus Status,
    decimal TotalBudget,
    decimal TotalActual,
    string? Notes);

public record CreateProjectDto(
    int ContractId,
    string ProjectCode,
    string ProjectName,
    string? ProjectType,
    string? Location,
    DateOnly? StartDate,
    DateOnly? EndDate,
    decimal TotalBudget,
    string? Notes);

public record UpdateProjectDto(
    int Id,
    int ContractId,
    string ProjectCode,
    string ProjectName,
    string? ProjectType,
    string? Location,
    DateOnly? StartDate,
    DateOnly? EndDate,
    decimal TotalBudget,
    string? Notes);
```

- [ ] **Step 8.4: Implement ProjectMappings**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Mapping/ProjectMappings.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Application.Mapping;

public static class ProjectMappings
{
    public static ProjectDto ToDto(this Project e) =>
        new(e.Id, e.ContractId, e.ProjectCode, e.ProjectName, e.ProjectType, e.Location, e.StartDate, e.EndDate, e.Status, e.TotalBudget, e.TotalActual, e.Notes);

    public static Project ToEntity(this CreateProjectDto d) =>
        new()
        {
            ContractId = d.ContractId,
            ProjectCode = d.ProjectCode,
            ProjectName = d.ProjectName,
            ProjectType = d.ProjectType,
            Location = d.Location,
            StartDate = d.StartDate,
            EndDate = d.EndDate,
            Status = ProjectStatus.Planning,
            TotalBudget = d.TotalBudget,
            Notes = d.Notes
        };

    public static void Apply(this UpdateProjectDto d, Project e)
    {
        e.ContractId = d.ContractId;
        e.ProjectCode = d.ProjectCode;
        e.ProjectName = d.ProjectName;
        e.ProjectType = d.ProjectType;
        e.Location = d.Location;
        e.StartDate = d.StartDate;
        e.EndDate = d.EndDate;
        e.TotalBudget = d.TotalBudget;
        e.Notes = d.Notes;
    }
}
```

- [ ] **Step 8.5: Implement IProjectService**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Services/Interfaces/IProjectService.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IProjectService
{
    Task<ProjectDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectDto>> GetByContractAsync(int contractId, CancellationToken ct = default);
    Task<ProjectDto> CreateAsync(CreateProjectDto dto, CancellationToken ct = default);
    Task<ProjectDto> UpdateAsync(UpdateProjectDto dto, CancellationToken ct = default);
    Task<ProjectDto> UpdateStatusAsync(int id, ProjectStatus newStatus, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
```

- [ ] **Step 8.6: Implement ProjectService**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Services/Implementations/ProjectService.cs`:
```csharp
using ConstructionFinance.Application.Common.Exceptions;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Mapping;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using FluentValidation;

namespace ConstructionFinance.Application.Services.Implementations;

public class ProjectService : IProjectService
{
    private static readonly Dictionary<ProjectStatus, ProjectStatus[]> AllowedTransitions = new()
    {
        [ProjectStatus.Planning] = new[] { ProjectStatus.InProgress, ProjectStatus.Cancelled },
        [ProjectStatus.InProgress] = new[] { ProjectStatus.OnHold, ProjectStatus.Completed, ProjectStatus.Cancelled },
        [ProjectStatus.OnHold] = new[] { ProjectStatus.InProgress },
        [ProjectStatus.Completed] = Array.Empty<ProjectStatus>(),
        [ProjectStatus.Cancelled] = Array.Empty<ProjectStatus>()
    };

    private readonly IRepository<Project> _projects;
    private readonly IRepository<Contract> _contracts;
    private readonly IRepository<CostItem> _costItems;
    private readonly IUnitOfWork _uow;
    private readonly IValidator<Project> _validator;

    public ProjectService(
        IRepository<Project> projects,
        IRepository<Contract> contracts,
        IRepository<CostItem> costItems,
        IUnitOfWork uow,
        IValidator<Project> validator)
    {
        _projects = projects;
        _contracts = contracts;
        _costItems = costItems;
        _uow = uow;
        _validator = validator;
    }

    public async Task<ProjectDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var e = await _projects.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Project), id);
        return e.ToDto();
    }

    public async Task<IReadOnlyList<ProjectDto>> GetByContractAsync(int contractId, CancellationToken ct = default)
    {
        var list = await _projects.FindAsync(p => p.ContractId == contractId, ct);
        return list.Select(x => x.ToDto()).ToList();
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectDto dto, CancellationToken ct = default)
    {
        if (await _contracts.GetByIdAsync(dto.ContractId, ct) is null)
            throw new NotFoundException(nameof(Contract), dto.ContractId);

        var entity = dto.ToEntity();
        await _validator.ValidateAndThrowAsync(entity, ct);
        await _projects.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<ProjectDto> UpdateAsync(UpdateProjectDto dto, CancellationToken ct = default)
    {
        var entity = await _projects.GetByIdAsync(dto.Id, ct) ?? throw new NotFoundException(nameof(Project), dto.Id);
        dto.Apply(entity);
        await _validator.ValidateAndThrowAsync(entity, ct);
        _projects.Update(entity);
        await _uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<ProjectDto> UpdateStatusAsync(int id, ProjectStatus newStatus, CancellationToken ct = default)
    {
        var entity = await _projects.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Project), id);
        if (entity.Status == newStatus)
            return entity.ToDto();
        if (!AllowedTransitions[entity.Status].Contains(newStatus))
            throw new BusinessRuleException($"Không thể chuyển trạng thái từ {entity.Status} sang {newStatus}.");

        entity.Status = newStatus;
        _projects.Update(entity);
        await _uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _projects.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Project), id);
        if (await _costItems.ExistsAsync(c => c.ProjectId == id, ct))
            throw new BusinessRuleException("Không thể xóa Dự án khi còn Đầu mục chi phí. Hãy xóa cây chi phí trước.");
        await _projects.SoftDeleteAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 8.7: Verify + commit**

```bash
dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~ProjectServiceTests"
git add . && git commit -m "feat(app): ProjectService with status state machine + delete guard"
```

---

## Task 9: PaymentService (auto payment_no, % calc, summary)

**Files:**
- Create: `src/ConstructionFinance.Application/DTOs/PaymentDtos.cs`
- Create: `src/ConstructionFinance.Application/Mapping/PaymentMappings.cs`
- Create: `src/ConstructionFinance.Application/Services/Interfaces/IPaymentService.cs`
- Create: `src/ConstructionFinance.Application/Services/Implementations/PaymentService.cs`
- Test: `tests/ConstructionFinance.Application.Tests/PaymentServiceTests.cs`

- [ ] **Step 9.1: Write failing tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Application.Tests/PaymentServiceTests.cs`:
```csharp
using ConstructionFinance.Application.Common.Exceptions;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Data;
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using ConstructionFinance.Domain.Validators;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class PaymentServiceTests
{
    private static IPaymentService CreateService(AppDbContext ctx) =>
        new PaymentService(
            new Repository<Payment>(ctx),
            new Repository<CostItem>(ctx),
            new UnitOfWork(ctx),
            new PaymentValidator());

    private static async Task<int> SeedCostItem(AppDbContext ctx, decimal estimated = 3_000_000_000m)
    {
        var d = new ApprovalDecision { DecisionCode = "D1", DecisionTitle = "X", IssuingAuthority = "U", DecisionDate = new DateOnly(2026, 1, 1), TotalApprovedAmount = 1m };
        ctx.ApprovalDecisions.Add(d); await ctx.SaveChangesAsync();
        var c = new Contract { ApprovalDecisionId = d.Id, ContractCode = "C1", ContractName = "C", ContractDate = new DateOnly(2026, 1, 1), ContractValue = 1m };
        ctx.Contracts.Add(c); await ctx.SaveChangesAsync();
        var p = new Project { ContractId = c.Id, ProjectCode = "P1", ProjectName = "P", Status = ProjectStatus.InProgress };
        ctx.Projects.Add(p); await ctx.SaveChangesAsync();
        var ci = new CostItem { ProjectId = p.Id, ItemName = "Móng", Path = "/", Level = 0, IsLeaf = true, EstimatedAmount = estimated };
        ctx.CostItems.Add(ci); await ctx.SaveChangesAsync();
        return ci.Id;
    }

    [Fact]
    public async Task AddAsync_auto_assigns_payment_no_sequentially()
    {
        using var h = new ServiceTestHarness();
        var ciId = await SeedCostItem(h.Context);
        var svc = CreateService(h.Context);

        var p1 = await svc.AddAsync(new CreatePaymentDto(ciId, PaymentType.Advance, new DateOnly(2026, 4, 10), 900_000_000m, null, "HD-1", null, null, null));
        var p2 = await svc.AddAsync(new CreatePaymentDto(ciId, PaymentType.Installment, new DateOnly(2026, 5, 10), 600_000_000m, null, "HD-2", null, null, null));

        p1.PaymentNo.ShouldBe(1);
        p2.PaymentNo.ShouldBe(2);
    }

    [Fact]
    public async Task AddAsync_auto_computes_percentage_from_estimated()
    {
        using var h = new ServiceTestHarness();
        var ciId = await SeedCostItem(h.Context, estimated: 3_000_000_000m);
        var svc = CreateService(h.Context);

        var p = await svc.AddAsync(new CreatePaymentDto(ciId, PaymentType.Advance, new DateOnly(2026, 4, 10), 900_000_000m, null, null, null, null, null));

        p.Percentage.ShouldBe(30m); // 900M / 3000M * 100
    }

    [Fact]
    public async Task AddAsync_throws_NotFound_when_cost_item_missing()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);

        await Should.ThrowAsync<NotFoundException>(async () =>
            await svc.AddAsync(new CreatePaymentDto(999, PaymentType.Advance, new DateOnly(2026, 4, 10), 1m, null, null, null, null, null)));
    }

    [Fact]
    public async Task GetSummaryAsync_computes_totals_and_completion()
    {
        using var h = new ServiceTestHarness();
        var ciId = await SeedCostItem(h.Context, estimated: 3_000_000_000m);
        var svc = CreateService(h.Context);
        await svc.AddAsync(new CreatePaymentDto(ciId, PaymentType.Advance, new DateOnly(2026, 4, 10), 900_000_000m, null, null, null, null, null));
        await svc.AddAsync(new CreatePaymentDto(ciId, PaymentType.Installment, new DateOnly(2026, 5, 10), 600_000_000m, null, null, null, null, null));

        var summary = await svc.GetSummaryAsync(ciId);

        summary.EstimatedAmount.ShouldBe(3_000_000_000m);
        summary.TotalPaid.ShouldBe(1_500_000_000m);
        summary.Remaining.ShouldBe(1_500_000_000m);
        summary.CompletionPct.ShouldBe(50m);
    }
}
```

- [ ] **Step 9.2: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~PaymentServiceTests"`
Expected: Types missing.

- [ ] **Step 9.3: Implement PaymentDtos**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/DTOs/PaymentDtos.cs`:
```csharp
using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Application.DTOs;

public record PaymentDto(
    int Id,
    int CostItemId,
    int PaymentNo,
    PaymentType PaymentType,
    DateOnly PaymentDate,
    decimal Amount,
    decimal Percentage,
    string? InvoiceNo,
    DateOnly? InvoiceDate,
    string? Description,
    string? Notes);

// Percentage nullable: if null, service auto-computes from cost item estimated amount.
public record CreatePaymentDto(
    int CostItemId,
    PaymentType PaymentType,
    DateOnly PaymentDate,
    decimal Amount,
    decimal? Percentage,
    string? InvoiceNo,
    DateOnly? InvoiceDate,
    string? Description,
    string? Notes);

public record UpdatePaymentDto(
    int Id,
    PaymentType PaymentType,
    DateOnly PaymentDate,
    decimal Amount,
    decimal? Percentage,
    string? InvoiceNo,
    DateOnly? InvoiceDate,
    string? Description,
    string? Notes);

public record PaymentSummaryDto(
    int CostItemId,
    decimal EstimatedAmount,
    decimal TotalPaid,
    decimal Remaining,
    decimal CompletionPct);
```

- [ ] **Step 9.4: Implement PaymentMappings**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Mapping/PaymentMappings.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Application.Mapping;

public static class PaymentMappings
{
    public static PaymentDto ToDto(this Payment e) =>
        new(e.Id, e.CostItemId, e.PaymentNo, e.PaymentType, e.PaymentDate, e.Amount, e.Percentage, e.InvoiceNo, e.InvoiceDate, e.Description, e.Notes);
}
```

- [ ] **Step 9.5: Implement IPaymentService**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Services/Interfaces/IPaymentService.cs`:
```csharp
using ConstructionFinance.Application.DTOs;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IPaymentService
{
    Task<IReadOnlyList<PaymentDto>> GetByCostItemAsync(int costItemId, CancellationToken ct = default);
    Task<PaymentDto> AddAsync(CreatePaymentDto dto, CancellationToken ct = default);
    Task<PaymentDto> UpdateAsync(UpdatePaymentDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<PaymentSummaryDto> GetSummaryAsync(int costItemId, CancellationToken ct = default);
}
```

- [ ] **Step 9.6: Implement PaymentService**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Services/Implementations/PaymentService.cs`:
```csharp
using ConstructionFinance.Application.Common.Exceptions;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Mapping;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Entities;
using FluentValidation;

namespace ConstructionFinance.Application.Services.Implementations;

public class PaymentService : IPaymentService
{
    private readonly IRepository<Payment> _payments;
    private readonly IRepository<CostItem> _costItems;
    private readonly IUnitOfWork _uow;
    private readonly IValidator<Payment> _validator;

    public PaymentService(
        IRepository<Payment> payments,
        IRepository<CostItem> costItems,
        IUnitOfWork uow,
        IValidator<Payment> validator)
    {
        _payments = payments;
        _costItems = costItems;
        _uow = uow;
        _validator = validator;
    }

    public async Task<IReadOnlyList<PaymentDto>> GetByCostItemAsync(int costItemId, CancellationToken ct = default)
    {
        var list = await _payments.FindAsync(p => p.CostItemId == costItemId, ct);
        return list.OrderBy(p => p.PaymentNo).Select(p => p.ToDto()).ToList();
    }

    public async Task<PaymentDto> AddAsync(CreatePaymentDto dto, CancellationToken ct = default)
    {
        var costItem = await _costItems.GetByIdAsync(dto.CostItemId, ct)
            ?? throw new NotFoundException(nameof(CostItem), dto.CostItemId);

        var existing = await _payments.FindAsync(p => p.CostItemId == dto.CostItemId, ct);
        var nextNo = existing.Count == 0 ? 1 : existing.Max(p => p.PaymentNo) + 1;

        var percentage = dto.Percentage ?? ComputePercentage(dto.Amount, costItem.EstimatedAmount);

        var entity = new Payment
        {
            CostItemId = dto.CostItemId,
            PaymentNo = nextNo,
            PaymentType = dto.PaymentType,
            PaymentDate = dto.PaymentDate,
            Amount = dto.Amount,
            Percentage = percentage,
            InvoiceNo = dto.InvoiceNo,
            InvoiceDate = dto.InvoiceDate,
            Description = dto.Description,
            Notes = dto.Notes
        };

        await _validator.ValidateAndThrowAsync(entity, ct);
        await _payments.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<PaymentDto> UpdateAsync(UpdatePaymentDto dto, CancellationToken ct = default)
    {
        var entity = await _payments.GetByIdAsync(dto.Id, ct) ?? throw new NotFoundException(nameof(Payment), dto.Id);
        var costItem = await _costItems.GetByIdAsync(entity.CostItemId, ct)
            ?? throw new NotFoundException(nameof(CostItem), entity.CostItemId);

        entity.PaymentType = dto.PaymentType;
        entity.PaymentDate = dto.PaymentDate;
        entity.Amount = dto.Amount;
        entity.Percentage = dto.Percentage ?? ComputePercentage(dto.Amount, costItem.EstimatedAmount);
        entity.InvoiceNo = dto.InvoiceNo;
        entity.InvoiceDate = dto.InvoiceDate;
        entity.Description = dto.Description;
        entity.Notes = dto.Notes;

        await _validator.ValidateAndThrowAsync(entity, ct);
        _payments.Update(entity);
        await _uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _payments.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Payment), id);
        _payments.HardDelete(entity); // Payment has no soft delete
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<PaymentSummaryDto> GetSummaryAsync(int costItemId, CancellationToken ct = default)
    {
        var costItem = await _costItems.GetByIdAsync(costItemId, ct)
            ?? throw new NotFoundException(nameof(CostItem), costItemId);
        var payments = await _payments.FindAsync(p => p.CostItemId == costItemId, ct);
        var totalPaid = payments.Sum(p => p.Amount);
        var remaining = costItem.EstimatedAmount - totalPaid;
        var pct = ComputePercentage(totalPaid, costItem.EstimatedAmount);
        return new PaymentSummaryDto(costItemId, costItem.EstimatedAmount, totalPaid, remaining, pct);
    }

    private static decimal ComputePercentage(decimal amount, decimal estimated) =>
        estimated > 0 ? Math.Round(amount * 100m / estimated, 2) : 0m;
}
```

- [ ] **Step 9.7: Verify + commit**

```bash
dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~PaymentServiceTests"
git add . && git commit -m "feat(app): PaymentService with auto payment_no, percentage calc, summary"
```

---

## Task 10: CostService core (tree read + create + update + rollup)

**Files:**
- Create: `src/ConstructionFinance.Application/DTOs/CostItemDtos.cs`
- Create: `src/ConstructionFinance.Application/Mapping/CostItemMappings.cs`
- Create: `src/ConstructionFinance.Application/Services/Interfaces/ICostService.cs`
- Create: `src/ConstructionFinance.Application/Services/Implementations/CostService.cs`
- Test: `tests/ConstructionFinance.Application.Tests/CostServiceTests.cs`

Rollup rule (design doc 3.3.1): leaf `EstimatedAmount = Quantity*UnitPrice` when both > 0 (else keep manual value); non-leaf `EstimatedAmount = SUM(children)`. `RecalculateRollupAsync` processes nodes deepest-first.

- [ ] **Step 10.1: Write failing tests (core + rollup)**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Application.Tests/CostServiceTests.cs`:
```csharp
using ConstructionFinance.Application.Common.Exceptions;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Data;
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using ConstructionFinance.Domain.Validators;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class CostServiceTests
{
    private static ICostService CreateService(AppDbContext ctx) =>
        new CostService(
            new CostRepository(ctx),
            new Repository<Payment>(ctx),
            new UnitOfWork(ctx),
            new CostItemValidator());

    private static async Task<int> SeedProject(AppDbContext ctx)
    {
        var d = new ApprovalDecision { DecisionCode = "D1", DecisionTitle = "X", IssuingAuthority = "U", DecisionDate = new DateOnly(2026, 1, 1), TotalApprovedAmount = 1m };
        ctx.ApprovalDecisions.Add(d); await ctx.SaveChangesAsync();
        var c = new Contract { ApprovalDecisionId = d.Id, ContractCode = "C1", ContractName = "C", ContractDate = new DateOnly(2026, 1, 1), ContractValue = 1m };
        ctx.Contracts.Add(c); await ctx.SaveChangesAsync();
        var p = new Project { ContractId = c.Id, ProjectCode = "P1", ProjectName = "P", Status = ProjectStatus.InProgress };
        ctx.Projects.Add(p); await ctx.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task CreateNodeAsync_root_sets_path_level()
    {
        using var h = new ServiceTestHarness();
        var projectId = await SeedProject(h.Context);
        var svc = CreateService(h.Context);

        var root = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, null, null, "Chi phí xây dựng", false, null, 0, 0, 0, null, null));

        root.Level.ShouldBe(0);
        root.Path.ShouldBe($"/{root.Id}/");
        root.ParentId.ShouldBeNull();
    }

    [Fact]
    public async Task CreateNodeAsync_leaf_computes_estimated_from_qty_price()
    {
        using var h = new ServiceTestHarness();
        var projectId = await SeedProject(h.Context);
        var svc = CreateService(h.Context);
        var root = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, null, null, "R", false, null, 0, 0, 0, null, null));

        var leaf = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, root.Id, null, "Móng", true, null, 120m, 25_000_000m, 0, null, null));

        leaf.EstimatedAmount.ShouldBe(3_000_000_000m); // 120 * 25,000,000
    }

    [Fact]
    public async Task UpdateNodeAsync_recomputes_leaf_estimated()
    {
        using var h = new ServiceTestHarness();
        var projectId = await SeedProject(h.Context);
        var svc = CreateService(h.Context);
        var leaf = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, null, null, "Móng", true, null, 10m, 1_000m, 0, null, null));

        var updated = await svc.UpdateNodeAsync(new UpdateCostItemDto(leaf.Id, null, null, "Móng", true, null, 20m, 1_000m, 0, null, null));

        updated.EstimatedAmount.ShouldBe(20_000m);
    }

    [Fact]
    public async Task RecalculateRollupAsync_sums_children_into_parents()
    {
        using var h = new ServiceTestHarness();
        var projectId = await SeedProject(h.Context);
        var svc = CreateService(h.Context);
        var root = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, null, null, "Xây dựng", false, null, 0, 0, 0, null, null));
        var phanTho = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, root.Id, null, "Phần thô", false, null, 0, 0, 0, null, null));
        await svc.CreateNodeAsync(new CreateCostItemDto(projectId, phanTho.Id, null, "Móng", true, null, 0, 0, 3_000_000_000m, null, null));
        await svc.CreateNodeAsync(new CreateCostItemDto(projectId, phanTho.Id, null, "Cột", true, null, 0, 0, 2_400_000_000m, null, null));

        await svc.RecalculateRollupAsync(projectId);

        var tree = await svc.GetProjectTreeAsync(projectId);
        tree.Single(x => x.ItemName == "Phần thô").EstimatedAmount.ShouldBe(5_400_000_000m);
        tree.Single(x => x.ItemName == "Xây dựng").EstimatedAmount.ShouldBe(5_400_000_000m);
    }

    [Fact]
    public async Task GetProjectTreeAsync_returns_nodes_ordered_by_path()
    {
        using var h = new ServiceTestHarness();
        var projectId = await SeedProject(h.Context);
        var svc = CreateService(h.Context);
        var root = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, null, null, "R", false, null, 0, 0, 0, null, null));
        await svc.CreateNodeAsync(new CreateCostItemDto(projectId, root.Id, null, "A", true, null, 0, 0, 0, null, null));

        var tree = await svc.GetProjectTreeAsync(projectId);

        tree.Count.ShouldBe(2);
        tree[0].ItemName.ShouldBe("R");
    }
}
```

- [ ] **Step 10.2: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~CostServiceTests"`
Expected: Types missing.

- [ ] **Step 10.3: Implement CostItemDtos**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/DTOs/CostItemDtos.cs`:
```csharp
namespace ConstructionFinance.Application.DTOs;

public record CostItemDto(
    int Id,
    int ProjectId,
    int? ParentId,
    int? UnitTypeId,
    int? VendorId,
    string? ItemCode,
    string ItemName,
    int Level,
    string Path,
    int SortOrder,
    bool IsLeaf,
    decimal Quantity,
    decimal UnitPrice,
    decimal EstimatedAmount,
    string? BidPackage,
    string? Notes);

public record CreateCostItemDto(
    int ProjectId,
    int? ParentId,
    int? UnitTypeId,
    string ItemName,
    bool IsLeaf,
    int? VendorId,
    decimal Quantity,
    decimal UnitPrice,
    decimal EstimatedAmount,
    string? BidPackage,
    string? Notes);

public record UpdateCostItemDto(
    int Id,
    int? UnitTypeId,
    int? VendorId,
    string ItemName,
    bool IsLeaf,
    string? BidPackage,
    decimal Quantity,
    decimal UnitPrice,
    decimal EstimatedAmount,
    int SortOrder,
    string? Notes);
```

- [ ] **Step 10.4: Implement CostItemMappings**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Mapping/CostItemMappings.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Application.Mapping;

public static class CostItemMappings
{
    public static CostItemDto ToDto(this CostItem e) =>
        new(e.Id, e.ProjectId, e.ParentId, e.UnitTypeId, e.VendorId, e.ItemCode, e.ItemName, e.Level, e.Path, e.SortOrder, e.IsLeaf, e.Quantity, e.UnitPrice, e.EstimatedAmount, e.BidPackage, e.Notes);
}
```

- [ ] **Step 10.5: Implement ICostService**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Services/Interfaces/ICostService.cs`:
```csharp
using ConstructionFinance.Application.DTOs;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface ICostService
{
    Task<IReadOnlyList<CostItemDto>> GetProjectTreeAsync(int projectId, CancellationToken ct = default);
    Task<CostItemDto> CreateNodeAsync(CreateCostItemDto dto, CancellationToken ct = default);
    Task<CostItemDto> UpdateNodeAsync(UpdateCostItemDto dto, CancellationToken ct = default);
    Task RecalculateRollupAsync(int projectId, CancellationToken ct = default);
    Task MoveNodeAsync(int nodeId, int? newParentId, CancellationToken ct = default);
    Task DeleteNodeAsync(int nodeId, CancellationToken ct = default);
}
```

- [ ] **Step 10.6: Implement CostService (core methods; Move/Delete added in Task 11)**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Services/Implementations/CostService.cs`:
```csharp
using ConstructionFinance.Application.Common.Exceptions;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Mapping;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Entities;
using FluentValidation;

namespace ConstructionFinance.Application.Services.Implementations;

public class CostService : ICostService
{
    private readonly ICostRepository _costs;
    private readonly IRepository<Payment> _payments;
    private readonly IUnitOfWork _uow;
    private readonly IValidator<CostItem> _validator;

    public CostService(
        ICostRepository costs,
        IRepository<Payment> payments,
        IUnitOfWork uow,
        IValidator<CostItem> validator)
    {
        _costs = costs;
        _payments = payments;
        _uow = uow;
        _validator = validator;
    }

    public async Task<IReadOnlyList<CostItemDto>> GetProjectTreeAsync(int projectId, CancellationToken ct = default)
    {
        var list = await _costs.GetTreeByProjectAsync(projectId, ct);
        return list.Select(x => x.ToDto()).ToList();
    }

    public async Task<CostItemDto> CreateNodeAsync(CreateCostItemDto dto, CancellationToken ct = default)
    {
        var node = new CostItem
        {
            ProjectId = dto.ProjectId,
            ItemName = dto.ItemName,
            IsLeaf = dto.IsLeaf,
            UnitTypeId = dto.UnitTypeId,
            VendorId = dto.VendorId,
            Quantity = dto.Quantity,
            UnitPrice = dto.UnitPrice,
            EstimatedAmount = ComputeLeafEstimate(dto.IsLeaf, dto.Quantity, dto.UnitPrice, dto.EstimatedAmount),
            BidPackage = dto.BidPackage,
            Notes = dto.Notes,
            Path = "/",   // placeholder valid for validator; AddNodeAsync overwrites with real path
            Level = 0
        };

        // Validate BEFORE persisting. Path/Level here are placeholders that satisfy the
        // validator (Path NotEmpty, Level >= 0); AddNodeAsync overwrites them with the
        // real materialized path/level after the Id is assigned.
        await _validator.ValidateAndThrowAsync(node, ct);

        var saved = await _costs.AddNodeAsync(dto.ProjectId, dto.ParentId, node, ct);
        await _uow.SaveChangesAsync(ct); // persist the final path set by AddNodeAsync
        return saved.ToDto();
    }

    public async Task<CostItemDto> UpdateNodeAsync(UpdateCostItemDto dto, CancellationToken ct = default)
    {
        var entity = await _costs.GetByIdAsync(dto.Id, ct) ?? throw new NotFoundException(nameof(CostItem), dto.Id);

        entity.ItemName = dto.ItemName;
        entity.IsLeaf = dto.IsLeaf;
        entity.UnitTypeId = dto.UnitTypeId;
        entity.VendorId = dto.VendorId;
        entity.Quantity = dto.Quantity;
        entity.UnitPrice = dto.UnitPrice;
        entity.SortOrder = dto.SortOrder;
        entity.BidPackage = dto.BidPackage;
        entity.Notes = dto.Notes;
        entity.EstimatedAmount = ComputeLeafEstimate(dto.IsLeaf, dto.Quantity, dto.UnitPrice, dto.EstimatedAmount);

        await _validator.ValidateAndThrowAsync(entity, ct);
        _costs.Update(entity);
        await _uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task RecalculateRollupAsync(int projectId, CancellationToken ct = default)
    {
        var all = (await _costs.GetTreeByProjectAsync(projectId, ct)).ToList();
        var byId = all.ToDictionary(x => x.Id);
        // Sum of children's amount keyed by parent id.
        var childSums = new Dictionary<int, decimal>();

        // Process deepest first so child sums are ready when we reach the parent.
        foreach (var node in all.OrderByDescending(x => x.Level))
        {
            decimal amount;
            if (node.IsLeaf)
            {
                amount = ComputeLeafEstimate(true, node.Quantity, node.UnitPrice, node.EstimatedAmount);
            }
            else
            {
                amount = childSums.TryGetValue(node.Id, out var s) ? s : 0m;
            }

            if (node.EstimatedAmount != amount)
            {
                node.EstimatedAmount = amount;
                _costs.Update(node);
            }

            if (node.ParentId is int parentId)
            {
                childSums[parentId] = (childSums.TryGetValue(parentId, out var existing) ? existing : 0m) + amount;
            }
        }

        await _uow.SaveChangesAsync(ct);
    }

    // Move/Delete implemented in Task 11.
    public Task MoveNodeAsync(int nodeId, int? newParentId, CancellationToken ct = default) =>
        throw new NotImplementedException("Implemented in Task 11");

    public Task DeleteNodeAsync(int nodeId, CancellationToken ct = default) =>
        throw new NotImplementedException("Implemented in Task 11");

    private static decimal ComputeLeafEstimate(bool isLeaf, decimal qty, decimal price, decimal manual)
    {
        if (isLeaf && qty > 0 && price > 0)
            return qty * price;
        return manual;
    }
}
```

> Note: `GetTreeByProjectAsync` returns AsNoTracking entities, but `RecalculateRollupAsync` needs to update them. To keep this simple and correct, change the rollup to load tracked entities. See Step 10.7.

- [ ] **Step 10.7: Add a tracked tree fetch for rollup**

The repository's `GetTreeByProjectAsync` uses `AsNoTracking`, so `_costs.Update(node)` on those instances won't reflect original values correctly for change tracking. Add a tracked query method.

Add to `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Abstractions/ICostRepository.cs` (new method in the interface):
```csharp
    Task<IReadOnlyList<CostItem>> GetTrackedTreeByProjectAsync(int projectId, CancellationToken ct = default);
```

Add the implementation to `/home/hieu-nguyen/management/src/ConstructionFinance.Data/Repositories/CostRepository.cs` (new method in the class, after `GetTreeByProjectAsync`):
```csharp
    public async Task<IReadOnlyList<CostItem>> GetTrackedTreeByProjectAsync(int projectId, CancellationToken ct = default) =>
        await DbSet
            .Where(c => c.ProjectId == projectId)
            .OrderBy(c => c.Path)
            .ThenBy(c => c.SortOrder)
            .ToListAsync(ct);
```

Then in `CostService.RecalculateRollupAsync`, change the first line from:
```csharp
        var all = (await _costs.GetTreeByProjectAsync(projectId, ct)).ToList();
```
to:
```csharp
        var all = (await _costs.GetTrackedTreeByProjectAsync(projectId, ct)).ToList();
```

- [ ] **Step 10.8: Run core tests**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~CostServiceTests"`
Expected: All 5 tests pass.

- [ ] **Step 10.9: Commit**

```bash
git add .
git commit -m "feat(app): CostService core (tree read, create, update, rollup)"
```

---

## Task 11: CostService move + delete (subtree path rebuild, payment guard)

**Files:**
- Modify: `src/ConstructionFinance.Application/Services/Implementations/CostService.cs`
- Test: `tests/ConstructionFinance.Application.Tests/CostServiceMoveDeleteTests.cs`

- [ ] **Step 11.1: Write failing tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Application.Tests/CostServiceMoveDeleteTests.cs`:
```csharp
using ConstructionFinance.Application.Common.Exceptions;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Data;
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using ConstructionFinance.Domain.Validators;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class CostServiceMoveDeleteTests
{
    private static ICostService CreateService(AppDbContext ctx) =>
        new CostService(
            new CostRepository(ctx),
            new Repository<Payment>(ctx),
            new UnitOfWork(ctx),
            new CostItemValidator());

    private static async Task<int> SeedProject(AppDbContext ctx)
    {
        var d = new ApprovalDecision { DecisionCode = "D1", DecisionTitle = "X", IssuingAuthority = "U", DecisionDate = new DateOnly(2026, 1, 1), TotalApprovedAmount = 1m };
        ctx.ApprovalDecisions.Add(d); await ctx.SaveChangesAsync();
        var c = new Contract { ApprovalDecisionId = d.Id, ContractCode = "C1", ContractName = "C", ContractDate = new DateOnly(2026, 1, 1), ContractValue = 1m };
        ctx.Contracts.Add(c); await ctx.SaveChangesAsync();
        var p = new Project { ContractId = c.Id, ProjectCode = "P1", ProjectName = "P", Status = ProjectStatus.InProgress };
        ctx.Projects.Add(p); await ctx.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task MoveNodeAsync_reparents_and_rebuilds_subtree_paths()
    {
        using var h = new ServiceTestHarness();
        var projectId = await SeedProject(h.Context);
        var svc = CreateService(h.Context);
        var r1 = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, null, null, "R1", false, null, 0, 0, 0, null, null));
        var r2 = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, null, null, "R2", false, null, 0, 0, 0, null, null));
        var child = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, r1.Id, null, "Child", false, null, 0, 0, 0, null, null));
        var grandchild = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, child.Id, null, "GC", true, null, 0, 0, 0, null, null));

        await svc.MoveNodeAsync(child.Id, r2.Id);

        var tree = await svc.GetProjectTreeAsync(projectId);
        var movedChild = tree.Single(x => x.ItemName == "Child");
        var movedGc = tree.Single(x => x.ItemName == "GC");
        movedChild.ParentId.ShouldBe(r2.Id);
        movedChild.Path.ShouldBe($"/{r2.Id}/{child.Id}/");
        movedChild.Level.ShouldBe(1);
        movedGc.Path.ShouldBe($"/{r2.Id}/{child.Id}/{grandchild.Id}/");
        movedGc.Level.ShouldBe(2);
    }

    [Fact]
    public async Task MoveNodeAsync_into_own_subtree_is_blocked()
    {
        using var h = new ServiceTestHarness();
        var projectId = await SeedProject(h.Context);
        var svc = CreateService(h.Context);
        var root = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, null, null, "Root", false, null, 0, 0, 0, null, null));
        var child = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, root.Id, null, "Child", false, null, 0, 0, 0, null, null));

        await Should.ThrowAsync<BusinessRuleException>(async () => await svc.MoveNodeAsync(root.Id, child.Id));
    }

    [Fact]
    public async Task DeleteNodeAsync_soft_deletes_whole_subtree()
    {
        using var h = new ServiceTestHarness();
        var projectId = await SeedProject(h.Context);
        var svc = CreateService(h.Context);
        var root = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, null, null, "Root", false, null, 0, 0, 0, null, null));
        await svc.CreateNodeAsync(new CreateCostItemDto(projectId, root.Id, null, "Child", true, null, 0, 0, 0, null, null));

        await svc.DeleteNodeAsync(root.Id);

        (await svc.GetProjectTreeAsync(projectId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteNodeAsync_blocked_when_subtree_has_payments()
    {
        using var h = new ServiceTestHarness();
        var projectId = await SeedProject(h.Context);
        var svc = CreateService(h.Context);
        var root = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, null, null, "Root", false, null, 0, 0, 0, null, null));
        var leaf = await svc.CreateNodeAsync(new CreateCostItemDto(projectId, root.Id, null, "Leaf", true, null, 0, 0, 1_000m, null, null));
        h.Context.Payments.Add(new Payment { CostItemId = leaf.Id, PaymentNo = 1, PaymentType = PaymentType.Advance, PaymentDate = new DateOnly(2026, 5, 1), Amount = 100m, Percentage = 10m });
        await h.Context.SaveChangesAsync();

        await Should.ThrowAsync<BusinessRuleException>(async () => await svc.DeleteNodeAsync(root.Id));
    }
}
```

- [ ] **Step 11.2: Run — fails (NotImplementedException)**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~CostServiceMoveDeleteTests"`
Expected: FAIL (NotImplementedException).

- [ ] **Step 11.3: Replace MoveNodeAsync + DeleteNodeAsync stubs with real implementation**

In `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Services/Implementations/CostService.cs`, replace the two stub methods:
```csharp
    // Move/Delete implemented in Task 11.
    public Task MoveNodeAsync(int nodeId, int? newParentId, CancellationToken ct = default) =>
        throw new NotImplementedException("Implemented in Task 11");

    public Task DeleteNodeAsync(int nodeId, CancellationToken ct = default) =>
        throw new NotImplementedException("Implemented in Task 11");
```
with:
```csharp
    public async Task MoveNodeAsync(int nodeId, int? newParentId, CancellationToken ct = default)
    {
        var node = await _costs.GetByIdAsync(nodeId, ct) ?? throw new NotFoundException(nameof(CostItem), nodeId);

        string newParentPath;
        int newParentLevel;
        if (newParentId is null)
        {
            newParentPath = "/";
            newParentLevel = -1;
        }
        else
        {
            var newParent = await _costs.GetByIdAsync(newParentId.Value, ct)
                ?? throw new NotFoundException(nameof(CostItem), newParentId.Value);
            // Cannot move a node into itself or one of its descendants.
            if (newParent.Path.StartsWith(node.Path, StringComparison.Ordinal))
                throw new BusinessRuleException("Không thể di chuyển một đầu mục vào chính nó hoặc nhánh con của nó.");
            newParentPath = newParent.Path;
            newParentLevel = newParent.Level;
        }

        var subtree = (await _costs.GetSubtreeAsync(nodeId, ct)).ToList();
        var oldPrefix = node.Path;
        var newPrefix = $"{newParentPath.TrimEnd('/')}/{node.Id}/";
        var levelDelta = (newParentLevel + 1) - node.Level;

        // Re-fetch tracked instances so updates persist.
        foreach (var descendant in subtree)
        {
            var tracked = await _costs.GetByIdAsync(descendant.Id, ct);
            if (tracked is null) continue;
            tracked.Path = newPrefix + tracked.Path.Substring(oldPrefix.Length);
            tracked.Level += levelDelta;
            if (tracked.Id == nodeId)
                tracked.ParentId = newParentId;
            _costs.Update(tracked);
        }

        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeleteNodeAsync(int nodeId, CancellationToken ct = default)
    {
        var node = await _costs.GetByIdAsync(nodeId, ct) ?? throw new NotFoundException(nameof(CostItem), nodeId);
        var subtree = (await _costs.GetSubtreeAsync(nodeId, ct)).ToList();
        var subtreeIds = subtree.Select(x => x.Id).ToHashSet();

        if (await _payments.ExistsAsync(p => subtreeIds.Contains(p.CostItemId), ct))
            throw new BusinessRuleException("Không thể xóa: có đầu mục trong nhánh này đã có đợt thanh toán. Hãy xóa các đợt thanh toán trước.");

        foreach (var item in subtree)
        {
            var tracked = await _costs.GetByIdAsync(item.Id, ct);
            if (tracked is not null)
                await _costs.SoftDeleteAsync(tracked, ct);
        }

        await _uow.SaveChangesAsync(ct);
    }
```

- [ ] **Step 11.4: Run move/delete tests**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~CostServiceMoveDeleteTests"`
Expected: All 4 tests pass.

- [ ] **Step 11.5: Re-run all CostService tests (no regression)**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~CostService"`
Expected: All pass (core + move/delete).

- [ ] **Step 11.6: Commit**

```bash
git add .
git commit -m "feat(app): CostService move (subtree path rebuild) + delete (payment guard)"
```

---

## Task 12: AuditService (query audit logs)

**Files:**
- Create: `src/ConstructionFinance.Application/DTOs/AuditLogDtos.cs`
- Create: `src/ConstructionFinance.Application/Mapping/AuditLogMappings.cs`
- Create: `src/ConstructionFinance.Application/Services/Interfaces/IAuditService.cs`
- Create: `src/ConstructionFinance.Application/Services/Implementations/AuditService.cs`
- Test: `tests/ConstructionFinance.Application.Tests/AuditServiceTests.cs`

- [ ] **Step 12.1: Write failing tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Application.Tests/AuditServiceTests.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Data;
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Entities;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class AuditServiceTests
{
    private static IAuditService CreateService(AppDbContext ctx) =>
        new AuditService(new Repository<AuditLog>(ctx));

    [Fact]
    public async Task GetForEntityAsync_returns_logs_of_that_entity_only()
    {
        using var h = new ServiceTestHarness();
        // Creating a vendor produces a Create audit log via interceptor.
        h.Context.Vendors.Add(new Vendor { VendorName = "X" });
        await h.Context.SaveChangesAsync();
        var vendorId = h.Context.Vendors.Single().Id;
        var svc = CreateService(h.Context);

        var logs = await svc.GetForEntityAsync(nameof(Vendor), vendorId);

        logs.ShouldNotBeEmpty();
        logs.ShouldAllBe(l => l.EntityType == nameof(Vendor) && l.EntityId == vendorId);
    }

    [Fact]
    public async Task QueryAsync_filters_by_entity_type()
    {
        using var h = new ServiceTestHarness();
        h.Context.Vendors.Add(new Vendor { VendorName = "X" });
        await h.Context.SaveChangesAsync();
        var svc = CreateService(h.Context);

        var logs = await svc.QueryAsync(new AuditLogFilter(EntityType: nameof(Vendor), FromUtc: null, ToUtc: null));

        logs.ShouldAllBe(l => l.EntityType == nameof(Vendor));
    }
}
```

- [ ] **Step 12.2: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~AuditServiceTests"`
Expected: Types missing.

- [ ] **Step 12.3: Implement AuditLogDtos**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/DTOs/AuditLogDtos.cs`:
```csharp
using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Application.DTOs;

public record AuditLogDto(
    int Id,
    string EntityType,
    int EntityId,
    AuditAction Action,
    string? FieldName,
    string? OldValue,
    string? NewValue,
    DateTime ChangedAt);

public record AuditLogFilter(
    string? EntityType,
    DateTime? FromUtc,
    DateTime? ToUtc);
```

- [ ] **Step 12.4: Implement AuditLogMappings**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Mapping/AuditLogMappings.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Application.Mapping;

public static class AuditLogMappings
{
    public static AuditLogDto ToDto(this AuditLog e) =>
        new(e.Id, e.EntityType, e.EntityId, e.Action, e.FieldName, e.OldValue, e.NewValue, e.ChangedAt);
}
```

- [ ] **Step 12.5: Implement IAuditService**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Services/Interfaces/IAuditService.cs`:
```csharp
using ConstructionFinance.Application.DTOs;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IAuditService
{
    Task<IReadOnlyList<AuditLogDto>> GetForEntityAsync(string entityType, int entityId, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLogDto>> QueryAsync(AuditLogFilter filter, CancellationToken ct = default);
}
```

- [ ] **Step 12.6: Implement AuditService**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Services/Implementations/AuditService.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Mapping;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Application.Services.Implementations;

public class AuditService : IAuditService
{
    private readonly IRepository<AuditLog> _logs;

    public AuditService(IRepository<AuditLog> logs) => _logs = logs;

    public async Task<IReadOnlyList<AuditLogDto>> GetForEntityAsync(string entityType, int entityId, CancellationToken ct = default)
    {
        var list = await _logs.FindAsync(l => l.EntityType == entityType && l.EntityId == entityId, ct);
        return list.OrderByDescending(l => l.ChangedAt).Select(l => l.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<AuditLogDto>> QueryAsync(AuditLogFilter filter, CancellationToken ct = default)
    {
        var list = await _logs.FindAsync(l =>
            (filter.EntityType == null || l.EntityType == filter.EntityType) &&
            (filter.FromUtc == null || l.ChangedAt >= filter.FromUtc) &&
            (filter.ToUtc == null || l.ChangedAt <= filter.ToUtc), ct);
        return list.OrderByDescending(l => l.ChangedAt).Select(l => l.ToDto()).ToList();
    }
}
```

- [ ] **Step 12.7: Verify + commit**

```bash
dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~AuditServiceTests"
git add . && git commit -m "feat(app): AuditService for querying audit logs"
```

---

## Task 13: DI registration extension + full-suite verification

**Files:**
- Create: `src/ConstructionFinance.Application/DependencyInjection.cs`
- Modify: `src/ConstructionFinance.Application/ConstructionFinance.Application.csproj` (add Microsoft.Extensions.DependencyInjection.Abstractions)
- Modify: `Directory.Packages.props` (add the abstractions package version)
- Modify: `src/ConstructionFinance.Data/ConstructionFinance.Data.csproj` (add DI abstractions for its own registration helper)
- Create: `src/ConstructionFinance.Data/DependencyInjection.cs`
- Test: `tests/ConstructionFinance.Application.Tests/DependencyInjectionTests.cs`

- [ ] **Step 13.1: Add DI abstractions package version to Directory.Packages.props**

Edit `/home/hieu-nguyen/management/Directory.Packages.props`. Add inside the `<ItemGroup>`:
```xml
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.2" />
```

- [ ] **Step 13.2: Reference DI abstractions in Application + Data csproj**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Application/ConstructionFinance.Application.csproj` — add to its package `<ItemGroup>`:
```xml
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
```

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Data/ConstructionFinance.Data.csproj` — add to its package `<ItemGroup>`:
```xml
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
```

- [ ] **Step 13.3: Write failing test for DI wiring**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Application.Tests/DependencyInjectionTests.cs`:
```csharp
using ConstructionFinance.Application;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplicationServices_resolves_all_services()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite("DataSource=:memory:"));
        services.AddDataServices();
        services.AddApplicationServices();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        sp.GetService<IVendorService>().ShouldNotBeNull();
        sp.GetService<IDecisionService>().ShouldNotBeNull();
        sp.GetService<IContractService>().ShouldNotBeNull();
        sp.GetService<IProjectService>().ShouldNotBeNull();
        sp.GetService<IPaymentService>().ShouldNotBeNull();
        sp.GetService<ICostService>().ShouldNotBeNull();
        sp.GetService<IAuditService>().ShouldNotBeNull();
    }
}
```

- [ ] **Step 13.4: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~DependencyInjectionTests"`
Expected: `AddDataServices`/`AddApplicationServices` not found.

- [ ] **Step 13.5: Implement Data DI registration**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Data/DependencyInjection.cs`:
```csharp
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructionFinance.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddDataServices(this IServiceCollection services)
    {
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<ICostRepository, CostRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}
```

- [ ] **Step 13.6: Implement Application DI registration**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/DependencyInjection.cs`:
```csharp
using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Validators;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructionFinance.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Validators
        services.AddScoped<IValidator<Vendor>, VendorValidator>();
        services.AddScoped<IValidator<ApprovalDecision>, ApprovalDecisionValidator>();
        services.AddScoped<IValidator<Contract>, ContractValidator>();
        services.AddScoped<IValidator<Project>, ProjectValidator>();
        services.AddScoped<IValidator<CostItem>, CostItemValidator>();
        services.AddScoped<IValidator<Payment>, PaymentValidator>();

        // Services
        services.AddScoped<IVendorService, VendorService>();
        services.AddScoped<IDecisionService, DecisionService>();
        services.AddScoped<IContractService, ContractService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ICostService, CostService>();
        services.AddScoped<IAuditService, AuditService>();

        return services;
    }
}
```

> Note: This requires Application to reference Domain.Validators (already in Domain) and FluentValidation (added Task 3). Data's `AddDataServices` registers repositories + UoW.

- [ ] **Step 13.7: Run DI test**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~DependencyInjectionTests"`
Expected: Pass — all 7 services resolve.

- [ ] **Step 13.8: Run the ENTIRE test suite (all 3 test projects)**

Run from `/home/hieu-nguyen/management/`:
```bash
dotnet test --configuration Release
```
Expected: All tests pass across Domain.Tests (71 from Plan 01 region), Data.Tests (+ new), Application.Tests (~35 new). 0 failures.

- [ ] **Step 13.9: Commit**

```bash
git add .
git commit -m "feat(app): DI registration for services, validators, repositories, UnitOfWork"
```

---

## Final verification

- [ ] **Step F.1: Clean build + full test**

Run:
```bash
cd /home/hieu-nguyen/management/
dotnet clean
dotnet build --configuration Release
dotnet test --configuration Release --verbosity minimal
```
Expected: 0 build errors. All tests pass.

- [ ] **Step F.2: Confirm architecture boundary — Application must NOT reference Data**

Run:
```bash
grep -n "ConstructionFinance.Data" src/ConstructionFinance.Application/*.csproj || echo "OK: Application does not reference Data"
```
Expected: "OK: Application does not reference Data" (Application depends only on Domain).

- [ ] **Step F.3: Confirm git log**

Run: `git log --oneline | head -16`
Expected: ~13 new commits for Plan 02 tasks on top of Plan 01.

---

## Deliverables

After this plan:

1. ✅ Repository abstractions live in `Domain/Abstractions` — Application depends only on Domain (Clean Architecture boundary verified)
2. ✅ `IUnitOfWork` controls transaction boundaries
3. ✅ `IRepository<T>` extended with `FindAsync`/`ExistsAsync`
4. ✅ AuditInterceptor Create EntityId bug fixed (real autoincrement Id captured)
5. ✅ 7 services: Vendor, Decision, Contract, Project, Payment, Cost (tree + rollup + move/delete), Audit
6. ✅ Business rules: delete guards (RESTRICT), project status state machine, payment auto-numbering + % calc, cost rollup, move-into-own-subtree prevention, delete-with-payments prevention
7. ✅ DTOs + hand-written mappings for all entities
8. ✅ Custom exceptions (NotFoundException, BusinessRuleException) + FluentValidation integration
9. ✅ DI registration (`AddDataServices`, `AddApplicationServices`)
10. ✅ ~35 integration-style service tests (real SQLite in-memory)

**What's NOT in this plan** (Plan 03+):
- Any Avalonia UI / ViewModels
- Attachment file storage service (Infrastructure)
- PDF/Excel export, Backup/Restore
- Cost tree Excel-like UI features (copy/paste, import)
- `app_settings` seeding / company info management
