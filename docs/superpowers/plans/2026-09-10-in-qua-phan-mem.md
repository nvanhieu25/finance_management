# In qua phần mềm (bản .xlsx căn sẵn trang in) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Một nút **In** cố định trên khung giao diện, hai lựa chọn *In sheet này* / *In toàn bộ*, cho ra tệp `.xlsx` đã căn sẵn khổ giấy · lề · ép vừa bề ngang · lặp dòng tiêu đề · đánh số trang, rồi tự mở bằng Excel.

**Architecture:** Nội dung tách hẳn khỏi việc dựng tệp. View-model màn hình mô tả "tôi in ra cái gì" bằng model thuần (`PrintSheet`) ở tầng Application; một `PrintWorkbookWriter` duy nhất biến danh sách `PrintSheet` thành `.xlsx`. ClosedXML không được rò lên tầng Desktop. Ba màn số liệu dùng dạng bảng (`PrintTableSheet`); ba biểu mẫu dùng lại nguyên bộ ghi đã có (`PrintFormSheet`).

**Tech Stack:** .NET 8, C# 12, Avalonia 11.2.1, FluentAvaloniaUI, CommunityToolkit.Mvvm, ClosedXML, xUnit + Shouldly.

**Spec:** `docs/superpowers/specs/2026-09-10-in-qua-phan-mem-design.md` — đọc §4 (kiến trúc), §5 (căn trang) và §6 (nội dung từng sheet) trước khi làm Task 1.

**Hai chỗ kế hoạch cố ý lệch spec — theo kế hoạch, không theo spec:**

1. Spec §5 để `PrintPageProfile` mang `XLPageOrientation` (kiểu của ClosedXML). Kế hoạch dùng enum riêng `PrintOrientation` và để bộ ghi tự ánh xạ, vì `PrintPageProfile` bị tầng Desktop tham chiếu tới, mà Global Constraints cấm ClosedXML rò lên Desktop.
2. Spec §4.1 để `PrintTableSheet` mang `HeaderRowCount`. Kế hoạch bỏ trường đó vì `PrintPageProfile.RepeatRowsTop` đã nói đúng điều ấy — giữ cả hai là hai nguồn sự thật cho cùng một con số.

## Global Constraints

- **Không thêm gói NuGet nào.** ClosedXML đã có trong `ConstructionFinance.Application`.
- **ClosedXML KHÔNG được xuất hiện trong `ConstructionFinance.Desktop`** — không `using ClosedXML`, không kiểu `IXL*` trong bất kỳ tệp Desktop nào. Đây là ranh giới phân lớp, vi phạm là lỗi nghiêm trọng.
- **Đặt tên:** biến cục bộ và tham số `camelCase`; kiểu, phương thức, thuộc tính `PascalCase`; field private `_camelCase`.
- **Chữ hiển thị bằng tiếng Việt**, tiền chỉ VNĐ.
- Font bản in: `Times New Roman` cỡ `13` — dùng lại hằng số `FormExcelExporter.FontName` / `FontSize` đang có, **không** khai lại giá trị mới.
- Tiền bằng `0` ghi ra **ô trống**, đúng quy ước `MoneyText.Format(0m)` trên màn hình.
- **Không** dùng `TwoWay` + `StringFormat` trên ô `decimal`/`DateOnly` trong XAML (bẫy StackOverflow của 6A).
- Avalonia ghim **11.2.1** — `Grid` chưa có `ColumnSpacing`/`RowSpacing`, dùng `Margin` từng con.
- **Không đụng** nhóm code đã chết đang chờ 7E xóa: `FinanceProjectGridViewModel`/`View`, `LineItemRowViewModel`, `FinanceSummaryViewModel`/`View`, `ProjectPageViewModel`, `SummaryPageViewModel`, `ViewModels/TongHop/`, `ViewModels/ThongTri/`, `ExcelExportService`, `ExcelImportService`.
- Build `dotnet build ConstructionFinance.sln`; test `dotnet test ConstructionFinance.sln`. Suite hiện có **614 test xanh, 0 warning** — kết thúc mỗi task phải vẫn xanh và 0 warning.
- Mỗi task kết thúc bằng một commit riêng.

## File Structure

**Tạo mới — `src/ConstructionFinance.Application/Printing/`**

| Tệp | Trách nhiệm |
|---|---|
| `PrintModels.cs` | `PrintCellKind`, `PrintAlign`, `PrintCell`, `PrintColumn`, `PrintRow`, `PrintSheet`, `PrintTableSheet`, `PrintFormSheet`, `FormPayload`. Record thuần, không hành vi, không biết ClosedXML. |
| `PrintPageProfile.cs` | `PrintPageProfile` + `PrintPageProfiles` (6 hồ sơ căn trang tĩnh). |
| `ExcelSheetName.cs` | Làm sạch + chống trùng tên sheet. Tách từ `FinanceExportService.UniqueSheetName` (7E sẽ xóa tệp đó). |
| `PrintWorkbookWriter.cs` | `PrintSheet[]` → `.xlsx`. Nơi DUY NHẤT trong tính năng này chạm ClosedXML. |

**Tạo mới — `src/ConstructionFinance.Desktop/`**

| Tệp | Trách nhiệm |
|---|---|
| `Common/IPrintablePage.cs` | `IPrintablePage`, `IPrintableWorkbook`. |
| `Common/PrintFileName.cs` | Dựng tên tệp gợi ý, bỏ dấu + loại ký tự Windows cấm. |
| `Services/IFileLauncher.cs` | `IFileLauncher` + `FileLauncher`. |

**Sửa:** `Application/Forms/FormExcelExporter.cs` (+`.KsChiGdntt.cs`), `Application/Services/Implementations/FinanceExportService.cs`, `Desktop/ViewModels/Finance/{LedgerRowViewModel, VitriLedgerViewModel, ViTriPageViewModel, ProjectTongHopPageViewModel, YearSettlementPageViewModel, WorkbookPageViewModel, FinanceWorkbookViewModel}.cs`, `Desktop/ViewModels/Finance/Forms/FormPageViewModels.cs`, `Desktop/ViewModels/MainWindowViewModel.cs`, `Desktop/Views/MainWindow.axaml`, `Desktop/App.axaml.cs` (đăng ký DI).

**Test:** `tests/ConstructionFinance.Application.Tests/Printing/` (mới) và `tests/ConstructionFinance.Desktop.Tests/Finance/` (thêm tệp).

---

### Task 1: Model in, hồ sơ căn trang, tên sheet

**Files:**
- Create: `src/ConstructionFinance.Application/Printing/PrintModels.cs`
- Create: `src/ConstructionFinance.Application/Printing/PrintPageProfile.cs`
- Create: `src/ConstructionFinance.Application/Printing/ExcelSheetName.cs`
- Modify: `src/ConstructionFinance.Application/Services/Implementations/FinanceExportService.cs` (bỏ `UniqueSheetName` riêng, gọi helper mới)
- Test: `tests/ConstructionFinance.Application.Tests/Printing/ExcelSheetNameTests.cs`

**Interfaces:**
- Consumes: `ThongTriForm`, `KsChiForm`, `GdnttForm` trong `ConstructionFinance.Application.Forms`.
- Produces: mọi kiểu liệt kê dưới đây. Task 2–6 dùng đúng tên và chữ ký này.

- [ ] **Step 1: Viết `PrintModels.cs`**

```csharp
using ConstructionFinance.Application.Forms;

namespace ConstructionFinance.Application.Printing;

/// Kiểu giá trị của một ô in. Quyết định bộ ghi đặt định dạng số nào — bản in phải
/// cộng được trong Excel, nên tiền và ngày đi vào tệp dưới dạng số/ngày thật chứ
/// không phải chuỗi đã định dạng sẵn.
public enum PrintCellKind { Text, Money, Date, Number }

public enum PrintAlign { Left, Center, Right }

public sealed record PrintCell(PrintCellKind Kind, string? Text, decimal? Number, DateOnly? Date)
{
    public static PrintCell Empty { get; } = new(PrintCellKind.Text, null, null, null);

    public static PrintCell OfText(string? value) => new(PrintCellKind.Text, value, null, null);

    /// Tiền bằng 0 thành ô TRỐNG — đúng quy ước MoneyText.Format(0m) trên màn hình.
    public static PrintCell OfMoney(decimal value) =>
        value == 0m ? Empty : new PrintCell(PrintCellKind.Money, null, value, null);

    public static PrintCell OfNumber(decimal value) => new(PrintCellKind.Number, null, value, null);

    public static PrintCell OfDate(DateOnly? value) =>
        value is null ? Empty : new PrintCell(PrintCellKind.Date, null, null, value);
}

/// Một cột của sheet dạng bảng. WidthChars tính theo KÝ TỰ (đơn vị của Excel),
/// không phải pixel — cố tình không lấy từ LedgerColumnWidths của màn hình.
public sealed record PrintColumn(string Header, double WidthChars, PrintAlign Align);

/// Một dòng. IndentLevel thụt ô đầu tiên theo bậc (Nhóm 0 · Gói 1 · Đợt 2).
public sealed record PrintRow(
    IReadOnlyList<PrintCell> Cells,
    int IndentLevel = 0,
    bool IsBold = false,
    PrintShade Shade = PrintShade.None);

public abstract record PrintSheet(string SheetName, PrintPageProfile Page);

/// Sheet dạng bảng: Sổ Vị trí, Tổng hợp, Quyết toán năm.
/// HeaderRowCount = số dòng đầu vừa in đậm/kẻ khung vừa lặp lại ở mỗi trang.
public sealed record PrintTableSheet(
    string SheetName,
    PrintPageProfile Page,
    IReadOnlyList<PrintColumn> Columns,
    IReadOnlyList<PrintRow> Rows) : PrintSheet(SheetName, Page);

/// Sheet biểu mẫu: dùng lại nguyên bộ ghi trong FormExcelExporter.
/// Đúng MỘT trong ba thuộc tính được khác null.
public sealed record FormPayload(
    ThongTriForm? ThongTri = null,
    KsChiForm? KsChi = null,
    GdnttForm? Gdntt = null);

public sealed record PrintFormSheet(
    string SheetName,
    PrintPageProfile Page,
    FormPayload Form) : PrintSheet(SheetName, Page);
```

