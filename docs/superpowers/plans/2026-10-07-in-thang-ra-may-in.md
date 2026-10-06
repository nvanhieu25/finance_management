# In thẳng ra máy in — Kế hoạch triển khai

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bấm In trong app là chọn máy in rồi giấy chạy ra, như Windows+P; đường xuất `.xlsx` vẫn giữ nguyên thành mục riêng.

**Architecture:** `PrintSheet` (model nội dung in hiện có) không sửa. Thêm `MigraDocBuilder` biến `PrintSheet[]` thành một `MigraDoc.Document`; render tài liệu đó hai đường — ra PDF bằng `PDFsharp-MigraDoc` (đa nền, chạy trên Linux, dùng làm đòn kiểm chứng) và ra giấy bằng `PDFsharp-MigraDoc-GDI` + `System.Drawing.Printing` (chỉ Windows). Vì gói GDI chỉ có `net8.0-windows7.0`, adapter máy in nằm ở dự án riêng `ConstructionFinance.Printing.Windows` sau interface `IPrinterPort`, và `Desktop` đa đích `net8.0;net8.0-windows` để 468 test Desktop vẫn chạy được trên máy phát triển Linux.

**Tech Stack:** .NET 8 · C# 12 · Avalonia 11.2.1 · CommunityToolkit.Mvvm · ClosedXML 0.102.2 (đường `.xlsx`, không đụng) · PDFsharp-MigraDoc 6.2.4 · PDFsharp-MigraDoc-GDI 6.2.4 · System.Drawing.Common 8.0.10 · xUnit + Shouldly + Moq · Avalonia.Headless

**Spec:** [docs/superpowers/specs/2026-10-07-in-thang-ra-may-in-design.md](../specs/2026-10-07-in-thang-ra-may-in-design.md)

## Global Constraints

- **Giấy phép 100% tự do.** Chỉ thêm ba gói, đều MIT: `PDFsharp-MigraDoc` 6.2.4, `PDFsharp-MigraDoc-GDI` 6.2.4, `System.Drawing.Common` 8.0.10. Cấm: Syncfusion, QuestPDF, EPPlus v5+, FluentAssertions v8+.
- **Quản lý phiên bản gói tập trung.** `Directory.Packages.props` có `ManagePackageVersionsCentrally=true`: phiên bản khai ở `<PackageVersion>`, còn `.csproj` chỉ `<PackageReference Include="..." />` **không có** `Version`.
- **`Directory.Build.props` đặt `<TargetFramework>net8.0</TargetFramework>` toàn cục.** Dự án nào muốn đa đích phải **xóa trắng** nó (`<TargetFramework></TargetFramework>`) rồi mới đặt `<TargetFrameworks>`, nếu không MSBuild lấy giá trị toàn cục và bỏ qua đa đích.
- **`WarningsAsErrors=nullable`.** Cảnh báo nullable là lỗi biên dịch. Mọi `e.Graphics`, `FormattedDocument`, `Styles["Normal"]` đều phải kiểm null tường minh.
- **Chữ người dùng đọc bằng tiếng Việt**, có dấu, không viết tắt lạ.
- **Đặt tên:** biến/tham số cục bộ `camelCase`; kiểu, phương thức, thuộc tính `PascalCase`; trường private `_camelCase`.
- **XAML:** KHÔNG bind thẳng `decimal`/`DateOnly` kiểu TwoWay kèm `StringFormat`. Số bản in bind `decimal?` ↔ `decimal?` (đúng kiểu `NumericUpDown.Value` của Avalonia 11), không qua chuỗi.
- **Cỡ chữ gốc của mọi bản in là 13pt Times New Roman** (`FormExcelExporter.FontName` / `FontSize`) — không hằng số mới.
- **Tỉ lệ co chặn trên ở 1,0:** co cho vừa bề ngang thì có, phóng to thì không (spec §5).
- **Chạy lại cả bộ test sau mỗi task.** Mốc đầu: `Passed: 817` (Domain 38 · Infrastructure 10 · Data 54 · Application 247 · Desktop 468).

## Review Focus

Năm lớp đầu vào spec hàm ý nhưng không task nào tự nhiên chạm tới. Mỗi dòng có một test, đặt ở task sở hữu đoạn mã:

1. **Số bản ngoài 1–99** (0, âm, 100000). `PrinterSettings.Copies` là `short`: `(short)100000` **tràn âm** và có thể in ngược hoặc ném. Phải kẹp 1–99 **trước** khi chạm `short`. → Task 9 + Task 10.
2. **Máy in biến mất giữa lúc chọn và lúc in** (bị xóa, bị ngắt mạng). Phải ra câu "máy in báo lỗi", không được sập app. → Task 9.
3. **Tên dự án / tên sheet mang ký tự lạ** (`&`, cặp surrogate đứt đôi, chuỗi 300 ký tự) đi vào chân trang MigraDoc. Đường `.xlsx` đã có luật riêng (`&&`, cắt 255 ký tự); MigraDoc không có luật đó và không được ném. → Task 4.
4. **Bảng rỗng** (0 dòng) và **bảng 0 cột**. `Table` không có cột mà `AddRow()` thì `Cells[0]` ném `ArgumentOutOfRangeException`. Người dùng mở một Vị trí chưa gõ gì rồi bấm In là gặp. → Task 4.
5. **Một ô chữ dài hơn cả trang sau khi co** (Nội dung 500 ký tự trong cột 38 đơn vị). Phải xuống dòng và sang trang, không được tràn ra ngoài giấy hay làm bộ phân trang quay vô hạn. → Task 4.

---

## File Structure

**Tạo mới**

| Tệp | Trách nhiệm |
|---|---|
| `src/ConstructionFinance.Application/Printing/ExcelFitScale.cs` | Số học co tỉ lệ: bề rộng cột → mm → tỉ lệ, chặn trên 1,0. Thuần hàm, không phụ thuộc gì |
| `src/ConstructionFinance.Application/Printing/PrintMoneyText.cs` | Định dạng tiền `#,##0` kiểu Việt (dấu `.`), đúng như Excel hiển thị |
| `src/ConstructionFinance.Application/Printing/MigraDocBuilder.cs` | `PrintSheet[]` → `MigraDoc.Document`: Section/khổ giấy/lề, bảng, chân trang |
| `src/ConstructionFinance.Application/Printing/MigraDocFormSheets.cs` | Ba biểu mẫu vẽ bằng `FormGrid` |
| `src/ConstructionFinance.Application/Printing/FormGrid.cs` | Lưới ô thưa 1-based mô phỏng cách ClosedXML ghi ô + gộp ô, kết xuất ra `Table` |
| `src/ConstructionFinance.Application/Printing/IPrinterPort.cs` | `IPrinterPort`, `IPrinterCatalog`, `PrinterJob`, `PrintJobFailedException` |
| `src/ConstructionFinance.Printing.Windows/ConstructionFinance.Printing.Windows.csproj` | Dự án `net8.0-windows`, chỉ chứa adapter |
| `src/ConstructionFinance.Printing.Windows/WindowsPrinterPort.cs` | Vòng lặp `PrintPage` (~25 dòng) — **vùng mù duy nhất** |
| `src/ConstructionFinance.Printing.Windows/WindowsPrinterCatalog.cs` | `PrinterSettings.InstalledPrinters` + máy in mặc định |
| `src/ConstructionFinance.Desktop/ViewModels/Printing/ChonMayInViewModel.cs` | View-model hộp thoại chọn máy in |
| `src/ConstructionFinance.Desktop/Views/Printing/ChonMayInView.axaml(.cs)` | View của hộp thoại |
| `tests/ConstructionFinance.Application.Tests/Printing/LinuxFontResolver.cs` | **Chỉ dùng cho test.** PDFsharp 6 không đọc phông của hệ điều hành |
| `tests/ConstructionFinance.Application.Tests/Printing/PdfProbe.cs` | Render `Document` ra PDF rồi mở lại đọc số trang / khổ giấy |
| `tests/ConstructionFinance.Application.Tests/Printing/ExcelFitScaleTests.cs` | |
| `tests/ConstructionFinance.Application.Tests/Printing/MigraDocBuilderTests.cs` | |
| `tests/ConstructionFinance.Application.Tests/Printing/FormGridTests.cs` | |
| `tests/ConstructionFinance.Application.Tests/Printing/MigraDocFormSheetTests.cs` | |
| `tests/ConstructionFinance.Application.Tests/Printing/SheetTextMap.cs` | Đọc chữ theo (dòng, cột) từ `XLWorkbook` và từ `Document` để so hai đầu ra |
| `tests/ConstructionFinance.Desktop.Tests/Printing/ChonMayInViewModelTests.cs` | |
| `tests/ConstructionFinance.Desktop.Tests/Printing/PrintToPrinterCommandTests.cs` | |

**Sửa**

| Tệp | Sửa gì |
|---|---|
| `Directory.Packages.props` | Thêm ba `<PackageVersion>` |
| `ConstructionFinance.sln` | Thêm dự án `Printing.Windows` |
| `src/ConstructionFinance.Application/ConstructionFinance.Application.csproj` | Thêm `PDFsharp-MigraDoc` |
| `src/ConstructionFinance.Desktop/ConstructionFinance.Desktop.csproj` | Đa đích + tham chiếu dự án Windows **chỉ** ở đích `net8.0-windows` |
| `src/ConstructionFinance.Desktop/Common/HostBuilder.cs` | Đăng ký `IMigraDocBuilder`; `IPrinterPort`/`IPrinterCatalog` chỉ ở đích Windows |
| `src/ConstructionFinance.Desktop/Services/DialogService.cs` | Thêm `IHasPrimaryButtonText` để nút chính đọc "In" chứ không phải "Lưu" |
| `src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs` | Hai lệnh cũ đổi tên thành `Export*`; thêm hai lệnh `Print*` ra giấy |
| `src/ConstructionFinance.Desktop/Views/MainWindow.axaml` | Menu năm mục + `Placement="Top"` |
| `tests/ConstructionFinance.Desktop.Tests/MainWindowPrintCommandTests.cs` | Đổi tên lệnh theo mã (trình biên dịch bắt) |
| `tests/ConstructionFinance.Desktop.Tests/Common/FakeDialogService.cs` | Thêm `OnDialog` để test sửa được view-model trước khi hộp thoại "đóng" |

---

## Task 1: Khung build — đa đích, dự án Windows, ba gói

Task cổng. Không có dòng logic nào; chỉ chứng minh cấu trúc dựng được mà không giết bộ test. **Làm không xong thì DỪNG và báo, đừng đi tiếp.**

Đã kiểm trước khi viết plan (2026-10-07, trên Linux): đổi `Desktop` sang `TargetFrameworks=net8.0;net8.0-windows` cho ra **build sạch cả hai đích, 0 warning**, và `dotnet test` dự án Desktop.Tests vẫn **468/468 xanh** vì nó tự lấy đích `net8.0`. Phần chưa kiểm là `ProjectReference` có điều kiện và ba gói mới.

**Files:**
- Modify: `Directory.Packages.props`
- Create: `src/ConstructionFinance.Printing.Windows/ConstructionFinance.Printing.Windows.csproj`
- Create: `src/ConstructionFinance.Printing.Windows/PlaceholderMarker.cs`
- Modify: `ConstructionFinance.sln`
- Modify: `src/ConstructionFinance.Application/ConstructionFinance.Application.csproj`
- Modify: `src/ConstructionFinance.Desktop/ConstructionFinance.Desktop.csproj`

**Interfaces:**
- Consumes: không có.
- Produces: dự án `ConstructionFinance.Printing.Windows` nhắm `net8.0-windows`, tham chiếu được từ đích `net8.0-windows` của `Desktop`. `Application` dùng được namespace `MigraDoc.DocumentObjectModel`.

- [ ] **Step 1: Thêm ba gói vào `Directory.Packages.props`**

Chèn vào `<ItemGroup>`, ngay sau dòng `ClosedXML`:

```xml
    <!-- In: MigraDoc dàn trang; bản GDI + System.Drawing.Printing để ra giấy trên Windows -->
    <PackageVersion Include="PDFsharp-MigraDoc" Version="6.2.4" />
    <PackageVersion Include="PDFsharp-MigraDoc-GDI" Version="6.2.4" />
    <PackageVersion Include="System.Drawing.Common" Version="8.0.10" />
```

- [ ] **Step 2: Tạo dự án `Printing.Windows`**

`src/ConstructionFinance.Printing.Windows/ConstructionFinance.Printing.Windows.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <!-- Directory.Build.props đặt TargetFramework=net8.0 cho mọi dự án; dự án một đích chỉ
         cần GHI ĐÈ nó ở đây (Desktop mới phải xóa trắng, vì nó chuyển sang TargetFrameworks). -->
    <TargetFramework>net8.0-windows</TargetFramework>
    <!-- Để biên dịch (và kiểm kiểu) được dự án net8.0-windows trên Linux. Chạy thì vẫn phải Windows. -->
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <RootNamespace>ConstructionFinance.Printing.Windows</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\ConstructionFinance.Application\ConstructionFinance.Application.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="PDFsharp-MigraDoc-GDI" />
    <PackageReference Include="System.Drawing.Common" />
  </ItemGroup>

</Project>
```

`src/ConstructionFinance.Printing.Windows/PlaceholderMarker.cs` — dự án rỗng không build được thành assembly có ích; một kiểu rỗng để Task 1 chứng minh được đường tham chiếu, Task 9 sẽ xóa:

```csharp
namespace ConstructionFinance.Printing.Windows;

/// Tạm thời: chỗ dựa để Task 1 chứng minh đường tham chiếu dự án chạy được.
/// Task 10 thay bằng WindowsPrinterPort và XÓA tệp này.
internal static class PlaceholderMarker
{
    internal const string Note = "Task 9 thay bằng WindowsPrinterPort.";
}
```

- [ ] **Step 3: Thêm dự án vào solution**

```bash
dotnet sln ConstructionFinance.sln add src/ConstructionFinance.Printing.Windows/ConstructionFinance.Printing.Windows.csproj
```

- [ ] **Step 4: Cho `Application` thấy MigraDoc**

Trong `src/ConstructionFinance.Application/ConstructionFinance.Application.csproj`, thêm vào `<ItemGroup>` đang có `ClosedXML`:

```xml
    <PackageReference Include="PDFsharp-MigraDoc" />
```

- [ ] **Step 5: Cho `Desktop` đa đích**

Trong `src/ConstructionFinance.Desktop/ConstructionFinance.Desktop.csproj`, thay dòng `<OutputType>WinExe</OutputType>` bằng:

```xml
    <!-- Hai đích, một lý do: gói PDFsharp-MigraDoc-GDI chỉ có net8.0-windows7.0, nhưng
         468 test Desktop phải chạy được trên máy phát triển Linux. Đích net8.0 là đích
         bộ test lấy; đích net8.0-windows là bản phát hành, và CHỈ nó thấy dự án
         Printing.Windows. Directory.Build.props đặt TargetFramework toàn cục nên phải
         xóa trắng trước khi đặt TargetFrameworks. -->
    <TargetFramework></TargetFramework>
    <TargetFrameworks>net8.0;net8.0-windows</TargetFrameworks>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <OutputType>WinExe</OutputType>
```

Và thêm một `<ItemGroup>` mới ngay sau `<ItemGroup>` chứa các `ProjectReference` hiện có:

```xml
  <!-- Adapter máy in chỉ tồn tại ở đích Windows. Đích net8.0 (bộ test) không có
       IPrinterPort nào được đăng ký, nên hai lệnh In ra giấy mờ — xem Task 10. -->
  <ItemGroup Condition="'$(TargetFramework)' == 'net8.0-windows'">
    <ProjectReference Include="..\ConstructionFinance.Printing.Windows\ConstructionFinance.Printing.Windows.csproj" />
  </ItemGroup>
```

- [ ] **Step 6: Build cả hai đích**

Run: `dotnet build ConstructionFinance.sln --nologo`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`, và trong log có **hai** dòng `ConstructionFinance.Desktop -> ...` — một `bin/Debug/net8.0/`, một `bin/Debug/net8.0-windows/`.

- [ ] **Step 7: Cả bộ test vẫn chạy đủ**

Run: `dotnet test --nologo`
Expected: 5 dòng `Passed!`, tổng **817**, trong đó `ConstructionFinance.Desktop.Tests.dll (net8.0)` là **468**. Không dòng nào `(net8.0-windows)` — bộ test không được chạy ở đích Windows.

- [ ] **Step 8: Commit**

```bash
git add Directory.Packages.props ConstructionFinance.sln \
        src/ConstructionFinance.Printing.Windows \
        src/ConstructionFinance.Application/ConstructionFinance.Application.csproj \
        src/ConstructionFinance.Desktop/ConstructionFinance.Desktop.csproj
git commit -m "build(print): Desktop đa đích + dự án Printing.Windows cho adapter máy in

Gói PDFsharp-MigraDoc-GDI chỉ có net8.0-windows7.0, nhưng 468 test Desktop
phải chạy được trên máy phát triển Linux. Nên adapter nằm riêng một dự án
net8.0-windows, và Desktop có hai đích: net8.0 cho test, net8.0-windows cho
bản phát hành và chỉ nó thấy dự án đó.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 2: `ExcelFitScale` — số học co tỉ lệ

Hồ sơ in đặt `FitToPages(1, 0)` và Excel tự thu nhỏ cho vừa bề ngang. MigraDoc không có khái niệm đó, nên phải tự tính. Thuần hàm, không phụ thuộc gì — test rẻ và chắc.

**Files:**
- Create: `src/ConstructionFinance.Application/Printing/ExcelFitScale.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Printing/ExcelFitScaleTests.cs`

**Interfaces:**
- Consumes: `PrintOrientation` (có sẵn trong `PrintPageProfile.cs`).
- Produces: `ExcelFitScale.ContentWidthMm(IEnumerable<double>) -> double`, `ExcelFitScale.UsableWidthMm(PrintOrientation) -> double`, `ExcelFitScale.ScaleFor(IEnumerable<double>, PrintOrientation) -> double`, hằng `ExcelFitScale.SideMarginInches`.

- [ ] **Step 1: Viết test đỏ**

`tests/ConstructionFinance.Application.Tests/Printing/ExcelFitScaleTests.cs`:

```csharp
using ConstructionFinance.Application.Printing;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Printing;

/// Số học co tỉ lệ của bản in. Các con số trong lớp này tính tay theo đúng công thức của
/// Excel (xem spec §5) chứ không chép lại từ mã, nên nếu mã đổi công thức thì test đỏ.
public class ExcelFitScaleTests
{
    private const double Money = 16;

    /// 14 cột của Sổ Vị trí. px = 237*7 + 14*5 = 1729 → 1729*25,4/96 = 457,4646mm.
    private static readonly double[] ViTriColumns =
    {
        11, 38, 22, Money, Money, 11, Money, Money, Money, 11, Money, Money, Money, Money,
    };

    [Fact]
    public void Be_rong_noi_dung_cong_ca_phan_dem_nam_pixel_moi_cot()
    {
        ExcelFitScale.ContentWidthMm(ViTriColumns).ShouldBe(457.4646, 0.001);
    }

    /// Bỏ phần đệm 5px/cột ra 438,9mm — lệch 18,6mm, đủ để bản in tràn mép giấy.
    /// Test này tồn tại vì bản thiết kế đầu tiên đã bỏ sót đúng chỗ này.
    [Fact]
    public void Phan_dem_khong_duoc_bo_qua()
    {
        var withoutPadding = 237 * 7 * 25.4 / 96;
        ExcelFitScale.ContentWidthMm(ViTriColumns).ShouldBeGreaterThan(withoutPadding + 18);
    }

    [Theory]
    [InlineData(PrintOrientation.Landscape, 271.6)]   // 297 - 2*0,5"
    [InlineData(PrintOrientation.Portrait, 184.6)]    // 210 - 2*0,5"
    public void Be_ngang_giay_dung_duoc_tru_le_nua_inch_moi_ben(
        PrintOrientation orientation, double expected)
    {
        ExcelFitScale.UsableWidthMm(orientation).ShouldBe(expected, 0.001);
    }

    [Fact]
    public void So_Vi_tri_co_con_khoang_sau_muoi_phan_tram()
    {
        ExcelFitScale.ScaleFor(ViTriColumns, PrintOrientation.Landscape).ShouldBe(0.5937, 0.0001);
    }

    /// Quyết toán năm chỉ 4 cột nên HẸP HƠN giấy: tỉ lệ thô 1,4475. Chốt của spec §5 là
    /// chặn trên ở 1,0 — in đúng 13pt với dải trắng bên phải, không kéo giãn thành 18,8pt.
    [Fact]
    public void Noi_dung_hep_hon_giay_thi_khong_phong_to()
    {
        double[] quyetToanNam = { 12, 18, 18, 18 };
        ExcelFitScale.ScaleFor(quyetToanNam, PrintOrientation.Portrait).ShouldBe(1.0);
    }

    [Fact]
    public void Bang_khong_cot_thi_khong_co_gi_de_co()
    {
        ExcelFitScale.ScaleFor(Array.Empty<double>(), PrintOrientation.Portrait).ShouldBe(1.0);
    }
}
```

- [ ] **Step 2: Chạy để thấy đỏ**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter ExcelFitScaleTests --nologo`
Expected: FAIL — `The type or namespace name 'ExcelFitScale' could not be found`.

- [ ] **Step 3: Viết mã tối thiểu**

`src/ConstructionFinance.Application/Printing/ExcelFitScale.cs`:

```csharp
namespace ConstructionFinance.Application.Printing;

/// Làm bằng tay đúng việc Excel đang làm hộ: hồ sơ in đặt FitToPages(1 ngang, 0 dọc) và Excel
/// tự thu nhỏ cho vừa bề ngang giấy. MigraDoc khai bề rộng cột bằng mm và rộng quá là tràn,
/// nên bộ dựng MigraDoc phải tự tính tỉ lệ đó. Xem spec §5.
public static class ExcelFitScale
{
    /// Một đơn vị bề rộng cột của Excel = bề ngang chữ '0' của phông Normal (Calibri 11) ở 96dpi.
    private const double PixelsPerWidthUnit = 7.0;

    /// Phần đệm hai bên trong một ô, cộng MỘT LẦN cho MỖI cột — công thức đủ là px = w*7 + 5.
    /// Bỏ nó đi thì càng nhiều cột càng tính thiếu: Sổ Vị trí 14 cột thiếu 70px ≈ 18,5mm.
    private const double CellPaddingPixels = 5.0;

    private const double PixelsPerInch = 96.0;
    private const double MillimetersPerInch = 25.4;

    /// Lề trái/phải, đúng giá trị FormExcelExporter.ApplyPageDefaults đang đặt cho đường .xlsx.
    public const double SideMarginInches = 0.5;

    private const double A4ShortSideMm = 210.0;
    private const double A4LongSideMm = 297.0;

    /// Tổng bề ngang nội dung, tính bằng mm theo đúng công thức của Excel.
    public static double ContentWidthMm(IEnumerable<double> widthChars)
    {
        var pixels = 0.0;
        foreach (var width in widthChars)
            pixels += width * PixelsPerWidthUnit + CellPaddingPixels;
        return pixels * MillimetersPerInch / PixelsPerInch;
    }

