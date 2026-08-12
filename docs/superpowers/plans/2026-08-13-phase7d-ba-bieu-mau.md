# Phase 7D — Ba biểu mẫu tự sinh + số thành chữ — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sinh ba chứng từ Thông tri / KS chi / GĐNTT thẳng từ các đợt người dùng tích chọn trong Sổ theo dõi, kèm số tiền bằng chữ tiếng Việt, xem trước dạng tờ giấy trong app và xuất ra `.xlsx`.

**Architecture:** Toàn bộ logic chứng từ nằm ở tầng Application trong thư mục mới `Forms/` — số-thành-chữ, trải cây Vị trí thành danh sách đợt phẳng, ba builder dựng record `*Form`, và một exporter ClosedXML. Desktop chỉ thêm một picker chọn đợt, ba view-model form và ba View dạng tờ giấy nối vào dải tab Vị trí đã có của 7B/7C. Ô nhập tay lưu thành ba bảng 1–1 với `Project`, đi chung đường `GetTreeAsync` / `ReplaceTreeAsync` nên Ctrl+S lưu một thể.

**Tech Stack:** .NET 8, C# 12, Avalonia 11, CommunityToolkit.Mvvm, EF Core 8 + SQLite, ClosedXML, xUnit + Shouldly + Moq.

**Spec:** `docs/superpowers/specs/2026-08-13-ba-bieu-mau-design.md` — đọc §4 (công thức) trước khi làm Task 4–6.

## Global Constraints

- **100% free-license.** Không thêm gói NuGet mới. Chỉ dùng thứ đã có: Avalonia (MIT), EF Core, SQLite, ClosedXML, CommunityToolkit.Mvvm, xUnit, Shouldly, Moq. **Cấm** Syncfusion / QuestPDF / EPPlus v5+ / FluentAssertions v8+.
- **Đặt tên:** biến cục bộ và tham số `camelCase`; kiểu, phương thức, thuộc tính `PascalCase`; field private `_camelCase`.
- **Chữ hiển thị bằng tiếng Việt**, tiền tệ chỉ VNĐ.
- **Không** dùng `TwoWay` + `StringFormat` trên ô `decimal` / `DateOnly` trong XAML (bài học StackOverflow 6A) — luôn qua facade chuỗi như `LedgerRowViewModel` đang làm.
- **Không đụng** `ViewModels/ThongTri/ThongTriViewModel.cs` và View của nó (legacy trước 6A) — 7E sẽ dọn.
- Chạy toàn bộ test bằng `dotnet test ConstructionFinance.sln`; build bằng `dotnet build ConstructionFinance.sln`.
- Mỗi task kết thúc bằng một commit riêng.

## File Structure

**Tạo mới — Application (`src/ConstructionFinance.Application/Forms/`)**

| Tệp | Trách nhiệm |
|---|---|
| `VietnameseNumberToWords.cs` | Đọc số tiền thành chữ. Không biết gì về biểu mẫu. |
| `FormModels.cs` | `FormDotKey`, `FormDotRow`, `FormOrgInfo`, `ThongTriLine`, `ThongTriForm`, `KsChiAmounts`, `KsChiForm`, `GdnttForm`, `GdnttBuildResult`. Record thuần, không hành vi. |
| `FormRowFlattener.cs` | Cây `ViTri` → danh sách `FormDotRow` phẳng theo thứ tự sổ. |
| `ThongTriBuilder.cs` | Đợt đã chọn (NSNN) → `ThongTriForm`. |
| `KsChiBuilder.cs` | Đợt đã chọn (cả hai nguồn) → `KsChiForm`. |
| `GdnttBuilder.cs` | Đợt đã chọn (Ngoại hối, một gói) → `GdnttBuildResult`. |
| `FormExcelExporter.cs` | Ba `*Form` → tệp `.xlsx`. Chỉ vẽ, không tính. |

**Tạo mới — Domain (`src/ConstructionFinance.Domain/Finance/`)**: `ThongTriDeclaration.cs`, `KsChiDeclaration.cs`, `GdnttDeclaration.cs`.

**Tạo mới — Data (`src/ConstructionFinance.Data/Configurations/Finance/`)**: `ThongTriDeclarationConfiguration.cs`, `KsChiDeclarationConfiguration.cs`, `GdnttDeclarationConfiguration.cs` + một migration.

**Tạo mới — Desktop (`src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/`)**: `FormDotItemViewModel.cs`, `FormDotPickerViewModel.cs`, `ThongTriFormViewModel.cs`, `KsChiFormViewModel.cs`, `GdnttFormViewModel.cs`, `FormPageViewModels.cs` (ba lớp page nhỏ trong một tệp).

**Tạo mới — Views (`src/ConstructionFinance.Desktop/Views/Finance/Forms/`)**: `ThongTriFormView.axaml(.cs)`, `KsChiFormView.axaml(.cs)`, `GdnttFormView.axaml(.cs)`, `FormDotPickerView.axaml(.cs)`.

**Sửa:** `Domain/Finance/Project.cs` (ba navigation), `Data/AppDbContext.cs` (ba DbSet), `Data/Repositories/FinanceProjectRepository.cs` (Include + ghi kèm), `Application/Services/Interfaces/IFormExcelExporter.cs` (mới), `Application/DTOs/CompanyInfoDto.cs` + `Services/Implementations/CompanyInfoService.cs` (hai khóa mới), `Desktop/ViewModels/Settings/CompanyInfoViewModel.cs` + `Views/Settings/CompanyInfoView.axaml`, `Desktop/ViewModels/Finance/FinanceWorkbookViewModel.cs` (ba tab + nạp/lưu bản khai), `Desktop/Common/HostBuilder.cs` (DI).

---

### Task 1: Số thành chữ tiếng Việt

**Files:**
- Create: `src/ConstructionFinance.Application/Forms/VietnameseNumberToWords.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Forms/VietnameseNumberToWordsTests.cs`

**Interfaces:**
- Consumes: không có.
- Produces: `static string VietnameseNumberToWords.ToWords(decimal amount)` — trả chuỗi đã viết hoa chữ đầu và kết thúc bằng `" đồng."`.

- [ ] **Step 1: Viết test thất bại**

Tạo `tests/ConstructionFinance.Application.Tests/Forms/VietnameseNumberToWordsTests.cs`:

```csharp
using ConstructionFinance.Application.Forms;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Forms;

public class VietnameseNumberToWordsTests
{
    [Theory]
    [InlineData(0, "Không đồng.")]
    [InlineData(1, "Một đồng.")]
    [InlineData(10, "Mười đồng.")]
    [InlineData(11, "Mười một đồng.")]
    [InlineData(15, "Mười lăm đồng.")]
    [InlineData(20, "Hai mươi đồng.")]
    [InlineData(21, "Hai mươi mốt đồng.")]
    [InlineData(24, "Hai mươi tư đồng.")]
    [InlineData(25, "Hai mươi lăm đồng.")]
    [InlineData(100, "Một trăm đồng.")]
    [InlineData(101, "Một trăm linh một đồng.")]
    [InlineData(105, "Một trăm linh năm đồng.")]
    [InlineData(110, "Một trăm mười đồng.")]
    [InlineData(1_000, "Một nghìn đồng.")]
    [InlineData(1_005, "Một nghìn không trăm linh năm đồng.")]
    [InlineData(10_000_000, "Mười triệu đồng.")]
    [InlineData(200_000_000, "Hai trăm triệu đồng.")]
    public void ToWords_reads_common_amounts(long amount, string expected) =>
        VietnameseNumberToWords.ToWords(amount).ShouldBe(expected);

    [Fact]
    public void ToWords_reads_a_billion_with_a_zero_hundreds_group() =>
        VietnameseNumberToWords.ToWords(1_234_567_021m).ShouldBe(
            "Một tỷ hai trăm ba mươi tư triệu năm trăm sáu mươi bảy nghìn không trăm hai mươi mốt đồng.");

    [Fact]
    public void ToWords_reads_fifteen_billion() =>
        VietnameseNumberToWords.ToWords(15_000_000_000m).ShouldBe("Mười lăm tỷ đồng.");

    [Fact]
    public void ToWords_reads_above_a_thousand_billion_as_nghin_ty() =>
        VietnameseNumberToWords.ToWords(1_000_000_000_000m).ShouldBe("Một nghìn tỷ đồng.");

    [Fact]
    public void ToWords_rounds_to_the_nearest_dong_away_from_zero() =>
        VietnameseNumberToWords.ToWords(10_000_000.5m).ShouldBe("Mười triệu không trăm linh một đồng.");

    [Fact]
    public void ToWords_prefixes_negative_amounts_with_am() =>
        VietnameseNumberToWords.ToWords(-1_000m).ShouldBe("Âm một nghìn đồng.");
}
```

- [ ] **Step 2: Chạy test cho chắc là đỏ**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter FullyQualifiedName~VietnameseNumberToWordsTests`
Expected: FAIL — không biên dịch được, `VietnameseNumberToWords` chưa tồn tại.

- [ ] **Step 3: Viết bản cài đặt tối thiểu**

Tạo `src/ConstructionFinance.Application/Forms/VietnameseNumberToWords.cs`:

```csharp
using System.Text;

namespace ConstructionFinance.Application.Forms;