- [ ] **Step 2: Viết `PrintPageProfile.cs`**

`XLPageOrientation` là kiểu của ClosedXML nhưng `PrintPageProfile` phải dùng được từ chỗ khác mà không kéo theo ClosedXML — nên dùng enum riêng, bộ ghi tự ánh xạ.

```csharp
namespace ConstructionFinance.Application.Printing;

public enum PrintOrientation { Portrait, Landscape }

/// Căn trang của một sheet. FitToPagesWide = 1 nghĩa là ép vừa đúng một trang bề
/// ngang; FitToPagesTall = 0 nghĩa là dọc bao nhiêu trang cũng được.
public sealed record PrintPageProfile(
    PrintOrientation Orientation,
    int FitToPagesWide,
    int FitToPagesTall,
    int RepeatRowsTop,
    int RepeatColsLeft);

/// Sáu hồ sơ căn trang, gom một chỗ để không trôi khỏi nhau. Xem spec §5.
public static class PrintPageProfiles
{
    /// Sổ Vị trí: 14 cột nên phải ngang; lặp dòng tiêu đề + dòng TỔNG CỘNG, và lặp
    /// hai cột trái (Ngày, Nội dung) để trang sau còn biết dòng nào là dòng nào.
    public static PrintPageProfile ViTriLedger { get; } =
        new(PrintOrientation.Landscape, 1, 0, 2, 2);

    public static PrintPageProfile ProjectTongHop { get; } =
        new(PrintOrientation.Landscape, 1, 0, 1, 2);

    /// Chỉ 4 cột — để dọc, ngang sẽ thừa nửa tờ giấy.
    public static PrintPageProfile YearSettlement { get; } =
        new(PrintOrientation.Portrait, 1, 0, 1, 0);

    // Ép NGANG 1 trang, cao bao nhiêu trang cũng được (spec §5 chỉ có cột "Ép ngang").
    // Ép dọc 1 trang là sai: Excel không có sàn tỉ lệ, Thông tri dài sẽ co tới mức không đọc nổi.
    public static PrintPageProfile ThongTri { get; } = new(PrintOrientation.Portrait, 1, 0, 0, 0);
    public static PrintPageProfile KsChi { get; } = new(PrintOrientation.Portrait, 1, 0, 0, 0);
    public static PrintPageProfile Gdntt { get; } = new(PrintOrientation.Landscape, 1, 0, 0, 0);
}
```

- [ ] **Step 3: Viết test thất bại cho `ExcelSheetName`**

Tạo `tests/ConstructionFinance.Application.Tests/Printing/ExcelSheetNameTests.cs`:

```csharp
using System.Collections.Generic;
using ConstructionFinance.Application.Printing;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Printing;

/// ExcelSheetName tách ra từ FinanceExportService.UniqueSheetName — tệp đó nằm trong
/// danh sách 7E sẽ xóa, mà tính năng in phụ thuộc quy tắc này, nên nó phải sống ở chỗ
/// dùng chung KÈM TEST trước khi 7E chạy.
public class ExcelSheetNameTests
{
    [Theory]
    [InlineData("Vị trí 1", "Vị trí 1")]
    [InlineData("A/B:C*D?E[F]G\\H", "ABCDEFGH")]   // ký tự Excel cấm bị loại
    [InlineData("'Mở đầu", "Mở đầu")]              // dấu nháy đơn đầu chuỗi bị loại
    [InlineData("   ", "Sheet")]                    // rỗng sau khi làm sạch → tên dự phòng
    public void Lam_sach_ten_sheet(string raw, string expected)
    {
        ExcelSheetName.Unique(raw, new HashSet<string>()).ShouldBe(expected);
    }

    [Fact]
    public void Cat_con_31_ky_tu()
    {
        var raw = new string('x', 50);
        ExcelSheetName.Unique(raw, new HashSet<string>()).Length.ShouldBe(31);
    }

    [Fact]
    public void Ten_trung_duoc_danh_so_va_van_khong_qua_31_ky_tu()
    {
        var used = new HashSet<string>();

        ExcelSheetName.Unique("Vị trí 1", used).ShouldBe("Vị trí 1");
        ExcelSheetName.Unique("Vị trí 1", used).ShouldBe("Vị trí 1 (2)");
        ExcelSheetName.Unique("Vị trí 1", used).ShouldBe("Vị trí 1 (3)");

        var longName = new string('y', 31);
        ExcelSheetName.Unique(longName, used);
        ExcelSheetName.Unique(longName, used).Length.ShouldBeLessThanOrEqualTo(31);
    }

    [Fact]
    public void Ghi_ten_da_dung_vao_tap_used()
    {
        var used = new HashSet<string>();
        ExcelSheetName.Unique("Tổng hợp", used);
        used.ShouldContain("Tổng hợp");
    }
}
```

- [ ] **Step 4: Chạy test để xác nhận nó THẤT BẠI**

Chạy: `dotnet test tests/ConstructionFinance.Application.Tests/ConstructionFinance.Application.Tests.csproj --filter "FullyQualifiedName~ExcelSheetNameTests"`

Kỳ vọng: **không biên dịch được** — chưa có `ExcelSheetName`. Đó là RED hợp lệ cho một kiểu chưa tồn tại.

- [ ] **Step 5: Viết `ExcelSheetName.cs`**

Đọc `FinanceExportService.UniqueSheetName` (khoảng dòng 132) trước, giữ nguyên hành vi, chỉ đổi chỗ ở và bổ sung phần cắt 31 ký tự khi đánh số:

```csharp
using System.Text;

namespace ConstructionFinance.Application.Printing;

/// Tên sheet Excel: tối đa 31 ký tự, không được chứa \ / * ? : [ ] và không được
/// bắt đầu bằng dấu nháy đơn. Tách ra từ FinanceExportService (7E sẽ xóa tệp đó)
/// để cả đường xuất cũ lẫn tính năng in dùng chung một quy tắc.
public static class ExcelSheetName
{
    private const int MaxLength = 31;
    private const string Fallback = "Sheet";
    private static readonly char[] Illegal = { '\\', '/', '*', '?', ':', '[', ']' };

    /// Làm sạch `raw`, cắt còn 31 ký tự, và nếu đã có trong `used` thì thêm hậu tố
    /// " (2)", " (3)"… Tên trả về được ghi vào `used`.
    public static string Unique(string? raw, HashSet<string> used)
    {
        var cleaned = Clean(raw);
        if (used.Add(cleaned)) return cleaned;

        for (var n = 2; ; n++)
        {
            var suffix = $" ({n})";
            var trimmed = cleaned.Length + suffix.Length > MaxLength
                ? cleaned[..(MaxLength - suffix.Length)]
                : cleaned;
            var candidate = trimmed + suffix;
            if (used.Add(candidate)) return candidate;
        }
    }

    private static string Clean(string? raw)
    {
        var sb = new StringBuilder();
        foreach (var ch in (raw ?? "").Trim())
            if (Array.IndexOf(Illegal, ch) < 0)
                sb.Append(ch);

        var text = sb.ToString().TrimStart('\'').Trim();
        if (text.Length == 0) return Fallback;
        return text.Length > MaxLength ? text[..MaxLength] : text;
    }
}
```

- [ ] **Step 6: Trỏ `FinanceExportService` sang helper mới**