    /// Bề ngang giấy còn dùng được: khổ A4 theo hướng giấy, trừ lề hai bên.
    public static double UsableWidthMm(PrintOrientation orientation)
    {
        var pageWidth = orientation == PrintOrientation.Landscape ? A4LongSideMm : A4ShortSideMm;
        return pageWidth - 2 * SideMarginInches * MillimetersPerInch;
    }

    /// Tỉ lệ áp cho CẢ bề rộng cột lẫn cỡ chữ. Chặn trên ở 1,0: nội dung hẹp hơn giấy thì in
    /// đúng cỡ gốc với dải trắng bên phải, chứ không kéo giãn — 13pt là cỡ của biểu mẫu gốc,
    /// phóng to lên là tự ý đổi hình thức văn bản hành chính (spec §5).
    public static double ScaleFor(IEnumerable<double> widthChars, PrintOrientation orientation)
    {
        var content = ContentWidthMm(widthChars);
        if (content <= 0) return 1.0;
        return Math.Min(1.0, UsableWidthMm(orientation) / content);
    }
}
```

- [ ] **Step 4: Chạy để thấy xanh**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter ExcelFitScaleTests --nologo`
Expected: `Passed: 7`.

- [ ] **Step 5: Ghim tỉ lệ thật của cả sáu hồ sơ**

Thêm vào cuối `ExcelFitScaleTests`. Test này nối số học với **hồ sơ thật của app**, nên đổi bề rộng cột ở bất kỳ màn nào cũng phải đi qua đây:

```csharp
    /// Tỉ lệ co thật của cả sáu tài liệu, đo lúc viết plan (2026-10-07). Không phải để khóa
    /// con số — bề rộng cột đổi thì số này đổi theo — mà để người sửa THẤY mình vừa làm bản
    /// in nhỏ đi bao nhiêu, ngay trong lần chạy test.
    [Theory]
    [InlineData("Sổ Vị trí", PrintOrientation.Landscape, 237, 14, 0.5937)]
    [InlineData("Tổng hợp", PrintOrientation.Landscape, 231, 13, 0.6103)]
    [InlineData("Quyết toán năm", PrintOrientation.Portrait, 66, 4, 1.0)]
    [InlineData("Thông tri", PrintOrientation.Portrait, 124, 8, 0.7684)]
    [InlineData("KS chi", PrintOrientation.Portrait, 102, 5, 0.9441)]
    [InlineData("GĐNTT", PrintOrientation.Landscape, 154, 9, 0.9141)]
    public void Ti_le_co_cua_sau_tai_lieu(
        string name, PrintOrientation orientation, double totalUnits, int columnCount, double expected)
    {
        // Dựng lại đúng tổng đơn vị và số cột: tỉ lệ chỉ phụ thuộc hai con số đó.
        var columns = new double[columnCount];
        columns[0] = totalUnits - (columnCount - 1);
        for (var i = 1; i < columnCount; i++) columns[i] = 1;

        ExcelFitScale.ScaleFor(columns, orientation).ShouldBe(expected, 0.0001, name);
    }
```

- [ ] **Step 6: Chạy lại**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter ExcelFitScaleTests --nologo`
Expected: `Passed: 13`.

- [ ] **Step 7: Commit**

```bash
git add src/ConstructionFinance.Application/Printing/ExcelFitScale.cs \
        tests/ConstructionFinance.Application.Tests/Printing/ExcelFitScaleTests.cs
git commit -m "feat(print): số học co tỉ lệ, cộng cả phần đệm 5px mỗi cột

MigraDoc khai bề rộng cột bằng mm nên phải tự làm việc FitToPages của Excel.
Công thức đủ là px = w*7 + 5; bỏ phần đệm thì Sổ Vị trí thiếu 18,5mm.

Tỉ lệ chặn trên ở 1,0: Quyết toán năm chỉ 4 cột nên hẹp hơn giấy, in đúng
13pt với dải trắng bên phải chứ không kéo giãn thành 18,8pt.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 3: `PrintMoneyText` + đòn kiểm chứng PDF trên Linux

Hai thứ hạ tầng mà mọi task sau dựa vào. `PrintMoneyText` vì `MoneyText` nằm ở `Desktop` còn bộ dựng nằm ở `Application`. Đòn PDF vì **PDFsharp 6 không đọc phông của hệ điều hành**: không gán `IFontResolver` thì nó ném ngay cả với phông nội bộ của chính nó (`'Courier New' cannot be resolved for predefined error font`) — đã đo trên máy này.

**Files:**
- Create: `src/ConstructionFinance.Application/Printing/PrintMoneyText.cs`
- Create: `tests/ConstructionFinance.Application.Tests/Printing/LinuxFontResolver.cs`
- Create: `tests/ConstructionFinance.Application.Tests/Printing/PdfProbe.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Printing/PdfProbeTests.cs`

**Interfaces:**
- Consumes: Task 1 (gói `PDFsharp-MigraDoc` trong `Application`).
- Produces: `PrintMoneyText.Format(decimal) -> string`; `PdfProbe.Render(Document) -> PdfProbe.Result` với `Result.PageCount` và `Result.Pages` (danh sách `(double WidthMm, double HeightMm)`); `PdfProbe.EnsureFonts()`.

- [ ] **Step 1: Viết test đỏ cho `PrintMoneyText`**

`tests/ConstructionFinance.Application.Tests/Printing/PrintMoneyTextTests.cs`:

```csharp
using ConstructionFinance.Application.Printing;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Printing;

/// Tiền trên bản in MigraDoc. Đường .xlsx ghi SỐ THẬT rồi để Excel tự định dạng theo máy
/// người dùng; MigraDoc chỉ nhận chuỗi, nên chuỗi phải do mình dựng đúng quy ước Việt.
public class PrintMoneyTextTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1")]
    [InlineData(999, "999")]
    [InlineData(1000, "1.000")]
    [InlineData(99_999_999_999L, "99.999.999.999")]
    [InlineData(-1_500_000L, "-1.500.000")]
    public void Dinh_dang_theo_quy_uoc_Viet_dau_cham_ngan(decimal value, string mong)
    {
        PrintMoneyText.Format(value).ShouldBe(mong);
    }

    /// Khác MoneyText của màn hình ĐÚNG MỘT CHỖ, có chủ ý: số 0. Màn hình và ô tiền của sheet
    /// bảng để TRỐNG (PrintCell.OfMoney(0) trả Empty), nhưng ba biểu mẫu gọi SetMoney(cell, 0)
    /// nên Excel in ra "0" — bản MigraDoc phải in "0" y hệt, nếu không hai bản in khác nhau.
    [Fact]
    public void So_khong_in_ra_chu_so_khong_chu_khong_phai_o_trong()
    {
        PrintMoneyText.Format(0m).ShouldBe("0");
    }
}
```

- [ ] **Step 2: Chạy để thấy đỏ**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter PrintMoneyTextTests --nologo`
Expected: FAIL — `'PrintMoneyText' could not be found`.

- [ ] **Step 3: Viết `PrintMoneyText`**

`src/ConstructionFinance.Application/Printing/PrintMoneyText.cs`:

```csharp
using System.Globalization;

namespace ConstructionFinance.Application.Printing;

/// Tiền thành chữ cho bản in MigraDoc. Đường .xlsx không cần lớp này: nó ghi số thật vào ô
/// rồi đặt định dạng "#,##0" để Excel tự hiển thị theo máy người dùng. MigraDoc chỉ nhận
/// chuỗi, nên quy ước phải chốt ở đây: dấu chấm phân nghìn, không phần thập phân.
/// Số 0 ra "0" (không phải ô trống) — đúng như Excel hiển thị ô SetMoney(cell, 0m) của ba
/// biểu mẫu. Ô tiền của sheet bảng không đi qua đây khi bằng 0: PrintCell.OfMoney(0) đã trả
/// PrintCell.Empty từ trước.
public static class PrintMoneyText
{
    private static readonly NumberFormatInfo VietnameseGrouping = new()
    {
        NumberGroupSeparator = ".",
        NumberDecimalDigits = 0,
        NumberGroupSizes = new[] { 3 },
        NegativeSign = "-",
    };

    public static string Format(decimal value) => value.ToString("N", VietnameseGrouping);
}
```

- [ ] **Step 4: Chạy để thấy xanh**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter PrintMoneyTextTests --nologo`
Expected: `Passed: 7`.

- [ ] **Step 5: Viết bộ tìm phông cho test**

`tests/ConstructionFinance.Application.Tests/Printing/LinuxFontResolver.cs`:

```csharp
using PdfSharp.Fonts;

namespace ConstructionFinance.Application.Tests.Printing;

/// CHỈ DÙNG CHO TEST. PDFsharp 6 không đọc phông của hệ điều hành: không gán IFontResolver
/// thì nó ném ngay khi dựng cả phông nội bộ của chính nó
/// ("The font 'Courier New' cannot be resolved for predefined error font"). Đã đo trên máy
/// phát triển Linux.
///
/// Bản phát hành KHÔNG cần lớp này: đường ra giấy dùng PDFsharp-MigraDoc-GDI, lấy phông qua
/// GDI+ của Windows, nên Times New Roman có sẵn.
///
/// Liberation Serif trùng khít metric với Times New Roman, nên bản PDF đo được bề rộng chữ
/// giống bản in trên Windows.
internal sealed class LinuxFontResolver : IFontResolver
{
    private static readonly string[] FontDirectories =
    {
        "/usr/share/fonts/truetype/liberation",
        "/usr/share/fonts/liberation",
        "/usr/share/fonts/truetype/liberation2",
    };

    private readonly Dictionary<string, byte[]> _cache = new(StringComparer.Ordinal);

    public byte[]? GetFont(string faceName)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(faceName, out var cached)) return cached;

            foreach (var directory in FontDirectories)
            {
                var path = Path.Combine(directory, faceName + ".ttf");
                if (!File.Exists(path)) continue;

                var bytes = File.ReadAllBytes(path);
                _cache[faceName] = bytes;
                return bytes;
            }
        }

        throw new FileNotFoundException(
            $"Không tìm thấy tệp phông cho \"{faceName}\". Cài gói phông Liberation " +
            $"(apt install fonts-liberation) rồi chạy lại test.");
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var monospaced = familyName.Contains("Courier", StringComparison.OrdinalIgnoreCase)
                      || familyName.Contains("Mono", StringComparison.OrdinalIgnoreCase);
        var family = monospaced ? "LiberationMono" : "LiberationSerif";
        var suffix = (isBold, isItalic) switch
        {
            (true, true) => "-BoldItalic",
            (true, false) => "-Bold",
            (false, true) => "-Italic",
            _ => "-Regular",
        };
        return new FontResolverInfo(family + suffix);
    }
}
```

- [ ] **Step 6: Viết `PdfProbe`**

`tests/ConstructionFinance.Application.Tests/Printing/PdfProbe.cs`:

```csharp
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using PdfSharp.Pdf.IO;

namespace ConstructionFinance.Application.Tests.Printing;

/// Render một MigraDoc Document ra PDF rồi MỞ LẠI tệp để đọc số trang và khổ từng trang.
/// Đây là đòn kiểm chứng dàn trang duy nhất chạy được trên Linux: bản in ra giấy và bản PDF
/// dùng CHUNG một bộ dàn trang của MigraDoc, nên số trang và hướng giấy soi được ở đây cũng
/// đúng trên giấy.
internal static class PdfProbe
{
    public sealed record Page(double WidthMm, double HeightMm)
    {
        public bool IsLandscape => WidthMm > HeightMm;
    }

    public sealed record Result(int PageCount, IReadOnlyList<Page> Pages);

    private static readonly object FontGate = new();
    private static bool _fontsReady;

    /// GlobalFontSettings.FontResolver là trạng thái TĨNH của cả tiến trình; xUnit chạy các
    /// lớp test song song, nên gán đúng một lần dưới khóa.
    public static void EnsureFonts()
    {
        lock (FontGate)
        {
            if (_fontsReady) return;
            GlobalFontSettings.FontResolver = new LinuxFontResolver();
            _fontsReady = true;
        }
    }

    public static Result Render(Document document)
    {
        EnsureFonts();

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();

        // Ghi ra tệp tạm rồi mở lại, chứ không đọc đối tượng còn trong bộ nhớ: tệp mở lại
        // được mới chứng minh bản in là PDF hợp lệ, không chỉ là một cây đối tượng đẹp.
        var path = Path.Combine(Path.GetTempPath(), $"cf-print-{Guid.NewGuid():N}.pdf");
        try
        {
            renderer.PdfDocument.Save(path);

            using var reopened = PdfReader.Open(path, PdfDocumentOpenMode.Import);
            var pages = new List<Page>();
            for (var i = 0; i < reopened.PageCount; i++)
                pages.Add(new Page(
                    Math.Round(reopened.Pages[i].Width.Millimeter, 1),
                    Math.Round(reopened.Pages[i].Height.Millimeter, 1)));

            return new Result(reopened.PageCount, pages);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
```

- [ ] **Step 7: Viết test chứng minh đòn kiểm chứng chạy**

`tests/ConstructionFinance.Application.Tests/Printing/PdfProbeTests.cs`:

```csharp
using MigraDoc.DocumentObjectModel;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Printing;

/// Bản thân đòn kiểm chứng cũng phải được kiểm: nếu PdfProbe im lặng trả sai thì mọi test
/// dàn trang sau nó đều vô nghĩa.
public class PdfProbeTests
{
    [Fact]
    public void Render_ra_PDF_mo_lai_duoc_va_doc_dung_kho_giay()
    {
        var document = new Document();
        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.Orientation = Orientation.Landscape;
        section.AddParagraph("Sổ Vị trí");

        var result = PdfProbe.Render(document);

        result.PageCount.ShouldBe(1);
        result.Pages[0].WidthMm.ShouldBe(297.0, 0.5);
        result.Pages[0].HeightMm.ShouldBe(210.0, 0.5);
        result.Pages[0].IsLandscape.ShouldBeTrue();
    }

    /// "In toàn bộ" đưa mọi sheet vào MỘT tài liệu, mỗi sheet một Section mang hướng giấy
    /// riêng. Test này ghim rằng MigraDoc đổi hướng giấy GIỮA các Section — nếu không,
    /// "In toàn bộ" phải chia thành nhiều lệnh in và cả thiết kế đổi.
    [Fact]
    public void Moi_Section_giu_huong_giay_rieng_trong_cung_mot_tai_lieu()
    {
        var document = new Document();
        foreach (var orientation in new[] { Orientation.Landscape, Orientation.Portrait, Orientation.Landscape })
        {
            var section = document.AddSection();
            section.PageSetup.PageFormat = PageFormat.A4;
            section.PageSetup.Orientation = orientation;
            section.AddParagraph("sheet");
        }

        var result = PdfProbe.Render(document);

        result.PageCount.ShouldBe(3);
        result.Pages.Select(page => page.IsLandscape).ShouldBe(new[] { true, false, true });
    }
}
```

- [ ] **Step 8: Chạy để thấy xanh**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter PdfProbeTests --nologo`
Expected: `Passed: 2`.

Hỏng với `'Courier New' cannot be resolved` nghĩa là `EnsureFonts()` không chạy trước khi render. Hỏng với `Không tìm thấy tệp phông` nghĩa là máy thiếu phông: `sudo apt install fonts-liberation`.

- [ ] **Step 9: Commit**

```bash
git add src/ConstructionFinance.Application/Printing/PrintMoneyText.cs \
        tests/ConstructionFinance.Application.Tests/Printing/
git commit -m "test(print): đòn kiểm chứng PDF chạy được trên Linux, và tiền thành chữ

PDFsharp 6 không đọc phông của hệ điều hành — không gán IFontResolver thì nó
ném cả với phông nội bộ của chính nó. LinuxFontResolver (chỉ dùng cho test)
trỏ sang Liberation Serif, trùng khít metric với Times New Roman.

PdfProbe render rồi MỞ LẠI tệp để đọc số trang và khổ giấy: bản in ra giấy và
bản PDF dùng chung một bộ dàn trang, nên soi được ở đây là soi được trên giấy.
Đã ghim rằng mỗi Section giữ hướng giấy riêng — điều kiện để In toàn bộ gộp
mọi sheet vào một lệnh in.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 4: `MigraDocBuilder` — ba sheet dạng bảng

Sheet dạng bảng là tiêu đề cột + nhiều dòng giống nhau, nên một `Table` của MigraDoc là xong. Task này cũng sở hữu bốn trong năm dòng **Review Focus** (3, 4, 5) vì chúng đều là đầu vào của bộ dựng bảng.

**Files:**
- Create: `src/ConstructionFinance.Application/Printing/MigraDocBuilder.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Printing/MigraDocBuilderTests.cs`

**Interfaces:**
- Consumes: `ExcelFitScale` (Task 2), `PrintMoneyText` (Task 3), `PdfProbe` (Task 3), và model có sẵn `PrintSheet`/`PrintTableSheet`/`PrintColumn`/`PrintRow`/`PrintCell`/`PrintShade`/`PrintFooterInfo`.
- Produces: `IMigraDocBuilder` với `Document Build(IReadOnlyList<PrintSheet> sheets, PrintFooterInfo footer)`; lớp `MigraDocBuilder`. Task 5–8 bổ sung nhánh biểu mẫu vào cùng phương thức này; Task 9–11 chỉ dùng `IMigraDocBuilder`.

- [ ] **Step 1: Viết test đỏ**

`tests/ConstructionFinance.Application.Tests/Printing/MigraDocBuilderTests.cs`:

```csharp
using ConstructionFinance.Application.Printing;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Printing;

/// Bộ dựng MigraDoc cho sheet dạng bảng. Kiểm trên CẤU TRÚC Document (rẻ, chính xác) và
/// thêm vài test render ra PDF cho những gì chỉ lộ ra khi dàn trang thật (số trang, hướng giấy).
public class MigraDocBuilderTests
{
    private static readonly PrintFooterInfo Footer = new("Dự án A", "Vị trí 1");

    private static PrintTableSheet Sheet(
        string name = "Sổ Vị trí",
        PrintPageProfile? page = null,
        IReadOnlyList<PrintColumn>? columns = null,
        IReadOnlyList<PrintRow>? rows = null) =>
        new(name,
            page ?? PrintPageProfiles.ViTriLedger,
            columns ?? new PrintColumn[]
            {
                new("Ngày", 11, PrintAlign.Center),
                new("Nội dung", 38, PrintAlign.Left),
                new("Giá trị HĐ", 16, PrintAlign.Right),
            },
            rows ?? new[]
            {
                new PrintRow(new[]
                {
                    PrintCell.OfDate(new DateOnly(2026, 10, 7)),
                    PrintCell.OfText("Gói thầu số 01"),
                    PrintCell.OfMoney(50_000_000_000m),
                }),
            });

    private static Table FirstTable(Document document) =>
        document.Sections[0]!.Elements.OfType<Table>().First();

    // ===== Khổ giấy và hướng giấy =====

    [Fact]
    public void Moi_sheet_mot_Section_rieng()
    {
        var document = new MigraDocBuilder().Build(
            new PrintSheet[] { Sheet("Sổ Vị trí"), Sheet("Quyết toán năm", PrintPageProfiles.YearSettlement) },
            Footer);

        document.Sections.Count.ShouldBe(2);
    }

    [Fact]
    public void Huong_giay_va_le_lay_theo_ho_so_cua_sheet()
    {
        var document = new MigraDocBuilder().Build(new PrintSheet[] { Sheet() }, Footer);

        var setup = document.Sections[0]!.PageSetup;
        setup.PageFormat.ShouldBe(PageFormat.A4);
        setup.Orientation.ShouldBe(Orientation.Landscape);
        setup.LeftMargin.Inch.ShouldBe(ExcelFitScale.SideMarginInches, 0.001);
        setup.RightMargin.Inch.ShouldBe(ExcelFitScale.SideMarginInches, 0.001);
    }

    // ===== Cột, chữ, kiểu =====

    [Fact]
    public void Be_rong_cot_co_theo_dung_ti_le_cua_ho_so()
    {
        var document = new MigraDocBuilder().Build(new PrintSheet[] { Sheet() }, Footer);
        var table = FirstTable(document);

        var scale = ExcelFitScale.ScaleFor(new double[] { 11, 38, 16 }, PrintOrientation.Landscape);
        table.Columns.Count.ShouldBe(3);
        table.Columns[0]!.Width.Millimeter.ShouldBe((11 * 7 + 5) * 25.4 / 96 * scale, 0.01);
    }

    [Fact]
    public void Co_chu_co_theo_cung_ti_le_ay()
    {
        var document = new MigraDocBuilder().Build(new PrintSheet[] { Sheet() }, Footer);

        var scale = ExcelFitScale.ScaleFor(new double[] { 11, 38, 16 }, PrintOrientation.Landscape);
        document.Sections[0]!.PageSetup.Orientation.ShouldBe(Orientation.Landscape);
        FirstTable(document).Format.Font.Size.Point.ShouldBe(13 * scale, 0.01);
    }

    [Fact]
    public void Dong_dau_la_tieu_de_cot_in_dam_va_duoc_lap_lai_moi_trang()
    {
        var document = new MigraDocBuilder().Build(new PrintSheet[] { Sheet() }, Footer);
        var table = FirstTable(document);
        var header = table.Rows[0]!;

        CellText(table, 0, 0).ShouldBe("Ngày");
        header.Format.Font.Bold.ShouldBeTrue();
        // HeadingFormat là cách MigraDoc lặp dòng này ở đầu MỖI trang — đúng việc
        // PageSetup.SetRowsToRepeatAtTop làm cho đường .xlsx. Ghim ở mức mô hình, vì PDFsharp
        // không trích được chữ từ PDF để đếm xem dòng đó có thật xuất hiện lại ở trang 2.
        header.HeadingFormat.ShouldBeTrue();
    }

    [Fact]
    public void Tien_in_theo_quy_uoc_Viet_khong_bao_gio_thanh_dau_thang()
    {
        var document = new MigraDocBuilder().Build(new PrintSheet[] { Sheet() }, Footer);

        CellText(FirstTable(document), 1, 2).ShouldBe("50.000.000.000");
    }

    [Fact]
    public void Ngay_in_theo_dinh_dang_ngay_thang_nam()
    {
        var document = new MigraDocBuilder().Build(new PrintSheet[] { Sheet() }, Footer);

        CellText(FirstTable(document), 1, 0).ShouldBe("07/10/2026");
    }

    [Fact]
    public void O_tien_bang_khong_de_trong()
    {
        var rows = new[] { new PrintRow(new[] { PrintCell.Empty, PrintCell.OfText("x"), PrintCell.OfMoney(0m) }) };
        var document = new MigraDocBuilder().Build(new PrintSheet[] { Sheet(rows: rows) }, Footer);

        CellText(FirstTable(document), 1, 2).ShouldBe("");
    }

    [Theory]
    [InlineData(PrintShade.Light, "#D9D9D9")]
    [InlineData(PrintShade.VeryLight, "#F2F2F2")]
    public void Hai_bac_xam_dung_bang_hai_bac_cua_duong_xlsx(PrintShade shade, string hex)
    {
        var rows = new[] { new PrintRow(new[] { PrintCell.OfText("x") }, Shade: shade) };
        var document = new MigraDocBuilder().Build(new PrintSheet[] { Sheet(rows: rows) }, Footer);

        FirstTable(document).Rows[1]!.Shading.Color.ShouldBe(Color.Parse(hex));
    }

    // ===== Review Focus =====

    /// Review Focus 4a: người dùng mở một Vị trí chưa gõ gì rồi bấm In.
    [Fact]
    public void Bang_khong_co_dong_nao_van_dung_duoc()
    {
        var document = new MigraDocBuilder().Build(
            new PrintSheet[] { Sheet(rows: Array.Empty<PrintRow>()) }, Footer);

        FirstTable(document).Rows.Count.ShouldBe(1);         // chỉ còn dòng tiêu đề
        PdfProbe.Render(document).PageCount.ShouldBe(1);
    }

    /// Review Focus 4b: Table không có cột mà AddRow() thì Cells[0] ném
    /// ArgumentOutOfRangeException. Bảng 0 cột phải thành một Section rỗng, không phải một lần sập.
    [Fact]
    public void Bang_khong_co_cot_nao_khong_lam_sap_bo_dung()
    {
        var sheet = Sheet(columns: Array.Empty<PrintColumn>(), rows: Array.Empty<PrintRow>());

        var document = Should.NotThrow(() => new MigraDocBuilder().Build(new PrintSheet[] { sheet }, Footer));

        document.Sections.Count.ShouldBe(1);
        document.Sections[0]!.Elements.OfType<Table>().ShouldBeEmpty();
    }

    /// Review Focus 3: chân trang mang ký tự lạ. Đường .xlsx phải viết "&&" và cắt ở 255 ký tự
    /// vì Excel coi & là mã điều khiển; MigraDoc không có luật đó, nhưng cũng không được ném.
    [Theory]
    [InlineData("Dự án A & B")]
    [InlineData("&P&N&D")]
    [InlineData("\ud83d")]                                  // nửa cặp surrogate, đứt đôi
    public void Chan_trang_mang_ky_tu_la_khong_lam_sap_bo_dung(string projectName)
    {
        var document = Should.NotThrow(() => new MigraDocBuilder()
            .Build(new PrintSheet[] { Sheet() }, new PrintFooterInfo(projectName, null)));

        document.Sections[0]!.Footers.Primary.ShouldNotBeNull();
    }

    [Fact]
    public void Ten_du_an_dai_ba_tram_ky_tu_khong_lam_sap_bo_dung()
    {
        var footer = new PrintFooterInfo(new string('A', 300), new string('B', 300));

        Should.NotThrow(() => new MigraDocBuilder().Build(new PrintSheet[] { Sheet() }, footer));
    }

    /// Review Focus 5: một ô chữ dài hơn cả trang sau khi co. MigraDoc phải xuống dòng và
    /// sang trang, không tràn ra ngoài giấy và không làm bộ phân trang quay vô hạn.
    [Fact]
    public void O_chu_dai_hon_ca_trang_thi_xuong_dong_chu_khong_treo()
    {
        var rows = new[]
        {
            new PrintRow(new[]
            {
                PrintCell.Empty,
                PrintCell.OfText(string.Join(" ", Enumerable.Repeat("khoản mục xây lắp", 60))),
                PrintCell.Empty,
            }),
        };

        var result = PdfProbe.Render(new MigraDocBuilder().Build(new PrintSheet[] { Sheet(rows: rows) }, Footer));

        result.PageCount.ShouldBeGreaterThanOrEqualTo(1);
        result.PageCount.ShouldBeLessThan(10);              // xuống dòng, không nổ số trang
        result.Pages[0].IsLandscape.ShouldBeTrue();
    }

    /// Dòng nhiều ô hơn số cột là lỗi của bên dựng PrintRow, và đường .xlsx đã ném
    /// ArgumentException có kèm tên sheet. Bản MigraDoc không được im lặng cắt bớt.
    [Fact]
    public void Dong_nhieu_o_hon_so_cot_thi_neu_ten_sheet_trong_loi()
    {
        var rows = new[] { new PrintRow(new[] { PrintCell.OfText("a"), PrintCell.OfText("b"), PrintCell.OfText("c"), PrintCell.OfText("d") }) };

        var ex = Should.Throw<ArgumentException>(() =>
            new MigraDocBuilder().Build(new PrintSheet[] { Sheet("Sổ Vị trí", rows: rows) }, Footer));

        ex.Message.ShouldContain("Sổ Vị trí");
    }

    // ===== Dàn trang thật =====

    [Fact]
    public void In_toan_bo_gom_moi_sheet_vao_mot_tai_lieu_moi_sheet_giu_huong_giay_rieng()
    {
        var document = new MigraDocBuilder().Build(
            new PrintSheet[]
            {
                Sheet("Sổ Vị trí", PrintPageProfiles.ViTriLedger),
                Sheet("Quyết toán năm", PrintPageProfiles.YearSettlement),
            },
            Footer);

        var result = PdfProbe.Render(document);

        result.Pages[0].IsLandscape.ShouldBeTrue();
        result.Pages[^1].IsLandscape.ShouldBeFalse();
    }

    [Fact]
    public void Bang_dai_sang_nhieu_trang()
    {
        var rows = Enumerable.Range(0, 200)
            .Select(i => new PrintRow(new[]
            {
                PrintCell.OfDate(new DateOnly(2026, 10, 7)),
                PrintCell.OfText($"Gói thầu số {i:00}"),
                PrintCell.OfMoney(1_000_000m),
            }))
            .ToArray();

        PdfProbe.Render(new MigraDocBuilder().Build(new PrintSheet[] { Sheet(rows: rows) }, Footer))
            .PageCount.ShouldBeGreaterThan(1);
    }

    private static string CellText(Table table, int row, int column)
    {
        var cell = table.Rows[row]!.Cells[column]!;
        var paragraph = cell.Elements.OfType<Paragraph>().FirstOrDefault();
        if (paragraph is null) return "";
        return string.Concat(paragraph.Elements.OfType<Text>().Select(text => text.Content));
    }
}
```