/// Đọc số tiền đồng thành chữ theo quy ước văn bản nhà nước:
/// "nghìn" (không phải "ngàn"), "linh" (không phải "lẻ"), và sau "mươi" thì
/// 1 → "mốt", 4 → "tư", 5 → "lăm". Thay cho add-in [3]!VND() của Mẫu.xlsx
/// (add-in đó không có giấy phép tự do nên không dùng được).
public static class VietnameseNumberToWords
{
    private static readonly string[] Digits =
        { "không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };

    // Hậu tố cho từng nhóm 3 chữ số, tính từ nhóm thấp nhất lên.
    // Nhóm thứ 4 (10^12) lại đọc là "nghìn tỷ", nhóm thứ 5 là "triệu tỷ".
    private static readonly string[] GroupSuffixes = { "", "nghìn", "triệu", "tỷ", "nghìn tỷ", "triệu tỷ" };

    public static string ToWords(decimal amount)
    {
        var rounded = Math.Round(amount, 0, MidpointRounding.AwayFromZero);
        if (rounded == 0m) return "Không đồng.";

        var negative = rounded < 0m;
        var value = Math.Abs(rounded);

        var groups = SplitIntoGroups(value);
        var parts = new List<string>();
        for (var i = groups.Count - 1; i >= 0; i--)
        {
            if (groups[i] == 0) continue;

            // "không trăm" chỉ cần khi nhóm này đứng SAU một nhóm đã đọc.
            var isLeading = parts.Count == 0;
            var text = ReadGroup(groups[i], forceHundreds: !isLeading);
            var suffix = GroupSuffixes[i];
            parts.Add(suffix.Length == 0 ? text : $"{text} {suffix}");
        }

        var sentence = string.Join(" ", parts);
        if (negative) sentence = "âm " + sentence;

        return char.ToUpperInvariant(sentence[0]) + sentence[1..] + " đồng.";
    }

    /// Tách số thành các nhóm 3 chữ số, chỉ số 0 là nhóm hàng đơn vị.
    private static List<int> SplitIntoGroups(decimal value)
    {
        var groups = new List<int>();
        while (value > 0m)
        {
            groups.Add((int)(value % 1000m));
            value = Math.Floor(value / 1000m);
        }
        return groups;
    }

    /// Đọc một nhóm 3 chữ số. forceHundreds=true thì hàng trăm bằng 0 vẫn đọc
    /// "không trăm" (vì nhóm này đứng sau một nhóm lớn hơn đã được đọc).
    private static string ReadGroup(int group, bool forceHundreds)
    {
        var hundreds = group / 100;
        var tens = group / 10 % 10;
        var units = group % 10;

        var sb = new StringBuilder();

        if (hundreds > 0 || forceHundreds)
            sb.Append(Digits[hundreds]).Append(" trăm");

        if (tens == 0)
        {
            if (units > 0)
            {
                if (sb.Length > 0) sb.Append(" linh");
                sb.Append(sb.Length > 0 ? " " : "").Append(Digits[units]);
            }
            return sb.ToString();
        }

        if (sb.Length > 0) sb.Append(' ');
        sb.Append(tens == 1 ? "mười" : $"{Digits[tens]} mươi");

        if (units == 0) return sb.ToString();

        sb.Append(' ').Append(units switch
        {
            1 when tens > 1 => "mốt",
            4 when tens > 1 => "tư",
            5 => "lăm",
            _ => Digits[units]
        });

        return sb.ToString();
    }
}
```

- [ ] **Step 4: Chạy test cho chắc là xanh**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter FullyQualifiedName~VietnameseNumberToWordsTests`
Expected: PASS — cả 23 ca.

- [ ] **Step 5: Commit**

```bash
git add src/ConstructionFinance.Application/Forms/VietnameseNumberToWords.cs \
        tests/ConstructionFinance.Application.Tests/Forms/VietnameseNumberToWordsTests.cs
git commit -m "feat(app): số tiền thành chữ tiếng Việt (nghìn/linh/mốt/lăm/tư)"
```

---

### Task 2: Ba bảng khai báo biểu mẫu (Domain + EF + migration + repo)

Ba thực thể 1–1 với `Project`, đi chung đường `GetTreeAsync` / `ReplaceTreeAsync` để Ctrl+S lưu một thể.

**Files:**
- Create: `src/ConstructionFinance.Domain/Finance/ThongTriDeclaration.cs`, `KsChiDeclaration.cs`, `GdnttDeclaration.cs`
- Create: `src/ConstructionFinance.Data/Configurations/Finance/ThongTriDeclarationConfiguration.cs`, `KsChiDeclarationConfiguration.cs`, `GdnttDeclarationConfiguration.cs`
- Modify: `src/ConstructionFinance.Domain/Finance/Project.cs` (thêm ba navigation)
- Modify: `src/ConstructionFinance.Data/AppDbContext.cs:1-9` (alias) và `:33-39` (DbSet)
- Modify: `src/ConstructionFinance.Data/Repositories/FinanceProjectRepository.cs:70-74` (`GetTreeAsync`) và `:83-160` (`ReplaceTreeAsync`)
- Test: `tests/ConstructionFinance.Data.Tests/Finance/FormDeclarationPersistenceTests.cs`

**Interfaces:**
- Consumes: `Project` (`Domain.Finance`), `FinanceProjectRepository`.
- Produces: `Project.ThongTri` / `Project.KsChi` / `Project.Gdntt` (kiểu `ThongTriDeclaration?` / `KsChiDeclaration?` / `GdnttDeclaration?`); ba lớp khai báo với các thuộc tính liệt kê ở Step 3 — Task 4–6 và Task 11 đọc thẳng các thuộc tính này.

- [ ] **Step 1: Viết test thất bại**

Tạo `tests/ConstructionFinance.Data.Tests/Finance/FormDeclarationPersistenceTests.cs`:

```csharp
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Data.Tests;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Finance;

public class FormDeclarationPersistenceTests
{
    [Fact]
    public async Task ReplaceTreeAsync_inserts_the_three_declarations_and_GetTreeAsync_loads_them()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new FinanceProjectRepository(ctx);
        var id = await repo.AddAsync(new Project { Name = "DA1" });

        await repo.ReplaceTreeAsync(new Project
        {
            Id = id,
            Name = "DA1",
            ThongTri = new ThongTriDeclaration { SoThongTri = "12/TT", Khoan = "11", Muc = "9300" },
            KsChi = new KsChiDeclaration { ThueGTGT = 5_000_000m, LyDoTuChoi = "thiếu hồ sơ" },
            Gdntt = new GdnttDeclaration { TaiNganHang = "Vietcombank Hải Phòng", ChuyenTienBaoHanh = 1_000m }
        });

        var tree = await repo.GetTreeAsync(id);

        tree!.ThongTri!.SoThongTri.ShouldBe("12/TT");
        tree.ThongTri.Khoan.ShouldBe("11");
        tree.ThongTri.Muc.ShouldBe("9300");
        tree.KsChi!.ThueGTGT.ShouldBe(5_000_000m);
        tree.KsChi.LyDoTuChoi.ShouldBe("thiếu hồ sơ");
        tree.Gdntt!.TaiNganHang.ShouldBe("Vietcombank Hải Phòng");
        tree.Gdntt.ChuyenTienBaoHanh.ShouldBe(1_000m);
    }

    [Fact]
    public async Task ReplaceTreeAsync_updates_an_existing_declaration_in_place()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new FinanceProjectRepository(ctx);
        var id = await repo.AddAsync(new Project { Name = "DA1" });

        await repo.ReplaceTreeAsync(new Project
        {
            Id = id, Name = "DA1",
            ThongTri = new ThongTriDeclaration { SoThongTri = "cũ" }
        });
        await repo.ReplaceTreeAsync(new Project
        {
            Id = id, Name = "DA1",
            ThongTri = new ThongTriDeclaration { SoThongTri = "mới" }
        });

        var tree = await repo.GetTreeAsync(id);
        tree!.ThongTri!.SoThongTri.ShouldBe("mới");
        ctx.ThongTriDeclarations.Count(d => d.ProjectId == id).ShouldBe(1);
    }

    [Fact]
    public async Task ReplaceTreeAsync_keeps_the_stored_declaration_when_none_is_supplied()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new FinanceProjectRepository(ctx);
        var id = await repo.AddAsync(new Project { Name = "DA1" });

        await repo.ReplaceTreeAsync(new Project
        {
            Id = id, Name = "DA1",
            ThongTri = new ThongTriDeclaration { SoThongTri = "12/TT" }
        });
        await repo.ReplaceTreeAsync(new Project { Id = id, Name = "DA1" });   // không kèm bản khai

        var tree = await repo.GetTreeAsync(id);
        tree!.ThongTri!.SoThongTri.ShouldBe("12/TT");
    }
}
```

- [ ] **Step 2: Chạy test cho chắc là đỏ**

Run: `dotnet test tests/ConstructionFinance.Data.Tests --filter FullyQualifiedName~FormDeclarationPersistenceTests`
Expected: FAIL — không biên dịch được, ba lớp khai báo chưa tồn tại.

- [ ] **Step 3: Thêm ba thực thể**

Tạo `src/ConstructionFinance.Domain/Finance/ThongTriDeclaration.cs`:

```csharp
using ConstructionFinance.Domain.Common;
namespace ConstructionFinance.Domain.Finance;

/// Các ô Mẫu số CQ06-HD để trống cho người dùng điền tay. Một bản cho mỗi dự án.
public class ThongTriDeclaration : BaseEntity
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string? SoThongTri { get; set; }
    public string? NgayGhiSo { get; set; }
    public string? TenCTMT { get; set; }
    public string? MaSo { get; set; }
    public string? VeThangNam { get; set; }
    public string? TaiKhoan1 { get; set; }
    public string? DonVi1 { get; set; }
    public string? SoChungTu1 { get; set; }
    public string? TaiKhoan2 { get; set; }
    public string? DonVi2 { get; set; }
    public string? SoChungTu2 { get; set; }

    // Khối Mục lục ngân sách (cột A–F của Mẫu).
    public string? LoaiNganSach { get; set; }
    public string? Khoan { get; set; }
    public string? Muc { get; set; }
    public string? TieuMuc { get; set; }
    public string? TietMuc { get; set; }
    public string? Nganh { get; set; }
}
```

Tạo `src/ConstructionFinance.Domain/Finance/KsChiDeclaration.cs`:

```csharp
using ConstructionFinance.Domain.Common;
namespace ConstructionFinance.Domain.Finance;

/// Các ô người dùng điền tay trên phần ghi của cơ quan kiểm soát, thanh toán.
public class KsChiDeclaration : BaseEntity
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string? NgayNhanGiayDeNghi { get; set; }
    public string? MucTieuMuc1 { get; set; }
    public string? MucTieuMuc2 { get; set; }
    public string? MucTieuMuc3 { get; set; }
    public string? MucTieuMuc4 { get; set; }
    public decimal ThuHoiCacNamTruoc { get; set; }
    public decimal ThuHoiNamNay { get; set; }
    public decimal ThueGTGT { get; set; }
    public decimal SoTuChoi { get; set; }
    public string? LyDoTuChoi { get; set; }

    /// Để trống thì bản in lấy Nội dung của đợt được chọn đầu tiên.
    public string? GhiChu { get; set; }
}
```

Tạo `src/ConstructionFinance.Domain/Finance/GdnttDeclaration.cs`:

```csharp
using ConstructionFinance.Domain.Common;
namespace ConstructionFinance.Domain.Finance;

/// Các ô người dùng điền tay trên Giấy đề nghị thanh toán vốn đầu tư.
public class GdnttDeclaration : BaseEntity
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string? So { get; set; }
    public string? NgayThangNam { get; set; }
    public string? KinhGui { get; set; }
    public string? TenDuAnCongTrinh { get; set; }
    public string? ChuDauTu { get; set; }
    public string? CanCuQuyetDinh { get; set; }
    public string? CanCuHopDong { get; set; }
    public string? CanCuGiayDeNghi { get; set; }
    public string? ThuocKeHoach { get; set; }
    public string? TaiNganHang { get; set; }
    public decimal ChuyenTienBaoHanh { get; set; }
    public decimal ChuyenTienTamGiu { get; set; }
}
```

Trong `src/ConstructionFinance.Domain/Finance/Project.cs`, thêm ba navigation ngay dưới `public List<ViTri> ViTris { get; set; } = new();`:

```csharp
    public ThongTriDeclaration? ThongTri { get; set; }
    public KsChiDeclaration? KsChi { get; set; }
    public GdnttDeclaration? Gdntt { get; set; }
```

- [ ] **Step 4: Thêm ba cấu hình EF và ba DbSet**

Tạo `src/ConstructionFinance.Data/Configurations/Finance/ThongTriDeclarationConfiguration.cs`:

```csharp
using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations.Finance;

public class ThongTriDeclarationConfiguration : IEntityTypeConfiguration<ThongTriDeclaration>
{
    public void Configure(EntityTypeBuilder<ThongTriDeclaration> b)
    {
        b.ToTable("finance_thongtri_decl");
        b.HasKey(x => x.Id);
        foreach (var name in new[]
                 {
                     nameof(ThongTriDeclaration.SoThongTri), nameof(ThongTriDeclaration.NgayGhiSo),
                     nameof(ThongTriDeclaration.TenCTMT), nameof(ThongTriDeclaration.MaSo),
                     nameof(ThongTriDeclaration.VeThangNam), nameof(ThongTriDeclaration.TaiKhoan1),
                     nameof(ThongTriDeclaration.DonVi1), nameof(ThongTriDeclaration.SoChungTu1),
                     nameof(ThongTriDeclaration.TaiKhoan2), nameof(ThongTriDeclaration.DonVi2),
                     nameof(ThongTriDeclaration.SoChungTu2), nameof(ThongTriDeclaration.LoaiNganSach),
                     nameof(ThongTriDeclaration.Khoan), nameof(ThongTriDeclaration.Muc),
                     nameof(ThongTriDeclaration.TieuMuc), nameof(ThongTriDeclaration.TietMuc),
                     nameof(ThongTriDeclaration.Nganh)
                 })
            b.Property<string>(name).HasMaxLength(255);

        b.HasIndex(x => x.ProjectId).IsUnique();
        b.HasOne(x => x.Project).WithOne(p => p.ThongTri)
            .HasForeignKey<ThongTriDeclaration>(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
```

Tạo `src/ConstructionFinance.Data/Configurations/Finance/KsChiDeclarationConfiguration.cs`:

```csharp
using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations.Finance;

public class KsChiDeclarationConfiguration : IEntityTypeConfiguration<KsChiDeclaration>
{
    public void Configure(EntityTypeBuilder<KsChiDeclaration> b)
    {
        b.ToTable("finance_kschi_decl");
        b.HasKey(x => x.Id);
        b.Property(x => x.NgayNhanGiayDeNghi).HasMaxLength(255);
        b.Property(x => x.MucTieuMuc1).HasMaxLength(255);
        b.Property(x => x.MucTieuMuc2).HasMaxLength(255);
        b.Property(x => x.MucTieuMuc3).HasMaxLength(255);
        b.Property(x => x.MucTieuMuc4).HasMaxLength(255);
        b.Property(x => x.LyDoTuChoi).HasMaxLength(255);
        b.Property(x => x.GhiChu).HasMaxLength(255);
        b.Property(x => x.ThuHoiCacNamTruoc).HasPrecision(18, 2);
        b.Property(x => x.ThuHoiNamNay).HasPrecision(18, 2);
        b.Property(x => x.ThueGTGT).HasPrecision(18, 2);
        b.Property(x => x.SoTuChoi).HasPrecision(18, 2);

        b.HasIndex(x => x.ProjectId).IsUnique();
        b.HasOne(x => x.Project).WithOne(p => p.KsChi)
            .HasForeignKey<KsChiDeclaration>(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
```

Tạo `src/ConstructionFinance.Data/Configurations/Finance/GdnttDeclarationConfiguration.cs`:

```csharp
using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations.Finance;

public class GdnttDeclarationConfiguration : IEntityTypeConfiguration<GdnttDeclaration>
{
    public void Configure(EntityTypeBuilder<GdnttDeclaration> b)
    {
        b.ToTable("finance_gdntt_decl");
        b.HasKey(x => x.Id);
        foreach (var name in new[]
                 {
                     nameof(GdnttDeclaration.So), nameof(GdnttDeclaration.NgayThangNam),
                     nameof(GdnttDeclaration.KinhGui), nameof(GdnttDeclaration.TenDuAnCongTrinh),
                     nameof(GdnttDeclaration.ChuDauTu), nameof(GdnttDeclaration.CanCuQuyetDinh),
                     nameof(GdnttDeclaration.CanCuHopDong), nameof(GdnttDeclaration.CanCuGiayDeNghi),
                     nameof(GdnttDeclaration.ThuocKeHoach), nameof(GdnttDeclaration.TaiNganHang)
                 })
            b.Property<string>(name).HasMaxLength(255);

        b.Property(x => x.ChuyenTienBaoHanh).HasPrecision(18, 2);
        b.Property(x => x.ChuyenTienTamGiu).HasPrecision(18, 2);

        b.HasIndex(x => x.ProjectId).IsUnique();
        b.HasOne(x => x.Project).WithOne(p => p.Gdntt)
            .HasForeignKey<GdnttDeclaration>(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
```

Trong `src/ConstructionFinance.Data/AppDbContext.cs`, thêm ba alias cạnh các alias `Finance*` đã có (dòng 1–9):

```csharp
using FinanceThongTriDeclaration = ConstructionFinance.Domain.Finance.ThongTriDeclaration;
using FinanceKsChiDeclaration = ConstructionFinance.Domain.Finance.KsChiDeclaration;
using FinanceGdnttDeclaration = ConstructionFinance.Domain.Finance.GdnttDeclaration;
```

và ba DbSet ngay dưới `public DbSet<FinanceDot> Dots => Set<FinanceDot>();`:

```csharp
    public DbSet<FinanceThongTriDeclaration> ThongTriDeclarations => Set<FinanceThongTriDeclaration>();
    public DbSet<FinanceKsChiDeclaration> KsChiDeclarations => Set<FinanceKsChiDeclaration>();
    public DbSet<FinanceGdnttDeclaration> GdnttDeclarations => Set<FinanceGdnttDeclaration>();
```

- [ ] **Step 5: Nạp và ghi bản khai trong repository**

Trong `src/ConstructionFinance.Data/Repositories/FinanceProjectRepository.cs`, sửa `GetTreeAsync` (dòng 70–74) thành:

```csharp
    public async Task<Project?> GetTreeAsync(int id, CancellationToken ct = default) =>
        await _context.FinanceProjects
            .Include(p => p.ViTris).ThenInclude(v => v.CostGroups)
                .ThenInclude(g => g.Packages).ThenInclude(pk => pk.Dots)
            .Include(p => p.ThongTri)
            .Include(p => p.KsChi)
            .Include(p => p.Gdntt)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
```

Trong `ReplaceTreeAsync`, ở nhánh dự án đã tồn tại, sau ba dòng gán `existing.Code/Name/Notes` thêm:

```csharp
        UpsertDeclarations(existing, project);
```

và thêm ba phương thức private ở cuối lớp:

```csharp
    /// Bản khai biểu mẫu đi cùng cây nhưng KHÔNG theo chiến lược xóa-rồi-thêm như
    /// ViTris: nó là hàng 1–1 lâu dài của dự án. Không truyền bản khai nào thì giữ
    /// nguyên bản đang lưu (màn hình workbook có thể lưu trước khi form được mở).
    private void UpsertDeclarations(Project existing, Project incoming)
    {
        if (incoming.ThongTri is not null)
        {
            existing.ThongTri ??= new ThongTriDeclaration { ProjectId = existing.Id };
            CopyThongTri(incoming.ThongTri, existing.ThongTri);
        }
        if (incoming.KsChi is not null)
        {
            existing.KsChi ??= new KsChiDeclaration { ProjectId = existing.Id };
            CopyKsChi(incoming.KsChi, existing.KsChi);
        }
        if (incoming.Gdntt is not null)
        {
            existing.Gdntt ??= new GdnttDeclaration { ProjectId = existing.Id };
            CopyGdntt(incoming.Gdntt, existing.Gdntt);
        }
    }

    private static void CopyThongTri(ThongTriDeclaration from, ThongTriDeclaration to)
    {
        to.SoThongTri = from.SoThongTri;
        to.NgayGhiSo = from.NgayGhiSo;
        to.TenCTMT = from.TenCTMT;
        to.MaSo = from.MaSo;
        to.VeThangNam = from.VeThangNam;
        to.TaiKhoan1 = from.TaiKhoan1;
        to.DonVi1 = from.DonVi1;
        to.SoChungTu1 = from.SoChungTu1;
        to.TaiKhoan2 = from.TaiKhoan2;
        to.DonVi2 = from.DonVi2;
        to.SoChungTu2 = from.SoChungTu2;
        to.LoaiNganSach = from.LoaiNganSach;
        to.Khoan = from.Khoan;
        to.Muc = from.Muc;
        to.TieuMuc = from.TieuMuc;
        to.TietMuc = from.TietMuc;
        to.Nganh = from.Nganh;
    }

    private static void CopyKsChi(KsChiDeclaration from, KsChiDeclaration to)
    {
        to.NgayNhanGiayDeNghi = from.NgayNhanGiayDeNghi;
        to.MucTieuMuc1 = from.MucTieuMuc1;
        to.MucTieuMuc2 = from.MucTieuMuc2;
        to.MucTieuMuc3 = from.MucTieuMuc3;
        to.MucTieuMuc4 = from.MucTieuMuc4;
        to.ThuHoiCacNamTruoc = from.ThuHoiCacNamTruoc;
        to.ThuHoiNamNay = from.ThuHoiNamNay;
        to.ThueGTGT = from.ThueGTGT;
        to.SoTuChoi = from.SoTuChoi;
        to.LyDoTuChoi = from.LyDoTuChoi;
        to.GhiChu = from.GhiChu;
    }

    private static void CopyGdntt(GdnttDeclaration from, GdnttDeclaration to)
    {
        to.So = from.So;
        to.NgayThangNam = from.NgayThangNam;
        to.KinhGui = from.KinhGui;
        to.TenDuAnCongTrinh = from.TenDuAnCongTrinh;
        to.ChuDauTu = from.ChuDauTu;
        to.CanCuQuyetDinh = from.CanCuQuyetDinh;
        to.CanCuHopDong = from.CanCuHopDong;
        to.CanCuGiayDeNghi = from.CanCuGiayDeNghi;
        to.ThuocKeHoach = from.ThuocKeHoach;
        to.TaiNganHang = from.TaiNganHang;
        to.ChuyenTienBaoHanh = from.ChuyenTienBaoHanh;
        to.ChuyenTienTamGiu = from.ChuyenTienTamGiu;
    }
```

- [ ] **Step 6: Chạy test cho chắc là xanh**

Run: `dotnet test tests/ConstructionFinance.Data.Tests --filter FullyQualifiedName~FormDeclarationPersistenceTests`
Expected: PASS — cả 3 ca.

- [ ] **Step 7: Sinh migration**

Run:
```bash
dotnet ef migrations add AddFormDeclarations \
  -p src/ConstructionFinance.Data -s src/ConstructionFinance.Desktop
```
Expected: sinh `Migrations/<timestamp>_AddFormDeclarations.cs`.

Mở tệp vừa sinh và kiểm: chỉ có ba lệnh `CreateTable` cho `finance_thongtri_decl`, `finance_kschi_decl`, `finance_gdntt_decl` (mỗi bảng một FK cascade tới `finance_projects` và một unique index trên `ProjectId`). Nếu thấy bất kỳ `DropTable` / `DropColumn` / `AlterColumn` nào trên bảng cũ thì DỪNG và sửa model — migration này phải thuần bổ sung. Nếu `dotnet ef` không chạy được trong máy, DỪNG và báo, không tự viết tay snapshot.

- [ ] **Step 8: Chạy toàn bộ test của Data**

Run: `dotnet test tests/ConstructionFinance.Data.Tests`
Expected: PASS — gồm cả `MigrationAppliesTests` đang có.

- [ ] **Step 9: Commit**

```bash
git add src/ConstructionFinance.Domain/Finance/ThongTriDeclaration.cs \
        src/ConstructionFinance.Domain/Finance/KsChiDeclaration.cs \
        src/ConstructionFinance.Domain/Finance/GdnttDeclaration.cs \
        src/ConstructionFinance.Domain/Finance/Project.cs \
        src/ConstructionFinance.Data/Configurations/Finance/ \
        src/ConstructionFinance.Data/AppDbContext.cs \
        src/ConstructionFinance.Data/Repositories/FinanceProjectRepository.cs \
        src/ConstructionFinance.Data/Migrations/ \
        tests/ConstructionFinance.Data.Tests/Finance/FormDeclarationPersistenceTests.cs
git commit -m "feat(data): ba bảng khai báo biểu mẫu 1-1 với dự án"
```

---

### Task 3: Model biểu mẫu + trải cây thành danh sách đợt phẳng

**Files:**
- Create: `src/ConstructionFinance.Application/Forms/FormModels.cs`
- Create: `src/ConstructionFinance.Application/Forms/FormRowFlattener.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Forms/FormRowFlattenerTests.cs`

**Interfaces:**
- Consumes: `ViTri` / `CostGroup` / `Package` / `Dot` / `Fund` (`Domain.Finance`), `TreeCalculator.Disbursed` (`Application.Calc`).
- Produces: `FormDotKey`, `FormDotRow`, `FormOrgInfo`, `ThongTriLine`, `ThongTriForm`, `KsChiAmounts`, `KsChiForm`, `GdnttForm`, `GdnttBuildResult`; `static IReadOnlyList<FormDotRow> FormRowFlattener.Flatten(IReadOnlyList<ViTri> viTris)`.

- [ ] **Step 1: Viết test thất bại**

Tạo `tests/ConstructionFinance.Application.Tests/Forms/FormRowFlattenerTests.cs`:

```csharp
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Forms;

public class FormRowFlattenerTests
{
    private static ViTri ViTri(string name, int sort, params Dot[] dots) => new()
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
                        Name = "Gói A", ContractorName = "Cty XL A", BankAccount = "123456789",
                        ContractValue = 50_000_000_000m, ContractAnnexValue = 2_000_000_000m,
                        SortOrder = 0, Dots = dots.ToList()
                    }
                }
            }
        }
    };

    private static Dot Advance(int sort, decimal amount, Fund fund = Fund.NSNN) => new()
    {
        SortOrder = sort, Date = new DateOnly(2024, 11, 28), Content = $"Tạm ứng lần {sort + 1}",
        DocumentNo = "CT01", Fund = fund, Advance = amount
    };

    private static Dot Payment(int sort, decimal klht, decimal recovered, Fund fund = Fund.NSNN) => new()
    {
        SortOrder = sort, Date = new DateOnly(2025, 5, 14), Content = "Thanh toán KLHT",
        Fund = fund, PaymentKLHT = klht, RecoveredAdvance = recovered
    };

    [Fact]
    public void Flatten_emits_one_row_per_dot_in_ledger_order()
    {
        var rows = FormRowFlattener.Flatten(new[]
        {
            ViTri("Vị trí 1", 0, Advance(0, 10_000_000m), Payment(1, 200_000_000m, 5_000_000m)),
            ViTri("Vị trí 2", 1, Advance(0, 30_000_000m))
        });

        rows.Count.ShouldBe(3);
        rows[0].ViTriName.ShouldBe("Vị trí 1");
        rows[0].Content.ShouldBe("Tạm ứng lần 1");
        rows[2].ViTriName.ShouldBe("Vị trí 2");
    }

    [Fact]
    public void Flatten_keys_each_row_by_its_position_in_the_tree()
    {
        var rows = FormRowFlattener.Flatten(new[]
        {
            ViTri("Vị trí 1", 0, Advance(0, 10_000_000m), Payment(1, 200_000_000m, 5_000_000m)),
            ViTri("Vị trí 2", 1, Advance(0, 30_000_000m))
        });

        rows[0].Key.ShouldBe(new FormDotKey(0, 0, 0, 0));
        rows[1].Key.ShouldBe(new FormDotKey(0, 0, 0, 1));
        rows[2].Key.ShouldBe(new FormDotKey(1, 0, 0, 0));
    }

    [Fact]
    public void Flatten_carries_package_context_onto_every_row()
    {
        var row = FormRowFlattener.Flatten(new[] { ViTri("Vị trí 1", 0, Advance(0, 10_000_000m)) })[0];

        row.CostGroupName.ShouldBe("CHI PHÍ XÂY DỰNG");
        row.PackageName.ShouldBe("Gói A");
        row.ContractorName.ShouldBe("Cty XL A");
        row.BankAccount.ShouldBe("123456789");
        row.ContractValue.ShouldBe(50_000_000_000m);
        row.ContractAnnexValue.ShouldBe(2_000_000_000m);
        row.DocumentNo.ShouldBe("CT01");
    }

    [Fact]
    public void Flatten_computes_disbursed_the_same_way_the_ledger_does()
    {
        var rows = FormRowFlattener.Flatten(new[]
        {
            ViTri("Vị trí 1", 0, Advance(0, 10_000_000m), Payment(1, 200_000_000m, 5_000_000m))
        });

        rows[0].Disbursed.ShouldBe(10_000_000m);        // đợt tạm ứng → chính số tạm ứng
        rows[1].Disbursed.ShouldBe(195_000_000m);       // đợt thanh toán → KLHT − thu hồi
    }

    [Fact]
    public void Flatten_orders_vitris_groups_packages_and_dots_by_SortOrder()
    {
        var viTri = ViTri("Vị trí 1", 0, Payment(1, 200_000_000m, 0m), Advance(0, 10_000_000m));

        var rows = FormRowFlattener.Flatten(new[] { viTri });

        rows[0].Advance.ShouldBe(10_000_000m);          // SortOrder 0 trước, dù thêm sau
        rows[1].PaymentKLHT.ShouldBe(200_000_000m);
    }

    [Fact]
    public void Flatten_returns_empty_for_a_vitri_with_no_dots() =>
        FormRowFlattener.Flatten(new[] { ViTri("Vị trí 1", 0) }).ShouldBeEmpty();
}
```

- [ ] **Step 2: Chạy test cho chắc là đỏ**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter FullyQualifiedName~FormRowFlattenerTests`
Expected: FAIL — `FormRowFlattener` chưa tồn tại.

- [ ] **Step 3: Viết model biểu mẫu**

Tạo `src/ConstructionFinance.Application/Forms/FormModels.cs`:

```csharp
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Forms;

/// Vị trí của một đợt trong cây, dùng làm khóa chọn. Chỉ số theo thứ tự sổ.
public readonly record struct FormDotKey(int ViTriIndex, int GroupIndex, int PackageIndex, int DotIndex);

/// Một đợt đã trải phẳng, mang đủ ngữ cảnh để builder không phải đi lại cây.
public record FormDotRow(
    FormDotKey Key,
    string ViTriName,
    string CostGroupName,
    string PackageName,
    string? ContractorName,
    string? BankAccount,
    decimal ContractValue,
    decimal ContractAnnexValue,
    DateOnly Date,
    string? Content,
    string? DocumentNo,
    Fund Fund,
    decimal Advance,
    decimal PaymentKLHT,
    decimal RecoveredAdvance,
    decimal Disbursed);

/// Tên đơn vị in trên đầu Thông tri và GĐNTT. Lưu ở Cài đặt (dùng chung mọi dự án),
/// không lưu theo dự án.
public record FormOrgInfo(string? DonViCapTren, string? DonViCoSo);

/// Một dòng trong bảng Thông tri. Level 0 = dự án, 1 = vị trí, 2 = đợt.
public record ThongTriLine(int Level, string Content, decimal Amount);

public record ThongTriForm(
    string? DonViCapTren,
    string? DonViCoSo,
    string? SoThongTri,
    string? NgayGhiSo,
    string? TenCTMT,
    string? MaSo,
    string? VeThangNam,
    string? TaiKhoan1,
    string? DonVi1,
    string? SoChungTu1,
    string? TaiKhoan2,
    string? DonVi2,
    string? SoChungTu2,
    string? LoaiNganSach,
    string? Khoan,
    string? Muc,
    string? TieuMuc,
    string? TietMuc,
    string? Nganh,
    IReadOnlyList<ThongTriLine> Lines,
    decimal Total,
    string TotalInWords);

/// Một dòng tiền của KS chi, tách theo nguồn.
public record KsChiAmounts(decimal Total, decimal NSNN, decimal NgoaiHoi);

public record KsChiForm(
    string? NgayNhanGiayDeNghi,
    IReadOnlyList<string> MucTieuMucLines,
    KsChiAmounts Accepted,
    KsChiAmounts Recovered,
    decimal ThuHoiCacNamTruoc,
    decimal ThuHoiNamNay,
    decimal ThueGTGT,
    KsChiAmounts PaidToBeneficiary,
    string PaidInWords,
    decimal SoTuChoi,
    string? LyDoTuChoi,
    string GhiChu);

public record GdnttForm(
    string? DonViCapTren,
    string? DonViCoSo,
    string? So,
    string? NgayThangNam,
    string? KinhGui,
    string? TenDuAnCongTrinh,
    string? ChuDauTu,
    string? CanCuQuyetDinh,
    string? CanCuHopDong,
    string? CanCuGiayDeNghi,
    string? ThuocKeHoach,
    string PackageName,
    string? ContractorName,
    string? BankAccount,
    string? TaiNganHang,
    decimal UnrecoveredAdvanceBefore,
    decimal ContractTotal,
    decimal CumulativeBefore,
    decimal RequestedThisPeriod,
    string RequestedInWords,
    decimal RecoveredThisPeriod,
    decimal ChuyenTienBaoHanh,
    decimal ChuyenTienTamGiu,
    decimal PaidToContractor,
    string NguonVon);

/// GĐNTT chỉ dựng được cho MỘT gói thầu, nên kết quả có thể là một lời từ chối
/// kèm lý do hiển thị cho người dùng thay vì ném ngoại lệ.
public record GdnttBuildResult(GdnttForm? Form, string? Error);
```

- [ ] **Step 4: Viết bộ trải cây**

Tạo `src/ConstructionFinance.Application/Forms/FormRowFlattener.cs`:

```csharp
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Forms;

/// Trải cây Vị trí → nhóm chi phí → gói thầu → đợt thành một danh sách phẳng theo
/// đúng thứ tự người dùng thấy trong sổ. Khóa của mỗi dòng là vị trí của nó trong
/// cây, nên Desktop quy được lựa chọn (giữ theo tham chiếu dòng lưới) về khóa.
public static class FormRowFlattener
{
    public static IReadOnlyList<FormDotRow> Flatten(IReadOnlyList<ViTri> viTris)
    {
        var rows = new List<FormDotRow>();
        var orderedViTris = viTris.OrderBy(v => v.SortOrder).ToList();

        for (var vi = 0; vi < orderedViTris.Count; vi++)
        {
            var groups = orderedViTris[vi].CostGroups.OrderBy(g => g.SortOrder).ToList();
            for (var gi = 0; gi < groups.Count; gi++)
            {
                var packages = groups[gi].Packages.OrderBy(p => p.SortOrder).ToList();
                for (var pi = 0; pi < packages.Count; pi++)
                {
                    var package = packages[pi];
                    var dots = package.Dots.OrderBy(d => d.SortOrder).ToList();
                    for (var di = 0; di < dots.Count; di++)
                    {
                        var dot = dots[di];
                        rows.Add(new FormDotRow(
                            Key: new FormDotKey(vi, gi, pi, di),
                            ViTriName: orderedViTris[vi].Name,
                            CostGroupName: groups[gi].Name,
                            PackageName: package.Name,
                            ContractorName: package.ContractorName,
                            BankAccount: package.BankAccount,
                            ContractValue: package.ContractValue,
                            ContractAnnexValue: package.ContractAnnexValue,
                            Date: dot.Date,
                            Content: dot.Content,
                            DocumentNo: dot.DocumentNo,
                            Fund: dot.Fund,
                            Advance: dot.Advance,
                            PaymentKLHT: dot.PaymentKLHT,
                            RecoveredAdvance: dot.RecoveredAdvance,
                            // Một nguồn công thức duy nhất: dùng lại đúng hàm của sổ.
                            Disbursed: TreeCalculator.Disbursed(dot.ToDotInput())));
                    }
                }
            }
        }

        return rows;
    }
}
```

- [ ] **Step 5: Chạy test cho chắc là xanh**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter FullyQualifiedName~FormRowFlattenerTests`
Expected: PASS — cả 6 ca.

- [ ] **Step 6: Commit**

```bash
git add src/ConstructionFinance.Application/Forms/FormModels.cs \
        src/ConstructionFinance.Application/Forms/FormRowFlattener.cs \
        tests/ConstructionFinance.Application.Tests/Forms/FormRowFlattenerTests.cs
git commit -m "feat(app): model biểu mẫu + trải cây Vị trí thành danh sách đợt phẳng"
```

---

### Task 4: Builder Thông tri

Đọc spec §4.1 trước khi làm.

**Files:**
- Create: `src/ConstructionFinance.Application/Forms/ThongTriBuilder.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Forms/ThongTriBuilderTests.cs`

**Interfaces:**
- Consumes: `FormDotRow`, `FormDotKey`, `FormOrgInfo`, `ThongTriLine`, `ThongTriForm` (Task 3); `ThongTriDeclaration` (Task 2); `VietnameseNumberToWords.ToWords` (Task 1).
- Produces: `static ThongTriForm ThongTriBuilder.Build(IReadOnlyList<FormDotRow> all, IReadOnlySet<FormDotKey> selected, ThongTriDeclaration? declaration, FormOrgInfo org, string projectName)`.

- [ ] **Step 1: Viết test thất bại**

Tạo `tests/ConstructionFinance.Application.Tests/Forms/ThongTriBuilderTests.cs`:

```csharp
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Forms;

public class ThongTriBuilderTests
{
    private static readonly FormOrgInfo Org = new("Sở Tài chính", "Ban QLDA số 1");

    private static FormDotRow Row(FormDotKey key, string viTri, Fund fund, decimal disbursed, string content) =>
        new(key, viTri, "CHI PHÍ XÂY DỰNG", "Gói A", "Cty XL A", "123456789",
            50_000_000_000m, 0m, new DateOnly(2026, 3, 1), content, "CT01", fund,
            Advance: disbursed, PaymentKLHT: 0m, RecoveredAdvance: 0m, Disbursed: disbursed);

    private static IReadOnlyList<FormDotRow> TwoViTris() => new[]
    {
        Row(new FormDotKey(0, 0, 0, 0), "Vị trí 1", Fund.NSNN, 10_000_000m, "Tạm ứng lần 1"),
        Row(new FormDotKey(0, 0, 0, 1), "Vị trí 1", Fund.NSNN, 5_000_000m, "Tạm ứng lần 2"),
        Row(new FormDotKey(1, 0, 0, 0), "Vị trí 2", Fund.NSNN, 20_000_000m, "Tạm ứng lần 1"),
        Row(new FormDotKey(2, 0, 0, 0), "Vị trí 3", Fund.NgoaiHoi, 99_000_000m, "Ngoại hối")
    };

    private static HashSet<FormDotKey> All(IReadOnlyList<FormDotRow> rows) =>
        rows.Select(r => r.Key).ToHashSet();

    [Fact]
    public void Build_nests_project_then_vitri_then_dot_lines()
    {
        var rows = TwoViTris();

        var form = ThongTriBuilder.Build(rows, All(rows), null, Org, "Dự án A");

        form.Lines.Select(l => (l.Level, l.Content, l.Amount)).ShouldBe(new[]
        {
            (0, "Dự án A", 35_000_000m),
            (1, "Vị trí 1", 15_000_000m),
            (2, "Tạm ứng lần 1", 10_000_000m),
            (2, "Tạm ứng lần 2", 5_000_000m),
            (1, "Vị trí 2", 20_000_000m),
            (2, "Tạm ứng lần 1", 20_000_000m)
        });
    }

    [Fact]
    public void Build_drops_dots_from_the_foreign_currency_fund()
    {
        var rows = TwoViTris();

        var form = ThongTriBuilder.Build(rows, All(rows), null, Org, "Dự án A");

        form.Lines.ShouldNotContain(l => l.Content == "Ngoại hối");
        form.Total.ShouldBe(35_000_000m);
    }

    [Fact]
    public void Build_only_includes_the_selected_dots()
    {
        var rows = TwoViTris();
        var selected = new HashSet<FormDotKey> { new(0, 0, 0, 0) };

        var form = ThongTriBuilder.Build(rows, selected, null, Org, "Dự án A");

        form.Total.ShouldBe(10_000_000m);
        form.Lines.Count.ShouldBe(3);           // dự án + vị trí 1 + một đợt
        form.Lines.ShouldNotContain(l => l.Content == "Vị trí 2");
    }

    [Fact]
    public void Build_reads_the_total_in_words()
    {
        var rows = TwoViTris();

        var form = ThongTriBuilder.Build(rows, All(rows), null, Org, "Dự án A");

        form.TotalInWords.ShouldBe("Ba mươi lăm triệu đồng.");
    }

    [Fact]
    public void Build_copies_the_declaration_and_org_fields_through()
    {
        var rows = TwoViTris();
        var declaration = new ThongTriDeclaration
        {
            SoThongTri = "12/TT", NgayGhiSo = "05/03/2026", TenCTMT = "CTMT X", MaSo = "A1",
            VeThangNam = "tháng 3 năm 2026", TaiKhoan1 = "TK1", DonVi1 = "ĐV1", SoChungTu1 = "CT1",
            TaiKhoan2 = "TK2", DonVi2 = "ĐV2", SoChungTu2 = "CT2",
            LoaiNganSach = "10", Khoan = "11", Muc = "9300", TieuMuc = "9301", TietMuc = "0", Nganh = "0"
        };

        var form = ThongTriBuilder.Build(rows, All(rows), declaration, Org, "Dự án A");

        form.DonViCapTren.ShouldBe("Sở Tài chính");
        form.DonViCoSo.ShouldBe("Ban QLDA số 1");
        form.SoThongTri.ShouldBe("12/TT");
        form.VeThangNam.ShouldBe("tháng 3 năm 2026");
        form.Khoan.ShouldBe("11");
        form.TieuMuc.ShouldBe("9301");
        form.SoChungTu2.ShouldBe("CT2");
    }

    [Fact]
    public void Build_with_nothing_selected_yields_a_zero_project_line_only()
    {
        var rows = TwoViTris();

        var form = ThongTriBuilder.Build(rows, new HashSet<FormDotKey>(), null, Org, "Dự án A");

        form.Total.ShouldBe(0m);
        form.Lines.Count.ShouldBe(1);
        form.Lines[0].Level.ShouldBe(0);
        form.TotalInWords.ShouldBe("Không đồng.");
    }

    [Fact]
    public void Build_uses_a_placeholder_when_a_dot_has_no_content()
    {
        var rows = new[] { Row(new FormDotKey(0, 0, 0, 0), "Vị trí 1", Fund.NSNN, 1_000m, "") };

        var form = ThongTriBuilder.Build(rows, All(rows), null, Org, "Dự án A");

        form.Lines[2].Content.ShouldBe("(chưa có nội dung)");
    }
}
```

- [ ] **Step 2: Chạy test cho chắc là đỏ**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter FullyQualifiedName~ThongTriBuilderTests`
Expected: FAIL — `ThongTriBuilder` chưa tồn tại.

- [ ] **Step 3: Viết builder**

Tạo `src/ConstructionFinance.Application/Forms/ThongTriBuilder.cs`:

```csharp
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Forms;

/// Dựng Thông tri (Mẫu số CQ06-HD) từ các đợt NSNN người dùng đã tích chọn.
/// Bảng in ba cấp: dự án → vị trí → đợt, số tiền mỗi đợt là giải ngân của đợt.
public static class ThongTriBuilder
{
    private const string NoContent = "(chưa có nội dung)";

