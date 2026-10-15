





# Plan 06: Settings + Backup/Restore + Inno Setup Installer (MVP Final)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Plan cuối của MVP v1.0. Triển khai 3 mảng còn lại để app sẵn sàng phân phối: (1) Settings UI với 3 tab — thông tin cơ quan, quản lý backup, tùy chọn chung; (2) Backup/Restore qua file `.cfb` (ZIP) — manual + auto-on-exit + safety pre-restore backup; (3) Inno Setup script + `dotnet publish` self-contained → ra `setup.exe` cài đặt trên Windows. Kết thúc plan: user có thể tải `setup.exe`, cài lên Windows 10/11, dùng đầy đủ chức năng MVP, backup dữ liệu, chia sẻ qua USB.

**Architecture:**
- `IAppSettingService` (Application) wraps `AppSetting` entity (key-value) với typed accessors.
- `ICompanyInfoService` (Application) là facade tiện dụng cho 6 key thông dụng (tên cơ quan, địa chỉ, logo path, người ký, chức vụ, nơi nhận).
- `IBackupService` + `IRestoreService` (Infrastructure) làm I/O thuần — không phụ thuộc Avalonia.
- Backup workflow: close DB → VACUUM → ZIP db + attachments + manifest + checksum → reopen DB.
- Restore workflow: validate manifest → pre-restore safety backup → close DB → atomic file swap → reopen DB → run pending migrations.
- `SettingsViewModel` + 3 sub-VM (Company / Backup / Preferences). Mở từ menu "Cài đặt" trong shell.
- Auto-backup-on-exit hook trong `App.axaml.cs` shutdown event.

**Tech Stack:** Đã có. Thêm 1 package `System.IO.Compression` (built-in .NET 8, không cần NuGet) cho ZIP. Inno Setup là tool ngoài (free, MPL-like).

**Reference:** [Design doc](../specs/2026-05-19-construction-finance-app-design.md) sections 4.12 (Settings module), 5.4 (`.cfb` format chi tiết), 5.5 (auto-backup), 8 (cài đặt & phân phối). Builds on [Plan 01](2026-05-19-plan-01-foundation.md) (`AppSetting` entity), [Plan 05](2026-05-27-plan-05-cost-tree-payments.md) (latest UI baseline).

**Out of scope** (v1.1+):
- Daily scheduled backup (cần background scheduler — bỏ ở MVP)
- Pre-migration auto-backup (rare event, manual OK trước khi update app)
- Encryption AES-256 cho `.cfb` (đã quyết "No" ở Plan brainstorm)
- Auto-update qua Velopack (Phase 2 sau MVP)
- Restore merge mode (import dự án từ backup khác) — design doc 5.6, defer v1.2
- Theme switching (light/dark) hot-reload — chỉ "system default" ở MVP
- Multi-language (chỉ tiếng Việt hard-coded)
- Logo upload (chỉ text path placeholder ở MVP, file picker → v1.1)

---

## Key Decisions (flag — phản hồi nếu không đồng ý)

| # | Quyết định | Lý do |
|---|---|---|
| D1 | `IBackupService`/`IRestoreService` ở **Infrastructure** project (chưa dùng project này) | Đúng Clean Architecture — I/O đi vào Infrastructure. Application chỉ biết interface |
| D2 | Manifest dùng `System.Text.Json` indented format | Built-in .NET, không cần Newtonsoft. Pretty-print giúp debug `.cfb` từ ngoài |
| D3 | SHA256 checksum lưu hex lowercase (64 ký tự) | Chuẩn — dễ verify bằng `sha256sum app.db` từ CLI |
| D4 | Pre-restore safety backup **luôn luôn** tạo, không cho user disable | An toàn — restore là destructive, mất 5 giây tạo backup nhỏ là đáng |
| D5 | Auto-backup-on-exit: **chỉ chạy nếu có thay đổi** trong session (track qua `AppSetting` key `last_modified_at`) | Tránh spam thư mục backup nếu user chỉ mở app xem |
| D6 | Settings lưu trong `app_settings` table (đã có từ Plan 01) — KHÔNG dùng JSON file ngoài | Nhất quán với DB; backup `.cfb` cuốn theo settings tự động |
| D7 | Inno Setup chạy bằng `iscc.exe` — cần Windows hoặc Wine để build installer | Linux dev có thể build self-contained `.exe` của app, nhưng wrap installer phải Windows/Wine. Tạo task riêng cho CI Windows |
| D8 | Publish mode: **self-contained + single-file + ready-to-run** | Đúng spec design doc 4.5 — user không cần cài .NET runtime |
| D9 | Backup file extension `.cfb` (Construction Finance Backup) — đăng ký file association trong Inno Setup | Double-click .cfb mở app → restore wizard |
| D10 | Restore từ backup phiên bản schema cũ hơn → **chạy migration tự động sau khi restore**. Phiên bản schema mới hơn → **refuse** | An toàn cho forward compat, chặn backward incompat |

---

## File Structure

```
src/ConstructionFinance.Application/
├── DTOs/
│   ├── AppSettingDtos.cs                       (NEW)
│   └── CompanyInfoDto.cs                       (NEW)
├── Services/Interfaces/
│   ├── IAppSettingService.cs                   (NEW)
│   └── ICompanyInfoService.cs                  (NEW)
├── Services/Implementations/
│   ├── AppSettingService.cs                    (NEW)
│   └── CompanyInfoService.cs                   (NEW)
└── DependencyInjection.cs                      (MODIFIED — register 2 services)

src/ConstructionFinance.Infrastructure/
├── ConstructionFinance.Infrastructure.csproj   (MODIFIED — package refs)
├── Backup/
│   ├── BackupManifest.cs                       (NEW — JSON model)
│   ├── BackupFileInfo.cs                       (NEW — listing model)
│   ├── IBackupService.cs                       (NEW)
│   ├── BackupService.cs                        (NEW — create .cfb)
│   ├── IRestoreService.cs                      (NEW)
│   └── RestoreService.cs                       (NEW — restore .cfb)
└── DependencyInjection.cs                      (NEW — AddInfrastructureServices)

src/ConstructionFinance.Desktop/
├── Common/HostBuilder.cs                       (MODIFIED — register Settings VMs + Infrastructure)
├── App.axaml.cs                                 (MODIFIED — auto-backup-on-exit)
├── ViewModels/Settings/
│   ├── SettingsViewModel.cs                    (NEW — TabControl host)
│   ├── CompanyInfoViewModel.cs                 (NEW)
│   ├── BackupManagementViewModel.cs            (NEW)
│   └── PreferencesViewModel.cs                 (NEW)
└── Views/Settings/
    ├── SettingsView.axaml(.cs)                 (NEW)
    ├── CompanyInfoTab.axaml(.cs)               (NEW)
    ├── BackupTab.axaml(.cs)                    (NEW)
    └── PreferencesTab.axaml(.cs)               (NEW)

tests/ConstructionFinance.Application.Tests/
├── AppSettingServiceTests.cs                   (NEW)
└── CompanyInfoServiceTests.cs                  (NEW)

tests/ConstructionFinance.Infrastructure.Tests/  (NEW PROJECT)
├── ConstructionFinance.Infrastructure.Tests.csproj
├── BackupServiceTests.cs
└── RestoreServiceTests.cs

tests/ConstructionFinance.Desktop.Tests/
└── Settings/
    ├── CompanyInfoViewModelTests.cs            (NEW)
    ├── BackupManagementViewModelTests.cs       (NEW)
    └── PreferencesViewModelTests.cs            (NEW)

installer/
├── inno-setup.iss                              (NEW — Inno Setup script)
├── assets/
│   ├── icon.ico                                (placeholder — 256x256 binary; provide your own)
│   └── license.txt                             (NEW)
└── build-installer.md                          (NEW — Windows-only build instructions)

.github/workflows/
└── release.yml                                  (NEW — manual workflow for publishing Windows .exe)
```

---

## Pre-flight

- [ ] **Step 0.1: Verify Plan 05 committed and green**

Run from `/home/hieu-nguyen/management/`:
```bash
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
git status --short
dotnet build --configuration Release
dotnet test --configuration Release
```
Expected: Working tree clean (commit Plan 05 first if not). Build 0 errors. 208 tests pass.

---

## Task 1: `AppSettingService` + `CompanyInfoService` (Application)

`AppSetting` entity exists from Plan 01 (key=string PK, value=string). We add typed access.

**Files:**
- Create: `src/ConstructionFinance.Application/DTOs/AppSettingDtos.cs`
- Create: `src/ConstructionFinance.Application/DTOs/CompanyInfoDto.cs`
- Create: `src/ConstructionFinance.Application/Services/Interfaces/IAppSettingService.cs`
- Create: `src/ConstructionFinance.Application/Services/Implementations/AppSettingService.cs`
- Create: `src/ConstructionFinance.Application/Services/Interfaces/ICompanyInfoService.cs`
- Create: `src/ConstructionFinance.Application/Services/Implementations/CompanyInfoService.cs`
- Modify: `src/ConstructionFinance.Application/DependencyInjection.cs`
- Test: `tests/ConstructionFinance.Application.Tests/AppSettingServiceTests.cs`
- Test: `tests/ConstructionFinance.Application.Tests/CompanyInfoServiceTests.cs`