- [ ] **Step 2: Chạy để thấy đỏ**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter MigraDocBuilderTests --nologo`
Expected: FAIL — `'MigraDocBuilder' could not be found`.

- [ ] **Step 3: Viết `MigraDocBuilder`**

`src/ConstructionFinance.Application/Printing/MigraDocBuilder.cs`:

```csharp
using ConstructionFinance.Application.Forms;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace ConstructionFinance.Application.Printing;

public interface IMigraDocBuilder
{
    /// Dựng MỘT tài liệu cho cả danh sách sheet: "In toàn bộ" là một lệnh in, một hộp thoại.
    Document Build(IReadOnlyList<PrintSheet> sheets, PrintFooterInfo footer);
}

/// Biến các PrintSheet thành một MigraDoc Document đã dàn trang. Nơi DUY NHẤT của tính năng
/// in-ra-giấy chạm MigraDoc; tầng Desktop chỉ thấy IMigraDocBuilder và model thuần.
///
/// Song song với PrintWorkbookWriter (đường .xlsx) và cố ý giống nó về cấu trúc: cùng model
/// vào, cùng hai bậc xám, cùng định dạng ngày/tiền. Hai bộ dựng phải cho ra cùng NỘI DUNG —
/// SheetTextMapTests so từng ô để bắt lúc chúng trôi khỏi nhau.
public sealed class MigraDocBuilder : IMigraDocBuilder
{
    private const string DateFormat = "dd/MM/yyyy";

    /// Hai bậc xám, đúng hai giá trị PrintWorkbookWriter đang dùng cho đường .xlsx.
    private static readonly Color LightShadeColor = Color.Parse("#D9D9D9");
    private static readonly Color VeryLightShadeColor = Color.Parse("#F2F2F2");

    /// Nét kẻ lưới, quy ra point. Excel "Thin" ≈ 0,5pt.
    private const double GridBorderPoints = 0.5;

    /// Cạnh dưới dòng tiêu đề kẻ đậm hơn để tách phần đầu bảng, như đường .xlsx ("Medium").
    private const double HeaderBorderPoints = 1.0;

    /// Thụt một bậc bằng bao nhiêu. Excel thụt theo "ký tự"; 3 đơn vị bề rộng cho một bậc là
    /// xấp xỉ gần nhất ở cỡ chữ đã co.
    private const double IndentUnitsPerLevel = 3.0;

    public Document Build(IReadOnlyList<PrintSheet> sheets, PrintFooterInfo footer)
    {
        var document = new Document();

        var normal = document.Styles["Normal"]
            ?? throw new InvalidOperationException("MigraDoc không có style \"Normal\".");
        normal.Font.Name = FormExcelExporter.FontName;
        normal.Font.Size = Unit.FromPoint(FormExcelExporter.FontSize);

        foreach (var sheet in sheets)
        {
            var section = document.AddSection();
            ApplyPage(section, sheet.Page, footer);

            switch (sheet)
            {
                case PrintTableSheet table:
                    WriteTable(section, table);
                    break;
                default:
                    throw new NotSupportedException(
                        $"Sheet \"{sheet.SheetName}\": chưa có hàm vẽ MigraDoc cho {sheet.GetType().Name}.");
            }
        }

        return document;
    }

    /// Khổ giấy, hướng giấy, lề và chân trang. Lề trái/phải lấy đúng con số của đường .xlsx,
    /// vì ExcelFitScale tính tỉ lệ co dựa trên chính nó.
    private static void ApplyPage(Section section, PrintPageProfile page, PrintFooterInfo footer)
    {
        var setup = section.PageSetup;
        setup.PageFormat = PageFormat.A4;
        setup.Orientation = page.Orientation == PrintOrientation.Landscape
            ? Orientation.Landscape
            : Orientation.Portrait;
        setup.LeftMargin = Unit.FromInch(ExcelFitScale.SideMarginInches);
        setup.RightMargin = Unit.FromInch(ExcelFitScale.SideMarginInches);
        setup.TopMargin = Unit.FromInch(0.6);
        setup.BottomMargin = Unit.FromInch(0.6);

        // Chân trang: tên dự án (kèm tên Vị trí) bên trái, số trang bên phải. KHÔNG phải nhân
        // đôi dấu & hay cắt ở 255 ký tự như đường .xlsx — đó là luật riêng của Excel, MigraDoc
        // nhận chuỗi thuần. Chuỗi dài chỉ bị cắt khi vẽ, không làm vỡ tài liệu.
        var paragraph = section.Footers.Primary.AddParagraph();
        paragraph.AddText(FooterLeftText(footer));
        paragraph.AddTab();
        paragraph.AddText("Trang ");
        paragraph.AddPageField();
        paragraph.AddText("/");
        paragraph.AddNumPagesField();
    }

    private static string FooterLeftText(PrintFooterInfo footer) =>
        string.Join(" · ", new[] { footer.ProjectName, footer.ViTriName }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    private static void WriteTable(Section section, PrintTableSheet sheet)
    {
        // Bảng không có cột thì không có gì để vẽ: Table.AddRow() rồi Cells[0] sẽ ném. Để
        // Section rỗng — một trang trắng mang chân trang, chứ không phải một lần sập.
        if (sheet.Columns.Count == 0) return;

        var scale = ExcelFitScale.ScaleFor(sheet.Columns.Select(column => column.WidthChars), sheet.Page.Orientation);

        var table = section.AddTable();
        table.Format.Font.Size = Unit.FromPoint(FormExcelExporter.FontSize * scale);
        table.Borders.Width = Unit.FromPoint(GridBorderPoints);

        foreach (var column in sheet.Columns)
        {
            var added = table.AddColumn(Unit.FromMillimeter(ExcelFitScale.ContentWidthMm(new[] { column.WidthChars }) * scale));
            added.Format.Alignment = ToMigraDoc(column.Align);
        }

        WriteHeaderRow(table, sheet.Columns);

        for (var index = 0; index < sheet.Rows.Count; index++)
            WriteRow(table, sheet, index, scale);
    }

    private static void WriteHeaderRow(Table table, IReadOnlyList<PrintColumn> columns)
    {
        var row = table.AddRow();
        row.Format.Font.Bold = true;

        // HeadingFormat: MigraDoc lặp lại dòng này ở đầu MỖI trang. Đúng việc
        // PageSetup.SetRowsToRepeatAtTop làm cho đường .xlsx.
        row.HeadingFormat = true;
        row.Borders.Bottom.Width = Unit.FromPoint(HeaderBorderPoints);
        row.VerticalAlignment = VerticalAlignment.Center;

        for (var index = 0; index < columns.Count; index++)
        {
            var paragraph = row.Cells[index]!.AddParagraph(columns[index].Header);
            paragraph.Format.Alignment = ParagraphAlignment.Center;
        }
    }

    private static void WriteRow(Table table, PrintTableSheet sheet, int index, double scale)
    {
        var source = sheet.Rows[index];
        if (source.Cells.Count > sheet.Columns.Count)
            throw new ArgumentException(
                $"Sheet \"{sheet.SheetName}\": Rows[{index}] có {source.Cells.Count} ô nhưng bảng chỉ có {sheet.Columns.Count} cột.",
                nameof(sheet));

        var row = table.AddRow();
        if (source.IsBold) row.Format.Font.Bold = true;
        if (source.Shade != PrintShade.None) row.Shading.Color = ShadeColor(source.Shade);

        var indentAt = IndexOfFirstIndentableText(source.Cells, sheet.Columns);

        for (var column = 0; column < source.Cells.Count; column++)
        {
            var text = CellText(source.Cells[column]);
            if (text.Length == 0) continue;

            var paragraph = row.Cells[column]!.AddParagraph(text);
            paragraph.Format.Alignment = ToMigraDoc(sheet.Columns[column].Align);

            if (column == indentAt && source.IndentLevel > 0)
                paragraph.Format.FirstLineIndent =
                    Unit.FromMillimeter(ExcelFitScale.ContentWidthMm(new[] { IndentUnitsPerLevel * source.IndentLevel }) * scale);
        }
    }

    /// Chữ của một ô. Giữ ĐÚNG quy ước của đường .xlsx: ô trống không ghi gì, tiền theo
    /// PrintMoneyText, ngày theo dd/MM/yyyy, số không-phải-tiền không có dấu phân nghìn
    /// (nếu không năm 2026 in thành "2.026").
    private static string CellText(PrintCell cell) => cell.Kind switch
    {
        PrintCellKind.Text => cell.Text ?? "",
        PrintCellKind.Money => PrintMoneyText.Format(cell.Number!.Value),
        PrintCellKind.Number => cell.Number!.Value.ToString("0.################"),
        PrintCellKind.Date => cell.Date!.Value.ToString(DateFormat),
        _ => "",
    };

    /// Ô nhận bậc thụt: ô Text đầu tiên có chữ nằm ở cột căn trái. Cùng luật với
    /// PrintWorkbookWriter.IndexOfFirstIndentableText — hai bộ dựng phải thụt cùng một ô.
    private static int IndexOfFirstIndentableText(IReadOnlyList<PrintCell> cells, IReadOnlyList<PrintColumn> columns)
    {
        for (var index = 0; index < cells.Count; index++)
            if (columns[index].Align == PrintAlign.Left
                && cells[index].Kind == PrintCellKind.Text
                && !string.IsNullOrEmpty(cells[index].Text))
                return index;
        return -1;
    }

    private static Color ShadeColor(PrintShade shade) => shade switch
    {
        PrintShade.Light => LightShadeColor,
        PrintShade.VeryLight => VeryLightShadeColor,
        _ => throw new ArgumentOutOfRangeException(nameof(shade), shade, null),
    };

    private static ParagraphAlignment ToMigraDoc(PrintAlign align) => align switch
    {
        PrintAlign.Left => ParagraphAlignment.Left,
        PrintAlign.Center => ParagraphAlignment.Center,
        PrintAlign.Right => ParagraphAlignment.Right,
        _ => throw new ArgumentOutOfRangeException(nameof(align), align, null),
    };
}
```

- [ ] **Step 4: Chạy để thấy xanh**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter MigraDocBuilderTests --nologo`
Expected: tất cả xanh.

- [ ] **Step 5: Kiểm bằng mắt một lần, rồi bỏ**

Để người thực thi tự thấy bản in trước khi đi tiếp. Dựng một PDF thật vào thư mục tạm rồi mở xem:

```bash
cat > /tmp/xem-ban-in.md <<'EOF'
Chạy một test tạm có dòng `renderer.PdfDocument.Save("/tmp/so-vi-tri.pdf")`
(hoặc tạm bỏ dòng File.Delete trong PdfProbe), mở tệp, và tự trả lời:
 - Chữ có đọc được ở cỡ đã co (7,7pt) không?
 - Dòng tiêu đề có lặp ở trang 2 không?
 - Số tiền có nguyên vẹn, không bị cắt cụt không?
EOF
echo "đọc /tmp/xem-ban-in.md"
```

Thấy sai thì **dừng và báo** — đừng sửa bằng cách nới số trong test.

- [ ] **Step 6: Cả bộ test**

Run: `dotnet test --nologo`
Expected: 817 + số test mới, 0 hỏng.

- [ ] **Step 7: Commit**

```bash
git add src/ConstructionFinance.Application/Printing/MigraDocBuilder.cs \
        tests/ConstructionFinance.Application.Tests/Printing/MigraDocBuilderTests.cs
git commit -m "feat(print): bộ dựng MigraDoc cho ba sheet dạng bảng

Mỗi sheet một Section nên mỗi sheet giữ hướng giấy riêng trong cùng một lệnh
in. Bề rộng cột và cỡ chữ co theo cùng một tỉ lệ của ExcelFitScale.

Bốn đầu vào không ai nghĩ tới đều có test: bảng 0 dòng, bảng 0 cột (AddRow rồi
Cells[0] sẽ ném), chân trang mang & hoặc surrogate đứt đôi, và một ô chữ dài
hơn cả trang.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 5: `SheetTextMap` — so từng ô giữa hai đầu ra

Cách nghiệm thu chính của cả plan này, và nó phải có **trước** ba task biểu mẫu để ba task đó được dùng miễn phí. Ý tưởng: cùng một `PrintSheet[]` cho qua `PrintWorkbookWriter` (ra `XLWorkbook`) và qua `MigraDocBuilder` (ra `Document`), rồi khẳng định **mọi ô có chữ trong sheet Excel đều có đúng chữ đó, ở đúng dòng–cột, trong bảng MigraDoc**.

Nó bắt được chuyện về sau ai sửa một bộ vẽ mà quên bộ kia. Nó **không** bắt được bề rộng cột, độ đậm viền, mức nền, chữ bị cắt hay vị trí thật trên giấy — những thứ đó thuộc phần render.

**Files:**
- Create: `tests/ConstructionFinance.Application.Tests/Printing/SheetTextMap.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Printing/SheetTextMapTests.cs`

**Interfaces:**
- Consumes: `MigraDocBuilder` (Task 4), `PrintWorkbookWriter` (có sẵn).
- Produces: `SheetTextMap.FromExcel(IXLWorksheet) -> IReadOnlyDictionary<(int Row, int Column), string>`, `SheetTextMap.FromMigraDoc(Table) -> IReadOnlyDictionary<(int Row, int Column), string>`, `SheetTextMap.Normalize(string) -> string`, `SheetTextMap.AssertSameText(string sheetName, IXLWorksheet, Table)`. Task 7–9 dùng `AssertSameText`.

- [ ] **Step 1: Viết `SheetTextMap`**

`tests/ConstructionFinance.Application.Tests/Printing/SheetTextMap.cs`:

```csharp
using ClosedXML.Excel;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using Shouldly;

namespace ConstructionFinance.Application.Tests.Printing;

/// Đọc chữ theo (dòng, cột) từ cả hai đầu ra của tính năng In, để so chúng với nhau.
/// Cả hai bản đồ dùng toạ độ 1-based như ClosedXML, nên so trực tiếp được.
internal static class SheetTextMap
{
    /// Chuẩn hoá trước khi so. Ô tiền trong .xlsx là SỐ THẬT mang định dạng "#,##0", nên chữ
    /// đọc ra phụ thuộc văn hoá của máy chạy test ("50,000,000,000" hay "50.000.000.000"),
    /// còn MigraDoc nhận chuỗi đã dựng theo quy ước Việt. Bỏ dấu phân nghìn để so theo CHỮ SỐ.
    ///
    /// Nói thẳng cái mất đi: sau khi chuẩn hoá, test này không còn phân biệt được dấu chấm với
    /// dấu phẩy. Quy ước dấu chấm do PrintMoneyTextTests ghim riêng.
    public static string Normalize(string raw) =>
        raw.Replace(".", "").Replace(",", "").Replace(" ", " ").Trim();

    public static IReadOnlyDictionary<(int Row, int Column), string> FromExcel(IXLWorksheet worksheet)
    {
        var map = new Dictionary<(int, int), string>();
        foreach (var cell in worksheet.CellsUsed())
        {
            var text = Normalize(cell.GetFormattedString());
            if (text.Length == 0) continue;
            map[(cell.Address.RowNumber, cell.Address.ColumnNumber)] = text;
        }
        return map;
    }

    public static IReadOnlyDictionary<(int Row, int Column), string> FromMigraDoc(Table table)
    {
        var map = new Dictionary<(int, int), string>();
        for (var row = 0; row < table.Rows.Count; row++)
        for (var column = 0; column < table.Columns.Count; column++)
        {
            var text = Normalize(CellText(table, row, column));
            if (text.Length == 0) continue;
            map[(row + 1, column + 1)] = text;
        }
        return map;
    }

    /// Khẳng định: mọi ô có chữ trong .xlsx đều có ĐÚNG chữ đó, ở ĐÚNG dòng–cột, trong bảng
    /// MigraDoc. Một chiều, có chủ ý — bản MigraDoc được phép có thêm chữ (ví dụ ô tiêu đề mà
    /// Excel để bộ vẽ tự ghi), nhưng không được THIẾU hay LỆCH chỗ.
    public static void AssertSameText(string sheetName, IXLWorksheet worksheet, Table table)
    {
        var excel = FromExcel(worksheet);
        var migraDoc = FromMigraDoc(table);

        var mismatches = new List<string>();
        foreach (var ((row, column), text) in excel.OrderBy(entry => entry.Key))
        {
            if (!migraDoc.TryGetValue((row, column), out var found))
                mismatches.Add($"  ({row},{column}) .xlsx = \"{text}\" — MigraDoc KHÔNG CÓ ô này");
            else if (found != text)
                mismatches.Add($"  ({row},{column}) .xlsx = \"{text}\" — MigraDoc = \"{found}\"");
        }

        mismatches.ShouldBeEmpty(
            $"Sheet \"{sheetName}\": hai bộ dựng đã trôi khỏi nhau ở {mismatches.Count} ô:\n"
            + string.Join("\n", mismatches.Take(20)));
    }

    private static string CellText(Table table, int row, int column)
    {
        var cell = table.Rows[row]!.Cells[column]!;
        return string.Concat(cell.Elements.OfType<Paragraph>()
            .Select(paragraph => string.Concat(paragraph.Elements.OfType<Text>().Select(text => text.Content))));
    }
}
```

- [ ] **Step 2: Viết test cho chính `SheetTextMap`** (một đòn đo mà không ai đo lại thì không tin được)

`tests/ConstructionFinance.Application.Tests/Printing/SheetTextMapTests.cs`:

```csharp
using ClosedXML.Excel;
using ConstructionFinance.Application.Printing;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Printing;

/// Ba sheet dạng bảng: hai bộ dựng phải cho ra cùng nội dung. Và trước đó, bản thân phép so
/// phải PHÂN BIỆT ĐƯỢC — một phép so luôn xanh thì không bảo vệ gì.
public class SheetTextMapTests
{
    private static readonly PrintFooterInfo Footer = new("Dự án A", "Vị trí 1");