    public static ThongTriForm Build(
        IReadOnlyList<FormDotRow> all,
        IReadOnlySet<FormDotKey> selected,
        ThongTriDeclaration? declaration,
        FormOrgInfo org,
        string projectName)
    {
        // Thông tri chỉ nói về kinh phí NSNN; đợt ngoại hối đi đường GĐNTT.
        var rows = all.Where(r => selected.Contains(r.Key) && r.Fund == Fund.NSNN).ToList();
        var total = rows.Sum(r => r.Disbursed);

        var lines = new List<ThongTriLine> { new(0, projectName, total) };
        // GroupBy giữ nguyên thứ tự xuất hiện, mà `all` đã theo thứ tự sổ.
        foreach (var group in rows.GroupBy(r => r.ViTriName))
        {
            lines.Add(new ThongTriLine(1, group.Key, group.Sum(r => r.Disbursed)));
            foreach (var row in group)
                lines.Add(new ThongTriLine(2,
                    string.IsNullOrWhiteSpace(row.Content) ? NoContent : row.Content!,
                    row.Disbursed));
        }

        return new ThongTriForm(
            DonViCapTren: org.DonViCapTren,
            DonViCoSo: org.DonViCoSo,
            SoThongTri: declaration?.SoThongTri,
            NgayGhiSo: declaration?.NgayGhiSo,
            TenCTMT: declaration?.TenCTMT,
            MaSo: declaration?.MaSo,
            VeThangNam: declaration?.VeThangNam,
            TaiKhoan1: declaration?.TaiKhoan1,
            DonVi1: declaration?.DonVi1,
            SoChungTu1: declaration?.SoChungTu1,
            TaiKhoan2: declaration?.TaiKhoan2,
            DonVi2: declaration?.DonVi2,
            SoChungTu2: declaration?.SoChungTu2,
            LoaiNganSach: declaration?.LoaiNganSach,
            Khoan: declaration?.Khoan,
            Muc: declaration?.Muc,
            TieuMuc: declaration?.TieuMuc,
            TietMuc: declaration?.TietMuc,
            Nganh: declaration?.Nganh,
            Lines: lines,
            Total: total,
            TotalInWords: VietnameseNumberToWords.ToWords(total));
    }
}
```

- [ ] **Step 4: Chạy test cho chắc là xanh**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter FullyQualifiedName~ThongTriBuilderTests`
Expected: PASS — cả 7 ca.

- [ ] **Step 5: Commit**

```bash
git add src/ConstructionFinance.Application/Forms/ThongTriBuilder.cs \
        tests/ConstructionFinance.Application.Tests/Forms/ThongTriBuilderTests.cs
git commit -m "feat(app): builder Thông tri — gom đợt NSNN theo dự án/vị trí/đợt"
```

---

### Task 5: Builder KS chi

Đọc spec §4.2 trước khi làm.

**Files:**
- Create: `src/ConstructionFinance.Application/Forms/KsChiBuilder.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Forms/KsChiBuilderTests.cs`

**Interfaces:**
- Consumes: `FormDotRow`, `FormDotKey`, `KsChiAmounts`, `KsChiForm` (Task 3); `KsChiDeclaration` (Task 2); `VietnameseNumberToWords.ToWords` (Task 1).
- Produces: `static KsChiForm KsChiBuilder.Build(IReadOnlyList<FormDotRow> all, IReadOnlySet<FormDotKey> selected, KsChiDeclaration? declaration)`.

- [ ] **Step 1: Viết test thất bại**

Tạo `tests/ConstructionFinance.Application.Tests/Forms/KsChiBuilderTests.cs`:

```csharp
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Forms;

public class KsChiBuilderTests
{
    private static FormDotRow Dot(
        int index, Fund fund, decimal advance, decimal klht, decimal recovered, string content = "Đợt") =>
        new(new FormDotKey(0, 0, 0, index), "Vị trí 1", "CHI PHÍ XÂY DỰNG", "Gói A",
            "Cty XL A", "123456789", 50_000_000_000m, 0m, new DateOnly(2026, 3, 1), content, "CT01",
            fund, advance, klht, recovered,
            Disbursed: advance > 0m && klht == 0m ? advance : klht - recovered);

    // Một đợt tạm ứng NSNN 10tr, một đợt thanh toán NSNN (KLHT 200tr, thu hồi 5tr),
    // một đợt tạm ứng ngoại hối 30tr.
    private static IReadOnlyList<FormDotRow> Sample() => new[]
    {
        Dot(0, Fund.NSNN, 10_000_000m, 0m, 0m, "Tạm ứng lần 1"),
        Dot(1, Fund.NSNN, 0m, 200_000_000m, 5_000_000m, "Thanh toán KLHT"),
        Dot(2, Fund.NgoaiHoi, 30_000_000m, 0m, 0m, "Tạm ứng ngoại hối")
    };

    private static HashSet<FormDotKey> All(IReadOnlyList<FormDotRow> rows) =>
        rows.Select(r => r.Key).ToHashSet();

    [Fact]
    public void Build_sums_accepted_capital_as_advance_plus_klht_split_by_fund()
    {
        var rows = Sample();

        var form = KsChiBuilder.Build(rows, All(rows), null);

        form.Accepted.Total.ShouldBe(240_000_000m);
        form.Accepted.NSNN.ShouldBe(210_000_000m);
        form.Accepted.NgoaiHoi.ShouldBe(30_000_000m);
    }

    [Fact]
    public void Build_sums_recovered_advance_split_by_fund()
    {
        var rows = Sample();

        var form = KsChiBuilder.Build(rows, All(rows), null);

        form.Recovered.Total.ShouldBe(5_000_000m);
        form.Recovered.NSNN.ShouldBe(5_000_000m);
        form.Recovered.NgoaiHoi.ShouldBe(0m);
    }

    [Fact]
    public void Build_pays_beneficiary_accepted_minus_recovered_which_equals_total_disbursed()
    {
        var rows = Sample();

        var form = KsChiBuilder.Build(rows, All(rows), null);

        form.PaidToBeneficiary.Total.ShouldBe(235_000_000m);
        form.PaidToBeneficiary.NSNN.ShouldBe(205_000_000m);
        form.PaidToBeneficiary.NgoaiHoi.ShouldBe(30_000_000m);
        form.PaidToBeneficiary.Total.ShouldBe(rows.Sum(r => r.Disbursed));
    }

    [Fact]
    public void Build_reads_the_beneficiary_amount_in_words()
    {
        var rows = Sample();

        var form = KsChiBuilder.Build(rows, All(rows), null);

        form.PaidInWords.ShouldBe(
            "Hai trăm ba mươi lăm triệu đồng.");
    }

    [Fact]
    public void Build_only_counts_the_selected_dots()
    {
        var rows = Sample();
        var selected = new HashSet<FormDotKey> { new(0, 0, 0, 0) };

        var form = KsChiBuilder.Build(rows, selected, null);

        form.Accepted.Total.ShouldBe(10_000_000m);
        form.PaidToBeneficiary.Total.ShouldBe(10_000_000m);
    }

    [Fact]
    public void Build_falls_back_to_the_first_selected_dot_content_for_ghi_chu()
    {
        var rows = Sample();

        var form = KsChiBuilder.Build(rows, All(rows), new KsChiDeclaration { GhiChu = "  " });

        form.GhiChu.ShouldBe("Tạm ứng lần 1");
    }

    [Fact]
    public void Build_keeps_a_typed_ghi_chu()
    {
        var rows = Sample();

        var form = KsChiBuilder.Build(rows, All(rows), new KsChiDeclaration { GhiChu = "Ghi tay" });

        form.GhiChu.ShouldBe("Ghi tay");
    }

    [Fact]
    public void Build_copies_the_manual_fields_through()
    {
        var rows = Sample();
        var declaration = new KsChiDeclaration
        {
            NgayNhanGiayDeNghi = "05/03/2026",
            MucTieuMuc1 = "Mục 9300, tiểu mục 9301",
            MucTieuMuc3 = "Mục 9400, tiểu mục 9401",
            ThuHoiCacNamTruoc = 1_000_000m,
            ThuHoiNamNay = 4_000_000m,
            ThueGTGT = 2_000_000m,
            SoTuChoi = 500_000m,
            LyDoTuChoi = "thiếu hồ sơ"
        };

        var form = KsChiBuilder.Build(rows, All(rows), declaration);

        form.NgayNhanGiayDeNghi.ShouldBe("05/03/2026");
        form.MucTieuMucLines.Count.ShouldBe(4);
        form.MucTieuMucLines[0].ShouldBe("Mục 9300, tiểu mục 9301");
        form.MucTieuMucLines[1].ShouldBe("");
        form.MucTieuMucLines[2].ShouldBe("Mục 9400, tiểu mục 9401");
        form.ThuHoiCacNamTruoc.ShouldBe(1_000_000m);
        form.ThuHoiNamNay.ShouldBe(4_000_000m);
        form.ThueGTGT.ShouldBe(2_000_000m);
        form.SoTuChoi.ShouldBe(500_000m);
        form.LyDoTuChoi.ShouldBe("thiếu hồ sơ");
    }

    [Fact]
    public void Build_with_nothing_selected_is_all_zero_and_has_an_empty_ghi_chu()
    {
        var rows = Sample();

        var form = KsChiBuilder.Build(rows, new HashSet<FormDotKey>(), null);

        form.Accepted.Total.ShouldBe(0m);
        form.PaidToBeneficiary.Total.ShouldBe(0m);
        form.PaidInWords.ShouldBe("Không đồng.");
        form.GhiChu.ShouldBe("");
    }
}
```

- [ ] **Step 2: Chạy test cho chắc là đỏ**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter FullyQualifiedName~KsChiBuilderTests`
Expected: FAIL — `KsChiBuilder` chưa tồn tại.

- [ ] **Step 3: Viết builder**

Tạo `src/ConstructionFinance.Application/Forms/KsChiBuilder.cs`:

```csharp
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Forms;

/// Dựng phần ghi của cơ quan kiểm soát, thanh toán từ các đợt đã tích chọn.
/// Khác hai form kia, KS chi lấy CẢ hai nguồn và tách ba cột
/// Tổng số / Vốn trong nước (NSNN) / Vốn nước ngoài (Ngoại hối).
public static class KsChiBuilder
{
    public static KsChiForm Build(
        IReadOnlyList<FormDotRow> all,
        IReadOnlySet<FormDotKey> selected,
        KsChiDeclaration? declaration)
    {
        var rows = all.Where(r => selected.Contains(r.Key)).ToList();

        var accepted = Split(rows, r => r.Advance + r.PaymentKLHT);
        var recovered = Split(rows, r => r.RecoveredAdvance);
        var paid = new KsChiAmounts(
            accepted.Total - recovered.Total,
            accepted.NSNN - recovered.NSNN,
            accepted.NgoaiHoi - recovered.NgoaiHoi);

        return new KsChiForm(
            NgayNhanGiayDeNghi: declaration?.NgayNhanGiayDeNghi,
            MucTieuMucLines: new[]
            {
                declaration?.MucTieuMuc1 ?? "",
                declaration?.MucTieuMuc2 ?? "",
                declaration?.MucTieuMuc3 ?? "",
                declaration?.MucTieuMuc4 ?? ""
            },
            Accepted: accepted,
            Recovered: recovered,
            ThuHoiCacNamTruoc: declaration?.ThuHoiCacNamTruoc ?? 0m,
            ThuHoiNamNay: declaration?.ThuHoiNamNay ?? 0m,
            ThueGTGT: declaration?.ThueGTGT ?? 0m,
            PaidToBeneficiary: paid,
            PaidInWords: VietnameseNumberToWords.ToWords(paid.Total),
            SoTuChoi: declaration?.SoTuChoi ?? 0m,
            LyDoTuChoi: declaration?.LyDoTuChoi,
            GhiChu: ResolveGhiChu(rows, declaration));
    }

    private static KsChiAmounts Split(IReadOnlyList<FormDotRow> rows, Func<FormDotRow, decimal> pick) =>
        new(rows.Sum(pick),
            rows.Where(r => r.Fund == Fund.NSNN).Sum(pick),
            rows.Where(r => r.Fund == Fund.NgoaiHoi).Sum(pick));

    /// Ô Ghi chú để trống thì in nội dung của đợt được chọn đầu tiên theo thứ tự sổ
    /// (Mẫu trỏ ô này sang dòng nội dung của Thông tri).
    private static string ResolveGhiChu(IReadOnlyList<FormDotRow> rows, KsChiDeclaration? declaration)
    {
        if (!string.IsNullOrWhiteSpace(declaration?.GhiChu)) return declaration!.GhiChu!;
        return rows.FirstOrDefault()?.Content ?? "";
    }
}
```

- [ ] **Step 4: Chạy test cho chắc là xanh**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter FullyQualifiedName~KsChiBuilderTests`
Expected: PASS — cả 9 ca.

- [ ] **Step 5: Commit**

```bash
git add src/ConstructionFinance.Application/Forms/KsChiBuilder.cs \
        tests/ConstructionFinance.Application.Tests/Forms/KsChiBuilderTests.cs
git commit -m "feat(app): builder KS chi — ba cột nguồn vốn + số trả đơn vị thụ hưởng"
```

---

### Task 6: Builder GĐNTT

Đọc spec §4.3 trước khi làm — nhất là định nghĩa "kỳ trước" và hai chỗ cố ý lệch Mẫu.

**Files:**
- Create: `src/ConstructionFinance.Application/Forms/GdnttBuilder.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Forms/GdnttBuilderTests.cs`

**Interfaces:**
- Consumes: `FormDotRow`, `FormDotKey`, `FormOrgInfo`, `GdnttForm`, `GdnttBuildResult` (Task 3); `GdnttDeclaration` (Task 2); `VietnameseNumberToWords.ToWords` (Task 1).
- Produces: `static GdnttBuildResult GdnttBuilder.Build(IReadOnlyList<FormDotRow> all, IReadOnlySet<FormDotKey> selected, GdnttDeclaration? declaration, FormOrgInfo org)`.

- [ ] **Step 1: Viết test thất bại**

Tạo `tests/ConstructionFinance.Application.Tests/Forms/GdnttBuilderTests.cs`:

```csharp
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Forms;

public class GdnttBuilderTests
{
    private static readonly FormOrgInfo Org = new("Sở Tài chính", "Ban QLDA số 1");

    private static FormDotRow Dot(
        FormDotKey key, decimal advance, decimal klht, decimal recovered,
        Fund fund = Fund.NgoaiHoi, string packageName = "Gói A") =>
        new(key, "Vị trí 1", "CHI PHÍ XÂY DỰNG", packageName, "Cty XL A", "123456789",
            50_000_000_000m, 2_000_000_000m, new DateOnly(2026, 3, 1), "Đợt", "CT01",
            fund, advance, klht, recovered,
            Disbursed: advance > 0m && klht == 0m ? advance : klht - recovered);

    /// Gói A: đợt 0 tạm ứng 10tr, đợt 1 thanh toán KLHT 200tr thu hồi 4tr,
    /// đợt 2 tạm ứng 30tr (kỳ này).
    private static IReadOnlyList<FormDotRow> OnePackage() => new[]
    {
        Dot(new FormDotKey(0, 0, 0, 0), 10_000_000m, 0m, 0m),
        Dot(new FormDotKey(0, 0, 0, 1), 0m, 200_000_000m, 4_000_000m),
        Dot(new FormDotKey(0, 0, 0, 2), 30_000_000m, 0m, 0m)
    };

    [Fact]
    public void Build_sums_this_period_as_advance_plus_klht_of_the_selected_dots()
    {
        var rows = OnePackage();
        var selected = new HashSet<FormDotKey> { new(0, 0, 0, 2) };

        var form = GdnttBuilder.Build(rows, selected, null, Org).Form;

        form!.RequestedThisPeriod.ShouldBe(30_000_000m);
        form.RequestedInWords.ShouldBe("Ba mươi triệu đồng.");
    }

    [Fact]
    public void Build_takes_the_cumulative_from_the_dots_before_the_earliest_selected_one()
    {
        var rows = OnePackage();
        var selected = new HashSet<FormDotKey> { new(0, 0, 0, 2) };

        var form = GdnttBuilder.Build(rows, selected, null, Org).Form;

        // đợt 0 (10tr tạm ứng) + đợt 1 (200tr KLHT) = 210tr, "gồm cả tạm ứng"
        form!.CumulativeBefore.ShouldBe(210_000_000m);
        // số dư tạm ứng đến hết kỳ trước = 10tr − 4tr
        form.UnrecoveredAdvanceBefore.ShouldBe(6_000_000m);
    }

    [Fact]
    public void Build_has_no_prior_period_when_the_first_dot_is_selected()
    {
        var rows = OnePackage();
        var selected = new HashSet<FormDotKey> { new(0, 0, 0, 0) };

        var form = GdnttBuilder.Build(rows, selected, null, Org).Form;

        form!.CumulativeBefore.ShouldBe(0m);
        form.UnrecoveredAdvanceBefore.ShouldBe(0m);
    }

    [Fact]
    public void Build_adds_the_contract_annex_to_the_contract_value()
    {
        var rows = OnePackage();
        var selected = new HashSet<FormDotKey> { new(0, 0, 0, 2) };

        var form = GdnttBuilder.Build(rows, selected, null, Org).Form;

        form!.ContractTotal.ShouldBe(52_000_000_000m);
    }

    [Fact]
    public void Build_pays_the_contractor_after_recovery_retention_and_warranty()
    {
        var rows = OnePackage();
        var selected = new HashSet<FormDotKey> { new(0, 0, 0, 1) };
        var declaration = new GdnttDeclaration
        {
            ChuyenTienBaoHanh = 2_000_000m,
            ChuyenTienTamGiu = 1_000_000m
        };

        var form = GdnttBuilder.Build(rows, selected, declaration, Org).Form;

        form!.RequestedThisPeriod.ShouldBe(200_000_000m);
        form.RecoveredThisPeriod.ShouldBe(4_000_000m);
        form.PaidToContractor.ShouldBe(193_000_000m);   // 200 − 4 − 2 − 1
    }

    [Fact]
    public void Build_takes_beneficiary_and_account_from_the_package()
    {
        var rows = OnePackage();
        var selected = new HashSet<FormDotKey> { new(0, 0, 0, 2) };

        var form = GdnttBuilder.Build(rows, selected, new GdnttDeclaration
        {
            TaiNganHang = "Vietcombank Hải Phòng", KinhGui = "KBNN Hải Phòng"
        }, Org).Form;

        form!.PackageName.ShouldBe("Gói A");
        form.ContractorName.ShouldBe("Cty XL A");
        form.BankAccount.ShouldBe("123456789");
        form.TaiNganHang.ShouldBe("Vietcombank Hải Phòng");
        form.KinhGui.ShouldBe("KBNN Hải Phòng");
        form.DonViCapTren.ShouldBe("Sở Tài chính");
        form.NguonVon.ShouldBe("Quỹ dự trữ ngoại hối");
    }

    [Fact]
    public void Build_refuses_dots_spanning_more_than_one_package()
    {
        var rows = new[]
        {
            Dot(new FormDotKey(0, 0, 0, 0), 10_000_000m, 0m, 0m),
            Dot(new FormDotKey(0, 0, 1, 0), 20_000_000m, 0m, 0m, packageName: "Gói B")
        };
        var selected = rows.Select(r => r.Key).ToHashSet();

        var result = GdnttBuilder.Build(rows, selected, null, Org);

        result.Form.ShouldBeNull();
        result.Error.ShouldContain("một gói thầu");
    }

    [Fact]
    public void Build_refuses_when_no_foreign_currency_dot_is_selected()
    {
        var rows = new[] { Dot(new FormDotKey(0, 0, 0, 0), 10_000_000m, 0m, 0m, Fund.NSNN) };

        var result = GdnttBuilder.Build(rows, rows.Select(r => r.Key).ToHashSet(), null, Org);

        result.Form.ShouldBeNull();
        result.Error.ShouldContain("Quỹ dự trữ ngoại hối");
    }

    [Fact]
    public void Build_ignores_dots_of_other_packages_when_computing_the_prior_period()
    {
        var rows = new[]
        {
            Dot(new FormDotKey(0, 0, 0, 0), 10_000_000m, 0m, 0m),
            Dot(new FormDotKey(0, 0, 1, 0), 500_000_000m, 0m, 0m, packageName: "Gói B"),
            Dot(new FormDotKey(0, 0, 0, 1), 30_000_000m, 0m, 0m)
        };
        var selected = new HashSet<FormDotKey> { new(0, 0, 0, 1) };

        var form = GdnttBuilder.Build(rows, selected, null, Org).Form;

        form!.CumulativeBefore.ShouldBe(10_000_000m);   // không dính 500tr của Gói B
    }

    [Fact]
    public void Build_counts_prior_dots_of_both_funds_because_the_package_state_is_shared()
    {
        var rows = new[]
        {
            Dot(new FormDotKey(0, 0, 0, 0), 10_000_000m, 0m, 0m, Fund.NSNN),
            Dot(new FormDotKey(0, 0, 0, 1), 30_000_000m, 0m, 0m)
        };
        var selected = new HashSet<FormDotKey> { new(0, 0, 0, 1) };

        var form = GdnttBuilder.Build(rows, selected, null, Org).Form;

        form!.CumulativeBefore.ShouldBe(10_000_000m);
        form.UnrecoveredAdvanceBefore.ShouldBe(10_000_000m);
    }
}
```

- [ ] **Step 2: Chạy test cho chắc là đỏ**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter FullyQualifiedName~GdnttBuilderTests`
Expected: FAIL — `GdnttBuilder` chưa tồn tại.

- [ ] **Step 3: Viết builder**

Tạo `src/ConstructionFinance.Application/Forms/GdnttBuilder.cs`:

```csharp
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Forms;

/// Dựng Giấy đề nghị thanh toán vốn đầu tư từ các đợt Ngoại hối đã tích chọn.
/// Một giấy = một gói thầu: đơn vị thụ hưởng, tài khoản, giá trị hợp đồng và lũy kế
/// đều là của gói đó (Mẫu lấy lũy kế theo cả Vị trí — xem spec §4.3, chỗ lệch có chủ ý).
public static class GdnttBuilder
{
    private const string NguonVonNgoaiHoi = "Quỹ dự trữ ngoại hối";

    public static GdnttBuildResult Build(
        IReadOnlyList<FormDotRow> all,
        IReadOnlySet<FormDotKey> selected,
        GdnttDeclaration? declaration,
        FormOrgInfo org)
    {
        var rows = all.Where(r => selected.Contains(r.Key) && r.Fund == Fund.NgoaiHoi).ToList();
        if (rows.Count == 0)
            return new GdnttBuildResult(null,
                $"Chưa chọn đợt nào thuộc {NguonVonNgoaiHoi}. Tích ít nhất một đợt để dựng giấy đề nghị.");

        var packages = rows.Select(PackageOf).Distinct().ToList();
        if (packages.Count > 1)
            return new GdnttBuildResult(null,
                "Các đợt đã chọn thuộc nhiều gói thầu. Một giấy đề nghị chỉ dựng cho một gói thầu — " +
                "hãy bỏ chọn các đợt của gói khác.");

        var package = packages[0];
        var first = rows[0];

        // "Kỳ trước" = các đợt của CHÍNH gói này đứng trước đợt được chọn sớm nhất
        // theo thứ tự sổ. Không lọc theo nguồn: số dư tạm ứng và lũy kế là trạng thái
        // của gói thầu, cả hai nguồn đều đổ vào đó.
        var firstSelectedIndex = rows.Min(r => r.Key.DotIndex);
        var before = all
            .Where(r => PackageOf(r) == package && r.Key.DotIndex < firstSelectedIndex)
            .ToList();

        var requested = rows.Sum(r => r.Advance + r.PaymentKLHT);
        var recovered = rows.Sum(r => r.RecoveredAdvance);
        var baoHanh = declaration?.ChuyenTienBaoHanh ?? 0m;
        var tamGiu = declaration?.ChuyenTienTamGiu ?? 0m;

        var form = new GdnttForm(
            DonViCapTren: org.DonViCapTren,
            DonViCoSo: org.DonViCoSo,
            So: declaration?.So,
            NgayThangNam: declaration?.NgayThangNam,
            KinhGui: declaration?.KinhGui,
            TenDuAnCongTrinh: declaration?.TenDuAnCongTrinh,
            ChuDauTu: declaration?.ChuDauTu,
            CanCuQuyetDinh: declaration?.CanCuQuyetDinh,
            CanCuHopDong: declaration?.CanCuHopDong,
            CanCuGiayDeNghi: declaration?.CanCuGiayDeNghi,
            ThuocKeHoach: declaration?.ThuocKeHoach,
            PackageName: first.PackageName,
            ContractorName: first.ContractorName,
            BankAccount: first.BankAccount,
            TaiNganHang: declaration?.TaiNganHang,
            UnrecoveredAdvanceBefore: before.Sum(r => r.Advance) - before.Sum(r => r.RecoveredAdvance),
            // Phụ lục HĐ làm thay đổi giá trị hợp đồng nên cộng vào (spec §4.3).
            ContractTotal: first.ContractValue + first.ContractAnnexValue,
            CumulativeBefore: before.Sum(r => r.Advance + r.PaymentKLHT),
            RequestedThisPeriod: requested,
            RequestedInWords: VietnameseNumberToWords.ToWords(requested),
            RecoveredThisPeriod: recovered,
            ChuyenTienBaoHanh: baoHanh,
            ChuyenTienTamGiu: tamGiu,
            PaidToContractor: requested - recovered - baoHanh - tamGiu,
            NguonVon: NguonVonNgoaiHoi);

        return new GdnttBuildResult(form, null);
    }

    private static (int ViTri, int Group, int Package) PackageOf(FormDotRow row) =>
        (row.Key.ViTriIndex, row.Key.GroupIndex, row.Key.PackageIndex);
}
```

- [ ] **Step 4: Chạy test cho chắc là xanh**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter FullyQualifiedName~GdnttBuilderTests`
Expected: PASS — cả 10 ca.

- [ ] **Step 5: Commit**

```bash
git add src/ConstructionFinance.Application/Forms/GdnttBuilder.cs \
        tests/ConstructionFinance.Application.Tests/Forms/GdnttBuilderTests.cs
git commit -m "feat(app): builder GĐNTT — lũy kế và số dư tạm ứng theo gói thầu"
```

---

### Task 7: Xuất Excel — hạ tầng + sheet Thông tri

**Files:**
- Create: `src/ConstructionFinance.Application/Services/Interfaces/IFormExcelExporter.cs`
- Create: `src/ConstructionFinance.Application/Forms/FormExcelExporter.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Forms/FormExcelExporterThongTriTests.cs`

**Interfaces:**
- Consumes: `ThongTriForm`, `KsChiForm`, `GdnttForm` (Task 3); ClosedXML.
- Produces: `IFormExcelExporter` với ba phương thức `Task ExportThongTriAsync(ThongTriForm form, string targetPath, CancellationToken ct = default)`, `ExportKsChiAsync(KsChiForm …)`, `ExportGdnttAsync(GdnttForm …)`; lớp `FormExcelExporter` cài đặt cả ba (Task 8 điền hai cái sau).

- [ ] **Step 1: Viết test thất bại**

Tạo `tests/ConstructionFinance.Application.Tests/Forms/FormExcelExporterThongTriTests.cs`:

```csharp
using ClosedXML.Excel;
using ConstructionFinance.Application.Forms;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Forms;

public class FormExcelExporterThongTriTests
{
    private static ThongTriForm Sample() => new(
        DonViCapTren: "Sở Tài chính",
        DonViCoSo: "Ban QLDA số 1",
        SoThongTri: "12/TT",
        NgayGhiSo: "05/03/2026",
        TenCTMT: "CTMT X",
        MaSo: "A1",
        VeThangNam: "tháng 3 năm 2026",
        TaiKhoan1: "TK1", DonVi1: "ĐV1", SoChungTu1: "CT1",
        TaiKhoan2: "TK2", DonVi2: "ĐV2", SoChungTu2: "CT2",
        LoaiNganSach: "10", Khoan: "11", Muc: "9300", TieuMuc: "9301", TietMuc: "0", Nganh: "0",
        Lines: new ThongTriLine[]
        {
            new(0, "Dự án A", 35_000_000m),
            new(1, "Vị trí 1", 15_000_000m),
            new(2, "Tạm ứng lần 1", 10_000_000m),
            new(2, "Tạm ứng lần 2", 5_000_000m),
            new(1, "Vị trí 2", 20_000_000m),
            new(2, "Tạm ứng lần 1", 20_000_000m)
        },
        Total: 35_000_000m,
        TotalInWords: "Ba mươi lăm triệu đồng.");

    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"tt-{Guid.NewGuid():N}.xlsx");

    [Fact]
    public async Task ExportThongTriAsync_writes_the_header_block()
    {
        var path = TempPath();
        try
        {
            await new FormExcelExporter().ExportThongTriAsync(Sample(), path);

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("Thông tri");
            ws.Cell(1, 1).GetString().ShouldBe("Sở Tài chính");
            ws.Cell(1, 7).GetString().ShouldBe("Mẫu số CQ06-HD");
            ws.Cell(2, 1).GetString().ShouldBe("Ban QLDA số 1");
            ws.Cell(3, 1).GetString().ShouldContain("12/TT");
            ws.Cell(3, 7).GetString().ShouldContain("05/03/2026");
            ws.Cell(4, 1).GetString().ShouldBe("THÔNG TRI");
            ws.Cell(9, 1).GetString().ShouldContain("tháng 3 năm 2026");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ExportThongTriAsync_writes_the_budget_index_row()
    {
        var path = TempPath();
        try
        {
            await new FormExcelExporter().ExportThongTriAsync(Sample(), path);

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("Thông tri");
            ws.Cell(15, 1).GetString().ShouldBe("10");     // Loại ngân sách
            ws.Cell(15, 2).GetString().ShouldBe("11");     // Khoản
            ws.Cell(15, 3).GetString().ShouldBe("9300");   // Mục
            ws.Cell(15, 4).GetString().ShouldBe("9301");   // Tiểu mục
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ExportThongTriAsync_writes_one_row_per_line_indented_by_level()
    {
        var path = TempPath();
        try
        {
            await new FormExcelExporter().ExportThongTriAsync(Sample(), path);

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("Thông tri");

            ws.Cell(15, 7).GetString().ShouldBe("Dự án A");
            ws.Cell(15, 8).GetValue<decimal>().ShouldBe(35_000_000m);
            ws.Cell(16, 7).GetString().ShouldBe("Vị trí 1");
            ws.Cell(17, 7).GetString().ShouldBe("Tạm ứng lần 1");
            ws.Cell(17, 8).GetValue<decimal>().ShouldBe(10_000_000m);
            ws.Cell(20, 7).GetString().ShouldBe("Tạm ứng lần 1");
            ws.Cell(20, 8).GetValue<decimal>().ShouldBe(20_000_000m);

            // Dòng Cộng ngay sau dòng cuối, rồi dòng Bằng chữ.
            ws.Cell(21, 7).GetString().ShouldBe("Cộng");
            ws.Cell(21, 8).GetValue<decimal>().ShouldBe(35_000_000m);
            ws.Cell(22, 1).GetString().ShouldBe("Bằng chữ: Ba mươi lăm triệu đồng.");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ExportThongTriAsync_formats_money_with_thousands_separators()
    {
        var path = TempPath();
        try
        {
            await new FormExcelExporter().ExportThongTriAsync(Sample(), path);

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("Thông tri");
            ws.Cell(15, 8).Style.NumberFormat.Format.ShouldBe("#,##0");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ExportThongTriAsync_survives_an_empty_selection()
    {
        var path = TempPath();
        var empty = Sample() with
        {
            Lines = new ThongTriLine[] { new(0, "Dự án A", 0m) },
            Total = 0m,
            TotalInWords = "Không đồng."
        };
        try
        {
            await new FormExcelExporter().ExportThongTriAsync(empty, path);

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("Thông tri");
            ws.Cell(16, 7).GetString().ShouldBe("Cộng");
            ws.Cell(17, 1).GetString().ShouldBe("Bằng chữ: Không đồng.");
        }
        finally { File.Delete(path); }
    }
}
```

- [ ] **Step 2: Chạy test cho chắc là đỏ**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter FullyQualifiedName~FormExcelExporterThongTriTests`
Expected: FAIL — `FormExcelExporter` chưa tồn tại.

- [ ] **Step 3: Khai báo interface**

Tạo `src/ConstructionFinance.Application/Services/Interfaces/IFormExcelExporter.cs`:

```csharp
using ConstructionFinance.Application.Forms;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IFormExcelExporter
{
    Task ExportThongTriAsync(ThongTriForm form, string targetPath, CancellationToken ct = default);
    Task ExportKsChiAsync(KsChiForm form, string targetPath, CancellationToken ct = default);
    Task ExportGdnttAsync(GdnttForm form, string targetPath, CancellationToken ct = default);
}
```

- [ ] **Step 4: Viết exporter với sheet Thông tri**

Tạo `src/ConstructionFinance.Application/Forms/FormExcelExporter.cs`:

```csharp
using ClosedXML.Excel;
using ConstructionFinance.Application.Services.Interfaces;

namespace ConstructionFinance.Application.Forms;

/// Vẽ ba biểu mẫu ra .xlsx bằng ClosedXML. Chỉ trình bày — mọi con số đã do
/// builder tính xong. Bố cục dựng lại theo reference/Mẫu.xlsx.
public partial class FormExcelExporter : IFormExcelExporter
{
    internal const string MoneyFormat = "#,##0";
    internal const string FontName = "Times New Roman";
    internal const double FontSize = 13;