Trong `src/ConstructionFinance.Application/Services/Implementations/FinanceExportService.cs`: **xóa** phương thức `private static string UniqueSheetName(string raw, HashSet<string> used)` và đổi chỗ gọi ở dòng ~75 thành `ExcelSheetName.Unique(project.Name, usedNames)`. Thêm `using ConstructionFinance.Application.Printing;`.

Nếu hành vi của hàm cũ khác test ở Step 3 ở điểm nào, **giữ hành vi của hàm cũ** và sửa test cho khớp, rồi ghi lại điểm khác biệt trong báo cáo — có test cũ đang phụ thuộc nó.

- [ ] **Step 7: Chạy lại test**

Chạy: `dotnet test tests/ConstructionFinance.Application.Tests/ConstructionFinance.Application.Tests.csproj --filter "FullyQualifiedName~ExcelSheetName"`
Kỳ vọng: **PASS** 7 test.

- [ ] **Step 8: Chạy toàn bộ suite**

Chạy: `dotnet test ConstructionFinance.sln`
Kỳ vọng: 614 cũ + 7 mới = **621 xanh**, 0 warning. Test cũ của `FinanceExportService` phải vẫn xanh — nếu đỏ thì hành vi đã đổi, quay lại Step 6.

- [ ] **Step 9: Commit**

```bash
git add src/ConstructionFinance.Application/Printing src/ConstructionFinance.Application/Services/Implementations/FinanceExportService.cs tests/ConstructionFinance.Application.Tests/Printing
git commit -m "feat(print): model in, hồ sơ căn trang, và helper tên sheet dùng chung"
```

---

### Task 2: `PrintWorkbookWriter` cho sheet dạng bảng

**Files:**
- Create: `src/ConstructionFinance.Application/Printing/PrintWorkbookWriter.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Printing/PrintWorkbookWriterTests.cs`

**Interfaces:**
- Consumes: mọi kiểu của Task 1.
- Produces: `IPrintWorkbookWriter` + `PrintWorkbookWriter`, chữ ký:
  - `void Write(IReadOnlyList<PrintSheet> sheets, Stream target, PrintFooterInfo footer)`
  - `Task WriteAsync(IReadOnlyList<PrintSheet> sheets, string targetPath, PrintFooterInfo footer, CancellationToken ct = default)`
  - `public sealed record PrintFooterInfo(string ProjectName, string? ViTriName)`
  Task 6 gọi `WriteAsync`. **Nhận `Stream` là bắt buộc** — test ghi vào `MemoryStream`, không đụng đĩa.
- Task này CHƯA xử lý `PrintFormSheet`: gặp nó thì `throw new NotSupportedException`. Task 3 thay thân đó.

- [ ] **Step 1: Viết test thất bại**

Tạo `tests/ConstructionFinance.Application.Tests/Printing/PrintWorkbookWriterTests.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using ConstructionFinance.Application.Printing;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Printing;

/// Ghi workbook ra bộ nhớ rồi ĐỌC LẠI bằng chính ClosedXML — kiểm cái tệp thật sự
/// chứa, chứ không kiểm lời hứa của bộ ghi.
public class PrintWorkbookWriterTests
{
    private static readonly PrintFooterInfo Footer = new("Dự án A", "Vị trí 1");

    private static PrintTableSheet SampleSheet() => new(
        "Vị trí 1",
        PrintPageProfiles.ViTriLedger,
        new[]
        {
            new PrintColumn("Ngày", 11, PrintAlign.Center),
            new PrintColumn("Nội dung", 38, PrintAlign.Left),
            new PrintColumn("Tạm ứng", 15, PrintAlign.Right),
        },
        new[]
        {
            new PrintRow(new[] { PrintCell.Empty, PrintCell.OfText("TỔNG CỘNG"), PrintCell.OfMoney(3_750_000m) },
                IndentLevel: 0, IsBold: true, Shade: PrintShade.Light),
            new PrintRow(new[] { PrintCell.Empty, PrintCell.OfText("CHI PHÍ XÂY DỰNG"), PrintCell.Empty },
                IndentLevel: 0, IsBold: true, Shade: PrintShade.VeryLight),
            new PrintRow(new[]
                {
                    PrintCell.OfDate(new DateOnly(2026, 3, 15)),
                    PrintCell.OfText("Tạm ứng lần 1"),
                    PrintCell.OfMoney(3_750_000m),
                },
                IndentLevel: 2),
            new PrintRow(new[] { PrintCell.Empty, PrintCell.OfText("Đợt chưa nhập"), PrintCell.OfMoney(0m) },
                IndentLevel: 2),
        });

    private static XLWorkbook WriteAndReopen(params PrintSheet[] sheets)
    {
        using var buffer = new MemoryStream();
        new PrintWorkbookWriter().Write(sheets, buffer, Footer);
        buffer.Position = 0;
        return new XLWorkbook(buffer);
    }

    [Fact]
    public void Ghi_dung_ten_va_thu_tu_sheet()
    {
        using var wb = WriteAndReopen(
            SampleSheet() with { SheetName = "Tổng hợp" },
            SampleSheet() with { SheetName = "Vị trí 1" });

        wb.Worksheets.Count.ShouldBe(2);
        wb.Worksheet(1).Name.ShouldBe("Tổng hợp");
        wb.Worksheet(2).Name.ShouldBe("Vị trí 1");
    }

    [Fact]
    public void Ten_sheet_trung_duoc_danh_so()
    {
        using var wb = WriteAndReopen(SampleSheet(), SampleSheet());
        wb.Worksheet(2).Name.ShouldBe("Vị trí 1 (2)");
    }

    [Fact]
    public void Dong_dau_la_tieu_de_cot()
    {
        using var wb = WriteAndReopen(SampleSheet());
        var ws = wb.Worksheet(1);

        ws.Cell(1, 1).GetString().ShouldBe("Ngày");
        ws.Cell(1, 2).GetString().ShouldBe("Nội dung");
        ws.Cell(1, 3).GetString().ShouldBe("Tạm ứng");
        ws.Cell(1, 1).Style.Font.Bold.ShouldBeTrue();
    }

    [Fact]
    public void Tien_ghi_thanh_SO_that_kem_dinh_dang_nghin()
    {
        using var wb = WriteAndReopen(SampleSheet());
        var cell = wb.Worksheet(1).Cell(2, 3);   // dòng TỔNG CỘNG, cột Tạm ứng

        cell.DataType.ShouldBe(XLDataType.Number);
        cell.GetDouble().ShouldBe(3_750_000d);
        cell.Style.NumberFormat.Format.ShouldBe("#,##0");
    }

    [Fact]
    public void Tien_bang_khong_ra_o_trong()
    {
        using var wb = WriteAndReopen(SampleSheet());
        wb.Worksheet(1).Cell(5, 3).IsEmpty().ShouldBeTrue();   // dòng "Đợt chưa nhập"
    }

    [Fact]
    public void Ngay_ghi_thanh_NGAY_that()
    {
        using var wb = WriteAndReopen(SampleSheet());
        var cell = wb.Worksheet(1).Cell(4, 1);

        cell.DataType.ShouldBe(XLDataType.DateTime);
        cell.GetDateTime().ShouldBe(new DateTime(2026, 3, 15));
    }

    [Fact]
    public void Dam_va_nen_theo_dung_loai_dong()
    {
        using var wb = WriteAndReopen(SampleSheet());
        var ws = wb.Worksheet(1);

        ws.Cell(2, 2).Style.Font.Bold.ShouldBeTrue();                       // TỔNG CỘNG
        ws.Cell(2, 2).Style.Fill.BackgroundColor.ShouldNotBe(XLColor.NoColor);
        ws.Cell(4, 2).Style.Font.Bold.ShouldBeFalse();                      // dòng Đợt
    }

    [Fact]
    public void Thut_bac_bang_Indent_cua_o_dau_tien_co_noi_dung()
    {
        using var wb = WriteAndReopen(SampleSheet());
        var ws = wb.Worksheet(1);

        ws.Cell(3, 2).Style.Alignment.Indent.ShouldBe(0);   // Nhóm
        ws.Cell(4, 2).Style.Alignment.Indent.ShouldBe(2);   // Đợt
    }

    [Fact]
    public void Dat_dung_can_trang_theo_ho_so()
    {
        using var wb = WriteAndReopen(SampleSheet());
        var page = wb.Worksheet(1).PageSetup;

        page.PaperSize.ShouldBe(XLPaperSize.A4Paper);
        page.PageOrientation.ShouldBe(XLPageOrientation.Landscape);
        page.PagesWide.ShouldBe(1);
        page.PagesTall.ShouldBe(0);
        page.CenterHorizontally.ShouldBeTrue();
    }

    [Fact]
    public void Chan_trang_co_so_trang_va_ten_du_an()
    {
        using var wb = WriteAndReopen(SampleSheet());
        var footer = wb.Worksheet(1).PageSetup.Footer;

        footer.Right.GetText(XLHFOccurrence.AllPages).ShouldContain("&P");
        footer.Right.GetText(XLHFOccurrence.AllPages).ShouldContain("&N");
        footer.Left.GetText(XLHFOccurrence.AllPages).ShouldContain("Dự án A");
        footer.Left.GetText(XLHFOccurrence.AllPages).ShouldContain("Vị trí 1");
    }

    [Fact]
    public void Be_rong_cot_lay_tu_PrintColumn()
    {
        using var wb = WriteAndReopen(SampleSheet());
        wb.Worksheet(1).Column(2).Width.ShouldBe(38d, 0.5);
    }

    [Fact]
    public void Font_dung_Times_New_Roman_13()
    {
        using var wb = WriteAndReopen(SampleSheet());
        var style = wb.Worksheet(1).Cell(4, 2).Style;

        style.Font.FontName.ShouldBe("Times New Roman");
        style.Font.FontSize.ShouldBe(13d);
    }
}
```