    private static PrintTableSheet Fixture() => new(
        "Sổ Vị trí",
        PrintPageProfiles.ViTriLedger,
        new PrintColumn[]
        {
            new("Ngày", 11, PrintAlign.Center),
            new("Nội dung", 38, PrintAlign.Left),
            new("Giá trị HĐ", 16, PrintAlign.Right),
        },
        new[]
        {
            new PrintRow(new[]
            {
                PrintCell.OfDate(new DateOnly(2026, 10, 7)),
                PrintCell.OfText("Gói thầu số 01"),
                PrintCell.OfMoney(50_000_000_000m),
            }),
            new PrintRow(new[]
            {
                PrintCell.Empty,
                PrintCell.OfText("TỔNG CỘNG"),
                PrintCell.OfMoney(50_000_000_000m),
            }, IsBold: true, Shade: PrintShade.Light),
        });

    private static IXLWorksheet ExcelSheet(PrintSheet sheet, out XLWorkbook workbook)
    {
        using var buffer = new MemoryStream();
        new PrintWorkbookWriter().Write(new[] { sheet }, buffer, Footer);
        buffer.Position = 0;
        workbook = new XLWorkbook(buffer);
        return workbook.Worksheets.First();
    }

    private static Table MigraDocTable(PrintSheet sheet) =>
        new MigraDocBuilder().Build(new[] { sheet }, Footer)
            .Sections[0]!.Elements.OfType<Table>().First();

    [Fact]
    public void Hai_bo_dung_cho_ra_cung_noi_dung_cho_so_Vi_tri()
    {
        var sheet = Fixture();
        var worksheet = ExcelSheet(sheet, out var workbook);
        using (workbook)
            SheetTextMap.AssertSameText(sheet.SheetName, worksheet, MigraDocTable(sheet));
    }

    /// Phép so có phân biệt được không: bỏ một ô khỏi bản MigraDoc thì nó PHẢI đỏ.
    [Fact]
    public void Phep_so_bat_duoc_khi_ban_MigraDoc_thieu_mot_o()
    {
        var sheet = Fixture();
        var worksheet = ExcelSheet(sheet, out var workbook);
        var table = MigraDocTable(sheet);

        // Xoá chữ ở ô (2,2) của bản MigraDoc — tức "Gói thầu số 01".
        table.Rows[1]!.Cells[1]!.Elements.Clear();

        using (workbook)
        {
            var ex = Should.Throw<Exception>(() =>
                SheetTextMap.AssertSameText(sheet.SheetName, worksheet, table));
            ex.Message.ShouldContain("Gói thầu số 01");
        }
    }

    /// Và bắt được cả khi chữ còn đó nhưng LỆCH CHỖ — lỗi hay gặp nhất khi chép tay bố cục.
    [Fact]
    public void Phep_so_bat_duoc_khi_chu_lech_sang_cot_khac()
    {
        var sheet = Fixture();
        var worksheet = ExcelSheet(sheet, out var workbook);
        var table = MigraDocTable(sheet);

        table.Rows[1]!.Cells[1]!.Elements.Clear();
        table.Rows[1]!.Cells[2]!.AddParagraph("Gói thầu số 01");

        using (workbook)
            Should.Throw<Exception>(() => SheetTextMap.AssertSameText(sheet.SheetName, worksheet, table));
    }

    [Fact]
    public void Chuan_hoa_bo_dau_phan_nghin_de_hai_van_hoa_so_sanh_duoc()
    {
        SheetTextMap.Normalize("50,000,000,000").ShouldBe(SheetTextMap.Normalize("50.000.000.000"));
    }
}
```

- [ ] **Step 3: Chạy**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter SheetTextMapTests --nologo`
Expected: `Passed: 4`.

`Hai_bo_dung_cho_ra_cung_noi_dung_cho_so_Vi_tri` đỏ nghĩa là Task 4 có chỗ lệch thật — đọc danh sách ô trong thông báo, sửa `MigraDocBuilder`, **đừng nới phép so**.

- [ ] **Step 4: Commit**

```bash
git add tests/ConstructionFinance.Application.Tests/Printing/SheetTextMap.cs \
        tests/ConstructionFinance.Application.Tests/Printing/SheetTextMapTests.cs
git commit -m "test(print): so từng ô giữa bản .xlsx và bản MigraDoc

Cùng một PrintSheet qua hai bộ dựng, rồi khẳng định mọi ô có chữ trong .xlsx
có đúng chữ đó ở đúng dòng-cột trong bảng MigraDoc. Bắt được lúc ai sửa một bộ
vẽ mà quên bộ kia.

Hai test tự kiểm phép so: bỏ một ô, và dời chữ sang cột khác, đều phải đỏ.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 6: `FormGrid` — lưới ô thưa cho ba biểu mẫu

Ba biểu mẫu không phải bảng: chúng là bản mẫu cứng, vẽ từng ô một với **26 lệnh gộp ô** trong 351 dòng `FormExcelExporter`. `FormGrid` cho phép chép bố cục đó **gần như từng dòng một** — toạ độ 1-based, tên lệnh song song với ClosedXML — nên ba task sau chỉ còn là việc dịch máy móc và người review đối chiếu được từng dòng.

**Một chỗ Excel và MigraDoc khác nhau về bản chất, và `FormGrid` phải bù.** Trong Excel, chữ ở ô không gộp **tràn sang các ô trống bên phải**: `ws.Cell(3,1).Value = "Ngày nhận giấy đề nghị thanh toán vốn đầu tư:"` (44 ký tự) nằm trong cột rộng 34 vẫn đọc hết được. Ô của MigraDoc thì **xuống dòng** trong lòng ô. Không bù thì cả ba biểu mẫu vỡ bố cục. Luật bù: **một ô có chữ, không gộp tường minh, mà chữ dài hơn cột của nó, thì tự gộp sang phải qua các ô trống liền kề** — đúng điều Excel làm bằng mắt.

**Files:**
- Create: `src/ConstructionFinance.Application/Printing/FormGrid.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Printing/FormGridTests.cs`

**Interfaces:**
- Consumes: `ExcelFitScale` (Task 2), `PrintMoneyText` (Task 3).
- Produces: `FormGrid` với hàm dựng `FormGrid(IReadOnlyList<double> columnWidthChars)` và các phương thức trả về chính nó — `Text(int row, int column, string? value)`, `Money(int row, int column, decimal value)`, `Bold(int fromRow, int fromColumn, int toRow, int toColumn)`, `Center(int fromRow, int fromColumn, int toRow, int toColumn)`, `Merge(int fromRow, int fromColumn, int toRow, int toColumn)`, `Indent(int row, int column, int level)`, `Border(int fromRow, int fromColumn, int toRow, int toColumn)` — cùng `Table Render(Section section, PrintOrientation orientation)`.

- [ ] **Step 1: Viết test đỏ**

`tests/ConstructionFinance.Application.Tests/Printing/FormGridTests.cs`:

```csharp
using ConstructionFinance.Application.Printing;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Printing;

/// Lưới ô thưa dùng để chép bố cục ba biểu mẫu từ ClosedXML sang MigraDoc.
public class FormGridTests
{
    private static Table Render(FormGrid grid, PrintOrientation orientation = PrintOrientation.Portrait)
    {
        var document = new Document();
        var section = document.AddSection();
        return grid.Render(section, orientation);
    }

    private static string CellText(Table table, int row, int column)
    {
        var cell = table.Rows[row - 1]!.Cells[column - 1]!;
        return string.Concat(cell.Elements.OfType<Paragraph>()
            .Select(p => string.Concat(p.Elements.OfType<Text>().Select(t => t.Content))));
    }

    [Fact]
    public void Toa_do_mot_based_nhu_ClosedXML()
    {
        var grid = new FormGrid(new double[] { 9, 9 }).Text(1, 1, "góc trên trái");

        CellText(Render(grid), 1, 1).ShouldBe("góc trên trái");
    }

    [Fact]
    public void Ghi_o_o_dong_xa_tu_dong_them_cac_dong_o_giua()
    {
        var grid = new FormGrid(new double[] { 9, 9 }).Text(5, 2, "x");
        var table = Render(grid);

        table.Rows.Count.ShouldBe(5);
        CellText(table, 5, 2).ShouldBe("x");
        CellText(table, 3, 1).ShouldBe("");
    }

    [Fact]
    public void Tien_dinh_dang_theo_quy_uoc_Viet()
    {
        var grid = new FormGrid(new double[] { 18 }).Money(1, 1, 1_500_000m);

        CellText(Render(grid), 1, 1).ShouldBe("1.500.000");
    }

    [Fact]
    public void Gop_o_tuong_minh_dung_bang_lenh_Merge_cua_Excel()
    {
        // ws.Range(4, 1, 4, 3).Merge() → một ô trải ba cột
        var grid = new FormGrid(new double[] { 9, 9, 9 }).Text(4, 1, "THÔNG TRI").Merge(4, 1, 4, 3);

        Render(grid).Rows[3]!.Cells[0]!.MergeRight.ShouldBe(2);
    }

    [Fact]
    public void Gop_o_doc_dung_bang_Merge_nhieu_dong()
    {
        // ws.Range(12, 1, 13, 1).Merge() → một ô cao hai dòng
        var grid = new FormGrid(new double[] { 9 }).Text(12, 1, "Loại ngân sách").Merge(12, 1, 13, 1);

        Render(grid).Rows[11]!.Cells[0]!.MergeDown.ShouldBe(1);
    }

    // ===== Luật bù tràn chữ =====

    /// Excel cho chữ tràn sang ô trống bên phải; MigraDoc xuống dòng trong lòng ô. Không bù
    /// thì cả ba biểu mẫu vỡ bố cục. 44 ký tự trong cột rộng 34 phải tự gộp sang phải.
    [Fact]
    public void Chu_dai_hon_cot_tu_gop_sang_cac_o_trong_ben_phai()
    {
        var grid = new FormGrid(new double[] { 34, 14, 18 })
            .Text(3, 1, "Ngày nhận giấy đề nghị thanh toán vốn đầu tư:");

        Render(grid).Rows[2]!.Cells[0]!.MergeRight.ShouldBe(2);
    }

    /// …nhưng dừng lại trước ô CÓ CHỮ: Excel cũng cắt chữ ở đúng chỗ đó.
    [Fact]
    public void Tu_gop_dung_truoc_o_da_co_chu()
    {
        var grid = new FormGrid(new double[] { 34, 14, 18 })
            .Text(3, 1, "Ngày nhận giấy đề nghị thanh toán vốn đầu tư:")
            .Text(3, 3, "Ngày ………");

        Render(grid).Rows[2]!.Cells[0]!.MergeRight.ShouldBe(1);
    }

    [Fact]
    public void Chu_vua_cot_thi_khong_tu_gop()
    {
        var grid = new FormGrid(new double[] { 34, 14 }).Text(1, 1, "Ghi chú:");

        Render(grid).Rows[0]!.Cells[0]!.MergeRight.ShouldBe(0);
    }

    [Fact]
    public void Gop_tuong_minh_thang_luat_tu_gop()
    {
        var grid = new FormGrid(new double[] { 9, 9, 9 })
            .Text(1, 1, "một dòng chữ rất dài vượt xa bề rộng chín đơn vị")
            .Merge(1, 1, 1, 2);

        Render(grid).Rows[0]!.Cells[0]!.MergeRight.ShouldBe(1);   // 1, không phải 2
    }

    // ===== Kiểu =====

    [Fact]
    public void Bold_dat_cho_ca_dai_o()
    {
        var grid = new FormGrid(new double[] { 9, 9 }).Text(1, 1, "a").Text(1, 2, "b").Bold(1, 1, 1, 2);
        var table = Render(grid);

        table.Rows[0]!.Cells[0]!.Format.Font.Bold.ShouldBeTrue();
        table.Rows[0]!.Cells[1]!.Format.Font.Bold.ShouldBeTrue();
    }

    [Fact]
    public void Border_ke_khung_ca_khoi()
    {
        var grid = new FormGrid(new double[] { 9, 9 }).Text(1, 1, "a").Border(1, 1, 2, 2);
        var table = Render(grid);

        table.Rows[0]!.Cells[0]!.Borders.Width.Point.ShouldBeGreaterThan(0);
        table.Rows[1]!.Cells[1]!.Borders.Width.Point.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Lenh_ghi_sau_thang_lenh_ghi_truoc_tren_cung_mot_o()
    {
        var grid = new FormGrid(new double[] { 9 }).Text(1, 1, "cũ").Text(1, 1, "mới");

        CellText(Render(grid), 1, 1).ShouldBe("mới");
    }

    [Fact]
    public void Luoi_khong_co_cot_nao_la_loi_cua_ben_goi()
    {
        Should.Throw<ArgumentException>(() => new FormGrid(Array.Empty<double>()));
    }

    [Fact]
    public void Ghi_ngoai_so_cot_la_loi_cua_ben_goi()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new FormGrid(new double[] { 9 }).Text(1, 2, "x"));
    }
}
```

- [ ] **Step 2: Chạy để thấy đỏ**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter FormGridTests --nologo`
Expected: FAIL — `'FormGrid' could not be found`.

- [ ] **Step 3: Viết `FormGrid`**

`src/ConstructionFinance.Application/Printing/FormGrid.cs`:

```csharp
using ConstructionFinance.Application.Forms;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace ConstructionFinance.Application.Printing;

/// Lưới ô thưa, toạ độ 1-based, dùng để chép bố cục ba biểu mẫu từ ClosedXML sang MigraDoc
/// gần như từng dòng một: Text/Money/Bold/Center/Merge/Indent/Border đứng song song với
/// ws.Cell(...).Value / Style.Font.Bold / Range(...).Merge() của bộ vẽ .xlsx. Người review
/// đối chiếu được từng dòng giữa hai bộ vẽ — đó là lý do lớp này tồn tại thay vì dựng Table
/// trực tiếp.
public sealed class FormGrid
{
    private const double GridBorderPoints = 0.5;

    private readonly IReadOnlyList<double> _columnWidthChars;
    private readonly Dictionary<(int Row, int Column), string> _text = new();
    private readonly HashSet<(int Row, int Column)> _bold = new();
    private readonly HashSet<(int Row, int Column)> _center = new();
    private readonly HashSet<(int Row, int Column)> _bordered = new();
    private readonly Dictionary<(int Row, int Column), int> _indent = new();
    private readonly Dictionary<(int Row, int Column), (int Rows, int Columns)> _merges = new();
    private int _rowCount;

    public FormGrid(IReadOnlyList<double> columnWidthChars)
    {
        if (columnWidthChars.Count == 0)
            throw new ArgumentException("Lưới biểu mẫu phải có ít nhất một cột.", nameof(columnWidthChars));
        _columnWidthChars = columnWidthChars;
    }

    public FormGrid Text(int row, int column, string? value)
    {
        Touch(row, column);
        _text[(row, column)] = value ?? "";
        return this;
    }

    public FormGrid Money(int row, int column, decimal value) =>
        Text(row, column, PrintMoneyText.Format(value));

    public FormGrid Bold(int fromRow, int fromColumn, int toRow, int toColumn) =>
        Mark(_bold, fromRow, fromColumn, toRow, toColumn);

    public FormGrid Center(int fromRow, int fromColumn, int toRow, int toColumn) =>
        Mark(_center, fromRow, fromColumn, toRow, toColumn);

    public FormGrid Border(int fromRow, int fromColumn, int toRow, int toColumn) =>
        Mark(_bordered, fromRow, fromColumn, toRow, toColumn);

    public FormGrid Indent(int row, int column, int level)
    {
        Touch(row, column);
        _indent[(row, column)] = level;
        return this;
    }

    /// Như ws.Range(fromRow, fromColumn, toRow, toColumn).Merge(). Gộp tường minh THẮNG luật
    /// tự gộp ở Render.
    public FormGrid Merge(int fromRow, int fromColumn, int toRow, int toColumn)
    {
        Touch(toRow, toColumn);
        Touch(fromRow, fromColumn);
        _merges[(fromRow, fromColumn)] = (toRow - fromRow, toColumn - fromColumn);
        return this;
    }

    public Table Render(Section section, PrintOrientation orientation)
    {
        var scale = ExcelFitScale.ScaleFor(_columnWidthChars, orientation);

        var table = section.AddTable();
        table.Format.Font.Size = Unit.FromPoint(FormExcelExporter.FontSize * scale);

        foreach (var width in _columnWidthChars)
            table.AddColumn(Unit.FromMillimeter(ExcelFitScale.ContentWidthMm(new[] { width }) * scale));

        for (var row = 0; row < _rowCount; row++) table.AddRow();

        for (var row = 1; row <= _rowCount; row++)
        for (var column = 1; column <= _columnWidthChars.Count; column++)
            FillCell(table, row, column, scale);

        return table;
    }

    private void FillCell(Table table, int row, int column, double scale)
    {
        var cell = table.Rows[row - 1]!.Cells[column - 1]!;

        if (_bold.Contains((row, column))) cell.Format.Font.Bold = true;
        if (_bordered.Contains((row, column))) cell.Borders.Width = Unit.FromPoint(GridBorderPoints);

        if (_merges.TryGetValue((row, column), out var span))
        {
            if (span.Rows > 0) cell.MergeDown = span.Rows;
            if (span.Columns > 0) cell.MergeRight = span.Columns;
        }

        if (!_text.TryGetValue((row, column), out var text) || text.Length == 0) return;

        // Luật bù tràn chữ: Excel cho chữ tràn sang ô TRỐNG bên phải, MigraDoc thì xuống dòng
        // trong lòng ô. Ô nào có chữ dài hơn cột của nó mà chưa gộp tường minh thì tự gộp sang
        // phải, dừng trước ô đã có chữ — đúng chỗ Excel cắt.
        if (!_merges.ContainsKey((row, column)) && text.Length > _columnWidthChars[column - 1])
        {
            var extra = 0;
            for (var next = column + 1; next <= _columnWidthChars.Count; next++)
            {
                if (_text.ContainsKey((row, next))) break;
                extra++;
            }
            if (extra > 0) cell.MergeRight = extra;
        }

        var paragraph = cell.AddParagraph(text);
        if (_center.Contains((row, column))) paragraph.Format.Alignment = ParagraphAlignment.Center;
        if (_indent.TryGetValue((row, column), out var level) && level > 0)
            paragraph.Format.FirstLineIndent =
                Unit.FromMillimeter(ExcelFitScale.ContentWidthMm(new[] { 3.0 * level }) * scale);
    }

    private FormGrid Mark(HashSet<(int Row, int Column)> target, int fromRow, int fromColumn, int toRow, int toColumn)
    {
        Touch(toRow, toColumn);
        for (var row = fromRow; row <= toRow; row++)
        for (var column = fromColumn; column <= toColumn; column++)
            target.Add((row, column));
        return this;
    }

    /// Ghi vào dòng xa thì các dòng ở giữa tự sinh ra, như ClosedXML. Cột thì không: số cột là
    /// cố định của biểu mẫu, ghi ra ngoài là lỗi của bên gọi và phải nổ ngay.
    private void Touch(int row, int column)
    {
        if (row < 1) throw new ArgumentOutOfRangeException(nameof(row), row, "Dòng tính từ 1.");
        if (column < 1 || column > _columnWidthChars.Count)
            throw new ArgumentOutOfRangeException(nameof(column), column,
                $"Lưới chỉ có {_columnWidthChars.Count} cột.");
        if (row > _rowCount) _rowCount = row;
    }
}
```

- [ ] **Step 4: Chạy để thấy xanh**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter FormGridTests --nologo`
Expected: `Passed: 14`.

- [ ] **Step 5: Cả bộ test**

Run: `dotnet test --nologo` — 0 hỏng.

- [ ] **Step 6: Commit**

```bash
git add src/ConstructionFinance.Application/Printing/FormGrid.cs \
        tests/ConstructionFinance.Application.Tests/Printing/FormGridTests.cs
git commit -m "feat(print): lưới ô thưa để chép bố cục ba biểu mẫu sang MigraDoc

Toạ độ 1-based và tên lệnh song song với ClosedXML, nên ba biểu mẫu chép được
gần như từng dòng một và người review đối chiếu được từng dòng.

Bù một chỗ Excel và MigraDoc khác nhau về bản chất: Excel cho chữ tràn sang ô
trống bên phải, MigraDoc xuống dòng trong lòng ô. Ô có chữ dài hơn cột mà chưa
gộp tường minh thì tự gộp sang phải, dừng trước ô đã có chữ.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 7: Biểu mẫu Thông tri

Chép bố cục `FormExcelExporter.WriteThongTriSheet` (dòng 30–119) sang `FormGrid`, **theo đúng thứ tự dòng của bản gốc** để người review đối chiếu được. Nghiệm thu bằng `SheetTextMap.AssertSameText`, không bằng mắt.

**Files:**
- Create: `src/ConstructionFinance.Application/Printing/MigraDocFormSheets.cs`
- Modify: `src/ConstructionFinance.Application/Printing/MigraDocBuilder.cs` (thêm nhánh `ThongTriPrintSheet`)
- Test: `tests/ConstructionFinance.Application.Tests/Printing/MigraDocFormSheetTests.cs`

**Interfaces:**
- Consumes: `FormGrid` (Task 6), `SheetTextMap` (Task 5), `FormExcelExporter.Blank` (internal, cùng assembly).
- Produces: `MigraDocFormSheets.WriteThongTri(Section section, ThongTriForm form) -> Table`. Task 8 và 9 thêm `WriteKsChi` và `WriteGdntt` vào cùng lớp này.

- [ ] **Step 1: Viết test đỏ**

`tests/ConstructionFinance.Application.Tests/Printing/MigraDocFormSheetTests.cs`:

```csharp
using ClosedXML.Excel;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Printing;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Printing;

/// Ba biểu mẫu dựng bằng MigraDoc. Nghiệm thu chính: so từng ô với bản .xlsx của cùng dữ liệu
/// (SheetTextMap) — chép sai một ô là đỏ, và thông báo nói rõ ô nào.
public class MigraDocFormSheetTests
{
    private static readonly PrintFooterInfo Footer = new("Dự án A", null);

    // ===== Thông tri =====

    private static ThongTriForm ThongTriFixture() => new(
        DonViCapTren: "SỞ XÂY DỰNG",
        DonViCoSo: "Ban QLDA số 1",
        SoThongTri: "42/TT",
        NgayGhiSo: "07/10/2026",
        TenCTMT: "Đường ven biển",
        MaSo: "DA-01",
        VeThangNam: "tháng 10 năm 2026",
        TaiKhoan1: "3741",
        DonVi1: "BQL",
        SoChungTu1: "CT-01",
        TaiKhoan2: "3742",
        DonVi2: "BQL",
        SoChungTu2: "CT-02",
        LoaiNganSach: "NSNN",
        Khoan: "012",
        Muc: "9050",
        TieuMuc: "9062",
        TietMuc: "01",
        Nganh: "280",
        Lines: new ThongTriLine[]
        {
            new(0, "Đường ven biển", 50_000_000_000m),
            new(1, "Gói thầu số 01", 30_000_000_000m),
            new(2, "Đợt 1", 30_000_000_000m),
        },
        Total: 50_000_000_000m,
        TotalInWords: "Năm mươi tỷ đồng");

    private static IXLWorksheet ExcelThongTri(ThongTriForm form, out XLWorkbook workbook)
    {
        var sheet = new ThongTriPrintSheet("Thông tri", PrintPageProfiles.ThongTri, form);
        using var buffer = new MemoryStream();
        new PrintWorkbookWriter().Write(new PrintSheet[] { sheet }, buffer, Footer);
        buffer.Position = 0;
        workbook = new XLWorkbook(buffer);
        return workbook.Worksheets.First();
    }

    private static Table MigraDocThongTri(ThongTriForm form)
    {
        var sheet = new ThongTriPrintSheet("Thông tri", PrintPageProfiles.ThongTri, form);
        return new MigraDocBuilder().Build(new PrintSheet[] { sheet }, Footer)
            .Sections[0]!.Elements.OfType<Table>().First();
    }

    [Fact]
    public void Thong_tri_khop_tung_o_voi_ban_xlsx()
    {
        var form = ThongTriFixture();
        var worksheet = ExcelThongTri(form, out var workbook);
        using (workbook)
            SheetTextMap.AssertSameText("Thông tri", worksheet, MigraDocThongTri(form));
    }

    [Fact]
    public void Thong_tri_co_tam_cot_dung_nhu_Mau()
    {
        MigraDocThongTri(ThongTriFixture()).Columns.Count.ShouldBe(8);
    }

    /// Đầu bảng gộp dọc hai dòng 12–13 như Mẫu.
    [Fact]
    public void Thong_tri_gop_doc_o_Loai_ngan_sach()
    {
        MigraDocThongTri(ThongTriFixture()).Rows[11]!.Cells[0]!.MergeDown.ShouldBe(1);
    }

    /// "Mục lục ngân sách" trải năm cột 2–6 trên đúng dòng 12.
    [Fact]
    public void Thong_tri_gop_ngang_o_Muc_luc_ngan_sach()
    {
        MigraDocThongTri(ThongTriFixture()).Rows[11]!.Cells[1]!.MergeRight.ShouldBe(4);
    }

    [Fact]
    public void Thong_tri_in_doc_tren_A4()
    {
        var sheet = new ThongTriPrintSheet("Thông tri", PrintPageProfiles.ThongTri, ThongTriFixture());
        var result = PdfProbe.Render(new MigraDocBuilder().Build(new PrintSheet[] { sheet }, Footer));

        result.Pages[0].IsLandscape.ShouldBeFalse();
        result.Pages[0].WidthMm.ShouldBe(210.0, 0.5);
    }

    /// Ô thủ tục bỏ trống in dấu chấm cho người dùng điền tay, đúng như đường .xlsx
    /// (FormExcelExporter.Blank). Bản MigraDoc không được in ô trống.
    [Fact]
    public void Thong_tri_o_thu_tuc_bo_trong_in_dau_cham()
    {
        var form = ThongTriFixture() with { SoThongTri = null, NgayGhiSo = "   " };
        var worksheet = ExcelThongTri(form, out var workbook);
        using (workbook)
            SheetTextMap.AssertSameText("Thông tri", worksheet, MigraDocThongTri(form));
    }

    /// Biểu mẫu không có dòng nội dung nào: builder vẫn phải dựng được (dòng "Cộng" ngay sau
    /// đầu bảng), chứ không ném vì row đếm sai.
    [Fact]
    public void Thong_tri_khong_co_dong_nao_van_dung_duoc()
    {
        var form = ThongTriFixture() with { Lines = Array.Empty<ThongTriLine>(), Total = 0m };
        var worksheet = ExcelThongTri(form, out var workbook);
        using (workbook)
            SheetTextMap.AssertSameText("Thông tri", worksheet, MigraDocThongTri(form));
    }
}
```

- [ ] **Step 2: Chạy để thấy đỏ**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter MigraDocFormSheetTests --nologo`
Expected: FAIL — `NotSupportedException: Sheet "Thông tri": chưa có hàm vẽ MigraDoc cho ThongTriPrintSheet.`

- [ ] **Step 3: Viết `MigraDocFormSheets.WriteThongTri`**

`src/ConstructionFinance.Application/Printing/MigraDocFormSheets.cs`:

```csharp
using ConstructionFinance.Application.Forms;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace ConstructionFinance.Application.Printing;

/// Ba biểu mẫu vẽ bằng MigraDoc. Mỗi hàm ở đây là bản chép của hàm cùng tên trong
/// FormExcelExporter, GIỮ NGUYÊN THỨ TỰ DÒNG của bản gốc để người review đối chiếu được từng
/// dòng giữa hai bộ vẽ. Đừng "dọn gọn" lại thứ tự — nó là tài sản của bản chép này.
///
/// MigraDocFormSheetTests so từng ô với bản .xlsx của cùng dữ liệu, nên hai bộ vẽ trôi khỏi
/// nhau là đỏ ngay.
internal static class MigraDocFormSheets
{
    /// Chép FormExcelExporter.WriteThongTriSheet. Bề rộng cột ở cuối bản gốc
    /// (Columns(1,6)=9, Column(7)=52, Column(8)=18) phải khai TRƯỚC ở đây, vì FormGrid cần
    /// biết số cột lúc dựng.
    public static Table WriteThongTri(Section section, ThongTriForm form)
    {
        var grid = new FormGrid(new double[] { 9, 9, 9, 9, 9, 9, 52, 18 });

        grid.Text(1, 1, form.DonViCapTren ?? "");
        grid.Text(1, 7, "Mẫu số CQ06-HD");
        grid.Text(2, 1, form.DonViCoSo ?? "");
        grid.Text(3, 1, $"Số      {FormExcelExporter.Blank(form.SoThongTri)}");
        grid.Text(3, 7, $"Ghi sổ ngày {FormExcelExporter.Blank(form.NgayGhiSo)}");

        grid.Text(4, 1, "THÔNG TRI");
        grid.Text(5, 1, "Thu hồi tạm ứng");
        grid.Text(6, 1, "Kinh phí: NSNN");
        foreach (var row in new[] { 4, 5, 6 })
        {
            grid.Merge(row, 1, row, 8);
            grid.Center(row, 1, row, 1);
        }
        grid.Bold(4, 1, 4, 1);

        grid.Text(8, 1, "Đơn vị:");
        grid.Text(8, 7, $" TK{FormExcelExporter.Blank(form.TaiKhoan1)}  Đơn vị{FormExcelExporter.Blank(form.DonVi1)}  Số CT{FormExcelExporter.Blank(form.SoChungTu1)}");
        grid.Text(9, 1, $"Về {FormExcelExporter.Blank(form.VeThangNam)}");
        grid.Text(9, 7, $" TK{FormExcelExporter.Blank(form.TaiKhoan2)}  Đơn vị{FormExcelExporter.Blank(form.DonVi2)}  Số CT{FormExcelExporter.Blank(form.SoChungTu2)}");
        grid.Text(10, 1, $"Tên CTMT, DA: {FormExcelExporter.Blank(form.TenCTMT)}    Mã số: {FormExcelExporter.Blank(form.MaSo)}");
        grid.Text(11, 7, "Đơn vị tính: Đồng");

        // Đầu bảng: hai dòng 12–13 gộp dọc, dòng 14 là dòng chữ cái A..G như Mẫu.
        grid.Text(12, 1, "Loại ngân sách");
        grid.Merge(12, 1, 13, 1);
        grid.Text(12, 2, "Mục lục ngân sách");
        grid.Merge(12, 2, 12, 6);
        grid.Text(13, 2, "Khoản");
        grid.Text(13, 3, "Mục");
        grid.Text(13, 4, "Tiểu mục");
        grid.Text(13, 5, "Tiết mục");
        grid.Text(13, 6, "Ngành");
        grid.Text(12, 7, "NỘI DUNG");
        grid.Merge(12, 7, 13, 7);
        grid.Text(12, 8, "SỐ TIỀN");
        grid.Merge(12, 8, 13, 8);
        grid.Bold(12, 1, 13, 8);
        grid.Center(12, 1, 13, 8);

        var letters = new[] { "A", "B", "C", "D", "E", "F", "G", "1" };
        for (var column = 0; column < letters.Length; column++)
        {
            grid.Text(14, column + 1, letters[column]);
            grid.Center(14, column + 1, 14, column + 1);
        }

        // Khối Mục lục ngân sách nằm trên dòng nội dung đầu tiên (dòng 15), như Mẫu.
        grid.Text(15, 1, form.LoaiNganSach ?? "");
        grid.Text(15, 2, form.Khoan ?? "");
        grid.Text(15, 3, form.Muc ?? "");
        grid.Text(15, 4, form.TieuMuc ?? "");
        grid.Text(15, 5, form.TietMuc ?? "");
        grid.Text(15, 6, form.Nganh ?? "");

        var currentRow = 15;
        foreach (var line in form.Lines)
        {
            grid.Text(currentRow, 7, line.Content);
            grid.Indent(currentRow, 7, line.Level);
            grid.Money(currentRow, 8, line.Amount);
            // Chỉ đậm hai ô Nội dung/Số tiền, không cả dòng: dòng 15 còn mang các giá trị
            // Mục lục ngân sách ở cột 1–6, phải để thường.
            if (line.Level == 0) grid.Bold(currentRow, 7, currentRow, 8);
            currentRow++;
        }

        grid.Text(currentRow, 7, "Cộng");
        grid.Bold(currentRow, 7, currentRow, 7);
        grid.Money(currentRow, 8, form.Total);
        grid.Bold(currentRow, 8, currentRow, 8);

        grid.Border(12, 1, currentRow, 8);

        currentRow++;
        grid.Text(currentRow, 1, $"Bằng chữ: {form.TotalInWords}");
        grid.Merge(currentRow, 1, currentRow, 8);

        return grid.Render(section, PrintOrientation.Portrait);
    }
}
```

- [ ] **Step 4: Nối vào `MigraDocBuilder`**

Trong `MigraDocBuilder.Build`, thêm nhánh trước `default:`:

```csharp
                case ThongTriPrintSheet thongTri:
                    MigraDocFormSheets.WriteThongTri(section, thongTri.Form);
                    break;
```

- [ ] **Step 5: Chạy để thấy xanh**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter MigraDocFormSheetTests --nologo`
Expected: `Passed: 7`.

`Thong_tri_khop_tung_o_voi_ban_xlsx` đỏ thì thông báo liệt kê đúng ô lệch, dạng `(dòng,cột) .xlsx = "..." — MigraDoc = "..."`. Sửa `WriteThongTri` cho khớp bản gốc; **đừng sửa phép so**.

- [ ] **Step 6: Commit**

```bash
git add src/ConstructionFinance.Application/Printing/MigraDocFormSheets.cs \
        src/ConstructionFinance.Application/Printing/MigraDocBuilder.cs \
        tests/ConstructionFinance.Application.Tests/Printing/MigraDocFormSheetTests.cs
git commit -m "feat(print): biểu mẫu Thông tri dựng bằng MigraDoc

Chép FormExcelExporter.WriteThongTriSheet sang FormGrid, giữ nguyên thứ tự dòng
của bản gốc để đối chiếu được từng dòng. Nghiệm thu bằng so từng ô với bản
.xlsx của cùng dữ liệu, kể cả khi ô thủ tục bỏ trống và khi không có dòng nào.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 8: Biểu mẫu KS chi

Chép `FormExcelExporter.WriteKsChiSheet` (`FormExcelExporter.KsChiGdntt.cs` dòng 22–84). Năm cột: 34, 14, 18, 18, 18. Giấy dọc.

**Files:**
- Modify: `src/ConstructionFinance.Application/Printing/MigraDocFormSheets.cs`
- Modify: `src/ConstructionFinance.Application/Printing/MigraDocBuilder.cs`
- Modify: `tests/ConstructionFinance.Application.Tests/Printing/MigraDocFormSheetTests.cs`

**Interfaces:**
- Consumes: `FormGrid` (Task 6), `SheetTextMap` (Task 5).
- Produces: `MigraDocFormSheets.WriteKsChi(Section section, KsChiForm form) -> Table`.

- [ ] **Step 1: Viết test đỏ** — thêm vào `MigraDocFormSheetTests`

```csharp
    // ===== KS chi =====

    private static KsChiForm KsChiFixture() => new(
        NgayNhanGiayDeNghi: "07/10/2026",
        MucTieuMucLines: new[] { "Mục 9050, tiểu mục 9062", "", "", "" },
        Accepted: new KsChiAmounts(50_000_000_000m, 30_000_000_000m, 20_000_000_000m),
        Recovered: new KsChiAmounts(10_000_000_000m, 6_000_000_000m, 4_000_000_000m),
        ThuHoiCacNamTruoc: 4_000_000_000m,
        ThuHoiNamNay: 6_000_000_000m,
        ThueGTGT: 5_000_000_000m,
        PaidToBeneficiary: new KsChiAmounts(40_000_000_000m, 24_000_000_000m, 16_000_000_000m),
        PaidInWords: "Bốn mươi tỷ đồng",
        SoTuChoi: 0m,
        LyDoTuChoi: null,
        GhiChu: "không");

    private static IXLWorksheet ExcelKsChi(KsChiForm form, out XLWorkbook workbook)
    {
        var sheet = new KsChiPrintSheet("KS chi", PrintPageProfiles.KsChi, form);
        using var buffer = new MemoryStream();
        new PrintWorkbookWriter().Write(new PrintSheet[] { sheet }, buffer, Footer);
        buffer.Position = 0;
        workbook = new XLWorkbook(buffer);
        return workbook.Worksheets.First();
    }

    private static Table MigraDocKsChi(KsChiForm form)
    {
        var sheet = new KsChiPrintSheet("KS chi", PrintPageProfiles.KsChi, form);
        return new MigraDocBuilder().Build(new PrintSheet[] { sheet }, Footer)
            .Sections[0]!.Elements.OfType<Table>().First();
    }

    [Fact]
    public void Ks_chi_khop_tung_o_voi_ban_xlsx()
    {
        var form = KsChiFixture();
        var worksheet = ExcelKsChi(form, out var workbook);
        using (workbook)
            SheetTextMap.AssertSameText("KS chi", worksheet, MigraDocKsChi(form));
    }

    [Fact]
    public void Ks_chi_co_nam_cot()
    {
        MigraDocKsChi(KsChiFixture()).Columns.Count.ShouldBe(5);
    }

    /// Bốn dòng "+ Mục …, tiểu mục …": dòng nào bỏ trống thì in mẫu chấm, không để trống.
    [Fact]
    public void Ks_chi_bon_dong_muc_tieu_muc_bo_trong_thi_in_mau_cham()
    {
        var form = KsChiFixture() with { MucTieuMucLines = new[] { "", "", "", "" } };
        var worksheet = ExcelKsChi(form, out var workbook);
        using (workbook)
            SheetTextMap.AssertSameText("KS chi", worksheet, MigraDocKsChi(form));
    }

    [Fact]
    public void Ks_chi_in_doc_tren_A4()
    {
        var sheet = new KsChiPrintSheet("KS chi", PrintPageProfiles.KsChi, KsChiFixture());
        PdfProbe.Render(new MigraDocBuilder().Build(new PrintSheet[] { sheet }, Footer))
            .Pages[0].IsLandscape.ShouldBeFalse();
    }
```

- [ ] **Step 2: Chạy để thấy đỏ**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter MigraDocFormSheetTests --nologo`
Expected: FAIL — `chưa có hàm vẽ MigraDoc cho KsChiPrintSheet`.

- [ ] **Step 3: Viết `WriteKsChi`** — thêm vào `MigraDocFormSheets`

```csharp
    /// Chép FormExcelExporter.WriteKsChiSheet. Bề rộng cột: 34, 14, 18, 18, 18.
    public static Table WriteKsChi(Section section, KsChiForm form)
    {
        var grid = new FormGrid(new double[] { 34, 14, 18, 18, 18 });

        grid.Text(1, 1, "PHẦN GHI CỦA CƠ QUAN KIỂM SOÁT, THANH TOÁN");
        grid.Merge(1, 1, 1, 5);
        grid.Bold(1, 1, 1, 1);
        grid.Center(1, 1, 1, 1);

        grid.Text(3, 1, "Ngày nhận giấy đề nghị thanh toán vốn đầu tư:");
        grid.Text(3, 4, $"Ngày {FormExcelExporter.Blank(form.NgayNhanGiayDeNghi)}");
        grid.Text(5, 5, "Đơn vị tính: đồng");

        grid.Text(6, 1, "Nội dung");
        grid.Merge(6, 1, 6, 2);
        grid.Text(6, 3, "Tổng số");
        grid.Text(6, 4, "Vốn trong nước");
        grid.Text(6, 5, "Vốn nước ngoài");
        grid.Bold(6, 1, 6, 5);
        grid.Center(6, 1, 6, 5);

        WriteKsChiAmountRow(grid, 7, "Số vốn chấp nhận", form.Accepted);

        // Bốn dòng "+ Mục ..., tiểu mục ..." — dòng nào bỏ trống thì in mẫu chấm.
        for (var index = 0; index < 4; index++)
        {
            var text = form.MucTieuMucLines[index];
            grid.Text(8 + index, 1, string.IsNullOrWhiteSpace(text)
                ? " + Mục ........, tiểu mục ........"
                : $" + {text}");
        }

        grid.Text(12, 1, "Trong đó:");
        WriteKsChiAmountRow(grid, 13, " + Số thu hồi tạm ứng", form.Recovered);
        grid.Text(14, 1, "  Các năm trước");
        grid.Money(14, 3, form.ThuHoiCacNamTruoc);
        grid.Text(15, 1, "  Năm nay");
        grid.Money(15, 3, form.ThuHoiNamNay);
        grid.Text(16, 1, " + Thuế giá trị gia tăng");
        grid.Money(16, 3, form.ThueGTGT);
        WriteKsChiAmountRow(grid, 17, " + Số trả đơn vị thụ hưởng", form.PaidToBeneficiary);
        grid.Bold(17, 1, 17, 5);

        grid.Text(18, 1, form.PaidInWords);
        grid.Merge(18, 1, 18, 5);

        grid.Text(19, 1, "Số từ chối:");
        grid.Money(19, 3, form.SoTuChoi);
        grid.Text(19, 4, "đồng");
        grid.Text(20, 1, $"Lý do từ chối: {form.LyDoTuChoi ?? ""}");
        grid.Merge(20, 1, 20, 5);

        grid.Text(22, 1, "Ghi chú:");
        grid.Text(22, 2, form.GhiChu);
        grid.Merge(22, 2, 22, 5);

        grid.Border(6, 1, 17, 5);

        return grid.Render(section, PrintOrientation.Portrait);
    }

    private static void WriteKsChiAmountRow(FormGrid grid, int row, string label, KsChiAmounts amounts)
    {
        grid.Text(row, 1, label);
        grid.Merge(row, 1, row, 2);
        grid.Money(row, 3, amounts.Total);
        grid.Money(row, 4, amounts.NSNN);
        grid.Money(row, 5, amounts.NgoaiHoi);
    }
```

- [ ] **Step 4: Nối vào `MigraDocBuilder`**

```csharp
                case KsChiPrintSheet ksChi:
                    MigraDocFormSheets.WriteKsChi(section, ksChi.Form);
                    break;
```

- [ ] **Step 5: Chạy để thấy xanh**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter MigraDocFormSheetTests --nologo`
Expected: `Passed: 11`.

- [ ] **Step 6: Commit**

```bash
git add src/ConstructionFinance.Application/Printing/ tests/ConstructionFinance.Application.Tests/Printing/
git commit -m "feat(print): biểu mẫu KS chi dựng bằng MigraDoc

Chép FormExcelExporter.WriteKsChiSheet sang FormGrid. So từng ô với bản .xlsx,
kể cả khi cả bốn dòng Mục/tiểu mục bỏ trống và phải in mẫu chấm.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 9: Biểu mẫu GĐNTT

Chép `FormExcelExporter.WriteGdnttSheet` (`FormExcelExporter.KsChiGdntt.cs` dòng 112–210). Chín cột: 26 rồi tám cột 16. Giấy **ngang**. Biểu mẫu rắc rối nhất: 20 lệnh gộp ô, trong đó ba cặp 4:5 / 6:7 / 8:9 lặp lại trên cả dòng gói thầu và dòng CỘNG.

**Files:**
- Modify: `src/ConstructionFinance.Application/Printing/MigraDocFormSheets.cs`
- Modify: `src/ConstructionFinance.Application/Printing/MigraDocBuilder.cs`
- Modify: `tests/ConstructionFinance.Application.Tests/Printing/MigraDocFormSheetTests.cs`

**Interfaces:**
- Consumes: `FormGrid` (Task 6), `SheetTextMap` (Task 5).
- Produces: `MigraDocFormSheets.WriteGdntt(Section section, GdnttForm form) -> Table`. Sau task này `MigraDocBuilder` biết vẽ cả sáu tài liệu — không còn nhánh `default` nào bị chạm trong đường dùng thật.

- [ ] **Step 1: Viết test đỏ** — thêm vào `MigraDocFormSheetTests`