- [ ] **Step 1.1: Failing test — AppSettingService**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Application.Tests/AppSettingServiceTests.cs`:
```csharp
using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Entities;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class AppSettingServiceTests
{
    private static IAppSettingService CreateService(AppDbContext ctx) =>
        new AppSettingService(new Repository<AppSetting>(ctx), new UnitOfWork(ctx));

    [Fact]
    public async Task GetAsync_returns_null_when_missing()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);

        (await svc.GetAsync("nonexistent")).ShouldBeNull();
    }

    [Fact]
    public async Task SetAsync_inserts_when_missing()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);

        await svc.SetAsync("company_name", "Sở Xây dựng Tỉnh ABC");

        (await svc.GetAsync("company_name")).ShouldBe("Sở Xây dựng Tỉnh ABC");
    }

    [Fact]
    public async Task SetAsync_updates_when_exists()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);
        await svc.SetAsync("k", "v1");

        await svc.SetAsync("k", "v2");

        (await svc.GetAsync("k")).ShouldBe("v2");
    }

    [Fact]
    public async Task GetAllAsync_returns_all_key_value_pairs()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);
        await svc.SetAsync("a", "1");
        await svc.SetAsync("b", "2");

        var all = await svc.GetAllAsync();
        all.ShouldContain(s => s.Key == "a" && s.Value == "1");
        all.ShouldContain(s => s.Key == "b" && s.Value == "2");
    }
}
```

- [ ] **Step 1.2: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~AppSettingServiceTests"`

- [ ] **Step 1.3: Implement AppSettingDtos**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/DTOs/AppSettingDtos.cs`:
```csharp
namespace ConstructionFinance.Application.DTOs;

public record AppSettingDto(string Key, string? Value, string? Description);
```

- [ ] **Step 1.4: Implement IAppSettingService + impl**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Services/Interfaces/IAppSettingService.cs`:
```csharp
using ConstructionFinance.Application.DTOs;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IAppSettingService
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string? value, string? description = null, CancellationToken ct = default);
    Task<IReadOnlyList<AppSettingDto>> GetAllAsync(CancellationToken ct = default);
}
```

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Services/Implementations/AppSettingService.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Application.Services.Implementations;

public class AppSettingService : IAppSettingService
{
    private readonly IRepository<AppSetting> _repo;
    private readonly IUnitOfWork _uow;

    public AppSettingService(IRepository<AppSetting> repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var matches = await _repo.FindAsync(s => s.Key == key, ct);
        return matches.FirstOrDefault()?.Value;
    }

    public async Task SetAsync(string key, string? value, string? description = null, CancellationToken ct = default)
    {
        var matches = await _repo.FindAsync(s => s.Key == key, ct);
        var existing = matches.FirstOrDefault();
        if (existing is null)
        {
            await _repo.AddAsync(new AppSetting
            {
                Key = key,
                Value = value,
                Description = description,
                UpdatedAt = DateTime.UtcNow
            }, ct);
        }
        else
        {
            existing.Value = value;
            existing.Description = description ?? existing.Description;
            existing.UpdatedAt = DateTime.UtcNow;
            _repo.Update(existing);
        }
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AppSettingDto>> GetAllAsync(CancellationToken ct = default)
    {
        var all = await _repo.ListAsync(ct);
        return all.Select(s => new AppSettingDto(s.Key, s.Value, s.Description)).ToList();
    }
}
```

- [ ] **Step 1.5: Failing test — CompanyInfoService**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Application.Tests/CompanyInfoServiceTests.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Implementations;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Entities;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests;

public class CompanyInfoServiceTests
{
    private static ICompanyInfoService CreateService(AppDbContext ctx)
    {
        var settings = new AppSettingService(new Repository<AppSetting>(ctx), new UnitOfWork(ctx));
        return new CompanyInfoService(settings);
    }

    [Fact]
    public async Task GetAsync_returns_empty_defaults_when_unset()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);

        var info = await svc.GetAsync();

        info.CompanyName.ShouldBeNullOrEmpty();
        info.LogoPath.ShouldBeNullOrEmpty();
    }

    [Fact]
    public async Task SaveAsync_then_GetAsync_round_trips_all_fields()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);

        var input = new CompanyInfoDto(
            CompanyName: "Sở Xây dựng",
            CompanyAddress: "123 Lê Lợi",
            CompanyPhone: "024-3xxx",
            CompanyEmail: "info@sxd.gov.vn",
            LogoPath: "C:/logos/sxd.png",
            DefaultSignerName: "Nguyễn Văn A",
            DefaultSignerTitle: "Giám đốc Sở",
            DefaultRecipients: "- UBND tỉnh\n- Lưu VT");
        await svc.SaveAsync(input);

        var output = await svc.GetAsync();
        output.CompanyName.ShouldBe("Sở Xây dựng");
        output.LogoPath.ShouldBe("C:/logos/sxd.png");
        output.DefaultSignerTitle.ShouldBe("Giám đốc Sở");
    }
}
```

- [ ] **Step 1.6: Implement CompanyInfoDto**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/DTOs/CompanyInfoDto.cs`:
```csharp
namespace ConstructionFinance.Application.DTOs;

public record CompanyInfoDto(
    string? CompanyName,
    string? CompanyAddress,
    string? CompanyPhone,
    string? CompanyEmail,
    string? LogoPath,
    string? DefaultSignerName,
    string? DefaultSignerTitle,
    string? DefaultRecipients);
```

- [ ] **Step 1.7: Implement ICompanyInfoService + impl**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Services/Interfaces/ICompanyInfoService.cs`:
```csharp
using ConstructionFinance.Application.DTOs;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface ICompanyInfoService
{
    Task<CompanyInfoDto> GetAsync(CancellationToken ct = default);
    Task SaveAsync(CompanyInfoDto info, CancellationToken ct = default);
}
```

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Application/Services/Implementations/CompanyInfoService.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;

namespace ConstructionFinance.Application.Services.Implementations;

public class CompanyInfoService : ICompanyInfoService
{
    private const string KeyCompanyName = "company_name";
    private const string KeyCompanyAddress = "company_address";
    private const string KeyCompanyPhone = "company_phone";
    private const string KeyCompanyEmail = "company_email";
    private const string KeyLogoPath = "company_logo_path";
    private const string KeySignerName = "default_signer_name";
    private const string KeySignerTitle = "default_signer_title";
    private const string KeyRecipients = "default_recipients";

    private readonly IAppSettingService _settings;

    public CompanyInfoService(IAppSettingService settings) => _settings = settings;

    public async Task<CompanyInfoDto> GetAsync(CancellationToken ct = default) => new(
        CompanyName: await _settings.GetAsync(KeyCompanyName, ct),
        CompanyAddress: await _settings.GetAsync(KeyCompanyAddress, ct),
        CompanyPhone: await _settings.GetAsync(KeyCompanyPhone, ct),
        CompanyEmail: await _settings.GetAsync(KeyCompanyEmail, ct),
        LogoPath: await _settings.GetAsync(KeyLogoPath, ct),
        DefaultSignerName: await _settings.GetAsync(KeySignerName, ct),
        DefaultSignerTitle: await _settings.GetAsync(KeySignerTitle, ct),
        DefaultRecipients: await _settings.GetAsync(KeyRecipients, ct));

    public async Task SaveAsync(CompanyInfoDto info, CancellationToken ct = default)
    {
        await _settings.SetAsync(KeyCompanyName, info.CompanyName, ct: ct);
        await _settings.SetAsync(KeyCompanyAddress, info.CompanyAddress, ct: ct);
        await _settings.SetAsync(KeyCompanyPhone, info.CompanyPhone, ct: ct);
        await _settings.SetAsync(KeyCompanyEmail, info.CompanyEmail, ct: ct);
        await _settings.SetAsync(KeyLogoPath, info.LogoPath, ct: ct);
        await _settings.SetAsync(KeySignerName, info.DefaultSignerName, ct: ct);
        await _settings.SetAsync(KeySignerTitle, info.DefaultSignerTitle, ct: ct);
        await _settings.SetAsync(KeyRecipients, info.DefaultRecipients, ct: ct);
    }
}
```

- [ ] **Step 1.8: Register in DI**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Application/DependencyInjection.cs`. Add to the Services group:
```csharp
        services.AddScoped<IAppSettingService, AppSettingService>();
        services.AddScoped<ICompanyInfoService, CompanyInfoService>();
```

- [ ] **Step 1.9: Verify + commit**

```bash
dotnet test tests/ConstructionFinance.Application.Tests/ --filter "FullyQualifiedName~Setting|FullyQualifiedName~CompanyInfo"
git add . && git commit -m "feat(app): AppSettingService + CompanyInfoService (key-value settings)"
```

---

## Task 2: Create `Infrastructure.Tests` project + `BackupManifest` + `BackupFileInfo`

**Files:**
- New test project: `tests/ConstructionFinance.Infrastructure.Tests/`
- Create: `src/ConstructionFinance.Infrastructure/Backup/BackupManifest.cs`
- Create: `src/ConstructionFinance.Infrastructure/Backup/BackupFileInfo.cs`

- [ ] **Step 2.1: Create Infrastructure.Tests project + references**

Run from `/home/hieu-nguyen/management/`:
```bash
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
cd tests
dotnet new xunit -n ConstructionFinance.Infrastructure.Tests -o ConstructionFinance.Infrastructure.Tests
cd ..
rm -f tests/ConstructionFinance.Infrastructure.Tests/UnitTest1.cs
dotnet sln add tests/ConstructionFinance.Infrastructure.Tests/ConstructionFinance.Infrastructure.Tests.csproj
dotnet add tests/ConstructionFinance.Infrastructure.Tests/ConstructionFinance.Infrastructure.Tests.csproj reference src/ConstructionFinance.Infrastructure/ConstructionFinance.Infrastructure.csproj
dotnet add tests/ConstructionFinance.Infrastructure.Tests/ConstructionFinance.Infrastructure.Tests.csproj reference src/ConstructionFinance.Application/ConstructionFinance.Application.csproj
dotnet add tests/ConstructionFinance.Infrastructure.Tests/ConstructionFinance.Infrastructure.Tests.csproj reference src/ConstructionFinance.Data/ConstructionFinance.Data.csproj
dotnet add tests/ConstructionFinance.Infrastructure.Tests/ConstructionFinance.Infrastructure.Tests.csproj reference src/ConstructionFinance.Domain/ConstructionFinance.Domain.csproj
```

- [ ] **Step 2.2: Set test packages**

Replace the test `<ItemGroup>` in `/home/hieu-nguyen/management/tests/ConstructionFinance.Infrastructure.Tests/ConstructionFinance.Infrastructure.Tests.csproj` with:
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

- [ ] **Step 2.3: Implement BackupManifest**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Infrastructure/Backup/BackupManifest.cs`:
```csharp
namespace ConstructionFinance.Infrastructure.Backup;

public record BackupManifest(
    string FormatVersion,
    string AppVersion,
    string DbSchemaVersion,
    DateTime CreatedAtUtc,
    BackupCreator CreatedBy,
    BackupDataSummary DataSummary,
    long DbSizeBytes,
    string BackupType,
    string? Description,
    string? CompanyName);

public record BackupCreator(string MachineName, string UserName, string OsVersion);

public record BackupDataSummary(
    int ApprovalDecisions,
    int Contracts,
    int Projects,
    int CostItems,
    int Payments,
    int Vendors,
    int AttachmentsCount,
    long AttachmentsTotalSizeBytes);
```

- [ ] **Step 2.4: Implement BackupFileInfo**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Infrastructure/Backup/BackupFileInfo.cs`:
```csharp
namespace ConstructionFinance.Infrastructure.Backup;

public record BackupFileInfo(
    string FullPath,
    string FileName,
    DateTime CreatedAtUtc,
    long SizeBytes,
    string BackupType,
    string? Description);
```

- [ ] **Step 2.5: Build + commit**

```bash
dotnet build
git add . && git commit -m "feat(infra): create Infrastructure.Tests + BackupManifest/BackupFileInfo models"
```

---

## Task 3: `IBackupService` + `BackupService` (create `.cfb`)

**Files:**
- Create: `src/ConstructionFinance.Infrastructure/Backup/IBackupService.cs`
- Create: `src/ConstructionFinance.Infrastructure/Backup/BackupService.cs`
- Test: `tests/ConstructionFinance.Infrastructure.Tests/BackupServiceTests.cs`

- [ ] **Step 3.1: Failing tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Infrastructure.Tests/BackupServiceTests.cs`:
```csharp
using System.IO.Compression;
using System.Text.Json;
using ConstructionFinance.Infrastructure.Backup;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Infrastructure.Tests;

public class BackupServiceTests : IDisposable
{
    private readonly string _workDir;
    private readonly string _dbPath;
    private readonly string _attachmentsDir;
    private readonly string _backupDir;

