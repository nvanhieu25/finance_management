# Plan 01: Foundation & Domain Layer

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tạo nền tảng cho Construction Finance Management app — solution structure, 13 entity domain, EF Core DbContext + migration, repositories, interceptors (audit + soft delete), seed data. Kết thúc plan này, có thể chạy test end-to-end: insert một cây Quyết định → Hợp đồng → Dự án → Đầu mục chi phí → Đợt thanh toán vào SQLite và query lại.

**Architecture:** Clean Architecture / Layered + repository pattern. 5 projects: Domain (entities + validators), Data (EF Core + repositories), Application (interfaces — stub trong plan này), Infrastructure (file storage, logging — stub), Desktop (Avalonia — stub, không build UI trong plan này). KHÔNG có UI nào trong plan này.

**Tech Stack:** .NET 8 (C# 12), EF Core 8 + SQLite, FluentValidation, Serilog, xUnit + Moq + Shouldly. Nullable reference types enabled, file-scoped namespaces.

**Reference:** [Design doc](../specs/2026-05-19-construction-finance-app-design.md) sections 2, 3, 4.

**Out of scope (will be in later plans):**
- Avalonia UI (Plan 2)
- Application Service implementations beyond stubs (Plan 2)
- Cost tree business logic (rollup, materialized path mgmt) — implemented as repository methods here, full service logic in Plan 3
- Backup/Restore (Plan 4)
- PDF/Excel export (Plan 2-4)

---

## File Structure

```
/home/hieu-nguyen/management/
├── ConstructionFinance.sln
├── Directory.Build.props                            (shared build config)
├── Directory.Packages.props                         (Central Package Management)
├── .gitignore
├── .editorconfig
├── global.json                                      (pin .NET 8 SDK)
├── src/
│   ├── ConstructionFinance.Domain/
│   │   ├── ConstructionFinance.Domain.csproj
│   │   ├── Common/
│   │   │   ├── BaseEntity.cs                        (Id, CreatedAt, UpdatedAt)
│   │   │   ├── SoftDeletableEntity.cs               (+ DeletedAt)
│   │   │   ├── IAuditable.cs                        (marker interface)
│   │   │   └── ISoftDeletable.cs
│   │   ├── Enums/
│   │   │   ├── ProjectStatus.cs
│   │   │   ├── PaymentType.cs
│   │   │   ├── AuditAction.cs
│   │   │   └── AttachmentEntityType.cs
│   │   ├── Entities/
│   │   │   ├── Vendor.cs
│   │   │   ├── CostUnitType.cs
│   │   │   ├── ApprovalDecision.cs
│   │   │   ├── Contract.cs
│   │   │   ├── Project.cs
│   │   │   ├── CostItem.cs
│   │   │   ├── Payment.cs
│   │   │   ├── Attachment.cs
│   │   │   ├── AuditLog.cs
│   │   │   ├── CostTemplate.cs
│   │   │   ├── CostTemplateItem.cs
│   │   │   └── AppSetting.cs
│   │   └── Validators/
│   │       ├── VendorValidator.cs
│   │       ├── ApprovalDecisionValidator.cs
│   │       ├── ContractValidator.cs
│   │       ├── ProjectValidator.cs
│   │       ├── CostItemValidator.cs
│   │       └── PaymentValidator.cs
│   ├── ConstructionFinance.Data/
│   │   ├── ConstructionFinance.Data.csproj
│   │   ├── AppDbContext.cs
│   │   ├── Configurations/
│   │   │   ├── VendorConfiguration.cs
│   │   │   ├── CostUnitTypeConfiguration.cs
│   │   │   ├── ApprovalDecisionConfiguration.cs
│   │   │   ├── ContractConfiguration.cs
│   │   │   ├── ProjectConfiguration.cs
│   │   │   ├── CostItemConfiguration.cs
│   │   │   ├── PaymentConfiguration.cs
│   │   │   ├── AttachmentConfiguration.cs
│   │   │   ├── AuditLogConfiguration.cs
│   │   │   ├── CostTemplateConfiguration.cs
│   │   │   ├── CostTemplateItemConfiguration.cs
│   │   │   └── AppSettingConfiguration.cs
│   │   ├── Interceptors/
│   │   │   ├── AuditInterceptor.cs
│   │   │   └── TimestampInterceptor.cs
│   │   ├── Repositories/
│   │   │   ├── IRepository.cs                       (generic interface)
│   │   │   ├── Repository.cs                        (generic implementation)
│   │   │   ├── ICostRepository.cs                   (tree-specific)
│   │   │   └── CostRepository.cs
│   │   ├── Seed/
│   │   │   └── CostUnitTypeSeeder.cs
│   │   └── Migrations/                              (auto-generated)
│   ├── ConstructionFinance.Application/
│   │   └── ConstructionFinance.Application.csproj   (stub, no code yet)
│   ├── ConstructionFinance.Infrastructure/
│   │   └── ConstructionFinance.Infrastructure.csproj (stub)
│   └── ConstructionFinance.Desktop/
│       └── ConstructionFinance.Desktop.csproj       (stub, no Avalonia yet)
├── tests/
│   ├── ConstructionFinance.Domain.Tests/
│   │   ├── ConstructionFinance.Domain.Tests.csproj
│   │   ├── Entities/                                (entity tests)
│   │   └── Validators/                              (validator tests)
│   └── ConstructionFinance.Data.Tests/
│       ├── ConstructionFinance.Data.Tests.csproj
│       ├── TestDbContextFactory.cs                  (in-memory + SQLite test factory)
│       ├── Repositories/
│       └── Integration/
│           └── EndToEndSmokeTests.cs
└── .github/workflows/
    └── ci.yml                                       (build + test on push)
```

---

## Pre-flight: Verify environment

- [ ] **Step 0.1: Check .NET 8 SDK availability**

Run: `dotnet --version`
Expected: Output starts with `8.` (e.g., `8.0.408`).

If NOT installed (output is "command not found" or similar), install:
```bash
wget https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh
/tmp/dotnet-install.sh --channel 8.0 --install-dir $HOME/.dotnet
export PATH=$HOME/.dotnet:$PATH
echo 'export PATH=$HOME/.dotnet:$PATH' >> ~/.bashrc
dotnet --version
```

- [ ] **Step 0.2: Verify working directory**

Run: `pwd && ls /home/hieu-nguyen/management/`
Expected: Working directory `/home/hieu-nguyen/management/`. Currently contains `.vscode/` and `docs/`.

---

## Task 1: Initialize git repository

**Files:**
- Create: `/home/hieu-nguyen/management/.gitignore`
- Create: `/home/hieu-nguyen/management/.editorconfig`

- [ ] **Step 1.1: Initialize git**

Run from `/home/hieu-nguyen/management/`:
```bash
git init
git config user.email "dev@local" 2>/dev/null || true
git config user.name "dev" 2>/dev/null || true
```
Expected: "Initialized empty Git repository..."

- [ ] **Step 1.2: Create `.gitignore`**

Write `/home/hieu-nguyen/management/.gitignore`:
```gitignore
# .NET
bin/
obj/
*.user
*.suo
.vs/

# Avalonia
*.designer.cs

# IDE
.idea/
.vs/

# OS
.DS_Store
Thumbs.db

# App data
*.db
*.db-shm
*.db-wal
publish/
TestResults/

# Logs
logs/
*.log
```

- [ ] **Step 1.3: Create `.editorconfig`**

Write `/home/hieu-nguyen/management/.editorconfig`:
```ini
root = true

[*]
indent_style = space
indent_size = 4
end_of_line = lf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

[*.{csproj,props,xml,axaml,xaml}]
indent_size = 2

[*.{json,yml,yaml,md}]
indent_size = 2

[*.cs]
csharp_style_namespace_declarations = file_scoped:suggestion
csharp_using_directive_placement = outside_namespace:suggestion
dotnet_sort_system_directives_first = true
```

- [ ] **Step 1.4: Commit**

Run:
```bash
cd /home/hieu-nguyen/management/
git add .gitignore .editorconfig docs/
git commit -m "chore: init repo with gitignore, editorconfig, design doc"
```
Expected: Commit succeeds.

---

## Task 2: Create solution structure with 5 projects

**Files:**
- Create: `/home/hieu-nguyen/management/ConstructionFinance.sln`
- Create: `/home/hieu-nguyen/management/global.json`
- Create: `/home/hieu-nguyen/management/Directory.Build.props`
- Create: 5 src project folders + 2 test project folders

- [ ] **Step 2.1: Create `global.json` to pin .NET 8 SDK**

Write `/home/hieu-nguyen/management/global.json`:
```json
{
  "sdk": {
    "version": "8.0.0",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

- [ ] **Step 2.2: Create `Directory.Build.props` for shared settings**

Write `/home/hieu-nguyen/management/Directory.Build.props`:
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>12.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <WarningsAsErrors>nullable</WarningsAsErrors>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2.3: Create empty solution**

Run from `/home/hieu-nguyen/management/`:
```bash
dotnet new sln -n ConstructionFinance
ls *.sln
```
Expected: `ConstructionFinance.sln` exists.

- [ ] **Step 2.4: Create 5 source projects**

Run:
```bash
mkdir -p src tests
cd src
dotnet new classlib -n ConstructionFinance.Domain -o ConstructionFinance.Domain
dotnet new classlib -n ConstructionFinance.Data -o ConstructionFinance.Data
dotnet new classlib -n ConstructionFinance.Application -o ConstructionFinance.Application
dotnet new classlib -n ConstructionFinance.Infrastructure -o ConstructionFinance.Infrastructure
dotnet new classlib -n ConstructionFinance.Desktop -o ConstructionFinance.Desktop
cd ..
```

Then delete default `Class1.cs` files:
```bash
find src/ -name "Class1.cs" -delete
```

- [ ] **Step 2.5: Create 2 test projects**

Run:
```bash
cd tests
dotnet new xunit -n ConstructionFinance.Domain.Tests -o ConstructionFinance.Domain.Tests
dotnet new xunit -n ConstructionFinance.Data.Tests -o ConstructionFinance.Data.Tests
cd ..
find tests/ -name "UnitTest1.cs" -delete
```

- [ ] **Step 2.6: Add all projects to solution**

Run:
```bash
dotnet sln add src/ConstructionFinance.Domain/ConstructionFinance.Domain.csproj
dotnet sln add src/ConstructionFinance.Data/ConstructionFinance.Data.csproj
dotnet sln add src/ConstructionFinance.Application/ConstructionFinance.Application.csproj
dotnet sln add src/ConstructionFinance.Infrastructure/ConstructionFinance.Infrastructure.csproj
dotnet sln add src/ConstructionFinance.Desktop/ConstructionFinance.Desktop.csproj
dotnet sln add tests/ConstructionFinance.Domain.Tests/ConstructionFinance.Domain.Tests.csproj
dotnet sln add tests/ConstructionFinance.Data.Tests/ConstructionFinance.Data.Tests.csproj
```

- [ ] **Step 2.7: Add project references**

Run:
```bash
# Domain depends on nothing (besides FluentValidation which is added in Task 3)
# Data depends on Domain
dotnet add src/ConstructionFinance.Data/ConstructionFinance.Data.csproj reference src/ConstructionFinance.Domain/ConstructionFinance.Domain.csproj
# Application depends on Domain
dotnet add src/ConstructionFinance.Application/ConstructionFinance.Application.csproj reference src/ConstructionFinance.Domain/ConstructionFinance.Domain.csproj
# Infrastructure depends on Application + Domain
dotnet add src/ConstructionFinance.Infrastructure/ConstructionFinance.Infrastructure.csproj reference src/ConstructionFinance.Application/ConstructionFinance.Application.csproj
dotnet add src/ConstructionFinance.Infrastructure/ConstructionFinance.Infrastructure.csproj reference src/ConstructionFinance.Domain/ConstructionFinance.Domain.csproj
# Desktop depends on all
dotnet add src/ConstructionFinance.Desktop/ConstructionFinance.Desktop.csproj reference src/ConstructionFinance.Application/ConstructionFinance.Application.csproj
dotnet add src/ConstructionFinance.Desktop/ConstructionFinance.Desktop.csproj reference src/ConstructionFinance.Infrastructure/ConstructionFinance.Infrastructure.csproj
dotnet add src/ConstructionFinance.Desktop/ConstructionFinance.Desktop.csproj reference src/ConstructionFinance.Data/ConstructionFinance.Data.csproj
dotnet add src/ConstructionFinance.Desktop/ConstructionFinance.Desktop.csproj reference src/ConstructionFinance.Domain/ConstructionFinance.Domain.csproj
# Test projects
dotnet add tests/ConstructionFinance.Domain.Tests/ConstructionFinance.Domain.Tests.csproj reference src/ConstructionFinance.Domain/ConstructionFinance.Domain.csproj
dotnet add tests/ConstructionFinance.Data.Tests/ConstructionFinance.Data.Tests.csproj reference src/ConstructionFinance.Data/ConstructionFinance.Data.csproj
dotnet add tests/ConstructionFinance.Data.Tests/ConstructionFinance.Data.Tests.csproj reference src/ConstructionFinance.Domain/ConstructionFinance.Domain.csproj
```

- [ ] **Step 2.8: Build to verify solution wired up**

Run:
```bash
dotnet build
```
Expected: Build succeeds (warnings OK, no errors).

- [ ] **Step 2.9: Commit**

```bash
git add .
git commit -m "chore: create solution with 5 src projects and 2 test projects"
```

---

## Task 3: Add NuGet packages with Central Package Management

**Files:**
- Create: `/home/hieu-nguyen/management/Directory.Packages.props`
- Modify each `.csproj` to add `<PackageReference>` (without version, managed centrally)

- [ ] **Step 3.1: Create `Directory.Packages.props`**

Write `/home/hieu-nguyen/management/Directory.Packages.props`:
```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- EF Core 8 -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="8.0.10" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.10" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.10" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.10" />

    <!-- Validation -->
    <PackageVersion Include="FluentValidation" Version="11.10.0" />

    <!-- Logging -->
    <PackageVersion Include="Serilog" Version="4.0.2" />
    <PackageVersion Include="Serilog.Extensions.Hosting" Version="8.0.0" />
    <PackageVersion Include="Serilog.Sinks.File" Version="6.0.0" />
    <PackageVersion Include="Serilog.Sinks.Console" Version="6.0.0" />

    <!-- DI / Hosting -->
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="8.0.1" />
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="8.0.1" />
    <PackageVersion Include="Microsoft.Extensions.Configuration.Json" Version="8.0.1" />

    <!-- Mapping -->
    <PackageVersion Include="AutoMapper" Version="13.0.1" />

    <!-- Test -->
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageVersion Include="xunit" Version="2.9.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageVersion Include="Moq" Version="4.20.72" />
    <PackageVersion Include="Shouldly" Version="4.2.1" />
    <PackageVersion Include="coverlet.collector" Version="6.0.2" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3.2: Add packages to Domain project**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/ConstructionFinance.Domain.csproj`. The file currently looks like:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>ConstructionFinance.Domain</RootNamespace>
  </PropertyGroup>
</Project>
```

Update to:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>ConstructionFinance.Domain</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="FluentValidation" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3.3: Add packages to Data project**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Data/ConstructionFinance.Data.csproj`. Add after the existing `<ItemGroup>` containing the project reference:
```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>
```

- [ ] **Step 3.4: Add packages to test projects**

Edit `/home/hieu-nguyen/management/tests/ConstructionFinance.Domain.Tests/ConstructionFinance.Domain.Tests.csproj`. Replace the existing `<ItemGroup>` containing test packages with:
```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Shouldly" />
    <PackageReference Include="Moq" />
    <PackageReference Include="coverlet.collector" />
  </ItemGroup>
```

Same for `/home/hieu-nguyen/management/tests/ConstructionFinance.Data.Tests/ConstructionFinance.Data.Tests.csproj`, but also add EF Core test packages:
```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Shouldly" />
    <PackageReference Include="Moq" />
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
  </ItemGroup>
```

- [ ] **Step 3.5: Restore + build**

Run from `/home/hieu-nguyen/management/`:
```bash
dotnet restore
dotnet build
```
Expected: Build succeeds.

- [ ] **Step 3.6: Run empty test suite to verify wiring**

Run:
```bash
dotnet test
```
Expected: "Total tests: 0, Passed: 0" (no tests yet, but runner works).

- [ ] **Step 3.7: Commit**

```bash
git add .
git commit -m "chore: add NuGet packages with Central Package Management"
```

---

## Task 4: Base abstractions (BaseEntity, SoftDeletableEntity, marker interfaces)

**Files:**
- Create: `src/ConstructionFinance.Domain/Common/BaseEntity.cs`
- Create: `src/ConstructionFinance.Domain/Common/SoftDeletableEntity.cs`
- Create: `src/ConstructionFinance.Domain/Common/ISoftDeletable.cs`
- Create: `src/ConstructionFinance.Domain/Common/IAuditable.cs`
- Test: `tests/ConstructionFinance.Domain.Tests/Common/BaseEntityTests.cs`

- [ ] **Step 4.1: Write failing test for BaseEntity**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Domain.Tests/Common/BaseEntityTests.cs`:
```csharp
using ConstructionFinance.Domain.Common;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Domain.Tests.Common;

public class BaseEntityTests
{
    private class TestEntity : BaseEntity { }
    private class TestSoftDeletableEntity : SoftDeletableEntity { }

    [Fact]
    public void BaseEntity_should_have_Id_CreatedAt_UpdatedAt_with_defaults()
    {
        var entity = new TestEntity();

        entity.Id.ShouldBe(0);
        entity.CreatedAt.ShouldBe(default);
        entity.UpdatedAt.ShouldBe(default);
    }

    [Fact]
    public void SoftDeletableEntity_should_extend_BaseEntity_with_DeletedAt()
    {
        var entity = new TestSoftDeletableEntity();

        entity.Id.ShouldBe(0);
        entity.DeletedAt.ShouldBeNull();
        entity.ShouldBeAssignableTo<BaseEntity>();
        entity.ShouldBeAssignableTo<ISoftDeletable>();
    }

    [Fact]
    public void ISoftDeletable_allows_setting_DeletedAt()
    {
        ISoftDeletable entity = new TestSoftDeletableEntity();
        var now = DateTime.UtcNow;

        entity.DeletedAt = now;

        entity.DeletedAt.ShouldBe(now);
    }
}
```

- [ ] **Step 4.2: Run test to see it fail**

Run:
```bash
dotnet test tests/ConstructionFinance.Domain.Tests/ConstructionFinance.Domain.Tests.csproj --filter "FullyQualifiedName~BaseEntityTests"
```
Expected: Compilation error — types `BaseEntity`, `SoftDeletableEntity`, `ISoftDeletable` not found.

- [ ] **Step 4.3: Implement `IAuditable.cs`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Common/IAuditable.cs`:
```csharp
namespace ConstructionFinance.Domain.Common;

public interface IAuditable
{
    int Id { get; }
}
```

- [ ] **Step 4.4: Implement `ISoftDeletable.cs`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Common/ISoftDeletable.cs`:
```csharp
namespace ConstructionFinance.Domain.Common;

public interface ISoftDeletable
{
    DateTime? DeletedAt { get; set; }
}
```

- [ ] **Step 4.5: Implement `BaseEntity.cs`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Common/BaseEntity.cs`:
```csharp
namespace ConstructionFinance.Domain.Common;

public abstract class BaseEntity : IAuditable
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

- [ ] **Step 4.6: Implement `SoftDeletableEntity.cs`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Common/SoftDeletableEntity.cs`:
```csharp
namespace ConstructionFinance.Domain.Common;

public abstract class SoftDeletableEntity : BaseEntity, ISoftDeletable
{
    public DateTime? DeletedAt { get; set; }
}
```

- [ ] **Step 4.7: Run test to verify pass**

Run:
```bash
dotnet test tests/ConstructionFinance.Domain.Tests/ConstructionFinance.Domain.Tests.csproj --filter "FullyQualifiedName~BaseEntityTests"
```
Expected: 3 tests pass.

- [ ] **Step 4.8: Commit**

```bash
git add .
git commit -m "feat(domain): add BaseEntity, SoftDeletableEntity, marker interfaces"
```

---

## Task 5: Enums

**Files:**
- Create: `src/ConstructionFinance.Domain/Enums/ProjectStatus.cs`
- Create: `src/ConstructionFinance.Domain/Enums/PaymentType.cs`
- Create: `src/ConstructionFinance.Domain/Enums/AuditAction.cs`
- Create: `src/ConstructionFinance.Domain/Enums/AttachmentEntityType.cs`
- Test: `tests/ConstructionFinance.Domain.Tests/Enums/EnumValuesTests.cs`

- [ ] **Step 5.1: Write failing test for enum values**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Domain.Tests/Enums/EnumValuesTests.cs`:
```csharp
using ConstructionFinance.Domain.Enums;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Domain.Tests.Enums;

public class EnumValuesTests
{
    [Fact]
    public void ProjectStatus_should_have_all_required_values()
    {
        Enum.GetNames<ProjectStatus>().ShouldBe(
            new[] { "Planning", "InProgress", "OnHold", "Completed", "Cancelled" },
            ignoreOrder: true);
    }

    [Fact]
    public void PaymentType_should_have_all_required_values()
    {
        Enum.GetNames<PaymentType>().ShouldBe(
            new[] { "Advance", "Installment", "FinalSettlement", "RetentionRelease" },
            ignoreOrder: true);
    }

    [Fact]
    public void AuditAction_should_have_create_update_delete()
    {
        Enum.GetNames<AuditAction>().ShouldBe(
            new[] { "Create", "Update", "Delete" },
            ignoreOrder: true);
    }

    [Fact]
    public void AttachmentEntityType_should_match_design_doc()
    {
        Enum.GetNames<AttachmentEntityType>().ShouldBe(
            new[] { "ApprovalDecision", "Contract", "Project", "CostItem", "Payment" },
            ignoreOrder: true);
    }
}
```

- [ ] **Step 5.2: Run test to see it fail**

Run:
```bash
dotnet test --filter "FullyQualifiedName~EnumValuesTests"
```
Expected: Compilation errors — enum types not found.

- [ ] **Step 5.3: Implement enums**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Enums/ProjectStatus.cs`:
```csharp
namespace ConstructionFinance.Domain.Enums;

public enum ProjectStatus
{
    Planning,
    InProgress,
    OnHold,
    Completed,
    Cancelled
}
```

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Enums/PaymentType.cs`:
```csharp
namespace ConstructionFinance.Domain.Enums;

public enum PaymentType
{
    Advance,
    Installment,
    FinalSettlement,
    RetentionRelease
}
```

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Enums/AuditAction.cs`:
```csharp
namespace ConstructionFinance.Domain.Enums;

public enum AuditAction
{
    Create,
    Update,
    Delete
}
```

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Enums/AttachmentEntityType.cs`:
```csharp
namespace ConstructionFinance.Domain.Enums;

public enum AttachmentEntityType
{
    ApprovalDecision,
    Contract,
    Project,
    CostItem,
    Payment
}
```

- [ ] **Step 5.4: Run test to verify pass**

Run:
```bash
dotnet test --filter "FullyQualifiedName~EnumValuesTests"
```
Expected: 4 tests pass.

- [ ] **Step 5.5: Commit**

```bash
git add .
git commit -m "feat(domain): add 4 enums (ProjectStatus, PaymentType, AuditAction, AttachmentEntityType)"
```

---

## Task 6: Vendor entity

**Files:**
- Create: `src/ConstructionFinance.Domain/Entities/Vendor.cs`
- Test: `tests/ConstructionFinance.Domain.Tests/Entities/VendorTests.cs`

- [ ] **Step 6.1: Write failing test**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Domain.Tests/Entities/VendorTests.cs`:
```csharp
using ConstructionFinance.Domain.Common;
using ConstructionFinance.Domain.Entities;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Domain.Tests.Entities;

public class VendorTests
{
    [Fact]
    public void Vendor_extends_SoftDeletableEntity()
    {
        new Vendor { VendorName = "X" }.ShouldBeAssignableTo<SoftDeletableEntity>();
    }

    [Fact]
    public void Vendor_can_set_all_properties()
    {
        var vendor = new Vendor
        {
            VendorCode = "V001",
            VendorName = "Cong ty TNHH ABC",
            TaxCode = "0123456789",
            Address = "123 Le Loi",
            Phone = "0901234567",
            Email = "info@abc.vn",
            ContactPerson = "Nguyen Van A",
            Notes = "Note"
        };

        vendor.VendorCode.ShouldBe("V001");
        vendor.VendorName.ShouldBe("Cong ty TNHH ABC");
        vendor.TaxCode.ShouldBe("0123456789");
    }

    [Fact]
    public void Vendor_VendorName_is_required_nonnull()
    {
        // Compile-time check: VendorName is `required string`
        var vendor = new Vendor { VendorName = "X" };
        vendor.VendorName.ShouldNotBeNullOrEmpty();
    }
}
```

- [ ] **Step 6.2: Run test — fails**

Run:
```bash
dotnet test --filter "FullyQualifiedName~VendorTests"
```
Expected: Compile error — `Vendor` not found.

- [ ] **Step 6.3: Implement Vendor**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Entities/Vendor.cs`:
```csharp
using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Domain.Entities;

public class Vendor : SoftDeletableEntity
{
    public string? VendorCode { get; set; }
    public required string VendorName { get; set; }
    public string? TaxCode { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? ContactPerson { get; set; }
    public string? Notes { get; set; }
}
```

- [ ] **Step 6.4: Run test — passes**

Run:
```bash
dotnet test --filter "FullyQualifiedName~VendorTests"
```
Expected: 3 pass.

- [ ] **Step 6.5: Commit**

```bash
git add .
git commit -m "feat(domain): add Vendor entity"
```

---

## Task 7: CostUnitType entity

**Files:**
- Create: `src/ConstructionFinance.Domain/Entities/CostUnitType.cs`
- Test: `tests/ConstructionFinance.Domain.Tests/Entities/CostUnitTypeTests.cs`

- [ ] **Step 7.1: Write failing test**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Domain.Tests/Entities/CostUnitTypeTests.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Domain.Tests.Entities;

public class CostUnitTypeTests
{
    [Fact]
    public void CostUnitType_can_be_constructed()
    {
        var unit = new CostUnitType
        {
            Code = "m2",
            Name = "Mét vuông",
            Description = "Đơn vị diện tích"
        };

        unit.Code.ShouldBe("m2");
        unit.Name.ShouldBe("Mét vuông");
    }
}
```

- [ ] **Step 7.2: Run test — fails (compile error)**

Run: `dotnet test --filter "FullyQualifiedName~CostUnitTypeTests"`
Expected: `CostUnitType` not found.

- [ ] **Step 7.3: Implement**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Entities/CostUnitType.cs`:
```csharp
namespace ConstructionFinance.Domain.Entities;

public class CostUnitType
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
}
```

- [ ] **Step 7.4: Verify + commit**

```bash
dotnet test --filter "FullyQualifiedName~CostUnitTypeTests"
git add . && git commit -m "feat(domain): add CostUnitType entity"
```

---

## Task 8: ApprovalDecision entity

**Files:**
- Create: `src/ConstructionFinance.Domain/Entities/ApprovalDecision.cs`
- Test: `tests/ConstructionFinance.Domain.Tests/Entities/ApprovalDecisionTests.cs`

- [ ] **Step 8.1: Write failing test**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Domain.Tests/Entities/ApprovalDecisionTests.cs`:
```csharp
using ConstructionFinance.Domain.Common;
using ConstructionFinance.Domain.Entities;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Domain.Tests.Entities;

public class ApprovalDecisionTests
{
    [Fact]
    public void ApprovalDecision_is_soft_deletable()
    {
        new ApprovalDecision
        {
            DecisionCode = "1234/QD",
            DecisionTitle = "Phê duyệt dự án X",
            IssuingAuthority = "UBND",
            DecisionDate = new DateOnly(2026, 5, 1),
            TotalApprovedAmount = 50_000_000_000m
        }.ShouldBeAssignableTo<SoftDeletableEntity>();
    }

    [Fact]
    public void ApprovalDecision_has_navigation_to_contracts_empty_by_default()
    {
        var decision = new ApprovalDecision
        {
            DecisionCode = "1234/QD",
            DecisionTitle = "X",
            IssuingAuthority = "UBND",
            DecisionDate = new DateOnly(2026, 5, 1),
            TotalApprovedAmount = 100m
        };

        decision.Contracts.ShouldNotBeNull();
        decision.Contracts.ShouldBeEmpty();
    }
}
```

- [ ] **Step 8.2: Run test — fails**

Run: `dotnet test --filter "FullyQualifiedName~ApprovalDecisionTests"`
Expected: `ApprovalDecision` and `Contract` not found.

- [ ] **Step 8.3: Create a stub `Contract.cs` to unblock compilation**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Entities/Contract.cs`:
```csharp
using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Domain.Entities;

public class Contract : SoftDeletableEntity
{
    // Full implementation in Task 9
}
```

- [ ] **Step 8.4: Implement ApprovalDecision**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Entities/ApprovalDecision.cs`:
```csharp
using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Domain.Entities;

public class ApprovalDecision : SoftDeletableEntity
{
    public required string DecisionCode { get; set; }
    public required string DecisionTitle { get; set; }
    public DateOnly DecisionDate { get; set; }
    public required string IssuingAuthority { get; set; }
    public decimal TotalApprovedAmount { get; set; }
    public string? Notes { get; set; }

    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}
```

- [ ] **Step 8.5: Verify + commit**

```bash
dotnet test --filter "FullyQualifiedName~ApprovalDecisionTests"
git add . && git commit -m "feat(domain): add ApprovalDecision entity + Contract stub"
```

---

## Task 9: Contract entity (full)

**Files:**
- Modify: `src/ConstructionFinance.Domain/Entities/Contract.cs`
- Test: `tests/ConstructionFinance.Domain.Tests/Entities/ContractTests.cs`

- [ ] **Step 9.1: Write failing test**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Domain.Tests/Entities/ContractTests.cs`:
```csharp
using ConstructionFinance.Domain.Common;
using ConstructionFinance.Domain.Entities;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Domain.Tests.Entities;

public class ContractTests
{
    [Fact]
    public void Contract_is_soft_deletable()
    {
        new Contract
        {
            ContractCode = "HD-001",
            ContractName = "HD nha thau X",
            ContractDate = new DateOnly(2026, 5, 1),
            ContractValue = 1_000_000_000m,
            ApprovalDecisionId = 1
        }.ShouldBeAssignableTo<SoftDeletableEntity>();
    }

    [Fact]
    public void Contract_has_required_FK_to_decision_and_optional_to_vendor()
    {
        var contract = new Contract
        {
            ContractCode = "HD-001",
            ContractName = "HD",
            ContractDate = new DateOnly(2026, 5, 1),
            ContractValue = 100m,
            ApprovalDecisionId = 5,
            ContractorVendorId = 10
        };

        contract.ApprovalDecisionId.ShouldBe(5);
        contract.ContractorVendorId.ShouldBe(10);
        contract.Projects.ShouldNotBeNull();
        contract.Projects.ShouldBeEmpty();
    }

    [Fact]
    public void Contract_ContractorVendorId_can_be_null()
    {
        var contract = new Contract
        {
            ContractCode = "HD",
            ContractName = "HD",
            ContractDate = new DateOnly(2026, 5, 1),
            ContractValue = 100m,
            ApprovalDecisionId = 1
        };

        contract.ContractorVendorId.ShouldBeNull();
    }
}
```

- [ ] **Step 9.2: Run — fails**

Run: `dotnet test --filter "FullyQualifiedName~ContractTests"`
Expected: Missing properties on Contract, missing `Project` type.

- [ ] **Step 9.3: Create stub `Project.cs`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Entities/Project.cs`:
```csharp
using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Domain.Entities;

public class Project : SoftDeletableEntity
{
    // Full implementation in Task 10
}
```

- [ ] **Step 9.4: Full implementation of Contract**

Replace `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Entities/Contract.cs` with:
```csharp
using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Domain.Entities;

public class Contract : SoftDeletableEntity
{
    public int ApprovalDecisionId { get; set; }
    public int? ContractorVendorId { get; set; }
    public required string ContractCode { get; set; }
    public required string ContractName { get; set; }
    public DateOnly ContractDate { get; set; }
    public DateOnly? SigningDate { get; set; }
    public decimal ContractValue { get; set; }
    public string? Notes { get; set; }

    public ApprovalDecision ApprovalDecision { get; set; } = null!;
    public Vendor? ContractorVendor { get; set; }
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}
```

- [ ] **Step 9.5: Verify + commit**

```bash
dotnet test --filter "FullyQualifiedName~ContractTests"
git add . && git commit -m "feat(domain): full Contract entity + Project stub"
```

---

## Task 10: Project entity (full)

**Files:**
- Modify: `src/ConstructionFinance.Domain/Entities/Project.cs`
- Test: `tests/ConstructionFinance.Domain.Tests/Entities/ProjectTests.cs`

- [ ] **Step 10.1: Write failing test**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Domain.Tests/Entities/ProjectTests.cs`:
```csharp
using ConstructionFinance.Domain.Common;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Domain.Tests.Entities;

public class ProjectTests
{
    [Fact]
    public void Project_is_soft_deletable_and_has_required_fields()
    {
        var project = new Project
        {
            ContractId = 1,
            ProjectCode = "DA-001",
            ProjectName = "Nhà máy XYZ",
            Status = ProjectStatus.Planning
        };

        project.ShouldBeAssignableTo<SoftDeletableEntity>();
        project.ProjectCode.ShouldBe("DA-001");
        project.Status.ShouldBe(ProjectStatus.Planning);
    }

    [Fact]
    public void Project_status_defaults_to_Planning()
    {
        var project = new Project
        {
            ContractId = 1,
            ProjectCode = "DA-001",
            ProjectName = "X"
        };

        project.Status.ShouldBe(ProjectStatus.Planning);
    }

    [Fact]
    public void Project_has_CostItems_collection_empty_by_default()
    {
        var project = new Project
        {
            ContractId = 1,
            ProjectCode = "DA-001",
            ProjectName = "X"
        };

        project.CostItems.ShouldNotBeNull();
        project.CostItems.ShouldBeEmpty();
    }
}
```

- [ ] **Step 10.2: Run — fails**

Run: `dotnet test --filter "FullyQualifiedName~ProjectTests"`
Expected: Missing properties, missing `CostItem` type.

- [ ] **Step 10.3: Create stub `CostItem.cs`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Entities/CostItem.cs`:
```csharp
using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Domain.Entities;

public class CostItem : SoftDeletableEntity
{
    // Full implementation in Task 11
}
```

- [ ] **Step 10.4: Full Project implementation**

Replace `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Entities/Project.cs` with:
```csharp
using ConstructionFinance.Domain.Common;
using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Domain.Entities;

public class Project : SoftDeletableEntity
{
    public int ContractId { get; set; }
    public required string ProjectCode { get; set; }
    public required string ProjectName { get; set; }
    public string? ProjectType { get; set; }
    public string? Location { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Planning;
    public decimal TotalBudget { get; set; }
    public decimal TotalActual { get; set; }
    public string? Notes { get; set; }

    public Contract Contract { get; set; } = null!;
    public ICollection<CostItem> CostItems { get; set; } = new List<CostItem>();
}
```

- [ ] **Step 10.5: Verify + commit**

```bash
dotnet test --filter "FullyQualifiedName~ProjectTests"
git add . && git commit -m "feat(domain): full Project entity + CostItem stub"
```

---

## Task 11: CostItem entity (full — tree self-ref)

**Files:**
- Modify: `src/ConstructionFinance.Domain/Entities/CostItem.cs`
- Test: `tests/ConstructionFinance.Domain.Tests/Entities/CostItemTests.cs`

- [ ] **Step 11.1: Write failing test**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Domain.Tests/Entities/CostItemTests.cs`:
```csharp
using ConstructionFinance.Domain.Common;
using ConstructionFinance.Domain.Entities;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Domain.Tests.Entities;

public class CostItemTests
{
    private static CostItem CreateMinimal() => new()
    {
        ProjectId = 1,
        ItemName = "Móng",
        Path = "/1/",
        Level = 0,
        IsLeaf = true
    };

    [Fact]
    public void CostItem_is_soft_deletable_with_required_props()
    {
        var item = CreateMinimal();
        item.ShouldBeAssignableTo<SoftDeletableEntity>();
        item.ItemName.ShouldBe("Móng");
        item.Path.ShouldBe("/1/");
        item.IsLeaf.ShouldBeTrue();
    }

    [Fact]
    public void CostItem_supports_self_referencing_tree()
    {
        var root = CreateMinimal();
        var child = new CostItem
        {
            ProjectId = 1,
            ItemName = "Phần thô",
            Path = "/1/2/",
            Level = 1,
            IsLeaf = false,
            ParentId = 1,
            Parent = root
        };
        root.Children.Add(child);

        child.Parent.ShouldBe(root);
        child.ParentId.ShouldBe(1);
        root.Children.ShouldContain(child);
    }

    [Fact]
    public void CostItem_has_optional_unit_and_vendor()
    {
        var item = CreateMinimal();
        item.UnitTypeId.ShouldBeNull();
        item.VendorId.ShouldBeNull();
        item.UnitType.ShouldBeNull();
        item.Vendor.ShouldBeNull();
    }

    [Fact]
    public void CostItem_has_Payments_collection_empty()
    {
        CreateMinimal().Payments.ShouldBeEmpty();
    }

    [Fact]
    public void CostItem_money_fields_default_to_zero()
    {
        var item = CreateMinimal();
        item.Quantity.ShouldBe(0m);
        item.UnitPrice.ShouldBe(0m);
        item.EstimatedAmount.ShouldBe(0m);
    }
}
```

- [ ] **Step 11.2: Run — fails**

Run: `dotnet test --filter "FullyQualifiedName~CostItemTests"`
Expected: Missing props on CostItem, missing `Payment` type.

- [ ] **Step 11.3: Create stub `Payment.cs`**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Entities/Payment.cs`:
```csharp
using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Domain.Entities;

public class Payment : BaseEntity
{
    // Full implementation in Task 12
}
```

- [ ] **Step 11.4: Full CostItem implementation**

Replace `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Entities/CostItem.cs` with:
```csharp
using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Domain.Entities;

public class CostItem : SoftDeletableEntity
{
    public int ProjectId { get; set; }
    public int? ParentId { get; set; }
    public int? UnitTypeId { get; set; }
    public int? VendorId { get; set; }

    public string? ItemCode { get; set; }
    public required string ItemName { get; set; }
    public int Level { get; set; }
    public required string Path { get; set; }
    public int SortOrder { get; set; }
    public bool IsLeaf { get; set; } = true;

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal EstimatedAmount { get; set; }

    public string? BidPackage { get; set; }
    public string? Notes { get; set; }

    public Project Project { get; set; } = null!;
    public CostItem? Parent { get; set; }
    public ICollection<CostItem> Children { get; set; } = new List<CostItem>();
    public CostUnitType? UnitType { get; set; }
    public Vendor? Vendor { get; set; }
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
```

- [ ] **Step 11.5: Verify + commit**

```bash
dotnet test --filter "FullyQualifiedName~CostItemTests"
git add . && git commit -m "feat(domain): full CostItem entity with self-ref tree + Payment stub"
```

---

## Task 12: Payment entity (full)

**Files:**
- Modify: `src/ConstructionFinance.Domain/Entities/Payment.cs`
- Test: `tests/ConstructionFinance.Domain.Tests/Entities/PaymentTests.cs`

- [ ] **Step 12.1: Write failing test**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Domain.Tests/Entities/PaymentTests.cs`:
```csharp
using ConstructionFinance.Domain.Common;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Domain.Tests.Entities;

public class PaymentTests
{
    [Fact]
    public void Payment_extends_BaseEntity_not_SoftDeletable()
    {
        var payment = new Payment
        {
            CostItemId = 1,
            PaymentNo = 1,
            PaymentType = PaymentType.Advance,
            PaymentDate = new DateOnly(2026, 5, 1),
            Amount = 1_000_000m
        };

        payment.ShouldBeAssignableTo<BaseEntity>();
        payment.ShouldNotBeAssignableTo<SoftDeletableEntity>();
    }

    [Fact]
    public void Payment_has_required_fields()
    {
        var payment = new Payment
        {
            CostItemId = 5,
            PaymentNo = 2,
            PaymentType = PaymentType.Installment,
            PaymentDate = new DateOnly(2026, 5, 15),
            Amount = 500_000_000m,
            Percentage = 20m,
            InvoiceNo = "HD-001",
            InvoiceDate = new DateOnly(2026, 5, 14)
        };

        payment.CostItemId.ShouldBe(5);
        payment.PaymentType.ShouldBe(PaymentType.Installment);
        payment.Amount.ShouldBe(500_000_000m);
    }
}
```

- [ ] **Step 12.2: Run — fails**

Run: `dotnet test --filter "FullyQualifiedName~PaymentTests"`
Expected: Missing props on Payment.

- [ ] **Step 12.3: Full Payment implementation**

Replace `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Entities/Payment.cs` with:
```csharp
using ConstructionFinance.Domain.Common;
using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Domain.Entities;

public class Payment : BaseEntity
{
    public int CostItemId { get; set; }
    public int PaymentNo { get; set; }
    public PaymentType PaymentType { get; set; }
    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public decimal Percentage { get; set; }
    public string? InvoiceNo { get; set; }
    public DateOnly? InvoiceDate { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }

    public CostItem CostItem { get; set; } = null!;
}
```

- [ ] **Step 12.4: Verify + commit**

```bash
dotnet test --filter "FullyQualifiedName~PaymentTests"
git add . && git commit -m "feat(domain): full Payment entity"
```

---

## Task 13: Supporting entities (Attachment, AuditLog, AppSetting)

**Files:**
- Create: `src/ConstructionFinance.Domain/Entities/Attachment.cs`
- Create: `src/ConstructionFinance.Domain/Entities/AuditLog.cs`
- Create: `src/ConstructionFinance.Domain/Entities/AppSetting.cs`
- Test: `tests/ConstructionFinance.Domain.Tests/Entities/SupportingEntitiesTests.cs`

- [ ] **Step 13.1: Write failing test**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Domain.Tests/Entities/SupportingEntitiesTests.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Domain.Tests.Entities;

public class SupportingEntitiesTests
{
    [Fact]
    public void Attachment_has_polymorphic_target()
    {
        var att = new Attachment
        {
            EntityType = AttachmentEntityType.Contract,
            EntityId = 42,
            FileName = "hd.pdf",
            FilePath = "contract/42/hd.pdf",
            FileSize = 12345,
            MimeType = "application/pdf"
        };

        att.EntityType.ShouldBe(AttachmentEntityType.Contract);
        att.EntityId.ShouldBe(42);
        att.FileName.ShouldBe("hd.pdf");
    }

    [Fact]
    public void AuditLog_records_change()
    {
        var log = new AuditLog
        {
            EntityType = "Project",
            EntityId = 1,
            Action = AuditAction.Update,
            FieldName = "ProjectName",
            OldValue = "Old",
            NewValue = "New",
            ChangedAt = new DateTime(2026, 5, 19, 14, 23, 0, DateTimeKind.Utc)
        };

        log.Action.ShouldBe(AuditAction.Update);
        log.OldValue.ShouldBe("Old");
        log.NewValue.ShouldBe("New");
    }

    [Fact]
    public void AppSetting_uses_string_key_as_pk()
    {
        var setting = new AppSetting
        {
            Key = "company_name",
            Value = "Sở Xây dựng"
        };

        setting.Key.ShouldBe("company_name");
        setting.Value.ShouldBe("Sở Xây dựng");
    }
}
```

- [ ] **Step 13.2: Run — fails**

Run: `dotnet test --filter "FullyQualifiedName~SupportingEntitiesTests"`
Expected: 3 types not found.

- [ ] **Step 13.3: Implement Attachment**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Entities/Attachment.cs`:
```csharp
using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Domain.Entities;

public class Attachment
{
    public int Id { get; set; }
    public AttachmentEntityType EntityType { get; set; }
    public int EntityId { get; set; }
    public required string FileName { get; set; }
    public required string FilePath { get; set; }
    public long FileSize { get; set; }
    public string? MimeType { get; set; }
    public string? Description { get; set; }
    public DateTime UploadedAt { get; set; }
}
```

- [ ] **Step 13.4: Implement AuditLog**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Entities/AuditLog.cs`:
```csharp
using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Domain.Entities;

public class AuditLog
{
    public int Id { get; set; }
    public required string EntityType { get; set; }
    public int EntityId { get; set; }
    public AuditAction Action { get; set; }
    public string? FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime ChangedAt { get; set; }
}
```

- [ ] **Step 13.5: Implement AppSetting**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Entities/AppSetting.cs`:
```csharp
namespace ConstructionFinance.Domain.Entities;

public class AppSetting
{
    public required string Key { get; set; }
    public string? Value { get; set; }
    public string? Description { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

- [ ] **Step 13.6: Verify + commit**

```bash
dotnet test --filter "FullyQualifiedName~SupportingEntitiesTests"
git add . && git commit -m "feat(domain): add Attachment, AuditLog, AppSetting entities"
```

---

## Task 14: Cost templates (CostTemplate + CostTemplateItem)

**Files:**
- Create: `src/ConstructionFinance.Domain/Entities/CostTemplate.cs`
- Create: `src/ConstructionFinance.Domain/Entities/CostTemplateItem.cs`
- Test: `tests/ConstructionFinance.Domain.Tests/Entities/CostTemplateTests.cs`

- [ ] **Step 14.1: Write failing test**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Domain.Tests/Entities/CostTemplateTests.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Domain.Tests.Entities;

public class CostTemplateTests
{
    [Fact]
    public void CostTemplate_has_items_collection()
    {
        var tpl = new CostTemplate { TemplateName = "Công trình dân dụng" };
        tpl.Items.ShouldNotBeNull();
        tpl.Items.ShouldBeEmpty();
    }

    [Fact]
    public void CostTemplateItem_supports_self_ref()
    {
        var item = new CostTemplateItem
        {
            TemplateId = 1,
            ItemName = "Chi phí xây dựng",
            Level = 0,
            SortOrder = 1
        };

        item.ParentId.ShouldBeNull();
        item.Children.ShouldBeEmpty();
    }
}
```

- [ ] **Step 14.2: Run — fails**

Run: `dotnet test --filter "FullyQualifiedName~CostTemplateTests"`
Expected: Types not found.

- [ ] **Step 14.3: Implement CostTemplate**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Entities/CostTemplate.cs`:
```csharp
namespace ConstructionFinance.Domain.Entities;

public class CostTemplate
{
    public int Id { get; set; }
    public required string TemplateName { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<CostTemplateItem> Items { get; set; } = new List<CostTemplateItem>();
}
```

- [ ] **Step 14.4: Implement CostTemplateItem**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Entities/CostTemplateItem.cs`:
```csharp
namespace ConstructionFinance.Domain.Entities;

public class CostTemplateItem
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public int? ParentId { get; set; }
    public int? DefaultUnitId { get; set; }
    public string? ItemCode { get; set; }
    public required string ItemName { get; set; }
    public int Level { get; set; }
    public int SortOrder { get; set; }

    public CostTemplate Template { get; set; } = null!;
    public CostTemplateItem? Parent { get; set; }
    public ICollection<CostTemplateItem> Children { get; set; } = new List<CostTemplateItem>();
    public CostUnitType? DefaultUnit { get; set; }
}
```

- [ ] **Step 14.5: Verify + commit**

```bash
dotnet test --filter "FullyQualifiedName~CostTemplateTests"
git add . && git commit -m "feat(domain): add CostTemplate and CostTemplateItem entities"
```

---

## Task 15: FluentValidation validators

**Files:**
- Create: `src/ConstructionFinance.Domain/Validators/ApprovalDecisionValidator.cs`
- Create: `src/ConstructionFinance.Domain/Validators/ContractValidator.cs`
- Create: `src/ConstructionFinance.Domain/Validators/ProjectValidator.cs`
- Create: `src/ConstructionFinance.Domain/Validators/CostItemValidator.cs`
- Create: `src/ConstructionFinance.Domain/Validators/PaymentValidator.cs`
- Create: `src/ConstructionFinance.Domain/Validators/VendorValidator.cs`
- Test: `tests/ConstructionFinance.Domain.Tests/Validators/ValidatorTests.cs`

- [ ] **Step 15.1: Write failing test**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Domain.Tests/Validators/ValidatorTests.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using ConstructionFinance.Domain.Validators;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Domain.Tests.Validators;

public class ValidatorTests
{
    [Fact]
    public void ApprovalDecision_invalid_when_DecisionCode_empty()
    {
        var v = new ApprovalDecisionValidator();
        var entity = new ApprovalDecision
        {
            DecisionCode = "",
            DecisionTitle = "X",
            IssuingAuthority = "Y",
            DecisionDate = new DateOnly(2026, 5, 1),
            TotalApprovedAmount = 1000
        };

        var result = v.Validate(entity);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "DecisionCode");
    }

    [Fact]
    public void ApprovalDecision_invalid_when_TotalApprovedAmount_negative()
    {
        var v = new ApprovalDecisionValidator();
        var entity = new ApprovalDecision
        {
            DecisionCode = "X",
            DecisionTitle = "X",
            IssuingAuthority = "X",
            DecisionDate = new DateOnly(2026, 5, 1),
            TotalApprovedAmount = -1
        };

        v.Validate(entity).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void ApprovalDecision_valid_with_all_required()
    {
        var v = new ApprovalDecisionValidator();
        var entity = new ApprovalDecision
        {
            DecisionCode = "1234/QD",
            DecisionTitle = "Phê duyệt",
            IssuingAuthority = "UBND",
            DecisionDate = new DateOnly(2026, 5, 1),
            TotalApprovedAmount = 1_000_000_000m
        };

        v.Validate(entity).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Project_invalid_when_end_before_start()
    {
        var v = new ProjectValidator();
        var p = new Project
        {
            ContractId = 1,
            ProjectCode = "DA",
            ProjectName = "X",
            Status = ProjectStatus.Planning,
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 5, 1)
        };

        v.Validate(p).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Payment_invalid_when_Amount_zero_or_negative()
    {
        var v = new PaymentValidator();
        v.Validate(new Payment
        {
            CostItemId = 1,
            PaymentNo = 1,
            PaymentType = PaymentType.Advance,
            PaymentDate = new DateOnly(2026, 5, 1),
            Amount = 0
        }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Vendor_invalid_when_name_empty()
    {
        new VendorValidator()
            .Validate(new Vendor { VendorName = "" })
            .IsValid.ShouldBeFalse();
    }
}
```

- [ ] **Step 15.2: Run — fails**

Run: `dotnet test --filter "FullyQualifiedName~ValidatorTests"`
Expected: Validator types not found.

- [ ] **Step 15.3: Implement ApprovalDecisionValidator**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Validators/ApprovalDecisionValidator.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using FluentValidation;

namespace ConstructionFinance.Domain.Validators;

public class ApprovalDecisionValidator : AbstractValidator<ApprovalDecision>
{
    public ApprovalDecisionValidator()
    {
        RuleFor(x => x.DecisionCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.DecisionTitle).NotEmpty().MaximumLength(255);
        RuleFor(x => x.IssuingAuthority).NotEmpty().MaximumLength(255);
        RuleFor(x => x.DecisionDate).NotEqual(default(DateOnly));
        RuleFor(x => x.TotalApprovedAmount).GreaterThanOrEqualTo(0);
    }
}
```

- [ ] **Step 15.4: Implement ContractValidator**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Validators/ContractValidator.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using FluentValidation;

namespace ConstructionFinance.Domain.Validators;

public class ContractValidator : AbstractValidator<Contract>
{
    public ContractValidator()
    {
        RuleFor(x => x.ApprovalDecisionId).GreaterThan(0);
        RuleFor(x => x.ContractCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ContractName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ContractDate).NotEqual(default(DateOnly));
        RuleFor(x => x.ContractValue).GreaterThanOrEqualTo(0);
    }
}
```

- [ ] **Step 15.5: Implement ProjectValidator**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Validators/ProjectValidator.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using FluentValidation;

namespace ConstructionFinance.Domain.Validators;

public class ProjectValidator : AbstractValidator<Project>
{
    public ProjectValidator()
    {
        RuleFor(x => x.ContractId).GreaterThan(0);
        RuleFor(x => x.ProjectCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ProjectName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.TotalBudget).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TotalActual).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate!.Value)
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue)
            .WithMessage("Ngày kết thúc phải sau ngày bắt đầu");
    }
}
```

- [ ] **Step 15.6: Implement CostItemValidator**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Validators/CostItemValidator.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using FluentValidation;

namespace ConstructionFinance.Domain.Validators;

public class CostItemValidator : AbstractValidator<CostItem>
{
    public CostItemValidator()
    {
        RuleFor(x => x.ProjectId).GreaterThan(0);
        RuleFor(x => x.ItemName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Path).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Level).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EstimatedAmount).GreaterThanOrEqualTo(0);
    }
}
```

- [ ] **Step 15.7: Implement PaymentValidator**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Validators/PaymentValidator.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using FluentValidation;

namespace ConstructionFinance.Domain.Validators;

public class PaymentValidator : AbstractValidator<Payment>
{
    public PaymentValidator()
    {
        RuleFor(x => x.CostItemId).GreaterThan(0);
        RuleFor(x => x.PaymentNo).GreaterThan(0);
        RuleFor(x => x.PaymentDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Percentage).InclusiveBetween(0, 100);
    }
}
```

- [ ] **Step 15.8: Implement VendorValidator**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Validators/VendorValidator.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using FluentValidation;

namespace ConstructionFinance.Domain.Validators;

public class VendorValidator : AbstractValidator<Vendor>
{
    public VendorValidator()
    {
        RuleFor(x => x.VendorName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.VendorCode).MaximumLength(50);
        RuleFor(x => x.TaxCode).MaximumLength(50);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Phone).MaximumLength(50);
    }
}
```

- [ ] **Step 15.9: Verify + commit**

```bash
dotnet test --filter "FullyQualifiedName~ValidatorTests"
git add . && git commit -m "feat(domain): add FluentValidation validators for 6 main entities"
```

---

## Task 16: AppDbContext skeleton

**Files:**
- Create: `src/ConstructionFinance.Data/AppDbContext.cs`
- Test: `tests/ConstructionFinance.Data.Tests/AppDbContextTests.cs`
- Test: `tests/ConstructionFinance.Data.Tests/TestDbContextFactory.cs`

- [ ] **Step 16.1: Write failing test for AppDbContext**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Data.Tests/TestDbContextFactory.cs`:
```csharp
using ConstructionFinance.Data;
using Microsoft.EntityFrameworkCore;

namespace ConstructionFinance.Data.Tests;

public static class TestDbContextFactory
{
    public static AppDbContext CreateInMemory()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    public static AppDbContext CreateSqliteInMemory()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var ctx = new AppDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }
}
```

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Data.Tests/AppDbContextTests.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests;

public class AppDbContextTests
{
    [Fact]
    public void AppDbContext_exposes_all_DbSets()
    {
        using var ctx = TestDbContextFactory.CreateInMemory();

        ctx.ApprovalDecisions.ShouldNotBeNull();
        ctx.Contracts.ShouldNotBeNull();
        ctx.Projects.ShouldNotBeNull();
        ctx.CostItems.ShouldNotBeNull();
        ctx.Payments.ShouldNotBeNull();
        ctx.Vendors.ShouldNotBeNull();
        ctx.CostUnitTypes.ShouldNotBeNull();
        ctx.Attachments.ShouldNotBeNull();
        ctx.AuditLogs.ShouldNotBeNull();
        ctx.CostTemplates.ShouldNotBeNull();
        ctx.CostTemplateItems.ShouldNotBeNull();
        ctx.AppSettings.ShouldNotBeNull();
    }

    [Fact]
    public void Can_insert_and_query_a_Vendor()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        ctx.Vendors.Add(new Vendor { VendorName = "ABC Ltd" });
        ctx.SaveChanges();

        var fetched = ctx.Vendors.AsNoTracking().Single();
        fetched.VendorName.ShouldBe("ABC Ltd");
        fetched.Id.ShouldBeGreaterThan(0);
    }
}
```

- [ ] **Step 16.2: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Data.Tests/ --filter "FullyQualifiedName~AppDbContextTests"`
Expected: `AppDbContext` not found.

- [ ] **Step 16.3: Implement AppDbContext (skeleton, no configurations yet)**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Data/AppDbContext.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConstructionFinance.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<CostItem> CostItems => Set<CostItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<CostUnitType> CostUnitTypes => Set<CostUnitType>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<CostTemplate> CostTemplates => Set<CostTemplate>();
    public DbSet<CostTemplateItem> CostTemplateItems => Set<CostTemplateItem>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

- [ ] **Step 16.4: Verify + commit**

```bash
dotnet test tests/ConstructionFinance.Data.Tests/ --filter "FullyQualifiedName~AppDbContextTests"
git add . && git commit -m "feat(data): add AppDbContext with all 12 DbSets and test factory"
```

---

## Task 17: EF Core configurations — Vendor, CostUnitType, AppSetting (simple ones)

**Files:**
- Create: `src/ConstructionFinance.Data/Configurations/VendorConfiguration.cs`
- Create: `src/ConstructionFinance.Data/Configurations/CostUnitTypeConfiguration.cs`
- Create: `src/ConstructionFinance.Data/Configurations/AppSettingConfiguration.cs`
- Test: `tests/ConstructionFinance.Data.Tests/Configurations/SimpleConfigurationTests.cs`

- [ ] **Step 17.1: Write failing test**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Data.Tests/Configurations/SimpleConfigurationTests.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Configurations;

public class SimpleConfigurationTests
{
    [Fact]
    public void Vendor_TaxCode_unique_when_not_null()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        ctx.Vendors.Add(new Vendor { VendorName = "A", TaxCode = "1111" });
        ctx.SaveChanges();

        ctx.Vendors.Add(new Vendor { VendorName = "B", TaxCode = "1111" });
        Should.Throw<DbUpdateException>(() => ctx.SaveChanges());
    }

    [Fact]
    public void Vendor_VendorName_max_length_255()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var prop = ctx.Model.FindEntityType(typeof(Vendor))!.FindProperty(nameof(Vendor.VendorName))!;
        prop.GetMaxLength().ShouldBe(255);
        prop.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void CostUnitType_Code_is_unique()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        ctx.CostUnitTypes.Add(new CostUnitType { Code = "m2", Name = "Mét vuông" });
        ctx.SaveChanges();

        ctx.CostUnitTypes.Add(new CostUnitType { Code = "m2", Name = "Other" });
        Should.Throw<DbUpdateException>(() => ctx.SaveChanges());
    }

    [Fact]
    public void AppSetting_Key_is_primary_key()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var pk = ctx.Model.FindEntityType(typeof(AppSetting))!.FindPrimaryKey()!;
        pk.Properties.Single().Name.ShouldBe(nameof(AppSetting.Key));
    }
}
```

- [ ] **Step 17.2: Run — fails**

Run: `dotnet test --filter "FullyQualifiedName~SimpleConfigurationTests"`
Expected: Unique constraint not enforced, no max length, AppSetting.Key not PK.

- [ ] **Step 17.3: Implement VendorConfiguration**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Data/Configurations/VendorConfiguration.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations;

public class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> b)
    {
        b.ToTable("vendors");
        b.HasKey(x => x.Id);

        b.Property(x => x.VendorCode).HasMaxLength(50);
        b.Property(x => x.VendorName).IsRequired().HasMaxLength(255);
        b.Property(x => x.TaxCode).HasMaxLength(50);
        b.Property(x => x.Address).HasMaxLength(500);
        b.Property(x => x.Phone).HasMaxLength(50);
        b.Property(x => x.Email).HasMaxLength(255);
        b.Property(x => x.ContactPerson).HasMaxLength(255);

        b.HasIndex(x => x.TaxCode)
            .IsUnique()
            .HasFilter("\"TaxCode\" IS NOT NULL AND \"DeletedAt\" IS NULL");

        b.HasQueryFilter(x => x.DeletedAt == null);
    }
}
```