- [ ] **Step 2: Chạy test để xác nhận nó THẤT BẠI**

Chạy: `dotnet test tests/ConstructionFinance.Application.Tests/ConstructionFinance.Application.Tests.csproj --filter "FullyQualifiedName~PrintWorkbookWriterTests"`
Kỳ vọng: **không biên dịch được** — chưa có `PrintWorkbookWriter`.

- [ ] **Step 3: Viết `PrintWorkbookWriter.cs`**

Điểm cần chú ý khi hiện thực:

- **Lặp dòng tiêu đề / cột trái** đặt bằng `ws.PageSetup.SetRowsToRepeatAtTop(1, n)` và `SetColumnsToRepeatAtLeft(1, n)`; **bỏ qua khi `n == 0`** (gọi với 0 sẽ ném).
- Dòng tiêu đề chiếm dòng 1, nên dữ liệu bắt đầu ở dòng 2. `RepeatRowsTop = 2` nghĩa là lặp dòng 1 (tiêu đề) và dòng 2 (TỔNG CỘNG).
- **Thụt bậc** dùng `Style.Alignment.Indent`, đặt lên **ô đầu tiên có nội dung Text** của dòng, không đặt lên cả dòng.
- Font/cỡ đặt một lần qua `ws.Style.Font` cho cả sheet, dùng `FormExcelExporter.FontName` / `FontSize`.
- Định dạng tiền dùng `FormExcelExporter.MoneyFormat` (`"#,##0"`), ngày dùng `"dd/MM/yyyy"`.
- Lề: trái/phải `0.5`, trên/dưới `0.6`, `CenterHorizontally = true`.
- Chân trang: `ws.PageSetup.Footer.Left.AddText(...)`, `Footer.Right.AddText("Trang &P/&N")`.
- Tên sheet đi qua `ExcelSheetName.Unique` với một `HashSet<string>` dùng chung cho cả workbook.
- Gặp `PrintFormSheet` → `throw new NotSupportedException("PrintFormSheet chưa được hỗ trợ — xem Task 3.");`

Hằng số `MoneyFormat`/`FontName`/`FontSize` hiện là `internal` trong `FormExcelExporter`, cùng assembly nên gọi được trực tiếp — **đừng khai lại giá trị mới**.

- [ ] **Step 4: Chạy lại test**

Chạy: `dotnet test tests/ConstructionFinance.Application.Tests/ConstructionFinance.Application.Tests.csproj --filter "FullyQualifiedName~PrintWorkbookWriterTests"`
Kỳ vọng: **PASS** 12 test.

- [ ] **Step 5: Chạy toàn bộ suite**

Chạy: `dotnet test ConstructionFinance.sln` — kỳ vọng **633 xanh**, 0 warning.

- [ ] **Step 6: Commit**

```bash
git add src/ConstructionFinance.Application/Printing/PrintWorkbookWriter.cs tests/ConstructionFinance.Application.Tests/Printing/PrintWorkbookWriterTests.cs
git commit -m "feat(print): bộ ghi .xlsx cho sheet dạng bảng, kèm căn trang và chân trang"
```

---

### Task 3: Ghép ba biểu mẫu vào bộ ghi

**Files:**
- Modify: `src/ConstructionFinance.Application/Forms/FormExcelExporter.cs`
- Modify: `src/ConstructionFinance.Application/Forms/FormExcelExporter.KsChiGdntt.cs`
- Modify: `src/ConstructionFinance.Application/Printing/PrintWorkbookWriter.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Printing/PrintFormSheetTests.cs`

**Interfaces:**
- Consumes: `PrintFormSheet`, `FormPayload` (Task 1); `PrintWorkbookWriter.Write` (Task 2).
- Produces: `FormExcelExporter` có thêm ba hàm `internal static void WriteThongTriSheet(IXLWorksheet ws, ThongTriForm form)`, `WriteKsChiSheet(IXLWorksheet ws, KsChiForm form)`, `WriteGdnttSheet(IXLWorksheet ws, GdnttForm form)`. API công khai `ExportThongTriAsync/ExportKsChiAsync/ExportGdnttAsync` **không đổi**.

- [ ] **Step 1: Tách bộ ghi hiện có ra khỏi việc tạo workbook**

Hiện `WriteThongTri(ThongTriForm form, string targetPath)` tự `new XLWorkbook()`, tự `AddWorksheet`, tự `SaveAs`. Tách làm hai:

```csharp
    private static void WriteThongTri(ThongTriForm form, string targetPath)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Thông tri");
        WriteThongTriSheet(ws, form);
        wb.SaveAs(targetPath);
    }

    /// Vẽ biểu mẫu lên một worksheet CÓ SẴN. Tách ra để PrintWorkbookWriter ghép được
    /// biểu mẫu vào workbook nhiều sheet mà không phải chép lại bố cục.
    internal static void WriteThongTriSheet(IXLWorksheet ws, ThongTriForm form)
    {
        // ... nguyên phần thân cũ, bỏ các dòng tạo workbook / AddWorksheet / SaveAs ...
    }
```

Làm y hệt cho `KsChi` và `Gdntt` trong `FormExcelExporter.KsChiGdntt.cs`. **Không đổi một ô nào của bố cục** — đây thuần túy là đổi chỗ. Test hiện có của ba biểu mẫu phải xanh nguyên không sửa; nếu phải sửa test cũ thì bạn đã đổi hành vi, quay lại.

- [ ] **Step 2: Chạy test biểu mẫu cũ để xác nhận không đổi hành vi**

Chạy: `dotnet test tests/ConstructionFinance.Application.Tests/ConstructionFinance.Application.Tests.csproj --filter "FullyQualifiedName~FormExcelExporter"`
Kỳ vọng: **PASS**, không sửa một dòng test nào.

- [ ] **Step 3: Viết test thất bại cho `PrintFormSheet`**

Tạo `tests/ConstructionFinance.Application.Tests/Printing/PrintFormSheetTests.cs`. Dựng một `ThongTriForm` tối thiểu (xem các test `FormExcelExporter` hiện có để biết cách dựng), rồi:

```csharp
    [Fact]
    public void Ghep_duoc_bieu_mau_va_bang_trong_cung_mot_workbook()
    {
        var sheets = new PrintSheet[]
        {
            SampleTableSheet(),                                  // helper riêng của tệp test này
            new PrintFormSheet("Thông tri", PrintPageProfiles.ThongTri,
                new FormPayload(ThongTri: SampleThongTriForm())),
        };

        using var buffer = new MemoryStream();
        new PrintWorkbookWriter().Write(sheets, buffer, new PrintFooterInfo("Dự án A", null));
        buffer.Position = 0;

        using var wb = new XLWorkbook(buffer);
        wb.Worksheets.Count.ShouldBe(2);
        wb.Worksheet(2).Name.ShouldBe("Thông tri");
        wb.Worksheet(2).Cell(4, 1).GetString().ShouldBe("THÔNG TRI");
        wb.Worksheet(2).PageSetup.PageOrientation.ShouldBe(XLPageOrientation.Portrait);
        wb.Worksheet(2).PageSetup.PagesTall.ShouldBe(0);   // sửa sau review Task 3: không ép dọc
    }

    [Fact]
    public void FormPayload_rong_thi_nem_loi_ro_rang()
    {
        var sheets = new PrintSheet[]
        {
            new PrintFormSheet("Trống", PrintPageProfiles.ThongTri, new FormPayload()),
        };

        using var buffer = new MemoryStream();
        Should.Throw<ArgumentException>(() =>
            new PrintWorkbookWriter().Write(sheets, buffer, new PrintFooterInfo("Dự án A", null)));
    }
```

- [ ] **Step 4: Chạy test để xác nhận nó THẤT BẠI**

Kỳ vọng: `NotSupportedException` từ thân tạm của Task 2.

- [ ] **Step 5: Hiện thực nhánh `PrintFormSheet` trong writer**