    public BackupServiceTests()
    {
        _workDir = Path.Combine(Path.GetTempPath(), $"cf-backup-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workDir);
        _dbPath = Path.Combine(_workDir, "app.db");
        _attachmentsDir = Path.Combine(_workDir, "attachments");
        _backupDir = Path.Combine(_workDir, "Backups");
        Directory.CreateDirectory(_attachmentsDir);
        Directory.CreateDirectory(_backupDir);

        // Create a fake "DB" file and one attachment
        File.WriteAllText(_dbPath, "FAKE DB CONTENT");
        Directory.CreateDirectory(Path.Combine(_attachmentsDir, "contract", "1"));
        File.WriteAllText(Path.Combine(_attachmentsDir, "contract", "1", "doc.pdf"), "FAKE PDF");
    }

    public void Dispose()
    {
        if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true);
    }

    private static BackupManifest SampleManifest() => new(
        FormatVersion: "1.0",
        AppVersion: "1.0.0",
        DbSchemaVersion: "20260519_001",
        CreatedAtUtc: DateTime.UtcNow,
        CreatedBy: new BackupCreator("TEST-PC", "tester", "TestOS"),
        DataSummary: new BackupDataSummary(1, 1, 1, 10, 5, 2, 1, 100),
        DbSizeBytes: 1024,
        BackupType: "manual",
        Description: "test",
        CompanyName: "Test Org");

    [Fact]
    public async Task CreateBackupAsync_produces_cfb_file_in_backup_dir()
    {
        var svc = new BackupService();
        var manifest = SampleManifest();

        var result = await svc.CreateBackupAsync(_dbPath, _attachmentsDir, _backupDir, manifest);

        result.FullPath.ShouldNotBeNullOrEmpty();
        File.Exists(result.FullPath).ShouldBeTrue();
        result.FileName.ShouldEndWith(".cfb");
        result.BackupType.ShouldBe("manual");
    }

    [Fact]
    public async Task CreateBackupAsync_zip_contains_required_entries()
    {
        var svc = new BackupService();
        var result = await svc.CreateBackupAsync(_dbPath, _attachmentsDir, _backupDir, SampleManifest());

        using var zip = ZipFile.OpenRead(result.FullPath);
        var entryNames = zip.Entries.Select(e => e.FullName).ToList();
        entryNames.ShouldContain("manifest.json");
        entryNames.ShouldContain("app.db");
        entryNames.ShouldContain("checksum.txt");
        entryNames.ShouldContain(e => e.StartsWith("attachments/"));
    }

    [Fact]
    public async Task CreateBackupAsync_checksum_matches_db_sha256()
    {
        var svc = new BackupService();
        var result = await svc.CreateBackupAsync(_dbPath, _attachmentsDir, _backupDir, SampleManifest());

        using var zip = ZipFile.OpenRead(result.FullPath);
        var checksumEntry = zip.GetEntry("checksum.txt");
        checksumEntry.ShouldNotBeNull();
        using var reader = new StreamReader(checksumEntry!.Open());
        var actualChecksum = (await reader.ReadToEndAsync()).Trim();

        // Compute expected
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var fs = File.OpenRead(_dbPath);
        var expected = Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
        actualChecksum.ShouldBe(expected);
    }

    [Fact]
    public async Task CreateBackupAsync_manifest_round_trips_through_json()
    {
        var svc = new BackupService();
        var inputManifest = SampleManifest();
        var result = await svc.CreateBackupAsync(_dbPath, _attachmentsDir, _backupDir, inputManifest);

        using var zip = ZipFile.OpenRead(result.FullPath);
        var manifestEntry = zip.GetEntry("manifest.json")!;
        using var reader = new StreamReader(manifestEntry.Open());
        var json = await reader.ReadToEndAsync();
        var parsed = JsonSerializer.Deserialize<BackupManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        parsed.ShouldNotBeNull();
        parsed!.AppVersion.ShouldBe("1.0.0");
        parsed.DataSummary.CostItems.ShouldBe(10);
    }