- [ ] **Step 17.4: Implement CostUnitTypeConfiguration**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Data/Configurations/CostUnitTypeConfiguration.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations;

public class CostUnitTypeConfiguration : IEntityTypeConfiguration<CostUnitType>
{
    public void Configure(EntityTypeBuilder<CostUnitType> b)
    {
        b.ToTable("cost_unit_types");
        b.HasKey(x => x.Id);

        b.Property(x => x.Code).IsRequired().HasMaxLength(20);
        b.Property(x => x.Name).IsRequired().HasMaxLength(100);
        b.Property(x => x.Description).HasMaxLength(255);

        b.HasIndex(x => x.Code).IsUnique();
    }
}
```

- [ ] **Step 17.5: Implement AppSettingConfiguration**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Data/Configurations/AppSettingConfiguration.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations;

public class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> b)
    {
        b.ToTable("app_settings");
        b.HasKey(x => x.Key);

        b.Property(x => x.Key).HasMaxLength(100);
        b.Property(x => x.Description).HasMaxLength(255);
    }
}
```

- [ ] **Step 17.6: Verify + commit**

```bash
dotnet test --filter "FullyQualifiedName~SimpleConfigurationTests"
git add . && git commit -m "feat(data): EF configurations for Vendor, CostUnitType, AppSetting"
```