    public Task ExportThongTriAsync(ThongTriForm form, string targetPath, CancellationToken ct = default) =>
        Task.Run(() => WriteThongTri(form, targetPath), ct);

    private static void WriteThongTri(ThongTriForm form, string targetPath)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Thông tri");
        ApplyPageDefaults(ws, XLPageOrientation.Portrait);

        ws.Cell(1, 1).Value = form.DonViCapTren ?? "";
        ws.Cell(1, 7).Value = "Mẫu số CQ06-HD";
        ws.Cell(2, 1).Value = form.DonViCoSo ?? "";
        ws.Cell(3, 1).Value = $"Số      {form.SoThongTri ?? "............."}";
        ws.Cell(3, 7).Value = $"Ghi sổ ngày {form.NgayGhiSo ?? "     tháng     năm"}";

        ws.Cell(4, 1).Value = "THÔNG TRI";
        ws.Cell(5, 1).Value = "Thu hồi tạm ứng";
        ws.Cell(6, 1).Value = "Kinh phí: NSNN";
        foreach (var r in new[] { 4, 5, 6 })
        {
            ws.Range(r, 1, r, 8).Merge();
            ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        ws.Cell(4, 1).Style.Font.Bold = true;

        ws.Cell(8, 1).Value = "Đơn vị:";
        ws.Cell(8, 7).Value = $" TK{Blank(form.TaiKhoan1)}  Đơn vị{Blank(form.DonVi1)}  Số CT{Blank(form.SoChungTu1)}";
        ws.Cell(9, 1).Value = $"Về {form.VeThangNam ?? "tháng      năm"}";
        ws.Cell(9, 7).Value = $" TK{Blank(form.TaiKhoan2)}  Đơn vị{Blank(form.DonVi2)}  Số CT{Blank(form.SoChungTu2)}";
        ws.Cell(10, 1).Value =
            $"Tên CTMT, DA: {form.TenCTMT ?? "................."}    Mã số: {form.MaSo ?? "............."}";
        ws.Cell(11, 7).Value = "Đơn vị tính: Đồng";

        // Đầu bảng: hai dòng 12–13 gộp dọc, dòng 14 là dòng chữ cái A..G như Mẫu.
        ws.Cell(12, 1).Value = "Loại ngân sách";
        ws.Range(12, 1, 13, 1).Merge();
        ws.Cell(12, 2).Value = "Mục lục ngân sách";
        ws.Range(12, 2, 12, 6).Merge();
        ws.Cell(13, 2).Value = "Khoản";
        ws.Cell(13, 3).Value = "Mục";
        ws.Cell(13, 4).Value = "Tiểu mục";
        ws.Cell(13, 5).Value = "Tiết mục";
        ws.Cell(13, 6).Value = "Ngành";
        ws.Cell(12, 7).Value = "NỘI DUNG";
        ws.Range(12, 7, 13, 7).Merge();
        ws.Cell(12, 8).Value = "SỐ TIỀN";
        ws.Range(12, 8, 13, 8).Merge();
        ws.Range(12, 1, 13, 8).Style.Font.Bold = true;
        ws.Range(12, 1, 13, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var letters = new[] { "A", "B", "C", "D", "E", "F", "G", "1" };
        for (var c = 0; c < letters.Length; c++)
        {
            ws.Cell(14, c + 1).Value = letters[c];
            ws.Cell(14, c + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Khối Mục lục ngân sách nằm trên dòng nội dung đầu tiên (dòng 15), như Mẫu.
        ws.Cell(15, 1).Value = form.LoaiNganSach ?? "";
        ws.Cell(15, 2).Value = form.Khoan ?? "";
        ws.Cell(15, 3).Value = form.Muc ?? "";
        ws.Cell(15, 4).Value = form.TieuMuc ?? "";
        ws.Cell(15, 5).Value = form.TietMuc ?? "";
        ws.Cell(15, 6).Value = form.Nganh ?? "";

        var row = 15;
        foreach (var line in form.Lines)
        {
            // Thụt đầu dòng bằng Indent của Excel, KHÔNG chèn dấu cách vào chuỗi —
            // để nội dung ô còn so khớp được và người dùng copy ra không dính khoảng trắng.
            ws.Cell(row, 7).Value = line.Content;
            ws.Cell(row, 7).Style.Alignment.Indent = line.Level;
            SetMoney(ws.Cell(row, 8), line.Amount);
            if (line.Level == 0) ws.Row(row).Style.Font.Bold = true;
            row++;
        }

        ws.Cell(row, 7).Value = "Cộng";
        ws.Cell(row, 7).Style.Font.Bold = true;
        SetMoney(ws.Cell(row, 8), form.Total);
        ws.Cell(row, 8).Style.Font.Bold = true;

        ws.Range(12, 1, row, 8).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(12, 1, row, 8).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        row++;
        ws.Cell(row, 1).Value = $"Bằng chữ: {form.TotalInWords}";
        ws.Range(row, 1, row, 8).Merge();

        ws.Columns(1, 6).Width = 9;
        ws.Column(7).Width = 52;
        ws.Column(8).Width = 18;
        wb.SaveAs(targetPath);
    }

    /// Ô thủ tục người dùng bỏ trống thì in dấu chấm cho họ điền tay như Mẫu.
    internal static string Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "…………" : value!;

    internal static void SetMoney(IXLCell cell, decimal value)
    {
        cell.Value = value;
        cell.Style.NumberFormat.Format = MoneyFormat;
    }

    internal static void ApplyPageDefaults(IXLWorksheet ws, XLPageOrientation orientation)
    {
        ws.Style.Font.FontName = FontName;
        ws.Style.Font.FontSize = FontSize;
        ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
        ws.PageSetup.PageOrientation = orientation;
        ws.PageSetup.Margins.Left = 0.5;
        ws.PageSetup.Margins.Right = 0.5;
    }
}
```

- [ ] **Step 5: Chạy test cho chắc là xanh**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter FullyQualifiedName~FormExcelExporterThongTriTests`
Expected: PASS — cả 5 ca.

- [ ] **Step 6: Commit**

```bash
git add src/ConstructionFinance.Application/Services/Interfaces/IFormExcelExporter.cs \
        src/ConstructionFinance.Application/Forms/FormExcelExporter.cs \
        tests/ConstructionFinance.Application.Tests/Forms/FormExcelExporterThongTriTests.cs
git commit -m "feat(app): xuất Excel biểu mẫu Thông tri (ClosedXML, khổ A4)"
```

---

### Task 8: Xuất Excel — sheet KS chi và GĐNTT

**Files:**
- Create: `src/ConstructionFinance.Application/Forms/FormExcelExporter.KsChiGdntt.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Forms/FormExcelExporterKsChiGdnttTests.cs`

**Interfaces:**
- Consumes: `FormExcelExporter` (partial class, Task 7) và các helper `internal` của nó: `MoneyFormat`, `Blank`, `SetMoney`, `ApplyPageDefaults`.
- Produces: hai phương thức còn lại của `IFormExcelExporter`: `ExportKsChiAsync`, `ExportGdnttAsync`.

- [ ] **Step 1: Viết test thất bại**

Tạo `tests/ConstructionFinance.Application.Tests/Forms/FormExcelExporterKsChiGdnttTests.cs`:

```csharp
using ClosedXML.Excel;
using ConstructionFinance.Application.Forms;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Forms;

public class FormExcelExporterKsChiGdnttTests
{
    private static string TempPath(string prefix) =>
        Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}.xlsx");

    private static KsChiForm SampleKsChi() => new(
        NgayNhanGiayDeNghi: "05/03/2026",
        MucTieuMucLines: new[] { "Mục 9300, tiểu mục 9301", "", "", "" },
        Accepted: new KsChiAmounts(240_000_000m, 210_000_000m, 30_000_000m),
        Recovered: new KsChiAmounts(5_000_000m, 5_000_000m, 0m),
        ThuHoiCacNamTruoc: 1_000_000m,
        ThuHoiNamNay: 4_000_000m,
        ThueGTGT: 2_000_000m,
        PaidToBeneficiary: new KsChiAmounts(235_000_000m, 205_000_000m, 30_000_000m),
        PaidInWords: "Hai trăm ba mươi lăm triệu đồng.",
        SoTuChoi: 500_000m,
        LyDoTuChoi: "thiếu hồ sơ",
        GhiChu: "Tạm ứng lần 1");

    private static GdnttForm SampleGdntt() => new(
        DonViCapTren: "Sở Tài chính",
        DonViCoSo: "Ban QLDA số 1",
        So: "07/GĐN",
        NgayThangNam: "05/03/2026",
        KinhGui: "KBNN Hải Phòng",
        TenDuAnCongTrinh: "Dự án A",
        ChuDauTu: "Ban QLDA số 1",
        CanCuQuyetDinh: "QĐ 91/TTg",
        CanCuHopDong: "HĐ 01/2024",
        CanCuGiayDeNghi: "GĐN 03",
        ThuocKeHoach: "Kế hoạch 2026",
        PackageName: "Gói A",
        ContractorName: "Cty XL A",
        BankAccount: "123456789",
        TaiNganHang: "Vietcombank Hải Phòng",
        UnrecoveredAdvanceBefore: 6_000_000m,
        ContractTotal: 52_000_000_000m,
        CumulativeBefore: 210_000_000m,
        RequestedThisPeriod: 30_000_000m,
        RequestedInWords: "Ba mươi triệu đồng.",
        RecoveredThisPeriod: 0m,
        ChuyenTienBaoHanh: 2_000_000m,
        ChuyenTienTamGiu: 1_000_000m,
        PaidToContractor: 27_000_000m,
        NguonVon: "Quỹ dự trữ ngoại hối");

    [Fact]
    public async Task ExportKsChiAsync_writes_the_three_fund_columns()
    {
        var path = TempPath("ks");
        try
        {
            await new FormExcelExporter().ExportKsChiAsync(SampleKsChi(), path);

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("KS chi");

            ws.Cell(1, 1).GetString().ShouldBe("PHẦN GHI CỦA CƠ QUAN KIỂM SOÁT, THANH TOÁN");
            ws.Cell(3, 4).GetString().ShouldContain("05/03/2026");
            ws.Cell(6, 3).GetString().ShouldBe("Tổng số");
            ws.Cell(6, 4).GetString().ShouldBe("Vốn trong nước");
            ws.Cell(6, 5).GetString().ShouldBe("Vốn nước ngoài");

            ws.Cell(7, 1).GetString().ShouldBe("Số vốn chấp nhận");
            ws.Cell(7, 3).GetValue<decimal>().ShouldBe(240_000_000m);
            ws.Cell(7, 4).GetValue<decimal>().ShouldBe(210_000_000m);
            ws.Cell(7, 5).GetValue<decimal>().ShouldBe(30_000_000m);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ExportKsChiAsync_writes_recovery_tax_and_beneficiary_rows()
    {
        var path = TempPath("ks");
        try
        {
            await new FormExcelExporter().ExportKsChiAsync(SampleKsChi(), path);

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("KS chi");

            ws.Cell(8, 1).GetString().ShouldContain("Mục 9300, tiểu mục 9301");
            ws.Cell(13, 3).GetValue<decimal>().ShouldBe(5_000_000m);      // Số thu hồi tạm ứng
            ws.Cell(14, 3).GetValue<decimal>().ShouldBe(1_000_000m);      // Các năm trước
            ws.Cell(15, 3).GetValue<decimal>().ShouldBe(4_000_000m);      // Năm nay
            ws.Cell(16, 3).GetValue<decimal>().ShouldBe(2_000_000m);      // Thuế GTGT
            ws.Cell(17, 1).GetString().ShouldContain("Số trả đơn vị thụ hưởng");
            ws.Cell(17, 3).GetValue<decimal>().ShouldBe(235_000_000m);
            ws.Cell(18, 1).GetString().ShouldBe("Hai trăm ba mươi lăm triệu đồng.");
            ws.Cell(19, 3).GetValue<decimal>().ShouldBe(500_000m);        // Số từ chối
            ws.Cell(20, 1).GetString().ShouldContain("thiếu hồ sơ");
            ws.Cell(22, 2).GetString().ShouldBe("Tạm ứng lần 1");         // Ghi chú
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ExportGdnttAsync_writes_the_header_and_basis_lines()
    {
        var path = TempPath("gd");
        try
        {
            await new FormExcelExporter().ExportGdnttAsync(SampleGdntt(), path);

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("GĐNTT");

            ws.Cell(1, 1).GetString().ShouldBe("Sở Tài chính");
            ws.Cell(1, 4).GetString().ShouldBe("CỘNG HOÀ XÃ HỘI CHỦ NGHĨA VIỆT NAM");
            ws.Cell(2, 4).GetString().ShouldBe("Độc lập - Tự do - Hạnh phúc");
            ws.Cell(3, 1).GetString().ShouldContain("07/GĐN");
            ws.Cell(4, 1).GetString().ShouldBe("GIẤY ĐỀ NGHỊ THANH TOÁN VỐN ĐẦU TƯ");
            ws.Cell(6, 1).GetString().ShouldContain("KBNN Hải Phòng");
            ws.Cell(10, 1).GetString().ShouldContain("QĐ 91/TTg");
            ws.Cell(11, 1).GetString().ShouldContain("HĐ 01/2024");
            ws.Cell(15, 1).GetString().ShouldContain("Quỹ dự trữ ngoại hối");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ExportGdnttAsync_writes_the_amount_table_and_beneficiary_block()
    {
        var path = TempPath("gd");
        try
        {
            await new FormExcelExporter().ExportGdnttAsync(SampleGdntt(), path);

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("GĐNTT");

            ws.Cell(13, 4).GetValue<decimal>().ShouldBe(6_000_000m);       // Số dư tạm ứng
            ws.Cell(18, 1).GetString().ShouldBe("Gói A");
            ws.Cell(18, 4).GetValue<decimal>().ShouldBe(52_000_000_000m);  // Giá trị HĐ + PLHĐ
            ws.Cell(18, 6).GetValue<decimal>().ShouldBe(210_000_000m);     // Lũy kế kỳ trước
            ws.Cell(18, 8).GetValue<decimal>().ShouldBe(30_000_000m);      // Kỳ này
            ws.Cell(19, 1).GetString().ShouldBe("CỘNG");
            ws.Cell(20, 8).GetValue<decimal>().ShouldBe(30_000_000m);
            ws.Cell(21, 2).GetString().ShouldBe("Ba mươi triệu đồng.");
            ws.Cell(23, 8).GetValue<decimal>().ShouldBe(0m);               // thu hồi tạm ứng
            ws.Cell(24, 8).GetValue<decimal>().ShouldBe(2_000_000m);       // bảo hành
            ws.Cell(25, 8).GetValue<decimal>().ShouldBe(1_000_000m);       // tạm giữ
            ws.Cell(26, 8).GetValue<decimal>().ShouldBe(27_000_000m);      // trả nhà thầu
            ws.Cell(27, 3).GetString().ShouldBe("Cty XL A");
            ws.Cell(28, 4).GetString().ShouldBe("123456789");
            ws.Cell(29, 2).GetString().ShouldBe("Vietcombank Hải Phòng");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ExportGdnttAsync_prints_landscape_a4()
    {
        var path = TempPath("gd");
        try
        {
            await new FormExcelExporter().ExportGdnttAsync(SampleGdntt(), path);

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheet("GĐNTT");
            ws.PageSetup.PageOrientation.ShouldBe(XLPageOrientation.Landscape);
        }
        finally { File.Delete(path); }
    }
}
```

- [ ] **Step 2: Chạy test cho chắc là đỏ**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter FullyQualifiedName~FormExcelExporterKsChiGdnttTests`
Expected: FAIL — `ExportKsChiAsync` / `ExportGdnttAsync` chưa có bản cài đặt.

- [ ] **Step 3: Viết hai sheet còn lại**

Tạo `src/ConstructionFinance.Application/Forms/FormExcelExporter.KsChiGdntt.cs`:

```csharp
using ClosedXML.Excel;

namespace ConstructionFinance.Application.Forms;

public partial class FormExcelExporter
{
    public Task ExportKsChiAsync(KsChiForm form, string targetPath, CancellationToken ct = default) =>
        Task.Run(() => WriteKsChi(form, targetPath), ct);

    public Task ExportGdnttAsync(GdnttForm form, string targetPath, CancellationToken ct = default) =>
        Task.Run(() => WriteGdntt(form, targetPath), ct);

    private static void WriteKsChi(KsChiForm form, string targetPath)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("KS chi");
        ApplyPageDefaults(ws, XLPageOrientation.Portrait);

        ws.Cell(1, 1).Value = "PHẦN GHI CỦA CƠ QUAN KIỂM SOÁT, THANH TOÁN";
        ws.Range(1, 1, 1, 5).Merge();
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Cell(3, 1).Value = "Ngày nhận giấy đề nghị thanh toán vốn đầu tư:";
        ws.Cell(3, 4).Value = $"Ngày {Blank(form.NgayNhanGiayDeNghi)}";
        ws.Cell(5, 5).Value = "Đơn vị tính: đồng";

        ws.Cell(6, 1).Value = "Nội dung";
        ws.Range(6, 1, 6, 2).Merge();
        ws.Cell(6, 3).Value = "Tổng số";
        ws.Cell(6, 4).Value = "Vốn trong nước";
        ws.Cell(6, 5).Value = "Vốn nước ngoài";
        ws.Range(6, 1, 6, 5).Style.Font.Bold = true;
        ws.Range(6, 1, 6, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        WriteAmountRow(ws, 7, "Số vốn chấp nhận", form.Accepted);

        // Bốn dòng "+ Mục ..., tiểu mục ..." — dòng nào bỏ trống thì in mẫu chấm.
        for (var i = 0; i < 4; i++)
        {
            var text = form.MucTieuMucLines[i];
            ws.Cell(8 + i, 1).Value = string.IsNullOrWhiteSpace(text)
                ? " + Mục ........, tiểu mục ........"
                : $" + {text}";
        }

        ws.Cell(12, 1).Value = "Trong đó:";
        WriteAmountRow(ws, 13, " + Số thu hồi tạm ứng", form.Recovered);
        ws.Cell(14, 1).Value = "  Các năm trước";
        SetMoney(ws.Cell(14, 3), form.ThuHoiCacNamTruoc);
        ws.Cell(15, 1).Value = "  Năm nay";
        SetMoney(ws.Cell(15, 3), form.ThuHoiNamNay);
        ws.Cell(16, 1).Value = " + Thuế giá trị gia tăng";
        SetMoney(ws.Cell(16, 3), form.ThueGTGT);
        WriteAmountRow(ws, 17, " + Số trả đơn vị thụ hưởng", form.PaidToBeneficiary);
        ws.Row(17).Style.Font.Bold = true;

        ws.Cell(18, 1).Value = form.PaidInWords;
        ws.Range(18, 1, 18, 5).Merge();

        ws.Cell(19, 1).Value = "Số từ chối:";
        SetMoney(ws.Cell(19, 3), form.SoTuChoi);
        ws.Cell(19, 4).Value = "đồng";
        ws.Cell(20, 1).Value = $"Lý do từ chối: {form.LyDoTuChoi ?? ""}";
        ws.Range(20, 1, 20, 5).Merge();

        ws.Cell(22, 1).Value = "Ghi chú:";
        ws.Cell(22, 2).Value = form.GhiChu;
        ws.Range(22, 2, 22, 5).Merge();

        ws.Range(6, 1, 17, 5).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(6, 1, 17, 5).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        ws.Column(1).Width = 34;
        ws.Column(2).Width = 14;
        ws.Columns(3, 5).Width = 18;
        wb.SaveAs(targetPath);
    }

    private static void WriteAmountRow(IXLWorksheet ws, int row, string label, KsChiAmounts amounts)
    {
        ws.Cell(row, 1).Value = label;
        ws.Range(row, 1, row, 2).Merge();
        SetMoney(ws.Cell(row, 3), amounts.Total);
        SetMoney(ws.Cell(row, 4), amounts.NSNN);
        SetMoney(ws.Cell(row, 5), amounts.NgoaiHoi);
    }

    private static void WriteGdntt(GdnttForm form, string targetPath)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("GĐNTT");
        ApplyPageDefaults(ws, XLPageOrientation.Landscape);

        ws.Cell(1, 1).Value = form.DonViCapTren ?? "";
        ws.Cell(2, 1).Value = form.DonViCoSo ?? "";
        ws.Cell(3, 1).Value = $"Số: {Blank(form.So)}";
        ws.Cell(1, 4).Value = "CỘNG HOÀ XÃ HỘI CHỦ NGHĨA VIỆT NAM";
        ws.Cell(2, 4).Value = "Độc lập - Tự do - Hạnh phúc";
        ws.Cell(3, 4).Value = $"Ngày {Blank(form.NgayThangNam)}";
        foreach (var r in new[] { 1, 2, 3 })
        {
            ws.Range(r, 4, r, 9).Merge();
            ws.Cell(r, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        ws.Cell(1, 4).Style.Font.Bold = true;
        ws.Cell(2, 4).Style.Font.Bold = true;

        ws.Cell(4, 1).Value = "GIẤY ĐỀ NGHỊ THANH TOÁN VỐN ĐẦU TƯ";
        ws.Range(4, 1, 4, 9).Merge();
        ws.Cell(4, 1).Style.Font.Bold = true;
        ws.Cell(4, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Cell(6, 1).Value = $"Kính gửi: {form.KinhGui ?? ""}";
        ws.Cell(8, 1).Value = $" - Tên dự án công trình: {form.TenDuAnCongTrinh ?? ""}";
        ws.Cell(9, 1).Value = $" - Chủ đầu tư/Ban QLDA: {form.ChuDauTu ?? ""}";
        ws.Cell(10, 1).Value = $" - Căn cứ Quyết định số: {form.CanCuQuyetDinh ?? ""}";
        ws.Cell(11, 1).Value = $" - Căn cứ Hợp đồng số: {form.CanCuHopDong ?? ""}";
        ws.Cell(12, 1).Value = $" - Căn cứ giấy đề nghị thanh toán vốn đầu tư số: {form.CanCuGiayDeNghi ?? ""}";
        ws.Cell(13, 1).Value = " - Số dư tạm ứng của hạng mục đề nghị thanh toán:";
        SetMoney(ws.Cell(13, 4), form.UnrecoveredAdvanceBefore);
        ws.Cell(14, 1).Value = " - Số tiền đề nghị (tạm ứng, thanh toán KLHT) đến kỳ này:";
        ws.Cell(15, 1).Value = $" - Thuộc nguồn vốn: {form.NguonVon}";
        ws.Cell(16, 1).Value = $" - Thuộc kế hoạch: {form.ThuocKeHoach ?? ""}";
        foreach (var r in Enumerable.Range(6, 11)) ws.Range(r, 1, r, 3).Merge();

        ws.Cell(17, 1).Value = "NỘI DUNG";
        ws.Range(17, 1, 17, 3).Merge();
        ws.Cell(17, 4).Value = "Dự toán duyệt hoặc giá trị trúng thầu hoặc giá trị HĐ";
        ws.Range(17, 4, 17, 5).Merge();
        ws.Cell(17, 6).Value = "Lũy kế số vốn đã T/Toán từ K/công đến cuối kỳ trước (Gồm cả tạm ứng)";
        ws.Range(17, 6, 17, 7).Merge();
        ws.Cell(17, 8).Value = "Số đề nghị tạm ứng, thanh toán KLHT kỳ này (Gồm cả tạm ứng)";
        ws.Range(17, 8, 17, 9).Merge();
        ws.Row(17).Style.Font.Bold = true;
        ws.Row(17).Style.Alignment.WrapText = true;
        ws.Range(17, 1, 17, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Cell(18, 1).Value = form.PackageName;
        ws.Range(18, 1, 18, 3).Merge();
        SetMoney(ws.Cell(18, 4), form.ContractTotal);
        ws.Range(18, 4, 18, 5).Merge();
        SetMoney(ws.Cell(18, 6), form.CumulativeBefore);
        ws.Range(18, 6, 18, 7).Merge();
        SetMoney(ws.Cell(18, 8), form.RequestedThisPeriod);
        ws.Range(18, 8, 18, 9).Merge();

        ws.Cell(19, 1).Value = "CỘNG";
        ws.Range(19, 1, 19, 3).Merge();
        SetMoney(ws.Cell(19, 4), form.ContractTotal);
        SetMoney(ws.Cell(19, 6), form.CumulativeBefore);
        SetMoney(ws.Cell(19, 8), form.RequestedThisPeriod);
        ws.Row(19).Style.Font.Bold = true;

        ws.Range(17, 1, 19, 9).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(17, 1, 19, 9).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        ws.Cell(20, 1).Value = " - Tổng số tiền đề nghị tạm ứng, thanh toán bằng số:";
        SetMoney(ws.Cell(20, 8), form.RequestedThisPeriod);
        ws.Cell(21, 1).Value = " - Bằng chữ:";
        ws.Cell(21, 2).Value = form.RequestedInWords;
        ws.Range(21, 2, 21, 9).Merge();
        ws.Cell(22, 1).Value = "Trong đó:";
        ws.Cell(23, 2).Value = " - Thanh toán để thu hồi tạm ứng:";
        SetMoney(ws.Cell(23, 8), form.RecoveredThisPeriod);
        ws.Cell(24, 2).Value = " - Chuyển tiền bảo hành:";
        SetMoney(ws.Cell(24, 8), form.ChuyenTienBaoHanh);
        ws.Cell(25, 2).Value = " - Chuyển tiền tạm giữ chờ quyết toán:";
        SetMoney(ws.Cell(25, 8), form.ChuyenTienTamGiu);
        ws.Cell(26, 2).Value = " - Số trả nhà thầu:";
        SetMoney(ws.Cell(26, 8), form.PaidToContractor);
        ws.Row(26).Style.Font.Bold = true;

        ws.Cell(27, 1).Value = " - Tên đơn vị thụ hưởng:";
        ws.Cell(27, 3).Value = form.ContractorName ?? "";
        ws.Cell(28, 1).Value = " - Tài khoản:";
        ws.Cell(28, 4).Value = form.BankAccount ?? "";
        ws.Cell(29, 1).Value = " - Tại:";
        ws.Cell(29, 2).Value = form.TaiNganHang ?? "";
        ws.Range(29, 2, 29, 9).Merge();

        ws.Cell(31, 1).Value = "Trưởng phòng Tài chính";
        ws.Cell(31, 5).Value = "Chủ đầu tư";
        ws.Range(31, 1, 31, 3).Merge();
        ws.Range(31, 5, 31, 9).Merge();
        ws.Range(31, 1, 31, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Column(1).Width = 26;
        ws.Columns(2, 9).Width = 16;
        wb.SaveAs(targetPath);
    }
}
```

- [ ] **Step 4: Chạy test cho chắc là xanh**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter FullyQualifiedName~FormExcelExporterKsChiGdnttTests`
Expected: PASS — cả 5 ca.

- [ ] **Step 5: Chạy cả tầng Application cho chắc không vỡ gì**

Run: `dotnet test tests/ConstructionFinance.Application.Tests`
Expected: PASS toàn bộ.

- [ ] **Step 6: Commit**

```bash
git add src/ConstructionFinance.Application/Forms/FormExcelExporter.KsChiGdntt.cs \
        tests/ConstructionFinance.Application.Tests/Forms/FormExcelExporterKsChiGdnttTests.cs
git commit -m "feat(app): xuất Excel biểu mẫu KS chi và GĐNTT"
```

---

### Task 9: Đơn vị cấp trên / Đơn vị cơ sở trong Cài đặt

Hai tên này in trên đầu Thông tri và GĐNTT, giống nhau ở mọi dự án → lưu ở Cài đặt, gõ một lần.

**Files:**
- Modify: `src/ConstructionFinance.Application/DTOs/CompanyInfoDto.cs`
- Modify: `src/ConstructionFinance.Application/Services/Implementations/CompanyInfoService.cs`
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Settings/CompanyInfoViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/Views/Settings/CompanyInfoView.axaml:39-42` (chèn hai Grid trước ô "Nơi nhận báo cáo mặc định")
- Test: `tests/ConstructionFinance.Application.Tests/CompanyInfoServiceTests.cs` (thêm một ca)

**Interfaces:**
- Consumes: `IAppSettingService`, `ICompanyInfoService` (đã có).
- Produces: `CompanyInfoDto.DonViCapTren` / `.DonViCoSo` (hai tham số **tùy chọn cuối** của record — mọi lời gọi 8 tham số đang có vẫn biên dịch); `CompanyInfoViewModel.DonViCapTren` / `.DonViCoSo`. Task 12 đọc hai giá trị này để dựng `FormOrgInfo`.

- [ ] **Step 1: Viết test thất bại**

Thêm vào `tests/ConstructionFinance.Application.Tests/CompanyInfoServiceTests.cs` (giữ nguyên các ca cũ):

```csharp
    [Fact]
    public async Task SaveAsync_then_GetAsync_round_trips_the_two_form_org_names()
    {
        using var h = new ServiceTestHarness();
        var svc = CreateService(h.Context);

        await svc.SaveAsync(new CompanyInfoDto(
            "Sở X", null, null, null, null, null, null, null,
            DonViCapTren: "Sở Tài chính", DonViCoSo: "Ban QLDA số 1"));

        var stored = await svc.GetAsync();

        stored.DonViCapTren.ShouldBe("Sở Tài chính");
        stored.DonViCoSo.ShouldBe("Ban QLDA số 1");
    }
```

(Dùng đúng helper `CreateService(h.Context)` mà các ca cũ trong tệp đang dùng — không cần thêm `using` nào.)

- [ ] **Step 2: Chạy test cho chắc là đỏ**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter FullyQualifiedName~CompanyInfoServiceTests`
Expected: FAIL — `CompanyInfoDto` chưa có `DonViCapTren` / `DonViCoSo`.

- [ ] **Step 3: Mở rộng DTO và service**

Sửa `src/ConstructionFinance.Application/DTOs/CompanyInfoDto.cs`:

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
    string? DefaultRecipients,
    // Hai tên in trên đầu Thông tri và GĐNTT. Tham số tùy chọn ở cuối để mọi
    // lời gọi 8 tham số đang có vẫn biên dịch nguyên vẹn.
    string? DonViCapTren = null,
    string? DonViCoSo = null);
```

Trong `CompanyInfoService.cs`, thêm hai khóa cạnh các khóa đang có:

```csharp
    private const string KeyDonViCapTren = "don_vi_cap_tren";
    private const string KeyDonViCoSo = "don_vi_co_so";
```

thêm hai dòng vào `GetAsync` (sau `DefaultRecipients`):

```csharp
        DonViCapTren: await _settings.GetAsync(KeyDonViCapTren, ct),
        DonViCoSo: await _settings.GetAsync(KeyDonViCoSo, ct));
```

và hai dòng vào `SaveAsync`:

```csharp
        await _settings.SetAsync(KeyDonViCapTren, info.DonViCapTren, ct: ct);
        await _settings.SetAsync(KeyDonViCoSo, info.DonViCoSo, ct: ct);
```

- [ ] **Step 4: Chạy test cho chắc là xanh**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter FullyQualifiedName~CompanyInfoServiceTests`
Expected: PASS — cả ca mới lẫn các ca cũ.

- [ ] **Step 5: Đưa hai ô lên màn Cài đặt**

Trong `CompanyInfoViewModel.cs`, thêm hai thuộc tính cạnh các `[ObservableProperty]` đang có:

```csharp
    [ObservableProperty] private string? _donViCapTren;
    [ObservableProperty] private string? _donViCoSo;
```

trong `LoadAsync` thêm:

```csharp
            DonViCapTren = info.DonViCapTren;
            DonViCoSo = info.DonViCoSo;
```

và sửa lời gọi trong `SaveAsync` thành:

```csharp
            await _service.SaveAsync(new CompanyInfoDto(
                CompanyName, CompanyAddress, CompanyPhone, CompanyEmail,
                LogoPath, DefaultSignerName, DefaultSignerTitle, DefaultRecipients,
                DonViCapTren, DonViCoSo));
```

Trong `CompanyInfoView.axaml`, chèn ngay TRƯỚC khối `Grid` của "Nơi nhận báo cáo mặc định" (dòng 39):

```xml
                <TextBlock Classes="sectionTitle" Text="Tên đơn vị in trên biểu mẫu"/>
                <Grid ColumnDefinitions="220,*">
                    <TextBlock Grid.Column="0" Classes="formLabel" Text="Đơn vị cấp trên"/>
                    <TextBox Grid.Column="1" Text="{Binding DonViCapTren}"/>
                </Grid>
                <Grid ColumnDefinitions="220,*">
                    <TextBlock Grid.Column="0" Classes="formLabel" Text="Đơn vị cơ sở"/>
                    <TextBox Grid.Column="1" Text="{Binding DonViCoSo}"/>
                </Grid>
```

- [ ] **Step 6: Build và chạy test Desktop**

Run: `dotnet build ConstructionFinance.sln && dotnet test tests/ConstructionFinance.Desktop.Tests --filter FullyQualifiedName~CompanyInfoViewModelTests`
Expected: build sạch, test PASS (các ca cũ dựng `CompanyInfoDto` 8 tham số vẫn chạy được nhờ tham số tùy chọn).

- [ ] **Step 7: Commit**

```bash
git add src/ConstructionFinance.Application/DTOs/CompanyInfoDto.cs \
        src/ConstructionFinance.Application/Services/Implementations/CompanyInfoService.cs \
        src/ConstructionFinance.Desktop/ViewModels/Settings/CompanyInfoViewModel.cs \
        src/ConstructionFinance.Desktop/Views/Settings/CompanyInfoView.axaml \
        tests/ConstructionFinance.Application.Tests/CompanyInfoServiceTests.cs
git commit -m "feat(settings): tên Đơn vị cấp trên/cơ sở dùng cho biểu mẫu"
```

---

### Task 10: Panel chọn đợt (picker)

Panel dùng chung cho cả ba tab form: liệt kê đợt đã lọc theo Nguồn của form, cho tích chọn, lọc nhanh theo tháng/năm.

**Files:**
- Create: `src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/FormDotItemViewModel.cs`
- Create: `src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/FormDotPickerViewModel.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Finance/Forms/FormDotPickerViewModelTests.cs`

**Interfaces:**
- Consumes: `FormDotRow`, `FormDotKey` (Task 3); `LedgerRowViewModel`, `MoneyText` (đã có).
- Produces:
  - `FormDotItemViewModel` với `Data`, `Token`, `IsSelected`, và các chuỗi hiển thị `DateText`, `ViTriName`, `PackageName`, `ContentText`, `FundText`, `AdvanceText`, `KlhtText`, `RecoveredText`, `DisbursedText`.
  - `FormDotPickerViewModel(Fund? fundFilter)` với `Items`, `MonthFilter`, `YearFilter`, `SelectedCount`, `SelectionSummary`, `SelectedKeys`, `SelectAllCommand`, `ClearSelectionCommand`, sự kiện `SelectionChanged`, và `void Refresh(IReadOnlyList<FormDotRow> rows, IReadOnlyList<LedgerRowViewModel> sources)`.

- [ ] **Step 1: Viết test thất bại**

Tạo `tests/ConstructionFinance.Desktop.Tests/Finance/Forms/FormDotPickerViewModelTests.cs`:

```csharp
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Desktop.ViewModels.Finance.Forms;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance.Forms;

public class FormDotPickerViewModelTests
{
    private static FormDotRow Row(int dotIndex, Fund fund, DateOnly date, decimal advance = 10_000_000m) =>
        new(new FormDotKey(0, 0, 0, dotIndex), "Vị trí 1", "CHI PHÍ XÂY DỰNG", "Gói A",
            "Cty XL A", "123456789", 50_000_000_000m, 0m, date, $"Đợt {dotIndex}", "CT01",
            fund, advance, 0m, 0m, advance);

    private static readonly IReadOnlyList<LedgerRowViewModel> NoSources = Array.Empty<LedgerRowViewModel>();

    [Fact]
    public void Refresh_lists_only_dots_of_the_requested_fund()
    {
        var vm = new FormDotPickerViewModel(Fund.NSNN);

        vm.Refresh(new[]
        {
            Row(0, Fund.NSNN, new DateOnly(2026, 3, 1)),
            Row(1, Fund.NgoaiHoi, new DateOnly(2026, 3, 2))
        }, NoSources);

        vm.Items.Count.ShouldBe(1);
        vm.Items[0].FundText.ShouldBe("Dự phòng NSNN");
    }

    [Fact]
    public void Refresh_with_no_fund_filter_lists_both_funds()
    {
        var vm = new FormDotPickerViewModel(null);

        vm.Refresh(new[]
        {
            Row(0, Fund.NSNN, new DateOnly(2026, 3, 1)),
            Row(1, Fund.NgoaiHoi, new DateOnly(2026, 3, 2))
        }, NoSources);

        vm.Items.Count.ShouldBe(2);
        vm.Items[1].FundText.ShouldBe("Quỹ dự trữ ngoại hối");
    }

    [Fact]
    public void SelectAll_ticks_every_visible_row_and_reports_the_count()
    {
        var vm = new FormDotPickerViewModel(Fund.NSNN);
        vm.Refresh(new[]
        {
            Row(0, Fund.NSNN, new DateOnly(2026, 3, 1)),
            Row(1, Fund.NSNN, new DateOnly(2026, 4, 1))
        }, NoSources);

        vm.SelectAllCommand.Execute(null);

        vm.SelectedCount.ShouldBe(2);
        vm.SelectionSummary.ShouldBe("Đang chọn 2 đợt");
        vm.SelectedKeys.Count.ShouldBe(2);
    }

    [Fact]
    public void ClearSelection_unticks_everything()
    {
        var vm = new FormDotPickerViewModel(Fund.NSNN);
        vm.Refresh(new[] { Row(0, Fund.NSNN, new DateOnly(2026, 3, 1)) }, NoSources);
        vm.SelectAllCommand.Execute(null);

        vm.ClearSelectionCommand.Execute(null);

        vm.SelectedCount.ShouldBe(0);
        vm.Items[0].IsSelected.ShouldBeFalse();
    }

    [Fact]
    public void Month_filter_narrows_the_visible_rows_only()
    {
        var vm = new FormDotPickerViewModel(Fund.NSNN);
        vm.Refresh(new[]
        {
            Row(0, Fund.NSNN, new DateOnly(2026, 3, 1)),
            Row(1, Fund.NSNN, new DateOnly(2026, 4, 1))
        }, NoSources);
        vm.SelectAllCommand.Execute(null);      // chọn cả hai khi chưa lọc

        vm.MonthFilter = "3";

        vm.Items.Count.ShouldBe(1);
        vm.SelectedCount.ShouldBe(2);           // đợt bị lọc ẩn VẪN nằm trong form
        vm.SelectedKeys.Count.ShouldBe(2);
    }

    [Fact]
    public void Year_filter_narrows_the_visible_rows()
    {
        var vm = new FormDotPickerViewModel(Fund.NSNN);
        vm.Refresh(new[]
        {
            Row(0, Fund.NSNN, new DateOnly(2025, 3, 1)),
            Row(1, Fund.NSNN, new DateOnly(2026, 3, 1))
        }, NoSources);

        vm.YearFilter = "2026";

        vm.Items.Count.ShouldBe(1);
        vm.Items[0].DateText.ShouldBe("01/03/2026");
    }

    [Fact]
    public void SelectAll_only_ticks_what_the_filter_shows()
    {
        var vm = new FormDotPickerViewModel(Fund.NSNN);
        vm.Refresh(new[]
        {
            Row(0, Fund.NSNN, new DateOnly(2026, 3, 1)),
            Row(1, Fund.NSNN, new DateOnly(2026, 4, 1))
        }, NoSources);
        vm.MonthFilter = "4";

        vm.SelectAllCommand.Execute(null);

        vm.SelectedCount.ShouldBe(1);
        vm.SelectedKeys.ShouldContain(new FormDotKey(0, 0, 0, 1));
    }

    [Fact]
    public void Selection_survives_a_refresh_that_inserts_a_dot_above_it()
    {
        var vm = new FormDotPickerViewModel(Fund.NSNN);
        var first = new LedgerRowViewModel(RowKind.Entry);
        var second = new LedgerRowViewModel(RowKind.Entry);

        vm.Refresh(new[] { Row(0, Fund.NSNN, new DateOnly(2026, 3, 1)) }, new[] { second });
        vm.Items[0].IsSelected = true;

        // Người dùng chèn một đợt mới lên trên: đợt cũ tụt xuống chỉ số 1.
        vm.Refresh(new[]
        {
            Row(0, Fund.NSNN, new DateOnly(2026, 2, 1)),
            Row(1, Fund.NSNN, new DateOnly(2026, 3, 1))
        }, new[] { first, second });

        vm.SelectedCount.ShouldBe(1);
        vm.SelectedKeys.ShouldContain(new FormDotKey(0, 0, 0, 1));   // khóa đã trượt theo
        vm.Items[1].IsSelected.ShouldBeTrue();
        vm.Items[0].IsSelected.ShouldBeFalse();
    }

    [Fact]
    public void Selection_of_a_deleted_dot_is_dropped()
    {
        var vm = new FormDotPickerViewModel(Fund.NSNN);
        var only = new LedgerRowViewModel(RowKind.Entry);
        vm.Refresh(new[] { Row(0, Fund.NSNN, new DateOnly(2026, 3, 1)) }, new[] { only });
        vm.Items[0].IsSelected = true;

        vm.Refresh(Array.Empty<FormDotRow>(), Array.Empty<LedgerRowViewModel>());

        vm.SelectedCount.ShouldBe(0);
        vm.SelectedKeys.ShouldBeEmpty();
    }

    [Fact]
    public void Ticking_a_row_raises_SelectionChanged()
    {
        var vm = new FormDotPickerViewModel(Fund.NSNN);
        vm.Refresh(new[] { Row(0, Fund.NSNN, new DateOnly(2026, 3, 1)) }, NoSources);
        var raised = 0;
        vm.SelectionChanged += (_, _) => raised++;

        vm.Items[0].IsSelected = true;

        raised.ShouldBe(1);
    }

    [Fact]
    public void Item_formats_money_and_date_for_display()
    {
        var vm = new FormDotPickerViewModel(Fund.NSNN);
        vm.Refresh(new[] { Row(0, Fund.NSNN, new DateOnly(2026, 3, 1), 10_000_000m) }, NoSources);

        var item = vm.Items[0];
        item.DateText.ShouldBe("01/03/2026");
        item.AdvanceText.ShouldBe("10.000.000");
        item.DisbursedText.ShouldBe("10.000.000");
        item.ContentText.ShouldBe("Đợt 0");
        item.PackageName.ShouldBe("Gói A");
    }
}
```

- [ ] **Step 2: Chạy test cho chắc là đỏ**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter FullyQualifiedName~FormDotPickerViewModelTests`
Expected: FAIL — `FormDotPickerViewModel` chưa tồn tại.

- [ ] **Step 3: Viết dòng của picker**

Tạo `src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/FormDotItemViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Desktop.ViewModels.Finance.Forms;

/// Một dòng trong panel chọn đợt: dữ liệu đợt đã trải phẳng + ô tích + các chuỗi
/// hiển thị (không bind thẳng decimal/DateOnly — bài học 6A).
public partial class FormDotItemViewModel : ViewModelBase
{
    public FormDotItemViewModel(FormDotRow data, object token)
    {
        Data = data;
        Token = token;
    }

    public FormDotRow Data { get; }

    /// Danh tính bền của dòng qua các lần Refresh: là chính đối tượng dòng lưới khi
    /// có (thêm/xóa đợt chỗ khác không làm trượt lựa chọn), nếu không thì là khóa.
    public object Token { get; }

    [ObservableProperty] private bool _isSelected;

    public string DateText => Data.Date.ToString("dd/MM/yyyy");
    public string ViTriName => Data.ViTriName;
    public string PackageName => Data.PackageName;
    public string ContentText => Data.Content ?? "";
    public string FundText => Data.Fund == Fund.NSNN ? "Dự phòng NSNN" : "Quỹ dự trữ ngoại hối";
    public string AdvanceText => MoneyText.Format(Data.Advance);
    public string KlhtText => MoneyText.Format(Data.PaymentKLHT);
    public string RecoveredText => MoneyText.Format(Data.RecoveredAdvance);
    public string DisbursedText => MoneyText.Format(Data.Disbursed);
}
```

- [ ] **Step 4: Viết picker**

Tạo `src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/FormDotPickerViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Desktop.ViewModels.Finance.Forms;

/// Panel chọn đợt dùng chung cho ba tab biểu mẫu. Danh sách đã lọc sẵn theo Nguồn
/// của form; ô lọc tháng/năm chỉ THU HẸP phần hiển thị — đợt đang tích mà bị ẩn vẫn
/// nằm trong form, nên SelectionSummary luôn hiện số đang chọn.
public partial class FormDotPickerViewModel : ViewModelBase
{
    private readonly Fund? _fundFilter;

    // Danh tính lựa chọn theo Token của từng dòng (xem FormDotItemViewModel.Token).
    private readonly HashSet<object> _selectedTokens = new();

    private List<FormDotItemViewModel> _allItems = new();

    public FormDotPickerViewModel(Fund? fundFilter) => _fundFilter = fundFilter;

    /// Các dòng đang hiển thị (sau bộ lọc tháng/năm).
    public ObservableCollection<FormDotItemViewModel> Items { get; } = new();

    [ObservableProperty] private string? _monthFilter;
    [ObservableProperty] private string? _yearFilter;

    public int SelectedCount => _allItems.Count(i => i.IsSelected);
    public string SelectionSummary => $"Đang chọn {SelectedCount} đợt";

    public IReadOnlySet<FormDotKey> SelectedKeys =>
        _allItems.Where(i => i.IsSelected).Select(i => i.Data.Key).ToHashSet();

    /// Bắn mỗi khi tập đợt đã chọn đổi, để tab form dựng lại bản xem trước.
    public event EventHandler? SelectionChanged;

    /// Dựng lại danh sách từ cây hiện tại. `sources` là các dòng đợt của lưới theo
    /// ĐÚNG thứ tự của `rows` (workbook truyền vào); truyền mảng rỗng thì lựa chọn
    /// bám theo khóa thay vì theo đối tượng dòng.
    public void Refresh(IReadOnlyList<FormDotRow> rows, IReadOnlyList<LedgerRowViewModel> sources)
    {
        var usable = sources.Count == rows.Count ? sources : Array.Empty<LedgerRowViewModel>();

        var items = new List<FormDotItemViewModel>();
        for (var i = 0; i < rows.Count; i++)
        {
            if (_fundFilter is not null && rows[i].Fund != _fundFilter) continue;

            object token = usable.Count == rows.Count ? usable[i] : rows[i].Key;
            var item = new FormDotItemViewModel(rows[i], token) { IsSelected = _selectedTokens.Contains(token) };
            item.PropertyChanged += OnItemPropertyChanged;
            items.Add(item);
        }

        foreach (var old in _allItems) old.PropertyChanged -= OnItemPropertyChanged;
        _allItems = items;

        // Đợt đã bị xóa khỏi sổ thì bỏ khỏi lựa chọn, đừng để nó âm thầm ở lại.
        var live = items.Select(i => i.Token).ToHashSet();
        _selectedTokens.RemoveWhere(t => !live.Contains(t));

        ApplyFilter();
        RaiseSelectionState();
    }

    partial void OnMonthFilterChanged(string? value) => ApplyFilter();
    partial void OnYearFilterChanged(string? value) => ApplyFilter();

    private void OnItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FormDotItemViewModel.IsSelected)) return;
        if (sender is not FormDotItemViewModel item) return;

        if (item.IsSelected) _selectedTokens.Add(item.Token);
        else _selectedTokens.Remove(item.Token);

        RaiseSelectionState();
    }

    private void ApplyFilter()
    {
        var month = ParseFilter(MonthFilter);
        var year = ParseFilter(YearFilter);

        Items.Clear();
        foreach (var item in _allItems)
        {
            if (month is not null && item.Data.Date.Month != month) continue;
            if (year is not null && item.Data.Date.Year != year) continue;
            Items.Add(item);
        }
    }

    private static int? ParseFilter(string? raw) =>
        int.TryParse(raw?.Trim(), out var value) ? value : null;

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var item in Items) item.IsSelected = true;
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var item in _allItems) item.IsSelected = false;
    }