Thay `throw new NotSupportedException(...)` bằng: tạo worksheet, gọi đúng một trong ba `Write*Sheet` theo thuộc tính khác null của `FormPayload`, rồi **áp cùng một hồ sơ căn trang + chân trang** như sheet bảng (dùng chung hàm áp căn trang của Task 2 — không chép lại). `FormPayload` không có thuộc tính nào khác null, hoặc có nhiều hơn một, thì `throw new ArgumentException` với thông báo nêu rõ tên sheet.

- [ ] **Step 6: Chạy lại test rồi chạy toàn bộ suite**

Chạy: `dotnet test ConstructionFinance.sln` — kỳ vọng **635 xanh**, 0 warning.

- [ ] **Step 7: Commit**

```bash
git add src/ConstructionFinance.Application tests/ConstructionFinance.Application.Tests/Printing
git commit -m "feat(print): ghép ba biểu mẫu vào bộ ghi chung, tách bộ vẽ khỏi việc tạo workbook"
```

---

### Task 4: Ba màn số liệu khai báo mình in được gì

**Files:**
- Create: `src/ConstructionFinance.Desktop/Common/IPrintablePage.cs`
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Finance/LedgerRowViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Finance/VitriLedgerViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Finance/WorkbookPageViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Finance/ViTriPageViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Finance/ProjectTongHopPageViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Finance/YearSettlementPageViewModel.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Finance/PrintablePageTests.cs`

**Interfaces:**
- Consumes: `PrintTableSheet`, `PrintColumn`, `PrintRow`, `PrintCell`, `PrintPageProfiles` (Task 1).
- Produces:

```csharp
public interface IPrintablePage
{
    /// Null nghĩa là màn này không in được — nút In mờ đi.
    string? PrintSheetName { get; }
    PrintSheet? BuildPrintSheet();
}

public interface IPrintableWorkbook
{
    string PrintWorkbookName { get; }
    bool CanPrintAll { get; }                      // rẻ, không tác dụng phụ
    IReadOnlyList<PrintSheet> BuildAllSheets();   // đường hành động: tự làm mới tab tổng hợp
}
```

Task 5 implement `IPrintableWorkbook`; Task 6 dùng cả hai.

- [ ] **Step 1: Thêm `Aggregate` vào `LedgerRowViewModel`**

Bốn cột tính sẵn (`DisbursedNsnnText`, `DisbursedNgoaiHoiText`, `UnrecoveredAdvanceText`, `TotalDisbursedText`) hiện chỉ tồn tại dưới dạng **chuỗi**. Bản in cần số thật (Global Constraints: tiền phải cộng được trong Excel). Đừng parse chuỗi ngược lại — giữ luôn bản gốc:

Trong `LedgerRowViewModel.cs`, thêm cạnh bốn thuộc tính chuỗi đó:

```csharp
    /// Bản gốc của bốn cột tính sẵn ở trên, giữ lại để bản in ghi được SỐ thật thay vì
    /// chuỗi đã định dạng. Null với dòng Đợt (đợt không có số tổng hợp riêng).
    public TreeAggregate? Aggregate { get; set; }
```

Trong `VitriLedgerViewModel.ApplyAggregate` (khoảng dòng 441), thêm dòng đầu tiên:

```csharp
    private static void ApplyAggregate(LedgerRowViewModel row, TreeAggregate agg)
    {
        row.Aggregate = agg;
        row.DisbursedNsnnText = MoneyText.Format(agg.DisbursedNSNN);
        // ... ba dòng còn lại giữ nguyên ...
    }
```

- [ ] **Step 2: Viết test thất bại**

Tạo `tests/ConstructionFinance.Desktop.Tests/Finance/PrintablePageTests.cs`.

Ba helper mà mọi test dưới đây dùng, viết ngay trong tệp test mới này (**đừng sửa `VitriLedgerViewTests.cs`**):

```csharp
    /// Một nhóm / một gói / một đợt — đủ chạm mọi loại dòng, mọi ô tiền đều 0.
    /// Cùng hình dạng với BuildOneOfEachRowKindFixture trong VitriLedgerViewTests.cs.
    private static ViTri Fixture()
    {
        var viTri = new ViTri { Name = "Vị trí 1 — Nhà điều hành" };
        var group = new CostGroup { Name = "CHI PHÍ XÂY DỰNG", SortOrder = 0 };
        var package = new Package { Name = "Gói thầu số 01", SortOrder = 0 };
        package.Dots.Add(new Dot { Content = "Đợt 1", SortOrder = 0, Fund = Fund.NSNN });
        group.Packages.Add(package);
        viTri.CostGroups.Add(group);
        return viTri;
        // Rows: [0] Total, [1] Nhóm, [2] Gói, [3] Đợt
    }

    /// Như Fixture() nhưng đợt có tiền, để các cột tính sẵn khác 0 — cần cho test
    /// "in ra SỐ thật", vì tiền bằng 0 ra ô trống nên không phân biệt được kiểu.
    private static ViTri FixtureWithMoney()
    {
        var viTri = Fixture();
        var dot = viTri.CostGroups[0].Packages[0].Dots[0];
        dot.Advance = 3_750_000m;
        dot.Date = new DateOnly(2026, 3, 15);
        return viTri;
    }
```

Các test bắt buộc:

```csharp
    [Fact]
    public void So_Vi_tri_in_ra_dung_so_dong_va_dung_thu_tu_nhu_tren_man_hinh()
    {
        var ledger = new VitriLedgerViewModel(Fixture());
        var page = new ViTriPageViewModel(ledger);

        var sheet = (PrintTableSheet)page.BuildPrintSheet()!;

        // Bản in phải khớp màn hình: mỗi dòng hiển thị là một dòng in, cùng thứ tự.
        sheet.Rows.Count.ShouldBe(ledger.Rows.Count);
        sheet.Columns.Count.ShouldBe(14);
        sheet.Columns[0].Header.ShouldBe("Ngày");
        sheet.Columns[13].Header.ShouldBe("Tổng đã GN");
        sheet.Page.ShouldBe(PrintPageProfiles.ViTriLedger);
    }

    [Fact]
    public void Cot_tinh_san_in_ra_SO_that_chu_khong_phai_chuoi()
    {
        var ledger = new VitriLedgerViewModel(FixtureWithMoney());   // có ít nhất một đợt có tiền
        var sheet = (PrintTableSheet)new ViTriPageViewModel(ledger).BuildPrintSheet()!;

        var totalRow = sheet.Rows[0];                                 // dòng TỔNG CỘNG
        var cell = totalRow.Cells[13];                                // cột Tổng đã GN

        cell.Kind.ShouldBe(PrintCellKind.Money);
        cell.Number.ShouldBe(ledger.Rows[0].Aggregate!.TotalDisbursed);
    }

    [Fact]
    public void Thut_bac_dung_theo_loai_dong()
    {
        var ledger = new VitriLedgerViewModel(Fixture());
        var sheet = (PrintTableSheet)new ViTriPageViewModel(ledger).BuildPrintSheet()!;

        for (var i = 0; i < ledger.Rows.Count; i++)
        {
            var expected = ledger.Rows[i].Kind switch
            {
                RowKind.Total or RowKind.Group => 0,
                RowKind.Package => 1,
                _ => 2,
            };
            sheet.Rows[i].IndentLevel.ShouldBe(expected, $"dòng {i} ({ledger.Rows[i].Kind})");
        }
    }

    [Fact]
    public void Ten_sheet_cua_So_Vi_tri_la_ten_vi_tri()
    {
        var ledger = new VitriLedgerViewModel(Fixture());
        new ViTriPageViewModel(ledger).PrintSheetName.ShouldBe(ledger.Name);
    }

    [Fact]
    public void Tong_hop_in_ra_dung_so_dong_dang_hien()
    {
        var inner = new ProjectTongHopViewModel();
        inner.Refresh(new[] { Fixture() });
        var sheet = (PrintTableSheet)new ProjectTongHopPageViewModel(inner).BuildPrintSheet()!;

        sheet.SheetName.ShouldBe("Tổng hợp");
        sheet.Rows.Count.ShouldBe(inner.Rows.Count);
        sheet.Page.ShouldBe(PrintPageProfiles.ProjectTongHop);
    }

    [Fact]
    public void Quyet_toan_nam_in_ra_bon_cot_va_dung_so_dong()
    {
        var inner = new YearSettlementViewModel();
        inner.Refresh(new[] { Fixture() });
        var sheet = (PrintTableSheet)new YearSettlementPageViewModel(inner).BuildPrintSheet()!;

        sheet.SheetName.ShouldBe("Quyết toán năm");
        sheet.Columns.Count.ShouldBe(4);
        sheet.Rows.Count.ShouldBe(inner.Rows.Count);
        sheet.Page.ShouldBe(PrintPageProfiles.YearSettlement);
    }

    [Fact]
    public void Trang_khong_in_duoc_tra_ve_null()
    {
        // Lớp cha mặc định là "không in được", nên trang nào chưa override thì nút In mờ.
        WorkbookPageViewModel page = new YearSettlementPageViewModel(new YearSettlementViewModel());
        page.ShouldBeAssignableTo<IPrintablePage>();
    }