---

## Task 18: EF configurations — core entities (Decision, Contract, Project)

**Files:**
- Create: `src/ConstructionFinance.Data/Configurations/ApprovalDecisionConfiguration.cs`
- Create: `src/ConstructionFinance.Data/Configurations/ContractConfiguration.cs`
- Create: `src/ConstructionFinance.Data/Configurations/ProjectConfiguration.cs`
- Test: `tests/ConstructionFinance.Data.Tests/Configurations/CoreEntitiesConfigurationTests.cs`

- [ ] **Step 18.1: Write failing test**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Data.Tests/Configurations/CoreEntitiesConfigurationTests.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Configurations;

public class CoreEntitiesConfigurationTests
{
    private static ApprovalDecision NewDecision(string code) => new()
    {
        DecisionCode = code,
        DecisionTitle = "T",
        IssuingAuthority = "A",
        DecisionDate = new DateOnly(2026, 5, 1),
        TotalApprovedAmount = 100m
    };

    [Fact]
    public void ApprovalDecision_DecisionCode_is_unique()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        ctx.ApprovalDecisions.Add(NewDecision("X"));
        ctx.SaveChanges();
        ctx.ApprovalDecisions.Add(NewDecision("X"));

        Should.Throw<DbUpdateException>(() => ctx.SaveChanges());
    }

    [Fact]
    public void Contract_FK_to_Decision_cascades_restrict()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var decision = NewDecision("1234");
        ctx.ApprovalDecisions.Add(decision);
        ctx.SaveChanges();

        ctx.Contracts.Add(new Contract
        {
            ApprovalDecisionId = decision.Id,
            ContractCode = "HD-1",
            ContractName = "HD",
            ContractDate = new DateOnly(2026, 5, 1),
            ContractValue = 100m
        });
        ctx.SaveChanges();

        ctx.ApprovalDecisions.Remove(decision);
        Should.Throw<DbUpdateException>(() => ctx.SaveChanges());
    }

    [Fact]
    public void Contract_unique_code_per_decision()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var d = NewDecision("D1");
        ctx.ApprovalDecisions.Add(d);
        ctx.SaveChanges();

        ctx.Contracts.Add(new Contract { ApprovalDecisionId = d.Id, ContractCode = "HD-1", ContractName = "A", ContractDate = new DateOnly(2026, 5, 1), ContractValue = 1 });
        ctx.SaveChanges();
        ctx.Contracts.Add(new Contract { ApprovalDecisionId = d.Id, ContractCode = "HD-1", ContractName = "B", ContractDate = new DateOnly(2026, 5, 1), ContractValue = 1 });

        Should.Throw<DbUpdateException>(() => ctx.SaveChanges());
    }

    [Fact]
    public void Project_status_stored_as_string()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var prop = ctx.Model.FindEntityType(typeof(Project))!.FindProperty(nameof(Project.Status))!;
        prop.GetProviderClrType().ShouldBe(typeof(string));
    }

    [Fact]
    public void Decimal_precision_on_money_fields()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var prop = ctx.Model.FindEntityType(typeof(ApprovalDecision))!
            .FindProperty(nameof(ApprovalDecision.TotalApprovedAmount))!;
        prop.GetPrecision().ShouldBe(18);
        prop.GetScale().ShouldBe(2);
    }
}
```

- [ ] **Step 18.2: Run — fails**

Run: `dotnet test --filter "FullyQualifiedName~CoreEntitiesConfigurationTests"`
Expected: Multiple failures.

- [ ] **Step 18.3: Implement ApprovalDecisionConfiguration**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Data/Configurations/ApprovalDecisionConfiguration.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations;

public class ApprovalDecisionConfiguration : IEntityTypeConfiguration<ApprovalDecision>
{
    public void Configure(EntityTypeBuilder<ApprovalDecision> b)
    {
        b.ToTable("approval_decisions");
        b.HasKey(x => x.Id);

        b.Property(x => x.DecisionCode).IsRequired().HasMaxLength(50);
        b.Property(x => x.DecisionTitle).IsRequired().HasMaxLength(255);
        b.Property(x => x.IssuingAuthority).IsRequired().HasMaxLength(255);
        b.Property(x => x.TotalApprovedAmount).HasPrecision(18, 2);

        b.HasIndex(x => x.DecisionCode)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        b.HasQueryFilter(x => x.DeletedAt == null);
    }
}
```