    private void RaiseSelectionState()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(SelectedKeys));
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
```

- [ ] **Step 5: Chạy test cho chắc là xanh**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter FullyQualifiedName~FormDotPickerViewModelTests`
Expected: PASS — cả 11 ca.

- [ ] **Step 6: Commit**

```bash
git add src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/FormDotItemViewModel.cs \
        src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/FormDotPickerViewModel.cs \
        tests/ConstructionFinance.Desktop.Tests/Finance/Forms/FormDotPickerViewModelTests.cs
git commit -m "feat(desktop): panel chọn đợt cho biểu mẫu (lọc nguồn + tháng/năm)"
```

---

### Task 11: View-model tab Thông tri

**Files:**
- Create: `src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/FormLineRow.cs`
- Create: `src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/ThongTriFormViewModel.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Finance/Forms/ThongTriFormViewModelTests.cs`

**Interfaces:**
- Consumes: `FormDotPickerViewModel` (Task 10); `ThongTriBuilder` (Task 4); `IFormExcelExporter` (Task 7); `ThongTriDeclaration` (Task 2); `IDialogService`, `MoneyText` (đã có).
- Produces:
  - `FormLineRow` (chuỗi hiển thị: `Indent`, `Content`, `AmountText`, `IsBold`).
  - `ThongTriFormViewModel(IFormExcelExporter exporter, IDialogService dialog)` với `Picker`, `Lines`, `TotalText`, `TotalInWords`, 17 thuộc tính ô nhập tay (`SoThongTri`, `NgayGhiSo`, `TenCTMT`, `MaSo`, `VeThangNam`, `TaiKhoan1`, `DonVi1`, `SoChungTu1`, `TaiKhoan2`, `DonVi2`, `SoChungTu2`, `LoaiNganSach`, `Khoan`, `Muc`, `TieuMuc`, `TietMuc`, `Nganh`), `ExportCommand`, sự kiện `DeclarationChanged`, `void Refresh(IReadOnlyList<FormDotRow> rows, IReadOnlyList<LedgerRowViewModel> sources, ThongTriDeclaration declaration, FormOrgInfo org, string projectName)` và `ThongTriDeclaration ToDeclaration()`.

- [ ] **Step 1: Viết test thất bại**

Tạo `tests/ConstructionFinance.Desktop.Tests/Finance/Forms/ThongTriFormViewModelTests.cs`:

```csharp
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Desktop.ViewModels.Finance.Forms;
using ConstructionFinance.Domain.Finance;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance.Forms;

public class ThongTriFormViewModelTests
{
    private static FormDotRow Row(int dotIndex, decimal advance, string viTri = "Vị trí 1") =>
        new(new FormDotKey(dotIndex, 0, 0, 0), viTri, "CHI PHÍ XÂY DỰNG", "Gói A",
            "Cty XL A", "123456789", 50_000_000_000m, 0m, new DateOnly(2026, 3, 1),
            $"Tạm ứng lần {dotIndex + 1}", "CT01", Fund.NSNN, advance, 0m, 0m, advance);

    private static (ThongTriFormViewModel vm, Mock<IFormExcelExporter> exporter, FakeDialogService dialog) Create()
    {
        var exporter = new Mock<IFormExcelExporter>();
        var dialog = new FakeDialogService();
        return (new ThongTriFormViewModel(exporter.Object, dialog), exporter, dialog);
    }

    private static void Load(ThongTriFormViewModel vm, params FormDotRow[] rows) =>
        vm.Refresh(rows, Array.Empty<LedgerRowViewModel>(), new ThongTriDeclaration(),
                   new FormOrgInfo("Sở Tài chính", "Ban QLDA số 1"), "Dự án A");

    [Fact]
    public void Refresh_with_no_selection_shows_only_the_zero_project_line()
    {
        var (vm, _, _) = Create();

        Load(vm, Row(0, 10_000_000m));

        vm.Lines.Count.ShouldBe(1);
        vm.Lines[0].Content.ShouldBe("Dự án A");
        vm.TotalText.ShouldBe("");                   // MoneyText hiển thị 0 là ô trống
        vm.TotalInWords.ShouldBe("Không đồng.");
    }

    [Fact]
    public void Ticking_a_dot_rebuilds_the_preview_live()
    {
        var (vm, _, _) = Create();
        Load(vm, Row(0, 10_000_000m), Row(1, 5_000_000m, "Vị trí 2"));

        vm.Picker.Items[0].IsSelected = true;

        vm.Lines.Count.ShouldBe(3);                  // dự án + vị trí + đợt
        vm.Lines[2].Content.ShouldBe("Tạm ứng lần 1");
        vm.Lines[2].AmountText.ShouldBe("10.000.000");
        vm.TotalText.ShouldBe("10.000.000");
        vm.TotalInWords.ShouldBe("Mười triệu đồng.");
    }

    [Fact]
    public void Project_line_is_bold_and_dot_lines_are_indented()
    {
        var (vm, _, _) = Create();
        Load(vm, Row(0, 10_000_000m));
        vm.Picker.SelectAllCommand.Execute(null);

        vm.Lines[0].IsBold.ShouldBeTrue();
        vm.Lines[0].Indent.ShouldBe(0);
        vm.Lines[1].Indent.ShouldBe(1);
        vm.Lines[2].Indent.ShouldBe(2);
    }

    [Fact]
    public void Editing_a_manual_field_writes_through_and_announces_the_change()
    {
        var (vm, _, _) = Create();
        Load(vm, Row(0, 10_000_000m));
        var announced = 0;
        vm.DeclarationChanged += (_, _) => announced++;

        vm.SoThongTri = "12/TT";
        vm.Khoan = "11";

        announced.ShouldBe(2);
        vm.ToDeclaration().SoThongTri.ShouldBe("12/TT");
        vm.ToDeclaration().Khoan.ShouldBe("11");
    }

    [Fact]
    public void Refresh_loads_the_stored_declaration_into_the_boxes()
    {
        var (vm, _, _) = Create();

        vm.Refresh(new[] { Row(0, 10_000_000m) }, Array.Empty<LedgerRowViewModel>(),
            new ThongTriDeclaration { SoThongTri = "9/TT", TieuMuc = "9301" },
            new FormOrgInfo(null, null), "Dự án A");

        vm.SoThongTri.ShouldBe("9/TT");
        vm.TieuMuc.ShouldBe("9301");
    }

    [Fact]
    public async Task Export_writes_the_file_the_user_picked()
    {
        var (vm, exporter, dialog) = Create();
        Load(vm, Row(0, 10_000_000m));
        vm.Picker.SelectAllCommand.Execute(null);
        dialog.PickSaveFileResult = "/tmp/tt.xlsx";

        await vm.ExportCommand.ExecuteAsync(null);

        exporter.Verify(e => e.ExportThongTriAsync(
            It.Is<ThongTriForm>(f => f.Total == 10_000_000m && f.SoThongTri == null),
            "/tmp/tt.xlsx", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Export_does_nothing_when_the_user_cancels_the_save_dialog()
    {
        var (vm, exporter, dialog) = Create();
        Load(vm, Row(0, 10_000_000m));
        dialog.PickSaveFileResult = null;

        await vm.ExportCommand.ExecuteAsync(null);

        exporter.Verify(e => e.ExportThongTriAsync(
            It.IsAny<ThongTriForm>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Export_surfaces_a_failure_as_a_message()
    {
        var (vm, exporter, dialog) = Create();
        Load(vm, Row(0, 10_000_000m));
        dialog.PickSaveFileResult = "/tmp/tt.xlsx";
        exporter.Setup(e => e.ExportThongTriAsync(
                   It.IsAny<ThongTriForm>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new IOException("ổ đĩa đầy"));

        await vm.ExportCommand.ExecuteAsync(null);

        dialog.ShownMessages.ShouldContain(m => m.Contains("ổ đĩa đầy"));
    }
}
```