    [Fact]
    public void ListBackupsAsync_returns_cfb_files_sorted_by_created_desc()
    {
        // Pre-create 2 fake .cfb files with different mtimes
        var oldPath = Path.Combine(_backupDir, "backup_20260101_120000_manual.cfb");
        var newPath = Path.Combine(_backupDir, "backup_20260520_120000_manual.cfb");
        File.WriteAllText(oldPath, "");
        File.WriteAllText(newPath, "");
        File.SetLastWriteTimeUtc(oldPath, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(newPath, new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc));

        var svc = new BackupService();
        var list = svc.ListBackups(_backupDir);

        list.Count.ShouldBe(2);
        list[0].FullPath.ShouldBe(newPath); // newest first
    }
}
```

- [ ] **Step 3.2: Run — fails**

Run: `dotnet test tests/ConstructionFinance.Infrastructure.Tests/`

- [ ] **Step 3.3: Implement IBackupService**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Infrastructure/Backup/IBackupService.cs`:
```csharp
namespace ConstructionFinance.Infrastructure.Backup;

public interface IBackupService
{
    Task<BackupFileInfo> CreateBackupAsync(
        string sourceDbPath,
        string attachmentsDir,
        string backupDir,
        BackupManifest manifest,
        CancellationToken ct = default);

    IReadOnlyList<BackupFileInfo> ListBackups(string backupDir);
}
```

- [ ] **Step 3.4: Implement BackupService**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Infrastructure/Backup/BackupService.cs`:
```csharp
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace ConstructionFinance.Infrastructure.Backup;

public class BackupService : IBackupService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public async Task<BackupFileInfo> CreateBackupAsync(
        string sourceDbPath,
        string attachmentsDir,
        string backupDir,
        BackupManifest manifest,
        CancellationToken ct = default)
    {
        if (!File.Exists(sourceDbPath))
            throw new FileNotFoundException("Source DB not found", sourceDbPath);
        Directory.CreateDirectory(backupDir);

        var stamp = manifest.CreatedAtUtc.ToString("yyyyMMdd_HHmmss");
        var fileName = $"backup_{stamp}_{manifest.BackupType}.cfb";
        var targetPath = Path.Combine(backupDir, fileName);

        // SHA256 of the DB file
        var checksum = await ComputeSha256Async(sourceDbPath, ct);

        await using (var zipStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            // 1. manifest.json
            var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
            await using (var w = new StreamWriter(manifestEntry.Open()))
            {
                await w.WriteAsync(JsonSerializer.Serialize(manifest, JsonOpts));
            }

            // 2. app.db
            archive.CreateEntryFromFile(sourceDbPath, "app.db", CompressionLevel.Optimal);

            // 3. checksum.txt
            var checksumEntry = archive.CreateEntry("checksum.txt", CompressionLevel.Optimal);
            await using (var w = new StreamWriter(checksumEntry.Open()))
            {
                await w.WriteAsync(checksum);
            }

            // 4. attachments/**
            if (Directory.Exists(attachmentsDir))
            {
                foreach (var file in Directory.EnumerateFiles(attachmentsDir, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(attachmentsDir, file).Replace(Path.DirectorySeparatorChar, '/');
                    archive.CreateEntryFromFile(file, $"attachments/{rel}", CompressionLevel.Optimal);
                }
            }
        }

        return new BackupFileInfo(
            FullPath: targetPath,
            FileName: fileName,
            CreatedAtUtc: manifest.CreatedAtUtc,
            SizeBytes: new FileInfo(targetPath).Length,
            BackupType: manifest.BackupType,
            Description: manifest.Description);
    }

    public IReadOnlyList<BackupFileInfo> ListBackups(string backupDir)
    {
        if (!Directory.Exists(backupDir)) return Array.Empty<BackupFileInfo>();
        return Directory.EnumerateFiles(backupDir, "*.cfb")
            .Select(p =>
            {
                var fi = new FileInfo(p);
                var name = fi.Name;
                var type = ParseBackupType(name);
                return new BackupFileInfo(
                    FullPath: p,
                    FileName: name,
                    CreatedAtUtc: fi.LastWriteTimeUtc,
                    SizeBytes: fi.Length,
                    BackupType: type,
                    Description: null);
            })
            .OrderByDescending(b => b.CreatedAtUtc)
            .ToList();
    }

    private static string ParseBackupType(string fileName)
    {
        // backup_YYYYMMDD_HHmmss_TYPE.cfb
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var parts = stem.Split('_');
        return parts.Length >= 4 ? parts[^1] : "unknown";
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

- [ ] **Step 3.5: Verify + commit**

```bash
dotnet test tests/ConstructionFinance.Infrastructure.Tests/
git add . && git commit -m "feat(infra): BackupService — create .cfb (manifest + db + checksum + attachments)"
```

---

## Task 4: `IRestoreService` + `RestoreService`

**Files:**
- Create: `src/ConstructionFinance.Infrastructure/Backup/IRestoreService.cs`
- Create: `src/ConstructionFinance.Infrastructure/Backup/RestoreService.cs`
- Test: `tests/ConstructionFinance.Infrastructure.Tests/RestoreServiceTests.cs`

Restore validates format/schema version compatibility, verifies checksum, then atomically swaps the DB file. Pre-restore safety backup is created before destructive swap.

- [ ] **Step 4.1: Failing tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Infrastructure.Tests/RestoreServiceTests.cs`:
```csharp
using ConstructionFinance.Infrastructure.Backup;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Infrastructure.Tests;

public class RestoreServiceTests : IDisposable
{
    private readonly string _workDir;
    private readonly string _dbPath;
    private readonly string _attachmentsDir;
    private readonly string _backupDir;

    public RestoreServiceTests()
    {
        _workDir = Path.Combine(Path.GetTempPath(), $"cf-restore-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workDir);
        _dbPath = Path.Combine(_workDir, "app.db");
        _attachmentsDir = Path.Combine(_workDir, "attachments");
        _backupDir = Path.Combine(_workDir, "Backups");
        Directory.CreateDirectory(_attachmentsDir);
        Directory.CreateDirectory(_backupDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true);
    }

    private async Task<string> CreateSampleBackup(string dbContent = "ORIGINAL", string schemaVersion = "20260519_001")
    {
        File.WriteAllText(_dbPath, dbContent);
        Directory.CreateDirectory(Path.Combine(_attachmentsDir, "contract", "1"));
        File.WriteAllText(Path.Combine(_attachmentsDir, "contract", "1", "x.pdf"), "PDF");
        var manifest = new BackupManifest(
            FormatVersion: "1.0",
            AppVersion: "1.0.0",
            DbSchemaVersion: schemaVersion,
            CreatedAtUtc: DateTime.UtcNow,
            CreatedBy: new BackupCreator("X", "y", "z"),
            DataSummary: new BackupDataSummary(0, 0, 0, 0, 0, 0, 0, 0),
            DbSizeBytes: dbContent.Length,
            BackupType: "manual",
            Description: null,
            CompanyName: null);
        var info = await new BackupService().CreateBackupAsync(_dbPath, _attachmentsDir, _backupDir, manifest);
        return info.FullPath;
    }

    [Fact]
    public async Task ReadManifestAsync_returns_manifest_for_valid_backup()
    {
        var path = await CreateSampleBackup();
        var svc = new RestoreService();

        var manifest = await svc.ReadManifestAsync(path);

        manifest.ShouldNotBeNull();
        manifest.AppVersion.ShouldBe("1.0.0");
    }

    [Fact]
    public async Task VerifyChecksumAsync_returns_true_when_valid()
    {
        var path = await CreateSampleBackup();
        (await new RestoreService().VerifyChecksumAsync(path)).ShouldBeTrue();
    }

    [Fact]
    public async Task RestoreAsync_replaces_db_and_attachments()
    {
        var path = await CreateSampleBackup(dbContent: "FROM_BACKUP");
        File.WriteAllText(_dbPath, "CURRENT"); // simulate current DB
        var svc = new RestoreService();

        await svc.RestoreAsync(path, _dbPath, _attachmentsDir, _backupDir, currentSchemaVersion: "20260519_001");

        File.ReadAllText(_dbPath).ShouldBe("FROM_BACKUP");
        File.Exists(Path.Combine(_attachmentsDir, "contract", "1", "x.pdf")).ShouldBeTrue();
    }

    [Fact]
    public async Task RestoreAsync_creates_pre_restore_safety_backup()
    {
        var path = await CreateSampleBackup();
        File.WriteAllText(_dbPath, "CURRENT_BEFORE_RESTORE");
        var svc = new RestoreService();

        await svc.RestoreAsync(path, _dbPath, _attachmentsDir, _backupDir, currentSchemaVersion: "20260519_001");

        var preRestoreFiles = Directory.GetFiles(_backupDir, "*pre_restore*.cfb");
        preRestoreFiles.Length.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task RestoreAsync_throws_when_backup_schema_newer_than_app()
    {
        var path = await CreateSampleBackup(schemaVersion: "20270101_999");
        File.WriteAllText(_dbPath, "CURRENT");
        var svc = new RestoreService();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await svc.RestoreAsync(path, _dbPath, _attachmentsDir, _backupDir, currentSchemaVersion: "20260519_001"));
    }
}
```

- [ ] **Step 4.2: Run — fails**

- [ ] **Step 4.3: Implement IRestoreService**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Infrastructure/Backup/IRestoreService.cs`:
```csharp
namespace ConstructionFinance.Infrastructure.Backup;

public interface IRestoreService
{
    Task<BackupManifest?> ReadManifestAsync(string cfbPath, CancellationToken ct = default);
    Task<bool> VerifyChecksumAsync(string cfbPath, CancellationToken ct = default);
    Task RestoreAsync(
        string cfbPath,
        string targetDbPath,
        string targetAttachmentsDir,
        string backupDir,
        string currentSchemaVersion,
        CancellationToken ct = default);
}
```

- [ ] **Step 4.4: Implement RestoreService**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Infrastructure/Backup/RestoreService.cs`:
```csharp
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace ConstructionFinance.Infrastructure.Backup;

public class RestoreService : IRestoreService
{
    public async Task<BackupManifest?> ReadManifestAsync(string cfbPath, CancellationToken ct = default)
    {
        using var zip = ZipFile.OpenRead(cfbPath);
        var entry = zip.GetEntry("manifest.json");
        if (entry is null) return null;
        await using var stream = entry.Open();
        return await JsonSerializer.DeserializeAsync<BackupManifest>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);
    }

    public async Task<bool> VerifyChecksumAsync(string cfbPath, CancellationToken ct = default)
    {
        using var zip = ZipFile.OpenRead(cfbPath);
        var checksumEntry = zip.GetEntry("checksum.txt");
        var dbEntry = zip.GetEntry("app.db");
        if (checksumEntry is null || dbEntry is null) return false;

        string expected;
        await using (var reader = checksumEntry.Open())
        using (var sr = new StreamReader(reader))
            expected = (await sr.ReadToEndAsync(ct)).Trim();

        await using var dbStream = dbEntry.Open();
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(dbStream, ct);
        var actual = Convert.ToHexString(hash).ToLowerInvariant();

        return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
    }

    public async Task RestoreAsync(
        string cfbPath,
        string targetDbPath,
        string targetAttachmentsDir,
        string backupDir,
        string currentSchemaVersion,
        CancellationToken ct = default)
    {
        // 1. Read & validate manifest
        var manifest = await ReadManifestAsync(cfbPath, ct)
            ?? throw new InvalidOperationException("Manifest không hợp lệ.");
        if (string.Compare(manifest.DbSchemaVersion, currentSchemaVersion, StringComparison.Ordinal) > 0)
            throw new InvalidOperationException(
                $"File backup có schema mới hơn ({manifest.DbSchemaVersion}) phiên bản app đang chạy ({currentSchemaVersion}). " +
                "Vui lòng cập nhật app trước khi restore.");

        // 2. Verify checksum
        if (!await VerifyChecksumAsync(cfbPath, ct))
            throw new InvalidOperationException("Checksum không khớp — file backup có thể bị hỏng.");

        // 3. Pre-restore safety backup of CURRENT state (if current DB exists)
        if (File.Exists(targetDbPath))
        {
            var safetyManifest = new BackupManifest(
                FormatVersion: "1.0",
                AppVersion: manifest.AppVersion,
                DbSchemaVersion: currentSchemaVersion,
                CreatedAtUtc: DateTime.UtcNow,
                CreatedBy: new BackupCreator(Environment.MachineName, Environment.UserName, Environment.OSVersion.ToString()),
                DataSummary: new BackupDataSummary(0, 0, 0, 0, 0, 0, 0, 0),
                DbSizeBytes: new FileInfo(targetDbPath).Length,
                BackupType: "pre_restore",
                Description: $"Auto safety backup before restoring {Path.GetFileName(cfbPath)}",
                CompanyName: manifest.CompanyName);
            await new BackupService().CreateBackupAsync(targetDbPath, targetAttachmentsDir, backupDir, safetyManifest, ct);
        }

        // 4. Atomic swap: extract to staging, then move into place
        var stagingDir = Path.Combine(Path.GetTempPath(), $"cf-restore-staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDir);
        try
        {
            ZipFile.ExtractToDirectory(cfbPath, stagingDir);

            // Replace DB
            var stagingDb = Path.Combine(stagingDir, "app.db");
            if (!File.Exists(stagingDb))
                throw new InvalidOperationException("Backup không chứa app.db.");
            if (File.Exists(targetDbPath)) File.Delete(targetDbPath);
            File.Move(stagingDb, targetDbPath);

            // Replace attachments — wipe existing dir + move from staging
            if (Directory.Exists(targetAttachmentsDir))
                Directory.Delete(targetAttachmentsDir, recursive: true);
            Directory.CreateDirectory(targetAttachmentsDir);
            var stagingAttachments = Path.Combine(stagingDir, "attachments");
            if (Directory.Exists(stagingAttachments))
            {
                CopyDirectory(stagingAttachments, targetAttachmentsDir);
            }
        }
        finally
        {
            if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, recursive: true);
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        foreach (var dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, dir)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)), overwrite: true);
    }
}
```

- [ ] **Step 4.5: Verify + commit**

```bash
dotnet test tests/ConstructionFinance.Infrastructure.Tests/
git add . && git commit -m "feat(infra): RestoreService — validate manifest, verify checksum, atomic swap with pre-restore safety"
```

---

## Task 5: Infrastructure DI registration + wire into HostBuilder

**Files:**
- Modify: `src/ConstructionFinance.Infrastructure/ConstructionFinance.Infrastructure.csproj` (add DI abstractions ref)
- Create: `src/ConstructionFinance.Infrastructure/DependencyInjection.cs`
- Modify: `src/ConstructionFinance.Desktop/Common/HostBuilder.cs`

- [ ] **Step 5.1: Add DI package to Infrastructure**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Infrastructure/ConstructionFinance.Infrastructure.csproj`. Add a package `<ItemGroup>` if not present:
```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
  </ItemGroup>
```

- [ ] **Step 5.2: Create Infrastructure DI extension**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Infrastructure/DependencyInjection.cs`:
```csharp
using ConstructionFinance.Infrastructure.Backup;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructionFinance.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IRestoreService, RestoreService>();
        return services;
    }
}
```

- [ ] **Step 5.3: Wire into HostBuilder**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Common/HostBuilder.cs`. Add `using ConstructionFinance.Infrastructure;` and inside `ConfigureServices` after `AddApplicationServices()`:
```csharp
                services.AddInfrastructureServices();
```