- [ ] **Step 18.4: Implement ContractConfiguration**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Data/Configurations/ContractConfiguration.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations;

public class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> b)
    {
        b.ToTable("contracts");
        b.HasKey(x => x.Id);

        b.Property(x => x.ContractCode).IsRequired().HasMaxLength(50);
        b.Property(x => x.ContractName).IsRequired().HasMaxLength(255);
        b.Property(x => x.ContractValue).HasPrecision(18, 2);

        b.HasOne(x => x.ApprovalDecision)
            .WithMany(d => d.Contracts)
            .HasForeignKey(x => x.ApprovalDecisionId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ContractorVendor)
            .WithMany()
            .HasForeignKey(x => x.ContractorVendorId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => x.ApprovalDecisionId);
        b.HasIndex(x => new { x.ApprovalDecisionId, x.ContractCode })
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        b.HasQueryFilter(x => x.DeletedAt == null);
    }
}
```

- [ ] **Step 18.5: Implement ProjectConfiguration**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Data/Configurations/ProjectConfiguration.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.ToTable("projects");
        b.HasKey(x => x.Id);

        b.Property(x => x.ProjectCode).IsRequired().HasMaxLength(50);
        b.Property(x => x.ProjectName).IsRequired().HasMaxLength(255);
        b.Property(x => x.ProjectType).HasMaxLength(100);
        b.Property(x => x.Location).HasMaxLength(500);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.TotalBudget).HasPrecision(18, 2);
        b.Property(x => x.TotalActual).HasPrecision(18, 2);

        b.HasOne(x => x.Contract)
            .WithMany(c => c.Projects)
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.ContractId);
        b.HasIndex(x => new { x.ContractId, x.ProjectCode })
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        b.HasQueryFilter(x => x.DeletedAt == null);
    }
}
```