- [ ] **Step 2: Chạy test cho chắc là đỏ**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter FullyQualifiedName~ThongTriFormViewModelTests`
Expected: FAIL — `ThongTriFormViewModel` chưa tồn tại.

- [ ] **Step 3: Viết dòng hiển thị dùng chung**

Tạo `src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/FormLineRow.cs`:

```csharp
namespace ConstructionFinance.Desktop.ViewModels.Finance.Forms;

/// Một dòng bảng trong bản xem trước biểu mẫu. Toàn chuỗi — View không tự định dạng.
public class FormLineRow
{
    public int Indent { get; init; }
    public string Content { get; init; } = "";
    public string AmountText { get; init; } = "";
    public bool IsBold { get; init; }
}
```

- [ ] **Step 4: Viết view-model tab Thông tri**

Tạo `src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/ThongTriFormViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Domain.Finance;
using Serilog;

namespace ConstructionFinance.Desktop.ViewModels.Finance.Forms;

/// Tab xem trước Thông tri: panel chọn đợt NSNN + các ô nhập tay + bảng in tính sống.
/// Không tự tính con số nào — mọi thứ đi qua ThongTriBuilder.
public partial class ThongTriFormViewModel : ViewModelBase
{
    private readonly IFormExcelExporter _exporter;
    private readonly IDialogService _dialog;

    private IReadOnlyList<FormDotRow> _rows = Array.Empty<FormDotRow>();
    private ThongTriDeclaration _declaration = new();
    private FormOrgInfo _org = new(null, null);
    private string _projectName = "";

    public ThongTriFormViewModel(IFormExcelExporter exporter, IDialogService dialog)
    {
        _exporter = exporter;
        _dialog = dialog;
        Picker.SelectionChanged += (_, _) => Rebuild();
    }

    public FormDotPickerViewModel Picker { get; } = new(Fund.NSNN);

    public ObservableCollection<FormLineRow> Lines { get; } = new();

    public string TotalText { get; private set; } = "";
    public string TotalInWords { get; private set; } = "";

    /// Bắn khi người dùng sửa một ô nhập tay, để workbook đánh dấu bẩn.
    public event EventHandler? DeclarationChanged;

    public string? SoThongTri { get => _declaration.SoThongTri; set => SetField(_declaration.SoThongTri, value, v => _declaration.SoThongTri = v); }
    public string? NgayGhiSo { get => _declaration.NgayGhiSo; set => SetField(_declaration.NgayGhiSo, value, v => _declaration.NgayGhiSo = v); }
    public string? TenCTMT { get => _declaration.TenCTMT; set => SetField(_declaration.TenCTMT, value, v => _declaration.TenCTMT = v); }
    public string? MaSo { get => _declaration.MaSo; set => SetField(_declaration.MaSo, value, v => _declaration.MaSo = v); }
    public string? VeThangNam { get => _declaration.VeThangNam; set => SetField(_declaration.VeThangNam, value, v => _declaration.VeThangNam = v); }
    public string? TaiKhoan1 { get => _declaration.TaiKhoan1; set => SetField(_declaration.TaiKhoan1, value, v => _declaration.TaiKhoan1 = v); }
    public string? DonVi1 { get => _declaration.DonVi1; set => SetField(_declaration.DonVi1, value, v => _declaration.DonVi1 = v); }
    public string? SoChungTu1 { get => _declaration.SoChungTu1; set => SetField(_declaration.SoChungTu1, value, v => _declaration.SoChungTu1 = v); }
    public string? TaiKhoan2 { get => _declaration.TaiKhoan2; set => SetField(_declaration.TaiKhoan2, value, v => _declaration.TaiKhoan2 = v); }
    public string? DonVi2 { get => _declaration.DonVi2; set => SetField(_declaration.DonVi2, value, v => _declaration.DonVi2 = v); }
    public string? SoChungTu2 { get => _declaration.SoChungTu2; set => SetField(_declaration.SoChungTu2, value, v => _declaration.SoChungTu2 = v); }
    public string? LoaiNganSach { get => _declaration.LoaiNganSach; set => SetField(_declaration.LoaiNganSach, value, v => _declaration.LoaiNganSach = v); }
    public string? Khoan { get => _declaration.Khoan; set => SetField(_declaration.Khoan, value, v => _declaration.Khoan = v); }
    public string? Muc { get => _declaration.Muc; set => SetField(_declaration.Muc, value, v => _declaration.Muc = v); }
    public string? TieuMuc { get => _declaration.TieuMuc; set => SetField(_declaration.TieuMuc, value, v => _declaration.TieuMuc = v); }
    public string? TietMuc { get => _declaration.TietMuc; set => SetField(_declaration.TietMuc, value, v => _declaration.TietMuc = v); }
    public string? Nganh { get => _declaration.Nganh; set => SetField(_declaration.Nganh, value, v => _declaration.Nganh = v); }

    /// Nạp lại từ cây hiện tại (kể cả phần chưa lưu) và từ bản khai đã lưu.
    public void Refresh(
        IReadOnlyList<FormDotRow> rows,
        IReadOnlyList<LedgerRowViewModel> sources,
        ThongTriDeclaration declaration,
        FormOrgInfo org,
        string projectName)
    {
        _rows = rows;
        _declaration = declaration;
        _org = org;
        _projectName = projectName;

        Picker.Refresh(rows, sources);
        RaiseAllDeclarationProperties();
        Rebuild();
    }

    /// Bản khai hiện tại để workbook lưu kèm cây.
    public ThongTriDeclaration ToDeclaration() => _declaration;

    private void Rebuild()
    {
        var form = ThongTriBuilder.Build(_rows, Picker.SelectedKeys, _declaration, _org, _projectName);

        Lines.Clear();
        foreach (var line in form.Lines)
            Lines.Add(new FormLineRow
            {
                Indent = line.Level,
                Content = line.Content,
                AmountText = MoneyText.Format(line.Amount),
                IsBold = line.Level == 0
            });

        TotalText = MoneyText.Format(form.Total);
        TotalInWords = form.TotalInWords;
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(TotalInWords));
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var suggested = $"ThongTri_{FileNames.Sanitize(_projectName)}_{DateTime.Now:yyyyMMdd}.xlsx";
        var path = await _dialog.PickSaveFileAsync("Xuất Thông tri", suggested, new[] { "xlsx" });
        if (path is null) return;

        try
        {
            var form = ThongTriBuilder.Build(_rows, Picker.SelectedKeys, _declaration, _org, _projectName);
            await _exporter.ExportThongTriAsync(form, path);
            await _dialog.ShowMessageAsync("Xuất Excel", $"Đã xuất Thông tri ra:\n{path}");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Xuất Thông tri thất bại");
            await _dialog.ShowMessageAsync("Lỗi", $"Không xuất được Thông tri: {ex.Message}");
        }
    }

    private void SetField(string? current, string? value, Action<string?> assign, [CallerMemberName] string? name = null)
    {
        if (current == value) return;
        assign(value);
        OnPropertyChanged(name);
        Rebuild();
        DeclarationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseAllDeclarationProperties()
    {
        foreach (var name in new[]
                 {
                     nameof(SoThongTri), nameof(NgayGhiSo), nameof(TenCTMT), nameof(MaSo), nameof(VeThangNam),
                     nameof(TaiKhoan1), nameof(DonVi1), nameof(SoChungTu1), nameof(TaiKhoan2), nameof(DonVi2),
                     nameof(SoChungTu2), nameof(LoaiNganSach), nameof(Khoan), nameof(Muc), nameof(TieuMuc),
                     nameof(TietMuc), nameof(Nganh)
                 })
            OnPropertyChanged(name);
    }
}
```

- [ ] **Step 5: Thêm bộ làm sạch tên tệp**

Tạo `src/ConstructionFinance.Desktop/Common/FileNames.cs`:

```csharp
namespace ConstructionFinance.Desktop.Common;

/// Tên dự án do người dùng đặt nên có thể chứa ký tự Windows không cho phép trong
/// tên tệp. Dùng chung cho tên tệp gợi ý của ba biểu mẫu.
public static class FileNames
{
    public static string Sanitize(string? raw)
    {
        var cleaned = new string((raw ?? "").Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)).ToArray())
            .Replace(' ', '_')
            .Trim('_');
        return cleaned.Length == 0 ? "DuAn" : cleaned;
    }
}
```

- [ ] **Step 6: Chạy test cho chắc là xanh**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter FullyQualifiedName~ThongTriFormViewModelTests`
Expected: PASS — cả 8 ca.

- [ ] **Step 7: Commit**

```bash
git add src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/FormLineRow.cs \
        src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/ThongTriFormViewModel.cs \
        src/ConstructionFinance.Desktop/Common/FileNames.cs \
        tests/ConstructionFinance.Desktop.Tests/Finance/Forms/ThongTriFormViewModelTests.cs
git commit -m "feat(desktop): tab xem trước Thông tri (tính sống + xuất Excel)"
```

---

### Task 12: View-model tab KS chi và GĐNTT

Cùng khuôn với Task 11. Khác: KS chi có ô tiền nhập tay (facade chuỗi qua `MoneyText`), GĐNTT có dải cảnh báo khi đợt trải nhiều gói và **chặn xuất**.

**Files:**
- Create: `src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/KsChiFormViewModel.cs`
- Create: `src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/GdnttFormViewModel.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Finance/Forms/KsChiFormViewModelTests.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Finance/Forms/GdnttFormViewModelTests.cs`

**Interfaces:**
- Consumes: `KsChiBuilder`, `GdnttBuilder` (Task 5–6); `FormDotPickerViewModel` (Task 10); `IFormExcelExporter`, `IDialogService`, `MoneyText`, `FileNames` (Task 11).
- Produces:
  - `KsChiFormViewModel(IFormExcelExporter, IDialogService)`: `Picker` (không lọc nguồn), `AcceptedTotalText/AcceptedNsnnText/AcceptedNgoaiHoiText`, `RecoveredTotalText/RecoveredNsnnText/RecoveredNgoaiHoiText`, `PaidTotalText/PaidNsnnText/PaidNgoaiHoiText`, `PaidInWords`, `GhiChuText`, ô nhập tay `NgayNhanGiayDeNghi`, `MucTieuMuc1..4`, `LyDoTuChoi`, `GhiChu`, và bốn ô tiền `ThuHoiCacNamTruocText`, `ThuHoiNamNayText`, `ThueGTGTText`, `SoTuChoiText`; `ExportCommand`; `DeclarationChanged`; `Refresh(rows, sources, KsChiDeclaration, string projectName)`; `ToDeclaration()`.
  - `GdnttFormViewModel(IFormExcelExporter, IDialogService)`: `Picker` (lọc `Fund.NgoaiHoi`), `ErrorMessage`, `CanExport`, các chuỗi `PackageName`, `ContractorName`, `BankAccountText`, `ContractTotalText`, `CumulativeBeforeText`, `UnrecoveredBeforeText`, `RequestedText`, `RequestedInWords`, `RecoveredText`, `PaidToContractorText`, ô nhập tay `So`, `NgayThangNam`, `KinhGui`, `TenDuAnCongTrinh`, `ChuDauTu`, `CanCuQuyetDinh`, `CanCuHopDong`, `CanCuGiayDeNghi`, `ThuocKeHoach`, `TaiNganHang`, hai ô tiền `ChuyenTienBaoHanhText`, `ChuyenTienTamGiuText`; `ExportCommand`; `DeclarationChanged`; `Refresh(rows, sources, GdnttDeclaration, FormOrgInfo, string projectName)`; `ToDeclaration()`.

- [ ] **Step 1: Viết test thất bại cho KS chi**

Tạo `tests/ConstructionFinance.Desktop.Tests/Finance/Forms/KsChiFormViewModelTests.cs`:

```csharp
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Desktop.ViewModels.Finance.Forms;
using ConstructionFinance.Domain.Finance;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance.Forms;

public class KsChiFormViewModelTests
{
    private static FormDotRow Row(int dotIndex, Fund fund, decimal advance, decimal klht, decimal recovered) =>
        new(new FormDotKey(0, 0, 0, dotIndex), "Vị trí 1", "CHI PHÍ XÂY DỰNG", "Gói A",
            "Cty XL A", "123456789", 50_000_000_000m, 0m, new DateOnly(2026, 3, 1), "Đợt", "CT01",
            fund, advance, klht, recovered,
            Disbursed: advance > 0m && klht == 0m ? advance : klht - recovered);

    private static (KsChiFormViewModel vm, Mock<IFormExcelExporter> exporter, FakeDialogService dialog) Create()
    {
        var exporter = new Mock<IFormExcelExporter>();
        var dialog = new FakeDialogService();
        return (new KsChiFormViewModel(exporter.Object, dialog), exporter, dialog);
    }

    [Fact]
    public void Selecting_dots_fills_the_three_fund_columns()
    {
        var (vm, _, _) = Create();
        vm.Refresh(new[]
        {
            Row(0, Fund.NSNN, 10_000_000m, 0m, 0m),
            Row(1, Fund.NgoaiHoi, 0m, 200_000_000m, 5_000_000m)
        }, Array.Empty<LedgerRowViewModel>(), new KsChiDeclaration(), "Dự án A");

        vm.Picker.SelectAllCommand.Execute(null);

        vm.AcceptedTotalText.ShouldBe("210.000.000");
        vm.AcceptedNsnnText.ShouldBe("10.000.000");
        vm.AcceptedNgoaiHoiText.ShouldBe("200.000.000");
        vm.RecoveredTotalText.ShouldBe("5.000.000");
        vm.PaidTotalText.ShouldBe("205.000.000");
        vm.PaidInWords.ShouldBe("Hai trăm linh năm triệu đồng.");
    }

    [Fact]
    public void Manual_money_boxes_parse_dotted_input_and_rebuild()
    {
        var (vm, _, _) = Create();
        vm.Refresh(new[] { Row(0, Fund.NSNN, 10_000_000m, 0m, 0m) },
            Array.Empty<LedgerRowViewModel>(), new KsChiDeclaration(), "Dự án A");

        vm.ThueGTGTText = "2.000.000";
        vm.SoTuChoiText = "500.000";

        vm.ToDeclaration().ThueGTGT.ShouldBe(2_000_000m);
        vm.ToDeclaration().SoTuChoi.ShouldBe(500_000m);
    }

    [Fact]
    public void Ghi_chu_falls_back_to_the_first_selected_dot()
    {
        var (vm, _, _) = Create();
        vm.Refresh(new[] { Row(0, Fund.NSNN, 10_000_000m, 0m, 0m) },
            Array.Empty<LedgerRowViewModel>(), new KsChiDeclaration(), "Dự án A");

        vm.Picker.SelectAllCommand.Execute(null);

        vm.GhiChuText.ShouldBe("Đợt");
    }

    [Fact]
    public async Task Export_hands_the_built_form_to_the_exporter()
    {
        var (vm, exporter, dialog) = Create();
        vm.Refresh(new[] { Row(0, Fund.NSNN, 10_000_000m, 0m, 0m) },
            Array.Empty<LedgerRowViewModel>(), new KsChiDeclaration(), "Dự án A");
        vm.Picker.SelectAllCommand.Execute(null);
        dialog.PickSaveFileResult = "/tmp/ks.xlsx";

        await vm.ExportCommand.ExecuteAsync(null);

        exporter.Verify(e => e.ExportKsChiAsync(
            It.Is<KsChiForm>(f => f.Accepted.Total == 10_000_000m),
            "/tmp/ks.xlsx", It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 2: Viết test thất bại cho GĐNTT**

Tạo `tests/ConstructionFinance.Desktop.Tests/Finance/Forms/GdnttFormViewModelTests.cs`:

```csharp
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Desktop.ViewModels.Finance.Forms;
using ConstructionFinance.Domain.Finance;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance.Forms;

public class GdnttFormViewModelTests
{
    private static FormDotRow Row(FormDotKey key, decimal advance, string packageName = "Gói A") =>
        new(key, "Vị trí 1", "CHI PHÍ XÂY DỰNG", packageName, "Cty XL A", "123456789",
            50_000_000_000m, 2_000_000_000m, new DateOnly(2026, 3, 1), "Đợt", "CT01",
            Fund.NgoaiHoi, advance, 0m, 0m, advance);

    private static (GdnttFormViewModel vm, Mock<IFormExcelExporter> exporter, FakeDialogService dialog) Create()
    {
        var exporter = new Mock<IFormExcelExporter>();
        var dialog = new FakeDialogService();
        return (new GdnttFormViewModel(exporter.Object, dialog), exporter, dialog);
    }

    private static void Load(GdnttFormViewModel vm, params FormDotRow[] rows) =>
        vm.Refresh(rows, Array.Empty<LedgerRowViewModel>(), new GdnttDeclaration(),
                   new FormOrgInfo("Sở Tài chính", "Ban QLDA số 1"), "Dự án A");

    [Fact]
    public void Selecting_one_package_fills_the_amount_block()
    {
        var (vm, _, _) = Create();
        Load(vm, Row(new FormDotKey(0, 0, 0, 0), 10_000_000m), Row(new FormDotKey(0, 0, 0, 1), 30_000_000m));

        vm.Picker.Items[1].IsSelected = true;

        vm.PackageName.ShouldBe("Gói A");
        vm.ContractorName.ShouldBe("Cty XL A");
        vm.ContractTotalText.ShouldBe("52.000.000.000");
        vm.CumulativeBeforeText.ShouldBe("10.000.000");
        vm.RequestedText.ShouldBe("30.000.000");
        vm.RequestedInWords.ShouldBe("Ba mươi triệu đồng.");
        vm.ErrorMessage.ShouldBeNull();
        vm.CanExport.ShouldBeTrue();
    }

    [Fact]
    public void Selecting_dots_from_two_packages_shows_a_warning_and_blocks_export()
    {
        var (vm, _, _) = Create();
        Load(vm, Row(new FormDotKey(0, 0, 0, 0), 10_000_000m),
                 Row(new FormDotKey(0, 0, 1, 0), 20_000_000m, "Gói B"));

        vm.Picker.SelectAllCommand.Execute(null);

        vm.ErrorMessage.ShouldNotBeNull();
        vm.ErrorMessage!.ShouldContain("một gói thầu");
        vm.CanExport.ShouldBeFalse();
    }

    [Fact]
    public void Nothing_selected_shows_the_prompt_and_blocks_export()
    {
        var (vm, _, _) = Create();
        Load(vm, Row(new FormDotKey(0, 0, 0, 0), 10_000_000m));

        vm.ErrorMessage.ShouldNotBeNull();
        vm.CanExport.ShouldBeFalse();
        vm.RequestedText.ShouldBe("");
    }