- [ ] **Step 5.4: Build + commit**

```bash
dotnet build
git add . && git commit -m "feat(infra): DI registration for Backup/Restore services + wire into Desktop host"
```

---

## Task 6: `CompanyInfoViewModel` + tests

**Files:**
- Create: `src/ConstructionFinance.Desktop/ViewModels/Settings/CompanyInfoViewModel.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Settings/CompanyInfoViewModelTests.cs`

- [ ] **Step 6.1: Failing tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Desktop.Tests/Settings/CompanyInfoViewModelTests.cs`:
```csharp
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Settings;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Settings;

public class CompanyInfoViewModelTests
{
    private static (CompanyInfoViewModel vm, Mock<ICompanyInfoService> svc, FakeDialogService dlg) Create(CompanyInfoDto? initial = null)
    {
        var svc = new Mock<ICompanyInfoService>(MockBehavior.Strict);
        svc.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(initial ?? new CompanyInfoDto(null, null, null, null, null, null, null, null));
        var dlg = new FakeDialogService();
        return (new CompanyInfoViewModel(svc.Object, dlg), svc, dlg);
    }

    [Fact]
    public async Task LoadAsync_populates_fields_from_service()
    {
        var dto = new CompanyInfoDto("Sở X", "123 Lê Lợi", "024-x", "i@x.vn", "/logo.png", "Mr. A", "GĐ", "Lưu VT");
        var (vm, _, _) = Create(dto);

        await vm.LoadAsync();

        vm.CompanyName.ShouldBe("Sở X");
        vm.DefaultSignerTitle.ShouldBe("GĐ");
    }

    [Fact]
    public async Task SaveCommand_calls_SaveAsync_and_shows_success_message()
    {
        var (vm, svc, dlg) = Create();
        svc.Setup(s => s.SaveAsync(It.IsAny<CompanyInfoDto>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        await vm.LoadAsync();
        vm.CompanyName = "New Org";

        await vm.SaveCommand.ExecuteAsync(null);

        svc.Verify(s => s.SaveAsync(It.Is<CompanyInfoDto>(d => d.CompanyName == "New Org"), It.IsAny<CancellationToken>()), Times.Once);
        dlg.ShownMessages.ShouldContain(m => m.Contains("lưu") || m.Contains("Đã lưu") || m.Contains("thành công"));
    }
}
```

- [ ] **Step 6.2: Run — fails**

- [ ] **Step 6.3: Implement CompanyInfoViewModel**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Settings/CompanyInfoViewModel.cs`:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.ViewModels.Settings;

public partial class CompanyInfoViewModel : ViewModelBase
{
    private readonly ICompanyInfoService _service;
    private readonly IDialogService _dialog;

    public CompanyInfoViewModel(ICompanyInfoService service, IDialogService dialog)
    {
        _service = service;
        _dialog = dialog;
    }

    [ObservableProperty] private string? _companyName;
    [ObservableProperty] private string? _companyAddress;
    [ObservableProperty] private string? _companyPhone;
    [ObservableProperty] private string? _companyEmail;
    [ObservableProperty] private string? _logoPath;
    [ObservableProperty] private string? _defaultSignerName;
    [ObservableProperty] private string? _defaultSignerTitle;
    [ObservableProperty] private string? _defaultRecipients;
    [ObservableProperty] private bool _isBusy;

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var info = await _service.GetAsync();
            CompanyName = info.CompanyName;
            CompanyAddress = info.CompanyAddress;
            CompanyPhone = info.CompanyPhone;
            CompanyEmail = info.CompanyEmail;
            LogoPath = info.LogoPath;
            DefaultSignerName = info.DefaultSignerName;
            DefaultSignerTitle = info.DefaultSignerTitle;
            DefaultRecipients = info.DefaultRecipients;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            await _service.SaveAsync(new CompanyInfoDto(
                CompanyName, CompanyAddress, CompanyPhone, CompanyEmail,
                LogoPath, DefaultSignerName, DefaultSignerTitle, DefaultRecipients));
            await _dialog.ShowMessageAsync("Cài đặt", "Đã lưu thông tin cơ quan thành công.");
        }
        catch (Exception ex)
        {
            await _dialog.ShowMessageAsync("Lỗi", ex.Message);
        }
    }
}
```

- [ ] **Step 6.4: Verify + commit**

```bash
dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~CompanyInfoViewModelTests"
git add . && git commit -m "feat(desktop): CompanyInfoViewModel (tab 1 of Settings)"
```

---

## Task 7: `BackupManagementViewModel` + tests

**Files:**
- Create: `src/ConstructionFinance.Desktop/ViewModels/Settings/BackupManagementViewModel.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Settings/BackupManagementViewModelTests.cs`

`BackupManagementViewModel` is responsible for:
- List current backups in `AppPaths.BackupsFolder`
- "Backup now" button → call `IBackupService.CreateBackupAsync` with a manually-constructed manifest
- "Restore from file..." → file picker (in dialog service) + `IRestoreService.RestoreAsync` with 2x confirm
- "Open backup folder" → opens Explorer (best-effort, just print path)

- [ ] **Step 7.1: Failing tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Desktop.Tests/Settings/BackupManagementViewModelTests.cs`:
```csharp
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Settings;
using ConstructionFinance.Infrastructure.Backup;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Settings;

public class BackupManagementViewModelTests
{
    private static (BackupManagementViewModel vm, Mock<IBackupService> bsvc, Mock<IRestoreService> rsvc, FakeDialogService dlg) Create(IEnumerable<BackupFileInfo>? listing = null)
    {
        var bsvc = new Mock<IBackupService>(MockBehavior.Strict);
        var rsvc = new Mock<IRestoreService>(MockBehavior.Strict);
        bsvc.Setup(s => s.ListBackups(It.IsAny<string>()))
            .Returns((listing ?? Array.Empty<BackupFileInfo>()).ToList());
        var dlg = new FakeDialogService();
        var vm = new BackupManagementViewModel(bsvc.Object, rsvc.Object, dlg);
        return (vm, bsvc, rsvc, dlg);
    }

    private static BackupFileInfo Info(string name, string type = "manual") =>
        new(Path.Combine("/tmp", name), name, DateTime.UtcNow, 12345, type, null);

    [Fact]
    public async Task LoadAsync_populates_list()
    {
        var (vm, _, _, _) = Create(new[] { Info("backup_20260520_120000_manual.cfb"), Info("backup_20260519_180000_on_exit.cfb", "on_exit") });
        await vm.LoadAsync();

        vm.Backups.Count.ShouldBe(2);
    }

    [Fact]
    public async Task BackupNowCommand_calls_CreateBackupAsync()
    {
        var (vm, bsvc, _, dlg) = Create();
        bsvc.Setup(s => s.CreateBackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BackupManifest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Info("backup_new.cfb"));
        await vm.LoadAsync();

        await vm.BackupNowCommand.ExecuteAsync(null);

        bsvc.Verify(s => s.CreateBackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.Is<BackupManifest>(m => m.BackupType == "manual"), It.IsAny<CancellationToken>()), Times.Once);
        dlg.ShownMessages.ShouldContain(m => m.Contains("backup") || m.Contains("Đã backup"));
    }

    [Fact]
    public async Task RestoreSelectedCommand_blocked_when_user_declines_first_confirm()
    {
        var (vm, _, rsvc, dlg) = Create(new[] { Info("b.cfb") });
        dlg.ConfirmResult = false;
        await vm.LoadAsync();
        vm.SelectedBackup = vm.Backups[0];

        await vm.RestoreSelectedCommand.ExecuteAsync(null);

        rsvc.Verify(s => s.RestoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

- [ ] **Step 7.2: Implement BackupManagementViewModel**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Settings/BackupManagementViewModel.cs`:
```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Infrastructure.Backup;

namespace ConstructionFinance.Desktop.ViewModels.Settings;

public partial class BackupManagementViewModel : ViewModelBase
{
    private const string CurrentAppVersion = "1.0.0";
    private const string CurrentDbSchemaVersion = "20260519_001";

    private readonly IBackupService _backup;
    private readonly IRestoreService _restore;
    private readonly IDialogService _dialog;

    public BackupManagementViewModel(IBackupService backup, IRestoreService restore, IDialogService dialog)
    {
        _backup = backup;
        _restore = restore;
        _dialog = dialog;
    }

    public ObservableCollection<BackupFileInfo> Backups { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreSelectedCommand))]
    private BackupFileInfo? _selectedBackup;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;

    public Task LoadAsync() => Task.Run(() =>
    {
        var list = _backup.ListBackups(AppPaths.BackupsFolder);
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Backups.Clear();
            foreach (var b in list) Backups.Add(b);
        });
    });

    [RelayCommand]
    private async Task BackupNowAsync()
    {
        IsBusy = true;
        try
        {
            var manifest = BuildManifest(backupType: "manual", description: null);
            var result = await _backup.CreateBackupAsync(AppPaths.DatabaseFile, AppPaths.AttachmentsFolder, AppPaths.BackupsFolder, manifest);
            Backups.Insert(0, result);
            StatusMessage = $"Đã backup: {result.FileName}";
            await _dialog.ShowMessageAsync("Backup", $"Đã backup thành công: {result.FileName}");
        }
        catch (Exception ex)
        {
            await _dialog.ShowMessageAsync("Lỗi", ex.Message);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task RestoreSelectedAsync()
    {
        if (SelectedBackup is null) return;
        var confirm1 = await _dialog.ConfirmAsync("Restore",
            $"Bạn có chắc muốn restore từ {SelectedBackup.FileName}?\nDữ liệu hiện tại sẽ được backup tự động (pre_restore) trước khi thay thế.");
        if (!confirm1) return;
        var confirm2 = await _dialog.ConfirmAsync("Xác nhận lần cuối",
            "Hành động này KHÔNG THỂ undo nếu chỉ dựa vào memory. Restore ngay?");
        if (!confirm2) return;

        IsBusy = true;
        try
        {
            await _restore.RestoreAsync(SelectedBackup.FullPath, AppPaths.DatabaseFile, AppPaths.AttachmentsFolder, AppPaths.BackupsFolder, CurrentDbSchemaVersion);
            await _dialog.ShowMessageAsync("Restore", "Restore thành công. Vui lòng khởi động lại app để dữ liệu mới có hiệu lực.");
        }
        catch (Exception ex)
        {
            await _dialog.ShowMessageAsync("Lỗi Restore", ex.Message);
        }
        finally { IsBusy = false; await LoadAsync(); }
    }

    private bool HasSelection() => SelectedBackup is not null;

    private static BackupManifest BuildManifest(string backupType, string? description) => new(
        FormatVersion: "1.0",
        AppVersion: CurrentAppVersion,
        DbSchemaVersion: CurrentDbSchemaVersion,
        CreatedAtUtc: DateTime.UtcNow,
        CreatedBy: new BackupCreator(Environment.MachineName, Environment.UserName, Environment.OSVersion.ToString()),
        DataSummary: new BackupDataSummary(0, 0, 0, 0, 0, 0, 0, 0), // detailed counts deferred
        DbSizeBytes: File.Exists(AppPaths.DatabaseFile) ? new FileInfo(AppPaths.DatabaseFile).Length : 0,
        BackupType: backupType,
        Description: description,
        CompanyName: null);
}
```