- [ ] **Step 18.6: Verify + commit**

```bash
dotnet test --filter "FullyQualifiedName~CoreEntitiesConfigurationTests"
git add . && git commit -m "feat(data): EF configurations for ApprovalDecision, Contract, Project"
```

---

## Task 19: EF configurations — CostItem + Payment

**Files:**
- Create: `src/ConstructionFinance.Data/Configurations/CostItemConfiguration.cs`
- Create: `src/ConstructionFinance.Data/Configurations/PaymentConfiguration.cs`
- Test: `tests/ConstructionFinance.Data.Tests/Configurations/CostItemPaymentConfigurationTests.cs`

- [ ] **Step 19.1: Write failing test**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Data.Tests/Configurations/CostItemPaymentConfigurationTests.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Configurations;

public class CostItemPaymentConfigurationTests
{
    private static (AppDbContext ctx, int projectId) BootstrapProject()
    {
        var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var d = new ApprovalDecision { DecisionCode = "D1", DecisionTitle = "X", IssuingAuthority = "U", DecisionDate = new DateOnly(2026, 1, 1), TotalApprovedAmount = 1m };
        ctx.ApprovalDecisions.Add(d);
        ctx.SaveChanges();
        var c = new Contract { ApprovalDecisionId = d.Id, ContractCode = "C1", ContractName = "C", ContractDate = new DateOnly(2026, 1, 1), ContractValue = 1m };
        ctx.Contracts.Add(c);
        ctx.SaveChanges();
        var p = new Project { ContractId = c.Id, ProjectCode = "P1", ProjectName = "P", Status = ProjectStatus.Planning };
        ctx.Projects.Add(p);
        ctx.SaveChanges();
        return (ctx, p.Id);
    }

    [Fact]
    public void CostItem_self_ref_works()
    {
        var (ctx, projectId) = BootstrapProject();
        var root = new CostItem { ProjectId = projectId, ItemName = "Root", Path = "/", Level = 0, IsLeaf = false };
        ctx.CostItems.Add(root);
        ctx.SaveChanges();
        ctx.CostItems.Add(new CostItem { ProjectId = projectId, ParentId = root.Id, ItemName = "Child", Path = $"/{root.Id}/", Level = 1, IsLeaf = true });
        ctx.SaveChanges();

        ctx.CostItems.Count().ShouldBe(2);
        var child = ctx.CostItems.Include(c => c.Parent).Single(c => c.ItemName == "Child");
        child.Parent.ShouldNotBeNull();
        child.Parent!.ItemName.ShouldBe("Root");
    }

    [Fact]
    public void Payment_FK_to_CostItem_cascades_delete()
    {
        var (ctx, projectId) = BootstrapProject();
        var item = new CostItem { ProjectId = projectId, ItemName = "X", Path = "/", Level = 0, IsLeaf = true };
        ctx.CostItems.Add(item);
        ctx.SaveChanges();
        ctx.Payments.Add(new Payment { CostItemId = item.Id, PaymentNo = 1, PaymentType = PaymentType.Advance, PaymentDate = new DateOnly(2026, 5, 1), Amount = 100m, Percentage = 10m });
        ctx.SaveChanges();
        ctx.Payments.Count().ShouldBe(1);

        ctx.CostItems.Remove(item);
        ctx.SaveChanges();

        ctx.Payments.Count().ShouldBe(0);
    }

    [Fact]
    public void Payment_PaymentNo_unique_per_CostItem()
    {
        var (ctx, projectId) = BootstrapProject();
        var item = new CostItem { ProjectId = projectId, ItemName = "X", Path = "/", Level = 0, IsLeaf = true };
        ctx.CostItems.Add(item);
        ctx.SaveChanges();
        ctx.Payments.Add(new Payment { CostItemId = item.Id, PaymentNo = 1, PaymentType = PaymentType.Advance, PaymentDate = new DateOnly(2026, 5, 1), Amount = 100m, Percentage = 10m });
        ctx.SaveChanges();
        ctx.Payments.Add(new Payment { CostItemId = item.Id, PaymentNo = 1, PaymentType = PaymentType.Installment, PaymentDate = new DateOnly(2026, 6, 1), Amount = 200m, Percentage = 20m });

        Should.Throw<DbUpdateException>(() => ctx.SaveChanges());
    }