```csharp
    // ===== GĐNTT =====

    private static GdnttForm GdnttFixture() => new(
        DonViCapTren: "SỞ XÂY DỰNG",
        DonViCoSo: "Ban QLDA số 1",
        So: "17",
        NgayThangNam: "07 tháng 10 năm 2026",
        KinhGui: "Kho bạc Nhà nước tỉnh",
        TenDuAnCongTrinh: "Đường ven biển",
        ChuDauTu: "Ban QLDA số 1",
        CanCuQuyetDinh: "123/QĐ-UBND",
        CanCuHopDong: "01/2026/HĐ-XL",
        CanCuGiayDeNghi: "05/GĐN",
        ThuocKeHoach: "năm 2026",
        PackageName: "Gói thầu số 01",
        ContractorName: "Công ty CP Xây dựng Thành Đạt",
        BankAccount: "0123456789",
        TaiNganHang: "Vietcombank chi nhánh tỉnh",
        UnrecoveredAdvanceBefore: 10_000_000_000m,
        ContractTotal: 50_000_000_000m,
        CumulativeBefore: 20_000_000_000m,
        RequestedThisPeriod: 15_000_000_000m,
        RequestedInWords: "Mười lăm tỷ đồng",
        RecoveredThisPeriod: 5_000_000_000m,
        ChuyenTienBaoHanh: 1_000_000_000m,
        ChuyenTienTamGiu: 500_000_000m,
        PaidToContractor: 8_500_000_000m,
        NguonVon: "NSNN");

    private static IXLWorksheet ExcelGdntt(GdnttForm form, out XLWorkbook workbook)
    {
        var sheet = new GdnttPrintSheet("GĐNTT", PrintPageProfiles.Gdntt, form);
        using var buffer = new MemoryStream();
        new PrintWorkbookWriter().Write(new PrintSheet[] { sheet }, buffer, Footer);
        buffer.Position = 0;
        workbook = new XLWorkbook(buffer);
        return workbook.Worksheets.First();
    }

    private static Table MigraDocGdntt(GdnttForm form)
    {
        var sheet = new GdnttPrintSheet("GĐNTT", PrintPageProfiles.Gdntt, form);
        return new MigraDocBuilder().Build(new PrintSheet[] { sheet }, Footer)
            .Sections[0]!.Elements.OfType<Table>().First();
    }

    [Fact]
    public void Gdntt_khop_tung_o_voi_ban_xlsx()
    {
        var form = GdnttFixture();
        var worksheet = ExcelGdntt(form, out var workbook);
        using (workbook)
            SheetTextMap.AssertSameText("GĐNTT", worksheet, MigraDocGdntt(form));
    }

    [Fact]
    public void Gdntt_co_chin_cot()
    {
        MigraDocGdntt(GdnttFixture()).Columns.Count.ShouldBe(9);
    }

    [Fact]
    public void Gdntt_in_ngang_tren_A4()
    {
        var sheet = new GdnttPrintSheet("GĐNTT", PrintPageProfiles.Gdntt, GdnttFixture());
        var result = PdfProbe.Render(new MigraDocBuilder().Build(new PrintSheet[] { sheet }, Footer));

        result.Pages[0].IsLandscape.ShouldBeTrue();
        result.Pages[0].WidthMm.ShouldBe(297.0, 0.5);
    }

    /// Dòng CỘNG (19) phải gộp 4:5 / 6:7 / 8:9 ĐÚNG NHƯ dòng gói thầu (18): trong cùng một
    /// khung kẻ, cột của hai dòng phải thẳng nhau, nếu không bản in lệch nhìn thấy rõ.
    [Theory]
    [InlineData(18)]
    [InlineData(19)]
    public void Gdntt_dong_goi_thau_va_dong_Cong_gop_cot_giong_nhau(int row)
    {
        var table = MigraDocGdntt(GdnttFixture());

        table.Rows[row - 1]!.Cells[0]!.MergeRight.ShouldBe(2);   // NỘI DUNG: 1:3
        table.Rows[row - 1]!.Cells[3]!.MergeRight.ShouldBe(1);   // 4:5
        table.Rows[row - 1]!.Cells[5]!.MergeRight.ShouldBe(1);   // 6:7
        table.Rows[row - 1]!.Cells[7]!.MergeRight.ShouldBe(1);   // 8:9
    }

    /// Sau task này bộ dựng MigraDoc biết cả sáu tài liệu. Cùng luật với
    /// PrintFormSheet_chi_co_ba_bieu_mau_ma_bo_ghi_biet_ve của đường .xlsx: thêm biểu mẫu mới
    /// (KH vốn / Vốn / QĐ…) mà quên hàm vẽ MigraDoc thì test này đỏ TRƯỚC khi người dùng bấm In.
    [Fact]
    public void Bo_dung_MigraDoc_biet_ve_dung_ba_bieu_mau()
    {
        var footer = new PrintFooterInfo("Dự án A", null);
        PrintSheet[] all =
        {
            new ThongTriPrintSheet("Thông tri", PrintPageProfiles.ThongTri, ThongTriFixture()),
            new KsChiPrintSheet("KS chi", PrintPageProfiles.KsChi, KsChiFixture()),
            new GdnttPrintSheet("GĐNTT", PrintPageProfiles.Gdntt, GdnttFixture()),
        };

        Should.NotThrow(() => new MigraDocBuilder().Build(all, footer));
    }
```

- [ ] **Step 2: Chạy để thấy đỏ**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter MigraDocFormSheetTests --nologo`
Expected: FAIL — `chưa có hàm vẽ MigraDoc cho GdnttPrintSheet`.

- [ ] **Step 3: Viết `WriteGdntt`** — thêm vào `MigraDocFormSheets`

```csharp
    /// Chép FormExcelExporter.WriteGdnttSheet. Chín cột: 26 rồi tám cột 16. Giấy NGANG.
    public static Table WriteGdntt(Section section, GdnttForm form)
    {
        var widths = new double[9];
        widths[0] = 26;
        for (var index = 1; index < widths.Length; index++) widths[index] = 16;
        var grid = new FormGrid(widths);

        grid.Text(1, 1, form.DonViCapTren ?? "");
        grid.Text(2, 1, form.DonViCoSo ?? "");
        grid.Text(3, 1, $"Số: {FormExcelExporter.Blank(form.So)}");
        grid.Text(1, 4, "CỘNG HOÀ XÃ HỘI CHỦ NGHĨA VIỆT NAM");
        grid.Text(2, 4, "Độc lập - Tự do - Hạnh phúc");
        grid.Text(3, 4, $"Ngày {FormExcelExporter.Blank(form.NgayThangNam)}");
        foreach (var row in new[] { 1, 2, 3 })
        {
            grid.Merge(row, 4, row, 9);
            grid.Center(row, 4, row, 4);
        }
        grid.Bold(1, 4, 1, 4);
        grid.Bold(2, 4, 2, 4);

        grid.Text(4, 1, "GIẤY ĐỀ NGHỊ THANH TOÁN VỐN ĐẦU TƯ");
        grid.Merge(4, 1, 4, 9);
        grid.Bold(4, 1, 4, 1);
        grid.Center(4, 1, 4, 1);

        grid.Text(6, 1, $"Kính gửi: {form.KinhGui ?? ""}");
        grid.Text(8, 1, $" - Tên dự án công trình: {form.TenDuAnCongTrinh ?? ""}");
        grid.Text(9, 1, $" - Chủ đầu tư/Ban QLDA: {form.ChuDauTu ?? ""}");
        grid.Text(10, 1, $" - Căn cứ Quyết định số: {form.CanCuQuyetDinh ?? ""}");
        grid.Text(11, 1, $" - Căn cứ Hợp đồng số: {form.CanCuHopDong ?? ""}");
        grid.Text(12, 1, $" - Căn cứ giấy đề nghị thanh toán vốn đầu tư số: {form.CanCuGiayDeNghi ?? ""}");
        grid.Text(13, 1, " - Số dư tạm ứng của hạng mục đề nghị thanh toán:");
        grid.Money(13, 4, form.UnrecoveredAdvanceBefore);
        grid.Text(14, 1, " - Số tiền đề nghị (tạm ứng, thanh toán KLHT) đến kỳ này:");
        grid.Text(15, 1, $" - Thuộc nguồn vốn: {form.NguonVon}");
        grid.Text(16, 1, $" - Thuộc kế hoạch: {form.ThuocKeHoach ?? ""}");
        foreach (var row in Enumerable.Range(6, 11)) grid.Merge(row, 1, row, 3);

        grid.Text(17, 1, "NỘI DUNG");
        grid.Merge(17, 1, 17, 3);
        grid.Text(17, 4, "Dự toán duyệt hoặc giá trị trúng thầu hoặc giá trị HĐ");
        grid.Merge(17, 4, 17, 5);
        grid.Text(17, 6, "Lũy kế số vốn đã T/Toán từ K/công đến cuối kỳ trước (Gồm cả tạm ứng)");
        grid.Merge(17, 6, 17, 7);
        grid.Text(17, 8, "Số đề nghị tạm ứng, thanh toán KLHT kỳ này (Gồm cả tạm ứng)");
        grid.Merge(17, 8, 17, 9);
        grid.Bold(17, 1, 17, 9);
        grid.Center(17, 1, 17, 9);

        grid.Text(18, 1, form.PackageName);
        grid.Merge(18, 1, 18, 3);
        WriteGdnttMoneyMerged(grid, 18, 4, 5, form.ContractTotal);
        WriteGdnttMoneyMerged(grid, 18, 6, 7, form.CumulativeBefore);
        WriteGdnttMoneyMerged(grid, 18, 8, 9, form.RequestedThisPeriod);

        grid.Text(19, 1, "CỘNG");
        grid.Merge(19, 1, 19, 3);
        // Cùng ba cặp gộp 4:5 / 6:7 / 8:9 như dòng 18 — trong cùng một khung kẻ, cột của
        // dòng CỘNG phải thẳng với dòng gói thầu bên trên.
        WriteGdnttMoneyMerged(grid, 19, 4, 5, form.ContractTotal);
        WriteGdnttMoneyMerged(grid, 19, 6, 7, form.CumulativeBefore);
        WriteGdnttMoneyMerged(grid, 19, 8, 9, form.RequestedThisPeriod);
        grid.Bold(19, 1, 19, 9);

        grid.Border(17, 1, 19, 9);

        grid.Text(20, 1, " - Tổng số tiền đề nghị tạm ứng, thanh toán bằng số:");
        grid.Money(20, 8, form.RequestedThisPeriod);
        grid.Text(21, 1, " - Bằng chữ:");
        grid.Text(21, 2, form.RequestedInWords);
        grid.Merge(21, 2, 21, 9);
        grid.Text(22, 1, "Trong đó:");
        grid.Text(23, 2, " - Thanh toán để thu hồi tạm ứng:");
        grid.Money(23, 8, form.RecoveredThisPeriod);
        grid.Text(24, 2, " - Chuyển tiền bảo hành:");
        grid.Money(24, 8, form.ChuyenTienBaoHanh);
        grid.Text(25, 2, " - Chuyển tiền tạm giữ chờ quyết toán:");
        grid.Money(25, 8, form.ChuyenTienTamGiu);
        grid.Text(26, 2, " - Số trả nhà thầu:");
        grid.Money(26, 8, form.PaidToContractor);
        grid.Bold(26, 1, 26, 9);

        grid.Text(27, 1, " - Tên đơn vị thụ hưởng:");
        grid.Text(27, 3, form.ContractorName ?? "");
        grid.Text(28, 1, " - Tài khoản:");
        grid.Text(28, 4, form.BankAccount ?? "");
        grid.Text(29, 1, " - Tại:");
        grid.Text(29, 2, form.TaiNganHang ?? "");
        grid.Merge(29, 2, 29, 9);

        grid.Text(31, 1, "Trưởng phòng Tài chính");
        grid.Text(31, 5, "Chủ đầu tư");
        grid.Merge(31, 1, 31, 3);
        grid.Merge(31, 5, 31, 9);
        grid.Center(31, 1, 31, 9);

        return grid.Render(section, PrintOrientation.Landscape);
    }

    /// Tiền ghi vào ô ĐẦU của một dải gộp — bảng tổng hợp GĐNTT gộp 4:5 / 6:7 / 8:9 trên cả
    /// dòng gói thầu và dòng CỘNG bên dưới.
    private static void WriteGdnttMoneyMerged(FormGrid grid, int row, int fromColumn, int toColumn, decimal value)
    {
        grid.Money(row, fromColumn, value);
        grid.Merge(row, fromColumn, row, toColumn);
    }
```

- [ ] **Step 4: Nối vào `MigraDocBuilder`**

```csharp
                case GdnttPrintSheet gdntt:
                    MigraDocFormSheets.WriteGdntt(section, gdntt.Form);
                    break;
```

- [ ] **Step 5: Chạy để thấy xanh**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter MigraDocFormSheetTests --nologo`
Expected: `Passed: 17`.

- [ ] **Step 6: Cả bộ test**

Run: `dotnet test --nologo` — 0 hỏng. Đến đây bộ dựng MigraDoc vẽ được **cả sáu tài liệu**.

- [ ] **Step 7: Commit**

```bash
git add src/ConstructionFinance.Application/Printing/ tests/ConstructionFinance.Application.Tests/Printing/
git commit -m "feat(print): biểu mẫu GĐNTT dựng bằng MigraDoc — đủ cả sáu tài liệu

Biểu mẫu rắc rối nhất: 20 lệnh gộp ô, trong đó ba cặp 4:5 / 6:7 / 8:9 lặp trên
cả dòng gói thầu và dòng CỘNG. Có test riêng ghim rằng hai dòng đó gộp giống
nhau, vì trong cùng một khung kẻ chúng lệch là nhìn thấy ngay.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 10: `IPrinterPort` + adapter Windows

Hai interface ở `Application` và hai bản cài đặt ở `Printing.Windows`. Vòng lặp `PrintPage` là **vùng mù duy nhất** của cả plan: nó chỉ được **biên dịch** trên Linux, chạy thì phải Windows. Thiết kế cố ý ép nó nhỏ và không có logic nội dung nào.

Đã kiểm trước khi viết plan: đoạn mã ở Step 3 **biên dịch 0 lỗi** trong một dự án `net8.0-windows` trên Linux.

**Files:**
- Create: `src/ConstructionFinance.Application/Printing/IPrinterPort.cs`
- Create: `src/ConstructionFinance.Printing.Windows/WindowsPrinterCatalog.cs`
- Create: `src/ConstructionFinance.Printing.Windows/WindowsPrinterPort.cs`
- Delete: `src/ConstructionFinance.Printing.Windows/PlaceholderMarker.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Printing/PrinterJobTests.cs`

**Interfaces:**
- Consumes: `MigraDocBuilder` (Task 4–9), Task 1 (dự án `Printing.Windows`).
- Produces: `IPrinterCatalog` (`IReadOnlyList<string> InstalledPrinters { get; }`, `string? DefaultPrinterName { get; }`), `IPrinterPort` (`Task PrintAsync(PrinterJob job, CancellationToken ct = default)`), `PrinterJob(Document Document, string PrinterName, int Copies)` với `PrinterJob.MinCopies`/`MaxCopies` và hàm dựng tự kẹp số bản, `PrintJobFailedException` mang `PrinterName`. Task 11 và 12 chỉ dùng những kiểu này.

- [ ] **Step 1: Viết test đỏ**

`tests/ConstructionFinance.Application.Tests/Printing/PrinterJobTests.cs`:

```csharp
using ConstructionFinance.Application.Printing;
using MigraDoc.DocumentObjectModel;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Printing;

/// Lệnh in: số bản phải được kẹp TRƯỚC khi chạm System.Drawing.
public class PrinterJobTests
{
    private static Document AnyDocument()
    {
        var document = new Document();
        document.AddSection().AddParagraph("x");
        return document;
    }

    /// Review Focus 1: PrinterSettings.Copies là short. (short)100000 TRÀN ÂM, và một số bản
    /// âm thì trình điều khiển có thể in ngược hoặc ném. Kẹp ở đây, không ở lớp giao diện —
    /// lớp giao diện có thể bị người khác viết lại, còn lệnh in thì không ai đi vòng qua được.
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(99, 99)]
    [InlineData(100, 99)]
    [InlineData(100_000, 99)]
    [InlineData(int.MaxValue, 99)]
    [InlineData(int.MinValue, 1)]
    public void So_ban_bi_kep_trong_khoang_mot_den_chin_muoi_chin(int requested, int expected)
    {
        new PrinterJob(AnyDocument(), "Máy in A", requested).Copies.ShouldBe(expected);
    }

    [Fact]
    public void So_ban_sau_khi_kep_luon_vua_kieu_short()
    {
        var job = new PrinterJob(AnyDocument(), "Máy in A", int.MaxValue);

        job.Copies.ShouldBeLessThanOrEqualTo(short.MaxValue);
        job.Copies.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Ten_may_in_trong_la_loi_cua_ben_goi()
    {
        Should.Throw<ArgumentException>(() => new PrinterJob(AnyDocument(), "   ", 1));
    }

    [Fact]
    public void Loi_may_in_mang_theo_ten_may_in_de_cau_thong_bao_noi_dung_su_that()
    {
        var ex = new PrintJobFailedException("Máy in A", new InvalidOperationException("hết giấy"));

        ex.PrinterName.ShouldBe("Máy in A");
        ex.Message.ShouldContain("Máy in A");
        ex.Message.ShouldContain("hết giấy");
    }
}
```

- [ ] **Step 2: Chạy để thấy đỏ**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter PrinterJobTests --nologo`
Expected: FAIL — `'PrinterJob' could not be found`.

- [ ] **Step 3: Viết hai interface ở `Application`**

`src/ConstructionFinance.Application/Printing/IPrinterPort.cs`:

```csharp
using MigraDoc.DocumentObjectModel;

namespace ConstructionFinance.Application.Printing;

/// Danh sách máy in của máy tính. Tách ra interface để hộp thoại chọn máy in test được trên
/// máy không có máy in nào.
public interface IPrinterCatalog
{
    IReadOnlyList<string> InstalledPrinters { get; }

    /// Máy in mặc định của hệ điều hành, hoặc null nếu máy chưa cài máy in nào.
    string? DefaultPrinterName { get; }
}

/// Một lệnh in. Số bản được KẸP ngay trong hàm dựng, không để lớp giao diện tự lo:
/// PrinterSettings.Copies là short, nên một con số to sẽ tràn âm trước khi tới trình điều khiển.
public sealed record PrinterJob
{
    public const int MinCopies = 1;
    public const int MaxCopies = 99;

    public PrinterJob(Document document, string printerName, int copies)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            throw new ArgumentException("Lệnh in phải có tên máy in.", nameof(printerName));

        Document = document;
        PrinterName = printerName;
        Copies = Math.Clamp(copies, MinCopies, MaxCopies);
    }

    public Document Document { get; }
    public string PrinterName { get; }

    /// Luôn nằm trong [MinCopies, MaxCopies].
    public int Copies { get; }
}

/// Máy in báo lỗi giữa lúc in. Mang tên máy in theo, để câu thông báo nói đúng sự thật về
/// việc người dùng vừa làm thay vì một câu chung chung.
public sealed class PrintJobFailedException : Exception
{
    public PrintJobFailedException(string printerName, Exception innerException)
        : base($"Máy in \"{printerName}\" báo lỗi: {innerException.Message}", innerException)
    {
        PrinterName = printerName;
    }

    public string PrinterName { get; }
}

/// Đưa một tài liệu MigraDoc ra giấy. Bản cài đặt duy nhất nằm ở
/// ConstructionFinance.Printing.Windows và chỉ có ở đích net8.0-windows; ở đích net8.0 (bộ
/// test) KHÔNG có bản nào được đăng ký, nên hai lệnh In ra giấy mờ đi.
public interface IPrinterPort
{
    /// Ném PrintJobFailedException khi máy in hoặc trình điều khiển báo lỗi.
    Task PrintAsync(PrinterJob job, CancellationToken ct = default);
}
```

- [ ] **Step 4: Chạy để thấy xanh**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter PrinterJobTests --nologo`
Expected: `Passed: 11`.

- [ ] **Step 5: Viết adapter Windows**

`src/ConstructionFinance.Printing.Windows/WindowsPrinterCatalog.cs`:

```csharp
using System.Drawing.Printing;
using ConstructionFinance.Application.Printing;

namespace ConstructionFinance.Printing.Windows;

/// Danh sách máy in lấy từ System.Drawing.Printing. CHỈ CHẠY TRÊN WINDOWS — trên Linux các
/// thành viên này ném PlatformNotSupportedException, nên lớp này không có test chạy; nó chỉ
/// được biên dịch.
public sealed class WindowsPrinterCatalog : IPrinterCatalog
{
    public IReadOnlyList<string> InstalledPrinters
    {
        get
        {
            var names = new List<string>();
            foreach (string? name in PrinterSettings.InstalledPrinters)
                if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
            return names;
        }
    }

    /// PrinterSettings mới mang tên máy in mặc định của hệ điều hành; chuỗi rỗng nghĩa là máy
    /// chưa cài máy in nào.
    public string? DefaultPrinterName
    {
        get
        {
            var name = new PrinterSettings().PrinterName;
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
    }
}
```

`src/ConstructionFinance.Printing.Windows/WindowsPrinterPort.cs`:

```csharp
using System.Drawing.Printing;
using ConstructionFinance.Application.Printing;
using MigraDoc.Rendering;
using PdfSharp.Drawing;

namespace ConstructionFinance.Printing.Windows;

/// VÙNG MÙ DUY NHẤT của tính năng in. Lớp này chỉ được BIÊN DỊCH trên máy phát triển Linux;
/// chạy thì phải Windows có máy in thật, và nghiệm thu cuối cùng là người dùng in thử.
///
/// Vì thế nó cố tình KHÔNG có logic nội dung nào: mọi quyết định về in cái gì, nằm ở đâu, cỡ
/// bao nhiêu đều nằm trong MigraDocBuilder và ExcelFitScale — hai lớp kiểm được trên Linux.
/// Ở đây chỉ còn: mỗi lần hệ điều hành đòi một trang thì vẽ trang đó.
///
/// Đừng thêm việc gì vào lớp này. Thêm vào đây là chuyển việc từ vùng kiểm được sang vùng mù.
public sealed class WindowsPrinterPort : IPrinterPort
{
    public Task PrintAsync(PrinterJob job, CancellationToken ct = default) =>
        Task.Run(() => Print(job), ct);