```

- [ ] **Step 3: Chạy test để xác nhận nó THẤT BẠI**

Chạy: `dotnet test tests/ConstructionFinance.Desktop.Tests/ConstructionFinance.Desktop.Tests.csproj --filter "FullyQualifiedName~PrintablePageTests"`
Kỳ vọng: không biên dịch được — chưa có `IPrintablePage`.

- [ ] **Step 4: Viết `IPrintablePage.cs` và cho `WorkbookPageViewModel` mặc định "không in được"**

Trong `WorkbookPageViewModel`, thêm:

```csharp
    /// Mặc định: trang không in được. Trang nào in được thì override cả hai.
    public virtual string? PrintSheetName => null;
    public virtual PrintSheet? BuildPrintSheet() => null;
```

và cho lớp khai báo `: ViewModelBase, IPrintablePage`.

- [ ] **Step 5: Override ở ba trang bảng**

`ViTriPageViewModel` — 14 cột đúng thứ tự và bề rộng (ký tự):

| # | Tiêu đề | Rộng | Canh |
|---|---|---|---|
| 0 | Ngày | 11 | Center |
| 1 | Nội dung | 38 | Left |
| 2 | Nhà thầu / TK | 22 | Left |
| 3 | Giá trị HĐ | 15 | Right |
| 4 | Phụ lục | 13 | Right |
| 5 | Số CT | 11 | Center |
| 6 | Tạm ứng | 15 | Right |
| 7 | KLHT | 15 | Right |
| 8 | Thu hồi | 14 | Right |
| 9 | Nguồn | 11 | Center |
| 10 | GN NSNN | 15 | Right |
| 11 | GN Ngoại hối | 15 | Right |
| 12 | Số dư TU | 14 | Right |
| 13 | Tổng đã GN | 15 | Right |

Ô theo loại dòng (ô nào không có dữ liệu thì `PrintCell.Empty`):

- **Total / Group**: cột 1 = `Name`; cột 10–13 = `Aggregate` (`DisbursedNSNN`, `DisbursedNgoaiHoi`, `UnrecoveredAdvance`, `TotalDisbursed`). `IsBold = true`; Total `Shade = PrintShade.Light`, Group `Shade = PrintShade.VeryLight` (spec §6.1 cho ba mức nền).
- **Package**: cột 1 = `Name`; cột 2 = `ContractorName` + `"\n"` + `BankAccount` (bỏ phần rỗng, cả hai rỗng thì `Empty`); cột 3 = `ContractValue`; cột 4 = `ContractAnnexValue`; cột 10–13 = `Aggregate`. `IsBold = true`.
- **Entry**: cột 0 = `Date`; cột 1 = `Content`; cột 5 = `DocumentNo`; cột 6 = `Advance`; cột 7 = `PaymentKLHT`; cột 8 = `RecoveredAdvance`; cột 9 = `FundText`. `IsBold = false`.

`IndentLevel`: Total/Group = 0, Package = 1, Entry = 2.

`PrintSheetName` = `Ledger.Name`; rỗng thì `"(vị trí mới)"` (dùng lại quy tắc `DisplayName` đã có trong lớp).

`ProjectTongHopPageViewModel` — 13 cột từ `SummaryDisplayRow`. **Chín cột tiền là `PrintCell.OfMoney`**, đọc số gốc qua `SummaryDisplayRow.Source` (spec §4.2: ô mang giá trị có kiểu, tệp phải cộng được trong Excel). Bốn cột còn lại — `STT`, `Tên vị trí`, `Đơn vị thi công`, `Quyết toán năm` — là `OfText`. `OfMoney` cho ra đúng cách nhóm nghìn và đúng quy ước 0-ra-ô-trống như `MoneyText`, nên bản in vẫn khớp màn hình:
`STT`(5,Center) · `Tên vị trí`(30,Left) · `Đơn vị thi công`(24,Left) · `Giá trị HĐ, PL`(16,Right) · `Tạm ứng`(16,Right) · `TT KLHT`(16,Right) · `Thu hồi`(15,Right) · `TT cho nhà thầu`(16,Right) · `Tạm ứng chưa thu hồi`(16,Right) · `GN Dự phòng NSNN`(16,Right) · `GN Quỹ DTNH`(16,Right) · `Tổng đã giải ngân`(16,Right) · `Quyết toán năm`(28,Left).
`IsBold = row.IsBold`. `SheetName = "Tổng hợp"`.

`YearSettlementPageViewModel` — 4 cột từ `YearSettlementRow`. `Năm` là `OfText`; **ba cột tiền là `OfMoney`** (spec §4.2), đọc số gốc giữ thêm trên `YearSettlementRow`:
`Năm`(12,Center) · `NSNN`(18,Right) · `Ngoại hối`(18,Right) · `Tổng`(18,Right). `IsBold = row.IsBold`. `SheetName = "Quyết toán năm"`.

- [ ] **Step 6: Chạy lại test rồi chạy toàn bộ suite**

Chạy: `dotnet test ConstructionFinance.sln` — kỳ vọng **642 xanh**, 0 warning.

- [ ] **Step 7: Kiểm tra ranh giới phân lớp**

Chạy: `grep -rn "ClosedXML\|IXLWorksheet\|XLWorkbook" src/ConstructionFinance.Desktop --include=*.cs | grep -v obj`
Kỳ vọng: **không có kết quả nào**. Có kết quả là vi phạm Global Constraints, phải sửa trước khi commit.

- [ ] **Step 8: Commit**

```bash
git add src/ConstructionFinance.Desktop tests/ConstructionFinance.Desktop.Tests/Finance/PrintablePageTests.cs
git commit -m "feat(print): ba màn số liệu khai báo nội dung in qua IPrintablePage"
```

---

### Task 5: Ba biểu mẫu + "In toàn bộ"

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/FormPageViewModels.cs`
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceWorkbookViewModel.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Finance/PrintWorkbookTests.cs`

**Interfaces:**
- Consumes: `IPrintablePage`, `IPrintableWorkbook` (Task 4); `PrintFormSheet`, `FormPayload`, `PrintPageProfiles` (Task 1).
- Produces: `FinanceWorkbookViewModel` implement **cả hai** interface. `IPrintablePage` của nó ủy quyền cho `SelectedViTriPage`.

- [ ] **Step 1: Viết test thất bại**

Tạo `tests/ConstructionFinance.Desktop.Tests/Finance/PrintWorkbookTests.cs`.

Hai helper dựng `FinanceWorkbookViewModel` phải viết trong tệp này. `FinanceWorkbookViewModel` nhận **sáu** service qua hàm dựng (`IFinanceProjectService`, `SummaryPageViewModel`, `IDialogService`, `IServiceProvider`, `IFormExcelExporter`, `ICompanyInfoService`) — dùng bản giả bằng Moq (đã có sẵn trong dự án test), và nạp dự án bằng chính đường nạp mà app dùng, đừng chọc thẳng vào `ViTriPages`. `BuildWorkbookWithTwoViTri()` dựng một dự án hai Vị trí (dùng `Fixture()` như Task 4, đổi tên thành "Vị trí 1"/"Vị trí 2"). `BuildWorkbookWhereGdnttHasNoPackage()` dựng dự án mà tab GĐNTT chưa chọn gói thầu — tức `GdnttFormViewModel.CanExport == false`; đọc view-model đó để biết điều kiện chính xác.

Nếu `FinanceWorkbookViewModel` khó dựng trong test đến mức phải sửa mã sản phẩm cho dễ test, **dừng lại và báo cáo BLOCKED** kèm mô tả vướng ở đâu — đừng tự ý đổi kiến trúc.

Các test bắt buộc:

```csharp
    [Fact]
    public void In_toan_bo_ra_dung_thu_tu_sheet_nhu_Mau()
    {
        var workbook = BuildWorkbookWithTwoViTri();          // helper riêng của tệp test này

        var names = workbook.BuildAllSheets().Select(s => s.SheetName).ToList();

        names.ShouldBe(new[]
        {
            "Tổng hợp", "Vị trí 1", "Vị trí 2", "Quyết toán năm",
            "Thông tri", "KS chi", "GĐNTT",
        });
    }

    [Fact]
    public void In_sheet_nay_uy_quyen_cho_tab_dang_mo()
    {
        var workbook = BuildWorkbookWithTwoViTri();
        workbook.SelectedViTriPage = workbook.ViTriPages.OfType<ViTriPageViewModel>().Last();

        ((IPrintablePage)workbook).PrintSheetName.ShouldBe("Vị trí 2");
    }

    [Fact]
    public void Doi_tab_thi_bao_PrintSheetName_da_doi()
    {
        var workbook = BuildWorkbookWithTwoViTri();
        var changed = new List<string>();
        workbook.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");

        workbook.SelectedViTriPage = workbook.ViTriPages.OfType<ViTriPageViewModel>().Last();

        // Không có tín hiệu này thì nút In ở khung chung không cập nhật được trạng thái.
        changed.ShouldContain(nameof(IPrintablePage.PrintSheetName));
    }

    [Fact]
    public void GDNTT_chua_chon_goi_thau_thi_bi_bo_qua_khi_in_toan_bo()
    {
        var workbook = BuildWorkbookWhereGdnttHasNoPackage();

        var names = workbook.BuildAllSheets().Select(s => s.SheetName).ToList();

        names.ShouldNotContain("GĐNTT");
        names.ShouldContain("Thông tri");     // các sheet khác vẫn ra bình thường
    }