    [Fact]
    public void CostItem_decimal_quantity_precision_4()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var prop = ctx.Model.FindEntityType(typeof(CostItem))!.FindProperty(nameof(CostItem.Quantity))!;
        prop.GetPrecision().ShouldBe(18);
        prop.GetScale().ShouldBe(4);
    }
}
```

- [ ] **Step 19.2: Run — fails**

Run: `dotnet test --filter "FullyQualifiedName~CostItemPaymentConfigurationTests"`
Expected: Multiple failures.

- [ ] **Step 19.3: Implement CostItemConfiguration**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Data/Configurations/CostItemConfiguration.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations;

public class CostItemConfiguration : IEntityTypeConfiguration<CostItem>
{
    public void Configure(EntityTypeBuilder<CostItem> b)
    {
        b.ToTable("cost_items");
        b.HasKey(x => x.Id);

        b.Property(x => x.ItemCode).HasMaxLength(50);
        b.Property(x => x.ItemName).IsRequired().HasMaxLength(255);
        b.Property(x => x.Path).IsRequired().HasMaxLength(500);
        b.Property(x => x.BidPackage).HasMaxLength(100);
        b.Property(x => x.Quantity).HasPrecision(18, 4);
        b.Property(x => x.UnitPrice).HasPrecision(18, 2);
        b.Property(x => x.EstimatedAmount).HasPrecision(18, 2);

        b.HasOne(x => x.Project)
            .WithMany(p => p.CostItems)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Parent)
            .WithMany(p => p.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.UnitType)
            .WithMany()
            .HasForeignKey(x => x.UnitTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.Vendor)
            .WithMany()
            .HasForeignKey(x => x.VendorId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => x.ProjectId);
        b.HasIndex(x => x.ParentId);
        b.HasIndex(x => new { x.ProjectId, x.ParentId, x.SortOrder });
        b.HasIndex(x => x.Path);

        b.HasQueryFilter(x => x.DeletedAt == null);
    }
}
```

- [ ] **Step 19.4: Implement PaymentConfiguration**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Data/Configurations/PaymentConfiguration.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("payments");
        b.HasKey(x => x.Id);

        b.Property(x => x.PaymentType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Percentage).HasPrecision(5, 2);
        b.Property(x => x.InvoiceNo).HasMaxLength(100);

        b.HasOne(x => x.CostItem)
            .WithMany(c => c.Payments)
            .HasForeignKey(x => x.CostItemId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.CostItemId, x.PaymentNo }).IsUnique();
        b.HasIndex(x => new { x.CostItemId, x.PaymentDate });
    }
}
```

- [ ] **Step 19.5: Verify + commit**

```bash
dotnet test --filter "FullyQualifiedName~CostItemPaymentConfigurationTests"
git add . && git commit -m "feat(data): EF configurations for CostItem (tree) and Payment"
```

---

## Task 20: EF configurations — Attachment, AuditLog, CostTemplate, CostTemplateItem

**Files:**
- Create: `src/ConstructionFinance.Data/Configurations/AttachmentConfiguration.cs`
- Create: `src/ConstructionFinance.Data/Configurations/AuditLogConfiguration.cs`
- Create: `src/ConstructionFinance.Data/Configurations/CostTemplateConfiguration.cs`
- Create: `src/ConstructionFinance.Data/Configurations/CostTemplateItemConfiguration.cs`
- Test: `tests/ConstructionFinance.Data.Tests/Configurations/SupportingConfigurationTests.cs`

- [ ] **Step 20.1: Write failing test**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Data.Tests/Configurations/SupportingConfigurationTests.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Configurations;

public class SupportingConfigurationTests
{
    [Fact]
    public void Attachment_EntityType_stored_as_string()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var prop = ctx.Model.FindEntityType(typeof(Attachment))!.FindProperty(nameof(Attachment.EntityType))!;
        prop.GetProviderClrType().ShouldBe(typeof(string));
    }

    [Fact]
    public void Attachment_composite_index_entity_type_and_id()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        ctx.Attachments.Add(new Attachment
        {
            EntityType = AttachmentEntityType.Contract,
            EntityId = 1,
            FileName = "x.pdf",
            FilePath = "x.pdf",
            UploadedAt = DateTime.UtcNow
        });
        ctx.SaveChanges();
        ctx.Attachments.Count().ShouldBe(1);
    }

    [Fact]
    public void CostTemplateItem_self_ref_supported()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var tpl = new CostTemplate { TemplateName = "T1", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        ctx.CostTemplates.Add(tpl);
        ctx.SaveChanges();
        var root = new CostTemplateItem { TemplateId = tpl.Id, ItemName = "Root", Level = 0, SortOrder = 1 };
        ctx.CostTemplateItems.Add(root);
        ctx.SaveChanges();
        ctx.CostTemplateItems.Add(new CostTemplateItem { TemplateId = tpl.Id, ParentId = root.Id, ItemName = "Child", Level = 1, SortOrder = 1 });
        ctx.SaveChanges();

        ctx.CostTemplateItems.Count().ShouldBe(2);
    }

    [Fact]
    public void AuditLog_action_stored_as_string()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var prop = ctx.Model.FindEntityType(typeof(AuditLog))!.FindProperty(nameof(AuditLog.Action))!;
        prop.GetProviderClrType().ShouldBe(typeof(string));
    }
}
```

- [ ] **Step 20.2: Run — fails**

Run: `dotnet test --filter "FullyQualifiedName~SupportingConfigurationTests"`
Expected: Multiple failures.

- [ ] **Step 20.3: Implement AttachmentConfiguration**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Data/Configurations/AttachmentConfiguration.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations;

public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> b)
    {
        b.ToTable("attachments");
        b.HasKey(x => x.Id);

        b.Property(x => x.EntityType).HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.FileName).IsRequired().HasMaxLength(255);
        b.Property(x => x.FilePath).IsRequired().HasMaxLength(500);
        b.Property(x => x.MimeType).HasMaxLength(100);

        b.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}
```

- [ ] **Step 20.4: Implement AuditLogConfiguration**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Data/Configurations/AuditLogConfiguration.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_logs");
        b.HasKey(x => x.Id);

        b.Property(x => x.EntityType).IsRequired().HasMaxLength(50);
        b.Property(x => x.Action).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.FieldName).HasMaxLength(100);

        b.HasIndex(x => new { x.EntityType, x.EntityId, x.ChangedAt });
    }
}
```

- [ ] **Step 20.5: Implement CostTemplateConfiguration**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Data/Configurations/CostTemplateConfiguration.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations;

public class CostTemplateConfiguration : IEntityTypeConfiguration<CostTemplate>
{
    public void Configure(EntityTypeBuilder<CostTemplate> b)
    {
        b.ToTable("cost_templates");
        b.HasKey(x => x.Id);

        b.Property(x => x.TemplateName).IsRequired().HasMaxLength(255);
        b.HasIndex(x => x.TemplateName).IsUnique();
    }
}
```

- [ ] **Step 20.6: Implement CostTemplateItemConfiguration**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Data/Configurations/CostTemplateItemConfiguration.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations;

public class CostTemplateItemConfiguration : IEntityTypeConfiguration<CostTemplateItem>
{
    public void Configure(EntityTypeBuilder<CostTemplateItem> b)
    {
        b.ToTable("cost_template_items");
        b.HasKey(x => x.Id);

        b.Property(x => x.ItemCode).HasMaxLength(50);
        b.Property(x => x.ItemName).IsRequired().HasMaxLength(255);

        b.HasOne(x => x.Template)
            .WithMany(t => t.Items)
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Parent)
            .WithMany(p => p.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.DefaultUnit)
            .WithMany()
            .HasForeignKey(x => x.DefaultUnitId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.TemplateId, x.ParentId, x.SortOrder });
    }
}
```

- [ ] **Step 20.7: Verify + commit**

```bash
dotnet test --filter "FullyQualifiedName~SupportingConfigurationTests"
git add . && git commit -m "feat(data): EF configurations for Attachment, AuditLog, CostTemplate(+Item)"
```

---

## Task 21: TimestampInterceptor (auto-set CreatedAt/UpdatedAt)

**Files:**
- Create: `src/ConstructionFinance.Data/Interceptors/TimestampInterceptor.cs`
- Test: `tests/ConstructionFinance.Data.Tests/Interceptors/TimestampInterceptorTests.cs`

- [ ] **Step 21.1: Write failing test**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Data.Tests/Interceptors/TimestampInterceptorTests.cs`:
```csharp
using ConstructionFinance.Data.Interceptors;
using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Interceptors;

public class TimestampInterceptorTests
{
    private static AppDbContext CreateCtxWithInterceptor()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new TimestampInterceptor())
            .Options;
        var ctx = new AppDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public void Insert_sets_CreatedAt_and_UpdatedAt()
    {
        using var ctx = CreateCtxWithInterceptor();
        var before = DateTime.UtcNow.AddSeconds(-1);

        var v = new Vendor { VendorName = "X" };
        ctx.Vendors.Add(v);
        ctx.SaveChanges();

        v.CreatedAt.ShouldBeGreaterThan(before);
        v.UpdatedAt.ShouldBe(v.CreatedAt);
    }

    [Fact]
    public void Update_changes_UpdatedAt_but_not_CreatedAt()
    {
        using var ctx = CreateCtxWithInterceptor();
        var v = new Vendor { VendorName = "X" };
        ctx.Vendors.Add(v);
        ctx.SaveChanges();
        var created = v.CreatedAt;
        Thread.Sleep(20);

        v.VendorName = "Y";
        ctx.SaveChanges();

        v.CreatedAt.ShouldBe(created);
        v.UpdatedAt.ShouldBeGreaterThan(created);
    }
}
```

- [ ] **Step 21.2: Run — fails (interceptor not found)**

Run: `dotnet test --filter "FullyQualifiedName~TimestampInterceptorTests"`
Expected: Type missing.

- [ ] **Step 21.3: Implement TimestampInterceptor**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Data/Interceptors/TimestampInterceptor.cs`:
```csharp
using ConstructionFinance.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ConstructionFinance.Data.Interceptors;

public class TimestampInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyTimestamps(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyTimestamps(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void ApplyTimestamps(DbContext? ctx)
    {
        if (ctx is null) return;
        var now = DateTime.UtcNow;
        foreach (var entry in ctx.ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(BaseEntity.CreatedAt)).IsModified = false;
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
```

- [ ] **Step 21.4: Verify + commit**

```bash
dotnet test --filter "FullyQualifiedName~TimestampInterceptorTests"
git add . && git commit -m "feat(data): TimestampInterceptor for CreatedAt/UpdatedAt"
```

---

## Task 22: AuditInterceptor (write audit_logs entries)

**Files:**
- Create: `src/ConstructionFinance.Data/Interceptors/AuditInterceptor.cs`
- Test: `tests/ConstructionFinance.Data.Tests/Interceptors/AuditInterceptorTests.cs`

The interceptor captures changes on entities marked auditable. We mark the 5 core business entities (ApprovalDecision, Contract, Project, CostItem, Payment) + Vendor by adding an `[Auditable]` attribute. The interceptor reads entries with this attribute on their type and writes `AuditLog` rows.

- [ ] **Step 22.1: Add `[Auditable]` attribute**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Domain/Common/AuditableAttribute.cs`:
```csharp
namespace ConstructionFinance.Domain.Common;

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class AuditableAttribute : Attribute { }
```

- [ ] **Step 22.2: Tag auditable entities**

Add `[Auditable]` to each of the following entity classes (just above the `public class X` declaration, after the `using` lines):
- `src/ConstructionFinance.Domain/Entities/ApprovalDecision.cs`
- `src/ConstructionFinance.Domain/Entities/Contract.cs`
- `src/ConstructionFinance.Domain/Entities/Project.cs`
- `src/ConstructionFinance.Domain/Entities/CostItem.cs`
- `src/ConstructionFinance.Domain/Entities/Payment.cs`
- `src/ConstructionFinance.Domain/Entities/Vendor.cs`

Each file gets:
```csharp
using ConstructionFinance.Domain.Common;
// existing using lines

namespace ConstructionFinance.Domain.Entities;

[Auditable]
public class X : ... { ... }
```

- [ ] **Step 22.3: Write failing test**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Data.Tests/Interceptors/AuditInterceptorTests.cs`:
```csharp
using ConstructionFinance.Data.Interceptors;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Interceptors;

public class AuditInterceptorTests
{
    private static AppDbContext CreateCtx()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new TimestampInterceptor(), new AuditInterceptor())
            .Options;
        var ctx = new AppDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public void Insert_creates_one_audit_log_with_action_Create()
    {
        using var ctx = CreateCtx();
        ctx.Vendors.Add(new Vendor { VendorName = "X" });
        ctx.SaveChanges();

        var log = ctx.AuditLogs.Single();
        log.Action.ShouldBe(AuditAction.Create);
        log.EntityType.ShouldBe(nameof(Vendor));
        log.FieldName.ShouldBeNull();
        log.NewValue.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Update_creates_one_audit_log_per_changed_field()
    {
        using var ctx = CreateCtx();
        var v = new Vendor { VendorName = "Old", Address = "A1" };
        ctx.Vendors.Add(v);
        ctx.SaveChanges();
        ctx.AuditLogs.RemoveRange(ctx.AuditLogs);
        ctx.SaveChanges();

        v.VendorName = "New";
        v.Address = "A2";
        ctx.SaveChanges();

        var logs = ctx.AuditLogs.Where(l => l.Action == AuditAction.Update).ToList();
        logs.Count.ShouldBe(2);
        logs.ShouldContain(l => l.FieldName == nameof(Vendor.VendorName) && l.OldValue == "Old" && l.NewValue == "New");
        logs.ShouldContain(l => l.FieldName == nameof(Vendor.Address) && l.OldValue == "A1" && l.NewValue == "A2");
    }

    [Fact]
    public void Soft_delete_creates_audit_log_with_action_Delete()
    {
        using var ctx = CreateCtx();
        var v = new Vendor { VendorName = "X" };
        ctx.Vendors.Add(v);
        ctx.SaveChanges();
        ctx.AuditLogs.RemoveRange(ctx.AuditLogs);
        ctx.SaveChanges();

        v.DeletedAt = DateTime.UtcNow;
        ctx.SaveChanges();

        ctx.AuditLogs.ShouldContain(l => l.Action == AuditAction.Delete && l.EntityType == nameof(Vendor));
    }
}
```