- [ ] **Step 7.3: Verify + commit**

```bash
dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~BackupManagementViewModelTests"
git add . && git commit -m "feat(desktop): BackupManagementViewModel (tab 2 of Settings) — list/backup/restore"
```

---

## Task 8: `PreferencesViewModel` (minimal — placeholder for future)

For MVP we only store 1 preference: "auto_backup_on_exit" toggle.

**Files:**
- Create: `src/ConstructionFinance.Desktop/ViewModels/Settings/PreferencesViewModel.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Settings/PreferencesViewModelTests.cs`

- [ ] **Step 8.1: Failing tests**

Write `/home/hieu-nguyen/management/tests/ConstructionFinance.Desktop.Tests/Settings/PreferencesViewModelTests.cs`:
```csharp
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Settings;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Settings;

public class PreferencesViewModelTests
{
    private static (PreferencesViewModel vm, Mock<IAppSettingService> svc, FakeDialogService dlg) Create(string? autoBackupValue = null)
    {
        var svc = new Mock<IAppSettingService>(MockBehavior.Strict);
        svc.Setup(s => s.GetAsync("auto_backup_on_exit", It.IsAny<CancellationToken>())).ReturnsAsync(autoBackupValue);
        return (new PreferencesViewModel(svc.Object, new FakeDialogService()), svc, new FakeDialogService());
    }

    [Fact]
    public async Task LoadAsync_AutoBackupOnExit_true_when_setting_is_true()
    {
        var (vm, _, _) = Create("true");
        await vm.LoadAsync();
        vm.AutoBackupOnExit.ShouldBeTrue();
    }

    [Fact]
    public async Task LoadAsync_defaults_to_true_when_setting_missing()
    {
        var (vm, _, _) = Create(autoBackupValue: null);
        await vm.LoadAsync();
        vm.AutoBackupOnExit.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveAsync_persists_AutoBackupOnExit()
    {
        var (vm, svc, _) = Create();
        svc.Setup(s => s.SetAsync("auto_backup_on_exit", "false", null, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        await vm.LoadAsync();
        vm.AutoBackupOnExit = false;

        await vm.SaveAsync();

        svc.Verify(s => s.SetAsync("auto_backup_on_exit", "false", null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 8.2: Implement PreferencesViewModel**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Settings/PreferencesViewModel.cs`:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.ViewModels.Settings;

public partial class PreferencesViewModel : ViewModelBase
{
    public const string KeyAutoBackupOnExit = "auto_backup_on_exit";

    private readonly IAppSettingService _settings;
    private readonly IDialogService _dialog;

    public PreferencesViewModel(IAppSettingService settings, IDialogService dialog)
    {
        _settings = settings;
        _dialog = dialog;
    }

    [ObservableProperty] private bool _autoBackupOnExit = true;