    [Fact]
    public async Task Export_is_refused_while_the_selection_is_invalid()
    {
        var (vm, exporter, dialog) = Create();
        Load(vm, Row(new FormDotKey(0, 0, 0, 0), 10_000_000m),
                 Row(new FormDotKey(0, 0, 1, 0), 20_000_000m, "Gói B"));
        vm.Picker.SelectAllCommand.Execute(null);
        dialog.PickSaveFileResult = "/tmp/gd.xlsx";

        await vm.ExportCommand.ExecuteAsync(null);

        exporter.Verify(e => e.ExportGdnttAsync(
            It.IsAny<GdnttForm>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        dialog.ShownMessages.ShouldContain(m => m.Contains("một gói thầu"));
    }

    [Fact]
    public async Task Export_hands_the_built_form_to_the_exporter()
    {
        var (vm, exporter, dialog) = Create();
        Load(vm, Row(new FormDotKey(0, 0, 0, 0), 30_000_000m));
        vm.Picker.SelectAllCommand.Execute(null);
        dialog.PickSaveFileResult = "/tmp/gd.xlsx";

        await vm.ExportCommand.ExecuteAsync(null);

        exporter.Verify(e => e.ExportGdnttAsync(
            It.Is<GdnttForm>(f => f.RequestedThisPeriod == 30_000_000m && f.PackageName == "Gói A"),
            "/tmp/gd.xlsx", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Manual_money_boxes_feed_the_contractor_payment()
    {
        var (vm, _, _) = Create();
        Load(vm, Row(new FormDotKey(0, 0, 0, 0), 30_000_000m));
        vm.Picker.SelectAllCommand.Execute(null);

        vm.ChuyenTienBaoHanhText = "2.000.000";
        vm.ChuyenTienTamGiuText = "1.000.000";

        vm.PaidToContractorText.ShouldBe("27.000.000");
        vm.ToDeclaration().ChuyenTienBaoHanh.ShouldBe(2_000_000m);
    }
}
```

- [ ] **Step 3: Chạy hai bộ test cho chắc là đỏ**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter "FullyQualifiedName~KsChiFormViewModelTests|FullyQualifiedName~GdnttFormViewModelTests"`
Expected: FAIL — hai view-model chưa tồn tại.

- [ ] **Step 4: Viết view-model KS chi**

Tạo `src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/KsChiFormViewModel.cs`:

```csharp
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Domain.Finance;
using Serilog;

namespace ConstructionFinance.Desktop.ViewModels.Finance.Forms;

/// Tab xem trước phần ghi của cơ quan kiểm soát, thanh toán. Lấy cả hai nguồn nên
/// picker không lọc Fund.
public partial class KsChiFormViewModel : ViewModelBase
{
    private readonly IFormExcelExporter _exporter;
    private readonly IDialogService _dialog;

    private IReadOnlyList<FormDotRow> _rows = Array.Empty<FormDotRow>();
    private KsChiDeclaration _declaration = new();
    private string _projectName = "";
    private KsChiForm _form = Empty();

    public KsChiFormViewModel(IFormExcelExporter exporter, IDialogService dialog)
    {
        _exporter = exporter;
        _dialog = dialog;
        Picker.SelectionChanged += (_, _) => Rebuild();
    }

    public FormDotPickerViewModel Picker { get; } = new(null);

    public event EventHandler? DeclarationChanged;

    public string AcceptedTotalText => MoneyText.Format(_form.Accepted.Total);
    public string AcceptedNsnnText => MoneyText.Format(_form.Accepted.NSNN);
    public string AcceptedNgoaiHoiText => MoneyText.Format(_form.Accepted.NgoaiHoi);
    public string RecoveredTotalText => MoneyText.Format(_form.Recovered.Total);
    public string RecoveredNsnnText => MoneyText.Format(_form.Recovered.NSNN);
    public string RecoveredNgoaiHoiText => MoneyText.Format(_form.Recovered.NgoaiHoi);
    public string PaidTotalText => MoneyText.Format(_form.PaidToBeneficiary.Total);
    public string PaidNsnnText => MoneyText.Format(_form.PaidToBeneficiary.NSNN);
    public string PaidNgoaiHoiText => MoneyText.Format(_form.PaidToBeneficiary.NgoaiHoi);
    public string PaidInWords => _form.PaidInWords;
    public string GhiChuText => _form.GhiChu;

    public string? NgayNhanGiayDeNghi { get => _declaration.NgayNhanGiayDeNghi; set => SetText(_declaration.NgayNhanGiayDeNghi, value, v => _declaration.NgayNhanGiayDeNghi = v); }
    public string? MucTieuMuc1 { get => _declaration.MucTieuMuc1; set => SetText(_declaration.MucTieuMuc1, value, v => _declaration.MucTieuMuc1 = v); }
    public string? MucTieuMuc2 { get => _declaration.MucTieuMuc2; set => SetText(_declaration.MucTieuMuc2, value, v => _declaration.MucTieuMuc2 = v); }
    public string? MucTieuMuc3 { get => _declaration.MucTieuMuc3; set => SetText(_declaration.MucTieuMuc3, value, v => _declaration.MucTieuMuc3 = v); }
    public string? MucTieuMuc4 { get => _declaration.MucTieuMuc4; set => SetText(_declaration.MucTieuMuc4, value, v => _declaration.MucTieuMuc4 = v); }
    public string? LyDoTuChoi { get => _declaration.LyDoTuChoi; set => SetText(_declaration.LyDoTuChoi, value, v => _declaration.LyDoTuChoi = v); }
    public string? GhiChu { get => _declaration.GhiChu; set => SetText(_declaration.GhiChu, value, v => _declaration.GhiChu = v); }

    // Ô tiền: facade chuỗi, không bind thẳng decimal (bài học 6A).
    public string ThuHoiCacNamTruocText { get => MoneyText.Format(_declaration.ThuHoiCacNamTruoc); set => SetMoney(_declaration.ThuHoiCacNamTruoc, value, v => _declaration.ThuHoiCacNamTruoc = v); }
    public string ThuHoiNamNayText { get => MoneyText.Format(_declaration.ThuHoiNamNay); set => SetMoney(_declaration.ThuHoiNamNay, value, v => _declaration.ThuHoiNamNay = v); }
    public string ThueGTGTText { get => MoneyText.Format(_declaration.ThueGTGT); set => SetMoney(_declaration.ThueGTGT, value, v => _declaration.ThueGTGT = v); }
    public string SoTuChoiText { get => MoneyText.Format(_declaration.SoTuChoi); set => SetMoney(_declaration.SoTuChoi, value, v => _declaration.SoTuChoi = v); }

    public void Refresh(
        IReadOnlyList<FormDotRow> rows,
        IReadOnlyList<LedgerRowViewModel> sources,
        KsChiDeclaration declaration,
        string projectName)
    {
        _rows = rows;
        _declaration = declaration;
        _projectName = projectName;

        Picker.Refresh(rows, sources);
        RaiseDeclarationProperties();
        Rebuild();
    }

    public KsChiDeclaration ToDeclaration() => _declaration;

    private static KsChiForm Empty() => KsChiBuilder.Build(
        Array.Empty<FormDotRow>(), new HashSet<FormDotKey>(), null);

    private void Rebuild()
    {
        _form = KsChiBuilder.Build(_rows, Picker.SelectedKeys, _declaration);
        foreach (var name in new[]
                 {
                     nameof(AcceptedTotalText), nameof(AcceptedNsnnText), nameof(AcceptedNgoaiHoiText),
                     nameof(RecoveredTotalText), nameof(RecoveredNsnnText), nameof(RecoveredNgoaiHoiText),
                     nameof(PaidTotalText), nameof(PaidNsnnText), nameof(PaidNgoaiHoiText),
                     nameof(PaidInWords), nameof(GhiChuText)
                 })
            OnPropertyChanged(name);
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var suggested = $"KSChi_{FileNames.Sanitize(_projectName)}_{DateTime.Now:yyyyMMdd}.xlsx";
        var path = await _dialog.PickSaveFileAsync("Xuất Kiểm soát chi", suggested, new[] { "xlsx" });
        if (path is null) return;

        try
        {
            await _exporter.ExportKsChiAsync(KsChiBuilder.Build(_rows, Picker.SelectedKeys, _declaration), path);
            await _dialog.ShowMessageAsync("Xuất Excel", $"Đã xuất Kiểm soát chi ra:\n{path}");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Xuất KS chi thất bại");
            await _dialog.ShowMessageAsync("Lỗi", $"Không xuất được Kiểm soát chi: {ex.Message}");
        }
    }

    private void SetText(string? current, string? value, Action<string?> assign, [CallerMemberName] string? name = null)
    {
        if (current == value) return;
        assign(value);
        OnPropertyChanged(name);
        Rebuild();
        DeclarationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetMoney(decimal current, string? raw, Action<decimal> assign, [CallerMemberName] string? name = null)
    {
        if (!MoneyText.TryParse(raw, out var parsed)) return;
        if (current == parsed) return;
        assign(parsed);
        OnPropertyChanged(name);
        Rebuild();
        DeclarationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseDeclarationProperties()
    {
        foreach (var name in new[]
                 {
                     nameof(NgayNhanGiayDeNghi), nameof(MucTieuMuc1), nameof(MucTieuMuc2), nameof(MucTieuMuc3),
                     nameof(MucTieuMuc4), nameof(LyDoTuChoi), nameof(GhiChu), nameof(ThuHoiCacNamTruocText),
                     nameof(ThuHoiNamNayText), nameof(ThueGTGTText), nameof(SoTuChoiText)
                 })
            OnPropertyChanged(name);
    }
}
```

- [ ] **Step 5: Viết view-model GĐNTT**

Tạo `src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/GdnttFormViewModel.cs`:

```csharp
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Domain.Finance;
using Serilog;

namespace ConstructionFinance.Desktop.ViewModels.Finance.Forms;

/// Tab xem trước Giấy đề nghị thanh toán vốn đầu tư. Một giấy = một gói thầu, nên
/// khi lựa chọn trải nhiều gói (hoặc chưa chọn gì) thì hiện dải cảnh báo và chặn xuất.
public partial class GdnttFormViewModel : ViewModelBase
{
    private readonly IFormExcelExporter _exporter;
    private readonly IDialogService _dialog;

    private IReadOnlyList<FormDotRow> _rows = Array.Empty<FormDotRow>();
    private GdnttDeclaration _declaration = new();
    private FormOrgInfo _org = new(null, null);
    private string _projectName = "";
    private GdnttForm? _form;

    public GdnttFormViewModel(IFormExcelExporter exporter, IDialogService dialog)
    {
        _exporter = exporter;
        _dialog = dialog;
        Picker.SelectionChanged += (_, _) => Rebuild();
    }

    public FormDotPickerViewModel Picker { get; } = new(Fund.NgoaiHoi);

    public event EventHandler? DeclarationChanged;

    public string? ErrorMessage { get; private set; }
    public bool CanExport => _form is not null;

    public string PackageName => _form?.PackageName ?? "";
    public string ContractorName => _form?.ContractorName ?? "";
    public string BankAccountText => _form?.BankAccount ?? "";
    public string ContractTotalText => Money(_form?.ContractTotal);
    public string CumulativeBeforeText => Money(_form?.CumulativeBefore);
    public string UnrecoveredBeforeText => Money(_form?.UnrecoveredAdvanceBefore);
    public string RequestedText => Money(_form?.RequestedThisPeriod);
    public string RequestedInWords => _form?.RequestedInWords ?? "";
    public string RecoveredText => Money(_form?.RecoveredThisPeriod);
    public string PaidToContractorText => Money(_form?.PaidToContractor);
    public string NguonVonText => "Quỹ dự trữ ngoại hối";

    public string? So { get => _declaration.So; set => SetText(_declaration.So, value, v => _declaration.So = v); }
    public string? NgayThangNam { get => _declaration.NgayThangNam; set => SetText(_declaration.NgayThangNam, value, v => _declaration.NgayThangNam = v); }
    public string? KinhGui { get => _declaration.KinhGui; set => SetText(_declaration.KinhGui, value, v => _declaration.KinhGui = v); }
    public string? TenDuAnCongTrinh { get => _declaration.TenDuAnCongTrinh; set => SetText(_declaration.TenDuAnCongTrinh, value, v => _declaration.TenDuAnCongTrinh = v); }
    public string? ChuDauTu { get => _declaration.ChuDauTu; set => SetText(_declaration.ChuDauTu, value, v => _declaration.ChuDauTu = v); }
    public string? CanCuQuyetDinh { get => _declaration.CanCuQuyetDinh; set => SetText(_declaration.CanCuQuyetDinh, value, v => _declaration.CanCuQuyetDinh = v); }
    public string? CanCuHopDong { get => _declaration.CanCuHopDong; set => SetText(_declaration.CanCuHopDong, value, v => _declaration.CanCuHopDong = v); }
    public string? CanCuGiayDeNghi { get => _declaration.CanCuGiayDeNghi; set => SetText(_declaration.CanCuGiayDeNghi, value, v => _declaration.CanCuGiayDeNghi = v); }
    public string? ThuocKeHoach { get => _declaration.ThuocKeHoach; set => SetText(_declaration.ThuocKeHoach, value, v => _declaration.ThuocKeHoach = v); }
    public string? TaiNganHang { get => _declaration.TaiNganHang; set => SetText(_declaration.TaiNganHang, value, v => _declaration.TaiNganHang = v); }

    public string ChuyenTienBaoHanhText { get => MoneyText.Format(_declaration.ChuyenTienBaoHanh); set => SetMoney(_declaration.ChuyenTienBaoHanh, value, v => _declaration.ChuyenTienBaoHanh = v); }
    public string ChuyenTienTamGiuText { get => MoneyText.Format(_declaration.ChuyenTienTamGiu); set => SetMoney(_declaration.ChuyenTienTamGiu, value, v => _declaration.ChuyenTienTamGiu = v); }

    public void Refresh(
        IReadOnlyList<FormDotRow> rows,
        IReadOnlyList<LedgerRowViewModel> sources,
        GdnttDeclaration declaration,
        FormOrgInfo org,
        string projectName)
    {
        _rows = rows;
        _declaration = declaration;
        _org = org;
        _projectName = projectName;

        Picker.Refresh(rows, sources);
        RaiseDeclarationProperties();
        Rebuild();
    }

    public GdnttDeclaration ToDeclaration() => _declaration;

    private static string Money(decimal? value) => value is null ? "" : MoneyText.Format(value.Value);

    private void Rebuild()
    {
        var result = GdnttBuilder.Build(_rows, Picker.SelectedKeys, _declaration, _org);
        _form = result.Form;
        ErrorMessage = result.Error;

        foreach (var name in new[]
                 {
                     nameof(ErrorMessage), nameof(CanExport), nameof(PackageName), nameof(ContractorName),
                     nameof(BankAccountText), nameof(ContractTotalText), nameof(CumulativeBeforeText),
                     nameof(UnrecoveredBeforeText), nameof(RequestedText), nameof(RequestedInWords),
                     nameof(RecoveredText), nameof(PaidToContractorText)
                 })
            OnPropertyChanged(name);
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var result = GdnttBuilder.Build(_rows, Picker.SelectedKeys, _declaration, _org);
        if (result.Form is null)
        {
            await _dialog.ShowMessageAsync("Chưa xuất được", result.Error ?? "Lựa chọn đợt chưa hợp lệ.");
            return;
        }

        var suggested = $"GDNTT_{FileNames.Sanitize(_projectName)}_{DateTime.Now:yyyyMMdd}.xlsx";
        var path = await _dialog.PickSaveFileAsync("Xuất Giấy đề nghị thanh toán", suggested, new[] { "xlsx" });
        if (path is null) return;

        try
        {
            await _exporter.ExportGdnttAsync(result.Form, path);
            await _dialog.ShowMessageAsync("Xuất Excel", $"Đã xuất Giấy đề nghị thanh toán ra:\n{path}");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Xuất GĐNTT thất bại");
            await _dialog.ShowMessageAsync("Lỗi", $"Không xuất được Giấy đề nghị thanh toán: {ex.Message}");
        }
    }

    private void SetText(string? current, string? value, Action<string?> assign, [CallerMemberName] string? name = null)
    {
        if (current == value) return;
        assign(value);
        OnPropertyChanged(name);
        Rebuild();
        DeclarationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetMoney(decimal current, string? raw, Action<decimal> assign, [CallerMemberName] string? name = null)
    {
        if (!MoneyText.TryParse(raw, out var parsed)) return;
        if (current == parsed) return;
        assign(parsed);
        OnPropertyChanged(name);
        Rebuild();
        DeclarationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseDeclarationProperties()
    {
        foreach (var name in new[]
                 {
                     nameof(So), nameof(NgayThangNam), nameof(KinhGui), nameof(TenDuAnCongTrinh),
                     nameof(ChuDauTu), nameof(CanCuQuyetDinh), nameof(CanCuHopDong), nameof(CanCuGiayDeNghi),
                     nameof(ThuocKeHoach), nameof(TaiNganHang), nameof(ChuyenTienBaoHanhText),
                     nameof(ChuyenTienTamGiuText)
                 })
            OnPropertyChanged(name);
    }
}
```

- [ ] **Step 6: Chạy hai bộ test cho chắc là xanh**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter "FullyQualifiedName~KsChiFormViewModelTests|FullyQualifiedName~GdnttFormViewModelTests"`
Expected: PASS — 4 ca KS chi + 6 ca GĐNTT.

- [ ] **Step 7: Commit**

```bash
git add src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/KsChiFormViewModel.cs \
        src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/GdnttFormViewModel.cs \
        tests/ConstructionFinance.Desktop.Tests/Finance/Forms/KsChiFormViewModelTests.cs \
        tests/ConstructionFinance.Desktop.Tests/Finance/Forms/GdnttFormViewModelTests.cs
git commit -m "feat(desktop): tab xem trước KS chi và GĐNTT"
```

---

### Task 13: Nối ba tab vào workbook (tính sống + lưu chung Ctrl+S)

**Files:**
- Create: `src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/FormPageViewModels.cs`
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceWorkbookViewModel.cs` (constructor, `LoadAsync`, `LoadSelectedProjectTreeAsync`, `RefreshAggregateTabs`, `IsDirty`, `SaveAllAsync`, `SaveViTriTreeAsync`, `AddProject`)
- Test: `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceWorkbookFormTabsTests.cs`

**Interfaces:**
- Consumes: `ThongTriFormViewModel`, `KsChiFormViewModel`, `GdnttFormViewModel` (Task 11–12); `FormRowFlattener` (Task 3); `ICompanyInfoService` (Task 9); `IFormExcelExporter`.
- Produces: `ThongTriFormPageViewModel`, `KsChiFormPageViewModel`, `GdnttFormPageViewModel` (đều kế thừa `WorkbookPageViewModel`, `Title` lần lượt "Thông tri", "KS chi", "GĐNTT", `Content` là VM bên trong); constructor `FinanceWorkbookViewModel(IFinanceProjectService, SummaryPageViewModel, IDialogService, IServiceProvider, IFormExcelExporter, ICompanyInfoService)`.

- [ ] **Step 1: Viết test thất bại**

Tạo `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceWorkbookFormTabsTests.cs`:

```csharp
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Desktop.ViewModels.Finance.Forms;
using ConstructionFinance.Domain.Finance;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class FinanceWorkbookFormTabsTests
{
    private static ViTri ViTriWithDots() => new()
    {
        Name = "Vị trí 1", SortOrder = 0,
        CostGroups =
        {
            new CostGroup { Name = "CHI PHÍ XÂY DỰNG", SortOrder = 0, Packages =
            {
                new Package { Name = "Gói A", ContractorName = "Cty XL A", SortOrder = 0, Dots =
                {
                    new Dot { Date = new DateOnly(2026, 3, 1), Fund = Fund.NSNN,
                              Advance = 10_000_000m, Content = "Tạm ứng lần 1", SortOrder = 0 },
                    new Dot { Date = new DateOnly(2026, 4, 1), Fund = Fund.NgoaiHoi,
                              Advance = 30_000_000m, Content = "Tạm ứng ngoại hối", SortOrder = 1 }
                } }
            } }
        }
    };

    private static (FinanceWorkbookViewModel vm, Mock<IFinanceProjectService> service) CreateSut(
        Project? tree = null)
    {
        var project = tree ?? new Project { Id = 7, Name = "Dự án A", ViTris = { ViTriWithDots() } };

        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.GetAllSummariesAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(Array.Empty<FinanceProjectSummary>());
        service.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<Project> { new() { Id = project.Id, Name = project.Name } });
        service.Setup(s => s.GetTreeAsync(project.Id, It.IsAny<CancellationToken>()))
               .ReturnsAsync(project);

        var summaryVm = new FinanceSummaryViewModel(
            service.Object, Mock.Of<IFinanceImportService>(),
            Mock.Of<IFinanceExportService>(), Mock.Of<IDialogService>());

        var companyInfo = new Mock<ICompanyInfoService>();
        companyInfo.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new CompanyInfoDto(null, null, null, null, null, null, null, null,
                       DonViCapTren: "Sở Tài chính", DonViCoSo: "Ban QLDA số 1"));

        var vm = new FinanceWorkbookViewModel(
            service.Object, new SummaryPageViewModel(summaryVm), Mock.Of<IDialogService>(),
            new ServiceCollection().BuildServiceProvider(),
            Mock.Of<IFormExcelExporter>(), companyInfo.Object);

        return (vm, service);
    }

    [Fact]
    public async Task LoadAsync_appends_the_three_form_tabs_after_quyet_toan_nam()
    {
        var (vm, _) = CreateSut();

        await vm.LoadAsync();

        vm.ViTriPages.Select(p => p.Title).ShouldBe(new[]
        {
            "Tổng hợp", "Vị trí 1", "Quyết toán năm", "Thông tri", "KS chi", "GĐNTT"
        });
    }

    [Fact]
    public async Task Form_tabs_see_the_dots_filtered_by_their_fund()
    {
        var (vm, _) = CreateSut();

        await vm.LoadAsync();

        var thongTri = vm.ViTriPages.OfType<ThongTriFormPageViewModel>().Single().Inner;
        var gdntt = vm.ViTriPages.OfType<GdnttFormPageViewModel>().Single().Inner;
        var ksChi = vm.ViTriPages.OfType<KsChiFormPageViewModel>().Single().Inner;

        thongTri.Picker.Items.Count.ShouldBe(1);      // chỉ đợt NSNN
        gdntt.Picker.Items.Count.ShouldBe(1);         // chỉ đợt ngoại hối
        ksChi.Picker.Items.Count.ShouldBe(2);         // cả hai
    }

    [Fact]
    public async Task Editing_a_form_field_makes_the_workbook_dirty()
    {
        var (vm, _) = CreateSut();
        await vm.LoadAsync();
        vm.IsDirty.ShouldBeFalse();

        vm.ViTriPages.OfType<ThongTriFormPageViewModel>().Single().Inner.SoThongTri = "12/TT";

        vm.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveAll_persists_the_three_declarations_with_the_tree()
    {
        var (vm, service) = CreateSut();
        Project? saved = null;
        service.Setup(s => s.ReplaceTreeAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
               .Callback<Project, CancellationToken>((p, _) => saved = p)
               .Returns(Task.CompletedTask);
        await vm.LoadAsync();

        vm.ViTriPages.OfType<ThongTriFormPageViewModel>().Single().Inner.SoThongTri = "12/TT";
        vm.ViTriPages.OfType<KsChiFormPageViewModel>().Single().Inner.ThueGTGTText = "2.000.000";
        vm.ViTriPages.OfType<GdnttFormPageViewModel>().Single().Inner.TaiNganHang = "Vietcombank";
        await vm.SaveAllCommand.ExecuteAsync(null);

        saved.ShouldNotBeNull();
        saved!.ThongTri!.SoThongTri.ShouldBe("12/TT");
        saved.KsChi!.ThueGTGT.ShouldBe(2_000_000m);
        saved.Gdntt!.TaiNganHang.ShouldBe("Vietcombank");
        vm.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public async Task Stored_declarations_load_into_the_form_tabs()
    {
        var project = new Project
        {
            Id = 7, Name = "Dự án A",
            ViTris = { ViTriWithDots() },
            ThongTri = new ThongTriDeclaration { SoThongTri = "9/TT" }
        };
        var (vm, _) = CreateSut(project);

        await vm.LoadAsync();

        vm.ViTriPages.OfType<ThongTriFormPageViewModel>().Single().Inner.SoThongTri.ShouldBe("9/TT");
    }

    [Fact]
    public async Task Adding_a_dot_in_the_ledger_shows_up_in_the_form_picker()
    {
        var (vm, _) = CreateSut();
        await vm.LoadAsync();
        var ledgerPage = vm.ViTriPages.OfType<ViTriPageViewModel>().Single();
        var ksChi = vm.ViTriPages.OfType<KsChiFormPageViewModel>().Single();

        var packageRow = ledgerPage.Ledger.Rows.First(r => r.Kind == RowKind.Package);
        ledgerPage.Ledger.AddEntryCommand.Execute(packageRow);
        vm.SelectedViTriPage = ksChi;      // chọn tab form → làm mới

        ksChi.Inner.Picker.Items.Count.ShouldBe(3);
    }
}
```

- [ ] **Step 2: Chạy test cho chắc là đỏ**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter FullyQualifiedName~FinanceWorkbookFormTabsTests`
Expected: FAIL — constructor `FinanceWorkbookViewModel` chưa nhận hai tham số mới và ba lớp page chưa tồn tại.

- [ ] **Step 3: Viết ba lớp tab**

Tạo `src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/FormPageViewModels.cs`:

```csharp
namespace ConstructionFinance.Desktop.ViewModels.Finance.Forms;

/// Ba tab biểu mẫu đứng cuối dải tab Vị trí. Chúng chỉ xem — không có IsDirty riêng;
/// phần bẩn do ô nhập tay được workbook theo dõi qua sự kiện DeclarationChanged.
public sealed class ThongTriFormPageViewModel : WorkbookPageViewModel
{
    public ThongTriFormPageViewModel(ThongTriFormViewModel inner)
    {
        Inner = inner;
        Title = "Thông tri";
    }

    public ThongTriFormViewModel Inner { get; }
    public override object Content => Inner;
}

public sealed class KsChiFormPageViewModel : WorkbookPageViewModel
{
    public KsChiFormPageViewModel(KsChiFormViewModel inner)
    {
        Inner = inner;
        Title = "KS chi";
    }

    public KsChiFormViewModel Inner { get; }
    public override object Content => Inner;
}

public sealed class GdnttFormPageViewModel : WorkbookPageViewModel
{
    public GdnttFormPageViewModel(GdnttFormViewModel inner)
    {
        Inner = inner;
        Title = "GĐNTT";
    }

    public GdnttFormViewModel Inner { get; }
    public override object Content => Inner;
}
```

- [ ] **Step 4: Nối vào workbook**

Trong `FinanceWorkbookViewModel.cs`:

1. Thêm `using ConstructionFinance.Application.Forms;` và `using ConstructionFinance.Desktop.ViewModels.Finance.Forms;`.

2. Đổi constructor và thêm hai field:

```csharp
    private readonly IFormExcelExporter _formExporter;
    private readonly ICompanyInfoService _companyInfo;
    private FormOrgInfo _orgInfo = new(null, null);

    public FinanceWorkbookViewModel(
        IFinanceProjectService service,
        SummaryPageViewModel summaryPage,
        IDialogService dialog,
        IServiceProvider services,
        IFormExcelExporter formExporter,
        ICompanyInfoService companyInfo)
    {
        _service = service;
        _dialog = dialog;
        _services = services;
        _formExporter = formExporter;
        _companyInfo = companyInfo;
        SummaryPage = summaryPage;

        SummaryPage.Summary.ProjectActivationRequested += async (_, projectId) =>
            await GoToProjectAsync(projectId);
    }
```

3. Thêm ba field tab cạnh `_tongHopPage` / `_yearPage`:

```csharp
    private ThongTriFormPageViewModel? _thongTriPage;
    private KsChiFormPageViewModel? _ksChiPage;
    private GdnttFormPageViewModel? _gdnttPage;

    // Ô nhập tay của biểu mẫu bẩn riêng: không ledger nào đánh dấu hộ được.
    private bool _formDeclarationDirty;
```

4. Trong `LoadAsync`, ngay trước `var projects = await _service.ListAsync();`, nạp tên đơn vị một lần:

```csharp
            var info = await _companyInfo.GetAsync();
            _orgInfo = new FormOrgInfo(info.DonViCapTren, info.DonViCoSo);
```

và thêm `_thongTriPage = null; _ksChiPage = null; _gdnttPage = null; _formDeclarationDirty = false;` cạnh các dòng dọn dẹp đang có.

5. `IsDirty` thêm một vế:

```csharp
    public bool IsDirty =>
        Pages.OfType<ProjectPageViewModel>().Any(p => p.IsDirty) ||
        ViTriPages.OfType<ViTriPageViewModel>().Any(p => p.IsDirty) ||
        _viTriStructureDirty ||
        _formDeclarationDirty;
```

6. Trong `LoadSelectedProjectTreeAsync`, sau `ViTriPages.Add(_yearPage);` thêm:

```csharp
            _thongTriPage = new ThongTriFormPageViewModel(new ThongTriFormViewModel(_formExporter, _dialog));
            _ksChiPage = new KsChiFormPageViewModel(new KsChiFormViewModel(_formExporter, _dialog));
            _gdnttPage = new GdnttFormPageViewModel(new GdnttFormViewModel(_formExporter, _dialog));

            _thongTriPage.Inner.DeclarationChanged += OnFormDeclarationChanged;
            _ksChiPage.Inner.DeclarationChanged += OnFormDeclarationChanged;
            _gdnttPage.Inner.DeclarationChanged += OnFormDeclarationChanged;

            ViTriPages.Add(_thongTriPage);
            ViTriPages.Add(_ksChiPage);
            ViTriPages.Add(_gdnttPage);
```

và ở đầu hàm, cạnh `_tongHopPage = null; _yearPage = null;`, thêm `_thongTriPage = null; _ksChiPage = null; _gdnttPage = null; _formDeclarationDirty = false;`.

Trong `AddProject` (nhánh dựng tab trong bộ nhớ cho dự án chưa lưu) cũng phải dựng ba tab, và phải dựng **trước** lời gọi `RefreshAggregateTabs()` đang có, nếu không tab form sẽ rỗng. Sửa đoạn đó thành:

```csharp
        ViTriPages.Add(_tongHopPage);
        AddViTriPage(new VitriLedgerViewModel(viTri));
        ViTriPages.Add(_yearPage);

        _thongTriPage = new ThongTriFormPageViewModel(new ThongTriFormViewModel(_formExporter, _dialog));
        _ksChiPage = new KsChiFormPageViewModel(new KsChiFormViewModel(_formExporter, _dialog));
        _gdnttPage = new GdnttFormPageViewModel(new GdnttFormViewModel(_formExporter, _dialog));
        _thongTriPage.Inner.DeclarationChanged += OnFormDeclarationChanged;
        _ksChiPage.Inner.DeclarationChanged += OnFormDeclarationChanged;
        _gdnttPage.Inner.DeclarationChanged += OnFormDeclarationChanged;
        ViTriPages.Add(_thongTriPage);
        ViTriPages.Add(_ksChiPage);
        ViTriPages.Add(_gdnttPage);

        RefreshAggregateTabs();
```

7. `InsertViTriPage` đang chèn trước tab "Quyết toán năm" — giữ nguyên, ba tab form nằm sau nó nên vẫn đúng chỗ.

8. Mở rộng `RefreshAggregateTabs` để làm mới cả ba tab form:

```csharp
    private void RefreshAggregateTabs()
    {
        if (_tongHopPage is null && _yearPage is null) return;

        var ledgerPages = ViTriPages.OfType<ViTriPageViewModel>().ToList();

        // Gán SortOrder theo đúng vị trí tab TRƯỚC khi trải phẳng, để thứ tự đợt mà
        // FormRowFlattener sinh ra khớp một-một với danh sách dòng lưới bên dưới —
        // picker dựa vào sự khớp đó để giữ lựa chọn theo đối tượng dòng.
        var viTris = new List<ViTri>();
        for (var i = 0; i < ledgerPages.Count; i++)
        {
            var viTri = ledgerPages[i].Ledger.ToViTri();
            viTri.SortOrder = i;
            viTris.Add(viTri);
        }

        _tongHopPage?.Inner.Refresh(viTris);
        _yearPage?.Inner.Refresh(viTris);
        RefreshFormTabs(viTris, ledgerPages);
    }

    /// Ba tab biểu mẫu ăn cùng một cây với hai tab tổng hợp, kể cả phần chưa lưu.
    private void RefreshFormTabs(IReadOnlyList<ViTri> viTris, IReadOnlyList<ViTriPageViewModel> ledgerPages)
    {
        if (_selectedProjectTree is null) return;
        if (_thongTriPage is null || _ksChiPage is null || _gdnttPage is null) return;

        var rows = FormRowFlattener.Flatten(viTris);
        var sources = ledgerPages
            .SelectMany(p => p.Ledger.Rows.Where(r => r.Kind == RowKind.Entry))
            .ToList();

        // Bản khai sống ngay trên cây đã nạp, nên Save-All ghi kèm mà không phải
        // ghép lại thủ công.
        _selectedProjectTree.ThongTri ??= new ThongTriDeclaration();
        _selectedProjectTree.KsChi ??= new KsChiDeclaration();
        _selectedProjectTree.Gdntt ??= new GdnttDeclaration();

        var projectName = _selectedProjectTree.Name;
        _thongTriPage.Inner.Refresh(rows, sources, _selectedProjectTree.ThongTri, _orgInfo, projectName);
        _ksChiPage.Inner.Refresh(rows, sources, _selectedProjectTree.KsChi, projectName);
        _gdnttPage.Inner.Refresh(rows, sources, _selectedProjectTree.Gdntt, _orgInfo, projectName);
    }

    private void OnFormDeclarationChanged(object? sender, EventArgs e)
    {
        _formDeclarationDirty = true;
        OnPropertyChanged(nameof(IsDirty));
    }
```

9. `OnSelectedViTriPageChanged` làm mới thêm khi chọn tab form:

```csharp
        if (value is ProjectTongHopPageViewModel or YearSettlementPageViewModel
            or ThongTriFormPageViewModel or KsChiFormPageViewModel or GdnttFormPageViewModel)
            RefreshAggregateTabs();
```

10. `SaveAllAsync`: thêm `_formDeclarationDirty` vào điều kiện lưu cây:

```csharp
        var saveViTriTree = SelectedProject is not null && _selectedProjectTree is not null &&
                             (ViTriPages.OfType<ViTriPageViewModel>().Any(p => p.IsDirty) ||
                              _viTriStructureDirty || _formDeclarationDirty);
```

11. `SaveViTriTreeAsync`: đính ba bản khai vào cây sắp ghi, và xóa cờ bẩn khi ghi xong:

```csharp
        var toSave = new Project
        {
            Id = loaded.Id,
            Code = loaded.Code,
            Name = loaded.Name,
            Notes = loaded.Notes,
            ThongTri = _thongTriPage?.Inner.ToDeclaration(),
            KsChi = _ksChiPage?.Inner.ToDeclaration(),
            Gdntt = _gdnttPage?.Inner.ToDeclaration()
        };
```

và trong khối `try` sau `ReplaceTreeAsync` thành công, thêm `_formDeclarationDirty = false;` cạnh `_viTriStructureDirty = false;`.

- [ ] **Step 5: Chạy test cho chắc là xanh**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter FullyQualifiedName~FinanceWorkbookFormTabsTests`
Expected: PASS — cả 6 ca.

- [ ] **Step 6: Chạy cả bộ test Desktop cho chắc không vỡ 7B/7C**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests`
Expected: PASS. Nếu `FinanceWorkbookViewModelTests` / `FinanceWorkbookTongHopTabsTests` gãy vì constructor đổi, sửa chỗ dựng SUT trong các tệp test đó: thêm `Mock.Of<IFormExcelExporter>()` và một `Mock<ICompanyInfoService>` trả về `new CompanyInfoDto(null, null, null, null, null, null, null, null)` — **không** đổi phần khẳng định của các ca cũ.

- [ ] **Step 7: Commit**

```bash
git add src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/FormPageViewModels.cs \
        src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceWorkbookViewModel.cs \
        tests/ConstructionFinance.Desktop.Tests/Finance/
git commit -m "feat(desktop): ba tab biểu mẫu trong workbook Vị trí, lưu chung Ctrl+S"
```

---

### Task 14: Ba View dạng tờ giấy + đăng ký DI + chạy thật

Task cuối: phần nhìn. `ViewLocator` tự tìm View theo tên (`ViewModels.X.YViewModel` → `Views.X.YView`), nên chỉ cần đặt đúng tên và namespace.

**Files:**
- Create: `src/ConstructionFinance.Desktop/Views/Finance/Forms/FormDotPickerView.axaml` + `.axaml.cs`
- Create: `src/ConstructionFinance.Desktop/Views/Finance/Forms/ThongTriFormView.axaml` + `.axaml.cs`
- Create: `src/ConstructionFinance.Desktop/Views/Finance/Forms/KsChiFormView.axaml` + `.axaml.cs`
- Create: `src/ConstructionFinance.Desktop/Views/Finance/Forms/GdnttFormView.axaml` + `.axaml.cs`
- Modify: `src/ConstructionFinance.Application/DependencyInjection.cs:38` (thêm một dòng)
- Test: `tests/ConstructionFinance.Application.Tests/DependencyInjectionTests.cs` (thêm một khẳng định)

**Interfaces:**
- Consumes: mọi thứ của Task 10–13.
- Produces: không có API mới — chỉ View và một đăng ký DI.

- [ ] **Step 1: Đăng ký exporter vào DI (test trước)**

Trong `tests/ConstructionFinance.Application.Tests/DependencyInjectionTests.cs`, thêm một dòng khẳng định vào cuối ca `AddApplicationServices_resolves_all_services` đang có (nó dựng provider thật rồi kiểm từng dịch vụ):

```csharp
        sp.GetService<IFormExcelExporter>().ShouldNotBeNull();
```

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter FullyQualifiedName~DependencyInjectionTests`
Expected: FAIL — chưa đăng ký.

- [ ] **Step 2: Thêm đăng ký**

Trong `src/ConstructionFinance.Application/DependencyInjection.cs`, ngay dưới dòng `services.AddScoped<IFinanceExportService, FinanceExportService>();`:

```csharp
        services.AddScoped<IFormExcelExporter, Forms.FormExcelExporter>();
```

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter FullyQualifiedName~DependencyInjectionTests`
Expected: PASS.

- [ ] **Step 3: View của panel chọn đợt**

Tạo `src/ConstructionFinance.Desktop/Views/Finance/Forms/FormDotPickerView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Finance.Forms"
             x:Class="ConstructionFinance.Desktop.Views.Finance.Forms.FormDotPickerView"
             x:DataType="vm:FormDotPickerViewModel">
    <DockPanel>
        <Border DockPanel.Dock="Top" Classes="toolbarStrip" Margin="0,0,0,6">
            <StackPanel Orientation="Horizontal" Spacing="8">
                <TextBlock Text="Tháng:" Classes="formLabel" VerticalAlignment="Center"/>
                <TextBox Width="60" Text="{Binding MonthFilter}"/>
                <TextBlock Text="Năm:" Classes="formLabel" VerticalAlignment="Center"/>
                <TextBox Width="80" Text="{Binding YearFilter}"/>
                <Button Classes="toolbar" Command="{Binding SelectAllCommand}" Content="Chọn tất cả"/>
                <Button Classes="toolbar" Command="{Binding ClearSelectionCommand}" Content="Bỏ chọn"/>
                <TextBlock Text="{Binding SelectionSummary}" Classes="muted" VerticalAlignment="Center"/>
            </StackPanel>
        </Border>

        <DataGrid ItemsSource="{Binding Items}" IsReadOnly="False" Height="180"
                  GridLinesVisibility="All" HeadersVisibility="All" CanUserSortColumns="False">
            <DataGrid.Columns>
                <DataGridCheckBoxColumn Header="Chọn" Binding="{Binding IsSelected}" Width="60"/>
                <DataGridTextColumn Header="Ngày" Binding="{Binding DateText}" Width="100" IsReadOnly="True"/>
                <DataGridTextColumn Header="Vị trí" Binding="{Binding ViTriName}" Width="130" IsReadOnly="True"/>
                <DataGridTextColumn Header="Gói thầu" Binding="{Binding PackageName}" Width="130" IsReadOnly="True"/>
                <DataGridTextColumn Header="Nội dung" Binding="{Binding ContentText}" Width="240" IsReadOnly="True"/>
                <DataGridTextColumn Header="Nguồn" Binding="{Binding FundText}" Width="150" IsReadOnly="True"/>
                <DataGridTextColumn Header="Tạm ứng" Binding="{Binding AdvanceText}" Width="120" IsReadOnly="True"/>
                <DataGridTextColumn Header="TT KLHT" Binding="{Binding KlhtText}" Width="120" IsReadOnly="True"/>
                <DataGridTextColumn Header="Thu hồi" Binding="{Binding RecoveredText}" Width="120" IsReadOnly="True"/>
                <DataGridTextColumn Header="Giải ngân" Binding="{Binding DisbursedText}" Width="120" IsReadOnly="True"/>
            </DataGrid.Columns>
        </DataGrid>
    </DockPanel>
</UserControl>
```

Tạo `FormDotPickerView.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ConstructionFinance.Desktop.Views.Finance.Forms;

public partial class FormDotPickerView : UserControl
{
    public FormDotPickerView() => AvaloniaXamlLoader.Load(this);
}
```

(Ba tệp `.axaml.cs` còn lại y hệt, chỉ đổi tên lớp.)

- [ ] **Step 4: View Thông tri**

Tạo `src/ConstructionFinance.Desktop/Views/Finance/Forms/ThongTriFormView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Finance.Forms"
             xmlns:conv="using:ConstructionFinance.Desktop.Common"
             x:Class="ConstructionFinance.Desktop.Views.Finance.Forms.ThongTriFormView"
             x:DataType="vm:ThongTriFormViewModel">
    <DockPanel>
        <ContentControl DockPanel.Dock="Top" Content="{Binding Picker}"/>
        <Button DockPanel.Dock="Top" Classes="toolbar accent" HorizontalAlignment="Left"
                Margin="0,6,0,10" Command="{Binding ExportCommand}" Content="Xuất Excel"/>

        <ScrollViewer>
            <Border Classes="card" MaxWidth="900" HorizontalAlignment="Left" Padding="24">
                <StackPanel Spacing="8">
                    <Grid ColumnDefinitions="*,*">
                        <StackPanel Grid.Column="0" Spacing="4">
                            <TextBox Watermark="Đơn vị cấp trên (Cài đặt)" IsEnabled="False"/>
                            <TextBox Watermark="Số" Text="{Binding SoThongTri}"/>
                        </StackPanel>
                        <StackPanel Grid.Column="1" Spacing="4">
                            <TextBlock Text="Mẫu số CQ06-HD" HorizontalAlignment="Right"/>
                            <TextBox Watermark="Ghi sổ ngày…" Text="{Binding NgayGhiSo}"/>
                        </StackPanel>
                    </Grid>

                    <TextBlock Text="THÔNG TRI" FontWeight="Bold" FontSize="18"
                               HorizontalAlignment="Center"/>
                    <TextBlock Text="Thu hồi tạm ứng — Kinh phí: NSNN" HorizontalAlignment="Center"/>

                    <Grid ColumnDefinitions="Auto,*" ColumnSpacing="8">
                        <TextBlock Grid.Column="0" Text="Về" VerticalAlignment="Center"/>
                        <TextBox Grid.Column="1" Watermark="tháng … năm …" Text="{Binding VeThangNam}"/>
                    </Grid>
                    <Grid ColumnDefinitions="Auto,*,Auto,*" ColumnSpacing="8">
                        <TextBlock Grid.Column="0" Text="Tên CTMT, DA" VerticalAlignment="Center"/>
                        <TextBox Grid.Column="1" Text="{Binding TenCTMT}"/>
                        <TextBlock Grid.Column="2" Text="Mã số" VerticalAlignment="Center"/>
                        <TextBox Grid.Column="3" Text="{Binding MaSo}"/>
                    </Grid>
                    <Grid ColumnDefinitions="Auto,*,Auto,*,Auto,*" ColumnSpacing="6">
                        <TextBlock Grid.Column="0" Text="TK" VerticalAlignment="Center"/>
                        <TextBox Grid.Column="1" Text="{Binding TaiKhoan1}"/>
                        <TextBlock Grid.Column="2" Text="Đơn vị" VerticalAlignment="Center"/>
                        <TextBox Grid.Column="3" Text="{Binding DonVi1}"/>
                        <TextBlock Grid.Column="4" Text="Số CT" VerticalAlignment="Center"/>
                        <TextBox Grid.Column="5" Text="{Binding SoChungTu1}"/>
                    </Grid>
                    <Grid ColumnDefinitions="Auto,*,Auto,*,Auto,*" ColumnSpacing="6">
                        <TextBlock Grid.Column="0" Text="TK" VerticalAlignment="Center"/>
                        <TextBox Grid.Column="1" Text="{Binding TaiKhoan2}"/>
                        <TextBlock Grid.Column="2" Text="Đơn vị" VerticalAlignment="Center"/>
                        <TextBox Grid.Column="3" Text="{Binding DonVi2}"/>
                        <TextBlock Grid.Column="4" Text="Số CT" VerticalAlignment="Center"/>
                        <TextBox Grid.Column="5" Text="{Binding SoChungTu2}"/>
                    </Grid>

                    <TextBlock Text="Mục lục ngân sách" FontWeight="Bold" Margin="0,8,0,0"/>
                    <Grid ColumnDefinitions="*,*,*,*,*,*" ColumnSpacing="6">
                        <TextBox Grid.Column="0" Watermark="Loại NS" Text="{Binding LoaiNganSach}"/>
                        <TextBox Grid.Column="1" Watermark="Khoản" Text="{Binding Khoan}"/>
                        <TextBox Grid.Column="2" Watermark="Mục" Text="{Binding Muc}"/>
                        <TextBox Grid.Column="3" Watermark="Tiểu mục" Text="{Binding TieuMuc}"/>
                        <TextBox Grid.Column="4" Watermark="Tiết mục" Text="{Binding TietMuc}"/>
                        <TextBox Grid.Column="5" Watermark="Ngành" Text="{Binding Nganh}"/>
                    </Grid>

                    <DataGrid ItemsSource="{Binding Lines}" IsReadOnly="True" Margin="0,8,0,0"
                              GridLinesVisibility="All" HeadersVisibility="All" CanUserSortColumns="False">
                        <DataGrid.Styles>
                            <Style Selector="DataGridRow" x:DataType="vm:FormLineRow">
                                <Setter Property="FontWeight"
                                        Value="{Binding IsBold, Converter={x:Static conv:BoolToFontWeightConverter.Instance}}"/>
                            </Style>
                        </DataGrid.Styles>
                        <DataGrid.Columns>
                            <DataGridTextColumn Header="NỘI DUNG" Binding="{Binding Content}" Width="*"/>
                            <DataGridTextColumn Header="SỐ TIỀN" Binding="{Binding AmountText}" Width="180"/>
                        </DataGrid.Columns>
                    </DataGrid>

                    <Grid ColumnDefinitions="*,180" Margin="0,4,0,0">
                        <TextBlock Grid.Column="0" Text="Cộng" FontWeight="Bold" HorizontalAlignment="Right"
                                   Margin="0,0,12,0"/>
                        <TextBlock Grid.Column="1" Text="{Binding TotalText}" FontWeight="Bold"/>
                    </Grid>
                    <TextBlock Margin="0,4,0,0">
                        <Run Text="Bằng chữ: "/><Run Text="{Binding TotalInWords}" FontStyle="Italic"/>
                    </TextBlock>
                </StackPanel>
            </Border>
        </ScrollViewer>
    </DockPanel>
</UserControl>
```

- [ ] **Step 5: View KS chi**

Tạo `src/ConstructionFinance.Desktop/Views/Finance/Forms/KsChiFormView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Finance.Forms"
             x:Class="ConstructionFinance.Desktop.Views.Finance.Forms.KsChiFormView"
             x:DataType="vm:KsChiFormViewModel">
    <DockPanel>
        <ContentControl DockPanel.Dock="Top" Content="{Binding Picker}"/>
        <Button DockPanel.Dock="Top" Classes="toolbar accent" HorizontalAlignment="Left"
                Margin="0,6,0,10" Command="{Binding ExportCommand}" Content="Xuất Excel"/>

        <ScrollViewer>
            <Border Classes="card" MaxWidth="900" HorizontalAlignment="Left" Padding="24">
                <StackPanel Spacing="8">
                    <TextBlock Text="PHẦN GHI CỦA CƠ QUAN KIỂM SOÁT, THANH TOÁN"
                               FontWeight="Bold" HorizontalAlignment="Center"/>
                    <Grid ColumnDefinitions="Auto,*" ColumnSpacing="8">
                        <TextBlock Grid.Column="0" Text="Ngày nhận giấy đề nghị" VerticalAlignment="Center"/>
                        <TextBox Grid.Column="1" Text="{Binding NgayNhanGiayDeNghi}"/>
                    </Grid>

                    <Grid ColumnDefinitions="2*,*,*,*" ColumnSpacing="8" Margin="0,8,0,0">
                        <TextBlock Grid.Column="0" Text="Nội dung" FontWeight="Bold"/>
                        <TextBlock Grid.Column="1" Text="Tổng số" FontWeight="Bold"/>
                        <TextBlock Grid.Column="2" Text="Vốn trong nước" FontWeight="Bold"/>
                        <TextBlock Grid.Column="3" Text="Vốn nước ngoài" FontWeight="Bold"/>
                    </Grid>
                    <Grid ColumnDefinitions="2*,*,*,*" ColumnSpacing="8">
                        <TextBlock Grid.Column="0" Text="Số vốn chấp nhận"/>
                        <TextBlock Grid.Column="1" Text="{Binding AcceptedTotalText}"/>
                        <TextBlock Grid.Column="2" Text="{Binding AcceptedNsnnText}"/>
                        <TextBlock Grid.Column="3" Text="{Binding AcceptedNgoaiHoiText}"/>
                    </Grid>
                    <TextBox Watermark="+ Mục ……, tiểu mục ……" Text="{Binding MucTieuMuc1}"/>
                    <TextBox Watermark="+ Mục ……, tiểu mục ……" Text="{Binding MucTieuMuc2}"/>
                    <TextBox Watermark="+ Mục ……, tiểu mục ……" Text="{Binding MucTieuMuc3}"/>
                    <TextBox Watermark="+ Mục ……, tiểu mục ……" Text="{Binding MucTieuMuc4}"/>

                    <TextBlock Text="Trong đó:" FontWeight="Bold" Margin="0,8,0,0"/>
                    <Grid ColumnDefinitions="2*,*,*,*" ColumnSpacing="8">
                        <TextBlock Grid.Column="0" Text="+ Số thu hồi tạm ứng"/>
                        <TextBlock Grid.Column="1" Text="{Binding RecoveredTotalText}"/>
                        <TextBlock Grid.Column="2" Text="{Binding RecoveredNsnnText}"/>
                        <TextBlock Grid.Column="3" Text="{Binding RecoveredNgoaiHoiText}"/>
                    </Grid>
                    <Grid ColumnDefinitions="2*,*" ColumnSpacing="8">
                        <TextBlock Grid.Column="0" Text="   Các năm trước" VerticalAlignment="Center"/>
                        <TextBox Grid.Column="1" Text="{Binding ThuHoiCacNamTruocText}"/>
                    </Grid>
                    <Grid ColumnDefinitions="2*,*" ColumnSpacing="8">
                        <TextBlock Grid.Column="0" Text="   Năm nay" VerticalAlignment="Center"/>
                        <TextBox Grid.Column="1" Text="{Binding ThuHoiNamNayText}"/>
                    </Grid>
                    <Grid ColumnDefinitions="2*,*" ColumnSpacing="8">
                        <TextBlock Grid.Column="0" Text="+ Thuế giá trị gia tăng" VerticalAlignment="Center"/>
                        <TextBox Grid.Column="1" Text="{Binding ThueGTGTText}"/>
                    </Grid>

                    <Grid ColumnDefinitions="2*,*,*,*" ColumnSpacing="8" Margin="0,8,0,0">
                        <TextBlock Grid.Column="0" Text="+ Số trả đơn vị thụ hưởng" FontWeight="Bold"/>
                        <TextBlock Grid.Column="1" Text="{Binding PaidTotalText}" FontWeight="Bold"/>
                        <TextBlock Grid.Column="2" Text="{Binding PaidNsnnText}" FontWeight="Bold"/>
                        <TextBlock Grid.Column="3" Text="{Binding PaidNgoaiHoiText}" FontWeight="Bold"/>
                    </Grid>
                    <TextBlock Text="{Binding PaidInWords}" FontStyle="Italic"/>

                    <Grid ColumnDefinitions="2*,*" ColumnSpacing="8" Margin="0,8,0,0">
                        <TextBlock Grid.Column="0" Text="Số từ chối" VerticalAlignment="Center"/>
                        <TextBox Grid.Column="1" Text="{Binding SoTuChoiText}"/>
                    </Grid>
                    <TextBox Watermark="Lý do từ chối" Text="{Binding LyDoTuChoi}"/>
                    <TextBox Watermark="Ghi chú (để trống thì lấy nội dung đợt đầu tiên)"
                             Text="{Binding GhiChu}"/>
                    <TextBlock Text="{Binding GhiChuText}" Classes="muted"/>
                </StackPanel>
            </Border>
        </ScrollViewer>
    </DockPanel>
</UserControl>
```

- [ ] **Step 6: View GĐNTT**

Tạo `src/ConstructionFinance.Desktop/Views/Finance/Forms/GdnttFormView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Finance.Forms"
             x:Class="ConstructionFinance.Desktop.Views.Finance.Forms.GdnttFormView"
             x:DataType="vm:GdnttFormViewModel">
    <DockPanel>
        <ContentControl DockPanel.Dock="Top" Content="{Binding Picker}"/>
        <Border DockPanel.Dock="Top" Background="#FFF4CE" Padding="10" Margin="0,6,0,0"
                IsVisible="{Binding ErrorMessage, Converter={x:Static ObjectConverters.IsNotNull}}">
            <TextBlock Text="{Binding ErrorMessage}" TextWrapping="Wrap"/>
        </Border>
        <Button DockPanel.Dock="Top" Classes="toolbar accent" HorizontalAlignment="Left"
                Margin="0,6,0,10" Command="{Binding ExportCommand}"
                IsEnabled="{Binding CanExport}" Content="Xuất Excel"/>

        <ScrollViewer>
            <Border Classes="card" MaxWidth="1000" HorizontalAlignment="Left" Padding="24">
                <StackPanel Spacing="8">
                    <TextBlock Text="CỘNG HOÀ XÃ HỘI CHỦ NGHĨA VIỆT NAM" FontWeight="Bold"
                               HorizontalAlignment="Center"/>
                    <TextBlock Text="Độc lập - Tự do - Hạnh phúc" HorizontalAlignment="Center"/>
                    <TextBlock Text="GIẤY ĐỀ NGHỊ THANH TOÁN VỐN ĐẦU TƯ" FontWeight="Bold" FontSize="18"
                               HorizontalAlignment="Center" Margin="0,8,0,8"/>

                    <Grid ColumnDefinitions="Auto,*,Auto,*" ColumnSpacing="8">
                        <TextBlock Grid.Column="0" Text="Số" VerticalAlignment="Center"/>
                        <TextBox Grid.Column="1" Text="{Binding So}"/>
                        <TextBlock Grid.Column="2" Text="Ngày" VerticalAlignment="Center"/>
                        <TextBox Grid.Column="3" Text="{Binding NgayThangNam}"/>
                    </Grid>
                    <TextBox Watermark="Kính gửi" Text="{Binding KinhGui}"/>
                    <TextBox Watermark="Tên dự án công trình" Text="{Binding TenDuAnCongTrinh}"/>
                    <TextBox Watermark="Chủ đầu tư / Ban QLDA" Text="{Binding ChuDauTu}"/>
                    <TextBox Watermark="Căn cứ Quyết định số" Text="{Binding CanCuQuyetDinh}"/>
                    <TextBox Watermark="Căn cứ Hợp đồng số" Text="{Binding CanCuHopDong}"/>
                    <TextBox Watermark="Căn cứ giấy đề nghị thanh toán số" Text="{Binding CanCuGiayDeNghi}"/>
                    <TextBox Watermark="Thuộc kế hoạch" Text="{Binding ThuocKeHoach}"/>

                    <Grid ColumnDefinitions="Auto,*" ColumnSpacing="8">
                        <TextBlock Grid.Column="0" Text="Số dư tạm ứng của hạng mục:"/>
                        <TextBlock Grid.Column="1" Text="{Binding UnrecoveredBeforeText}"/>
                    </Grid>
                    <Grid ColumnDefinitions="Auto,*" ColumnSpacing="8">
                        <TextBlock Grid.Column="0" Text="Thuộc nguồn vốn:"/>
                        <TextBlock Grid.Column="1" Text="{Binding NguonVonText}"/>
                    </Grid>

                    <Grid ColumnDefinitions="2*,*,*,*" ColumnSpacing="8" Margin="0,8,0,0">
                        <TextBlock Grid.Column="0" Text="NỘI DUNG" FontWeight="Bold" TextWrapping="Wrap"/>
                        <TextBlock Grid.Column="1" Text="Giá trị HĐ (gồm PLHĐ)" FontWeight="Bold" TextWrapping="Wrap"/>
                        <TextBlock Grid.Column="2" Text="Lũy kế đến cuối kỳ trước" FontWeight="Bold" TextWrapping="Wrap"/>
                        <TextBlock Grid.Column="3" Text="Số đề nghị kỳ này" FontWeight="Bold" TextWrapping="Wrap"/>
                    </Grid>
                    <Grid ColumnDefinitions="2*,*,*,*" ColumnSpacing="8">
                        <TextBlock Grid.Column="0" Text="{Binding PackageName}"/>
                        <TextBlock Grid.Column="1" Text="{Binding ContractTotalText}"/>
                        <TextBlock Grid.Column="2" Text="{Binding CumulativeBeforeText}"/>
                        <TextBlock Grid.Column="3" Text="{Binding RequestedText}"/>
                    </Grid>

                    <TextBlock Margin="0,8,0,0">
                        <Run Text="Bằng chữ: "/><Run Text="{Binding RequestedInWords}" FontStyle="Italic"/>
                    </TextBlock>

                    <TextBlock Text="Trong đó:" FontWeight="Bold" Margin="0,8,0,0"/>
                    <Grid ColumnDefinitions="2*,*" ColumnSpacing="8">
                        <TextBlock Grid.Column="0" Text=" - Thanh toán để thu hồi tạm ứng"/>
                        <TextBlock Grid.Column="1" Text="{Binding RecoveredText}"/>
                    </Grid>
                    <Grid ColumnDefinitions="2*,*" ColumnSpacing="8">
                        <TextBlock Grid.Column="0" Text=" - Chuyển tiền bảo hành" VerticalAlignment="Center"/>
                        <TextBox Grid.Column="1" Text="{Binding ChuyenTienBaoHanhText}"/>
                    </Grid>
                    <Grid ColumnDefinitions="2*,*" ColumnSpacing="8">
                        <TextBlock Grid.Column="0" Text=" - Chuyển tiền tạm giữ chờ quyết toán" VerticalAlignment="Center"/>
                        <TextBox Grid.Column="1" Text="{Binding ChuyenTienTamGiuText}"/>
                    </Grid>
                    <Grid ColumnDefinitions="2*,*" ColumnSpacing="8">
                        <TextBlock Grid.Column="0" Text=" - Số trả nhà thầu" FontWeight="Bold"/>
                        <TextBlock Grid.Column="1" Text="{Binding PaidToContractorText}" FontWeight="Bold"/>
                    </Grid>

                    <Grid ColumnDefinitions="Auto,*" ColumnSpacing="8" Margin="0,8,0,0">
                        <TextBlock Grid.Column="0" Text="Tên đơn vị thụ hưởng:"/>
                        <TextBlock Grid.Column="1" Text="{Binding ContractorName}"/>
                    </Grid>
                    <Grid ColumnDefinitions="Auto,*" ColumnSpacing="8">
                        <TextBlock Grid.Column="0" Text="Tài khoản:"/>
                        <TextBlock Grid.Column="1" Text="{Binding BankAccountText}"/>
                    </Grid>
                    <TextBox Watermark="Tại (ngân hàng)" Text="{Binding TaiNganHang}"/>
                </StackPanel>
            </Border>
        </ScrollViewer>
    </DockPanel>
</UserControl>
```

- [ ] **Step 7: Build và chạy toàn bộ test**

Run: `dotnet build ConstructionFinance.sln && dotnet test ConstructionFinance.sln`
Expected: build sạch (không cảnh báo XAML về binding thiếu), toàn bộ test PASS.

- [ ] **Step 8: Chạy app thật và tự chụp màn hình**

```bash
DISPLAY=:1 dotnet run --project src/ConstructionFinance.Desktop
```

Đặt `_NET_WM_STATE_ABOVE` cho cửa sổ (không dời cửa sổ), chụp bằng `mss`, rồi kiểm bằng mắt:
1. Dải tab của một dự án có đủ `Tổng hợp · <các Vị trí> · Quyết toán năm · Thông tri · KS chi · GĐNTT`.
2. Mở tab Thông tri: panel trên liệt kê **chỉ** đợt NSNN; tích một đợt thì bảng dưới hiện ngay dòng dự án/vị trí/đợt và dòng "Bằng chữ".
3. Mở tab GĐNTT khi chưa chọn gì: có dải cảnh báo vàng và nút Xuất Excel mờ.
4. Gõ vào một ô nhập tay → tiêu đề workbook hiện dấu bẩn, Ctrl+S lưu không lỗi, chọn lại dự án thấy giá trị còn nguyên.

Nếu bước nào lệch, sửa rồi chạy lại — đây là cổng duy nhất bắt được lỗi bố cục vì test không thấy giao diện.

- [ ] **Step 9: Commit**

```bash
git add src/ConstructionFinance.Desktop/Views/Finance/Forms/ \
        src/ConstructionFinance.Application/DependencyInjection.cs \
        tests/ConstructionFinance.Application.Tests/DependencyInjectionTests.cs
git commit -m "feat(desktop): ba View biểu mẫu dạng tờ giấy + đăng ký exporter"
```

---

## Gate hoàn thành pha 7D

Sau Task 14, người dùng chạy app, tích đợt trong ba tab biểu mẫu, xuất ba tệp `.xlsx` và đối chiếu với `reference/Mẫu.xlsx` — đúng bảng pha của spec cha `2026-07-28-vitri-hierarchy-forms-redesign-design.md`. Hai chỗ **cố ý lệch Mẫu** (lũy kế theo gói thầu, cộng Phụ lục HĐ vào giá trị hợp đồng) đã ghi ở spec §4.3; nếu người dùng muốn bám Mẫu thì sửa `GdnttBuilder` chứ không sửa chỗ khác.

Không thuộc 7D: dọn `ViewModels/ThongTri/*` legacy (7E), lịch sử phiếu, xuất PDF, sheet KH vốn / Vốn / QĐ.