- [ ] **Step 22.4: Run — fails**

Run: `dotnet test --filter "FullyQualifiedName~AuditInterceptorTests"`
Expected: AuditInterceptor not found.

- [ ] **Step 22.5: Implement AuditInterceptor**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Data/Interceptors/AuditInterceptor.cs`:
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
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        CaptureAudit(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CaptureAudit(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void CaptureAudit(DbContext? ctx)
    {
        if (ctx is null) return;
        var now = DateTime.UtcNow;
        var logs = new List<AuditLog>();

        foreach (var entry in ctx.ChangeTracker.Entries())
        {
            if (entry.Entity.GetType().GetCustomAttributes(typeof(AuditableAttribute), true).Length == 0)
                continue;
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var entityType = entry.Entity.GetType().Name;
            var id = entry.Property("Id").CurrentValue is int idVal ? idVal : 0;

            if (entry.State == EntityState.Added)
            {
                logs.Add(new AuditLog
                {
                    EntityType = entityType,
                    EntityId = id,
                    Action = AuditAction.Create,
                    NewValue = SerializeSnapshot(entry),
                    ChangedAt = now
                });
            }
            else if (entry.State == EntityState.Modified)
            {
                if (IsSoftDelete(entry))
                {
                    logs.Add(new AuditLog
                    {
                        EntityType = entityType,
                        EntityId = id,
                        Action = AuditAction.Delete,
                        OldValue = SerializeSnapshot(entry),
                        ChangedAt = now
                    });
                    continue;
                }
                foreach (var prop in entry.Properties.Where(p => p.IsModified))
                {
                    if (prop.Metadata.Name == nameof(BaseEntity.UpdatedAt)) continue;
                    logs.Add(new AuditLog
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
                logs.Add(new AuditLog
                {
                    EntityType = entityType,
                    EntityId = id,
                    Action = AuditAction.Delete,
                    OldValue = SerializeSnapshot(entry),
                    ChangedAt = now
                });
            }
        }

        if (logs.Count > 0)
            ctx.Set<AuditLog>().AddRange(logs);
    }

    private static bool IsSoftDelete(EntityEntry entry)
    {
        if (entry.Entity is not ISoftDeletable) return false;
        var deletedAt = entry.Property(nameof(ISoftDeletable.DeletedAt));
        var wasNull = deletedAt.OriginalValue is null;
        var isNotNull = deletedAt.CurrentValue is not null;
        return wasNull && isNotNull;
    }

    private static string SerializeSnapshot(EntityEntry entry)
    {
        var dict = entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);
        return JsonSerializer.Serialize(dict);
    }
}
```

- [ ] **Step 22.6: Verify + commit**

```bash
dotnet test --filter "FullyQualifiedName~AuditInterceptorTests"
git add . && git commit -m "feat(data): AuditInterceptor writing AuditLog entries for [Auditable] entities"
```

---

## Task 23: Seed data for CostUnitTypes

**Files:**
- Create: `src/ConstructionFinance.Data/Seed/CostUnitTypeSeeder.cs`
- Test: `tests/ConstructionFinance.Data.Tests/Seed/CostUnitTypeSeederTests.cs`

- [ ] **Step 23.1: Write failing test**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Data.Tests/Seed/CostUnitTypeSeederTests.cs`:
```csharp
using ConstructionFinance.Data.Seed;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Seed;

public class CostUnitTypeSeederTests
{
    [Fact]
    public void Seed_inserts_default_units_when_empty()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        ctx.CostUnitTypes.Count().ShouldBe(0);

        CostUnitTypeSeeder.Seed(ctx);

        var codes = ctx.CostUnitTypes.Select(u => u.Code).ToList();
        codes.ShouldContain("m2");
        codes.ShouldContain("m3");
        codes.ShouldContain("kg");
        codes.ShouldContain("pcs");
        codes.Count.ShouldBeGreaterThanOrEqualTo(10);
    }

    [Fact]
    public void Seed_is_idempotent()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        CostUnitTypeSeeder.Seed(ctx);
        var firstCount = ctx.CostUnitTypes.Count();

        CostUnitTypeSeeder.Seed(ctx);

        ctx.CostUnitTypes.Count().ShouldBe(firstCount);
    }
}
```

- [ ] **Step 23.2: Run — fails (seeder not found)**

Run: `dotnet test --filter "FullyQualifiedName~CostUnitTypeSeederTests"`

- [ ] **Step 23.3: Implement seeder**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Data/Seed/CostUnitTypeSeeder.cs`:
```csharp
using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Data.Seed;

public static class CostUnitTypeSeeder
{
    private static readonly (string Code, string Name)[] Defaults =
    {
        ("m2", "Mét vuông"),
        ("m3", "Mét khối"),
        ("kg", "Ki-lô-gam"),
        ("tan", "Tấn"),
        ("pcs", "Cái"),
        ("bo", "Bộ"),
        ("m", "Mét"),
        ("md", "Mét dài"),
        ("tg", "Trọng gói"),
        ("cong", "Công"),
        ("cm", "Ca máy"),
        ("pct", "Phần trăm")
    };

    public static void Seed(AppDbContext ctx)
    {
        var existing = ctx.CostUnitTypes.Select(u => u.Code).ToHashSet();
        foreach (var (code, name) in Defaults)
        {
            if (!existing.Contains(code))
            {
                ctx.CostUnitTypes.Add(new CostUnitType { Code = code, Name = name });
            }
        }
        ctx.SaveChanges();
    }
}
```

- [ ] **Step 23.4: Verify + commit**

```bash
dotnet test --filter "FullyQualifiedName~CostUnitTypeSeederTests"
git add . && git commit -m "feat(data): seed default CostUnitTypes (idempotent)"
```

---

## Task 24: Generic Repository<T>

**Files:**
- Create: `src/ConstructionFinance.Data/Repositories/IRepository.cs`
- Create: `src/ConstructionFinance.Data/Repositories/Repository.cs`
- Test: `tests/ConstructionFinance.Data.Tests/Repositories/RepositoryTests.cs`

- [ ] **Step 24.1: Write failing test**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Data.Tests/Repositories/RepositoryTests.cs`:
```csharp
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Entities;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Repositories;

public class RepositoryTests
{
    [Fact]
    public async Task Add_then_GetById_returns_entity()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        IRepository<Vendor> repo = new Repository<Vendor>(ctx);

        await repo.AddAsync(new Vendor { VendorName = "X" });
        await ctx.SaveChangesAsync();

        var all = await repo.ListAsync();
        var first = all.Single();
        var fetched = await repo.GetByIdAsync(first.Id);
        fetched.ShouldNotBeNull();
        fetched!.VendorName.ShouldBe("X");
    }

    [Fact]
    public async Task SoftDelete_marks_DeletedAt_and_hides_from_default_query()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        IRepository<Vendor> repo = new Repository<Vendor>(ctx);
        await repo.AddAsync(new Vendor { VendorName = "X" });
        await ctx.SaveChangesAsync();
        var v = (await repo.ListAsync()).Single();

        await repo.SoftDeleteAsync(v);
        await ctx.SaveChangesAsync();

        (await repo.ListAsync()).ShouldBeEmpty();
        (await repo.ListIncludingDeletedAsync()).Single().DeletedAt.ShouldNotBeNull();
    }
}
```

- [ ] **Step 24.2: Run — fails**

Run: `dotnet test --filter "FullyQualifiedName~RepositoryTests"`
Expected: Types missing.

- [ ] **Step 24.3: Implement IRepository<T>**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Data/Repositories/IRepository.cs`:
```csharp
using ConstructionFinance.Domain.Common;

namespace ConstructionFinance.Data.Repositories;

public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListIncludingDeletedAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    Task SoftDeleteAsync(T entity, CancellationToken ct = default);
    void HardDelete(T entity);
}
```

- [ ] **Step 24.4: Implement Repository<T>**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Data/Repositories/Repository.cs`:
```csharp
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

- [ ] **Step 24.5: Verify + commit**

```bash
dotnet test --filter "FullyQualifiedName~RepositoryTests"
git add . && git commit -m "feat(data): generic Repository<T> with soft delete support"
```

---

## Task 25: CostRepository (tree-specific operations)

**Files:**
- Create: `src/ConstructionFinance.Data/Repositories/ICostRepository.cs`
- Create: `src/ConstructionFinance.Data/Repositories/CostRepository.cs`
- Test: `tests/ConstructionFinance.Data.Tests/Repositories/CostRepositoryTests.cs`

- [ ] **Step 25.1: Write failing test**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Data.Tests/Repositories/CostRepositoryTests.cs`:
```csharp
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Repositories;

public class CostRepositoryTests
{
    private static (AppDbContext ctx, int projectId) BootstrapProject()
    {
        var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var d = new ApprovalDecision { DecisionCode = "D1", DecisionTitle = "X", IssuingAuthority = "U", DecisionDate = new DateOnly(2026, 1, 1), TotalApprovedAmount = 1m };
        ctx.ApprovalDecisions.Add(d);
        ctx.SaveChanges();
        var c = new Contract { ApprovalDecisionId = d.Id, ContractCode = "C1", ContractName = "C", ContractDate = new DateOnly(2026, 1, 1), ContractValue = 1m };
        ctx.Contracts.Add(c);
        ctx.SaveChanges();
        var p = new Project { ContractId = c.Id, ProjectCode = "P1", ProjectName = "P", Status = ProjectStatus.Planning };
        ctx.Projects.Add(p);
        ctx.SaveChanges();
        return (ctx, p.Id);
    }

    [Fact]
    public async Task AddRootAsync_sets_path_and_level_correctly()
    {
        var (ctx, projectId) = BootstrapProject();
        ICostRepository repo = new CostRepository(ctx);

        var root = await repo.AddNodeAsync(projectId, null, new CostItem
        {
            ProjectId = projectId,
            ItemName = "Chi phí xây dựng",
            Path = "",
            Level = 0,
            IsLeaf = false
        });
        await ctx.SaveChangesAsync();

        root.Level.ShouldBe(0);
        root.Path.ShouldBe($"/{root.Id}/");
    }

    [Fact]
    public async Task AddChildAsync_inherits_parent_path_and_increments_level()
    {
        var (ctx, projectId) = BootstrapProject();
        ICostRepository repo = new CostRepository(ctx);
        var root = await repo.AddNodeAsync(projectId, null, new CostItem
        {
            ProjectId = projectId,
            ItemName = "R",
            Path = "",
            Level = 0,
            IsLeaf = false
        });
        await ctx.SaveChangesAsync();

        var child = await repo.AddNodeAsync(projectId, root.Id, new CostItem
        {
            ProjectId = projectId,
            ItemName = "C",
            Path = "",
            Level = 0,
            IsLeaf = true
        });
        await ctx.SaveChangesAsync();

        child.Level.ShouldBe(1);
        child.Path.ShouldBe($"/{root.Id}/{child.Id}/");
        child.ParentId.ShouldBe(root.Id);
    }

    [Fact]
    public async Task GetTreeByProjectAsync_returns_all_items_for_project()
    {
        var (ctx, projectId) = BootstrapProject();
        ICostRepository repo = new CostRepository(ctx);
        var r = await repo.AddNodeAsync(projectId, null, new CostItem { ProjectId = projectId, ItemName = "R", Path = "", Level = 0, IsLeaf = false });
        await ctx.SaveChangesAsync();
        await repo.AddNodeAsync(projectId, r.Id, new CostItem { ProjectId = projectId, ItemName = "A", Path = "", Level = 0, IsLeaf = true });
        await repo.AddNodeAsync(projectId, r.Id, new CostItem { ProjectId = projectId, ItemName = "B", Path = "", Level = 0, IsLeaf = true });
        await ctx.SaveChangesAsync();

        var all = await repo.GetTreeByProjectAsync(projectId);
        all.Count.ShouldBe(3);
        all.Select(x => x.ItemName).ShouldBe(new[] { "R", "A", "B" }, ignoreOrder: true);
    }

    [Fact]
    public async Task GetSubtreeAsync_returns_only_node_and_descendants()
    {
        var (ctx, projectId) = BootstrapProject();
        ICostRepository repo = new CostRepository(ctx);
        var r1 = await repo.AddNodeAsync(projectId, null, new CostItem { ProjectId = projectId, ItemName = "R1", Path = "", Level = 0, IsLeaf = false });
        await ctx.SaveChangesAsync();
        await repo.AddNodeAsync(projectId, r1.Id, new CostItem { ProjectId = projectId, ItemName = "C1", Path = "", Level = 0, IsLeaf = true });
        await ctx.SaveChangesAsync();
        var r2 = await repo.AddNodeAsync(projectId, null, new CostItem { ProjectId = projectId, ItemName = "R2", Path = "", Level = 0, IsLeaf = false });
        await ctx.SaveChangesAsync();

        var sub = await repo.GetSubtreeAsync(r1.Id);

        sub.Select(x => x.ItemName).ShouldBe(new[] { "R1", "C1" }, ignoreOrder: true);
    }
}
```

- [ ] **Step 25.2: Run — fails**

Run: `dotnet test --filter "FullyQualifiedName~CostRepositoryTests"`
Expected: Type missing.

- [ ] **Step 25.3: Implement ICostRepository**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Data/Repositories/ICostRepository.cs`:
```csharp
using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Data.Repositories;

public interface ICostRepository : IRepository<CostItem>
{
    Task<CostItem> AddNodeAsync(int projectId, int? parentId, CostItem newNode, CancellationToken ct = default);
    Task<IReadOnlyList<CostItem>> GetTreeByProjectAsync(int projectId, CancellationToken ct = default);
    Task<IReadOnlyList<CostItem>> GetSubtreeAsync(int nodeId, CancellationToken ct = default);
}
```

- [ ] **Step 25.4: Implement CostRepository**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Data/Repositories/CostRepository.cs`:
```csharp
using ConstructionFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConstructionFinance.Data.Repositories;

public class CostRepository : Repository<CostItem>, ICostRepository
{
    public CostRepository(AppDbContext context) : base(context) { }