    private static void Print(PrinterJob job)
    {
        var renderer = new DocumentRenderer(job.Document);
        renderer.PrepareDocument();

        var formatted = renderer.FormattedDocument
            ?? throw new InvalidOperationException("MigraDoc chưa dàn trang xong tài liệu.");

        var page = 0;
        using var printDocument = new PrintDocument();
        printDocument.PrinterSettings.PrinterName = job.PrinterName;
        printDocument.PrinterSettings.Copies = (short)job.Copies;   // PrinterJob đã kẹp 1..99
        printDocument.PrintPage += (_, args) =>
        {
            page++;
            var graphics = args.Graphics
                ?? throw new InvalidOperationException("Hệ thống không cấp mặt vẽ cho trang in.");

            using var gfx = XGraphics.FromGraphics(
                graphics, new XSize(args.PageBounds.Width, args.PageBounds.Height));
            renderer.RenderPage(gfx, page);

            args.HasMorePages = page < formatted.PageCount;
        };

        try
        {
            printDocument.Print();
        }
        catch (Exception ex) when (ex is not PrintJobFailedException)
        {
            // Trình điều khiển máy in ném đủ loại (InvalidPrinterException khi máy in vừa bị
            // xoá, Win32Exception khi hết giấy / mất kết nối...). Gói lại một kiểu mang theo
            // TÊN MÁY IN, để câu thông báo nói đúng chuyện vừa xảy ra.
            throw new PrintJobFailedException(job.PrinterName, ex);
        }
    }
}
```

- [ ] **Step 6: Xoá chỗ dựa tạm của Task 1**

```bash
git rm src/ConstructionFinance.Printing.Windows/PlaceholderMarker.cs
```

- [ ] **Step 7: Build cả hai đích + cả bộ test**

Run: `dotnet build ConstructionFinance.sln --nologo` → `0 Warning(s) 0 Error(s)`
Run: `dotnet test --nologo` → 0 hỏng.

Build là **toàn bộ** sự kiểm chứng mà `WindowsPrinterPort` nhận được trên máy này. Đó là điều đã biết trước và đã chấp nhận (spec §8).

- [ ] **Step 8: Commit**

```bash
git add src/ConstructionFinance.Application/Printing/IPrinterPort.cs \
        src/ConstructionFinance.Printing.Windows/ \
        tests/ConstructionFinance.Application.Tests/Printing/PrinterJobTests.cs
git commit -m "feat(print): cổng máy in và adapter Windows

PrinterJob kẹp số bản 1..99 ngay trong hàm dựng: PrinterSettings.Copies là
short, nên 100000 bản sẽ tràn âm trước khi tới trình điều khiển. Kẹp ở lệnh in
chứ không ở lớp giao diện — không ai đi vòng qua lệnh in được.

WindowsPrinterPort là vùng mù duy nhất: chỉ biên dịch được ở đây, chạy phải
Windows. Nên nó không chứa logic nội dung nào, chỉ vẽ trang khi hệ điều hành
đòi. Lỗi của trình điều khiển gói lại thành PrintJobFailedException mang tên
máy in, để câu thông báo nói đúng sự thật.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 11: Hộp thoại Chọn máy in

Dialog Avalonia tự dựng (spec §2: không kéo WinForms, không P/Invoke `PrintDlgEx`). Dùng đúng cơ chế sẵn có: `ViewModelBase` + `ViewLocator` (`ViewModels.Printing.ChonMayInViewModel` → `Views.Printing.ChonMayInView`) + `IDialogService.ShowDialogAsync<T>`.

Một chỗ phải sửa hạ tầng: `DialogService.ShowDialogAsync` **gán cứng** `PrimaryButtonText = "Lưu"`. Hộp thoại in bấm nút "Lưu" để in là sai chữ. Thêm `IHasPrimaryButtonText` theo đúng khuôn `IHasDialogTitle` đang có.

**Files:**
- Create: `src/ConstructionFinance.Desktop/ViewModels/Printing/ChonMayInViewModel.cs`
- Create: `src/ConstructionFinance.Desktop/Views/Printing/ChonMayInView.axaml`
- Create: `src/ConstructionFinance.Desktop/Views/Printing/ChonMayInView.axaml.cs`
- Modify: `src/ConstructionFinance.Desktop/Services/DialogService.cs`
- Modify: `tests/ConstructionFinance.Desktop.Tests/Common/FakeDialogService.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Printing/ChonMayInViewModelTests.cs`

**Interfaces:**
- Consumes: `IPrinterCatalog`, `PrinterJob.MinCopies`/`MaxCopies` (Task 10).
- Produces: `ChonMayInViewModel(IPrinterCatalog catalog)` với `IReadOnlyList<string> Printers`, `string? SelectedPrinter` (ghi được), `decimal? CopiesValue` (ghi được, bind cho `NumericUpDown`), `int Copies` (chỉ đọc, đã kẹp), `bool CoMayIn`, `DialogTitle`, `PrimaryButtonText`. `IHasPrimaryButtonText` trong `Services`. `FakeDialogService.OnDialog`.

- [ ] **Step 1: Viết test đỏ**

`tests/ConstructionFinance.Desktop.Tests/Printing/ChonMayInViewModelTests.cs`:

```csharp
using ConstructionFinance.Application.Printing;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.ViewModels.Printing;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Printing;

public class ChonMayInViewModelTests
{
    private sealed class FakeCatalog : IPrinterCatalog
    {
        public IReadOnlyList<string> InstalledPrinters { get; init; } = Array.Empty<string>();
        public string? DefaultPrinterName { get; init; }
    }

    private static ChonMayInViewModel Create(params string[] printers) =>
        new(new FakeCatalog { InstalledPrinters = printers, DefaultPrinterName = printers.FirstOrDefault() });

    [Fact]
    public void Liet_ke_moi_may_in_da_cai()
    {
        Create("Máy in A", "Máy in B").Printers.ShouldBe(new[] { "Máy in A", "Máy in B" });
    }

    [Fact]
    public void May_in_mac_dinh_duoc_chon_san()
    {
        var vm = new ChonMayInViewModel(new FakeCatalog
        {
            InstalledPrinters = new[] { "Máy in A", "Máy in B" },
            DefaultPrinterName = "Máy in B",
        });

        vm.SelectedPrinter.ShouldBe("Máy in B");
    }

    /// Máy in mặc định mà hệ điều hành báo lại không có trong danh sách (vừa bị xoá giữa hai
    /// lần đọc): chọn máy đầu danh sách, không để trống rồi bắt người dùng đoán.
    [Fact]
    public void May_in_mac_dinh_khong_co_trong_danh_sach_thi_chon_may_dau_tien()
    {
        var vm = new ChonMayInViewModel(new FakeCatalog
        {
            InstalledPrinters = new[] { "Máy in A" },
            DefaultPrinterName = "Máy in đã bị xoá",
        });

        vm.SelectedPrinter.ShouldBe("Máy in A");
    }

    [Fact]
    public void Mac_dinh_in_mot_ban()
    {
        Create("Máy in A").Copies.ShouldBe(1);
    }

    /// Review Focus 1, lần kẹp thứ hai — ở lớp giao diện. NumericUpDown có Minimum/Maximum,
    /// nhưng người dùng GÕ được chữ vào nó, và một bản dựng XAML sau này có thể bỏ sót hai
    /// thuộc tính đó. PrinterJob kẹp lần nữa ở Task 10; hai lần kẹp là có chủ ý.
    [Theory]
    [InlineData(null, 1)]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(1, 1)]
    [InlineData(99, 99)]
    [InlineData(100, 99)]
    [InlineData(100000, 99)]
    public void So_ban_bi_kep_trong_khoang_cho_phep(int? requested, int expected)
    {
        var vm = Create("Máy in A");
        vm.CopiesValue = requested;

        vm.Copies.ShouldBe(expected);
    }

    [Fact]
    public void So_ban_go_so_le_thi_lay_phan_nguyen()
    {
        var vm = Create("Máy in A");
        vm.CopiesValue = 2.7m;

        vm.Copies.ShouldBe(2);
    }

    /// Review Focus 2: máy chưa cài máy in nào. Hộp thoại không được mở ra rỗng rồi để người
    /// dùng bấm In vào hư không — CoMayIn là false và Task 12 chặn trước khi mở hộp thoại.
    [Fact]
    public void May_chua_cai_may_in_nao_thi_khong_co_gi_de_chon()
    {
        var vm = Create();

        vm.CoMayIn.ShouldBeFalse();
        vm.Printers.ShouldBeEmpty();
        vm.SelectedPrinter.ShouldBeNull();
    }

    [Fact]
    public void Nut_chinh_doc_la_In_chu_khong_phai_Luu()
    {
        var vm = Create("Máy in A");

        vm.DialogTitle.ShouldBe("Chọn máy in");
        ((IHasPrimaryButtonText)vm).PrimaryButtonText.ShouldBe("In");
    }
}
```

- [ ] **Step 2: Chạy để thấy đỏ**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter ChonMayInViewModelTests --nologo`
Expected: FAIL — `'ChonMayInViewModel' could not be found`.

- [ ] **Step 3: Thêm `IHasPrimaryButtonText` và dùng nó**

Trong `src/ConstructionFinance.Desktop/Services/DialogService.cs`, thêm cạnh `IHasDialogTitle`:

```csharp
/// Hộp thoại nào muốn nút chính mang chữ khác "Lưu" thì cài interface này. Hộp thoại chọn máy
/// in bấm "Lưu" để in là nói sai việc nó làm.
public interface IHasPrimaryButtonText
{
    string PrimaryButtonText { get; }
}
```

và trong `ShowDialogAsync`, đổi dòng `PrimaryButtonText = "Lưu"` thành:

```csharp
            PrimaryButtonText = (viewModel as IHasPrimaryButtonText)?.PrimaryButtonText ?? "Lưu",
```

- [ ] **Step 4: Viết view-model**

`src/ConstructionFinance.Desktop/ViewModels/Printing/ChonMayInViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.Printing;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.ViewModels.Printing;

/// Hộp thoại "Chọn máy in". Danh sách máy in vào qua IPrinterCatalog nên test chạy được trên
/// máy không có máy in nào.
public sealed partial class ChonMayInViewModel : ViewModelBase, IHasDialogTitle, IHasPrimaryButtonText
{
    public ChonMayInViewModel(IPrinterCatalog catalog)
    {
        Printers = catalog.InstalledPrinters;

        // Máy in mặc định của hệ điều hành, nếu nó còn trong danh sách. Không còn (vừa bị xoá
        // giữa hai lần đọc) thì lấy máy đầu danh sách — hơn là để trống rồi bắt người dùng đoán.
        var defaultName = catalog.DefaultPrinterName;
        _selectedPrinter = defaultName is not null && Printers.Contains(defaultName)
            ? defaultName
            : Printers.FirstOrDefault();
    }

    public string DialogTitle => "Chọn máy in";

    public string PrimaryButtonText => "In";

    public IReadOnlyList<string> Printers { get; }

    /// Máy này chưa cài máy in nào thì không có gì để chọn. Lệnh In kiểm cờ này TRƯỚC khi mở
    /// hộp thoại, nên người dùng không bao giờ thấy một danh sách rỗng.
    public bool CoMayIn => Printers.Count > 0;

    [ObservableProperty]
    private string? _selectedPrinter;

    /// Bind cho NumericUpDown, kiểu decimal? đúng bằng kiểu của NumericUpDown.Value — KHÔNG
    /// đi qua chuỗi và KHÔNG StringFormat, vì bind hai chiều qua chuỗi trên kiểu số là chỗ
    /// dự án này đã trả giá một lần.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Copies))]
    private decimal? _copiesValue = PrinterJob.MinCopies;

    /// Số bản thật dùng để in: đã kẹp trong [1, 99] và đã về số nguyên. NumericUpDown có
    /// Minimum/Maximum, nhưng người dùng GÕ được chữ vào nó, nên không tin vào XAML.
    /// PrinterJob kẹp lần nữa — hai lần kẹp là có chủ ý.
    public int Copies
    {
        get
        {
            var value = CopiesValue ?? PrinterJob.MinCopies;
            var whole = decimal.Truncate(value);
            if (whole < PrinterJob.MinCopies) return PrinterJob.MinCopies;
            if (whole > PrinterJob.MaxCopies) return PrinterJob.MaxCopies;
            return (int)whole;
        }
    }
}
```

- [ ] **Step 5: Viết view**

`src/ConstructionFinance.Desktop/Views/Printing/ChonMayInView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Printing"
             x:Class="ConstructionFinance.Desktop.Views.Printing.ChonMayInView"
             x:DataType="vm:ChonMayInViewModel">
    <StackPanel Spacing="12" MinWidth="380" Margin="4">
        <TextBlock Text="Máy in" FontWeight="SemiBold"/>
        <ComboBox ItemsSource="{Binding Printers}"
                  SelectedItem="{Binding SelectedPrinter}"
                  HorizontalAlignment="Stretch"/>

        <TextBlock Text="Số bản" FontWeight="SemiBold"/>
        <!-- Value là decimal? của Avalonia, và CopiesValue cũng là decimal?: bind đúng kiểu,
             không StringFormat. Minimum/Maximum chỉ là lớp chắn đầu tiên — view-model kẹp lại. -->
        <NumericUpDown Value="{Binding CopiesValue}"
                       Minimum="1" Maximum="99" Increment="1"
                       FormatString="0"
                       HorizontalAlignment="Left" Width="140"/>
    </StackPanel>
</UserControl>
```

`src/ConstructionFinance.Desktop/Views/Printing/ChonMayInView.axaml.cs`:

```csharp
using Avalonia.Controls;

namespace ConstructionFinance.Desktop.Views.Printing;

public partial class ChonMayInView : UserControl
{
    public ChonMayInView() => InitializeComponent();
}
```

- [ ] **Step 6: Cho `FakeDialogService` sửa được view-model**

Trong `tests/ConstructionFinance.Desktop.Tests/Common/FakeDialogService.cs`, thêm cạnh `OnMessage`:

```csharp
    /// Gọi mỗi khi một hộp thoại được mở, với chính view-model của nó — để test giả lập việc
    /// người dùng chọn máy in và gõ số bản TRƯỚC khi hộp thoại "đóng". Không có nó thì mọi
    /// test chỉ kiểm được giá trị mặc định.
    public Action<object>? OnDialog { get; set; }
```

và trong `ShowDialogAsync`, thêm sau `OpenedDialogs.Add(viewModel);`:

```csharp
        OnDialog?.Invoke(viewModel);
```

- [ ] **Step 7: Chạy để thấy xanh**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter ChonMayInViewModelTests --nologo`
Expected: `Passed: 14`.

- [ ] **Step 8: Test headless rằng View dựng được**

Thêm vào cuối `ChonMayInViewModelTests`:

```csharp
    /// ViewLocator tìm View theo tên (ViewModels.Printing.ChonMayInViewModel →
    /// Views.Printing.ChonMayInView). Đặt sai namespace hay sai tên tệp thì hộp thoại hiện ra
    /// chữ "View not found" mà không test nào trên view-model thấy được.
    [Fact]
    public void ViewLocator_tim_duoc_View_cua_hop_thoai()
    {
        HeadlessTestSession.Run(() =>
        {
            var control = new ViewLocator().Build(Create("Máy in A"));

            control.ShouldBeOfType<ConstructionFinance.Desktop.Views.Printing.ChonMayInView>();
        });
    }
```

Thêm `using ConstructionFinance.Desktop;` và `using ConstructionFinance.Desktop.Tests.Finance;` vào đầu tệp.

- [ ] **Step 9: Chạy + cả bộ test**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --filter ChonMayInViewModelTests --nologo` → `Passed: 15`
Run: `dotnet test --nologo` → 0 hỏng.

- [ ] **Step 10: Commit**

```bash
git add src/ConstructionFinance.Desktop/ViewModels/Printing/ \
        src/ConstructionFinance.Desktop/Views/Printing/ \
        src/ConstructionFinance.Desktop/Services/DialogService.cs \
        tests/ConstructionFinance.Desktop.Tests/Common/FakeDialogService.cs \
        tests/ConstructionFinance.Desktop.Tests/Printing/
git commit -m "feat(print): hộp thoại Chọn máy in, tự dựng bằng Avalonia

Không kéo WinForms, không P/Invoke PrintDlgEx: dùng đúng ViewModelBase +
ViewLocator + IDialogService đang có. Danh sách máy in vào qua IPrinterCatalog
nên test chạy trên máy không có máy in nào.

Thêm IHasPrimaryButtonText vì ShowDialogAsync gán cứng nút chính là \"Lưu\" —
bấm Lưu để in là nói sai việc nó làm.

Số bản bind decimal? đúng kiểu NumericUpDown.Value, không qua chuỗi. View-model
kẹp lại 1..99 vì người dùng gõ được chữ vào NumericUpDown.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 12: Bốn lệnh và menu năm mục

Nối đường in vào giao diện. **Thứ tự các bước ở đây quan trọng**: hai lệnh mới dùng lại đúng tên `PrintCurrentSheetAsync` / `PrintAllAsync` của hai lệnh cũ (vì "In" bây giờ nghĩa là ra giấy). Nếu đổi tên lệnh cũ và thêm lệnh mới trong **cùng một bước**, các test cũ vẫn biên dịch nhưng lặng lẽ chuyển sang kiểm đường in giấy. Nên: đổi tên trước, **build cho trình biên dịch bắt hết**, rồi mới thêm lệnh mới.

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/Views/MainWindow.axaml`
- Modify: `src/ConstructionFinance.Desktop/Common/HostBuilder.cs`
- Modify: `tests/ConstructionFinance.Desktop.Tests/MainWindowPrintCommandTests.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Printing/PrintToPrinterCommandTests.cs`

**Interfaces:**
- Consumes: `IMigraDocBuilder` (Task 4–9), `IPrinterPort`/`IPrinterCatalog`/`PrinterJob`/`PrintJobFailedException` (Task 10), `ChonMayInViewModel` (Task 11), `FakeDialogService.OnDialog` (Task 11).
- Produces: `MainWindowViewModel` với `ExportCurrentSheetCommand`, `ExportAllCommand`, `PrintCurrentSheetCommand`, `PrintAllCommand`; `CanPrint` đúng khi bất kỳ lệnh nào trong bốn dùng được.

- [ ] **Step 1: Đổi tên hai lệnh cũ thành `Export*`**

Trong `MainWindowViewModel.cs`, đổi **chỉ tên**, không đổi thân:

| Cũ | Mới |
|---|---|
| `PrintCurrentSheetAsync` | `ExportCurrentSheetAsync` |
| `CanPrintCurrentSheet` | `CanExportCurrentSheet` |
| `PrintAllAsync` | `ExportAllAsync` |
| `CanPrintAll` | `CanExportAll` |

`CanPrint` thành:

```csharp
    /// Nút In ở khung chung sáng khi ÍT NHẤT MỘT trong bốn mục của nó dùng được; không thì mờ
    /// (vẫn nằm đúng chỗ). Chỉ ĐỌC trạng thái của màn đang mở và của danh sách máy in.
    public bool CanPrint =>
        CanExportCurrentSheet() || CanExportAll() || CanPrintCurrentSheet() || CanPrintAll();
```

(hai hàm `CanPrint*` mới viết ở Step 4; tạm thời chưa có nên Step 2 sẽ đỏ — đó là chủ ý.)

- [ ] **Step 2: Build để trình biên dịch chỉ ra mọi chỗ còn tên cũ**

Run: `dotnet build ConstructionFinance.sln --nologo`
Expected: lỗi `CS1061` ở `MainWindowPrintCommandTests.cs` cho `PrintCurrentSheetCommand` / `PrintAllCommand`, cộng lỗi `CanPrintCurrentSheet` chưa tồn tại.

Đổi `PrintCurrentSheetCommand` → `ExportCurrentSheetCommand` và `PrintAllCommand` →
`ExportAllCommand` trong **ba** tệp (không phải một):

| Tệp | Số chỗ |
|---|---|
| `tests/ConstructionFinance.Desktop.Tests/MainWindowPrintCommandTests.cs` | ~65 |
| `tests/ConstructionFinance.Desktop.Tests/Finance/PrintWorkbookTests.cs` | 3 (dòng 312, 330, 371) |
| `src/ConstructionFinance.Desktop/Views/MainWindow.axaml` | 2 (Step 7 làm) |

**Đừng đổi gì khác** — những test đó đang kiểm đường ghi tệp và phải tiếp tục kiểm đúng đường đó.

> ⚠️ **Bẫy ngược: `CanPrintAll` còn là tên một THUỘC TÍNH của `IPrintableWorkbook`**
> (`src/ConstructionFinance.Desktop/Common/IPrintablePage.cs:52`), dùng ở
> `FinanceWorkbookViewModel.cs:762`, `MainWindowViewModel.cs:337`
> (`nameof(IPrintableWorkbook.CanPrintAll)`) và `MainWindowPrintCommandTests.cs:1198`.
> **Chỉ đổi tên phương thức `CanPrintAll()` của `MainWindowViewModel`.** Thay thế mù cả tệp sẽ
> phá ba chỗ kia — và chúng là thứ làm nút In bật/mờ, nên vỡ ở đây là vỡ âm thầm.

- [ ] **Step 2b: `RaisePrintStateChanged` phải báo cho CẢ BỐN lệnh**

`MainWindowViewModel.RaisePrintStateChanged()` (dòng ~342) hiện gọi `NotifyCanExecuteChanged()`
cho hai lệnh. Thêm hai lệnh mà quên chỗ này thì **hai mục menu mới không bật/mờ lại khi người
dùng đổi màn** — chúng giữ nguyên trạng thái của màn trước, và không test nào hiện có thấy.

```csharp
    /// CanExecuteChanged là thứ Avalonia nghe để bật/mờ lại từng mục menu; CanPrint/PrintToolTip là thứ nút In nghe.
    /// Bốn lệnh, bốn lần báo: thiếu một là một mục menu đứng im với trạng thái của màn trước.
    private void RaisePrintStateChanged()
    {
        ExportCurrentSheetCommand.NotifyCanExecuteChanged();
        ExportAllCommand.NotifyCanExecuteChanged();
        PrintCurrentSheetCommand.NotifyCanExecuteChanged();
        PrintAllCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanPrint));
        OnPropertyChanged(nameof(PrintToolTip));
    }
```

Và ghim nó — thêm vào `PrintToPrinterCommandTests` (Step 4):

```csharp
    /// Đổi màn thì cả bốn mục menu phải được báo bật/mờ lại. Hai mục In mới dễ bị quên ở
    /// RaisePrintStateChanged, và quên thì chúng đứng im với trạng thái của màn trước.
    [Fact]
    public void Doi_man_thi_ca_bon_lenh_duoc_bao_bat_mo_lai()
    {
        var harness = Create();
        var raised = new List<string>();
        foreach (var (name, command) in new (string, System.Windows.Input.ICommand)[]
                 {
                     ("export-sheet", harness.Vm.ExportCurrentSheetCommand),
                     ("export-all", harness.Vm.ExportAllCommand),
                     ("print-sheet", harness.Vm.PrintCurrentSheetCommand),
                     ("print-all", harness.Vm.PrintAllCommand),
                 })
        {
            var captured = name;
            command.CanExecuteChanged += (_, _) => raised.Add(captured);
        }

        harness.Vm.CurrentContent = new FakePrintableWorkbook();

        raised.ShouldBe(new[] { "export-sheet", "export-all", "print-sheet", "print-all" },
            ignoreOrder: true);
    }
```

- [ ] **Step 3: Chuyển bốn bộ giả ra chỗ dùng chung**

`FakePrintablePage`, `FakePrintableWorkbook`, `FakeLauncher` và `FakeWriter` đang là lớp lồng
`private` trong `MainWindowPrintCommandTests.cs` (dòng ~1120 trở xuống). Lớp test mới cần ba lớp
đầu, nên **chuyển chúng ra** `tests/ConstructionFinance.Desktop.Tests/Common/PrintFakes.cs` thành
`internal`, rồi xoá bản lồng. Đừng chép lại — hai bản sẽ trôi khỏi nhau.

Thêm vào `FakePrintableWorkbook` một thuộc tính để test dựng được trường hợp "có sheet bị bỏ qua":

```csharp
    /// true thì BuildAllSheets() trả về một phần tử trong Skipped, để test dựng được cảnh báo
    /// "đã bỏ qua" mà không phải dựng cả một workbook thật.
    public bool SkipGdntt { get; set; }
```

và trong `BuildAllSheets()` của nó (trả `PrintAllResult`), trả `Skipped` theo cờ đó —
`SkippedPrintPage` là `record SkippedPrintPage(string Title, string? Reason)`, nên một phần tử
đủ dùng là `new SkippedPrintPage("GĐNTT", "chưa chọn gói thầu")`.

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --nologo`
Expected: vẫn 0 hỏng — đây là bước chuyển chỗ, không đổi hành vi.

- [ ] **Step 4: Viết test đỏ cho đường in giấy**

`tests/ConstructionFinance.Desktop.Tests/Printing/PrintToPrinterCommandTests.cs`:

```csharp
using ConstructionFinance.Application.Printing;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels;
using ConstructionFinance.Desktop.ViewModels.Printing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Printing;

/// Đường "In ra giấy": dựng tài liệu → hộp thoại chọn máy in → gửi lệnh in. Cổng máy in, hộp
/// thoại và danh sách máy in đều là bản giả: test không chạm máy in thật.
public class PrintToPrinterCommandTests
{
    private const string NoPrinterMessage =
        "MSG: In ra máy in — Máy này chưa cài máy in nào. Bạn có thể dùng \"Xuất Excel\" để lưu tệp rồi in từ máy khác.";

    private sealed class FakeCatalog : IPrinterCatalog
    {
        public IReadOnlyList<string> InstalledPrinters { get; init; } = new[] { "Máy in A", "Máy in B" };
        public string? DefaultPrinterName { get; init; } = "Máy in A";
    }

    private sealed class FakePrinterPort : IPrinterPort
    {
        public List<PrinterJob> Jobs { get; } = new();
        public Exception? Failure { get; set; }

        public Task PrintAsync(PrinterJob job, CancellationToken ct = default)
        {
            Jobs.Add(job);
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }

    private sealed record Harness(
        MainWindowViewModel Vm, FakeDialogService Dialog, FakePrinterPort Port);

    /// Dựng view-model với bản giả cho mọi thứ chạm thế giới ngoài. `printers` rỗng nghĩa là
    /// máy chưa cài máy in nào.
    private static Harness Create(string[]? printers = null)
    {
        var dialog = new FakeDialogService();
        var port = new FakePrinterPort();
        var services = new ServiceCollection();
        services.AddSingleton<IDialogService>(dialog);
        services.AddSingleton<IFileLauncher>(new FakeLauncher());
        services.AddSingleton<IPrintWorkbookWriter>(new PrintWorkbookWriter());
        services.AddSingleton<IMigraDocBuilder>(new MigraDocBuilder());
        services.AddSingleton<IPrinterPort>(port);
        services.AddSingleton<IPrinterCatalog>(new FakeCatalog
        {
            InstalledPrinters = printers ?? new[] { "Máy in A", "Máy in B" },
            DefaultPrinterName = (printers ?? new[] { "Máy in A" }).FirstOrDefault(),
        });

        var provider = services.BuildServiceProvider();
        // Ctor nhận HAI tham số: IServiceProvider và INavigationService.
        var vm = new MainWindowViewModel(provider, new FakeNavigationService())
        {
            CurrentContent = new FakePrintablePage(),
        };
        return new Harness(vm, dialog, port);
    }

    // ===== Đường đi thuận =====

    [Fact]
    public async Task In_sheet_nay_gui_dung_mot_lenh_in_den_may_in_da_chon()
    {
        var harness = Create();
        harness.Dialog.OnDialog = vm => ((ChonMayInViewModel)vm).SelectedPrinter = "Máy in B";

        await harness.Vm.PrintCurrentSheetCommand.ExecuteAsync(null);

        harness.Port.Jobs.Count.ShouldBe(1);
        harness.Port.Jobs[0].PrinterName.ShouldBe("Máy in B");
    }

    [Fact]
    public async Task So_ban_nguoi_dung_go_di_vao_lenh_in()
    {
        var harness = Create();
        harness.Dialog.OnDialog = vm => ((ChonMayInViewModel)vm).CopiesValue = 3m;

        await harness.Vm.PrintCurrentSheetCommand.ExecuteAsync(null);

        harness.Port.Jobs[0].Copies.ShouldBe(3);
    }

    /// Review Focus 1, lần kẹp cuối: dù hộp thoại trả về bao nhiêu, lệnh in không bao giờ
    /// mang một số bản làm tràn short.
    [Fact]
    public async Task So_ban_vo_ly_khong_bao_gio_ra_duoc_den_may_in()
    {
        var harness = Create();
        harness.Dialog.OnDialog = vm => ((ChonMayInViewModel)vm).CopiesValue = 100_000m;

        await harness.Vm.PrintCurrentSheetCommand.ExecuteAsync(null);

        harness.Port.Jobs[0].Copies.ShouldBe(PrinterJob.MaxCopies);
    }

    [Fact]
    public async Task In_toan_bo_gom_moi_sheet_vao_MOT_lenh_in()
    {
        var harness = Create();
        harness.Vm.CurrentContent = new FakePrintableWorkbook();

        await harness.Vm.PrintAllCommand.ExecuteAsync(null);

        harness.Port.Jobs.Count.ShouldBe(1);
        harness.Dialog.OpenedDialogs.OfType<ChonMayInViewModel>().Count().ShouldBe(1);
    }

    // ===== Người dùng hủy =====

    [Fact]
    public async Task Bam_Huy_thi_khong_in_va_khong_noi_gi()
    {
        var harness = Create();
        harness.Dialog.DialogResult = false;

        await harness.Vm.PrintCurrentSheetCommand.ExecuteAsync(null);

        harness.Port.Jobs.ShouldBeEmpty();
        harness.Dialog.ShownMessages.ShouldBeEmpty();
    }

    // ===== Máy chưa cài máy in nào =====

    /// Review Focus 2a. Không mở ra một hộp thoại rỗng: nói thẳng và gợi ý đường còn dùng được.
    [Fact]
    public async Task May_chua_cai_may_in_nao_thi_noi_thang_va_goi_y_xuat_Excel()
    {
        var harness = Create(Array.Empty<string>());

        await harness.Vm.PrintCurrentSheetCommand.ExecuteAsync(null);

        harness.Dialog.ShownMessages.ShouldBe(new[] { NoPrinterMessage });
        harness.Dialog.OpenedDialogs.ShouldBeEmpty();
        harness.Port.Jobs.ShouldBeEmpty();
    }

    /// …nhưng nút In vẫn phải sáng, vì hai mục Xuất Excel còn dùng được.
    [Fact]
    public void May_chua_cai_may_in_nao_thi_nut_In_van_sang_cho_hai_muc_Xuat_Excel()
    {
        Create(Array.Empty<string>()).Vm.CanPrint.ShouldBeTrue();
    }

    // ===== Máy in báo lỗi giữa chừng =====

    /// Review Focus 2b: máy in bị xoá hoặc ngắt kết nối giữa lúc chọn và lúc in. Câu thông báo
    /// phải nêu TÊN MÁY IN và lời của hệ thống, chứ không phải một câu chung chung.
    [Fact]
    public async Task May_in_bao_loi_thi_cau_thong_bao_neu_ten_may_in_va_loi_cua_he_thong()
    {
        var harness = Create();
        harness.Port.Failure = new PrintJobFailedException(
            "Máy in A", new InvalidOperationException("Máy in không còn tồn tại."));

        await harness.Vm.PrintCurrentSheetCommand.ExecuteAsync(null);

        var message = harness.Dialog.ShownMessages.ShouldHaveSingleItem();
        message.ShouldContain("Máy in A");
        message.ShouldContain("Máy in không còn tồn tại.");
        message.ShouldContain("thử lại");
    }

    /// Lỗi lạ ở bất kỳ bước nào cũng không được làm sập app: người dùng có thể đang có sổ chưa lưu.
    [Fact]
    public async Task Loi_bat_ngo_thi_bao_mot_cau_chung_chu_khong_sap_app()
    {
        var harness = Create();
        harness.Port.Failure = new OutOfMemoryException("hết bộ nhớ");

        await Should.NotThrowAsync(() => harness.Vm.PrintCurrentSheetCommand.ExecuteAsync(null));

        harness.Dialog.ShownMessages.ShouldHaveSingleItem().ShouldContain("lỗi bất ngờ");
    }

    // ===== Cảnh báo "đã bỏ qua" =====

    /// Đường ghi tệp báo "đã bỏ qua sheet nào" SAU khi ghi, trước khi mở — tệp đã nằm trên đĩa
    /// nên đọc cảnh báo rồi vẫn xử lý được. Giấy thì không rút lại được, nên ở đường in cảnh
    /// báo phải hiện TRƯỚC hộp thoại chọn máy in, để người dùng còn kịp hủy.
    [Fact]
    public async Task Canh_bao_da_bo_qua_hien_TRUOC_hop_thoai_chon_may_in()
    {
        var harness = Create();
        harness.Vm.CurrentContent = new FakePrintableWorkbook { SkipGdntt = true };

        var order = new List<string>();
        harness.Dialog.OnMessage = _ => order.Add("cảnh báo");
        harness.Dialog.OnDialog = _ => order.Add("hộp thoại");

        await harness.Vm.PrintAllCommand.ExecuteAsync(null);

        order.ShouldBe(new[] { "cảnh báo", "hộp thoại" });
    }

    /// Và đọc xong cảnh báo mà bấm Hủy thì KHÔNG in — đó là lý do cảnh báo đứng trước.
    [Fact]
    public async Task Doc_canh_bao_roi_bam_Huy_thi_khong_in()
    {
        var harness = Create();
        harness.Vm.CurrentContent = new FakePrintableWorkbook { SkipGdntt = true };
        harness.Dialog.DialogResult = false;

        await harness.Vm.PrintAllCommand.ExecuteAsync(null);

        harness.Port.Jobs.ShouldBeEmpty();
    }
}
```

- [ ] **Step 5: Viết hai lệnh mới**

Trong `MainWindowViewModel.cs`, thêm cạnh hai lệnh `Export*`:

```csharp
    private const string PrintPaperTitle = "In ra máy in";
    private const string NoPrinterMessage =
        "Máy này chưa cài máy in nào. Bạn có thể dùng \"Xuất Excel\" để lưu tệp rồi in từ máy khác.";

    [RelayCommand(CanExecute = nameof(CanPrintCurrentSheet))]
    private Task PrintCurrentSheetAsync() => GuardPrintAsync(PrintPaperTitle, async () =>
    {
        var page = (IPrintablePage)CurrentContent!;
        var sheet = page.BuildPrintSheet();
        if (sheet is null)
        {
            await ShowNothingToPrintAsync();
            return;
        }
        await PrintToPrinterAsync(new[] { sheet }, footerViTriName: page.PrintSheetName);
    });

    /// Đích net8.0 (bộ test) không đăng ký IPrinterPort nào, nên hai lệnh này mờ ở đó thay vì
    /// ném NullReference giữa một lần chạy test. Bản phát hành luôn là net8.0-windows.
    private bool CanPrintCurrentSheet() =>
        HasPrinterSupport && CurrentContent is IPrintablePage { PrintSheetName: { Length: > 0 } };

    [RelayCommand(CanExecute = nameof(CanPrintAll))]
    private Task PrintAllAsync() => GuardPrintAsync(PrintPaperTitle, async () =>
    {
        var workbook = (IPrintableWorkbook)CurrentContent!;
        var all = workbook.BuildAllSheets();
        if (all.Sheets.Count == 0)
        {
            await ShowNothingToPrintAsync();
            return;
        }
        await PrintToPrinterAsync(all.Sheets, footerViTriName: null, warningBeforePrint: DescribeSkipped(all.Skipped));
    });

    private bool CanPrintAll() =>
        HasPrinterSupport && CurrentContent is IPrintableWorkbook { CanPrintAll: true };

    private bool HasPrinterSupport =>
        _services.GetService<IPrinterPort>() is not null
        && _services.GetService<IPrinterCatalog>() is not null;

    /// Đường chung của cả hai lệnh in giấy: cảnh báo (nếu có) → chọn máy in → gửi lệnh in.
    ///
    /// `warningBeforePrint` hiện TRƯỚC hộp thoại, khác đường ghi tệp (ở đó cảnh báo hiện sau
    /// khi ghi). Cố ý: tệp đã nằm trên đĩa thì đọc cảnh báo rồi vẫn xử lý được, còn giấy đã
    /// chạy ra thì không rút lại được — nên người dùng phải đọc trước khi quyết.
    private async Task PrintToPrinterAsync(
        IReadOnlyList<PrintSheet> sheets, string? footerViTriName, string? warningBeforePrint = null)
    {
        var dialog = _services.GetRequiredService<IDialogService>();
        var catalog = _services.GetRequiredService<IPrinterCatalog>();

        var chooser = new ChonMayInViewModel(catalog);
        if (!chooser.CoMayIn)
        {
            await dialog.ShowMessageAsync(PrintPaperTitle, NoPrinterMessage);
            return;
        }

        if (warningBeforePrint is not null)
            await dialog.ShowMessageAsync(PrintPaperTitle, warningBeforePrint);

        if (await dialog.ShowDialogAsync(chooser) != true) return;   // Hủy: im lặng, không in

        var printerName = chooser.SelectedPrinter;
        if (string.IsNullOrWhiteSpace(printerName))
        {
            await dialog.ShowMessageAsync(PrintPaperTitle, NoPrinterMessage);
            return;
        }

        var workbook = CurrentContent as IPrintableWorkbook;
        var footer = new PrintFooterInfo(workbook?.PrintFooterProjectName ?? "", footerViTriName);
        var document = _services.GetRequiredService<IMigraDocBuilder>().Build(sheets, footer);

        try
        {
            await _services.GetRequiredService<IPrinterPort>()
                .PrintAsync(new PrinterJob(document, printerName, chooser.Copies));
        }
        catch (PrintJobFailedException ex)
        {
            Log.Error(ex, "Máy in {Printer} báo lỗi", ex.PrinterName);
            await dialog.ShowMessageAsync(PrintPaperTitle,
                $"Máy in \"{ex.PrinterName}\" không in được. {ex.InnerException?.Message} " +
                "Hãy kiểm tra máy in đã bật, có giấy và còn kết nối, rồi thử lại.");
        }
    }
```

Và đổi `GuardPrintAsync` để nhận tiêu đề (đường ghi tệp truyền `PrintFileTitle` như cũ, nên câu thông báo của nó không đổi):

```csharp
    private async Task GuardPrintAsync(string title, Func<Task> print)
    {
        try
        {
            await print();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Lệnh In gặp lỗi bất ngờ");
            await _services.GetRequiredService<IDialogService>().ShowMessageAsync(title,
                "Không in được do lỗi bất ngờ. Chi tiết lỗi đã được ghi vào nhật ký ứng dụng.");
        }
    }
```

Đổi chữ ký `GuardPrintAsync` là đổi **cả hai** chỗ gọi cũ: `ExportCurrentSheetAsync` và
`ExportAllAsync` phải thành `GuardPrintAsync(PrintFileTitle, async () => ...)`. Giữ đúng
`PrintFileTitle` để câu thông báo của đường ghi tệp không đổi — các test cũ ghim nó từng chữ.

Thêm `using ConstructionFinance.Desktop.ViewModels.Printing;` vào đầu tệp.

- [ ] **Step 6: Đăng ký dịch vụ**

Trong `HostBuilder.cs`, sau dòng `IPrintWorkbookWriter`:

```csharp
                services.AddSingleton<IMigraDocBuilder, MigraDocBuilder>();
#if NET8_0_WINDOWS
                // Adapter máy in chỉ tồn tại ở đích net8.0-windows. Ở đích net8.0 (bộ test)
                // không có bản nào, nên hai lệnh In ra giấy mờ đi — đúng như CanPrint* kiểm.
                services.AddSingleton<Application.Printing.IPrinterPort,
                    Printing.Windows.WindowsPrinterPort>();
                services.AddSingleton<Application.Printing.IPrinterCatalog,
                    Printing.Windows.WindowsPrinterCatalog>();
#endif
```

`NET8_0_WINDOWS` là ký hiệu SDK tự định nghĩa cho đích `net8.0-windows`; không cần khai thêm gì.

- [ ] **Step 7: Menu năm mục**

Trong `MainWindow.axaml`, thay khối `<MenuFlyout>` bằng:

```xml
                            <!-- Placement="Top": nút In nằm sát đáy khung trái, menu mở xuống
                                 chỉ còn ~10px dư trên màn 1080px và che nút Import Excel. Năm
                                 mục thì mở xuống là tràn khỏi màn hình. -->
                            <MenuFlyout Placement="Top">
                                <MenuItem Header="In sheet này" Command="{Binding PrintCurrentSheetCommand}"/>
                                <MenuItem Header="In toàn bộ" Command="{Binding PrintAllCommand}"/>
                                <Separator/>
                                <MenuItem Header="Xuất Excel sheet này" Command="{Binding ExportCurrentSheetCommand}"/>
                                <MenuItem Header="Xuất Excel toàn bộ" Command="{Binding ExportAllCommand}"/>
                            </MenuFlyout>
```

- [ ] **Step 8: Chạy**

Run: `dotnet build ConstructionFinance.sln --nologo` → 0 lỗi, 0 warning
Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --nologo` → 0 hỏng
Run: `dotnet test --nologo` → 0 hỏng

- [ ] **Step 9: Commit**

```bash
git add src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs \
        src/ConstructionFinance.Desktop/Views/MainWindow.axaml \
        src/ConstructionFinance.Desktop/Common/HostBuilder.cs \
        tests/ConstructionFinance.Desktop.Tests/
git commit -m "feat(print): bốn lệnh và menu năm mục — In ra giấy, Xuất Excel ra tệp

Hai lệnh cũ đổi tên thành Export*; hai lệnh In mới dựng tài liệu MigraDoc, mở
hộp thoại chọn máy in, rồi gửi một lệnh in. In toàn bộ vẫn là MỘT lệnh in, MỘT
hộp thoại.

Cảnh báo \"đã bỏ qua sheet nào\" ở đường in hiện TRƯỚC hộp thoại chọn máy in,
khác đường ghi tệp: tệp đã nằm trên đĩa thì đọc cảnh báo rồi vẫn xử lý được,
còn giấy đã chạy ra thì không rút lại được.

MenuFlyout đổi sang Placement=\"Top\": nút In sát đáy khung trái, mở xuống chỉ
còn ~10px dư trên màn 1080px và che nút Import Excel.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 13: Khoá lại — tài liệu, kiểm tổng, và nói rõ còn gì chưa kiểm

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-in-thang-ra-may-in-design.md`
- Modify: `docs/superpowers/plans/2026-10-07-in-thang-ra-may-in.md`

**Interfaces:**
- Consumes: mọi task trước.
- Produces: không có mã mới.

- [ ] **Step 1: Kiểm tổng, không bỏ qua cảnh báo**

```bash
dotnet build ConstructionFinance.sln --nologo -warnaserror
dotnet test --nologo
```
Expected: `0 Warning(s) 0 Error(s)`; mọi dự án test `Passed!`, 0 hỏng, 0 bỏ qua.

- [ ] **Step 2: Đếm lại và ghi con số thật**

```bash
dotnet test --nologo 2>&1 | grep -E "Passed!|Failed:"
```

Ghi tổng mới vào mục **Global Constraints** của plan này (thay `817`) và vào §4.3 của spec.

- [ ] **Step 2b: Chặn một lỗi trôi âm thầm — nối lề Excel vào hằng số của bản in**

`FormExcelExporter.ApplyPageDefaults` đặt `Margins.Left = 0.5` / `Margins.Right = 0.5` bằng số
viết thẳng, còn `ExcelFitScale.SideMarginInches` là **bản sao thứ hai** của đúng con số đó. Ai đổi
lề của đường `.xlsx` sẽ làm tỉ lệ co của bản in MigraDoc sai, mà không test nào đỏ.

Trong `src/ConstructionFinance.Application/Forms/FormExcelExporter.cs`, đổi hai dòng đó thành:

```csharp
        // Cùng MỘT con số với ExcelFitScale.SideMarginInches: bộ dựng MigraDoc tính tỉ lệ co dựa
        // trên lề này, nên hai nơi dùng hai bản sao là một lỗi trôi âm thầm chờ xảy ra.
        ws.PageSetup.Margins.Left = Printing.ExcelFitScale.SideMarginInches;
        ws.PageSetup.Margins.Right = Printing.ExcelFitScale.SideMarginInches;
```

và ghim bằng một test trong `tests/ConstructionFinance.Application.Tests/Printing/ExcelFitScaleTests.cs`:

```csharp
    /// Đường .xlsx và bộ dựng MigraDoc phải dùng CÙNG MỘT lề: ExcelFitScale tính tỉ lệ co dựa
    /// trên lề đó, nên hai bản sao lệch nhau là bản in sai mà không ai thấy.
    [Fact]
    public void Duong_xlsx_dung_dung_le_ma_bo_dung_MigraDoc_tinh_theo()
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var worksheet = workbook.AddWorksheet("x");
        ConstructionFinance.Application.Forms.FormExcelExporter.ApplyPageDefaults(
            worksheet, ClosedXML.Excel.XLPageOrientation.Landscape);

        worksheet.PageSetup.Margins.Left.ShouldBe(ExcelFitScale.SideMarginInches);
        worksheet.PageSetup.Margins.Right.ShouldBe(ExcelFitScale.SideMarginInches);
    }
```

(`ApplyPageDefaults` là `internal`, và `Application` đã có `InternalsVisibleTo` cho
`ConstructionFinance.Application.Tests`.)

Run: `dotnet test tests/ConstructionFinance.Application.Tests --filter ExcelFitScaleTests --nologo` → xanh.

- [ ] **Step 3: Ghi vào spec những gì đã lệch khỏi nó**

Thêm vào cuối spec, mục `## 12. Khác với thiết kế ban đầu`, mỗi dòng một chỗ thực thi đã phải đổi — tối thiểu là:

- Luật **bù tràn chữ** của `FormGrid` (spec §5.1 không có): Excel cho chữ tràn sang ô trống bên phải, MigraDoc xuống dòng trong lòng ô, nên ô có chữ dài hơn cột mà chưa gộp tường minh thì tự gộp sang phải.
- Thêm `IHasPrimaryButtonText` vì `ShowDialogAsync` gán cứng nút chính là "Lưu".
- Số bản bị kẹp **hai lần** (view-model và `PrinterJob`), không một lần.
- **Bộ dựng PDF nằm ở dự án TEST, không ở `Application`** như bảng §4.3 của spec ghi. Lý do:
  §3 đã xếp PDF là công cụ kiểm chứng chứ không phải tính năng người dùng, nên để nó trong
  `Application` là đưa mã không ai dùng vào bản phát hành. Muốn biến PDF thành tính năng thật
  sau này thì chuyển `PdfProbe` sang `Application` và đặt tên `PdfRenderer`.
- **Dòng tiêu đề lặp mỗi trang chỉ được ghim ở mức mô hình** (`Row.HeadingFormat == true`), không
  ghim bằng cách đọc chữ trên từng trang PDF. Bảng §8 của spec nói "render ra PDF rồi mở lại soi
  số trang / chữ / hướng" — phần "chữ" không làm được: PDFsharp không trích được chữ từ PDF.

Lệch khác thì ghi thêm. **Không sửa lại spec cho giống mã rồi im lặng** — mục này tồn tại để người sau biết chỗ nào thiết kế đã đoán sai.

- [ ] **Step 4: Nhắc lại ba việc còn treo**

Thêm vào cuối spec:

```markdown
## 13. Chưa kiểm được ở đây

- **In thử trên Windows có máy in thật.** `WindowsPrinterPort` chỉ được biên dịch trên máy
  phát triển. Đây là nghiệm thu cuối cùng của cả đợt và chỉ người dùng làm được.
- **Độ đậm viền và hai mức nền trên giấy thật** (việc "nhìn bằng mắt" số 2 của spec 10/9) —
  vẫn treo, và đợt này không giải quyết.
- **Cỡ chữ 7,7pt của Sổ Vị trí có đọc nổi trên giấy hay không.** Bản .xlsx hôm nay cũng đang
  thu còn 59%, nên đây không phải lỗi mới — nhưng giờ con số đã hiện ra, nó là một câu hỏi
  thật: hoặc chấp nhận, hoặc bớt cột, hoặc sang A3.
```

- [ ] **Step 5: Commit**

```bash
git add docs/superpowers/specs/2026-10-07-in-thang-ra-may-in-design.md \
        docs/superpowers/plans/2026-10-07-in-thang-ra-may-in.md
git commit -m "docs(print): chốt spec theo mã đã ship, và nói rõ còn gì chưa kiểm

Ba chỗ thực thi lệch khỏi thiết kế đều ghi lại chứ không sửa spec cho giống mã:
luật bù tràn chữ của FormGrid, IHasPrimaryButtonText, và số bản kẹp hai lần.

Ba việc còn treo: in thử trên Windows có máy in thật (nghiệm thu cuối, chỉ
người dùng làm được), độ đậm viền trên giấy, và câu hỏi 7,7pt có đọc nổi không.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```