    public async Task LoadAsync()
    {
        var raw = await _settings.GetAsync(KeyAutoBackupOnExit);
        AutoBackupOnExit = raw is null || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task SaveAsync()
    {
        await _settings.SetAsync(KeyAutoBackupOnExit, AutoBackupOnExit ? "true" : "false");
    }
}
```

- [ ] **Step 8.3: Verify + commit**

```bash
dotnet test tests/ConstructionFinance.Desktop.Tests/ --filter "FullyQualifiedName~PreferencesViewModelTests"
git add . && git commit -m "feat(desktop): PreferencesViewModel (tab 3 of Settings) — auto-backup toggle"
```

---

## Task 9: `SettingsViewModel` (host) + Settings XAML views

**Files:**
- Create: `src/ConstructionFinance.Desktop/ViewModels/Settings/SettingsViewModel.cs`
- Create: `src/ConstructionFinance.Desktop/Views/Settings/SettingsView.axaml(.cs)`
- Create: `src/ConstructionFinance.Desktop/Views/Settings/CompanyInfoTab.axaml(.cs)`
- Create: `src/ConstructionFinance.Desktop/Views/Settings/BackupTab.axaml(.cs)`
- Create: `src/ConstructionFinance.Desktop/Views/Settings/PreferencesTab.axaml(.cs)`

- [ ] **Step 9.1: Implement SettingsViewModel**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/Settings/SettingsViewModel.cs`:
```csharp
using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.ViewModels.Settings;

public class SettingsViewModel : ViewModelBase
{
    public SettingsViewModel(CompanyInfoViewModel company, BackupManagementViewModel backup, PreferencesViewModel preferences)
    {
        CompanyInfo = company;
        Backup = backup;
        Preferences = preferences;
    }

    public CompanyInfoViewModel CompanyInfo { get; }
    public BackupManagementViewModel Backup { get; }
    public PreferencesViewModel Preferences { get; }

    public async Task InitializeAsync()
    {
        await CompanyInfo.LoadAsync();
        await Backup.LoadAsync();
        await Preferences.LoadAsync();
    }
}
```

- [ ] **Step 9.2: Create SettingsView.axaml (TabControl)**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Settings/SettingsView.axaml`:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Settings"
             x:Class="ConstructionFinance.Desktop.Views.Settings.SettingsView"
             x:DataType="vm:SettingsViewModel">
    <DockPanel>
        <TextBlock DockPanel.Dock="Top" Text="Cài đặt hệ thống" FontSize="20" FontWeight="SemiBold" Margin="0,0,0,12"/>
        <TabControl>
            <TabItem Header="Thông tin cơ quan">
                <ContentControl Content="{Binding CompanyInfo}"/>
            </TabItem>
            <TabItem Header="Backup &amp; Restore">
                <ContentControl Content="{Binding Backup}"/>
            </TabItem>
            <TabItem Header="Tùy chọn chung">
                <ContentControl Content="{Binding Preferences}"/>
            </TabItem>
        </TabControl>
    </DockPanel>
</UserControl>
```

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Settings/SettingsView.axaml.cs`:
```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop.Views.Settings;

public partial class SettingsView : UserControl
{
    public SettingsView() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 9.3: Create CompanyInfoTab.axaml**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Settings/CompanyInfoTab.axaml`:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Settings"
             x:Class="ConstructionFinance.Desktop.Views.Settings.CompanyInfoTab"
             x:DataType="vm:CompanyInfoViewModel">
    <ScrollViewer>
        <StackPanel Margin="16" Spacing="12">
            <Grid ColumnDefinitions="220,*">
                <TextBlock Grid.Column="0" Text="Tên cơ quan" VerticalAlignment="Center"/>
                <TextBox Grid.Column="1" Text="{Binding CompanyName}"/>
            </Grid>
            <Grid ColumnDefinitions="220,*">
                <TextBlock Grid.Column="0" Text="Địa chỉ" VerticalAlignment="Center"/>
                <TextBox Grid.Column="1" Text="{Binding CompanyAddress}"/>
            </Grid>
            <Grid ColumnDefinitions="220,*">
                <TextBlock Grid.Column="0" Text="Điện thoại" VerticalAlignment="Center"/>
                <TextBox Grid.Column="1" Text="{Binding CompanyPhone}"/>
            </Grid>
            <Grid ColumnDefinitions="220,*">
                <TextBlock Grid.Column="0" Text="Email" VerticalAlignment="Center"/>
                <TextBox Grid.Column="1" Text="{Binding CompanyEmail}"/>
            </Grid>
            <Grid ColumnDefinitions="220,*">
                <TextBlock Grid.Column="0" Text="Đường dẫn logo" VerticalAlignment="Center"/>
                <TextBox Grid.Column="1" Text="{Binding LogoPath}"/>
            </Grid>
            <Grid ColumnDefinitions="220,*">
                <TextBlock Grid.Column="0" Text="Người ký mặc định" VerticalAlignment="Center"/>
                <TextBox Grid.Column="1" Text="{Binding DefaultSignerName}"/>
            </Grid>
            <Grid ColumnDefinitions="220,*">
                <TextBlock Grid.Column="0" Text="Chức vụ người ký" VerticalAlignment="Center"/>
                <TextBox Grid.Column="1" Text="{Binding DefaultSignerTitle}"/>
            </Grid>
            <Grid ColumnDefinitions="220,*">
                <TextBlock Grid.Column="0" Text="Nơi nhận báo cáo mặc định" VerticalAlignment="Top"/>
                <TextBox Grid.Column="1" Text="{Binding DefaultRecipients}" AcceptsReturn="True" Height="100" TextWrapping="Wrap"/>
            </Grid>
            <StackPanel Orientation="Horizontal" Spacing="8">
                <Button Content="💾 Lưu" Command="{Binding SaveCommand}"/>
                <ProgressBar IsIndeterminate="True" IsVisible="{Binding IsBusy}" Width="80" VerticalAlignment="Center"/>
            </StackPanel>
        </StackPanel>
    </ScrollViewer>
</UserControl>
```

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Settings/CompanyInfoTab.axaml.cs`:
```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop.Views.Settings;

public partial class CompanyInfoTab : UserControl
{
    public CompanyInfoTab() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 9.4: Create BackupTab.axaml**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Settings/BackupTab.axaml`:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Settings"
             x:Class="ConstructionFinance.Desktop.Views.Settings.BackupTab"
             x:DataType="vm:BackupManagementViewModel">
    <DockPanel Margin="16">
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Spacing="8" Margin="0,0,0,12">
            <Button Content="💾 Backup ngay" Command="{Binding BackupNowCommand}"/>
            <Button Content="🔄 Restore từ bản đã chọn" Command="{Binding RestoreSelectedCommand}"/>
            <ProgressBar IsIndeterminate="True" IsVisible="{Binding IsBusy}" Width="80" VerticalAlignment="Center"/>
            <TextBlock Text="{Binding StatusMessage}" VerticalAlignment="Center"/>
        </StackPanel>
        <DataGrid ItemsSource="{Binding Backups}"
                  SelectedItem="{Binding SelectedBackup}"
                  IsReadOnly="True"
                  GridLinesVisibility="Horizontal"
                  HeadersVisibility="Column">
            <DataGrid.Columns>
                <DataGridTextColumn Header="Tên file" Binding="{Binding FileName}" Width="*"/>
                <DataGridTextColumn Header="Loại" Binding="{Binding BackupType}" Width="120"/>
                <DataGridTextColumn Header="Tạo lúc (UTC)" Binding="{Binding CreatedAtUtc}" Width="180"/>
                <DataGridTextColumn Header="Kích thước (bytes)" Binding="{Binding SizeBytes, StringFormat=#\\,##0}" Width="160"/>
            </DataGrid.Columns>
        </DataGrid>
    </DockPanel>
</UserControl>
```

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Settings/BackupTab.axaml.cs`:
```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop.Views.Settings;

public partial class BackupTab : UserControl
{
    public BackupTab() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 9.5: Create PreferencesTab.axaml**

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Settings/PreferencesTab.axaml`:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Settings"
             x:Class="ConstructionFinance.Desktop.Views.Settings.PreferencesTab"
             x:DataType="vm:PreferencesViewModel">
    <StackPanel Margin="16" Spacing="12">
        <CheckBox IsChecked="{Binding AutoBackupOnExit}" Content="Tự động backup khi đóng app (nếu có thay đổi trong phiên)"/>
        <TextBlock TextWrapping="Wrap" Foreground="Gray" FontSize="11"
                   Text="Lưu ý: backup tự động sẽ ghi vào thư mục Backups trong %LocalAppData%/ConstructionFinance/. Bạn có thể tắt nếu không cần."/>
    </StackPanel>
</UserControl>
```

Write `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Views/Settings/PreferencesTab.axaml.cs`:
```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop.Views.Settings;

public partial class PreferencesTab : UserControl
{
    public PreferencesTab() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 9.6: Build + commit**

```bash
dotnet build
git add . && git commit -m "feat(desktop): SettingsView (3-tab TabControl) — Company / Backup / Preferences"
```

---

## Task 10: Wire "Cài đặt" menu in MainWindow + auto-backup on exit

**Files:**
- Modify: `src/ConstructionFinance.Desktop/Common/HostBuilder.cs`
- Modify: `src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/App.axaml.cs`

- [ ] **Step 10.1: Register Settings VMs in HostBuilder**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/Common/HostBuilder.cs`. Inside `ConfigureServices`, after `ProjectListViewModel` registration, add:
```csharp
                services.AddTransient<ViewModels.Settings.CompanyInfoViewModel>();
                services.AddTransient<ViewModels.Settings.BackupManagementViewModel>();
                services.AddTransient<ViewModels.Settings.PreferencesViewModel>();
                services.AddTransient<ViewModels.Settings.SettingsViewModel>();
```

- [ ] **Step 10.2: Add "Settings" case in MainWindowViewModel.NavigateToAsync**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs`. Inside the `switch (tag)` block in `NavigateToAsync`, add before the `default:` case:
```csharp
            case "Settings":
                var svm = _services.GetRequiredService<ViewModels.Settings.SettingsViewModel>();
                _nav.Reset();
                _nav.NavigateTo(svm);
                await svm.InitializeAsync();
                break;
```

- [ ] **Step 10.3: Auto-backup-on-exit hook in App.axaml.cs**

Edit `/home/hieu-nguyen/management/src/ConstructionFinance.Desktop/App.axaml.cs`. In the `ShutdownRequested` handler, before the `Host?.Dispose()` line, add the auto-backup attempt:
```csharp
            desktop.ShutdownRequested += async (_, _) =>
            {
                if (Host is not null)
                {
                    try
                    {
                        using var scope = Host.Services.CreateScope();
                        var settings = scope.ServiceProvider.GetRequiredService<Application.Services.Interfaces.IAppSettingService>();
                        var prefRaw = await settings.GetAsync("auto_backup_on_exit");
                        var enabled = prefRaw is null || string.Equals(prefRaw, "true", StringComparison.OrdinalIgnoreCase);
                        if (!enabled) return;

                        var modifiedRaw = await settings.GetAsync("session_modified");
                        if (!string.Equals(modifiedRaw, "true", StringComparison.OrdinalIgnoreCase)) return;

                        var backup = scope.ServiceProvider.GetRequiredService<Infrastructure.Backup.IBackupService>();
                        var manifest = new Infrastructure.Backup.BackupManifest(
                            FormatVersion: "1.0",
                            AppVersion: "1.0.0",
                            DbSchemaVersion: "20260519_001",
                            CreatedAtUtc: DateTime.UtcNow,
                            CreatedBy: new Infrastructure.Backup.BackupCreator(Environment.MachineName, Environment.UserName, Environment.OSVersion.ToString()),
                            DataSummary: new Infrastructure.Backup.BackupDataSummary(0, 0, 0, 0, 0, 0, 0, 0),
                            DbSizeBytes: File.Exists(Common.AppPaths.DatabaseFile) ? new FileInfo(Common.AppPaths.DatabaseFile).Length : 0,
                            BackupType: "on_exit",
                            Description: "Auto backup on session close",
                            CompanyName: null);
                        await backup.CreateBackupAsync(Common.AppPaths.DatabaseFile, Common.AppPaths.AttachmentsFolder, Common.AppPaths.BackupsFolder, manifest);
                        await settings.SetAsync("session_modified", "false");
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Auto-backup on exit failed");
                    }
                }
                Host?.Dispose();
                Log.CloseAndFlush();
            };
```

Make sure these usings exist in `App.axaml.cs` (already there from Plan 03/05):
```csharp
using ConstructionFinance.Desktop.Common;
```

Note: tracking `session_modified` requires services that mutate data to call `_settings.SetAsync("session_modified", "true")`. This is **out of scope** for this plan — Plan 06 only sets up the infrastructure; the per-service write-tracking is a v1.1 polish. For MVP, manual backup via Settings tab is the primary mechanism; auto-backup will only fire if someone manually sets the flag.

- [ ] **Step 10.4: Build + test**

```bash
dotnet build --configuration Release
dotnet test --configuration Release
git add . && git commit -m "feat(desktop): wire Settings menu + auto-backup-on-exit hook (scaffold)"
```

---

## Task 11: Inno Setup script + publish documentation

This task creates the Windows installer. Tests/builds run on Linux but the installer itself requires Windows or Wine.

**Files:**
- Create: `installer/inno-setup.iss`
- Create: `installer/assets/license.txt`
- Create: `installer/build-installer.md`
- Create: `.github/workflows/release.yml` (optional CI)

- [ ] **Step 11.1: Create license.txt**

Write `/home/hieu-nguyen/management/installer/assets/license.txt`:
```
Phần mềm Quản lý Tài chính Công trình
Bản quyền © 2026 Sở Xây dựng

Phần mềm này được xây dựng từ các thành phần mã nguồn mở giấy phép MIT/Apache 2.0.
Sử dụng nội bộ trong cơ quan. Không bán lại.
```

- [ ] **Step 11.2: Create Inno Setup script**

Write `/home/hieu-nguyen/management/installer/inno-setup.iss`:
```inno
; Inno Setup script for Construction Finance Management
; Compile with: iscc.exe inno-setup.iss (Windows or Wine)

#define MyAppName "Quản lý Tài chính Công trình"
#define MyAppNameAscii "ConstructionFinance"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Sở Xây dựng"
#define MyAppExeName "ConstructionFinance.exe"

[Setup]
AppId={{B7A9F4F1-2C8E-4D6E-9F2A-1B3C4D5E6F7A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppNameAscii}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=assets\license.txt
OutputDir=output
OutputBaseFilename=ConstructionFinance-Setup-{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "vietnamese"; MessagesFile: "compiler:Languages\Vietnamese.isl"

[Files]
; Self-contained publish output (built before running this script)
Source: "..\src\ConstructionFinance.Desktop\bin\Release\net8.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Gỡ cài đặt {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Tạo shortcut trên Desktop"; GroupDescription: "Tùy chọn:"; Flags: unchecked

[Registry]
; Associate .cfb files with this app
Root: HKCU; Subkey: "Software\Classes\.cfb"; ValueType: string; ValueData: "ConstructionFinance.Backup"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\ConstructionFinance.Backup"; ValueType: string; ValueData: "{#MyAppName} Backup"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\ConstructionFinance.Backup\DefaultIcon"; ValueType: string; ValueData: "{app}\{#MyAppExeName},0"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\ConstructionFinance.Backup\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Khởi chạy {#MyAppName}"; Flags: nowait postinstall skipifsilent
```

- [ ] **Step 11.3: Create build instructions**

Write `/home/hieu-nguyen/management/installer/build-installer.md`:
````markdown
# Building the Windows Installer

## Prerequisites

- Windows 10/11 (or Linux with Wine for `iscc.exe`)
- .NET 8 SDK
- Inno Setup 6.x (https://jrsoftware.org/isinfo.php) — free

## Steps

### 1. Publish self-contained Windows binary

From repo root (Linux or Windows):

```bash
dotnet publish src/ConstructionFinance.Desktop/ \
    --configuration Release \
    --runtime win-x64 \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:PublishReadyToRun=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o src/ConstructionFinance.Desktop/bin/Release/net8.0/win-x64/publish
```

Output: `~150 MB` directory with `ConstructionFinance.exe` + dependencies.

### 2. Compile Inno Setup script

**Windows**:
```cmd
cd installer
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" inno-setup.iss
```

**Linux (Wine)**:
```bash
sudo apt install wine
# Install Inno Setup via Wine first (one-time):
#   wget https://jrsoftware.org/download.php/is.exe
#   wine is.exe /SILENT
cd installer
wine "$HOME/.wine/drive_c/Program Files (x86)/Inno Setup 6/ISCC.exe" inno-setup.iss
```

Output: `installer/output/ConstructionFinance-Setup-1.0.0.exe`

### 3. Test the installer

1. Copy `setup.exe` to a clean Windows VM
2. Run as a normal user (no admin)
3. Verify install goes to `%LocalAppData%\Programs\ConstructionFinance\`
4. Verify Start Menu shortcut + (optional) Desktop shortcut work
5. Verify `.cfb` file association: double-click any `.cfb` should open the app

### 4. Version bump

For new versions:
1. Update `MyAppVersion` in `inno-setup.iss`
2. Update `<Version>` in `src/ConstructionFinance.Desktop/ConstructionFinance.Desktop.csproj` (`<Version>1.0.0</Version>` — add if missing)
3. Rebuild
````

- [ ] **Step 11.4: Create release workflow (optional)**

Write `/home/hieu-nguyen/management/.github/workflows/release.yml`:
```yaml
name: Release Windows Installer

on:
  workflow_dispatch:
    inputs:
      version:
        description: 'Version (e.g., 1.0.0)'
        required: true
        default: '1.0.0'

jobs:
  build-windows:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET 8
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Publish self-contained
        run: |
          dotnet publish src/ConstructionFinance.Desktop/ `
            --configuration Release `
            --runtime win-x64 `
            --self-contained true `
            -p:PublishSingleFile=false `
            -p:PublishReadyToRun=true `
            -o publish-out

      - name: Install Inno Setup
        run: choco install innosetup -y

      - name: Compile installer
        run: |
          copy publish-out\* src\ConstructionFinance.Desktop\bin\Release\net8.0\win-x64\publish\ /Y
          & "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DMyAppVersion="${{ github.event.inputs.version }}" installer\inno-setup.iss

      - name: Upload installer
        uses: actions/upload-artifact@v4
        with:
          name: ConstructionFinance-Setup-${{ github.event.inputs.version }}
          path: installer/output/*.exe
```

- [ ] **Step 11.5: Commit**

```bash
git add installer/ .github/workflows/release.yml
git commit -m "build(installer): Inno Setup script + Windows publish docs + release workflow"
```

---

## Task 12: Self-contained publish test (Linux dev machine)

Even though the installer requires Windows, we can verify the publish output builds correctly on Linux dev.

- [ ] **Step 12.1: Run publish target locally**

Run from `/home/hieu-nguyen/management/`:
```bash
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
dotnet publish src/ConstructionFinance.Desktop/ \
    --configuration Release \
    --runtime win-x64 \
    --self-contained true \
    -p:PublishReadyToRun=true \
    -o /tmp/cf-publish-test
ls -la /tmp/cf-publish-test/ | head -20
du -sh /tmp/cf-publish-test/
```

Expected:
- Output succeeds
- `/tmp/cf-publish-test/ConstructionFinance.exe` exists
- Total size ~130-160 MB

- [ ] **Step 12.2: Clean up publish test**

Run: `rm -rf /tmp/cf-publish-test/`

---

## Final verification

- [ ] **Step F.1: Clean build + full test suite**

Run from `/home/hieu-nguyen/management/`:
```bash
dotnet clean
dotnet build --configuration Release
dotnet test --configuration Release --verbosity minimal
```

Expected: 0 errors. Approximately **225+ tests pass** (208 baseline + 4 AppSetting + 2 CompanyInfo service + 5 BackupService + 5 RestoreService + 2 CompanyInfo VM + 3 BackupManagement + 3 Preferences = ~232).

- [ ] **Step F.2: Confirm commits**

Run: `git log --oneline | head -14`
Expected: ~12 Plan 06 commits + base.

- [ ] **Step F.3: Manual smoke test (Linux dev)**

```bash
dotnet run --project src/ConstructionFinance.Desktop/
```

Manual checklist (run in real desktop, not headless):
- [ ] Click "Cài đặt" menu → SettingsView appears with 3 tabs
- [ ] Tab "Thông tin cơ quan" → fill form → click 💾 Lưu → message "Đã lưu thành công"
- [ ] Tab "Backup & Restore" → click "💾 Backup ngay" → new `.cfb` appears in list
- [ ] Tab "Backup & Restore" → select backup → click "🔄 Restore" → 2x confirm dialog appears
- [ ] Tab "Tùy chọn chung" → toggle "Tự động backup khi đóng" → close app → `.cfb` with type "on_exit" appears (only if `session_modified` was set)

---

## Deliverables

After Plan 06 (MVP complete):

1. ✅ `AppSettingService` + `CompanyInfoService` (key-value settings + typed company info)
2. ✅ `BackupService` — creates `.cfb` (ZIP) with manifest + SHA256 + db + attachments
3. ✅ `RestoreService` — validates manifest, verifies checksum, creates pre-restore safety backup, atomic file swap
4. ✅ `Infrastructure.Tests` project with 10 backup/restore tests
5. ✅ Settings UI (3 tabs: Company / Backup / Preferences) ~8 VM tests
6. ✅ Auto-backup-on-exit hook (infrastructure only; per-service write-tracking deferred v1.1)
7. ✅ Inno Setup `.iss` script — produces `ConstructionFinance-Setup-1.0.0.exe`
8. ✅ Release GitHub Actions workflow for Windows builds
9. ✅ Self-contained `win-x64` publish verified on Linux dev (~150 MB output)
10. ✅ `.cfb` file association registered in installer (double-click opens app)

**🎉 MVP v1.0 COMPLETE.** App đóng gói thành `setup.exe`, cài lên Windows, dùng đầy đủ chức năng nghiệp vụ, backup/share qua `.cfb`.

**What's NOT in MVP** (v1.1+ roadmap):
- Per-service write-tracking for auto-backup (currently manual flag — Plan 07a)
- Daily scheduled backup (background scheduler — Plan 07a)
- Theme switching (light/dark hot reload — Plan 07b)
- Excel-like Cost Tree (copy/paste, drag-fill, undo/redo — Plan 07c)
- 8 báo cáo PDF/Excel (R1-R8 — Plan 07d)
- Dashboard + LiveCharts (Plan 07e)
- Audit Log viewer (Plan 07f)
- Cost Templates (Plan 07g)
- Multi-language English (Plan 07h)
- Auto-update via Velopack (Plan 07i)

**Next plan (v1.1):** TBD based on user priority — recommend Reports first (R1-R3) since they're explicitly needed for government workflow.