    public async Task<CostItem> AddNodeAsync(int projectId, int? parentId, CostItem newNode, CancellationToken ct = default)
    {
        newNode.ProjectId = projectId;
        newNode.ParentId = parentId;

        string parentPath;
        int parentLevel;
        if (parentId is null)
        {
            parentPath = "/";
            parentLevel = -1;
        }
        else
        {
            var parent = await DbSet.FirstOrDefaultAsync(c => c.Id == parentId, ct)
                ?? throw new InvalidOperationException($"Parent {parentId} not found");
            parentPath = parent.Path;
            parentLevel = parent.Level;
        }

        newNode.Level = parentLevel + 1;
        newNode.Path = parentPath; // temp, will fix after Id assigned

        await DbSet.AddAsync(newNode, ct);
        await Context.SaveChangesAsync(ct);

        newNode.Path = $"{parentPath.TrimEnd('/')}/{newNode.Id}/";
        return newNode;
    }

    public async Task<IReadOnlyList<CostItem>> GetTreeByProjectAsync(int projectId, CancellationToken ct = default) =>
        await DbSet
            .Where(c => c.ProjectId == projectId)
            .OrderBy(c => c.Path)
            .ThenBy(c => c.SortOrder)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CostItem>> GetSubtreeAsync(int nodeId, CancellationToken ct = default)
    {
        var root = await DbSet.AsNoTracking().FirstOrDefaultAsync(c => c.Id == nodeId, ct)
            ?? throw new InvalidOperationException($"Node {nodeId} not found");
        return await DbSet
            .Where(c => c.Path.StartsWith(root.Path))
            .OrderBy(c => c.Path)
            .AsNoTracking()
            .ToListAsync(ct);
    }
}
```

- [ ] **Step 25.5: Verify + commit**

```bash
dotnet test --filter "FullyQualifiedName~CostRepositoryTests"
git add . && git commit -m "feat(data): CostRepository with tree ops (AddNode, GetTree, GetSubtree)"
```

---

## Task 26: Initial EF Core migration

**Files:**
- Auto-generated under: `src/ConstructionFinance.Data/Migrations/`

- [ ] **Step 26.1: Install EF Core CLI tool**

Run:
```bash
dotnet tool install --global dotnet-ef --version 8.0.10 2>/dev/null || dotnet tool update --global dotnet-ef --version 8.0.10
export PATH="$PATH:$HOME/.dotnet/tools"
dotnet ef --version
```
Expected: Version `8.0.10`.

- [ ] **Step 26.2: Create a temporary `IDesignTimeDbContextFactory` so `dotnet ef` can build the context without a host**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Data/DesignTimeDbContextFactory.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ConstructionFinance.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=design_time.db")
            .Options;
        return new AppDbContext(options);
    }
}
```

- [ ] **Step 26.3: Generate initial migration**

Run from `/home/hieu-nguyen/management/`:
```bash
dotnet ef migrations add InitialCreate \
    --project src/ConstructionFinance.Data \
    --startup-project src/ConstructionFinance.Data \
    --output-dir Migrations
```
Expected: Two files appear in `src/ConstructionFinance.Data/Migrations/`: `<timestamp>_InitialCreate.cs` and `AppDbContextModelSnapshot.cs`.

- [ ] **Step 26.4: Verify migration builds**

Run:
```bash
dotnet build src/ConstructionFinance.Data/
```
Expected: Build succeeds.

- [ ] **Step 26.5: Apply migration to a test SQLite file and verify schema**

Run:
```bash
rm -f /tmp/test_initial.db
dotnet ef database update \
    --project src/ConstructionFinance.Data \
    --startup-project src/ConstructionFinance.Data \
    --connection "DataSource=/tmp/test_initial.db"
ls -la /tmp/test_initial.db
sqlite3 /tmp/test_initial.db ".tables" 2>/dev/null || echo "sqlite3 not installed — skip table list verification"
```
Expected: `/tmp/test_initial.db` created. If `sqlite3` available, output shows all expected tables (`approval_decisions`, `contracts`, `projects`, `cost_items`, `payments`, `vendors`, `cost_unit_types`, `attachments`, `audit_logs`, `cost_templates`, `cost_template_items`, `app_settings`, `__EFMigrationsHistory`).

- [ ] **Step 26.6: Commit**

```bash
git add .
git commit -m "feat(data): add InitialCreate EF Core migration"
```

---

## Task 27: End-to-end smoke test

**Files:**
- Create: `tests/ConstructionFinance.Data.Tests/Integration/EndToEndSmokeTests.cs`

This test exercises the full stack: insert a complete tree QĐ → HĐ → DA → CostItems (cây nhiều cấp) → Payments, then query back and verify counts + relationships + audit logs.

- [ ] **Step 27.1: Write the smoke test**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Data.Tests/Integration/EndToEndSmokeTests.cs`:
```csharp
using ConstructionFinance.Data.Interceptors;
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Data.Seed;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Integration;

public class EndToEndSmokeTests
{
    private static AppDbContext CreateFullStack()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new TimestampInterceptor(), new AuditInterceptor())
            .Options;
        var ctx = new AppDbContext(options);
        ctx.Database.EnsureCreated();
        CostUnitTypeSeeder.Seed(ctx);
        return ctx;
    }

    [Fact]
    public async Task FullTree_QD_HD_DA_CostItems_Payments_inserts_and_queries_correctly()
    {
        using var ctx = CreateFullStack();

        // 1. Decision
        var decision = new ApprovalDecision
        {
            DecisionCode = "1234/QD-UBND",
            DecisionTitle = "Phê duyệt dự án XYZ",
            IssuingAuthority = "UBND Tỉnh ABC",
            DecisionDate = new DateOnly(2026, 3, 15),
            TotalApprovedAmount = 50_000_000_000m
        };
        ctx.ApprovalDecisions.Add(decision);
        await ctx.SaveChangesAsync();
        decision.Id.ShouldBeGreaterThan(0);

        // 2. Contract
        var contract = new Contract
        {
            ApprovalDecisionId = decision.Id,
            ContractCode = "HD-001",
            ContractName = "Hợp đồng xây dựng Nhà máy XYZ",
            ContractDate = new DateOnly(2026, 4, 1),
            ContractValue = 30_000_000_000m
        };
        ctx.Contracts.Add(contract);
        await ctx.SaveChangesAsync();

        // 3. Project
        var project = new Project
        {
            ContractId = contract.Id,
            ProjectCode = "DA-A",
            ProjectName = "Nhà máy XYZ giai đoạn 1",
            Status = ProjectStatus.InProgress,
            TotalBudget = 16_500_000_000m
        };
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();

        // 4. Cost tree via CostRepository
        ICostRepository costRepo = new CostRepository(ctx);
        var ciXayDung = await costRepo.AddNodeAsync(project.Id, null,
            new CostItem { ProjectId = project.Id, ItemName = "Chi phí xây dựng", Path = "", Level = 0, IsLeaf = false, EstimatedAmount = 10_000_000_000m });
        await ctx.SaveChangesAsync();
        var ciPhanTho = await costRepo.AddNodeAsync(project.Id, ciXayDung.Id,
            new CostItem { ProjectId = project.Id, ItemName = "Phần thô", Path = "", Level = 0, IsLeaf = false, EstimatedAmount = 6_000_000_000m });
        await ctx.SaveChangesAsync();
        var unitM3 = ctx.CostUnitTypes.Single(u => u.Code == "m3");
        var ciMong = await costRepo.AddNodeAsync(project.Id, ciPhanTho.Id,
            new CostItem
            {
                ProjectId = project.Id,
                ItemName = "Móng cọc khoan nhồi",
                Path = "",
                Level = 0,
                IsLeaf = true,
                UnitTypeId = unitM3.Id,
                Quantity = 120m,
                UnitPrice = 25_000_000m,
                EstimatedAmount = 3_000_000_000m
            });
        await ctx.SaveChangesAsync();

        // 5. Payments for ciMong
        ctx.Payments.AddRange(
            new Payment { CostItemId = ciMong.Id, PaymentNo = 1, PaymentType = PaymentType.Advance, PaymentDate = new DateOnly(2026, 4, 10), Amount = 900_000_000m, Percentage = 30m, InvoiceNo = "HD-001" },
            new Payment { CostItemId = ciMong.Id, PaymentNo = 2, PaymentType = PaymentType.Installment, PaymentDate = new DateOnly(2026, 5, 15), Amount = 600_000_000m, Percentage = 20m, InvoiceNo = "HD-045" }
        );
        await ctx.SaveChangesAsync();

        // Assertions
        ctx.ApprovalDecisions.Count().ShouldBe(1);
        ctx.Contracts.Count().ShouldBe(1);
        ctx.Projects.Count().ShouldBe(1);
        ctx.CostItems.Count().ShouldBe(3);
        ctx.Payments.Count().ShouldBe(2);

        // Tree path correctness
        var mong = await ctx.CostItems.AsNoTracking().FirstAsync(c => c.ItemName == "Móng cọc khoan nhồi");
        mong.Path.ShouldBe($"/{ciXayDung.Id}/{ciPhanTho.Id}/{ciMong.Id}/");
        mong.Level.ShouldBe(2);

        // Subtree query
        var subtree = await costRepo.GetSubtreeAsync(ciXayDung.Id);
        subtree.Count.ShouldBe(3);

        // Total payments for móng
        var totalPaid = ctx.Payments.Where(p => p.CostItemId == ciMong.Id).Sum(p => p.Amount);
        totalPaid.ShouldBe(1_500_000_000m);

        // Audit logs were written
        ctx.AuditLogs.Count(l => l.Action == AuditAction.Create).ShouldBeGreaterThanOrEqualTo(5); // at least: decision, contract, project, 3 cost items, 2 payments
    }

    [Fact]
    public async Task SoftDelete_decision_hides_from_default_query_but_RESTRICT_blocks_when_children_exist()
    {
        using var ctx = CreateFullStack();
        var d = new ApprovalDecision { DecisionCode = "X", DecisionTitle = "X", IssuingAuthority = "X", DecisionDate = new DateOnly(2026, 1, 1), TotalApprovedAmount = 100m };
        ctx.ApprovalDecisions.Add(d);
        await ctx.SaveChangesAsync();
        ctx.Contracts.Add(new Contract { ApprovalDecisionId = d.Id, ContractCode = "C", ContractName = "C", ContractDate = new DateOnly(2026, 1, 1), ContractValue = 100m });
        await ctx.SaveChangesAsync();

        // RESTRICT: cannot hard-delete decision while contracts exist
        ctx.ApprovalDecisions.Remove(d);
        await Should.ThrowAsync<DbUpdateException>(async () => await ctx.SaveChangesAsync());

        // But soft delete works
        ctx.ChangeTracker.Clear();
        var dReload = await ctx.ApprovalDecisions.FirstAsync();
        dReload.DeletedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        ctx.ApprovalDecisions.Count().ShouldBe(0);
        ctx.ApprovalDecisions.IgnoreQueryFilters().Count().ShouldBe(1);
    }
}
```

- [ ] **Step 27.2: Run smoke test**

Run:
```bash
dotnet test tests/ConstructionFinance.Data.Tests/ --filter "FullyQualifiedName~EndToEndSmokeTests"
```
Expected: 2 tests pass.

- [ ] **Step 27.3: Run the entire suite**

Run:
```bash
dotnet test
```
Expected: All tests pass. Approximately 50+ tests, all green.

- [ ] **Step 27.4: Commit**

```bash
git add . && git commit -m "test: add end-to-end smoke tests for full QĐ→HĐ→DA→CostItems→Payments stack"
```

---

## Task 28: CI workflow

**Files:**
- Create: `.github/workflows/ci.yml`

- [ ] **Step 28.1: Write CI workflow**

Write `/home/hieu-nguyen/management/.github/workflows/ci.yml`:
```yaml
name: CI

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 8
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Test
        run: dotnet test --no-build --configuration Release --verbosity normal --logger "trx;LogFileName=test_results.trx"

      - name: Upload test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: test-results
          path: '**/TestResults/*.trx'
```

- [ ] **Step 28.2: Verify workflow file syntax locally (optional)**

Run (only if `yamllint` is available):
```bash
yamllint .github/workflows/ci.yml 2>/dev/null || echo "yamllint not installed — skipping local lint"
```

- [ ] **Step 28.3: Commit**

```bash
git add .github/
git commit -m "ci: add GitHub Actions workflow for build + test on push/PR"
```

---

## Final verification

- [ ] **Step F.1: Full clean build + test from scratch**

Run:
```bash
cd /home/hieu-nguyen/management/
dotnet clean
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --verbosity minimal
```
Expected: Build succeeds with no errors. All tests pass.

- [ ] **Step F.2: Confirm git log**

Run:
```bash
git log --oneline
```
Expected: ~28 commits, each task as its own commit.

- [ ] **Step F.3: Verify file structure matches plan**

Run:
```bash
find src/ tests/ -name "*.cs" -type f | sort | head -60
ls src/ConstructionFinance.Data/Migrations/
```
Expected: All files from the "File Structure" section exist. Migrations folder has `<timestamp>_InitialCreate.cs` and `AppDbContextModelSnapshot.cs`.

---

## Deliverables

After this plan is complete:

1. ✅ Solution with 5 src projects + 2 test projects, all building cleanly
2. ✅ 13 domain entities + 4 enums + 6 FluentValidation validators
3. ✅ EF Core DbContext with all 12 DbSets and full configurations (FK, indexes, unique constraints, query filters)
4. ✅ TimestampInterceptor + AuditInterceptor (auto-write CreatedAt/UpdatedAt + audit_logs)
5. ✅ Generic `Repository<T>` + specialized `CostRepository` (tree ops: AddNode, GetTree, GetSubtree)
6. ✅ Seed data for 12 default cost unit types
7. ✅ Initial EF Core migration applies cleanly to an SQLite database
8. ✅ ~50+ unit + integration tests covering entities, validators, configurations, interceptors, repositories
9. ✅ End-to-end smoke test inserting a full QĐ → HĐ → DA → CostItems → Payments tree
10. ✅ GitHub Actions CI workflow building + testing on every push

**What's NOT in this plan** (will be in Plan 2-4):
- Any UI (Avalonia)
- Application Service implementations (only Domain + Data here)
- Backup/Restore logic
- PDF/Excel export
- Inno Setup installer
- Cost rollup business logic (only repo-level tree primitives here)