```

- [ ] **Step 2: Chạy test để xác nhận nó THẤT BẠI**

Chạy: `dotnet test tests/ConstructionFinance.Desktop.Tests/ConstructionFinance.Desktop.Tests.csproj --filter "FullyQualifiedName~PrintWorkbookTests"`

- [ ] **Step 3: Override ở ba trang biểu mẫu**

Trong `FormPageViewModels.cs`, mỗi lớp trả về `PrintFormSheet` bọc model biểu mẫu mà view-model bên trong đã dựng sẵn:

- `ThongTriFormPageViewModel` → `SheetName = "Thông tri"`, `PrintPageProfiles.ThongTri`, `new FormPayload(ThongTri: Inner.BuildForm())`
- `KsChiFormPageViewModel` → `"KS chi"`, `PrintPageProfiles.KsChi`
- `GdnttFormPageViewModel` → `"GĐNTT"`, `PrintPageProfiles.Gdntt`, và **trả về `null` khi `Inner.CanExport == false`** (chưa chọn gói thầu) — đúng như nút "Xuất Excel" đang mờ trong trường hợp đó.

Tên hàm dựng model trên ba view-model bên trong có thể khác `BuildForm()` — **đọc `ThongTriFormViewModel` / `KsChiFormViewModel` / `GdnttFormViewModel` để lấy đúng tên**, đừng đoán. Nếu chúng chỉ dựng model bên trong `ExportCommand`, hãy tách phần dựng ra một phương thức công khai rồi cho `ExportCommand` gọi lại — **không nhân bản logic dựng**.

- [ ] **Step 4: Cho `FinanceWorkbookViewModel` implement hai interface**

```csharp
public partial class FinanceWorkbookViewModel : ViewModelBase, IPrintablePage, IPrintableWorkbook
{
    // Ủy quyền: khung chung chỉ biết một interface, không cần biết đến khái niệm tab.
    public string? PrintSheetName => (SelectedViTriPage as IPrintablePage)?.PrintSheetName;
    public PrintSheet? BuildPrintSheet() => (SelectedViTriPage as IPrintablePage)?.BuildPrintSheet();

    public string PrintWorkbookName => SelectedProjectCode is { Length: > 0 } code
        ? code
        : SelectedProjectName;

    /// Thứ tự sheet theo Mẫu.xlsx: Tổng hợp → Vị trí 1..N → Quyết toán năm → 3 biểu mẫu.
    /// Trang nào trả null (ví dụ GĐNTT chưa chọn gói thầu) thì bỏ qua, không chặn cả lô.
    public IReadOnlyList<PrintSheet> BuildAllSheets() =>
        ViTriPages.OfType<IPrintablePage>()
                  .Select(p => p.BuildPrintSheet())
                  .OfType<PrintSheet>()
                  .ToList();
}
```

`ViTriPages` đã giữ đúng thứ tự Mẫu (Tổng hợp pinned đầu, Quyết toán năm và ba biểu mẫu ở cuối) — **kiểm chứng lại bằng cách đọc chỗ dựng `ViTriPages`** trước khi tin; nếu thứ tự thực tế khác, sắp lại trong `BuildAllSheets` chứ đừng đổi thứ tự tab đang hiển thị.

Trong `OnSelectedViTriPageChanged` (đã có sẵn, khoảng dòng 357), thêm:

```csharp
        OnPropertyChanged(nameof(PrintSheetName));
```

- [ ] **Step 5: Chạy lại test rồi chạy toàn bộ suite**

Chạy: `dotnet test ConstructionFinance.sln` — kỳ vọng **646 xanh**, 0 warning.

- [ ] **Step 6: Commit**

```bash
git add src/ConstructionFinance.Desktop tests/ConstructionFinance.Desktop.Tests/Finance/PrintWorkbookTests.cs
git commit -m "feat(print): ba biểu mẫu khai báo nội dung in, workbook gom sheet cho In toàn bộ"
```

---

### Task 6: Nút In trên khung chung

**Files:**
- Create: `src/ConstructionFinance.Desktop/Services/IFileLauncher.cs`
- Create: `src/ConstructionFinance.Desktop/Common/PrintFileName.cs`
- Modify: `src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/Views/MainWindow.axaml`
- Modify: `src/ConstructionFinance.Desktop/App.axaml.cs` (đăng ký `IFileLauncher`, `IPrintWorkbookWriter`)
- Test: `tests/ConstructionFinance.Desktop.Tests/PrintFileNameTests.cs`, `tests/ConstructionFinance.Desktop.Tests/MainWindowPrintCommandTests.cs`

**Interfaces:**
- Consumes: `IPrintablePage`, `IPrintableWorkbook` (Task 4–5); `IPrintWorkbookWriter`, `PrintFooterInfo` (Task 2); `IDialogService.PickSaveFileAsync(title, defaultFileName, extensions)` (đã có).
- Produces: đây là task cuối, không có gì cho task sau.

- [ ] **Step 1: Viết test thất bại cho `PrintFileName`**

```csharp
    [Theory]
    [InlineData("DA-01", "Vị trí 1", "DA-01-Vi tri 1")]
    [InlineData("DA/01", "A:B*C", "DA01-ABC")]
    [InlineData("", "Tổng hợp", "Tong hop")]
    public void Bo_dau_va_loai_ky_tu_Windows_cam(string prefix, string sheet, string expectedStem)
    {
        var name = PrintFileName.Suggest(prefix, sheet, new DateTime(2026, 9, 10, 14, 32, 0));
        name.ShouldBe($"{expectedStem}-20260910-1432.xlsx");
    }
```

- [ ] **Step 2: Chạy test để xác nhận nó THẤT BẠI**, rồi viết `PrintFileName.Suggest(string? prefix, string sheetName, DateTime now)`: bỏ dấu tiếng Việt (chuẩn hóa `FormD` rồi loại `NonSpacingMark`, riêng `đ`/`Đ` → `d`/`D`), loại `\ / : * ? " < > |`, gộp khoảng trắng, nối `prefix-sheet-yyyyMMdd-HHmm.xlsx`, `prefix` rỗng thì bỏ luôn cả dấu gạch.

- [ ] **Step 3: Viết `IFileLauncher`**

```csharp
namespace ConstructionFinance.Desktop.Services;

public interface IFileLauncher
{
    /// false nghĩa là hệ điều hành không mở được — người gọi hiện đường dẫn thay thế.
    bool TryOpen(string path);
}

public sealed class FileLauncher : IFileLauncher
{
    public bool TryOpen(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Không mở được tệp {Path}", path);
            return false;
        }
    }
}
```

- [ ] **Step 4: Viết test thất bại cho hai lệnh In**

Tạo `tests/ConstructionFinance.Desktop.Tests/MainWindowPrintCommandTests.cs` với `IDialogService`, `IFileLauncher`, `IPrintWorkbookWriter` giả:

```csharp
    [Fact]
    public void Nut_In_mo_khi_man_hien_tai_khong_in_duoc()
    {
        var vm = CreateMainWindowViewModel();
        vm.CurrentContent = new object();                    // màn không implement IPrintablePage

        vm.PrintCurrentSheetCommand.CanExecute(null).ShouldBeFalse();
        vm.PrintAllCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public void Nut_In_sang_khi_man_hien_tai_in_duoc()
    {
        var vm = CreateMainWindowViewModel();
        vm.CurrentContent = new FakePrintablePage("Vị trí 1");

        vm.PrintCurrentSheetCommand.CanExecute(null).ShouldBeTrue();
        vm.PrintAllCommand.CanExecute(null).ShouldBeFalse();  // không phải IPrintableWorkbook
    }

    [Fact]
    public void PrintSheetName_doi_thi_trang_thai_nut_cap_nhat_lai()
    {
        var vm = CreateMainWindowViewModel();
        var page = new FakePrintablePage(null);
        vm.CurrentContent = page;
        vm.PrintCurrentSheetCommand.CanExecute(null).ShouldBeFalse();

        page.SetSheetName("Vị trí 2");                        // bắn PropertyChanged

        vm.PrintCurrentSheetCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public async Task In_sheet_nay_ghi_tep_roi_mo_bang_ung_dung_mac_dinh()
    {
        var writer = new FakeWriter();
        var launcher = new FakeLauncher();
        var vm = CreateMainWindowViewModel(writer, launcher, savePath: "/tmp/x.xlsx");
        vm.CurrentContent = new FakePrintablePage("Vị trí 1");

        await vm.PrintCurrentSheetCommand.ExecuteAsync(null);

        writer.WrittenSheets.Count.ShouldBe(1);
        launcher.Opened.ShouldBe("/tmp/x.xlsx");
    }

    [Fact]
    public async Task Nguoi_dung_huy_hop_thoai_thi_khong_ghi_gi()
    {
        var writer = new FakeWriter();
        var vm = CreateMainWindowViewModel(writer, savePath: null);   // null = bấm Hủy
        vm.CurrentContent = new FakePrintablePage("Vị trí 1");

        await vm.PrintCurrentSheetCommand.ExecuteAsync(null);

        writer.WrittenSheets.ShouldBeEmpty();
    }
```

- [ ] **Step 5: Chạy test để xác nhận nó THẤT BẠI**, rồi hiện thực trong `MainWindowViewModel`

```csharp
    [RelayCommand(CanExecute = nameof(CanPrintCurrentSheet))]
    private Task PrintCurrentSheetAsync()
    {
        var page = (IPrintablePage)CurrentContent!;
        var sheet = page.BuildPrintSheet();
        return sheet is null
            ? Task.CompletedTask
            : PrintAsync(new[] { sheet }, page.PrintSheetName!);
    }

    private bool CanPrintCurrentSheet() =>
        CurrentContent is IPrintablePage { PrintSheetName: { Length: > 0 } };

    [RelayCommand(CanExecute = nameof(CanPrintAll))]
    private Task PrintAllAsync()
    {
        var workbook = (IPrintableWorkbook)CurrentContent!;
        var sheets = workbook.BuildAllSheets();
        return sheets.Count == 0 ? Task.CompletedTask : PrintAsync(sheets, "Toan bo");
    }

    private bool CanPrintAll() =>
        CurrentContent is IPrintableWorkbook wb && wb.CanPrintAll;
        // KHÔNG gọi BuildAllSheets() ở đây: nó làm mới tab tổng hợp, nên đặt trong
        // CanExecute là nhét tác dụng phụ vào một phép hỏi bị gọi rất thường xuyên.

    /// Đường chung của cả hai lệnh: chọn nơi lưu → ghi → mở bằng ứng dụng mặc định.
    private async Task PrintAsync(IReadOnlyList<PrintSheet> sheets, string sheetLabel)
    {
        var dialog = _services.GetRequiredService<IDialogService>();
        var writer = _services.GetRequiredService<IPrintWorkbookWriter>();
        var launcher = _services.GetRequiredService<IFileLauncher>();

        var prefix = (CurrentContent as IPrintableWorkbook)?.PrintWorkbookName ?? "";
        var suggested = PrintFileName.Suggest(prefix, sheetLabel, DateTime.Now);

        var path = await dialog.PickSaveFileAsync("In ra Excel", suggested, new[] { ".xlsx" });
        if (path is null) return;          // người dùng bấm Hủy — im lặng, không báo lỗi

        var footer = new PrintFooterInfo(prefix, sheets.Count == 1 ? sheetLabel : null);
        try
        {
            await writer.WriteAsync(sheets, path, footer);
        }
        catch (IOException)
        {
            await dialog.ShowMessageAsync("In ra Excel",
                "Tệp đang được mở trong chương trình khác. Đóng tệp rồi thử lại.");
            return;
        }

        if (!launcher.TryOpen(path))
            await dialog.ShowMessageAsync("In ra Excel", $"Đã lưu tệp tại:\n{path}");
    }
```

`IDialogService.ShowMessageAsync(string title, string message)` đã có sẵn — đã kiểm chứng, dùng thẳng.

Theo dõi đổi trạng thái: trong `partial void OnCurrentContentChanged(object? oldValue, object? newValue)`, gỡ handler khỏi `oldValue` rồi gắn vào `newValue` nếu nó là `INotifyPropertyChanged`; handler thấy `PrintSheetName` đổi thì gọi `PrintCurrentSheetCommand.NotifyCanExecuteChanged()` và `PrintAllCommand.NotifyCanExecuteChanged()`. Cũng gọi cặp `NotifyCanExecuteChanged` này ngay trong chính `OnCurrentContentChanged`, vì đổi màn là đổi khả năng in. **Bắt buộc gỡ handler cũ** — không gỡ là rò bộ nhớ, đúng loại lỗi review 7B đã bắt được với `KeyDown`.

- [ ] **Step 6: Thêm nút vào `MainWindow.axaml`**

Bọc `PaneFooter` hiện tại thành `StackPanel` dọc, đặt nút In **trên** nút "Import Excel" đã có, cùng `Classes`, `HorizontalAlignment`, `Margin` để trông đồng bộ:

```xml
            <ui:NavigationView.PaneFooter>
                <StackPanel>
                    <Button Classes="accent" HorizontalAlignment="Stretch"
                            HorizontalContentAlignment="Center" Margin="8,4"
                            ToolTip.Tip="In màn hình đang mở hoặc toàn bộ sổ">
                        <StackPanel Orientation="Horizontal" Spacing="8">
                            <ui:SymbolIcon Symbol="{x:Static ui:Symbol.Print}"/>
                            <TextBlock Text="In"/>
                        </StackPanel>
                        <Button.Flyout>
                            <MenuFlyout>
                                <MenuItem Header="In sheet này" Command="{Binding PrintCurrentSheetCommand}"/>
                                <MenuItem Header="In toàn bộ" Command="{Binding PrintAllCommand}"/>
                            </MenuFlyout>
                        </Button.Flyout>
                    </Button>
                    <!-- nút Import Excel giữ nguyên, chuyển vào đây -->
                </StackPanel>
            </ui:NavigationView.PaneFooter>
```

`ui:Symbol.Print` có trong FluentAvaloniaUI 2.2.0 (bản dự án đang ghim) — đã kiểm chứng, dùng thẳng.

- [ ] **Step 7: Đăng ký DI trong `App.axaml.cs`**

Thêm `services.AddSingleton<IFileLauncher, FileLauncher>();` và `services.AddSingleton<IPrintWorkbookWriter, PrintWorkbookWriter>();` cạnh các đăng ký đã có.

- [ ] **Step 8: Chạy toàn bộ suite + kiểm ranh giới phân lớp**

```bash
dotnet test ConstructionFinance.sln
grep -rn "ClosedXML\|IXLWorksheet\|XLWorkbook" src/ConstructionFinance.Desktop --include=*.cs | grep -v obj
```
Kỳ vọng: **~657 xanh**, 0 warning, và lệnh `grep` **không có kết quả**.

- [ ] **Step 9: Commit**

```bash
git add src/ConstructionFinance.Desktop tests/ConstructionFinance.Desktop.Tests
git commit -m "feat(print): nút In trên khung chung, chọn nơi lưu rồi mở bằng Excel"
```

---

## Nghiệm thu bằng mắt (người điều phối làm, không phải subagent)

Sau khi cả 6 task xanh, chạy app thật với DB đã seed, rồi:

1. Vào tab **Vị trí 1** → In → *In sheet này* → mở tệp: kiểm 14 cột đúng thứ tự, tiền canh phải và **cộng được**, thụt bậc đúng, xem trước bản in thấy vừa **một trang bề ngang**, dòng tiêu đề và TỔNG CỘNG **lặp lại ở trang 2**, chân trang có "Trang 1/2".
2. Sang tab **Quyết toán năm** → nút In vẫn ở nguyên chỗ cũ → *In sheet này* → phải ra **A4 dọc**.
3. Sang màn **Cài đặt** → nút In phải **mờ**, vẫn nằm đúng chỗ.
4. *In toàn bộ* → một tệp, các sheet đúng thứ tự Mẫu, mỗi sheet căn trang riêng.
5. Mở sẵn tệp đích trong Excel rồi In đè lên → phải hiện thông báo tiếng Việt, không văng app.

## Ghi chú phải cập nhật sang kế hoạch 7E

`FinanceExportService.UniqueSheetName` đã chuyển thành `Printing/ExcelSheetName` ở Task 1. Đây là **mối nối thứ tư** cần gỡ cẩn thận khi 7E xóa `FinanceExportService`, cùng loại với ba mối đã ghi (`ImportMerge.NextFreeName`, `IFinanceProjectService`, `FinanceWorkbookViewModel`). Cập nhật ghi chú 7E ngay khi Task 1 xong.
