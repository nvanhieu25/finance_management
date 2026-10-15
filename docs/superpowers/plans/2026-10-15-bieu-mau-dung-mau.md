# Ba biểu mẫu dựng đúng Mẫu.xlsx — Kế hoạch triển khai

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Cách dùng khối mã trong plan này:** mỗi khối có thông tin `file=<đường dẫn>` là NỘI DUNG ĐẦY ĐỦ của tệp đó. Có thể ghi hàng loạt bằng một script nhỏ đọc plan và ghi các khối `file=` thay vì gõ lại.

**Goal:** Ba tab Thông tri / KS chi / GĐNTT hiện đúng Mẫu.xlsx (cùng hàng, cột, ô gộp, bề rộng, cỡ chữ, đường kẻ), chỉ phần nhập số liệu được tự động hoá; xuất Excel và in ra giấy dựng từ cùng một bản gốc.

**Architecture:** Mỗi biểu mẫu có MỘT bản gốc dữ liệu (`FormTemplate`, chép từ Mẫu.xlsx) gồm các ô, mỗi ô là danh sách đoạn (chữ cố định / ô tự nhập / ô tự tính / ô tự tính cho gõ đè). `FormSheetResolver` trộn bản gốc với số đã tính (từ các builder CÓ SẴN, không đổi) và chữ người dùng đã gõ thành một `ResolvedSheet`; ba bộ vẽ (lưới Avalonia, ClosedXML, MigraDoc) cùng đọc `ResolvedSheet`. Chữ tự nhập lưu ở bảng mới `finance_form_inputs (dự án, biểu mẫu, khoá)`.

**Tech Stack:** .NET 8, Avalonia 11.2.1 (FluentAvalonia), EF Core 8 + SQLite, ClosedXML 0.102.2, PDFsharp-MigraDoc 6, xUnit + Shouldly + Moq, Avalonia.Headless. Không thêm gói nào.

**Spec:** `docs/superpowers/specs/2026-10-15-bieu-mau-dung-mau-design.md` (đã duyệt). Quyết định bổ sung của plan này ghi ở §"Chỗ plan chốt thêm so với spec" bên dưới.

## Global Constraints

- Giấy phép tự do: KHÔNG thêm gói NuGet; không dùng Fable 5 làm mô hình con.
- Quy ước đặt tên .NET của dự án: biến cục bộ camelCase, mọi thứ khác PascalCase, trường private `_camelCase`.
- Avalonia ghim **11.2.1**: KHÔNG dùng `Grid.ColumnSpacing/RowSpacing` (cần 11.3+) — dùng `Margin` từng con.
- Trong Avalonia, selector hậu duệ đi xuyên vào ControlTemplate; muốn chỉnh container của ItemsControl phải dùng `ItemContainerTheme`. `Margin` nở cột Grid, `Padding` thì không.
- KHÔNG đụng cách tính số: `ThongTriBuilder`, `KsChiBuilder`, `GdnttBuilder`, `FormRowFlattener`, `VietnameseNumberToWords`, `TreeCalculator` giữ nguyên chữ ký và kết quả; test số liệu hiện có phải xanh.
- Bộ test KHÔNG chạy song song (`xunit.runner.json`) vì lỗi đồng thời của ClosedXML — đừng bật lại.
- Mọi `#if` phụ thuộc TFM: KHÔNG dùng `NET8_0_WINDOWS` (không phải ký hiệu SDK). Plan này không thêm `#if` nào; cuối plan kiểm lại `strings … | grep -c WindowsPrinterPort` ở đích Windows.
- `dotnet build -warnaserror` phải sạch ở CẢ HAI đích của `ConstructionFinance.Desktop` (`net8.0` và `net8.0-windows`).
- Commit: tiếng Việt, kiểu `feat(forms): …` / `test(forms): …` / `refactor(forms): …`; mỗi commit kết thúc bằng dòng `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`. Làm trên nhánh hiện tại `refactor/vitri-hierarchy-7` (chưa merge).
- Mọi chữ hiển thị cho người dùng bằng tiếng Việt có dấu. Mọi chuỗi chữ so sánh với Mẫu.xlsx chuẩn hoá về NFC (Mẫu có một ô viết ở dạng NFD).
- Bảng `finance_*_decl` bị xoá không đảo ngược khi app khởi động (`App.axaml.cs` chạy `MigrateAsync`). Sao lưu `app.db` THẬT (`AppPaths.DatabaseFile`) trước lần chạy app thật đầu tiên sau Task 7 — xem Task 7 bước 9.

## Chỗ plan chốt thêm so với spec

Spec nói "chép tay từng ô"; thực tế plan chép bằng script một lần (`tools/gen_form_templates.py`, đọc Mẫu.xlsx) rồi sửa tay phần chú giải, vì ba bản gốc có ~230 ô và chép tay là nơi sinh lỗi. Test `TemplateMatchesMauTests` (đọc Mẫu.xlsx bằng ClosedXML — một thư viện KHÁC với script dùng openpyxl) là chốt chặn độc lập. Các chỗ plan cố ý khác Mẫu hoặc làm rõ spec:

1. **Gộp ô thêm ở GĐNTT: `H13:I13`, `H25:I25` và `D28:I28`.** Cột H của Mẫu rộng 0,86 ký tự nên số tiền đặt ở H13 / H25 chỉ hiện `###` trong Excel; các dòng quanh nó (H15:I15, H17:I17, H23:I23, H24:I24) đã gộp H:I. Hai gộp đầu làm hai ô ấy dùng được; `D28:I28` cho số tài khoản (13–16 chữ số, căn giữa, bề rộng một cột 8,43 không chứa nổi) chỗ rộng như Mẫu vẫn tràn ra hai bên trong Excel. Test cho phép đúng ba ngoại lệ này.
2. **Mục lục ngân sách (dòng 15 Thông tri) hiện `010 · 011 · 9300 · 9301 · 00 · 00`**, không phải `10 · 11 · 9300 · 9301 · 0 · 0` như spec §5.2 ghi — đó là giá trị lưu; định dạng ô của Mẫu (`"0"0`, `"0"\1\1`, `00`) mới là chữ hiện ra, và Mẫu in chữ hiện ra.
3. **Đường kẻ có hai nét: thường và `hair` (rất mảnh).** KS chi dòng 7–17 dùng `hair` trên/dưới. `CellBorders` mang kiểu nét từng cạnh.
4. **Có chữ gạch chân** (`GĐNTT!D2`, `KS chi!A22`) nên kiểu ô có `Underline`.
5. **Lề giấy giữ nguyên của ứng dụng** (0,5″ hai bên), không theo Mẫu (0,45″ / 0″ — lề phải 0 không in được trên máy in thật). Khổ A4 dọc theo Mẫu.
6. **Số 0 ở ô tự tính hiện `0`** (đúng `#,##0` của Mẫu, và đúng quy ước `PrintMoneyText` đã ghi: "Số 0 ra '0'"); ô số tự nhập còn trống thì để trống.
7. **KS chi C8:E12 (số theo từng mục) vẫn là ô trống khoá**, vì spec §5.2 chỉ liệt kê C14, C15, C16, C19 là ô số tự nhập. Nếu người dùng muốn gõ số ở đó, thêm sau bằng một dòng `Tpl.Number(...)` — không cần migration.
8. **Chữ trong ghi chú của Mẫu** (`Tự nhập liệu`, `( có thể gõ…)`, `Ngân hàng A ( Tự nhập)`) là `Hint`: hiện mờ trên màn hình, KHÔNG in.
9. **`ThuocKeHoach` (GĐNTT C16:D16)**: Mẫu có ô gộp trống ngay sau nhãn `- Thuộc kế hoạch:`; plan coi đó là ô tự nhập chữ (khoá cũ `ThuocKeHoach`).

## Review Focus

Năm điều spec ngầm nói tới nhưng không task nào tự nhiên thử; mỗi dòng có test ghim ở task sở hữu mã tương ứng (tên test ghi trong ngoặc):

1. **Số đợt cực đoan ở Thông tri**: 0 đợt (chỉ dòng dự án), đúng 9 dòng nội dung và 30 đợt — `Cộng` / `Bằng chữ` phải dời xuống đúng, không mất dòng (Task 3 `Resolve_inserts_rows_before_Cong_when_there_are_more_than_nine_lines`, Task 6 `Thong_tri_nhieu_dot_chay_sang_trang_khong_ne`).
2. **Người dùng gõ chữ có `{năm}` hoặc bắt đầu bằng `=`**: chữ người dùng gõ KHÔNG được thay năm và KHÔNG thành công thức Excel (Task 3 `User_text_containing_the_year_token_is_not_substituted`, Task 5 `Text_starting_with_equals_stays_text`).
3. **GĐNTT bị chặn (chưa chọn gói / nhiều gói)**: tờ vẫn hiện, vẫn gõ được các ô chữ, chỉ nút Xuất và In bị khoá kèm lý do (Task 4 `Gdntt_blocked_still_resolves_the_sheet`, Task 8 `Gdntt_blocked_still_accepts_typing`).
4. **Gõ số rác vào ô số** (`abc`, `1.2.3`, để trống, số âm): không làm hỏng ô, không ném (Task 8 `Typing_garbage_in_a_number_cell_keeps_the_old_value`).
5. **Migration trên dữ liệu thật có cột NULL/rỗng và trên DB rỗng** (Task 7 `Migration_copies_non_empty_values_only`, `Migration_runs_on_an_empty_database`).

---

### Task 0: Chuẩn bị — đưa Mẫu.xlsx vào repo

**Files:**
- Add to git: `reference/Mẫu.xlsx`

- [ ] **Step 1: Xác nhận đang ở đúng nhánh, cây sạch trừ các tệp đã biết**

Run: `git status --short && git branch --show-current`
Expected: nhánh `refactor/vitri-hierarchy-7`; chỉ các tệp untracked/modified đã biết (`reference/Mẫu.xlsx`, `reference/TongHop.xlsx`, hai tệp docs/plans). KHÔNG `git add` `reference/TongHop.xlsx` hay hai tệp docs không liên quan.

- [ ] **Step 2: Chạy bộ test nền, ghi số lượng**

Run: `dotnet test ConstructionFinance.sln --nologo -v q 2>&1 | grep -E "Passed!|Failed!"`
Expected: tất cả `Passed!`, không `Failed!`. Ghi lại tổng số test (xấp xỉ 1019) để so sánh cuối plan.

- [ ] **Step 3: Commit Mẫu.xlsx**

```bash
git add reference/Mẫu.xlsx
git commit -m "chore(forms): đưa reference/Mẫu.xlsx vào repo để test so thẳng với nó

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 1: Kiểu dữ liệu của bản gốc, của tờ đã trộn, và định dạng số

**Files:**
- Create: `src/ConstructionFinance.Application/Forms/FormTemplate.cs`
- Create: `src/ConstructionFinance.Application/Forms/Tpl.cs`
- Create: `src/ConstructionFinance.Application/Forms/FormData.cs`
- Create: `src/ConstructionFinance.Application/Forms/FormNumberFormat.cs`
- Create: `src/ConstructionFinance.Application/Forms/FormSheet.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Forms/FormTemplateTypesTests.cs`

**Interfaces:**
- Produces (dùng ở mọi task sau):
  - `enum SegmentKind { Fixed, Input, InputNumber, SettingInput, Computed, Overridable }`
  - `enum CellAlign { General, Left, Center, Right, Justify }`, `enum CellVAlign { Bottom, Center, Top }`, `enum BorderLine { None, Thin, Hair }`
  - `readonly record struct CellBorders(BorderLine Left, BorderLine Right, BorderLine Top, BorderLine Bottom)` + `static CellBorders Parse(string flags)` (chữ thường = thin, HOA = hair; `l r t b`)
  - `record TemplateSegment(SegmentKind Kind, string Text, string? Key = null, string? Default = null, string? Hint = null)`
  - `record CellStyle(double FontSize, bool Bold, bool Italic, bool Underline, CellAlign Align, CellVAlign VAlign, bool Wrap, CellBorders Borders, string? NumberFormat)`
  - `record TemplateCell(int Row, int Col, int RowSpan, int ColSpan, CellStyle Style, IReadOnlyList<TemplateSegment> Segments)`
  - `record TemplateRow(double Height, IReadOnlyList<TemplateCell> Cells)` (ô có `Row == 0`, chỉ `Col` có nghĩa)
  - `record RepeatRegion(int FirstRow, int MinRows, TemplateRow ProjectRow, TemplateRow ViTriRow, TemplateRow DotRow, TemplateRow BlankRow)`
  - `record FormTemplate(string SheetName, PrintOrientation Orientation, IReadOnlyList<double> ColumnWidths, IReadOnlyDictionary<int,double> RowHeights, IReadOnlyList<TemplateCell> Cells, RepeatRegion? Repeat)` + `IEnumerable<TemplateSegment> AllSegments()`
  - `static class Tpl` (cú pháp rút gọn cho các tệp bản gốc sinh ra)
  - `readonly record struct ComputedValue(string? Text, decimal? Number)`; `class FormValues` (`Text`, `Number`, `TryGet`, `BlankMissing`); `class FormInputSet` (`Get/Set/Remove/Has/GetNumber/TryGetNumber/Values`, `static ToStored(decimal)`); `static class FormOrgKeys` (`DonViCapTren`, `DonViCoSo`, `Get(this FormOrgInfo,string)`, `With(this FormOrgInfo,string,string?)`)
  - `static class FormNumberFormat` (`Default = "#,##0"`, `Format(decimal, string?)`, `Group(decimal)`)
  - `record ResolvedSegment(SegmentKind Kind, string? Key, string Text, string Value, string? Hint, bool IsPlaceholder, bool IsOverridden, decimal? Number)`; `record ResolvedCell(...)` với `DisplayText`, `IsEditable`; `record ResolvedSheet(...)`

- [ ] **Step 1: Viết test (đỏ)**

```csharp file=tests/ConstructionFinance.Application.Tests/Forms/FormTemplateTypesTests.cs
using ConstructionFinance.Application.Forms;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Forms;

public class FormTemplateTypesTests
{
    [Fact]
    public void Borders_parse_lowercase_is_thin_and_uppercase_is_hair()
    {
        var borders = CellBorders.Parse("lrTB");

        borders.Left.ShouldBe(BorderLine.Thin);
        borders.Right.ShouldBe(BorderLine.Thin);
        borders.Top.ShouldBe(BorderLine.Hair);
        borders.Bottom.ShouldBe(BorderLine.Hair);
    }

    [Fact]
    public void Borders_parse_empty_string_is_no_border()
    {
        var borders = CellBorders.Parse("");

        borders.Left.ShouldBe(BorderLine.None);
        borders.Any.ShouldBeFalse();
    }

    [Fact]
    public void Borders_parse_rejects_an_unknown_flag()
    {
        Should.Throw<ArgumentException>(() => CellBorders.Parse("lx"));
    }

    [Theory]
    [InlineData(1234567.0, null, "1.234.567")]
    [InlineData(0.0, "#,##0", "0")]
    [InlineData(5000000.0, "#,##0\\ \"đ\"", "5.000.000 đ")]
    [InlineData(5000000.0, "#,##0\"đ\"", "5.000.000đ")]
    [InlineData(-1500.0, "#,##0_);(#,##0)", "(1.500)")]
    [InlineData(1500.0, "#,##0_);(#,##0)", "1.500")]
    public void Format_groups_with_dots_and_honours_the_suffix_and_parentheses(
        double value, string? format, string expected)
    {
        FormNumberFormat.Format((decimal)value, format).ShouldBe(expected);
    }

    [Fact]
    public void InputSet_number_round_trips_through_the_stored_text()
    {
        var inputs = new FormInputSet();
        inputs.Set("ThueGTGT", FormInputSet.ToStored(1_500_000.5m));

        inputs.GetNumber("ThueGTGT").ShouldBe(1_500_000.5m);
        inputs.TryGetNumber("ChuaCo", out _).ShouldBeFalse();
        inputs.GetNumber("ChuaCo").ShouldBe(0m);
    }

    [Fact]
    public void InputSet_distinguishes_a_missing_key_from_an_explicit_empty_value()
    {
        var inputs = new FormInputSet();
        inputs.Set("A", "");

        inputs.Has("A").ShouldBeTrue();
        inputs.Get("A").ShouldBe("");
        inputs.Has("B").ShouldBeFalse();
        inputs.Get("B").ShouldBeNull();
    }

    [Fact]
    public void OrgKeys_read_and_write_the_two_settings()
    {
        var org = new FormOrgInfo("Sở", "Ban");

        org.Get(FormOrgKeys.DonViCapTren).ShouldBe("Sở");
        org.Get(FormOrgKeys.DonViCoSo).ShouldBe("Ban");
        org.With(FormOrgKeys.DonViCoSo, "Mới").DonViCoSo.ShouldBe("Mới");
        org.With(FormOrgKeys.DonViCoSo, "Mới").DonViCapTren.ShouldBe("Sở");
        Should.Throw<ArgumentException>(() => org.Get("khong_co"));
    }
}
```

- [ ] **Step 2: Chạy test, thấy đỏ vì chưa có kiểu**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --nologo -v q --filter FormTemplateTypesTests 2>&1 | tail -5`
Expected: lỗi biên dịch (`CellBorders` không tồn tại).

- [ ] **Step 3: Viết các kiểu**

```csharp file=src/ConstructionFinance.Application/Forms/FormTemplate.cs
using ConstructionFinance.Application.Printing;

namespace ConstructionFinance.Application.Forms;

/// Một đoạn của một ô biểu mẫu. Ô là DANH SÁCH đoạn vì Mẫu hay nhét nhãn và chỗ điền vào cùng
/// một chữ ("Số ........", " TK……Đơn vị……Số CT……", "Tên CTMT, DA…… Mã số: ……").
public enum SegmentKind
{
    /// Chữ cố định, khoá.
    Fixed,
    /// Ô tự nhập chữ. Lưu theo (dự án, biểu mẫu, khoá).
    Input,
    /// Ô tự nhập số. Lưu dạng số bất biến văn hoá; hiện có dấu chấm phân nghìn.
    InputNumber,
    /// Ô tự nhập chữ nhưng lưu ở Cài đặt, dùng chung mọi dự án và mọi biểu mẫu (tên đơn vị).
    SettingInput,
    /// Ô tự tính từ các builder; khoá.
    Computed,
    /// Ô tự tính, điền sẵn, cho gõ đè; xoá chữ gõ đè thì trở về tự động.
    Overridable
}

public enum CellAlign { General, Left, Center, Right, Justify }

public enum CellVAlign { Bottom, Center, Top }

public enum BorderLine { None, Thin, Hair }

/// Nét kẻ bốn cạnh của một ô (hoặc của cả vùng gộp). Chuỗi cờ: l r t b, chữ thường = nét thường,
/// chữ HOA = nét hair (KS chi dòng 7–17 kẻ trên/dưới bằng nét hair).
public readonly record struct CellBorders(BorderLine Left, BorderLine Right, BorderLine Top, BorderLine Bottom)
{
    public bool Any => Left != BorderLine.None || Right != BorderLine.None
                       || Top != BorderLine.None || Bottom != BorderLine.None;

    public static CellBorders Parse(string flags)
    {
        var (left, right, top, bottom) = (BorderLine.None, BorderLine.None, BorderLine.None, BorderLine.None);
        foreach (var flag in flags)
        {
            var line = char.IsUpper(flag) ? BorderLine.Hair : BorderLine.Thin;
            switch (char.ToLowerInvariant(flag))
            {
                case 'l': left = line; break;
                case 'r': right = line; break;
                case 't': top = line; break;
                case 'b': bottom = line; break;
                default: throw new ArgumentException($"Cờ viền không hợp lệ: '{flag}' trong \"{flags}\".", nameof(flags));
            }
        }
        return new CellBorders(left, right, top, bottom);
    }
}

/// `Text`: với Fixed là chữ in; với các loại còn lại là chữ IN RA khi ô còn trống (chấm chấm của
/// Mẫu, có thể rỗng). `Default`: giá trị điền sẵn khi người dùng chưa gõ gì (không mờ, sửa được).
/// `Hint`: chữ mờ trên MÀN HÌNH, không bao giờ in — chép từ ghi chú của Mẫu. Chuỗi `{năm}` trong
/// Text/Default/Hint được thay bằng năm hiện tại (chữ người dùng gõ thì KHÔNG bao giờ bị thay).
public sealed record TemplateSegment(
    SegmentKind Kind,
    string Text,
    string? Key = null,
    string? Default = null,
    string? Hint = null);

/// `NumberFormat != null` đánh dấu ô số. Ô số không tự khai định dạng thì dùng FormNumberFormat.Default.
public sealed record CellStyle(
    double FontSize,
    bool Bold,
    bool Italic,
    bool Underline,
    CellAlign Align,
    CellVAlign VAlign,
    bool Wrap,
    CellBorders Borders,
    string? NumberFormat);

public sealed record TemplateCell(
    int Row,
    int Col,
    int RowSpan,
    int ColSpan,
    CellStyle Style,
    IReadOnlyList<TemplateSegment> Segments);

/// Một hàng mẫu của vùng lặp. Ô trong hàng có `Row == 0`: chỉ cột (`Col`) có nghĩa, hàng thật
/// do bộ trộn gán.
public sealed record TemplateRow(double Height, IReadOnlyList<TemplateCell> Cells);

/// Vùng lặp của Thông tri: Mẫu dành `MinRows` dòng từ `FirstRow` cho bảng nội dung. Mỗi dòng của
/// bảng là một trong bốn loại hàng mẫu; ít dòng thì phần còn lại là hàng trống, nhiều dòng thì
/// chèn thêm và mọi hàng bên dưới dời xuống.
public sealed record RepeatRegion(
    int FirstRow,
    int MinRows,
    TemplateRow ProjectRow,
    TemplateRow ViTriRow,
    TemplateRow DotRow,
    TemplateRow BlankRow);

public sealed record FormTemplate(
    string SheetName,
    PrintOrientation Orientation,
    IReadOnlyList<double> ColumnWidths,
    IReadOnlyDictionary<int, double> RowHeights,
    IReadOnlyList<TemplateCell> Cells,
    RepeatRegion? Repeat)
{
    /// Mọi đoạn của bản gốc, kể cả các hàng mẫu của vùng lặp.
    public IEnumerable<TemplateSegment> AllSegments()
    {
        var all = Cells.AsEnumerable();
        if (Repeat is { } region)
            all = all.Concat(region.ProjectRow.Cells).Concat(region.ViTriRow.Cells)
                .Concat(region.DotRow.Cells).Concat(region.BlankRow.Cells);
        return all.SelectMany(cell => cell.Segments);
    }
}
```

```csharp file=src/ConstructionFinance.Application/Forms/Tpl.cs
namespace ConstructionFinance.Application.Forms;

/// Cú pháp rút gọn cho các tệp bản gốc (ThongTriTemplate…). Chỉ để các tệp đó đọc được cạnh Mẫu.xlsx.
internal static class Tpl
{
    public static CellStyle Style(
        double size, bool bold, bool italic, bool underline, CellAlign align, CellVAlign v, bool wrap,
        string borders, string? format) =>
        new(size, bold, italic, underline, align, v, wrap, CellBorders.Parse(borders), format);

    public static TemplateCell Cell(int row, int col, CellStyle style, params TemplateSegment[] segments) =>
        new(row, col, 1, 1, style, segments);

    public static TemplateCell Merged(int row, int col, int rows, int cols, CellStyle style,
        params TemplateSegment[] segments) =>
        new(row, col, rows, cols, style, segments);

    public static TemplateSegment Fixed(string text) => new(SegmentKind.Fixed, text);

    public static TemplateSegment Input(string key, string placeholder, string? @default = null, string? hint = null) =>
        new(SegmentKind.Input, placeholder, key, @default, hint);

    public static TemplateSegment Number(string key) => new(SegmentKind.InputNumber, "", key);

    public static TemplateSegment Setting(string key, string placeholder) =>
        new(SegmentKind.SettingInput, placeholder, key);

    public static TemplateSegment Computed(string key) => new(SegmentKind.Computed, "", key);

    public static TemplateSegment Over(string key, string? hint = null) =>
        new(SegmentKind.Overridable, "", key, null, hint);
}
```

```csharp file=src/ConstructionFinance.Application/Forms/FormData.cs
using System.Globalization;

namespace ConstructionFinance.Application.Forms;

public readonly record struct ComputedValue(string? Text, decimal? Number);

/// Giá trị các ô TỰ TÍNH, theo khoá của bản gốc. Do `FormDefinition` điền từ kết quả của builder.
public sealed class FormValues
{
    private readonly Dictionary<string, ComputedValue> _values = new(StringComparer.Ordinal);

    /// Đường lỗi (GĐNTT bị chặn) trả về tờ trống: khoá thiếu thì thành ô rỗng thay vì ném. Đường
    /// bình thường để false, nên quên một khoá là lỗi ném ngay chứ không âm thầm ra ô trống.
    public bool BlankMissing { get; init; }

    public FormValues Text(string key, string? text)
    {
        _values[key] = new ComputedValue(text, null);
        return this;
    }

    public FormValues Number(string key, decimal number)
    {
        _values[key] = new ComputedValue(null, number);
        return this;
    }

    public bool TryGet(string key, out ComputedValue value) => _values.TryGetValue(key, out value);
}

/// Chữ người dùng đã gõ cho MỘT biểu mẫu của MỘT dự án, theo khoá của bản gốc. Khoá vắng ≠ khoá
/// có giá trị rỗng: vắng nghĩa là "chưa gõ" (dùng giá trị điền sẵn); rỗng nghĩa là "đã xoá".
public sealed class FormInputSet
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal);

    public FormInputSet() { }

    public FormInputSet(IEnumerable<KeyValuePair<string, string?>> initial)
    {
        foreach (var pair in initial) _values[pair.Key] = pair.Value;
    }

    public IReadOnlyDictionary<string, string?> Values => _values;

    public bool Has(string key) => _values.ContainsKey(key);

    public string? Get(string key) => _values.TryGetValue(key, out var value) ? value : null;

    public void Set(string key, string? value) => _values[key] = value;

    public bool Remove(string key) => _values.Remove(key);

    public bool TryGetNumber(string key, out decimal number)
    {
        number = 0m;
        return decimal.TryParse(Get(key), NumberStyles.Number, CultureInfo.InvariantCulture, out number);
    }

    public decimal GetNumber(string key) => TryGetNumber(key, out var number) ? number : 0m;

    /// Dạng lưu của một số: bất biến văn hoá, không dấu phân nghìn.
    public static string ToStored(decimal number) => number.ToString(CultureInfo.InvariantCulture);
}

/// Hai khoá Cài đặt mà ô SettingInput đọc/ghi. Cùng chuỗi với CompanyInfoService.
public static class FormOrgKeys
{
    public const string DonViCapTren = "don_vi_cap_tren";
    public const string DonViCoSo = "don_vi_co_so";

    public static string? Get(this FormOrgInfo org, string key) => key switch
    {
        DonViCapTren => org.DonViCapTren,
        DonViCoSo => org.DonViCoSo,
        _ => throw new ArgumentException($"Khoá Cài đặt không biết: \"{key}\".", nameof(key))
    };

    public static FormOrgInfo With(this FormOrgInfo org, string key, string? value) => key switch
    {
        DonViCapTren => org with { DonViCapTren = value },
        DonViCoSo => org with { DonViCoSo = value },
        _ => throw new ArgumentException($"Khoá Cài đặt không biết: \"{key}\".", nameof(key))
    };
}
```

```csharp file=src/ConstructionFinance.Application/Forms/FormNumberFormat.cs
using ConstructionFinance.Application.Printing;

namespace ConstructionFinance.Application.Forms;

/// Hiện một số theo định dạng ô của Mẫu cho màn hình và bản in (đường .xlsx đặt thẳng chuỗi định
/// dạng cho Excel). Chỉ hỗ trợ đúng các định dạng Mẫu dùng: `#,##0`, `#,##0_);(#,##0)`,
/// `#,##0"đ"`, `#,##0\ "đ"`. Dấu phân nghìn là dấu chấm (quy ước Việt, như PrintMoneyText).
public static class FormNumberFormat
{
    public const string Default = "#,##0";

    public static string Format(decimal value, string? format)
    {
        format ??= Default;
        var suffix = format.Contains("\\ \"đ\"") ? " đ" : format.Contains("\"đ\"") ? "đ" : "";
        var digits = Group(Math.Abs(value));
        if (value >= 0) return digits + suffix;
        return format.Contains(");(") ? $"({digits}){suffix}" : $"-{digits}{suffix}";
    }

    public static string Group(decimal value) => PrintMoneyText.Format(value);
}
```

```csharp file=src/ConstructionFinance.Application/Forms/FormSheet.cs
using ConstructionFinance.Application.Printing;

namespace ConstructionFinance.Application.Forms;

/// Một đoạn đã trộn. `Text` là chữ IN RA (đã thay năm, đã điền chữ chấm chấm khi ô trống);
/// `Value` là chữ trong ô nhập trên màn hình (rỗng khi chưa gõ); `Hint` là chữ mờ trên màn hình.
public sealed record ResolvedSegment(
    SegmentKind Kind,
    string? Key,
    string Text,
    string Value,
    string? Hint,
    bool IsPlaceholder,
    bool IsOverridden,
    decimal? Number);

/// `TextColSpan >= ColSpan`: số cột mà CHỮ được tràn sang (Excel cho chữ trái/thường tràn sang ô
/// trống bên phải). Đường kẻ của ô vẫn chỉ nằm trong `ColSpan`.
public sealed record ResolvedCell(
    int Row,
    int Col,
    int RowSpan,
    int ColSpan,
    int TextColSpan,
    CellStyle Style,
    IReadOnlyList<ResolvedSegment> Segments)
{
    public string DisplayText => string.Concat(Segments.Select(segment => segment.Text));

    public bool IsEditable => Segments.Any(segment =>
        segment.Kind is SegmentKind.Input or SegmentKind.InputNumber
            or SegmentKind.SettingInput or SegmentKind.Overridable);

    public bool IsComputed => Segments.Any(segment =>
        segment.Kind is SegmentKind.Computed or SegmentKind.Overridable);
}

/// Tờ biểu mẫu đã trộn xong — thứ DUY NHẤT mà ba bộ vẽ (màn hình, Excel, in) đọc.
/// `RowHeights[i]` là chiều cao hàng i+1 (pt); `ColumnWidths[i]` là bề rộng cột i+1 (ký tự Excel).
public sealed record ResolvedSheet(
    string SheetName,
    PrintOrientation Orientation,
    IReadOnlyList<double> ColumnWidths,
    IReadOnlyList<double> RowHeights,
    IReadOnlyList<ResolvedCell> Cells)
{
    public int RowCount => RowHeights.Count;
    public int ColumnCount => ColumnWidths.Count;
}
```

- [ ] **Step 4: Sửa `FormOrgInfo` thành record có `with`**

`FormOrgInfo` đã là `record FormOrgInfo(string? DonViCapTren, string? DonViCoSo)` trong `FormModels.cs` nên `with` dùng được; không cần sửa.

- [ ] **Step 5: Chạy test, thấy xanh**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --nologo -v q --filter FormTemplateTypesTests 2>&1 | tail -5`
Expected: `Passed!` 11 test (6 Fact + 6 InlineData − tính theo Theory).

- [ ] **Step 6: Commit**

```bash
git add src/ConstructionFinance.Application/Forms tests/ConstructionFinance.Application.Tests/Forms/FormTemplateTypesTests.cs
git commit -m "feat(forms): kiểu dữ liệu của bản gốc biểu mẫu, tờ đã trộn và định dạng số

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Ba bản gốc chép từ Mẫu.xlsx + test so thẳng với Mẫu

**Files:**
- Create: `tools/gen_form_templates.py`
- Create (sinh ra rồi sửa tay): `src/ConstructionFinance.Application/Forms/ThongTriTemplate.cs`, `KsChiTemplate.cs`, `GdnttTemplate.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Forms/TemplateMatchesMauTests.cs`

**Interfaces:**
- Consumes: Task 1.
- Produces: `ThongTriTemplate.Create()`, `KsChiTemplate.Create()`, `GdnttTemplate.Create()` : `FormTemplate`. Khoá của bản gốc (dùng ở Task 3–4, 7–9):
  - Thông tri — Setting: `don_vi_cap_tren`, `don_vi_co_so`; Input: `SoPhieu`, `SoThongTri`, `NgayGhiSo`, `DonVi`, `TaiKhoan1`, `DonVi1`, `SoChungTu1`, `TaiKhoan2`, `DonVi2`, `SoChungTu2`, `VeThangNam`, `TenCTMT`, `MaSo`, và (hàng dự án) `LoaiNganSach`, `Khoan`, `Muc`, `TieuMuc`, `TietMuc`, `Nganh`; Computed: `Total`, `TotalInWords`, `Line.Text`, `Line.Amount`.
  - KS chi — Input: `NgayNhanGiayDeNghi`, `DonVi`, `Muc1..4`, `TieuMuc1..4`, `LyDoTuChoi`; Number: `ThuHoiCacNamTruoc`, `ThuHoiNamNay`, `ThueGTGT`, `SoTuChoi`; Computed: `Accepted.{Total,NSNN,NgoaiHoi}`, `Recovered.{…}`, `Paid.{…}`, `PaidInWords`; Overridable: `GhiChu`.
  - GĐNTT — Setting như Thông tri; Input: `So`, `NgayThangNam`, `KinhGui`, `ChuDauTu`, `CanCuQuyetDinh`, `CanCuHopDong`, `CanCuGiayDeNghi`, `ThuocKeHoach`, `TaiNganHang`; Number: `ChuyenTienBaoHanh`, `ChuyenTienTamGiu`; Computed: `PackageName`, `ContractTotal`, `CumulativeBefore`, `Requested`, `RequestedInWords`, `Recovered`, `PaidToContractor`, `ContractorName`, `BankAccount`; Overridable: `TenDuAnCongTrinh` (chữ), `UnrecoveredAdvanceBefore` (số).

- [ ] **Step 1: Ghi script sinh bản gốc**

Script chạy MỘT LẦN: đọc Mẫu.xlsx bằng openpyxl, xuất ba tệp C# dùng `Tpl.*`. Bảng chú giải `ANNOT` ở đầu script là chỗ quyết định ô nào là ô tự nhập/tự tính; mọi công thức chưa chú giải làm script dừng.

```python file=tools/gen_form_templates.py
#!/usr/bin/env python3
"""Sinh ba tệp mẫu C# từ reference/Mẫu.xlsx. CHẠY MỘT LẦN: từ đó sửa tay các tệp *Template.cs,
test TemplateMatchesMauTests giữ cho chúng đúng với Mẫu.xlsx.

    python3 -I gen_form_templates.py <Mẫu.xlsx> <thư mục đích>
"""
import re, sys, os, unicodedata
import openpyxl
from openpyxl.utils import get_column_letter as L, column_index_from_string as CI

DOTS = re.compile(r"[.…]+")
DEFAULT_COL = 8.43
DEFAULT_ROW = 15.0

def esc(s):  # C# regular string literal (luôn NFC: Mẫu có một ô viết ở dạng NFD)
    s = unicodedata.normalize("NFC", s)
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"').replace("\n", "\\n").replace("\r", "") + '"'

def addr(r, c): return f"{L(c)}{r}"

# ---- chú giải: ô -> danh sách đoạn. Mỗi đoạn là một tuple (loại, ...). -------------------------
# ("F", text)                      Fixed
# ("I", key, placeholder[, default[, hint]])  Input
# ("N", key)                       InputNumber
# ("S", key, placeholder)          SettingInput (Cài đặt)
# ("C", key)                       Computed
# ("O", key[, hint])               Overridable
# ("DOTS", [keys], strip)          tách theo dải chấm: chữ giữa các dải là Fixed, mỗi dải là Input
# ("TAIL", prefix, key, strip)     Fixed(prefix) + Input(key, phần còn lại của chữ Mẫu làm placeholder)
# ("HINT", prefix, key)            Fixed(prefix) + Input(key, "", hint = phần còn lại)
YEAR = "{năm}"
# khoá Computed mang CHỮ (không định dạng số) — Mẫu vẫn để định dạng #,##0 trên các ô này
TEXT_KEYS = {"PackageName", "RequestedInWords", "ContractorName", "BankAccount", "TotalInWords",
             "PaidInWords", "Line.Text"}

ANNOT = {
 "Thông tri": {
  "A1": [("S", "don_vi_cap_tren", "Đơn vị cấp trên")],
  "A2": [("S", "don_vi_co_so", "Đơn vị cơ sở")],
  "G2": [("DOTS", ["SoPhieu"], True)],
  "A3": [("DOTS", ["SoThongTri"], False)],
  "G3": [("TAIL", "Ghi sổ ngày", "NgayGhiSo", False)],
  "C8": [("DOTS", ["DonVi"], False)],
  "G8": [("DOTS", ["TaiKhoan1", "DonVi1", "SoChungTu1"], False)],
  "G9": [("DOTS", ["TaiKhoan2", "DonVi2", "SoChungTu2"], False)],
  "A9": [("TAIL", "Về", "VeThangNam", False)],
  "A10": [("DOTS", ["TenCTMT", "MaSo"], False)],
  "H24": [("C", "Total")],
  "C25": [("C", "TotalInWords")],
 },
 "KS chi": {
  "D3": [("TAIL", "Ngày", "NgayNhanGiayDeNghi", False)],
  "A4": [("DOTS", ["DonVi"], False)],
  "C7": [("C", "Accepted.Total")], "D7": [("C", "Accepted.NSNN")], "E7": [("C", "Accepted.NgoaiHoi")],
  "A8": [("DOTS", ["Muc1", "TieuMuc1"], False)],
  "A9": [("DOTS", ["Muc2", "TieuMuc2"], False)],
  "A10": [("DOTS", ["Muc3", "TieuMuc3"], False)],
  "A11": [("DOTS", ["Muc4", "TieuMuc4"], False)],
  "C13": [("C", "Recovered.Total")], "D13": [("C", "Recovered.NSNN")], "E13": [("C", "Recovered.NgoaiHoi")],
  "C14": [("N", "ThuHoiCacNamTruoc")], "C15": [("N", "ThuHoiNamNay")], "C16": [("N", "ThueGTGT")],
  "C17": [("C", "Paid.Total")], "D17": [("C", "Paid.NSNN")], "E17": [("C", "Paid.NgoaiHoi")],
  "A18": [("C", "PaidInWords")],
  "C19": [("N", "SoTuChoi")],
  "A20": [("TAIL", "Lý do từ chối: ", "LyDoTuChoi", False)],
  "B22": [("O", "GhiChu")],
 },
 "GĐNTT": {
  "A1": [("S", "don_vi_cap_tren", "Đơn vị cấp trên")],
  "A2": [("S", "don_vi_co_so", "Đơn vị cấp cơ sở")],
  "A3": [("TAIL", "Số: ", "So", False)],
  "D3": [("I", "NgayThangNam", "", "Hải phòng, ngày     tháng   năm " + YEAR, None)],
  "A6": [("HINT", "Kính gửi: ", "KinhGui")],
  "C8": [("O", "TenDuAnCongTrinh", "( Có thể gõ tự ra nội dung hoặc tự nhập liệu)")],
  "A9": [("HINT", " - Chủ đầu tư/Ban QLDA:  ", "ChuDauTu")],
  "A10": [("HINT", " - Căn cứ Quyết định số ", "CanCuQuyetDinh")],
  "A11": [("HINT", " - Căn cứ Hợp đồng số: ", "CanCuHopDong")],
  "A12": [("HINT", " - Căn cứ giấy đề nghị thanh toán vốn đầu tư số ", "CanCuGiayDeNghi")],
  "H13": [("O", "UnrecoveredAdvanceBefore", "( có thể gõ ra số liệu hoặc Tự nhập liệu)")],
  "C16": [("I", "ThuocKeHoach", "", None, None)],
  "A18": [("C", "PackageName")],
  "D18": [("C", "ContractTotal")], "F18": [("C", "CumulativeBefore")], "H18": [("C", "Requested")],
  "D19": [("C", "ContractTotal")], "F19": [("C", "CumulativeBefore")], "I19": [("C", "Requested")],
  "I20": [("C", "Requested")],
  "B21": [("C", "RequestedInWords")],
  "H23": [("C", "Recovered")],
  "H24": [("N", "ChuyenTienBaoHanh")],
  "H25": [("N", "ChuyenTienTamGiu")],
  "I26": [("C", "PaidToContractor")],
  "C27": [("C", "ContractorName")],
  "D28": [("C", "BankAccount")],
  "B29": [("I", "TaiNganHang", "", None, "Ngân hàng A ( Tự nhập)")],
 },
}

# ô thêm hoặc sửa so với Mẫu (chỗ lệch có chủ ý, ghi trong spec §5.8 của plan)
EXTRA_MERGES = {"GĐNTT": ["H13:I13", "H25:I25", "D28:I28"]}
DROP_CELLS = {"GĐNTT": {"M13"}}
LAST_COL = {"Thông tri": 8, "KS chi": 5, "GĐNTT": 9}
LAST_ROW = {"Thông tri": 33, "KS chi": 34, "GĐNTT": 36}

# vùng lặp của Thông tri (hàng mẫu -> nguyên mẫu hàng dự án / vị trí / đợt / trống)
REGION = {"first": 15, "min": 9, "project": 15, "vitri": 16, "dot": 17, "blank": 20}
REGION_DEFAULTS = {"A": "010", "B": "011", "C": "9300", "D": "9301", "E": "00", "F": "00"}
REGION_KEYS = {"A": "LoaiNganSach", "B": "Khoan", "C": "Muc", "D": "TieuMuc", "E": "TietMuc", "F": "Nganh"}

HALIGN = {None: "General", "general": "General", "left": "Left", "center": "Center", "right": "Right", "justify": "Justify"}
VALIGN = {None: "Bottom", "bottom": "Bottom", "center": "Center", "top": "Top"}

def num(x):
    s = repr(float(x))
    if s.endswith(".0"): s = s[:-2]
    return s

def _side(cells, side):
    styles = {getattr(c.border, side).style for c in cells} - {None}
    if not styles: return None
    return "hair" if styles == {"hair"} else "thin"

def borders_str(cells):
    """hợp của các cạnh của vùng: chữ thường = nét thường, chữ HOA = nét hair (rất mảnh)."""
    rows = sorted({c.row for c in cells}); cols = sorted({c.column for c in cells})
    flags = ""
    for ch, side, edge in (("l", "left", [c for c in cells if c.column == cols[0]]),
                           ("r", "right", [c for c in cells if c.column == cols[-1]]),
                           ("t", "top", [c for c in cells if c.row == rows[0]]),
                           ("b", "bottom", [c for c in cells if c.row == rows[-1]])):
        st = _side(edge, side)
        if st == "thin": flags += ch
        elif st == "hair": flags += ch.upper()
    return flags

def style_expr(c, flags, is_money):
    f, a = c.font, c.alignment
    parts = [num(f.sz or 11)]
    kw = []
    if f.b: kw.append("bold: true")
    if f.i: kw.append("italic: true")
    if f.u: kw.append("underline: true")
    h = HALIGN[a.horizontal]
    if h != "General": kw.append(f"align: CellAlign.{h}")
    v = VALIGN[a.vertical]
    if v != "Bottom": kw.append(f"v: CellVAlign.{v}")
    if a.wrap_text: kw.append("wrap: true")
    if flags: kw.append(f'borders: "{flags}"')
    if is_money: kw.append(f"format: {esc(c.number_format)}")
    return "S(" + ", ".join(parts + kw) + ")"

def check_font(sheet, c):
    f = c.font
    if c.value is None: return
    if f.name != "Times New Roman":
        print(f"  CẢNH BÁO {sheet}!{c.coordinate}: font {f.name}", file=sys.stderr)
    if f.u or f.strike:
        print(f"  CẢNH BÁO {sheet}!{c.coordinate}: underline/strike", file=sys.stderr)

def segments(sheet, ws, c, text, strip_default=False):
    """trả về danh sách biểu thức C# cho các đoạn của ô (đã chú giải) hoặc Fixed(text)."""
    key = c.coordinate
    ann = ANNOT.get(sheet, {}).get(key)
    if ann is None:
        if isinstance(text, str) and text.startswith("="):
            raise SystemExit(f"{sheet}!{key}: công thức chưa chú giải: {text}")
        if text is None: return []
        if isinstance(text, (int, float)): text = str(text)
        text = text.replace("2026", YEAR)
        if DOTS.search(text) and len(DOTS.sub("", text)) != len(text) and "…" in text or ".." in text:
            print(f"  XEM LẠI {sheet}!{key}: có chấm chấm, chưa chú giải: {text!r}", file=sys.stderr)
        return [f"Tpl.Fixed({esc(text)})"]
    out = []
    for a in ann:
        kind = a[0]
        if kind == "F": out.append(f"Tpl.Fixed({esc(a[1])})")
        elif kind == "I":
            _, k, ph, *rest = a
            d = rest[0] if len(rest) > 0 else None
            hint = rest[1] if len(rest) > 1 else None
            out.append(f"Tpl.Input({esc(k)}, {esc(ph)}" + (f", @default: {esc(d)}" if d else "") + (f", hint: {esc(hint)}" if hint else "") + ")")
        elif kind == "N": out.append(f"Tpl.Number({esc(a[1])})")
        elif kind == "S": out.append(f"Tpl.Setting({esc(a[1])}, {esc(a[2])})")
        elif kind == "C": out.append(f"Tpl.Computed({esc(a[1])})")
        elif kind == "O": out.append(f"Tpl.Over({esc(a[1])}" + (f", hint: {esc(a[2])}" if len(a) > 2 else "") + ")")
        elif kind == "DOTS":
            keys, strip = a[1], a[2]
            t = str(text).replace("2026", YEAR)
            pieces, last = [], 0
            runs = list(DOTS.finditer(t))
            if len(runs) != len(keys):
                raise SystemExit(f"{sheet}!{key}: {len(runs)} dải chấm nhưng {len(keys)} khoá")
            for m, k in zip(runs, keys):
                pieces.append(("F", t[last:m.start()]))
                pieces.append(("I", k, m.group(0)))
                last = m.end()
            if t[last:]: pieces.append(("F", t[last:]))
            pieces = [p for p in pieces if not (p[0] == "F" and p[1] == "")]
            if strip:
                if pieces and pieces[0][0] == "F": pieces[0] = ("F", pieces[0][1].lstrip())
            for p in pieces:
                out.append(f"Tpl.Fixed({esc(p[1])})" if p[0] == "F" else f"Tpl.Input({esc(p[1])}, {esc(p[2])})")
        elif kind == "TAIL":
            prefix, k = a[1], a[2]
            t = str(text).replace("2026", YEAR)
            t = unicodedata.normalize("NFC", t)
            if not t.startswith(prefix): raise SystemExit(f"{sheet}!{key}: không bắt đầu bằng {prefix!r}: {t!r}")
            out.append(f"Tpl.Fixed({esc(prefix)})")
            out.append(f"Tpl.Input({esc(k)}, {esc(t[len(prefix):])})")
        elif kind == "HINT":
            prefix, k = a[1], a[2]
            t = str(text)
            if not t.startswith(prefix.rstrip()): raise SystemExit(f"{sheet}!{key}: không bắt đầu bằng {prefix!r}: {t!r}")
            rest = t[len(prefix.rstrip()):].strip()
            out.append(f"Tpl.Fixed({esc(prefix)})")
            out.append(f"Tpl.Input({esc(k)}, \"\", hint: {esc(rest)})")
    return out

def emit_sheet(wb, sheet, cls, out_path, region):
    ws = wb[sheet]
    last_col, last_row = LAST_COL[sheet], LAST_ROW[sheet]
    merges = {}
    covered = set()
    mranges = [str(r) for r in ws.merged_cells.ranges] + EXTRA_MERGES.get(sheet, [])
    for rng in mranges:
        a, b = rng.split(":")
        ca, cb = ws[a], ws[b]
        if ca.column > last_col: continue
        merges[(ca.row, ca.column)] = (cb.row - ca.row + 1, cb.column - ca.column + 1)
        for r in range(ca.row, cb.row + 1):
            for c in range(ca.column, cb.column + 1):
                if (r, c) != (ca.row, ca.column): covered.add((r, c))
    lines = []
    reg_rows = set()
    if region:
        reg_rows = set(range(region["first"], region["first"] + region["min"]))

    def cell_expr(row, col, relative=False, force=None):
        c = ws.cell(row, col)
        rs, cs = merges.get((row, col), (1, 1))
        group = [ws.cell(r, cc) for r in range(row, row + rs) for cc in range(col, col + cs)]
        flags = borders_str(group)
        text = c.value
        key = c.coordinate
        ann = force if force is not None else ANNOT.get(sheet, {}).get(key)
        is_money = bool(ann) and any(x[0] in ("C", "N", "O") and x[1] not in TEXT_KEYS and x[1] != "GhiChu"
                                     and x[1] != "TenDuAnCongTrinh" for x in ann) and "#,##0" in c.number_format
        check_font(sheet, c)
        st = style_expr(c, flags, is_money)
        if force is not None:
            ANNOT.setdefault(sheet, {})["__tmp__"] = force
            ANNOT[sheet][key] = force
        segs = segments(sheet, ws, c, text)
        if force is not None: del ANNOT[sheet][key]
        r = 0 if relative else row
        head = f"Tpl.Merged({r}, {col}, {rs}, {cs}" if (rs > 1 or cs > 1) else f"Tpl.Cell({r}, {col}"
        return f'{head}, {st}' + "".join(", " + s for s in segs) + ")"

    # ô thường
    body = []
    for row in range(1, last_row + 1):
        if row in reg_rows: continue
        for col in range(1, last_col + 1):
            if (row, col) in covered: continue
            if addr(row, col) in DROP_CELLS.get(sheet, set()): continue
            c = ws.cell(row, col)
            has_border = any(getattr(c.border, s).style for s in ("left", "right", "top", "bottom"))
            val = c.value
            if isinstance(val, str) and val.strip() == "": val = None
            is_anchor = (row, col) in merges
            if val is None and not has_border and not is_anchor: continue
            body.append("            " + cell_expr(row, col) + ",")

    # hàng mẫu của vùng lặp
    protos = {}
    if region:
        for name in ("project", "vitri", "dot", "blank"):
            r = region[name]
            h = ws.row_dimensions[r].height or DEFAULT_ROW
            cells = []
            for col in range(1, last_col + 1):
                letter = L(col)
                if name == "project" and col <= 6:
                    # ô Mục lục ngân sách: Input có mặc định
                    c = ws.cell(r, col)
                    flags = borders_str([c])
                    st = style_expr(c, flags, False)
                    cells.append(f'Tpl.Cell(0, {col}, {st}, Tpl.Input({esc(REGION_KEYS[letter])}, "", @default: {esc(REGION_DEFAULTS[letter])}))')
                elif name != "blank" and col == 7:
                    cells.append(cell_expr(r, col, relative=True, force=[("C", "Line.Text")]).replace("Tpl.Cell(0, 7,", "Tpl.Cell(0, 7,"))
                elif name != "blank" and col == 8:
                    c = ws.cell(r, col)
                    flags = borders_str([c])
                    st = style_expr(c, flags, True)
                    cells.append(f'Tpl.Cell(0, 8, {st}, Tpl.Computed("Line.Amount"))')
                else:
                    c = ws.cell(r, col)
                    flags = borders_str([c])
                    st = style_expr(c, flags, False)
                    cells.append(f"Tpl.Cell(0, {col}, {st})")
            protos[name] = (h, cells)

    heights = []
    for r in range(1, last_row + 1):
        if r in reg_rows: continue
        h = ws.row_dimensions[r].height
        if h: heights.append(f"[{r}] = {num(h)}")

    widths = []
    for col in range(1, last_col + 1):
        dim = None
        for k, d in ws.column_dimensions.items():
            if d.min and d.max and d.min <= col <= d.max: dim = d
        # openpyxl cho bề rộng LƯU TRONG TỆP (đã gồm 5px đệm). Excel hiện (và ClosedXML, và ExcelFitScale: px = w*7+5)
        # dùng bề rộng theo KÝ TỰ = (px − 5)/7 với px = làm tròn(lưu*7).
        if dim is not None and dim.width:
            w = round((round(dim.width * 7) - 5) / 7, 4)
        else:
            w = DEFAULT_COL
        widths.append(num(w))

    with open(out_path, "w", encoding="utf-8") as f:
        w = f.write
        w("// Sinh từ reference/Mẫu.xlsx bởi tools/gen_form_templates.py MỘT LẦN. Từ nay sửa tay tệp này;\n")
        w("// TemplateMatchesMauTests so nó với Mẫu.xlsx để không trôi khỏi mẫu.\n")
        w("using ConstructionFinance.Application.Printing;\n\n")
        w("namespace ConstructionFinance.Application.Forms;\n\n")
        w(f"/// Bản gốc của sheet \"{sheet}\" trong Mẫu.xlsx.\n")
        w(f"public static class {cls}\n{{\n")
        w("    private static CellStyle S(double size, bool bold = false, bool italic = false, bool underline = false,\n")
        w("        CellAlign align = CellAlign.General, CellVAlign v = CellVAlign.Bottom, bool wrap = false,\n")
        w("        string borders = \"\", string? format = null) =>\n")
        w("        Tpl.Style(size, bold, italic, underline, align, v, wrap, borders, format);\n\n")
        w("    public static FormTemplate Create() => new(\n")
        w(f"        SheetName: {esc(sheet)},\n")
        w("        Orientation: PrintOrientation.Portrait,\n")
        w("        ColumnWidths: new double[] { " + ", ".join(widths) + " },\n")
        w("        RowHeights: new Dictionary<int, double>\n        {\n")
        for i in range(0, len(heights), 6):
            w("            " + ", ".join(heights[i:i+6]) + ",\n")
        w("        },\n")
        w("        Cells: new[]\n        {\n")
        w("\n".join(body) + "\n")
        w("        },\n")
        if region:
            w("        Repeat: new RepeatRegion(\n")
            w(f"            FirstRow: {region['first']}, MinRows: {region['min']},\n")
            for i, name in enumerate(("project", "vitri", "dot", "blank")):
                h, cells = protos[name]
                label = {"project": "ProjectRow", "vitri": "ViTriRow", "dot": "DotRow", "blank": "BlankRow"}[name]
                w(f"            {label}: new TemplateRow({num(h)}, new[]\n            {{\n")
                for c in cells: w("                " + c + ",\n")
                w("            })" + (",\n" if i < 3 else ")") + "\n")
        else:
            w("        Repeat: null");
        w(");\n}\n")
    print("đã ghi", out_path, "—", len(body), "ô,", len(covered), "ô bị gộp che", file=sys.stderr)

if __name__ == "__main__":
    src, outdir = sys.argv[1], sys.argv[2]
    wb = openpyxl.load_workbook(src)
    emit_sheet(wb, "Thông tri", "ThongTriTemplate", os.path.join(outdir, "ThongTriTemplate.cs"), REGION)
    emit_sheet(wb, "KS chi", "KsChiTemplate", os.path.join(outdir, "KsChiTemplate.cs"), None)
    emit_sheet(wb, "GĐNTT", "GdnttTemplate", os.path.join(outdir, "GdnttTemplate.cs"), None)
```

- [ ] **Step 2: Chạy script**

Run: `python3 -I tools/gen_form_templates.py reference/Mẫu.xlsx src/ConstructionFinance.Application/Forms`
Expected: ba dòng `đã ghi …`; hai dòng cảnh báo underline (`KS chi!A22`, `GĐNTT!D2`) là bình thường (đã hỗ trợ gạch chân). KHÔNG được có dòng `XEM LẠI` hay `SystemExit`.

- [ ] **Step 3: Biên dịch**

Run: `dotnet build src/ConstructionFinance.Application --nologo -v q 2>&1 | tail -3`
Expected: `Build succeeded`, 0 cảnh báo. Nếu có lỗi cú pháp từ tệp sinh ra, sửa SCRIPT (không sửa tay tệp sinh) rồi chạy lại bước 2.

- [ ] **Step 4: Viết test so với Mẫu**

```csharp file=tests/ConstructionFinance.Application.Tests/Forms/TemplateMatchesMauTests.cs
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using ConstructionFinance.Application.Forms;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Forms;

/// So thẳng ba bản gốc với reference/Mẫu.xlsx. Đỏ khi ai sửa bản gốc lệch khỏi Mẫu, VÀ khi Mẫu đổi
/// mà bản gốc không theo. Đọc Mẫu bằng ClosedXML — khác thư viện với script sinh bản gốc (openpyxl),
/// nên hai bên không cùng sai một kiểu.
///
/// Chỉ bỏ qua đúng các khác biệt đã ghi ở plan §"Chỗ plan chốt thêm": chữ ghi chú không in (Hint),
/// năm hiện tại ({năm} ↔ 2026), vùng lặp của Thông tri, ô công thức (so vị trí+loại, không so chữ),
/// hai gộp thêm của GĐNTT, cột/ô ngoài vùng biểu mẫu (GĐNTT cột J–M).
public class TemplateMatchesMauTests
{
    private sealed record Sheet(string MauName, Func<FormTemplate> Create, int LastRow, int LastCol);

    private static readonly Dictionary<string, Sheet> Sheets = new()
    {
        ["Thông tri"] = new("Thông tri", ThongTriTemplate.Create, LastRow: 33, LastCol: 8),
        ["KS chi"] = new("KS chi", KsChiTemplate.Create, LastRow: 34, LastCol: 5),
        ["GĐNTT"] = new("GĐNTT", GdnttTemplate.Create, LastRow: 36, LastCol: 9),
    };

    /// Gộp chỉ có ở bản gốc (cột H của Mẫu rộng 0,86 nên số đặt ở H13/H25 chỉ ra ###).
    private static readonly Dictionary<string, string[]> ExtraMerges = new()
    {
        ["GĐNTT"] = new[] { "H13:I13", "H25:I25", "D28:I28" },
    };

    /// Mẫu ghi thẳng CHỮ `="Vị trí 1"!D9` (không bao giờ tính ra) vào GĐNTT!D28: lỗi của Mẫu.
    private static readonly HashSet<string> LiteralTextWhereMauHasBug = new() { "GĐNTT!D28" };

    /// Ghi chú nội bộ của người soạn Mẫu, không phải nội dung biểu mẫu.
    private static readonly HashSet<string> MauNotes = new() { "GĐNTT!M13" };

    private const int RegionFirstRow = 15;
    private const int RegionRows = 9;
    private const int RegionProjectRow = 15, RegionViTriRow = 16, RegionDotRow = 17, RegionBlankRow = 20;

    private static readonly Lazy<XLWorkbook> Mau = new(() => new XLWorkbook(FindMau()));

    private static string FindMau()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "reference", "Mẫu.xlsx");
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("Không thấy reference/Mẫu.xlsx — tệp này phải nằm trong repo.");
    }

    public static IEnumerable<object[]> SheetNames() => Sheets.Keys.Select(name => new object[] { name });

    private static string Nfc(string? text) => (text ?? "").Normalize(NormalizationForm.FormC);

    private static string Squash(string text) => Regex.Replace(Nfc(text), @"\s+", " ").Trim();

    private static string Where(string sheet, int row, int col) => $"{sheet}!{XLHelper.GetColumnLetterFromNumber(col)}{row}";

    private static void ShouldHaveNoProblems(List<string> problems) =>
        Assert.True(problems.Count == 0, "\n" + string.Join("\n", problems));

    // ===== khổ giấy, cột, hàng =====

    [Theory]
    [MemberData(nameof(SheetNames))]
    public void Paper_and_orientation_match(string name)
    {
        var template = Sheets[name].Create();
        var ws = Mau.Value.Worksheet(name);

        ws.PageSetup.PaperSize.ShouldBe(XLPaperSize.A4Paper);
        ws.PageSetup.PageOrientation.ShouldBe(XLPageOrientation.Portrait);
        template.Orientation.ShouldBe(ConstructionFinance.Application.Printing.PrintOrientation.Portrait);
        template.SheetName.ShouldBe(name);
    }

    [Theory]
    [MemberData(nameof(SheetNames))]
    public void Column_widths_match(string name)
    {
        var sheet = Sheets[name];
        var template = sheet.Create();
        var ws = Mau.Value.Worksheet(name);
        var problems = new List<string>();

        template.ColumnWidths.Count.ShouldBe(sheet.LastCol);
        for (var col = 1; col <= sheet.LastCol; col++)
            if (Math.Abs(ws.Column(col).Width - template.ColumnWidths[col - 1]) > 0.01)
                problems.Add($"cột {col}: Mẫu {ws.Column(col).Width} ≠ bản gốc {template.ColumnWidths[col - 1]}");

        ShouldHaveNoProblems(problems);
    }

    [Theory]
    [MemberData(nameof(SheetNames))]
    public void Row_heights_match(string name)
    {
        var sheet = Sheets[name];
        var template = sheet.Create();
        var ws = Mau.Value.Worksheet(name);
        var problems = new List<string>();
        var regionRows = template.Repeat is null
            ? new HashSet<int>()
            : Enumerable.Range(RegionFirstRow, RegionRows).ToHashSet();

        for (var row = 1; row <= sheet.LastRow; row++)
        {
            if (regionRows.Contains(row)) continue;
            var expected = template.RowHeights.TryGetValue(row, out var h) ? h : 15.0;
            if (Math.Abs(ws.Row(row).Height - expected) > 0.01)
                problems.Add($"hàng {row}: Mẫu {ws.Row(row).Height} ≠ bản gốc {expected}");
        }

        if (template.Repeat is { } region)
        {
            Height(region.ProjectRow.Height, RegionProjectRow);
            Height(region.ViTriRow.Height, RegionViTriRow);
            Height(region.DotRow.Height, RegionDotRow);
            Height(region.BlankRow.Height, RegionBlankRow);
        }

        ShouldHaveNoProblems(problems);

        void Height(double expected, int mauRow)
        {
            if (Math.Abs(ws.Row(mauRow).Height - expected) > 0.01)
                problems.Add($"hàng mẫu vùng lặp (Mẫu hàng {mauRow}): Mẫu {ws.Row(mauRow).Height} ≠ bản gốc {expected}");
        }
    }

    // ===== ô gộp =====

    [Theory]
    [MemberData(nameof(SheetNames))]
    public void Merged_ranges_match(string name)
    {
        var sheet = Sheets[name];
        var template = sheet.Create();
        var ws = Mau.Value.Worksheet(name);

        var fromMau = ws.MergedRanges
            .Where(range => range.RangeAddress.FirstAddress.ColumnNumber <= sheet.LastCol)
            .Select(range => range.RangeAddress.ToStringRelative())
            .ToHashSet();
        var fromTemplate = template.Cells
            .Where(cell => cell.RowSpan > 1 || cell.ColSpan > 1)
            .Select(cell => $"{XLHelper.GetColumnLetterFromNumber(cell.Col)}{cell.Row}:" +
                            $"{XLHelper.GetColumnLetterFromNumber(cell.Col + cell.ColSpan - 1)}{cell.Row + cell.RowSpan - 1}")
            .ToHashSet();
        var extras = ExtraMerges.GetValueOrDefault(name, Array.Empty<string>());

        var onlyInTemplate = fromTemplate.Except(fromMau).Except(extras).ToList();
        var onlyInMau = fromMau.Except(fromTemplate).ToList();
        var missingExtras = extras.Except(fromTemplate).ToList();

        var problems = new List<string>();
        problems.AddRange(onlyInTemplate.Select(range => $"gộp {range} chỉ có ở bản gốc"));
        problems.AddRange(onlyInMau.Select(range => $"gộp {range} chỉ có ở Mẫu"));
        problems.AddRange(missingExtras.Select(range => $"gộp thêm {range} đã khai nhưng bản gốc không có"));
        ShouldHaveNoProblems(problems);
    }

    // ===== từng ô của bản gốc đối chiếu ô Mẫu =====

    [Theory]
    [MemberData(nameof(SheetNames))]
    public void Every_template_cell_matches_the_Mau_cell(string name)
    {
        var template = Sheets[name].Create();
        var ws = Mau.Value.Worksheet(name);
        var problems = new List<string>();

        foreach (var cell in template.Cells)
            CompareCell(name, ws, cell, cell.Row, problems);

        if (template.Repeat is { } region)
        {
            CompareRegionRow(name, ws, region.ProjectRow, RegionProjectRow, problems);
            CompareRegionRow(name, ws, region.ViTriRow, RegionViTriRow, problems);
            CompareRegionRow(name, ws, region.DotRow, RegionDotRow, problems);
            CompareRegionRow(name, ws, region.BlankRow, RegionBlankRow, problems);
        }

        ShouldHaveNoProblems(problems);
    }

    private static void CompareRegionRow(string name, IXLWorksheet ws, TemplateRow row, int mauRow, List<string> problems)
    {
        foreach (var cell in row.Cells)
            CompareCell(name, ws, cell with { Row = mauRow }, mauRow, problems, skipText: true);
    }

    private static void CompareCell(
        string sheet, IXLWorksheet ws, TemplateCell cell, int mauRow, List<string> problems, bool skipText = false)
    {
        var where = Where(sheet, mauRow, cell.Col);
        var mau = ws.Cell(mauRow, cell.Col);
        var style = cell.Style;

        // --- kiểu ---
        var hasText = cell.Segments.Count > 0 || !string.IsNullOrWhiteSpace(mau.GetString());
        if (hasText && mau.Style.Font.FontName != "Times New Roman")
            problems.Add($"{where}: phông Mẫu {mau.Style.Font.FontName}");
        if (Math.Abs(mau.Style.Font.FontSize - style.FontSize) > 0.01)
            problems.Add($"{where}: cỡ chữ Mẫu {mau.Style.Font.FontSize} ≠ {style.FontSize}");
        if (mau.Style.Font.Bold != style.Bold) problems.Add($"{where}: đậm Mẫu {mau.Style.Font.Bold} ≠ {style.Bold}");
        if (mau.Style.Font.Italic != style.Italic) problems.Add($"{where}: nghiêng Mẫu {mau.Style.Font.Italic} ≠ {style.Italic}");
        var underlined = mau.Style.Font.Underline != XLFontUnderlineValues.None;
        if (underlined != style.Underline) problems.Add($"{where}: gạch chân Mẫu {underlined} ≠ {style.Underline}");
        if (ToAlign(mau.Style.Alignment.Horizontal) != style.Align)
            problems.Add($"{where}: canh ngang Mẫu {mau.Style.Alignment.Horizontal} ≠ {style.Align}");
        if (ToVAlign(mau.Style.Alignment.Vertical) != style.VAlign)
            problems.Add($"{where}: canh dọc Mẫu {mau.Style.Alignment.Vertical} ≠ {style.VAlign}");
        if (mau.Style.Alignment.WrapText != style.Wrap) problems.Add($"{where}: xuống dòng Mẫu {mau.Style.Alignment.WrapText} ≠ {style.Wrap}");
        if (style.NumberFormat is not null && MauNumberFormat(mau) != style.NumberFormat)
            problems.Add($"{where}: định dạng số Mẫu «{MauNumberFormat(mau)}» ≠ «{style.NumberFormat}»");

        var borders = MauBorders(ws, mauRow, cell.Col, cell.RowSpan, cell.ColSpan);
        if (borders != style.Borders) problems.Add($"{where}: viền Mẫu {borders} ≠ bản gốc {style.Borders}");

        if (skipText) return;

        // --- chữ ---
        var mauText = Nfc(mau.GetString());
        var key = $"{sheet}!{XLHelper.GetColumnLetterFromNumber(cell.Col)}{mauRow}";
        var kinds = cell.Segments.Select(s => s.Kind).ToList();
        var year = (Func<string, string>)(text => text.Replace("{năm}", "2026"));

        if (kinds.Any(k => k is SegmentKind.Computed or SegmentKind.Overridable))
        {
            // Ô tự tính: Mẫu phải để công thức, hoặc để trống (app tự tính cả những chỗ Mẫu quên).
            var acceptable = mau.HasFormula || string.IsNullOrWhiteSpace(mauText) || LiteralTextWhereMauHasBug.Contains(key)
                             || kinds.Contains(SegmentKind.Overridable);
            if (!acceptable) problems.Add($"{where}: ô tự tính nhưng Mẫu có chữ «{mauText}»");
            return;
        }

        if (kinds.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(mauText)) problems.Add($"{where}: Mẫu có chữ «{mauText}» nhưng bản gốc không có đoạn nào");
            return;
        }

        // Ô số tự nhập: Mẫu có thể để sẵn một số ví dụ (KS chi C19 = 0); số ví dụ không phải nội dung biểu mẫu.
        if (kinds.All(k => k == SegmentKind.InputNumber)) return;

        if (kinds.All(k => k == SegmentKind.Fixed))
        {
            var fixedText = year(string.Concat(cell.Segments.Select(s => s.Text)));
            if (Nfc(fixedText) != mauText) problems.Add($"{where}: chữ Mẫu «{mauText}» ≠ bản gốc «{fixedText}»");
            return;
        }

        // Ô có chỗ điền: chữ Mẫu phải khớp một trong ba cách ghép (chấm chấm / ghi chú / điền sẵn),
        // so sau khi gộp khoảng trắng.
        string Join(Func<TemplateSegment, string> pick) => Squash(year(string.Concat(cell.Segments.Select(pick))));
        var candidates = new[]
        {
            Join(s => s.Text),
            Join(s => s.Kind == SegmentKind.Fixed ? s.Text : s.Hint ?? ""),
            Join(s => s.Kind == SegmentKind.Fixed ? s.Text : s.Default ?? s.Text),
        };
        if (!candidates.Contains(Squash(mauText)))
            problems.Add($"{where}: chữ Mẫu «{mauText}» không khớp bản gốc ({string.Join(" | ", candidates.Distinct())})");
    }

    /// ClosedXML trả `Format` rỗng cho định dạng có sẵn của Excel (id 3 = `#,##0`, id 37 = `#,##0_);(#,##0)`).
    private static string MauNumberFormat(IXLCell cell)
    {
        var format = cell.Style.NumberFormat;
        if (format.Format.Length > 0) return format.Format;
        return format.NumberFormatId switch
        {
            3 => "#,##0",
            37 => "#,##0_);(#,##0)",
            var id => $"(định dạng có sẵn số {id})",
        };
    }

    private static CellBorders MauBorders(IXLWorksheet ws, int row, int col, int rowSpan, int colSpan)
    {
        BorderLine Of(XLBorderStyleValues value) => value switch
        {
            XLBorderStyleValues.None => BorderLine.None,
            XLBorderStyleValues.Hair => BorderLine.Hair,
            _ => BorderLine.Thin,
        };

        BorderLine Edge(IEnumerable<IXLCell> cells, Func<IXLCell, XLBorderStyleValues> pick)
        {
            var lines = cells.Select(c => Of(pick(c))).Where(line => line != BorderLine.None).ToList();
            return lines.Count == 0 ? BorderLine.None : lines.All(l => l == BorderLine.Hair) ? BorderLine.Hair : BorderLine.Thin;
        }

        IEnumerable<IXLCell> Column(int c) => Enumerable.Range(row, rowSpan).Select(r => ws.Cell(r, c));
        IEnumerable<IXLCell> RowCells(int r) => Enumerable.Range(col, colSpan).Select(c => ws.Cell(r, c));

        return new CellBorders(
            Left: Edge(Column(col), c => c.Style.Border.LeftBorder),
            Right: Edge(Column(col + colSpan - 1), c => c.Style.Border.RightBorder),
            Top: Edge(RowCells(row), c => c.Style.Border.TopBorder),
            Bottom: Edge(RowCells(row + rowSpan - 1), c => c.Style.Border.BottomBorder));
    }

    private static CellAlign ToAlign(XLAlignmentHorizontalValues value) => value switch
    {
        XLAlignmentHorizontalValues.General => CellAlign.General,
        XLAlignmentHorizontalValues.Left => CellAlign.Left,
        XLAlignmentHorizontalValues.Center => CellAlign.Center,
        XLAlignmentHorizontalValues.Right => CellAlign.Right,
        XLAlignmentHorizontalValues.Justify => CellAlign.Justify,
        _ => throw new NotSupportedException($"Mẫu dùng canh ngang chưa hỗ trợ: {value}")
    };

    private static CellVAlign ToVAlign(XLAlignmentVerticalValues value) => value switch
    {
        XLAlignmentVerticalValues.Bottom => CellVAlign.Bottom,
        XLAlignmentVerticalValues.Center => CellVAlign.Center,
        XLAlignmentVerticalValues.Top => CellVAlign.Top,
        _ => throw new NotSupportedException($"Mẫu dùng canh dọc chưa hỗ trợ: {value}")
    };

    // ===== chiều ngược lại: Mẫu có gì thì bản gốc phải có =====

    [Theory]
    [MemberData(nameof(SheetNames))]
    public void Every_Mau_cell_with_text_or_border_is_covered_by_the_template(string name)
    {
        var sheet = Sheets[name];
        var template = sheet.Create();
        var ws = Mau.Value.Worksheet(name);
        var problems = new List<string>();

        var covered = new HashSet<(int Row, int Col)>();
        foreach (var cell in template.Cells)
            for (var r = cell.Row; r < cell.Row + cell.RowSpan; r++)
            for (var c = cell.Col; c < cell.Col + cell.ColSpan; c++)
                covered.Add((r, c));
        if (template.Repeat is not null)
            for (var r = RegionFirstRow; r < RegionFirstRow + RegionRows; r++)
            for (var c = 1; c <= sheet.LastCol; c++)
                covered.Add((r, c));

        for (var row = 1; row <= sheet.LastRow; row++)
        for (var col = 1; col <= sheet.LastCol; col++)
        {
            var cell = ws.Cell(row, col);
            var hasBorder = MauBorders(ws, row, col, 1, 1).Any;
            var hasText = !string.IsNullOrWhiteSpace(cell.GetString()) || cell.HasFormula;
            if (!hasText && !hasBorder) continue;
            if (MauNotes.Contains(Where(name, row, col))) continue;
            if (!covered.Contains((row, col)))
                problems.Add($"{Where(name, row, col)}: Mẫu có {(hasText ? "chữ/công thức" : "viền")} nhưng bản gốc không phủ ô này");
        }

        ShouldHaveNoProblems(problems);
    }

    // ===== những thứ riêng =====

    [Fact]
    public void Budget_code_defaults_show_the_same_text_as_the_Mau_number_formats()
    {
        var template = ThongTriTemplate.Create();
        var ws = Mau.Value.Worksheet("Thông tri");
        var defaults = template.Repeat!.ProjectRow.Cells
            .Where(cell => cell.Col <= 6)
            .OrderBy(cell => cell.Col)
            .Select(cell => cell.Segments.Single().Default)
            .ToList();

        for (var col = 1; col <= 6; col++)
            defaults[col - 1].ShouldBe(ws.Cell(RegionProjectRow, col).GetFormattedString(), $"cột {col}");
    }

    [Theory]
    [MemberData(nameof(SheetNames))]
    public void Segment_keys_are_unique_per_input_and_never_empty(string name)
    {
        var template = Sheets[name].Create();
        var inputs = template.AllSegments()
            .Where(s => s.Kind is SegmentKind.Input or SegmentKind.InputNumber or SegmentKind.Overridable or SegmentKind.SettingInput)
            .ToList();

        inputs.ShouldAllBe(s => !string.IsNullOrWhiteSpace(s.Key));
        inputs.GroupBy(s => s.Key).Where(g => g.Count() > 1).Select(g => g.Key)
            .ShouldBeEmpty("khoá ô nhập bị trùng — hai ô cùng ghi vào một chỗ");
        template.AllSegments().Where(s => s.Kind is SegmentKind.Computed)
            .ShouldAllBe(s => !string.IsNullOrWhiteSpace(s.Key));
    }
}
```

- [ ] **Step 5: Chạy test**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --nologo -v q --filter TemplateMatchesMauTests 2>&1 | tail -40`
Expected: xanh. Nếu đỏ, ĐỌC danh sách lệch: lệch ở bản gốc sinh ra → sửa SCRIPT/chú giải và chạy lại bước 2; lệch do cách ClosedXML đọc khác openpyxl (vd. `GetFormattedString()` của `"0"\1\1`) → sửa TEST và ghi lý do bằng một dòng chú thích. Nếu `new XLWorkbook(...)` ném vì liên kết ngoài `[3]!VND`, mở bằng `new XLWorkbook(new LoadOptions { RecalculateAllFormulas = false })` hoặc sao chép tệp sang thư mục tạm rồi gỡ `externalLinks` khỏi gói — nhưng KHÔNG sửa Mẫu.xlsx trong repo.

- [ ] **Step 6: Chạy lại mà KHÔNG cần script — kiểm tra test thật sự bắt lỗi**

Sửa tạm một số trong `KsChiTemplate.cs` (đổi cỡ chữ 13 → 12 ở một ô), chạy lại test, xác nhận đỏ đúng ô đó, rồi hoàn tác (`git checkout` không dùng được vì tệp chưa commit — sửa tay lại).

- [ ] **Step 7: Commit**

```bash
git add tools/gen_form_templates.py src/ConstructionFinance.Application/Forms/*Template.cs tests/ConstructionFinance.Application.Tests/Forms/TemplateMatchesMauTests.cs
git commit -m "feat(forms): ba bản gốc chép từ Mẫu.xlsx và test so thẳng với Mẫu

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Bộ trộn — bản gốc + số đã tính + chữ đã gõ → tờ đã trộn

**Files:**
- Create: `src/ConstructionFinance.Application/Forms/FormSheetResolver.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Forms/FormSheetResolverTests.cs`

**Interfaces:**
- Consumes: Task 1 (kiểu), Task 2 (`ThongTriTemplate`, `KsChiTemplate`, `GdnttTemplate`), `ThongTriLine` (FormModels.cs).
- Produces: `FormSheetResolver.Resolve(FormTemplate template, FormValues computed, FormInputSet inputs, FormOrgInfo org, int year, IReadOnlyList<ThongTriLine>? lines = null) : ResolvedSheet`; hằng `YearToken = "{năm}"`, `LineText = "Line.Text"`, `LineAmount = "Line.Amount"`, `DefaultRowHeight = 15`.

- [ ] **Step 1: Viết test (đỏ)**

```csharp file=tests/ConstructionFinance.Application.Tests/Forms/FormSheetResolverTests.cs
using ConstructionFinance.Application.Forms;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Forms;

public class FormSheetResolverTests
{
    private static readonly FormOrgInfo Org = new("Sở Tài chính", "Ban QLDA số 1");

    private static FormValues ThongTriValues(decimal total = 0m) =>
        new FormValues().Number("Total", total).Text("TotalInWords", "Không đồng.");

    private static ResolvedSheet ResolveThongTri(
        FormInputSet? inputs = null, IReadOnlyList<ThongTriLine>? lines = null, int year = 2031, FormOrgInfo? org = null) =>
        FormSheetResolver.Resolve(ThongTriTemplate.Create(), ThongTriValues(), inputs ?? new FormInputSet(),
            org ?? Org, year, lines ?? new[] { new ThongTriLine(0, "Dự án A", 0m) });

    internal static ResolvedCell Cell(ResolvedSheet sheet, int row, int col) =>
        sheet.Cells.Single(c => c.Row == row && c.Col == col);

    private static IReadOnlyList<ThongTriLine> Lines(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new ThongTriLine(i == 0 ? 0 : i % 2 == 1 ? 1 : 2, $"Dòng {i}", 1_000m * i))
            .ToList();

    // ===== chữ, năm, ô trống =====

    [Fact]
    public void Resolve_substitutes_the_year_in_template_text()
    {
        var sheet = ResolveThongTri(year: 2031);

        Cell(sheet, 9, 1).DisplayText.ShouldBe("Về tháng      năm 2031");
    }

    [Fact]
    public void User_text_containing_the_year_token_is_not_substituted()
    {
        var inputs = new FormInputSet();
        inputs.Set("VeThangNam", "{năm}");

        var sheet = ResolveThongTri(inputs, year: 2031);

        Cell(sheet, 9, 1).DisplayText.ShouldBe("Về{năm}");
    }

    [Fact]
    public void Empty_input_prints_the_Mau_placeholder_and_is_flagged_as_placeholder()
    {
        var sheet = ResolveThongTri();
        var cell = Cell(sheet, 3, 1);

        cell.DisplayText.ShouldBe("Số      .............");
        var input = cell.Segments.Single(s => s.Kind == SegmentKind.Input);
        input.IsPlaceholder.ShouldBeTrue();
        input.Value.ShouldBe("");
        input.Hint.ShouldBe(".............");
    }

    [Fact]
    public void Typed_input_replaces_the_placeholder()
    {
        var inputs = new FormInputSet();
        inputs.Set("SoThongTri", "12/TT");

        var sheet = ResolveThongTri(inputs);
        var input = Cell(sheet, 3, 1).Segments.Single(s => s.Kind == SegmentKind.Input);

        Cell(sheet, 3, 1).DisplayText.ShouldBe("Số      12/TT");
        input.IsPlaceholder.ShouldBeFalse();
        input.Value.ShouldBe("12/TT");
    }

    [Fact]
    public void Default_is_used_until_the_user_types_and_an_explicit_empty_clears_it()
    {
        string KhoanText(FormInputSet inputs) =>
            Cell(ResolveThongTri(inputs), 15, 2).Segments.Single().Text;

        KhoanText(new FormInputSet()).ShouldBe("011");

        var cleared = new FormInputSet();
        cleared.Set("Khoan", "");
        KhoanText(cleared).ShouldBe("");

        var typed = new FormInputSet();
        typed.Set("Khoan", "012");
        KhoanText(typed).ShouldBe("012");
    }

    [Fact]
    public void Setting_input_reads_the_org_value_and_falls_back_to_the_Mau_text()
    {
        var withOrg = Cell(ResolveThongTri(), 1, 1);
        withOrg.DisplayText.ShouldBe("Sở Tài chính");

        var blankOrg = Cell(ResolveThongTri(org: new FormOrgInfo(null, "  ".Trim())), 1, 1);
        blankOrg.DisplayText.ShouldBe("Đơn vị cấp trên");
        blankOrg.Segments.Single().IsPlaceholder.ShouldBeTrue();
    }

    // ===== số =====

    [Fact]
    public void Number_input_formats_with_dots_and_stays_blank_until_typed()
    {
        var kschi = KsChiTemplate.Create();
        var values = new FormValues { BlankMissing = true };

        ResolvedCell Cell14(FormInputSet inputs) => Cell(
            FormSheetResolver.Resolve(kschi, values, inputs, Org, 2026), 14, 3);

        Cell14(new FormInputSet()).DisplayText.ShouldBe("");
        var typed = new FormInputSet();
        typed.Set("ThuHoiCacNamTruoc", FormInputSet.ToStored(1_500_000m));
        var cell = Cell14(typed);
        cell.DisplayText.ShouldBe("1.500.000");
        cell.Segments.Single().Number.ShouldBe(1_500_000m);
        cell.Segments.Single().Value.ShouldBe("1.500.000");
    }

    [Fact]
    public void Computed_number_shows_zero_as_zero_like_the_Mau()
    {
        var sheet = ResolveThongTri();

        Cell(sheet, 24, 8).DisplayText.ShouldBe("0");
        Cell(sheet, 24, 8).Segments.Single().Number.ShouldBe(0m);
    }

    [Fact]
    public void Computed_number_without_a_cell_format_uses_the_default_grouping()
    {
        var kschi = KsChiTemplate.Create();
        var values = new FormValues { BlankMissing = true }
            .Number("Accepted.NgoaiHoi", 2_000_000m);

        var sheet = FormSheetResolver.Resolve(kschi, values, new FormInputSet(), Org, 2026);

        Cell(sheet, 7, 5).DisplayText.ShouldBe("2.000.000");
    }

    // ===== tự tính cho gõ đè =====

    [Fact]
    public void Overridable_text_shows_the_computed_value_until_typed_over()
    {
        var gdntt = GdnttTemplate.Create();
        var values = new FormValues { BlankMissing = true }.Text("TenDuAnCongTrinh", "Dự án A");

        ResolvedSegment Segment(FormInputSet inputs) =>
            Cell(FormSheetResolver.Resolve(gdntt, values, inputs, Org, 2026), 8, 3).Segments.Single();

        var automatic = Segment(new FormInputSet());
        automatic.Text.ShouldBe("Dự án A");
        automatic.IsOverridden.ShouldBeFalse();

        var typed = new FormInputSet();
        typed.Set("TenDuAnCongTrinh", "Tên tôi tự gõ");
        var overridden = Segment(typed);
        overridden.Text.ShouldBe("Tên tôi tự gõ");
        overridden.IsOverridden.ShouldBeTrue();
    }

    [Fact]
    public void Overridable_number_parses_the_stored_text_and_ignores_garbage()
    {
        var gdntt = GdnttTemplate.Create();
        var values = new FormValues { BlankMissing = true }.Number("UnrecoveredAdvanceBefore", 6_000_000m);

        ResolvedSegment Segment(FormInputSet inputs) =>
            Cell(FormSheetResolver.Resolve(gdntt, values, inputs, Org, 2026), 13, 8).Segments.Single();

        Segment(new FormInputSet()).Number.ShouldBe(6_000_000m);

        var typed = new FormInputSet();
        typed.Set("UnrecoveredAdvanceBefore", FormInputSet.ToStored(15_000_000m));
        var overridden = Segment(typed);
        overridden.Number.ShouldBe(15_000_000m);
        overridden.IsOverridden.ShouldBeTrue();
        overridden.Text.ShouldBe("15.000.000 đ");

        var garbage = new FormInputSet();
        garbage.Set("UnrecoveredAdvanceBefore", "abc");
        var fallback = Segment(garbage);
        fallback.Number.ShouldBe(6_000_000m);
        fallback.IsOverridden.ShouldBeFalse();
    }

    // ===== khoá thiếu =====

    [Fact]
    public void Missing_computed_key_throws_unless_the_values_blank_missing_keys()
    {
        var template = ThongTriTemplate.Create();
        var lines = new[] { new ThongTriLine(0, "DA", 0m) };

        Should.Throw<InvalidOperationException>(() =>
            FormSheetResolver.Resolve(template, new FormValues(), new FormInputSet(), Org, 2026, lines))
            .Message.ShouldContain("Total");

        var sheet = FormSheetResolver.Resolve(template, new FormValues { BlankMissing = true },
            new FormInputSet(), Org, 2026, lines);
        Cell(sheet, 24, 8).DisplayText.ShouldBe("");
    }

    // ===== vùng lặp của Thông tri =====

    [Fact]
    public void Resolve_with_one_project_line_fills_the_rest_of_the_region_with_blank_rows()
    {
        var sheet = ResolveThongTri();

        Cell(sheet, 15, 7).DisplayText.ShouldBe("Dự án A");
        sheet.RowHeights[14].ShouldBe(24.95, 0.001);           // hàng 15
        for (var row = 16; row <= 23; row++)
            sheet.RowHeights[row - 1].ShouldBe(24.95, 0.001);  // hàng trống
        Cell(sheet, 24, 7).DisplayText.ShouldBe("Cộng");
    }

    [Fact]
    public void Resolve_with_exactly_nine_lines_keeps_Cong_on_row_24()
    {
        var sheet = ResolveThongTri(lines: Lines(9));

        Cell(sheet, 24, 7).DisplayText.ShouldBe("Cộng");
        Cell(sheet, 25, 1).DisplayText.ShouldBe("Bằng chữ: ");
    }

    [Fact]
    public void Resolve_inserts_rows_before_Cong_when_there_are_more_than_nine_lines()
    {
        var sheet = ResolveThongTri(lines: Lines(30));

        Cell(sheet, 15 + 29, 7).DisplayText.ShouldBe("Dòng 29");
        Cell(sheet, 45, 7).DisplayText.ShouldBe("Cộng");       // 24 + 21 dòng thêm
        Cell(sheet, 46, 1).DisplayText.ShouldBe("Bằng chữ: ");
        sheet.RowHeights.Count.ShouldBeGreaterThanOrEqualTo(54); // 33 + 21: các hàng chữ ký cũng dời
        sheet.Cells.Any(c => c.Row == 24 && c.DisplayText == "Cộng").ShouldBeFalse();
    }

    [Fact]
    public void Region_rows_take_the_prototype_of_their_level()
    {
        var lines = new[]
        {
            new ThongTriLine(0, "DA", 300m), new ThongTriLine(1, "Vị trí 1", 300m), new ThongTriLine(2, "Đợt 1", 300m)
        };

        var sheet = ResolveThongTri(lines: lines);

        sheet.RowHeights[15].ShouldBe(24.95, 0.001);   // hàng 16: vị trí
        sheet.RowHeights[16].ShouldBe(47.25, 0.001);   // hàng 17: đợt
        Cell(sheet, 17, 7).DisplayText.ShouldBe("Đợt 1");
        Cell(sheet, 17, 8).Segments.Single().Number.ShouldBe(300m);
        Cell(sheet, 16, 7).Style.Bold.ShouldBeTrue();
        Cell(sheet, 17, 7).Style.Bold.ShouldBeFalse();
    }

    [Fact]
    public void Only_the_project_row_carries_the_budget_code_inputs()
    {
        var sheet = ResolveThongTri(lines: Lines(3));

        Cell(sheet, 15, 1).IsEditable.ShouldBeTrue();
        Cell(sheet, 16, 1).IsEditable.ShouldBeFalse();
        Cell(sheet, 17, 1).IsEditable.ShouldBeFalse();
    }

    [Fact]
    public void Zero_lines_leaves_a_blank_region_without_throwing()
    {
        var sheet = FormSheetResolver.Resolve(ThongTriTemplate.Create(), ThongTriValues(), new FormInputSet(),
            Org, 2026, Array.Empty<ThongTriLine>());

        sheet.Cells.Any(c => c.Row == 15 && c.Col == 1 && c.IsEditable).ShouldBeFalse();
        Cell(sheet, 24, 7).DisplayText.ShouldBe("Cộng");
    }

    // ===== chữ tràn sang ô trống bên phải =====

    [Fact]
    public void Left_text_overflows_into_empty_cells_and_stops_at_the_next_content()
    {
        var sheet = ResolveThongTri();

        Cell(sheet, 10, 1).TextColSpan.ShouldBe(8);   // Tên CTMT… tràn hết tới cột H
        Cell(sheet, 8, 3).TextColSpan.ShouldBe(4);    // "Đơn vị" ô C8 dừng trước G8:H8
    }

    [Fact]
    public void Wrapped_merged_centered_and_numeric_cells_do_not_overflow()
    {
        var sheet = ResolveThongTri();

        Cell(sheet, 4, 1).TextColSpan.ShouldBe(Cell(sheet, 4, 1).ColSpan);   // gộp
        Cell(sheet, 14, 4).TextColSpan.ShouldBe(1);                          // "D" giữa ô
        Cell(sheet, 24, 8).TextColSpan.ShouldBe(1);                          // số
    }

    [Fact]
    public void Row_heights_default_to_15_for_rows_without_an_explicit_height()
    {
        var sheet = ResolveThongTri();

        sheet.RowHeights[13].ShouldBe(15.0, 0.001);                          // hàng 14: Mẫu để mặc định
        sheet.RowCount.ShouldBe(33);
        sheet.ColumnCount.ShouldBe(8);
    }
}
```

- [ ] **Step 2: Chạy test, thấy đỏ**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --nologo -v q --filter FormSheetResolverTests 2>&1 | tail -5`
Expected: lỗi biên dịch (`FormSheetResolver` chưa có).

- [ ] **Step 3: Viết bộ trộn**

```csharp file=src/ConstructionFinance.Application/Forms/FormSheetResolver.cs
using System.Globalization;

namespace ConstructionFinance.Application.Forms;

/// Trộn bản gốc với số đã tính, chữ người dùng đã gõ và tên đơn vị thành một ResolvedSheet.
/// Thuần: không biết gì về Excel, Avalonia hay MigraDoc.
public static class FormSheetResolver
{
    public const string YearToken = "{năm}";

    /// Khoá dành riêng của các hàng mẫu ở vùng lặp: chữ và số tiền của dòng đang vẽ.
    public const string LineText = "Line.Text";
    public const string LineAmount = "Line.Amount";

    public const double DefaultRowHeight = 15;

    private sealed record Context(
        FormTemplate Template, FormValues Computed, FormInputSet Inputs, FormOrgInfo Org, string Year);

    public static ResolvedSheet Resolve(
        FormTemplate template,
        FormValues computed,
        FormInputSet inputs,
        FormOrgInfo org,
        int year,
        IReadOnlyList<ThongTriLine>? lines = null)
    {
        var context = new Context(template, computed, inputs, org, year.ToString(CultureInfo.InvariantCulture));
        var cells = new List<ResolvedCell>();
        var heights = new Dictionary<int, double>();
        var shift = 0;
        var regionEnd = int.MaxValue;

        if (template.Repeat is { } region)
        {
            var regionLines = lines ?? Array.Empty<ThongTriLine>();
            var count = Math.Max(region.MinRows, regionLines.Count);
            shift = count - region.MinRows;
            regionEnd = region.FirstRow + region.MinRows;

            for (var i = 0; i < count; i++)
            {
                var row = region.FirstRow + i;
                var line = i < regionLines.Count ? regionLines[i] : null;
                var prototype = line is null
                    ? region.BlankRow
                    : line.Level switch { 0 => region.ProjectRow, 1 => region.ViTriRow, _ => region.DotRow };

                heights[row] = prototype.Height;
                foreach (var cell in prototype.Cells)
                    cells.Add(ResolveCell(cell with { Row = row }, context, line));
            }
        }

        foreach (var cell in template.Cells)
        {
            var row = cell.Row >= regionEnd ? cell.Row + shift : cell.Row;
            cells.Add(ResolveCell(cell with { Row = row }, context, null));
        }

        foreach (var (row, height) in template.RowHeights)
            heights[row >= regionEnd ? row + shift : row] = height;

        var rowCount = Math.Max(
            cells.Count == 0 ? 0 : cells.Max(c => c.Row + c.RowSpan - 1),
            heights.Count == 0 ? 0 : heights.Keys.Max());
        var rowHeights = Enumerable.Range(1, rowCount)
            .Select(row => heights.TryGetValue(row, out var h) ? h : DefaultRowHeight)
            .ToList();

        var ordered = cells.OrderBy(c => c.Row).ThenBy(c => c.Col).ToList();
        return new ResolvedSheet(
            template.SheetName, template.Orientation, template.ColumnWidths, rowHeights,
            WithOverflow(ordered, template.ColumnWidths.Count));
    }

    private static ResolvedCell ResolveCell(TemplateCell cell, Context context, ThongTriLine? line) =>
        new(cell.Row, cell.Col, cell.RowSpan, cell.ColSpan, cell.ColSpan, cell.Style,
            cell.Segments.Select(segment => ResolveSegment(segment, cell.Style, context, line)).ToList());

    private static ResolvedSegment ResolveSegment(
        TemplateSegment segment, CellStyle style, Context context, ThongTriLine? line)
    {
        switch (segment.Kind)
        {
            case SegmentKind.Fixed:
            {
                var text = Year(segment.Text, context)!;
                return new ResolvedSegment(segment.Kind, null, text, text, null, false, false, null);
            }
            case SegmentKind.Input:
                return ResolveText(segment, context.Inputs.Get(segment.Key!) ?? Year(segment.Default, context), context);
            case SegmentKind.SettingInput:
                return ResolveText(segment, context.Org.Get(segment.Key!), context);
            case SegmentKind.InputNumber:
                return context.Inputs.TryGetNumber(segment.Key!, out var typed)
                    ? Numeric(segment, typed, style, overridden: false)
                    : new ResolvedSegment(segment.Kind, segment.Key, "", "", Year(segment.Hint, context), false, false, null);
            case SegmentKind.Computed:
            {
                var value = Lookup(segment.Key!, context, line);
                return value.Number is { } number
                    ? Numeric(segment, number, style, overridden: false)
                    : Plain(segment, value.Text ?? "", overridden: false, context);
            }
            case SegmentKind.Overridable:
            {
                var value = Lookup(segment.Key!, context, line);
                var stored = context.Inputs.Get(segment.Key!);
                if (value.Number is { } computedNumber)
                    return !string.IsNullOrEmpty(stored) && context.Inputs.TryGetNumber(segment.Key!, out var overriddenNumber)
                        ? Numeric(segment, overriddenNumber, style, overridden: true)
                        : Numeric(segment, computedNumber, style, overridden: false);

                return !string.IsNullOrEmpty(stored)
                    ? Plain(segment, stored, overridden: true, context)
                    : Plain(segment, value.Text ?? "", overridden: false, context);
            }
            default:
                throw new NotSupportedException($"Loại đoạn chưa hỗ trợ: {segment.Kind}");
        }
    }

    /// Ô chữ tự nhập: có chữ (đã gõ hoặc điền sẵn) thì in chữ đó; trống thì in chữ chấm chấm của Mẫu và
    /// cho màn hình một Hint mờ (ghi chú của Mẫu, nếu không có thì chính chữ chấm chấm).
    private static ResolvedSegment ResolveText(TemplateSegment segment, string? value, Context context)
    {
        var hint = Year(segment.Hint, context);
        if (!string.IsNullOrEmpty(value))
            return new ResolvedSegment(segment.Kind, segment.Key, value, value, hint, false, false, null);

        var placeholder = Year(segment.Text, context)!;
        return new ResolvedSegment(
            segment.Kind, segment.Key, placeholder, "", hint ?? (placeholder.Length > 0 ? placeholder : null),
            IsPlaceholder: true, false, null);
    }

    private static ResolvedSegment Plain(TemplateSegment segment, string text, bool overridden, Context context) =>
        new(segment.Kind, segment.Key, text, text, Year(segment.Hint, context), false, overridden, null);

    private static ResolvedSegment Numeric(TemplateSegment segment, decimal number, CellStyle style, bool overridden) =>
        new(segment.Kind, segment.Key, FormNumberFormat.Format(number, style.NumberFormat),
            FormNumberFormat.Group(number), segment.Hint, false, overridden, number);

    private static ComputedValue Lookup(string key, Context context, ThongTriLine? line)
    {
        if (key == LineText) return new ComputedValue(line?.Content ?? "", null);
        if (key == LineAmount) return line is null ? new ComputedValue("", null) : new ComputedValue(null, line.Amount);
        if (context.Computed.TryGet(key, out var value)) return value;
        if (context.Computed.BlankMissing) return new ComputedValue("", null);

        throw new InvalidOperationException(
            $"Biểu mẫu \"{context.Template.SheetName}\" cần giá trị cho khoá tự tính \"{key}\" nhưng không được cấp.");
    }

    private static string? Year(string? text, Context context) => text?.Replace(YearToken, context.Year);

    // ===== chữ tràn sang ô trống bên phải =====

    /// Excel cho chữ canh trái (hoặc mặc định) của ô KHÔNG gộp, KHÔNG xuống dòng, tràn sang các ô trống bên
    /// phải. Tính MỘT lần ở đây để màn hình và bản in cùng đọc.
    private static IReadOnlyList<ResolvedCell> WithOverflow(List<ResolvedCell> cells, int columnCount)
    {
        var occupied = new HashSet<(int Row, int Col)>();
        foreach (var cell in cells)
        {
            var merged = cell.RowSpan > 1 || cell.ColSpan > 1;
            for (var row = cell.Row; row < cell.Row + cell.RowSpan; row++)
            for (var col = cell.Col; col < cell.Col + cell.ColSpan; col++)
                if (merged || cell.DisplayText.Length > 0 || cell.IsEditable)
                    occupied.Add((row, col));
        }

        return cells.Select(cell =>
        {
            if (!CanOverflow(cell)) return cell;
            var span = 1;
            while (cell.Col + span <= columnCount && !occupied.Contains((cell.Row, cell.Col + span))) span++;
            return span > 1 ? cell with { TextColSpan = span } : cell;
        }).ToList();
    }

    private static bool CanOverflow(ResolvedCell cell) =>
        cell.RowSpan == 1 && cell.ColSpan == 1 && !cell.Style.Wrap
        && (cell.Style.Align == CellAlign.General || cell.Style.Align == CellAlign.Left)
        && (cell.DisplayText.Length > 0 || cell.IsEditable)
        && cell.Segments.All(segment => segment.Number is null);
}
```

- [ ] **Step 4: Chạy test, thấy xanh**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --nologo -v q --filter FormSheetResolverTests 2>&1 | tail -15`
Expected: `Passed!`. Nếu test số hàng/`Cộng` lệch một đơn vị, kiểm lại `RegionFirstRow`/`MinRows` trong `ThongTriTemplate.cs` (15 và 9) rồi sửa ĐIỀU KIỆN trong test chứ không nới bộ trộn.

- [ ] **Step 5: Commit**

```bash
git add src/ConstructionFinance.Application/Forms/FormSheetResolver.cs tests/ConstructionFinance.Application.Tests/Forms/FormSheetResolverTests.cs
git commit -m "feat(forms): bộ trộn bản gốc + số đã tính + chữ đã gõ thành tờ biểu mẫu

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 4: `FormDefinition` — nối builder có sẵn vào bộ trộn

**Files:**
- Create: `src/ConstructionFinance.Application/Forms/FormDefinitions.cs`
- Test: `tests/ConstructionFinance.Application.Tests/Forms/FormDefinitionsTests.cs`

**Interfaces:**
- Consumes: Task 2–3, `ThongTriBuilder`, `KsChiBuilder`, `GdnttBuilder`, `GdnttDeclaration` (Domain, chỉ dùng hai trường số), `PrintPageProfiles`.
- Produces:
  - `record FormBuildContext(IReadOnlyList<FormDotRow> Rows, IReadOnlySet<FormDotKey> Selected, FormInputSet Inputs, FormOrgInfo Org, string ProjectName, int Year)`
  - `record FormBuildResult(ResolvedSheet Sheet, string? Error, string? PrintBlockReason = null)` với `bool CanOutput => Error is null`
  - `abstract class FormDefinition { string Id; string Title; string ExportPrefix; string ExportDialogTitle; Fund? FundFilter; PrintPageProfile PrintProfile; FormTemplate Template; FormBuildResult Build(FormBuildContext) }`
  - `static class FormDefinitions { ThongTri; KsChi; Gdntt; All }` — `Id` lần lượt `"thongtri"`, `"kschi"`, `"gdntt"`; `Title` `"Thông tri"`, `"KS chi"`, `"GĐNTT"`.

- [ ] **Step 1: Viết test (đỏ)**

```csharp file=tests/ConstructionFinance.Application.Tests/Forms/FormDefinitionsTests.cs
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Forms;

public class FormDefinitionsTests
{
    private static readonly FormOrgInfo Org = new("Sở Tài chính", "Ban QLDA số 1");

    private static FormDotRow Dot(
        FormDotKey key, string viTri, Fund fund, decimal advance, decimal klht, decimal recovered,
        string content, string package = "Gói A") =>
        new(key, viTri, "CHI PHÍ XÂY DỰNG", package, "Cty XL A", "123456789",
            50_000_000_000m, 2_000_000_000m, new DateOnly(2026, 3, 1), content, "CT01", fund,
            advance, klht, recovered, Disbursed: advance + klht - recovered);

    private static IReadOnlyList<FormDotRow> Rows() => new[]
    {
        Dot(new(0, 0, 0, 0), "Vị trí 1", Fund.NSNN, 10_000_000m, 0m, 0m, "Tạm ứng lần 1"),
        Dot(new(0, 0, 0, 1), "Vị trí 1", Fund.NSNN, 0m, 200_000_000m, 4_000_000m, "Thanh toán KLHT"),
        Dot(new(0, 0, 0, 2), "Vị trí 1", Fund.NgoaiHoi, 30_000_000m, 0m, 0m, "Ngoại hối 1"),
        Dot(new(1, 0, 0, 0), "Vị trí 2", Fund.NgoaiHoi, 5_000_000m, 0m, 0m, "Ngoại hối 2", package: "Gói B"),
    };

    private static FormBuildContext Context(
        IReadOnlySet<FormDotKey> selected, FormInputSet? inputs = null, string project = "Dự án A") =>
        new(Rows(), selected, inputs ?? new FormInputSet(), Org, project, 2026);

    private static IReadOnlySet<FormDotKey> Keys(params (int, int, int, int)[] keys) =>
        keys.Select(k => new FormDotKey(k.Item1, k.Item2, k.Item3, k.Item4)).ToHashSet();

    private static ResolvedCell Cell(ResolvedSheet sheet, int row, int col) =>
        FormSheetResolverTests.Cell(sheet, row, col);

    [Fact]
    public void The_three_definitions_have_stable_ids_titles_and_fund_filters()
    {
        FormDefinitions.All.Select(d => (d.Id, d.Title, d.FundFilter)).ShouldBe(new (string, string, Fund?)[]
        {
            ("thongtri", "Thông tri", Fund.NSNN),
            ("kschi", "KS chi", null),
            ("gdntt", "GĐNTT", Fund.NgoaiHoi),
        });
    }

    // ===== Thông tri =====

    [Fact]
    public void ThongTri_fills_the_project_row_the_lines_and_the_total()
    {
        var result = FormDefinitions.ThongTri.Build(Context(Keys((0, 0, 0, 0), (0, 0, 0, 1))));

        result.Error.ShouldBeNull();
        var sheet = result.Sheet;
        Cell(sheet, 15, 7).DisplayText.ShouldBe("Dự án A");
        Cell(sheet, 15, 8).Segments.Single().Number.ShouldBe(206_000_000m);   // 10tr + (200tr − 4tr)
        Cell(sheet, 16, 7).DisplayText.ShouldBe("Vị trí 1");
        Cell(sheet, 17, 7).DisplayText.ShouldBe("Tạm ứng lần 1");
        Cell(sheet, 24, 8).Segments.Single().Number.ShouldBe(206_000_000m);
        Cell(sheet, 25, 3).DisplayText.ShouldNotBeNullOrWhiteSpace();         // bằng chữ
    }

    [Fact]
    public void ThongTri_with_nothing_selected_still_resolves_the_whole_sheet()
    {
        var result = FormDefinitions.ThongTri.Build(Context(Keys()));

        result.Error.ShouldBeNull();
        Cell(result.Sheet, 15, 7).DisplayText.ShouldBe("Dự án A");
        Cell(result.Sheet, 24, 8).Segments.Single().Number.ShouldBe(0m);
    }

    [Fact]
    public void ThongTri_keeps_typed_text_in_the_cells_the_Mau_leaves_open()
    {
        var inputs = new FormInputSet();
        inputs.Set("SoThongTri", "07/TT");

        var result = FormDefinitions.ThongTri.Build(Context(Keys(), inputs));

        Cell(result.Sheet, 3, 1).DisplayText.ShouldBe("Số      07/TT");
    }

    // ===== KS chi =====

    [Fact]
    public void KsChi_splits_the_amounts_by_fund_and_defaults_the_note_to_the_first_dot_content()
    {
        var result = FormDefinitions.KsChi.Build(
            Context(Keys((0, 0, 0, 0), (0, 0, 0, 2))));

        var sheet = result.Sheet;
        Cell(sheet, 7, 3).Segments.Single().Number.ShouldBe(40_000_000m);     // chấp nhận: tổng
        Cell(sheet, 7, 4).Segments.Single().Number.ShouldBe(10_000_000m);     // NSNN
        Cell(sheet, 7, 5).Segments.Single().Number.ShouldBe(30_000_000m);     // ngoại hối
        Cell(sheet, 17, 3).Segments.Single().Number.ShouldBe(40_000_000m);    // số trả = chấp nhận − thu hồi
        Cell(sheet, 22, 2).DisplayText.ShouldBe("Tạm ứng lần 1");
        Cell(sheet, 22, 2).Segments.Single().IsOverridden.ShouldBeFalse();
    }

    [Fact]
    public void KsChi_note_override_wins_over_the_first_dot()
    {
        var inputs = new FormInputSet();
        inputs.Set("GhiChu", "Ghi chú tự gõ");

        var result = FormDefinitions.KsChi.Build(Context(Keys((0, 0, 0, 0)), inputs));

        Cell(result.Sheet, 22, 2).DisplayText.ShouldBe("Ghi chú tự gõ");
        Cell(result.Sheet, 22, 2).Segments.Single().IsOverridden.ShouldBeTrue();
    }

    [Fact]
    public void KsChi_typed_numbers_do_not_change_the_computed_amounts()
    {
        var inputs = new FormInputSet();
        inputs.Set("ThueGTGT", FormInputSet.ToStored(5_000_000m));

        var sheet = FormDefinitions.KsChi.Build(Context(Keys((0, 0, 0, 0)), inputs)).Sheet;

        Cell(sheet, 16, 3).Segments.Single().Number.ShouldBe(5_000_000m);
        Cell(sheet, 17, 3).Segments.Single().Number.ShouldBe(10_000_000m);
    }

    // ===== GĐNTT =====

    [Fact]
    public void Gdntt_for_one_package_fills_every_computed_cell()
    {
        var result = FormDefinitions.Gdntt.Build(Context(Keys((0, 0, 0, 2))));

        result.Error.ShouldBeNull();
        var sheet = result.Sheet;
        Cell(sheet, 18, 1).DisplayText.ShouldBe("Gói A");
        Cell(sheet, 18, 4).Segments.Single().Number.ShouldBe(52_000_000_000m);   // giá trị HĐ + phụ lục
        Cell(sheet, 18, 8).Segments.Single().Number.ShouldBe(30_000_000m);
        Cell(sheet, 19, 9).Segments.Single().Number.ShouldBe(30_000_000m);
        Cell(sheet, 27, 3).DisplayText.ShouldBe("Cty XL A");
        Cell(sheet, 28, 4).DisplayText.ShouldBe("123456789");
        Cell(sheet, 8, 3).DisplayText.ShouldBe("Dự án A");                      // tên dự án điền sẵn
        Cell(sheet, 8, 3).Segments.Single().IsOverridden.ShouldBeFalse();
    }

    [Fact]
    public void Gdntt_paid_to_contractor_subtracts_the_typed_withholdings()
    {
        var inputs = new FormInputSet();
        inputs.Set("ChuyenTienBaoHanh", FormInputSet.ToStored(1_000_000m));
        inputs.Set("ChuyenTienTamGiu", FormInputSet.ToStored(2_000_000m));

        var sheet = FormDefinitions.Gdntt.Build(Context(Keys((0, 0, 0, 2)), inputs)).Sheet;

        Cell(sheet, 26, 9).Segments.Single().Number.ShouldBe(27_000_000m);
    }

    [Theory]
    [InlineData(false, "chưa chọn gói thầu")]
    [InlineData(true, "đã chọn đợt của nhiều gói thầu")]
    public void Gdntt_blocked_still_resolves_the_sheet(bool twoPackages, string reason)
    {
        var selected = twoPackages ? Keys((0, 0, 0, 2), (1, 0, 0, 0)) : Keys();
        var inputs = new FormInputSet();
        inputs.Set("KinhGui", "Kho bạc Nhà nước");

        var result = FormDefinitions.Gdntt.Build(Context(selected, inputs));

        result.Error.ShouldNotBeNullOrWhiteSpace();
        result.PrintBlockReason.ShouldBe(reason);
        result.CanOutput.ShouldBeFalse();
        Cell(result.Sheet, 6, 1).DisplayText.ShouldBe("Kính gửi: Kho bạc Nhà nước");   // vẫn gõ được, vẫn hiện
        Cell(result.Sheet, 18, 1).DisplayText.ShouldBe("");
        Cell(result.Sheet, 8, 3).DisplayText.ShouldBe("Dự án A");
    }

    // ===== chung =====

    [Fact]
    public void Every_definition_supplies_every_computed_key_of_its_template()
    {
        // Resolve ném nếu thiếu khoá tự tính; chạy với một lựa chọn hợp lệ cho từng biểu mẫu.
        var all = Keys((0, 0, 0, 0), (0, 0, 0, 1));
        FormDefinitions.ThongTri.Build(Context(all)).Error.ShouldBeNull();
        FormDefinitions.KsChi.Build(Context(all)).Error.ShouldBeNull();
        FormDefinitions.Gdntt.Build(Context(Keys((0, 0, 0, 2)))).Error.ShouldBeNull();
    }

    [Fact]
    public void Org_edit_reaches_both_forms_that_show_it()
    {
        var org = new FormOrgInfo("Đơn vị mới", "Ban mới");
        var context = Context(Keys()) with { Org = org };

        Cell(FormDefinitions.ThongTri.Build(context).Sheet, 1, 1).DisplayText.ShouldBe("Đơn vị mới");
        Cell(FormDefinitions.Gdntt.Build(context).Sheet, 2, 1).DisplayText.ShouldBe("Ban mới");
    }
}
```

- [ ] **Step 2: Chạy test, thấy đỏ** (`FormDefinitions` chưa có).

- [ ] **Step 3: Viết các định nghĩa**

```csharp file=src/ConstructionFinance.Application/Forms/FormDefinitions.cs
using ConstructionFinance.Application.Printing;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Forms;

/// Mọi thứ một lần dựng biểu mẫu cần: các đợt đã trải phẳng, đợt nào đang chọn, chữ người dùng đã gõ,
/// tên đơn vị, tên dự án và năm (tham số để test không phụ thuộc đồng hồ).
public sealed record FormBuildContext(
    IReadOnlyList<FormDotRow> Rows,
    IReadOnlySet<FormDotKey> Selected,
    FormInputSet Inputs,
    FormOrgInfo Org,
    string ProjectName,
    int Year);

/// `Sheet` LUÔN có (kể cả khi `Error` khác null — tờ trống để người dùng vẫn gõ được các ô chữ).
/// `Error` khác null nghĩa là chưa xuất/in được; `PrintBlockReason` là cụm viết thường không dấu chấm
/// cho thông báo "In toàn bộ".
public sealed record FormBuildResult(ResolvedSheet Sheet, string? Error, string? PrintBlockReason = null)
{
    public bool CanOutput => Error is null;
}

/// Một biểu mẫu: bản gốc + cách dựng số từ các builder có sẵn. Không có cách tính số mới nào ở đây.
public abstract class FormDefinition
{
    protected FormDefinition(FormTemplate template) => Template = template;

    /// Khoá lưu trong `finance_form_inputs.Form`. ĐỔI là mất chữ đã gõ — đừng đổi.
    public abstract string Id { get; }
    public abstract string Title { get; }
    public abstract string ExportPrefix { get; }
    public abstract string ExportDialogTitle { get; }
    public abstract Fund? FundFilter { get; }
    public abstract PrintPageProfile PrintProfile { get; }

    public FormTemplate Template { get; }

    public abstract FormBuildResult Build(FormBuildContext context);
}

public sealed class ThongTriDefinition : FormDefinition
{
    public ThongTriDefinition() : base(ThongTriTemplate.Create()) { }

    public override string Id => "thongtri";
    public override string Title => "Thông tri";
    public override string ExportPrefix => "ThongTri";
    public override string ExportDialogTitle => "Xuất Thông tri";
    public override Fund? FundFilter => Fund.NSNN;
    public override PrintPageProfile PrintProfile => PrintPageProfiles.ThongTri;

    public override FormBuildResult Build(FormBuildContext context)
    {
        var form = ThongTriBuilder.Build(context.Rows, context.Selected, null, context.Org, context.ProjectName);
        var values = new FormValues()
            .Number("Total", form.Total)
            .Text("TotalInWords", form.TotalInWords);

        return new FormBuildResult(
            FormSheetResolver.Resolve(Template, values, context.Inputs, context.Org, context.Year, form.Lines),
            Error: null);
    }
}

public sealed class KsChiDefinition : FormDefinition
{
    public KsChiDefinition() : base(KsChiTemplate.Create()) { }

    public override string Id => "kschi";
    public override string Title => "KS chi";
    public override string ExportPrefix => "KSChi";
    public override string ExportDialogTitle => "Xuất phần ghi của KS chi";
    public override Fund? FundFilter => null;
    public override PrintPageProfile PrintProfile => PrintPageProfiles.KsChi;

    public override FormBuildResult Build(FormBuildContext context)
    {
        // Số tự nhập (thu hồi các năm, thuế GTGT, số từ chối) không đi vào phép tính: Mẫu để "Số trả đơn vị
        // thụ hưởng" = chấp nhận − thu hồi tạm ứng, nên builder không cần bản khai.
        var form = KsChiBuilder.Build(context.Rows, context.Selected, null);
        var values = new FormValues()
            .Number("Accepted.Total", form.Accepted.Total)
            .Number("Accepted.NSNN", form.Accepted.NSNN)
            .Number("Accepted.NgoaiHoi", form.Accepted.NgoaiHoi)
            .Number("Recovered.Total", form.Recovered.Total)
            .Number("Recovered.NSNN", form.Recovered.NSNN)
            .Number("Recovered.NgoaiHoi", form.Recovered.NgoaiHoi)
            .Number("Paid.Total", form.PaidToBeneficiary.Total)
            .Number("Paid.NSNN", form.PaidToBeneficiary.NSNN)
            .Number("Paid.NgoaiHoi", form.PaidToBeneficiary.NgoaiHoi)
            .Text("PaidInWords", form.PaidInWords)
            .Text("GhiChu", form.GhiChu);

        return new FormBuildResult(
            FormSheetResolver.Resolve(Template, values, context.Inputs, context.Org, context.Year),
            Error: null);
    }
}

public sealed class GdnttDefinition : FormDefinition
{
    public GdnttDefinition() : base(GdnttTemplate.Create()) { }

    public override string Id => "gdntt";
    public override string Title => "GĐNTT";
    public override string ExportPrefix => "GDNTT";
    public override string ExportDialogTitle => "Xuất Giấy đề nghị thanh toán";
    public override Fund? FundFilter => Fund.NgoaiHoi;
    public override PrintPageProfile PrintProfile => PrintPageProfiles.Gdntt;

    public override FormBuildResult Build(FormBuildContext context)
    {
        // Hai ô số tự nhập duy nhất mà builder dùng (Số trả nhà thầu = đề nghị − thu hồi − bảo hành − tạm giữ).
        var declaration = new GdnttDeclaration
        {
            ChuyenTienBaoHanh = context.Inputs.GetNumber("ChuyenTienBaoHanh"),
            ChuyenTienTamGiu = context.Inputs.GetNumber("ChuyenTienTamGiu"),
        };
        var result = GdnttBuilder.Build(context.Rows, context.Selected, declaration, context.Org);

        if (result.Form is null)
        {
            // Tờ trống nhưng vẫn hiện: tên dự án điền sẵn, các ô chữ vẫn gõ được; chỉ khoá xuất và in.
            var blank = new FormValues { BlankMissing = true }.Text("TenDuAnCongTrinh", context.ProjectName);
            var anySelected = context.Rows.Any(row =>
                context.Selected.Contains(row.Key) && row.Fund == Fund.NgoaiHoi);
            return new FormBuildResult(
                FormSheetResolver.Resolve(Template, blank, context.Inputs, context.Org, context.Year),
                result.Error,
                // Đúng với việc người dùng đã làm: chưa tích đợt nào ≠ đã tích đợt của nhiều gói.
                anySelected ? "đã chọn đợt của nhiều gói thầu" : "chưa chọn gói thầu");
        }

        var form = result.Form;
        var values = new FormValues()
            .Text("TenDuAnCongTrinh", context.ProjectName)
            .Number("UnrecoveredAdvanceBefore", form.UnrecoveredAdvanceBefore)
            .Text("PackageName", form.PackageName)
            .Number("ContractTotal", form.ContractTotal)
            .Number("CumulativeBefore", form.CumulativeBefore)
            .Number("Requested", form.RequestedThisPeriod)
            .Text("RequestedInWords", form.RequestedInWords)
            .Number("Recovered", form.RecoveredThisPeriod)
            .Number("PaidToContractor", form.PaidToContractor)
            .Text("ContractorName", form.ContractorName)
            .Text("BankAccount", form.BankAccount);

        return new FormBuildResult(
            FormSheetResolver.Resolve(Template, values, context.Inputs, context.Org, context.Year),
            Error: null);
    }
}

public static class FormDefinitions
{
    public static ThongTriDefinition ThongTri { get; } = new();
    public static KsChiDefinition KsChi { get; } = new();
    public static GdnttDefinition Gdntt { get; } = new();

    /// Thứ tự là thứ tự ba tab.
    public static IReadOnlyList<FormDefinition> All { get; } = new FormDefinition[] { ThongTri, KsChi, Gdntt };
}
```

- [ ] **Step 4: Chạy test, thấy xanh**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --nologo -v q --filter "FormDefinitionsTests|FormSheetResolverTests|ThongTriBuilderTests|KsChiBuilderTests|GdnttBuilderTests" 2>&1 | tail -8`
Expected: `Passed!`; các test builder cũ vẫn xanh (không sửa builder).

- [ ] **Step 5: Commit**

```bash
git add src/ConstructionFinance.Application/Forms/FormDefinitions.cs tests/ConstructionFinance.Application.Tests/Forms/FormDefinitionsTests.cs
git commit -m "feat(forms): FormDefinition nối ba builder có sẵn vào bộ trộn, GĐNTT bị chặn vẫn hiện tờ

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Xuất Excel và bản in `.xlsx` từ tờ đã trộn

**Files:**
- Modify: `src/ConstructionFinance.Application/Forms/FormExcelExporter.cs` (thêm `WriteSheet`, `ApplyPaper`, `ExportAsync`)
- Modify: `src/ConstructionFinance.Application/Services/Interfaces/IFormExcelExporter.cs` (thêm `ExportAsync(ResolvedSheet…)`)
- Modify: `src/ConstructionFinance.Application/Printing/PrintModels.cs` (thêm `FormTemplatePrintSheet`)
- Modify: `src/ConstructionFinance.Application/Printing/PrintWorkbookWriter.cs` (nhánh mới + `resetFont`)
- Test: `tests/ConstructionFinance.Application.Tests/Forms/FormExcelExporterTemplateTests.cs`

**Interfaces:**
- Consumes: Task 1–4.
- Produces: `IFormExcelExporter.ExportAsync(ResolvedSheet sheet, string targetPath, CancellationToken ct = default)`; `FormExcelExporter.WriteSheet(IXLWorksheet, ResolvedSheet)` (internal static); `record FormTemplatePrintSheet(string SheetName, PrintPageProfile Page, ResolvedSheet Sheet) : PrintSheet(SheetName, Page)`. CÁC PHƯƠNG THỨC CŨ (`ExportThongTriAsync`…, `WriteThongTriSheet`…, `ThongTriPrintSheet`…) GIỮ NGUYÊN tới Task 11 (chiến lược bao quanh để mỗi commit xanh).

- [ ] **Step 1: Viết test (đỏ)**

```csharp file=tests/ConstructionFinance.Application.Tests/Forms/FormExcelExporterTemplateTests.cs
using ClosedXML.Excel;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Printing;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Forms;

public class FormExcelExporterTemplateTests
{
    private static readonly FormOrgInfo Org = new("Sở Tài chính", "Ban QLDA số 1");

    private static FormDotRow Dot(FormDotKey key, Fund fund, decimal advance, string content) =>
        new(key, "Vị trí 1", "CHI PHÍ XÂY DỰNG", "Gói A", "Cty XL A", "123456789",
            50_000_000_000m, 0m, new DateOnly(2026, 3, 1), content, "CT01", fund,
            advance, 0m, 0m, advance);

    private static FormBuildContext Context(FormInputSet? inputs = null) =>
        new(new[]
            {
                Dot(new(0, 0, 0, 0), Fund.NSNN, 10_000_000m, "Tạm ứng lần 1"),
                Dot(new(0, 0, 0, 1), Fund.NgoaiHoi, 30_000_000m, "Ngoại hối"),
            },
            new[] { new FormDotKey(0, 0, 0, 0), new FormDotKey(0, 0, 0, 1) }.ToHashSet(),
            inputs ?? new FormInputSet(), Org, "Dự án A", 2026);

    private static async Task<XLWorkbook> Export(ResolvedSheet sheet)
    {
        var path = Path.Combine(Path.GetTempPath(), $"cf-form-{Guid.NewGuid():N}.xlsx");
        try
        {
            await new FormExcelExporter().ExportAsync(sheet, path);
            using var stream = new MemoryStream(File.ReadAllBytes(path));
            return new XLWorkbook(stream);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Export_writes_widths_heights_merges_and_page_setup_of_the_template()
    {
        var sheet = FormDefinitions.ThongTri.Build(Context()).Sheet;

        using var wb = await Export(sheet);
        var ws = wb.Worksheet("Thông tri");

        ws.Column(7).Width.ShouldBe(35.0, 0.01);
        ws.Row(17).Height.ShouldBe(47.25, 0.01);
        ws.MergedRanges.Select(r => r.RangeAddress.ToStringRelative()).ShouldContain("A4:H4");
        ws.PageSetup.PageOrientation.ShouldBe(XLPageOrientation.Portrait);
        ws.PageSetup.PaperSize.ShouldBe(XLPaperSize.A4Paper);
    }

    [Fact]
    public async Task Export_writes_per_cell_fonts_not_one_size_for_all()
    {
        var sheet = FormDefinitions.ThongTri.Build(Context()).Sheet;

        using var wb = await Export(sheet);
        var ws = wb.Worksheet("Thông tri");

        ws.Cell(4, 1).Style.Font.FontSize.ShouldBe(14);          // THÔNG TRI
        ws.Cell(4, 1).Style.Font.Bold.ShouldBeTrue();
        ws.Cell(12, 1).Style.Font.FontSize.ShouldBe(10);         // Loại ngân sách
        ws.Cell(1, 7).Style.Font.FontSize.ShouldBe(11);          // Mẫu số CQ06-HD
        ws.Cell(4, 1).Style.Font.FontName.ShouldBe("Times New Roman");
    }

    [Fact]
    public async Task Export_writes_computed_money_as_numbers_with_the_template_format()
    {
        var sheet = FormDefinitions.ThongTri.Build(Context()).Sheet;

        using var wb = await Export(sheet);
        var ws = wb.Worksheet("Thông tri");

        ws.Cell(24, 8).GetDouble().ShouldBe(10_000_000d);
        ws.Cell(24, 8).DataType.ShouldBe(XLDataType.Number);
        ws.Cell(24, 8).Style.NumberFormat.Format.ShouldBe("#,##0_);(#,##0)");
        ws.Cell(15, 8).GetDouble().ShouldBe(10_000_000d);
    }

    [Fact]
    public async Task Export_writes_typed_text_and_the_dotted_placeholder_of_the_Mau_for_empty_inputs()
    {
        var inputs = new FormInputSet();
        inputs.Set("SoThongTri", "07/TT");
        var sheet = FormDefinitions.ThongTri.Build(Context(inputs)).Sheet;

        using var wb = await Export(sheet);
        var ws = wb.Worksheet("Thông tri");

        ws.Cell(3, 1).GetString().ShouldBe("Số      07/TT");
        ws.Cell(10, 1).GetString().ShouldStartWith("Tên CTMT, DA.....");   // còn trống → chữ chấm chấm của Mẫu
    }

    [Fact]
    public async Task Export_leaves_a_blank_number_input_empty()
    {
        var sheet = FormDefinitions.KsChi.Build(Context()).Sheet;

        using var wb = await Export(sheet);
        var ws = wb.Worksheet("KS chi");

        ws.Cell(14, 3).IsEmpty().ShouldBeTrue();
    }

    [Fact]
    public async Task Export_draws_thin_and_hair_borders_on_the_right_edges()
    {
        var sheet = FormDefinitions.KsChi.Build(Context()).Sheet;

        using var wb = await Export(sheet);
        var ws = wb.Worksheet("KS chi");

        ws.Cell(6, 3).Style.Border.LeftBorder.ShouldBe(XLBorderStyleValues.Thin);
        ws.Cell(8, 3).Style.Border.TopBorder.ShouldBe(XLBorderStyleValues.Hair);
        ws.Cell(8, 3).Style.Border.LeftBorder.ShouldBe(XLBorderStyleValues.Thin);
    }

    [Fact]
    public async Task Text_starting_with_equals_stays_text()
    {
        var inputs = new FormInputSet();
        inputs.Set("SoThongTri", "=1+1");
        var sheet = FormDefinitions.ThongTri.Build(Context(inputs)).Sheet;

        using var wb = await Export(sheet);
        var cell = wb.Worksheet("Thông tri").Cell(3, 1);

        cell.HasFormula.ShouldBeFalse();
        cell.GetString().ShouldBe("Số      =1+1");   // chữ đứng sau "Số      " nên vốn không bắt đầu bằng "="
        // Ô chỉ có chữ người dùng gõ (không nhãn đứng trước): phải vẫn là chữ.
        var org = new FormOrgInfo("=SUM(A1)", "Ban");
        var withOrg = FormDefinitions.ThongTri.Build(Context() with { Org = org }).Sheet;
        using var wb2 = await Export(withOrg);
        var first = wb2.Worksheet("Thông tri").Cell(1, 1);
        first.HasFormula.ShouldBeFalse();
        first.GetString().ShouldBe("=SUM(A1)");
    }

    [Fact]
    public async Task Gdntt_export_has_the_two_extra_merges_and_money_suffix()
    {
        var context = Context() with { Selected = new[] { new FormDotKey(0, 0, 0, 1) }.ToHashSet() };
        var sheet = FormDefinitions.Gdntt.Build(context).Sheet;

        using var wb = await Export(sheet);
        var ws = wb.Worksheet("GĐNTT");

        var merges = ws.MergedRanges.Select(r => r.RangeAddress.ToStringRelative()).ToList();
        merges.ShouldContain("H13:I13");
        merges.ShouldContain("H25:I25");
        ws.Cell(26, 9).Style.NumberFormat.Format.ShouldBe("#,##0\"đ\"");
        ws.Column(8).Width.ShouldBe(0.1429, 0.01);
    }

    [Fact]
    public void Print_workbook_keeps_each_cell_font_size_for_a_template_sheet()
    {
        var sheet = FormDefinitions.ThongTri.Build(Context()).Sheet;
        var printSheet = new FormTemplatePrintSheet("Thông tri", PrintPageProfiles.ThongTri, sheet);

        using var stream = new MemoryStream();
        new PrintWorkbookWriter().Write(new PrintSheet[] { printSheet }, stream, new PrintFooterInfo("Dự án A", null));
        stream.Position = 0;
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet("Thông tri");

        ws.Cell(4, 1).Style.Font.FontSize.ShouldBe(14);
        ws.Cell(12, 1).Style.Font.FontSize.ShouldBe(10);
        ws.PageSetup.PagesWide.ShouldBe(1);      // hồ sơ căn trang vẫn áp
        ws.PageSetup.Footer.Left.GetText(XLHFOccurrence.OddPages).ShouldContain("Dự án A");
    }
}
```

- [ ] **Step 2: Chạy test, thấy đỏ** (chưa có `ExportAsync(ResolvedSheet…)`, `FormTemplatePrintSheet`).

- [ ] **Step 3: Thêm `FormTemplatePrintSheet`** vào cuối `PrintModels.cs`:

```csharp
/// Sheet biểu mẫu dựng từ bản gốc: bộ ghi .xlsx, bộ dựng MigraDoc và màn hình cùng đọc MỘT ResolvedSheet,
/// nên thêm biểu mẫu mới không cần thêm record nào ở đây.
public sealed record FormTemplatePrintSheet(
    string SheetName,
    PrintPageProfile Page,
    ResolvedSheet Sheet) : PrintSheet(SheetName, Page);
```
(thêm `using ConstructionFinance.Application.Forms;` nếu chưa có — đã có sẵn ở đầu tệp.)

- [ ] **Step 4: Thêm vào `IFormExcelExporter`**

```csharp
    /// Xuất tờ đã trộn của MỘT biểu mẫu (bất kỳ biểu mẫu nào) ra .xlsx đúng bố cục Mẫu.
    Task ExportAsync(ResolvedSheet sheet, string targetPath, CancellationToken ct = default);
```
(đầu tệp thêm không gì — `using ConstructionFinance.Application.Forms;` đã có.)

- [ ] **Step 5: Sửa `FormExcelExporter.cs`**

(a) Tách `ApplyPageDefaults`: giữ tên cũ cho đường cũ, thêm `ApplyPaper`:

```csharp
    /// Khổ A4, hướng giấy, lề trái/phải — KHÔNG đụng tới phông. Bộ vẽ từ bản gốc dùng hàm này vì cỡ chữ
    /// của nó đặt từng ô; `ApplyPageDefaults` đặt phông cả sheet và sẽ nghiền mọi cỡ chữ riêng.
    internal static void ApplyPaper(IXLWorksheet ws, XLPageOrientation orientation)
    {
        ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
        ws.PageSetup.PageOrientation = orientation;
        ws.PageSetup.Margins.Left = Printing.ExcelFitScale.SideMarginInches;
        ws.PageSetup.Margins.Right = Printing.ExcelFitScale.SideMarginInches;
    }

    internal static void ApplyPageDefaults(IXLWorksheet ws, XLPageOrientation orientation)
    {
        ws.Style.Font.FontName = FontName;
        ws.Style.Font.FontSize = FontSize;
        ApplyPaper(ws, orientation);
    }
```
(thay thân `ApplyPageDefaults` hiện có bằng hai hàm trên; giữ nguyên chú thích về `ExcelFitScale.SideMarginInches`.)

(b) Thêm tệp mới `src/ConstructionFinance.Application/Forms/FormExcelExporter.Sheet.cs`:

```csharp file=src/ConstructionFinance.Application/Forms/FormExcelExporter.Sheet.cs
using ClosedXML.Excel;
using ConstructionFinance.Application.Printing;

namespace ConstructionFinance.Application.Forms;

public partial class FormExcelExporter
{
    public Task ExportAsync(ResolvedSheet sheet, string targetPath, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            using var wb = new XLWorkbook();
            WriteSheet(wb.AddWorksheet(sheet.SheetName), sheet);
            wb.SaveAs(targetPath);
        }, ct);

    /// Vẽ một tờ đã trộn lên worksheet CÓ SẴN: bề rộng cột, chiều cao hàng, ô gộp, phông/cỡ chữ/đậm/nghiêng/
    /// gạch chân, canh lề, viền từng cạnh, định dạng số. Số tiền ghi là SỐ thật (Excel cộng được).
    internal static void WriteSheet(IXLWorksheet ws, ResolvedSheet sheet)
    {
        ApplyPaper(ws, sheet.Orientation == PrintOrientation.Landscape
            ? XLPageOrientation.Landscape : XLPageOrientation.Portrait);
        ws.Style.Font.FontName = FontName;
        ws.Style.Font.FontSize = FontSize;

        for (var col = 0; col < sheet.ColumnWidths.Count; col++)
            ws.Column(col + 1).Width = sheet.ColumnWidths[col];
        for (var row = 0; row < sheet.RowHeights.Count; row++)
            ws.Row(row + 1).Height = sheet.RowHeights[row];

        foreach (var cell in sheet.Cells)
        {
            var range = ws.Range(cell.Row, cell.Col, cell.Row + cell.RowSpan - 1, cell.Col + cell.ColSpan - 1);
            if (cell.RowSpan > 1 || cell.ColSpan > 1) range.Merge();

            ApplyStyle(range, cell.Style);
            WriteValue(ws.Cell(cell.Row, cell.Col), cell);
        }
    }

    private static void ApplyStyle(IXLRange range, CellStyle style)
    {
        var font = range.Style.Font;
        font.FontName = FontName;
        font.FontSize = style.FontSize;
        font.Bold = style.Bold;
        font.Italic = style.Italic;
        font.Underline = style.Underline ? XLFontUnderlineValues.Single : XLFontUnderlineValues.None;

        range.Style.Alignment.Horizontal = style.Align switch
        {
            CellAlign.Left => XLAlignmentHorizontalValues.Left,
            CellAlign.Center => XLAlignmentHorizontalValues.Center,
            CellAlign.Right => XLAlignmentHorizontalValues.Right,
            CellAlign.Justify => XLAlignmentHorizontalValues.Justify,
            _ => XLAlignmentHorizontalValues.General,
        };
        range.Style.Alignment.Vertical = style.VAlign switch
        {
            CellVAlign.Center => XLAlignmentVerticalValues.Center,
            CellVAlign.Top => XLAlignmentVerticalValues.Top,
            _ => XLAlignmentVerticalValues.Bottom,
        };
        range.Style.Alignment.WrapText = style.Wrap;

        // Viền: chỉ cạnh NGOÀI của vùng (ô gộp cũng là một vùng). Đặt trên cả vùng sẽ kẻ mọi cạnh trong.
        var borders = style.Borders;
        if (borders.Left != BorderLine.None) range.FirstColumn().Style.Border.LeftBorder = ToExcel(borders.Left);
        if (borders.Right != BorderLine.None) range.LastColumn().Style.Border.RightBorder = ToExcel(borders.Right);
        if (borders.Top != BorderLine.None) range.FirstRow().Style.Border.TopBorder = ToExcel(borders.Top);
        if (borders.Bottom != BorderLine.None) range.LastRow().Style.Border.BottomBorder = ToExcel(borders.Bottom);
    }

    private static XLBorderStyleValues ToExcel(BorderLine line) =>
        line == BorderLine.Hair ? XLBorderStyleValues.Hair : XLBorderStyleValues.Thin;

    private static void WriteValue(IXLCell target, ResolvedCell cell)
    {
        // Một đoạn số duy nhất → ô số thật (cộng được, mang định dạng của Mẫu).
        if (cell.Segments.Count == 1 && cell.Segments[0].Number is { } number)
        {
            target.Value = number;
            target.Style.NumberFormat.Format = cell.Style.NumberFormat ?? FormNumberFormat.Default;
            return;
        }

        var text = cell.DisplayText;
        if (text.Length == 0) return;

        // Chữ người dùng gõ có thể bắt đầu bằng "=" hay "'": gán thẳng một XLCellValue chữ (không qua
        // trình phân tích công thức) và đặt định dạng Text để Excel cũng không coi là công thức khi mở lại.
        target.Style.NumberFormat.Format = "@";
        target.Value = XLCellValue.FromObject(text);
    }
}
```
Nếu `SetValue(text)` của ClosedXML 0.102 vẫn biến `=…` thành công thức ở test `Text_starting_with_equals_stays_text`, đổi thành `target.Value = XLCellValue.FromObject(text)` hoặc ghi qua `target.SetValue(text)` SAU khi đặt `DataType = XLDataType.Text`; test là chốt chặn.

(c) Trong `PrintWorkbookWriter.cs`:

- `ApplyPage(IXLWorksheet ws, PrintPageProfile page, PrintFooterInfo footer, bool resetFont = true)`: thay dòng `FormExcelExporter.ApplyPageDefaults(ws, ToExcel(page.Orientation));` bằng
```csharp
        if (resetFont) FormExcelExporter.ApplyPageDefaults(ws, ToExcel(page.Orientation));
        else FormExcelExporter.ApplyPaper(ws, ToExcel(page.Orientation));
```
- Trong `Write`, thêm nhánh vào `switch (sheet)` TRƯỚC `case PrintFormSheet`:
```csharp
                case FormTemplatePrintSheet template:
                    FormExcelExporter.WriteSheet(ws, template.Sheet);
                    ApplyPage(ws, template.Page, footer, resetFont: false);
                    break;
```

- [ ] **Step 6: Chạy test**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --nologo -v q 2>&1 | grep -E "Passed!|Failed!|error" | head`
Expected: `Passed!` toàn bộ (mọi test cũ về `FormExcelExporter`/`PrintWorkbookWriter` vẫn xanh vì đường cũ không đổi).

- [ ] **Step 7: Commit**

```bash
git add src/ConstructionFinance.Application tests/ConstructionFinance.Application.Tests/Forms/FormExcelExporterTemplateTests.cs
git commit -m "feat(forms): xuất Excel và bản in .xlsx từ tờ đã trộn, giữ cỡ chữ từng ô

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Bản in MigraDoc từ tờ đã trộn

**Files:**
- Create: `src/ConstructionFinance.Application/Printing/MigraDocFormRenderer.cs`
- Modify: `src/ConstructionFinance.Application/Printing/ExcelFitScale.cs` (thêm `UsableHeightMm`)
- Modify: `src/ConstructionFinance.Application/Printing/MigraDocBuilder.cs` (nhánh `FormTemplatePrintSheet`, `FontScaleOf`)
- Test: `tests/ConstructionFinance.Application.Tests/Printing/MigraDocFormRendererTests.cs`

**Interfaces:**
- Consumes: Task 1–5, `ExcelFitScale`, `PrintPageProfile`, `PdfProbe`, `SheetTextMap` (test).
- Produces: `MigraDocFormRenderer.Write(Section, ResolvedSheet, PrintPageProfile) : Table` và `ScaleOf(ResolvedSheet, PrintPageProfile) : double` (internal static); `ExcelFitScale.UsableHeightMm(PrintOrientation)`.

- [ ] **Step 1: Viết test (đỏ)**

```csharp file=tests/ConstructionFinance.Application.Tests/Printing/MigraDocFormRendererTests.cs
using ClosedXML.Excel;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Printing;
using ConstructionFinance.Domain.Finance;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Printing;

public class MigraDocFormRendererTests
{
    private static readonly FormOrgInfo Org = new("Sở Tài chính", "Ban QLDA số 1");
    private static readonly PrintFooterInfo Footer = new("Dự án A", null);

    /// Hồ sơ như sẽ dùng sau Task 11: ba biểu mẫu dọc; KS chi và GĐNTT ép vừa một trang cả chiều dọc.
    private static readonly PrintPageProfile Thong = new(PrintOrientation.Portrait, 1, 0, 0, 0);
    private static readonly PrintPageProfile OnePage = new(PrintOrientation.Portrait, 1, 1, 0, 0);

    private static FormDotRow Dot(FormDotKey key, Fund fund, decimal advance, string content, string viTri = "Vị trí 1") =>
        new(key, viTri, "CHI PHÍ XÂY DỰNG", "Gói A", "Công ty cổ phần xây dựng và thương mại Hải Phòng",
            "0123456789012", 50_000_000_000m, 2_000_000_000m, new DateOnly(2026, 3, 1), content, "CT01", fund,
            advance, 0m, 0m, advance);

    private static FormBuildContext Context(int dots = 3, FormInputSet? inputs = null) =>
        new(Enumerable.Range(0, dots)
                .Select(i => Dot(new(i % 2, 0, 0, i / 2), i % 2 == 0 ? Fund.NSNN : Fund.NgoaiHoi,
                    10_000_000m * (i + 1), $"Nội dung đợt số {i + 1} khá dài để thử xuống dòng trong ô",
                    $"Vị trí {i % 2 + 1}"))
                .ToList(),
            Enumerable.Range(0, dots).Select(i => new FormDotKey(i % 2, 0, 0, i / 2)).ToHashSet(),
            inputs ?? new FormInputSet(), Org, "Dự án A", 2026);

    private static Document Build(ResolvedSheet sheet, PrintPageProfile profile) =>
        new MigraDocBuilder().Build(
            new PrintSheet[] { new FormTemplatePrintSheet(sheet.SheetName, profile, sheet) }, Footer);

    private static Table TableOf(Document document) =>
        document.Sections[0]!.Elements.OfType<Table>().First();

    // ===== một trang =====

    [Fact]
    public void ThongTri_fits_one_portrait_page_at_the_Mau_widths()
    {
        var sheet = FormDefinitions.ThongTri.Build(Context()).Sheet;

        var probe = PdfProbe.Render(Build(sheet, Thong));

        probe.PageCount.ShouldBe(1);
        probe.Pages[0].IsLandscape.ShouldBeFalse();
        probe.Pages[0].WidthMm.ShouldBe(210.0, 0.5);
    }

    [Fact]
    public void KsChi_fits_one_portrait_page()
    {
        var sheet = FormDefinitions.KsChi.Build(Context()).Sheet;

        PdfProbe.Render(Build(sheet, OnePage)).PageCount.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Gdntt_fits_one_portrait_page_even_with_long_values(bool longValues)
    {
        var inputs = new FormInputSet();
        if (longValues)
        {
            inputs.Set("TenDuAnCongTrinh", new string('A', 400));
            inputs.Set("TaiNganHang", new string('B', 300));
            inputs.Set("CanCuHopDong", new string('C', 300));
        }
        var context = Context(inputs: inputs) with
        {
            Selected = new[] { new FormDotKey(1, 0, 0, 0) }.ToHashSet()
        };
        var sheet = FormDefinitions.Gdntt.Build(context).Sheet;

        var probe = PdfProbe.Render(Build(sheet, OnePage));

        probe.PageCount.ShouldBe(1);
        probe.Pages[0].IsLandscape.ShouldBeFalse();
    }

    [Fact]
    public void Thong_tri_nhieu_dot_chay_sang_trang_khong_ne()
    {
        var sheet = FormDefinitions.ThongTri.Build(Context(dots: 60)).Sheet;

        var probe = PdfProbe.Render(Build(sheet, Thong));

        probe.PageCount.ShouldBeGreaterThan(1);
    }

    // ===== tỉ lệ co =====

    [Fact]
    public void Scale_is_one_for_widths_and_only_the_one_page_profile_shrinks_the_tall_gdntt()
    {
        // Bề ngang cả ba vừa A4 dọc nên không co theo bề ngang.
        MigraDocFormRenderer.ScaleOf(FormDefinitions.ThongTri.Build(Context()).Sheet, Thong).ShouldBe(1.0, 0.0001);
        MigraDocFormRenderer.ScaleOf(FormDefinitions.KsChi.Build(Context()).Sheet, OnePage).ShouldBeInRange(0.9, 1.0);   // chỉ co nhẹ theo biên an toàn chiều dọc
        var gdntt = FormDefinitions.Gdntt.Build(Context()).Sheet;
        MigraDocFormRenderer.ScaleOf(gdntt, Thong).ShouldBe(1.0, 0.0001);
        // GĐNTT cao 798pt > 276,7mm khả dụng: ép một trang thì co nhẹ (đã đo: ~0,9 là đủ để vừa kể cả chữ dài).
        MigraDocFormRenderer.ScaleOf(gdntt, OnePage).ShouldBeInRange(0.85, 0.99);
    }

    [Fact]
    public void One_page_profile_shrinks_a_sheet_that_is_too_tall()
    {
        var sheet = FormDefinitions.ThongTri.Build(Context(dots: 40)).Sheet;

        MigraDocFormRenderer.ScaleOf(sheet, Thong).ShouldBe(1.0, 0.0001);                // nhiều trang được
        MigraDocFormRenderer.ScaleOf(sheet, OnePage).ShouldBeLessThan(1.0);              // ép một trang thì co
        PdfProbe.Render(Build(sheet, OnePage)).PageCount.ShouldBe(1);
    }

    [Fact]
    public void Footer_font_equals_the_body_font_scale()
    {
        var sheet = FormDefinitions.ThongTri.Build(Context(dots: 40)).Sheet;
        var document = Build(sheet, OnePage);

        var footerPoints = document.Sections[0]!.Footers.Primary.Elements
            .OfType<Paragraph>().First().Format.Font.Size.Point;

        footerPoints.ShouldBe(TableOf(document).Format.Font.Size.Point, 0.01);
    }

    // ===== nội dung =====

    [Theory]
    [InlineData("thongtri")]
    [InlineData("kschi")]
    [InlineData("gdntt")]
    public void Printed_text_matches_the_excel_text_cell_by_cell(string id)
    {
        var definition = FormDefinitions.All.Single(d => d.Id == id);
        var context = Context() with
        {
            Selected = id == "gdntt" ? new[] { new FormDotKey(1, 0, 0, 0) }.ToHashSet() : Context().Selected
        };
        var sheet = definition.Build(context).Sheet;

        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet(sheet.SheetName);
        FormExcelExporter.WriteSheet(ws, sheet);
        var table = TableOf(Build(sheet, id == "thongtri" ? Thong : OnePage));

        SheetTextMap.AssertSameText(sheet.SheetName, ws, table);
    }

    [Fact]
    public void Overflowing_text_cells_merge_to_the_right_but_keep_their_text_at_the_anchor()
    {
        var sheet = FormDefinitions.ThongTri.Build(Context()).Sheet;   // A10 "Tên CTMT…" tràn qua 8 cột
        var table = TableOf(Build(sheet, Thong));

        table.Rows[9]!.Cells[0]!.MergeRight.ShouldBe(7);
    }

    [Fact]
    public void Row_heights_are_at_least_the_template_heights()
    {
        var sheet = FormDefinitions.ThongTri.Build(Context()).Sheet;
        var table = TableOf(Build(sheet, Thong));

        table.Rows[16]!.Height.Point.ShouldBe(47.25, 0.01);                // hàng 17: đợt
        table.Rows[16]!.HeightRule.ShouldBe(RowHeightRule.AtLeast);
    }
}
```

- [ ] **Step 2: Chạy test, thấy đỏ.**

- [ ] **Step 3: Thêm vào `ExcelFitScale.cs`**

```csharp
    /// Bề cao giấy còn dùng được: khổ A4 theo hướng giấy, trừ lề trên và dưới của bản giấy (MigraDocBuilder).
    public static double UsableHeightMm(PrintOrientation orientation)
    {
        var pageHeight = orientation == PrintOrientation.Landscape ? A4ShortSideMm : A4LongSideMm;
        return pageHeight - 2 * MigraDocBuilder.VerticalMarginInches * MillimetersPerInch;
    }
```

- [ ] **Step 4: Viết bộ vẽ**

```csharp file=src/ConstructionFinance.Application/Printing/MigraDocFormRenderer.cs
using ConstructionFinance.Application.Forms;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace ConstructionFinance.Application.Printing;

/// Vẽ một tờ biểu mẫu đã trộn ra MigraDoc. Chỉ đọc ResolvedSheet — không biết gì về Mẫu hay builder.
/// Thay MigraDocFormSheets + FormGrid (ba bản chép tay) bằng MỘT bộ vẽ chung.
internal static class MigraDocFormRenderer
{
    private const double ThinPoints = 0.5;
    private const double HairPoints = 0.25;
    private const double CellPaddingPoints = 1.5;
    private const double PointsPerMillimeter = 72.0 / 25.4;

    /// Tỉ lệ co chung cho bề rộng cột, cỡ chữ và chiều cao hàng. Luôn co theo bề ngang giấy (như Excel
    /// FitToPages(1 ngang)); hồ sơ ép MỘT trang theo chiều dọc (`FitToPagesTall == 1`) thì co thêm cho vừa chiều cao.
    /// Không bao giờ phóng to (trần 1,0 — 13pt là cỡ của biểu mẫu).
    public static double ScaleOf(ResolvedSheet sheet, PrintPageProfile page)
    {
        var scale = ExcelFitScale.ScaleFor(sheet.ColumnWidths, page.Orientation);
        if (page.FitToPagesTall == 1)
        {
            var contentMm = sheet.RowHeights.Sum() / PointsPerMillimeter;
            if (contentMm > 0)
                scale = Math.Min(scale, ExcelFitScale.UsableHeightMm(page.Orientation) * VerticalSafety / contentMm);
        }
        return scale;
    }

    /// Chừa một chút: MigraDoc cộng thêm viền và làm tròn từng hàng, nên vừa khít là tràn sang trang hai.
    private const double VerticalSafety = 0.92;

    public static Table Write(Section section, ResolvedSheet sheet, PrintPageProfile page)
    {
        var scale = ScaleOf(sheet, page);

        var table = section.AddTable();
        table.Format.Font.Size = Unit.FromPoint(Forms.FormExcelExporter.FontSize * scale);
        table.LeftPadding = Unit.FromPoint(CellPaddingPoints * scale);
        table.RightPadding = Unit.FromPoint(CellPaddingPoints * scale);
        table.TopPadding = Unit.FromPoint(0);
        table.BottomPadding = Unit.FromPoint(0);

        foreach (var width in sheet.ColumnWidths)
            table.AddColumn(Unit.FromMillimeter(ExcelFitScale.ContentWidthMm(new[] { width }) * scale));

        foreach (var height in sheet.RowHeights)
        {
            var row = table.AddRow();
            // AtLeast: Excel cắt chữ ở đáy ô, tờ in thì để hàng nở ra — mất chữ trên giấy ký là lỗi nặng hơn tờ dài thêm vài mm.
            row.Height = Unit.FromPoint(height * scale);
            row.HeightRule = RowHeightRule.AtLeast;
        }

        var byAnchor = sheet.Cells.ToDictionary(cell => (cell.Row, cell.Col));
        var covered = new HashSet<(int Row, int Col)>();
        foreach (var cell in sheet.Cells)
        {
            var span = SpanOf(cell);
            for (var col = cell.Col; col < cell.Col + span; col++)
            for (var row = cell.Row; row < cell.Row + cell.RowSpan; row++)
                if ((row, col) != (cell.Row, cell.Col)) covered.Add((row, col));
        }

        foreach (var cell in sheet.Cells)
        {
            // Ô nằm dưới vùng gộp/tràn của ô khác: MigraDoc bỏ qua nó khi dàn trang, đừng đụng vào.
            if (covered.Contains((cell.Row, cell.Col))) continue;
            WriteCell(table, cell, byAnchor, scale);
        }

        return table;
    }

    /// Số cột ô thực sự chiếm trên giấy: vùng gộp của Mẫu, hoặc — với ô chữ tràn — cả dải chữ tràn sang.
    /// Excel cho chữ tràn sang ô trống bên phải; MigraDoc thì xuống dòng trong lòng ô, nên phải gộp ô
    /// (đường kẻ DỌC nằm giữa dải bị mất — chấp nhận, đỡ hơn đường kẻ cắt ngang chữ).
    private static int SpanOf(ResolvedCell cell) =>
        cell.RowSpan == 1 && cell.ColSpan == 1 && cell.TextColSpan > 1 && cell.DisplayText.Length > 0
            ? cell.TextColSpan
            : cell.ColSpan;

    private static void WriteCell(
        Table table, ResolvedCell cell, Dictionary<(int Row, int Col), ResolvedCell> byAnchor, double scale)
    {
        var target = table.Rows[cell.Row - 1]!.Cells[cell.Col - 1]!;
        var span = SpanOf(cell);
        if (span > 1) target.MergeRight = span - 1;
        if (cell.RowSpan > 1) target.MergeDown = cell.RowSpan - 1;

        // Cạnh phải của dải tràn là cạnh phải của ô cuối dải, không phải của ô đầu.
        var right = cell.Style.Borders.Right;
        if (span > cell.ColSpan && byAnchor.TryGetValue((cell.Row, cell.Col + span - 1), out var last))
            right = last.Style.Borders.Right;
        ApplyBorders(target, cell.Style.Borders with { Right = right });

        var style = cell.Style;
        target.Format.Font.Size = Unit.FromPoint(style.FontSize * scale);
        if (style.Bold) target.Format.Font.Bold = true;
        if (style.Italic) target.Format.Font.Italic = true;
        if (style.Underline) target.Format.Font.Underline = Underline.Single;
        target.VerticalAlignment = style.VAlign switch
        {
            CellVAlign.Center => VerticalAlignment.Center,
            CellVAlign.Top => VerticalAlignment.Top,
            _ => VerticalAlignment.Bottom,
        };

        var text = cell.DisplayText;
        if (text.Length == 0) return;

        var paragraph = target.AddParagraph(text);
        var numeric = cell.Segments.Any(segment => segment.Number is not null);
        paragraph.Format.Alignment = style.Align switch
        {
            CellAlign.Center => ParagraphAlignment.Center,
            CellAlign.Right => ParagraphAlignment.Right,
            CellAlign.Justify => ParagraphAlignment.Justify,
            CellAlign.Left => ParagraphAlignment.Left,
            _ => numeric ? ParagraphAlignment.Right : ParagraphAlignment.Left,   // Excel canh phải ô số theo mặc định
        };
    }

    private static void ApplyBorders(Cell target, CellBorders borders)
    {
        Set(target.Borders.Left, borders.Left);
        Set(target.Borders.Right, borders.Right);
        Set(target.Borders.Top, borders.Top);
        Set(target.Borders.Bottom, borders.Bottom);

        static void Set(Border border, BorderLine line)
        {
            if (line == BorderLine.None) return;
            border.Width = Unit.FromPoint(line == BorderLine.Hair ? HairPoints : ThinPoints);
        }
    }
}
```

- [ ] **Step 4b: Cho `SheetTextMap` hiểu hậu tố "đ"**

Ô tiền của GĐNTT có định dạng `#,##0"đ"`: .xlsx đọc ra `10,000,000đ` (dấu phẩy theo văn hoá máy), MigraDoc in `10.000.000đ`. Trong `tests/ConstructionFinance.Application.Tests/Printing/SheetTextMap.cs`, hàm `Normalize`, đổi biểu thức nhận diện ô số từ
`@"^-?(?=.*\d)[\d.,]+$"` thành `@"^-?(?=.*\d)[\d.,]+( ?đ)?$"` (phần còn lại của hàm — bỏ `.` và `,` — giữ nguyên) và thêm một dòng vào chú thích của hàm nói rằng hậu tố " đ"/"đ" của định dạng tiền Mẫu cũng được coi là ô số. Chạy `SheetTextMapTests` để chắc test cũ của chính nó vẫn xanh.

- [ ] **Step 5: Sửa `MigraDocBuilder.cs`**

Trong `switch (sheet)` của `Build`, thêm trước `default:`:
```csharp
                case FormTemplatePrintSheet template:
                    MigraDocFormRenderer.Write(section, template.Sheet, template.Page);
                    break;
```
và trong `FontScaleOf`, thêm dòng trước `PrintFormSheet form => …`:
```csharp
        FormTemplatePrintSheet template => MigraDocFormRenderer.ScaleOf(template.Sheet, template.Page),
```
(đặt TRƯỚC `PrintFormSheet` vì cả hai là `PrintSheet`; hai kiểu không liên quan nên thứ tự không quan trọng, nhưng đặt cạnh nhau cho dễ đọc.)

- [ ] **Step 6: Chạy test**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --nologo -v q --filter MigraDocFormRendererTests 2>&1 | tail -25`
Expected: xanh. Nếu `Gdntt_fits_one_portrait_page_even_with_long_values` đỏ vì 2 trang: `VerticalSafety` hạ xuống 0,95 hoặc kiểm `ScaleOf` có áp `FitToPagesTall == 1` — ĐỪNG nới test. Nếu `Printed_text_matches…` lệch ở ô đã gộp tràn, kiểm `SheetTextMap.FromMigraDoc` đọc ô neo (dòng, cột) đúng chỗ.

- [ ] **Step 7: Chạy cả bộ test Application để chắc đường cũ chưa vỡ**

Run: `dotnet test tests/ConstructionFinance.Application.Tests --nologo -v q 2>&1 | grep -E "Passed!|Failed!"`
Expected: `Passed!`.

- [ ] **Step 8: Commit**

```bash
git add src/ConstructionFinance.Application/Printing tests/ConstructionFinance.Application.Tests/Printing/MigraDocFormRendererTests.cs
git commit -m "feat(print): bộ vẽ MigraDoc chung cho ba biểu mẫu từ tờ đã trộn, ép một trang theo chiều dọc

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

---

### Task 7: Lưu chữ tự nhập — bảng `finance_form_inputs`, migration, kho dữ liệu

**Files:**
- Create: `src/ConstructionFinance.Domain/Finance/FormInput.cs`
- Modify: `src/ConstructionFinance.Domain/Finance/Project.cs` (thêm `FormInputs`)
- Create: `src/ConstructionFinance.Data/Configurations/Finance/FormInputConfiguration.cs`
- Modify: `src/ConstructionFinance.Data/Configurations/Finance/FinanceProjectConfiguration.cs` (bỏ ánh xạ ba bản khai cũ)
- Delete: `src/ConstructionFinance.Data/Configurations/Finance/{ThongTri,KsChi,Gdntt}DeclarationConfiguration.cs`
- Modify: `src/ConstructionFinance.Data/AppDbContext.cs`, `src/ConstructionFinance.Data/Repositories/FinanceProjectRepository.cs`
- Create (sinh bằng `dotnet ef`, sửa tay): `src/ConstructionFinance.Data/Migrations/<ts>_AddFormInputs.cs` (+ Designer, + snapshot cập nhật)
- Delete: `tests/ConstructionFinance.Data.Tests/Finance/FormDeclarationPersistenceTests.cs`
- Test: `tests/ConstructionFinance.Data.Tests/Finance/FormInputPersistenceTests.cs`, `tests/ConstructionFinance.Data.Tests/Finance/FormInputMigrationTests.cs`

**Interfaces:**
- Consumes: Task 1 không trực tiếp (Domain không phụ thuộc Application).
- Produces: `class FormInput : BaseEntity { int ProjectId; Project Project; string Form; string Key; string? Value }`; `Project.FormInputs : ICollection<FormInput>`; `GetTreeAsync` nạp `FormInputs`; `ReplaceTreeAsync` ghi `project.FormInputs` theo kiểu upsert-theo-khoá (xoá khoá không còn, cập nhật khoá có, thêm khoá mới). `Project.ThongTri/KsChi/Gdntt` VẪN còn trên lớp nhưng không ánh xạ (chỉ để mã cũ biên dịch tới Task 11).

- [ ] **Step 1: Viết test lưu trữ (đỏ)**

```csharp file=tests/ConstructionFinance.Data.Tests/Finance/FormInputPersistenceTests.cs
using ConstructionFinance.Data.Repositories;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Finance;

public class FormInputPersistenceTests
{
    private static FormInput Input(string form, string key, string? value) =>
        new() { Form = form, Key = key, Value = value };

    [Fact]
    public async Task ReplaceTreeAsync_inserts_the_inputs_and_GetTreeAsync_loads_them()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new FinanceProjectRepository(ctx);
        var id = await repo.AddAsync(new Project { Name = "DA1" });

        await repo.ReplaceTreeAsync(new Project
        {
            Id = id, Name = "DA1",
            FormInputs =
            {
                Input("thongtri", "SoThongTri", "12/TT"),
                Input("kschi", "ThueGTGT", "5000000"),
                Input("gdntt", "TaiNganHang", "Vietcombank Hải Phòng"),
            }
        });

        // GetTreeAsync là AsNoTracking; không Clear() thì identity map trả lại chính đối tượng vừa ghi.
        ctx.ChangeTracker.Clear();
        var tree = await repo.GetTreeAsync(id);

        tree!.FormInputs.Select(i => (i.Form, i.Key, i.Value)).OrderBy(t => t.Key).ShouldBe(new[]
        {
            ("thongtri", "SoThongTri", (string?)"12/TT"),
            ("gdntt", "TaiNganHang", "Vietcombank Hải Phòng"),
            ("kschi", "ThueGTGT", "5000000"),
        }.OrderBy(t => t.Item2));
    }

    [Fact]
    public async Task ReplaceTreeAsync_updates_adds_and_removes_keys()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new FinanceProjectRepository(ctx);
        var id = await repo.AddAsync(new Project { Name = "DA1" });
        await repo.ReplaceTreeAsync(new Project
        {
            Id = id, Name = "DA1",
            FormInputs = { Input("thongtri", "A", "1"), Input("thongtri", "B", "2") }
        });

        await repo.ReplaceTreeAsync(new Project
        {
            Id = id, Name = "DA1",
            FormInputs = { Input("thongtri", "B", "hai"), Input("thongtri", "C", "3") }
        });

        ctx.ChangeTracker.Clear();
        var tree = await repo.GetTreeAsync(id);
        tree!.FormInputs.ToDictionary(i => i.Key, i => i.Value)
            .ShouldBe(new Dictionary<string, string?> { ["B"] = "hai", ["C"] = "3" });
        ctx.FormInputs.Count(i => i.ProjectId == id).ShouldBe(2);
    }

    [Fact]
    public async Task The_same_key_may_exist_in_two_forms_and_two_projects()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new FinanceProjectRepository(ctx);
        var first = await repo.AddAsync(new Project { Name = "DA1" });
        var second = await repo.AddAsync(new Project { Name = "DA2" });

        await repo.ReplaceTreeAsync(new Project
        {
            Id = first, Name = "DA1",
            FormInputs = { Input("thongtri", "DonVi", "x"), Input("kschi", "DonVi", "y") }
        });
        await repo.ReplaceTreeAsync(new Project
        {
            Id = second, Name = "DA2",
            FormInputs = { Input("thongtri", "DonVi", "z") }
        });

        ctx.ChangeTracker.Clear();
        (await repo.GetTreeAsync(first))!.FormInputs.Count.ShouldBe(2);
        (await repo.GetTreeAsync(second))!.FormInputs.Single().Value.ShouldBe("z");
    }

    [Fact]
    public async Task An_explicit_empty_value_is_kept_distinct_from_a_missing_key()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new FinanceProjectRepository(ctx);
        var id = await repo.AddAsync(new Project { Name = "DA1" });

        await repo.ReplaceTreeAsync(new Project { Id = id, Name = "DA1", FormInputs = { Input("thongtri", "Khoan", "") } });

        ctx.ChangeTracker.Clear();
        (await repo.GetTreeAsync(id))!.FormInputs.Single().Value.ShouldBe("");
    }

    [Fact]
    public async Task ReplaceTreeAsync_for_a_new_project_inserts_its_inputs()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new FinanceProjectRepository(ctx);
        var project = new Project { Id = 0, Name = "Mới", FormInputs = { Input("gdntt", "So", "01") } };

        await repo.ReplaceTreeAsync(project);

        ctx.ChangeTracker.Clear();
        var tree = await repo.GetTreeAsync(project.Id);
        tree!.FormInputs.Single().Key.ShouldBe("So");
    }

    [Fact]
    public async Task Deleting_a_project_cascades_to_its_inputs()
    {
        using var ctx = TestDbContextFactory.CreateSqliteInMemory();
        var repo = new FinanceProjectRepository(ctx);
        var id = await repo.AddAsync(new Project { Name = "DA1" });
        await repo.ReplaceTreeAsync(new Project { Id = id, Name = "DA1", FormInputs = { Input("kschi", "A", "1") } });

        await repo.DeleteAsync(id);

        ctx.FormInputs.Count().ShouldBe(0);
    }
}
```

- [ ] **Step 2: Viết test migration (đỏ)**

```csharp file=tests/ConstructionFinance.Data.Tests/Finance/FormInputMigrationTests.cs
using ConstructionFinance.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Data.Tests.Finance;

/// Chạy migration thật trên tệp SQLite thật: dừng ở migration ngay trước, nhét dữ liệu bằng SQL thô vào ba bảng
/// cũ, rồi chạy `AddFormInputs` và đọc lại. App chạy MigrateAsync mỗi lần khởi động và xoá bảng là không đảo
/// ngược — nên đường này phải được thử trên dữ liệu thật dạng, không chỉ trên model.
public class FormInputMigrationTests : IDisposable
{
    private const string Before = "20260812180157_AddFormDeclarations";
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"cf-mig-{Guid.NewGuid():N}.db");

    private AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source={_path};Foreign Keys=True").Options);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
            if (File.Exists(file)) File.Delete(file);
    }

    private async Task MigrateTo(string? target)
    {
        using var ctx = NewContext();
        var migrator = ctx.GetService<IMigrator>();
        if (target is null) await migrator.MigrateAsync();
        else await migrator.MigrateAsync(target);
    }

    private static async Task Exec(AppDbContext ctx, string sql) => await ctx.Database.ExecuteSqlRawAsync(sql);

    private async Task<List<(int ProjectId, string Form, string Key, string? Value)>> ReadInputs()
    {
        using var ctx = NewContext();
        var rows = new List<(int, string, string, string?)>();
        await ctx.Database.OpenConnectionAsync();
        using var command = ctx.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT ProjectId, Form, Key, Value FROM finance_form_inputs ORDER BY ProjectId, Form, Key";
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
        return rows;
    }

    private async Task<int> InsertProject(AppDbContext ctx, string name)
    {
        await Exec(ctx, "INSERT INTO finance_projects (Name, ContractValue, ContractAnnexValue, CreatedAt, UpdatedAt) " +
                        $"VALUES ('{name}', '0', '0', '2026-01-01', '2026-01-01')");
        using var command = ctx.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT last_insert_rowid()";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Migration_copies_non_empty_values_only()
    {
        await MigrateTo(Before);
        int project;
        using (var ctx = NewContext())
        {
            await ctx.Database.OpenConnectionAsync();
            project = await InsertProject(ctx, "DA1");
            await Exec(ctx, "INSERT INTO finance_thongtri_decl (ProjectId, SoThongTri, Khoan, Muc, CreatedAt, UpdatedAt) " +
                            $"VALUES ({project}, '12/TT', '', NULL, '2026-01-01', '2026-01-01')");
            await Exec(ctx, "INSERT INTO finance_kschi_decl (ProjectId, MucTieuMuc1, ThueGTGT, SoTuChoi, ThuHoiNamNay, ThuHoiCacNamTruoc, " +
                            "CreatedAt, UpdatedAt) " +
                            $"VALUES ({project}, '9300 / 9301', '5000000', '0', '1500000.5', '0', '2026-01-01', '2026-01-01')");
            await Exec(ctx, "INSERT INTO finance_gdntt_decl (ProjectId, TaiNganHang, NgayThangNam, ChuyenTienBaoHanh, ChuyenTienTamGiu, " +
                            "CreatedAt, UpdatedAt) " +
                            $"VALUES ({project}, 'Vietcombank', '3 tháng 4', '1000', '0', '2026-01-01', '2026-01-01')");
        }

        await MigrateTo(null);
        var inputs = await ReadInputs();

        inputs.Select(i => (i.Form, i.Key, i.Value)).ShouldBe(new (string, string, string?)[]
        {
            ("gdntt", "ChuyenTienBaoHanh", "1000"),
            ("gdntt", "NgayThangNam", "Hải phòng, ngày 3 tháng 4"),
            ("gdntt", "TaiNganHang", "Vietcombank"),
            ("kschi", "Muc1", "9300 / 9301"),
            ("kschi", "ThuHoiNamNay", "1500000.5"),
            ("kschi", "ThueGTGT", "5000000"),
            ("thongtri", "SoThongTri", "12/TT"),
        }, ignoreOrder: false);
        inputs.ShouldAllBe(i => i.ProjectId == project);
    }

    [Fact]
    public async Task Migration_runs_on_an_empty_database()
    {
        await MigrateTo(null);

        (await ReadInputs()).ShouldBeEmpty();
        using var ctx = NewContext();
        (await ctx.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Migration_drops_the_three_old_declaration_tables()
    {
        await MigrateTo(null);

        using var ctx = NewContext();
        await ctx.Database.OpenConnectionAsync();
        using var command = ctx.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'finance_%_decl'";
        using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Migration_keeps_the_unique_index_on_project_form_key()
    {
        await MigrateTo(null);
        using var ctx = NewContext();
        await ctx.Database.OpenConnectionAsync();
        var project = await InsertProject(ctx, "DA1");
        await Exec(ctx, "INSERT INTO finance_form_inputs (ProjectId, Form, Key, Value, CreatedAt, UpdatedAt) " +
                        $"VALUES ({project}, 'thongtri', 'A', '1', '2026-01-01', '2026-01-01')");

        var duplicate = async () => await Exec(ctx, "INSERT INTO finance_form_inputs (ProjectId, Form, Key, Value, CreatedAt, UpdatedAt) " +
                                                    $"VALUES ({project}, 'thongtri', 'A', '2', '2026-01-01', '2026-01-01')");

        await duplicate.ShouldThrowAsync<Exception>();
    }
}
```

- [ ] **Step 3: Viết entity, cấu hình**

```csharp file=src/ConstructionFinance.Domain/Finance/FormInput.cs
using ConstructionFinance.Domain.Common;
namespace ConstructionFinance.Domain.Finance;

/// Một ô người dùng tự gõ trên một biểu mẫu của một dự án: (dự án, biểu mẫu, khoá ô) → chữ. Khoá là tên
/// ổn định trong bản gốc biểu mẫu (vd. "SoThongTri"), KHÔNG phải địa chỉ ô, để dời ô không mất chữ; thêm ô
/// mới vào bản gốc không cần migration. Số lưu dạng số bất biến văn hoá ("1500000.5"). `Value == ""` nghĩa
/// là người dùng đã xoá cố ý (khác với không có dòng nào: chưa gõ).
public class FormInput : BaseEntity
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    /// "thongtri" | "kschi" | "gdntt" (FormDefinition.Id).
    public string Form { get; set; } = "";

    public string Key { get; set; } = "";

    public string? Value { get; set; }
}
```

Trong `Project.cs`, ngay dưới ba dòng `ThongTri/KsChi/Gdntt` thêm:
```csharp
    /// Chữ người dùng tự gõ trên ba biểu mẫu của dự án này (finance_form_inputs).
    public ICollection<FormInput> FormInputs { get; set; } = new List<FormInput>();
```
và đổi chú thích ba dòng cũ thành: `// Chuyển tiếp tới Task 11: không còn ánh xạ vào DB (xem FinanceProjectConfiguration), sẽ xoá cùng mã cũ.`

```csharp file=src/ConstructionFinance.Data/Configurations/Finance/FormInputConfiguration.cs
using ConstructionFinance.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConstructionFinance.Data.Configurations.Finance;

public class FormInputConfiguration : IEntityTypeConfiguration<FormInput>
{
    public void Configure(EntityTypeBuilder<FormInput> b)
    {
        b.ToTable("finance_form_inputs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Form).IsRequired().HasMaxLength(32);
        b.Property(x => x.Key).IsRequired().HasMaxLength(64);
        b.Property(x => x.Value).HasMaxLength(4000);

        b.HasIndex(x => new { x.ProjectId, x.Form, x.Key }).IsUnique();
        b.HasOne(x => x.Project).WithMany(p => p.FormInputs)
            .HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 4: Sửa `FinanceProjectConfiguration.cs`, `AppDbContext.cs`, xoá ba cấu hình cũ**

Trong `FinanceProjectConfiguration.Configure`, thêm cuối thân:
```csharp
        // Ba bản khai cũ chỉ còn tồn tại trong bộ nhớ tới Task 11 — bảng của chúng đã bị migration AddFormInputs xoá.
        b.Ignore(x => x.ThongTri);
        b.Ignore(x => x.KsChi);
        b.Ignore(x => x.Gdntt);
```
Trong `AppDbContext.cs`: xoá ba dòng `using FinanceXxxDeclaration = …` và ba `DbSet<…Declaration>`, thêm
```csharp
using FinanceFormInput = ConstructionFinance.Domain.Finance.FormInput;
…
    public DbSet<FinanceFormInput> FormInputs => Set<FinanceFormInput>();
```
Xoá ba tệp `ThongTriDeclarationConfiguration.cs`, `KsChiDeclarationConfiguration.cs`, `GdnttDeclarationConfiguration.cs` (`git rm`).

- [ ] **Step 5: Sửa kho dữ liệu `FinanceProjectRepository.cs`**

(a) `GetTreeAsync`: thay ba `.Include(p => p.ThongTri/KsChi/Gdntt)` bằng `.Include(p => p.FormInputs)`.
(b) `ReplaceTreeAsync`: ở truy vấn `existing`, thay ba Include đó bằng `.Include(p => p.FormInputs)`; thay dòng `UpsertDeclarations(existing, project);` bằng `UpsertFormInputs(existing, project);`.
(c) Xoá `UpsertDeclarations`, `CopyThongTri`, `CopyKsChi`, `CopyGdntt` và thay bằng:

```csharp
    /// Chữ tự nhập đi cùng cây nhưng KHÔNG theo chiến lược xoá-rồi-thêm của ViTris: upsert theo (Form, Key) —
    /// khoá còn thì cập nhật, khoá mới thì thêm, khoá không còn trong bản gửi lên thì xoá. Xoá-rồi-thêm cùng
    /// khoá trong MỘT lần lưu sẽ đụng chỉ mục duy nhất tuỳ thứ tự lệnh EF phát ra.
    private void UpsertFormInputs(Project existing, Project incoming)
    {
        var wanted = incoming.FormInputs
            .GroupBy(i => (i.Form, i.Key))
            .ToDictionary(g => g.Key, g => g.Last().Value);

        foreach (var current in existing.FormInputs.ToList())
        {
            if (wanted.TryGetValue((current.Form, current.Key), out var value))
            {
                current.Value = value;
                wanted.Remove((current.Form, current.Key));
            }
            else
            {
                _context.FormInputs.Remove(current);
            }
        }

        foreach (var ((form, key), value) in wanted)
            existing.FormInputs.Add(new FormInput { ProjectId = existing.Id, Form = form, Key = key, Value = value });
    }
```
Cập nhật chú thích của `GetTreeAsync` ("form pages mutate the declaration entities") thành "…the form view-models read FormInputs from this tree".

- [ ] **Step 6: Xoá test cũ, biên dịch riêng lớp dữ liệu**

Run: `git rm tests/ConstructionFinance.Data.Tests/Finance/FormDeclarationPersistenceTests.cs && dotnet build src/ConstructionFinance.Data --nologo -v q 2>&1 | tail -3`
Expected: `Build succeeded` (migration chưa có, model snapshot chưa khớp — chưa sao).

- [ ] **Step 7: Sinh migration**

Run: `dotnet ef migrations add AddFormInputs --project src/ConstructionFinance.Data --startup-project src/ConstructionFinance.Data`
Expected: tệp `…_AddFormInputs.cs`, `…_AddFormInputs.Designer.cs`, và `AppDbContextModelSnapshot.cs` đổi. Mở `…_AddFormInputs.cs`: `Up` sẽ có `DropTable` ba bảng cũ trước, rồi `CreateTable finance_form_inputs`. **Sắp lại thứ tự** `Up` thành: `CreateTable finance_form_inputs` → `CreateIndex` → `migrationBuilder.Sql(…)` chép dữ liệu → `DropTable` ba bảng cũ. Khối SQL:

```csharp
            // Chép chữ người dùng đã gõ từ ba bảng cũ sang bảng khoá-giá trị. Chỉ chép giá trị khác NULL và khác rỗng
            // (rỗng ở bảng cũ nghĩa là "chưa gõ", không phải "đã xoá"). Số (decimal TEXT) chép nguyên chuỗi — đã ở dạng
            // bất biến văn hoá. KS chi: bản cũ lưu MỘT chuỗi "mục, tiểu mục" mỗi dòng → vào khoá Muc{i} (bản mới tách
            // hai ô; chuỗi cũ nằm trọn ở ô Mục, người dùng tự tách khi mở lại).
            migrationBuilder.Sql(@"
INSERT INTO finance_form_inputs (ProjectId, Form, Key, Value, CreatedAt, UpdatedAt)
SELECT ProjectId, 'thongtri', 'SoThongTri', SoThongTri, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE SoThongTri IS NOT NULL AND SoThongTri <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'NgayGhiSo', NgayGhiSo, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE NgayGhiSo IS NOT NULL AND NgayGhiSo <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'TenCTMT', TenCTMT, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE TenCTMT IS NOT NULL AND TenCTMT <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'MaSo', MaSo, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE MaSo IS NOT NULL AND MaSo <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'VeThangNam', VeThangNam, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE VeThangNam IS NOT NULL AND VeThangNam <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'TaiKhoan1', TaiKhoan1, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE TaiKhoan1 IS NOT NULL AND TaiKhoan1 <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'DonVi1', DonVi1, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE DonVi1 IS NOT NULL AND DonVi1 <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'SoChungTu1', SoChungTu1, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE SoChungTu1 IS NOT NULL AND SoChungTu1 <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'TaiKhoan2', TaiKhoan2, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE TaiKhoan2 IS NOT NULL AND TaiKhoan2 <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'DonVi2', DonVi2, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE DonVi2 IS NOT NULL AND DonVi2 <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'SoChungTu2', SoChungTu2, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE SoChungTu2 IS NOT NULL AND SoChungTu2 <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'LoaiNganSach', LoaiNganSach, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE LoaiNganSach IS NOT NULL AND LoaiNganSach <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'Khoan', Khoan, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE Khoan IS NOT NULL AND Khoan <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'Muc', Muc, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE Muc IS NOT NULL AND Muc <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'TieuMuc', TieuMuc, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE TieuMuc IS NOT NULL AND TieuMuc <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'TietMuc', TietMuc, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE TietMuc IS NOT NULL AND TietMuc <> ''
UNION ALL SELECT ProjectId, 'thongtri', 'Nganh', Nganh, CreatedAt, UpdatedAt FROM finance_thongtri_decl WHERE Nganh IS NOT NULL AND Nganh <> ''
UNION ALL SELECT ProjectId, 'kschi', 'NgayNhanGiayDeNghi', NgayNhanGiayDeNghi, CreatedAt, UpdatedAt FROM finance_kschi_decl WHERE NgayNhanGiayDeNghi IS NOT NULL AND NgayNhanGiayDeNghi <> ''
UNION ALL SELECT ProjectId, 'kschi', 'Muc1', MucTieuMuc1, CreatedAt, UpdatedAt FROM finance_kschi_decl WHERE MucTieuMuc1 IS NOT NULL AND MucTieuMuc1 <> ''
UNION ALL SELECT ProjectId, 'kschi', 'Muc2', MucTieuMuc2, CreatedAt, UpdatedAt FROM finance_kschi_decl WHERE MucTieuMuc2 IS NOT NULL AND MucTieuMuc2 <> ''
UNION ALL SELECT ProjectId, 'kschi', 'Muc3', MucTieuMuc3, CreatedAt, UpdatedAt FROM finance_kschi_decl WHERE MucTieuMuc3 IS NOT NULL AND MucTieuMuc3 <> ''
UNION ALL SELECT ProjectId, 'kschi', 'Muc4', MucTieuMuc4, CreatedAt, UpdatedAt FROM finance_kschi_decl WHERE MucTieuMuc4 IS NOT NULL AND MucTieuMuc4 <> ''
UNION ALL SELECT ProjectId, 'kschi', 'ThuHoiCacNamTruoc', ThuHoiCacNamTruoc, CreatedAt, UpdatedAt FROM finance_kschi_decl WHERE CAST(ThuHoiCacNamTruoc AS REAL) <> 0
UNION ALL SELECT ProjectId, 'kschi', 'ThuHoiNamNay', ThuHoiNamNay, CreatedAt, UpdatedAt FROM finance_kschi_decl WHERE CAST(ThuHoiNamNay AS REAL) <> 0
UNION ALL SELECT ProjectId, 'kschi', 'ThueGTGT', ThueGTGT, CreatedAt, UpdatedAt FROM finance_kschi_decl WHERE CAST(ThueGTGT AS REAL) <> 0
UNION ALL SELECT ProjectId, 'kschi', 'SoTuChoi', SoTuChoi, CreatedAt, UpdatedAt FROM finance_kschi_decl WHERE CAST(SoTuChoi AS REAL) <> 0
UNION ALL SELECT ProjectId, 'kschi', 'LyDoTuChoi', LyDoTuChoi, CreatedAt, UpdatedAt FROM finance_kschi_decl WHERE LyDoTuChoi IS NOT NULL AND LyDoTuChoi <> ''
UNION ALL SELECT ProjectId, 'kschi', 'GhiChu', GhiChu, CreatedAt, UpdatedAt FROM finance_kschi_decl WHERE GhiChu IS NOT NULL AND GhiChu <> ''
UNION ALL SELECT ProjectId, 'gdntt', 'So', So, CreatedAt, UpdatedAt FROM finance_gdntt_decl WHERE So IS NOT NULL AND So <> ''
UNION ALL SELECT ProjectId, 'gdntt', 'NgayThangNam', 'Hải phòng, ngày ' || NgayThangNam, CreatedAt, UpdatedAt FROM finance_gdntt_decl WHERE NgayThangNam IS NOT NULL AND NgayThangNam <> ''
UNION ALL SELECT ProjectId, 'gdntt', 'KinhGui', KinhGui, CreatedAt, UpdatedAt FROM finance_gdntt_decl WHERE KinhGui IS NOT NULL AND KinhGui <> ''
UNION ALL SELECT ProjectId, 'gdntt', 'TenDuAnCongTrinh', TenDuAnCongTrinh, CreatedAt, UpdatedAt FROM finance_gdntt_decl WHERE TenDuAnCongTrinh IS NOT NULL AND TenDuAnCongTrinh <> ''
UNION ALL SELECT ProjectId, 'gdntt', 'ChuDauTu', ChuDauTu, CreatedAt, UpdatedAt FROM finance_gdntt_decl WHERE ChuDauTu IS NOT NULL AND ChuDauTu <> ''
UNION ALL SELECT ProjectId, 'gdntt', 'CanCuQuyetDinh', CanCuQuyetDinh, CreatedAt, UpdatedAt FROM finance_gdntt_decl WHERE CanCuQuyetDinh IS NOT NULL AND CanCuQuyetDinh <> ''
UNION ALL SELECT ProjectId, 'gdntt', 'CanCuHopDong', CanCuHopDong, CreatedAt, UpdatedAt FROM finance_gdntt_decl WHERE CanCuHopDong IS NOT NULL AND CanCuHopDong <> ''
UNION ALL SELECT ProjectId, 'gdntt', 'CanCuGiayDeNghi', CanCuGiayDeNghi, CreatedAt, UpdatedAt FROM finance_gdntt_decl WHERE CanCuGiayDeNghi IS NOT NULL AND CanCuGiayDeNghi <> ''
UNION ALL SELECT ProjectId, 'gdntt', 'ThuocKeHoach', ThuocKeHoach, CreatedAt, UpdatedAt FROM finance_gdntt_decl WHERE ThuocKeHoach IS NOT NULL AND ThuocKeHoach <> ''
UNION ALL SELECT ProjectId, 'gdntt', 'TaiNganHang', TaiNganHang, CreatedAt, UpdatedAt FROM finance_gdntt_decl WHERE TaiNganHang IS NOT NULL AND TaiNganHang <> ''
UNION ALL SELECT ProjectId, 'gdntt', 'ChuyenTienBaoHanh', ChuyenTienBaoHanh, CreatedAt, UpdatedAt FROM finance_gdntt_decl WHERE CAST(ChuyenTienBaoHanh AS REAL) <> 0
UNION ALL SELECT ProjectId, 'gdntt', 'ChuyenTienTamGiu', ChuyenTienTamGiu, CreatedAt, UpdatedAt FROM finance_gdntt_decl WHERE CAST(ChuyenTienTamGiu AS REAL) <> 0;
");
```
(Bản cũ lưu `NgayThangNam` là phần SAU chữ "Ngày"; bản mới là cả dòng "Hải phòng, ngày …", nên chép kèm tiền tố.) `Down` do EF sinh (tạo lại ba bảng rỗng, xoá bảng mới) — giữ nguyên, thêm chú thích "dữ liệu không được chép ngược".

- [ ] **Step 8: Chạy test Data**

Run: `dotnet test tests/ConstructionFinance.Data.Tests --nologo -v q 2>&1 | grep -E "Passed!|Failed!|error"`
Expected: `Passed!`. Nếu `Migration_copies_non_empty_values_only` đỏ vì thứ tự, đọc danh sách thực tế rồi chỉnh THỨ TỰ MONG ĐỢI trong test (SQL `ORDER BY ProjectId, Form, Key` là thứ tự theo chữ, không theo số).

- [ ] **Step 9: Biên dịch cả solution (mã Desktop cũ vẫn dùng `Project.ThongTri`…)**

Run: `dotnet build ConstructionFinance.sln --nologo -v q 2>&1 | grep -E "error|Build succeeded" | head`
Expected: `Build succeeded`. Test Desktop cũ vẫn xanh (ba bản khai chỉ còn trong bộ nhớ).

- [ ] **Step 10: SAO LƯU `app.db` THẬT trước mọi lần chạy app thật**

```bash
cp -p ~/.local/share/ConstructionFinance/app.db "$HOME/.local/share/ConstructionFinance/app.db.truoc-form-inputs-$(date +%Y%m%d-%H%M%S)"
sqlite3 ~/.local/share/ConstructionFinance/app.db "select count(*) from finance_projects; select count(*) from finance_thongtri_decl; select count(*) from finance_kschi_decl; select count(*) from finance_gdntt_decl;"
```
Expected: sao lưu tồn tại; ba bảng khai có 0 dòng (đúng như spec §5.3 đã ghi ngày 2026-10-15). Từ đây MỌI lần chạy app để thử đều đặt `CONSTRUCTIONFINANCE_DB=<tệp tạm>` — không để app chạm `app.db` thật cho tới khi người dùng tự mở.

- [ ] **Step 11: Commit**

```bash
git add -A src/ConstructionFinance.Domain src/ConstructionFinance.Data tests/ConstructionFinance.Data.Tests
git commit -m "feat(data): bảng finance_form_inputs thay ba bảng khai biểu mẫu, migration chép dữ liệu

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 8: View-model — một `FormPageViewModel` chung, hộp thoại chọn đợt, nối vào workbook

**Files:**
- Create: `src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/FormPageViewModel.cs`
- Create: `src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/FormTabPageViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/FormDotPickerViewModel.cs`
- Modify: `src/ConstructionFinance.Desktop/ViewModels/Finance/FinanceWorkbookViewModel.cs`
- Test: `tests/ConstructionFinance.Desktop.Tests/Finance/Forms/FormPageViewModelTests.cs` (mới)
- Test: sửa `tests/ConstructionFinance.Desktop.Tests/Finance/FinanceWorkbookFormTabsTests.cs`, `PrintWorkbookTests.cs`, `Forms/FormDotPickerViewModelTests.cs`

**Interfaces:**
- Consumes: Task 4 (`FormDefinition`, `FormBuildContext`, `FormBuildResult`), Task 5 (`IFormExcelExporter.ExportAsync`, `FormTemplatePrintSheet`), Task 7 (`FormInput`, `Project.FormInputs`).
- Produces:
  - `interface IFormSheetEditor { void SetInput(string key, string? text); void ResetOverride(string key); }`
  - `sealed class FormPageViewModel : ViewModelBase, IFormSheetEditor` — `Definition`, `Picker`, `Sheet` (`ResolvedSheet`, luôn khác null), `ErrorMessage`, `PrintBlockReason`, `CanExport`, `HasError`, `ChooseDotsText`, `StatusText`, `int Year { get; set; }`, `event EventHandler? InputsChanged`, `event EventHandler<OrgEditedEventArgs>? OrgEdited`, `Refresh(rows, sources, org, projectName)`, `LoadInputs(IEnumerable<FormInput>)`, `IEnumerable<FormInput> ToFormInputs()`, lệnh `ChooseDotsCommand`, `ExportCommand`
  - `record OrgEditedEventArgs(string Key, string? Value)`
  - `sealed class FormTabPageViewModel : WorkbookPageViewModel` — `Inner : FormPageViewModel`, `PrintSheetName`, `BuildPrintSheet()`, `PrintUnavailableReason`
  - `FormDotPickerViewModel.SaveSelection()/RestoreSelection(...)`, `Title`, implements `IHasDialogTitle`, `IHasPrimaryButtonText` ("OK")
  - `FinanceWorkbookViewModel.OpenFormTabAsync(string formId)`

- [ ] **Step 1: Viết test cho `FormPageViewModel` (đỏ)**

```csharp file=tests/ConstructionFinance.Desktop.Tests/Finance/Forms/FormPageViewModelTests.cs
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

public class FormPageViewModelTests
{
    private static readonly FormOrgInfo Org = new("Sở Tài chính", "Ban QLDA số 1");

    private static FormDotRow Dot(FormDotKey key, Fund fund, decimal advance, string content, string package = "Gói A") =>
        new(key, "Vị trí 1", "CHI PHÍ XÂY DỰNG", package, "Cty XL A", "123456789",
            50_000_000_000m, 0m, new DateOnly(2026, 3, 1), content, "CT01", fund, advance, 0m, 0m, advance);

    private static IReadOnlyList<FormDotRow> Rows() => new[]
    {
        Dot(new(0, 0, 0, 0), Fund.NSNN, 10_000_000m, "Tạm ứng lần 1"),
        Dot(new(0, 0, 0, 1), Fund.NgoaiHoi, 30_000_000m, "Ngoại hối 1"),
        Dot(new(1, 0, 0, 0), Fund.NgoaiHoi, 5_000_000m, "Ngoại hối 2", package: "Gói B"),
    };

    private static (FormPageViewModel Vm, FakeDialogService Dialog, Mock<IFormExcelExporter> Exporter) Create(
        FormDefinition definition)
    {
        var dialog = new FakeDialogService();
        var exporter = new Mock<IFormExcelExporter>();
        var vm = new FormPageViewModel(definition, exporter.Object, dialog) { Year = 2026 };
        vm.Refresh(Rows(), Array.Empty<LedgerRowViewModel>(), Org, "Dự án A");
        return (vm, dialog, exporter);
    }

    private static ResolvedSegment Segment(FormPageViewModel vm, string key) =>
        vm.Sheet.Cells.SelectMany(c => c.Segments).First(s => s.Key == key);

    private static void SelectAll(FormPageViewModel vm)
    {
        foreach (var item in vm.Picker.Items) item.IsSelected = true;
    }

    // ===== dựng tờ =====

    [Fact]
    public void Sheet_is_available_before_anything_is_selected()
    {
        var (vm, _, _) = Create(FormDefinitions.ThongTri);

        vm.Sheet.ShouldNotBeNull();
        vm.CanExport.ShouldBeTrue();
        vm.ChooseDotsText.ShouldBe("Chọn đợt (0)…");
    }

    [Fact]
    public void Selecting_dots_rebuilds_the_sheet()
    {
        var (vm, _, _) = Create(FormDefinitions.ThongTri);

        SelectAll(vm);

        vm.Sheet.Cells.Single(c => c.Row == 15 && c.Col == 8).Segments.Single().Number.ShouldBe(10_000_000m);
        vm.ChooseDotsText.ShouldBe("Chọn đợt (1)…");
        vm.StatusText.ShouldBe("Đang chọn 1 đợt");
    }

    [Fact]
    public void Picker_lists_only_the_dots_of_the_form_fund()
    {
        Create(FormDefinitions.ThongTri).Vm.Picker.Items.Count.ShouldBe(1);
        Create(FormDefinitions.Gdntt).Vm.Picker.Items.Count.ShouldBe(2);
        Create(FormDefinitions.KsChi).Vm.Picker.Items.Count.ShouldBe(3);
    }

    // ===== gõ chữ =====

    [Fact]
    public void Typing_text_updates_the_sheet_and_raises_InputsChanged_once()
    {
        var (vm, _, _) = Create(FormDefinitions.ThongTri);
        var raised = 0;
        vm.InputsChanged += (_, _) => raised++;

        vm.SetInput("SoThongTri", "12/TT");

        Segment(vm, "SoThongTri").Value.ShouldBe("12/TT");
        raised.ShouldBe(1);

        vm.SetInput("SoThongTri", "12/TT");     // không đổi → không báo
        raised.ShouldBe(1);
    }

    [Fact]
    public void Typing_a_number_keeps_it_as_a_number_and_ignores_garbage()
    {
        var (vm, _, _) = Create(FormDefinitions.KsChi);

        vm.SetInput("ThueGTGT", "1.500.000");
        Segment(vm, "ThueGTGT").Number.ShouldBe(1_500_000m);

        vm.SetInput("ThueGTGT", "abc");
        Typing_garbage_in_a_number_cell_keeps_the_old_value(vm);

        vm.SetInput("ThueGTGT", "");
        Segment(vm, "ThueGTGT").Number.ShouldBeNull();
        vm.ToFormInputs().Any(i => i.Key == "ThueGTGT").ShouldBeFalse();
    }

    private static void Typing_garbage_in_a_number_cell_keeps_the_old_value(FormPageViewModel vm) =>
        Segment(vm, "ThueGTGT").Number.ShouldBe(1_500_000m);

    [Fact]
    public void Typing_a_negative_number_is_accepted()
    {
        var (vm, _, _) = Create(FormDefinitions.KsChi);

        vm.SetInput("SoTuChoi", "-500");

        Segment(vm, "SoTuChoi").Number.ShouldBe(-500m);
    }

    [Fact]
    public void Setting_input_edits_the_org_and_reports_it_instead_of_storing_it_per_project()
    {
        var (vm, _, _) = Create(FormDefinitions.ThongTri);
        OrgEditedEventArgs? seen = null;
        vm.OrgEdited += (_, e) => seen = e;
        var inputsRaised = 0;
        vm.InputsChanged += (_, _) => inputsRaised++;

        vm.SetInput(FormOrgKeys.DonViCapTren, "Bộ Tài chính");

        seen.ShouldBe(new OrgEditedEventArgs(FormOrgKeys.DonViCapTren, "Bộ Tài chính"));
        vm.Sheet.Cells.Single(c => c.Row == 1 && c.Col == 1).DisplayText.ShouldBe("Bộ Tài chính");
        vm.ToFormInputs().ShouldBeEmpty();
        inputsRaised.ShouldBe(0);
    }

    // ===== tự tính cho gõ đè =====

    [Fact]
    public void Overridable_text_is_overridden_when_typed_and_reverts_when_cleared_or_equal_to_automatic()
    {
        var (vm, _, _) = Create(FormDefinitions.KsChi);
        SelectAll(vm);

        Segment(vm, "GhiChu").IsOverridden.ShouldBeFalse();
        Segment(vm, "GhiChu").Value.ShouldBe("Tạm ứng lần 1");

        vm.SetInput("GhiChu", "Ghi chú của tôi");
        Segment(vm, "GhiChu").IsOverridden.ShouldBeTrue();

        vm.SetInput("GhiChu", "");
        Segment(vm, "GhiChu").IsOverridden.ShouldBeFalse();

        vm.SetInput("GhiChu", "Ghi chú của tôi");
        vm.SetInput("GhiChu", "Tạm ứng lần 1");     // gõ đúng giá trị tự động → bỏ gõ đè
        Segment(vm, "GhiChu").IsOverridden.ShouldBeFalse();
        vm.ToFormInputs().Any(i => i.Key == "GhiChu").ShouldBeFalse();
    }

    [Fact]
    public void ResetOverride_returns_to_automatic_and_marks_changed()
    {
        var (vm, _, _) = Create(FormDefinitions.KsChi);
        SelectAll(vm);
        vm.SetInput("GhiChu", "Ghi chú của tôi");
        var raised = 0;
        vm.InputsChanged += (_, _) => raised++;

        vm.ResetOverride("GhiChu");

        Segment(vm, "GhiChu").IsOverridden.ShouldBeFalse();
        Segment(vm, "GhiChu").Value.ShouldBe("Tạm ứng lần 1");
        raised.ShouldBe(1);

        vm.ResetOverride("GhiChu");      // đã tự động → không báo thừa
        raised.ShouldBe(1);
    }

    [Fact]
    public void Overridable_number_override_can_be_a_different_number_than_the_computed_one()
    {
        var (vm, _, _) = Create(FormDefinitions.Gdntt);
        foreach (var item in vm.Picker.Items.Where(i => i.Data.PackageName == "Gói A")) item.IsSelected = true;
        Segment(vm, "UnrecoveredAdvanceBefore").Number.ShouldBe(10_000_000m);   // đợt 0 (tạm ứng 10tr) đứng trước đợt được chọn

        vm.SetInput("UnrecoveredAdvanceBefore", "15.000.000");

        Segment(vm, "UnrecoveredAdvanceBefore").Number.ShouldBe(15_000_000m);
        Segment(vm, "UnrecoveredAdvanceBefore").IsOverridden.ShouldBeTrue();
    }

    // ===== GĐNTT bị chặn =====

    [Fact]
    public void Gdntt_blocked_still_accepts_typing()
    {
        var (vm, _, _) = Create(FormDefinitions.Gdntt);
        SelectAll(vm);   // hai gói → bị chặn

        vm.CanExport.ShouldBeFalse();
        vm.HasError.ShouldBeTrue();
        vm.StatusText.ShouldBe(vm.ErrorMessage);
        vm.PrintBlockReason.ShouldBe("đã chọn đợt của nhiều gói thầu");

        vm.SetInput("KinhGui", "Kho bạc Nhà nước");

        Segment(vm, "KinhGui").Value.ShouldBe("Kho bạc Nhà nước");
        vm.ExportCommand.CanExecute(null).ShouldBeFalse();
    }

    // ===== xuất =====

    [Fact]
    public async Task Export_writes_the_current_sheet_through_the_exporter()
    {
        var (vm, dialog, exporter) = Create(FormDefinitions.ThongTri);
        dialog.PickSaveFileResult = "/tmp/out.xlsx";

        await vm.ExportCommand.ExecuteAsync(null);

        exporter.Verify(e => e.ExportAsync(vm.Sheet, "/tmp/out.xlsx", It.IsAny<CancellationToken>()), Times.Once);
        dialog.SaveFileRequests.Single().DefaultFileName.ShouldStartWith("ThongTri_Dự_án_A");
        dialog.ShownMessages.ShouldContain(m => m.Contains("Đã xuất"));
    }

    [Fact]
    public async Task Export_cancelled_in_the_file_picker_does_nothing()
    {
        var (vm, dialog, exporter) = Create(FormDefinitions.ThongTri);
        dialog.PickSaveFileResult = null;

        await vm.ExportCommand.ExecuteAsync(null);

        exporter.Verify(e => e.ExportAsync(It.IsAny<ResolvedSheet>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Export_failure_is_reported_not_thrown()
    {
        var (vm, dialog, exporter) = Create(FormDefinitions.ThongTri);
        dialog.PickSaveFileResult = "/tmp/out.xlsx";
        exporter.Setup(e => e.ExportAsync(It.IsAny<ResolvedSheet>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("đĩa đầy"));

        await vm.ExportCommand.ExecuteAsync(null);

        dialog.ShownMessages.ShouldContain(m => m.Contains("đĩa đầy"));
    }

    // ===== hộp thoại chọn đợt =====

    [Fact]
    public async Task ChooseDots_ok_keeps_the_new_selection()
    {
        var (vm, dialog, _) = Create(FormDefinitions.ThongTri);
        dialog.DialogResult = true;
        dialog.OnDialog = picker => ((FormDotPickerViewModel)picker).Items[0].IsSelected = true;

        await vm.ChooseDotsCommand.ExecuteAsync(null);

        dialog.OpenedDialogs.Single().ShouldBeSameAs(vm.Picker);
        vm.Picker.SelectedCount.ShouldBe(1);
        vm.ChooseDotsText.ShouldBe("Chọn đợt (1)…");
    }

    [Fact]
    public async Task ChooseDots_cancel_restores_the_selection_from_before_the_dialog()
    {
        var (vm, dialog, _) = Create(FormDefinitions.KsChi);
        vm.Picker.Items[0].IsSelected = true;
        dialog.DialogResult = false;
        dialog.OnDialog = picker =>
        {
            var p = (FormDotPickerViewModel)picker;
            p.Items[0].IsSelected = false;
            p.Items[1].IsSelected = true;
            p.Items[2].IsSelected = true;
        };

        await vm.ChooseDotsCommand.ExecuteAsync(null);

        vm.Picker.Items.Select(i => i.IsSelected).ShouldBe(new[] { true, false, false });
        vm.ChooseDotsText.ShouldBe("Chọn đợt (1)…");
    }

    // ===== lưu =====

    [Fact]
    public void Inputs_round_trip_through_FormInput_entities()
    {
        var (vm, _, _) = Create(FormDefinitions.ThongTri);
        vm.SetInput("SoThongTri", "12/TT");
        vm.SetInput("Khoan", "");                 // xoá giá trị điền sẵn: phải lưu rỗng, không biến mất

        var saved = vm.ToFormInputs().ToList();

        saved.Select(i => (i.Form, i.Key, i.Value)).OrderBy(t => t.Key).ShouldBe(new (string, string, string?)[]
        {
            ("thongtri", "Khoan", ""),
            ("thongtri", "SoThongTri", "12/TT"),
        });

        var (fresh, _, _) = Create(FormDefinitions.ThongTri);
        fresh.LoadInputs(saved.Append(new FormInput { Form = "kschi", Key = "ThueGTGT", Value = "9" }));
        Segment(fresh, "SoThongTri").Value.ShouldBe("12/TT");
        Segment(fresh, "Khoan").Value.ShouldBe("");
        fresh.ToFormInputs().Count().ShouldBe(2);        // dòng của biểu mẫu khác bị bỏ qua
    }
}
```

- [ ] **Step 2: Chạy test, thấy đỏ** (`FormPageViewModel` chưa có).

- [ ] **Step 3: Sửa `FormDotPickerViewModel.cs`**

Đổi khai báo lớp và thêm thành viên:
```csharp
using ConstructionFinance.Desktop.Services;
…
public partial class FormDotPickerViewModel : ViewModelBase, IHasDialogTitle, IHasPrimaryButtonText
{
    …
    /// Tiêu đề hộp thoại (ghi theo biểu mẫu: "Chọn đợt — Thông tri").
    public string Title { get; init; } = "Chọn đợt";

    string IHasDialogTitle.DialogTitle => Title;
    string IHasPrimaryButtonText.PrimaryButtonText => "OK";

    /// Chụp lựa chọn trước khi mở hộp thoại, để Hủy trả lại đúng như cũ.
    public IReadOnlySet<object> SaveSelection() => _selectedTokens.ToHashSet();

    public void RestoreSelection(IReadOnlySet<object> tokens)
    {
        foreach (var item in _allItems) item.IsSelected = tokens.Contains(item.Token);
    }
```
Thêm vào `FormDotPickerViewModelTests.cs` hai test: `SaveSelection_then_RestoreSelection_returns_to_the_snapshot` (chọn A, chụp, bỏ A chọn B, khôi phục → A chọn B không) và `Picker_is_a_dialog_with_a_title_and_an_OK_button` (`IHasDialogTitle.DialogTitle == "Chọn đợt — X"` khi đặt `Title`, `PrimaryButtonText == "OK"`).

- [ ] **Step 4: Viết `FormPageViewModel`**

```csharp file=src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/FormPageViewModel.cs
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Domain.Finance;
using Serilog;

namespace ConstructionFinance.Desktop.ViewModels.Finance.Forms;

/// Đầu mối mà lưới biểu mẫu (FormSheetControl) gọi khi người dùng gõ vào một ô.
public interface IFormSheetEditor
{
    void SetInput(string key, string? text);
    void ResetOverride(string key);
}

public sealed record OrgEditedEventArgs(string Key, string? Value);

/// Nội dung của MỘT tab biểu mẫu — dùng chung cho Thông tri, KS chi và GĐNTT (khác nhau ở FormDefinition).
/// Không tự tính con số nào: mọi thứ đi qua FormDefinition.Build → builder có sẵn → FormSheetResolver.
public sealed partial class FormPageViewModel : ViewModelBase, IFormSheetEditor
{
    private readonly IFormExcelExporter _exporter;
    private readonly IDialogService _dialog;
    private readonly Dictionary<string, SegmentKind> _kinds;

    private IReadOnlyList<FormDotRow> _rows = Array.Empty<FormDotRow>();
    private FormInputSet _inputs = new();
    private FormOrgInfo _org = new(null, null);
    private string _projectName = "";

    public FormPageViewModel(FormDefinition definition, IFormExcelExporter exporter, IDialogService dialog)
    {
        Definition = definition;
        _exporter = exporter;
        _dialog = dialog;
        _kinds = definition.Template.AllSegments()
            .Where(segment => segment.Key is not null)
            .GroupBy(segment => segment.Key!)
            .ToDictionary(group => group.Key, group => group.First().Kind);

        Picker = new FormDotPickerViewModel(definition.FundFilter) { Title = $"Chọn đợt — {definition.Title}" };
        Picker.SelectionChanged += (_, _) => Rebuild();
        Rebuild();
    }

    public FormDefinition Definition { get; }

    public FormDotPickerViewModel Picker { get; }

    /// Năm điền vào chữ "năm …" của Mẫu. Đặt được để test không phụ thuộc đồng hồ.
    public int Year { get; set; } = DateTime.Now.Year;

    /// Tờ hiện tại. LUÔN khác null, kể cả khi GĐNTT đang bị chặn.
    public ResolvedSheet Sheet { get; private set; } = null!;

    public string? ErrorMessage { get; private set; }

    /// Vì sao "In toàn bộ" bỏ qua tab này (cụm viết thường, không dấu chấm cuối); null khi in được.
    public string? PrintBlockReason { get; private set; }

    public bool CanExport => ErrorMessage is null;
    public bool HasError => ErrorMessage is not null;
    public string ChooseDotsText => $"Chọn đợt ({Picker.SelectedCount})…";
    public string StatusText => ErrorMessage ?? Picker.SelectionSummary;

    /// Bắn khi một ô chữ/số của dự án này thật sự đổi — để workbook đánh dấu bẩn.
    public event EventHandler? InputsChanged;

    /// Bắn khi người dùng sửa tên đơn vị (lưu ở Cài đặt, không theo dự án) — workbook cập nhật mọi tab.
    public event EventHandler<OrgEditedEventArgs>? OrgEdited;

    // ===== nạp / lưu =====

    public void LoadInputs(IEnumerable<FormInput> stored)
    {
        _inputs = new FormInputSet(stored
            .Where(input => input.Form == Definition.Id)
            .Select(input => new KeyValuePair<string, string?>(input.Key, input.Value)));
        Rebuild();
    }

    public IEnumerable<FormInput> ToFormInputs() =>
        _inputs.Values
            .Where(pair => pair.Value is not null)
            .Select(pair => new FormInput { Form = Definition.Id, Key = pair.Key, Value = pair.Value });

    /// Nạp lại từ cây hiện tại (kể cả phần chưa lưu). KHÔNG đụng tới chữ đã gõ.
    public void Refresh(
        IReadOnlyList<FormDotRow> rows,
        IReadOnlyList<LedgerRowViewModel> sources,
        FormOrgInfo org,
        string projectName)
    {
        _rows = rows;
        _org = org;
        _projectName = projectName;
        Picker.Refresh(rows, sources);
        Rebuild();
    }

    // ===== gõ chữ =====

    public void SetInput(string key, string? text)
    {
        if (!_kinds.TryGetValue(key, out var kind)) return;
        text ??= "";

        var changed = kind switch
        {
            SegmentKind.SettingInput => SetOrg(key, text),
            SegmentKind.InputNumber => SetNumber(key, text),
            SegmentKind.Overridable => SetOverridable(key, text),
            _ => SetText(key, text),
        };
        if (!changed) return;

        Rebuild();
        if (kind != SegmentKind.SettingInput) InputsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ResetOverride(string key)
    {
        if (!_inputs.Remove(key)) return;
        Rebuild();
        InputsChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool SetText(string key, string text)
    {
        // So với chữ ĐANG HIỆN (đã gõ hoặc điền sẵn): gõ lại đúng chữ đó không phải là một thay đổi.
        if (CurrentSegment(key)?.Value == text && (_inputs.Has(key) || text.Length > 0)) return false;
        _inputs.Set(key, text);
        return true;
    }

    private bool SetOrg(string key, string text)
    {
        if ((_org.Get(key) ?? "") == text) return false;
        _org = _org.With(key, text);
        OrgEdited?.Invoke(this, new OrgEditedEventArgs(key, text));
        return true;
    }

    private bool SetNumber(string key, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return _inputs.Remove(key);
        if (!MoneyText.TryParse(text, out var number)) return false;      // rác: giữ giá trị cũ
        var stored = FormInputSet.ToStored(number);
        if (_inputs.Get(key) == stored) return false;
        _inputs.Set(key, stored);
        return true;
    }

    private bool SetOverridable(string key, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return _inputs.Remove(key);     // xoá hết = trở về tự động

        var automatic = AutomaticSegment(key);
        if (automatic?.Number is not null)
        {
            if (!MoneyText.TryParse(text, out var number)) return false;
            if (number == automatic.Number) return _inputs.Remove(key);
            var stored = FormInputSet.ToStored(number);
            if (_inputs.Get(key) == stored) return false;
            _inputs.Set(key, stored);
            return true;
        }

        if (text == automatic?.Text) return _inputs.Remove(key);              // gõ đúng giá trị tự động
        if (_inputs.Get(key) == text) return false;
        _inputs.Set(key, text);
        return true;
    }

    private ResolvedSegment? CurrentSegment(string key) =>
        Sheet.Cells.SelectMany(cell => cell.Segments).FirstOrDefault(segment => segment.Key == key);

    /// Đoạn sẽ hiện nếu KHÔNG có chữ gõ đè — dựng thử một lần với bản sao đã bỏ khoá đó.
    private ResolvedSegment? AutomaticSegment(string key)
    {
        var probe = new FormInputSet(_inputs.Values.Where(pair => pair.Key != key));
        return Definition.Build(Context(probe)).Sheet.Cells
            .SelectMany(cell => cell.Segments).FirstOrDefault(segment => segment.Key == key);
    }

    // ===== dựng lại =====

    private FormBuildContext Context(FormInputSet inputs) =>
        new(_rows, Picker.SelectedKeys, inputs, _org, _projectName, Year);

    private void Rebuild()
    {
        var result = Definition.Build(Context(_inputs));
        Sheet = result.Sheet;
        ErrorMessage = result.Error;
        PrintBlockReason = result.PrintBlockReason;

        foreach (var name in new[]
                 {
                     nameof(Sheet), nameof(ErrorMessage), nameof(PrintBlockReason), nameof(CanExport),
                     nameof(HasError), nameof(StatusText), nameof(ChooseDotsText)
                 })
            OnPropertyChanged(name);
        ExportCommand.NotifyCanExecuteChanged();
    }

    // ===== lệnh =====

    [RelayCommand]
    private async Task ChooseDotsAsync()
    {
        var snapshot = Picker.SaveSelection();
        var confirmed = await _dialog.ShowDialogAsync(Picker);
        if (confirmed != true) Picker.RestoreSelection(snapshot);
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        var suggested = $"{Definition.ExportPrefix}_{FileNames.Sanitize(_projectName)}_{DateTime.Now:yyyyMMdd}.xlsx";
        var path = await _dialog.PickSaveFileAsync(Definition.ExportDialogTitle, suggested, new[] { "xlsx" });
        if (path is null) return;

        try
        {
            await _exporter.ExportAsync(Sheet, path);
            await _dialog.ShowMessageAsync("Xuất Excel", $"Đã xuất {Definition.Title} ra:\n{path}");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Xuất {Form} thất bại", Definition.Title);
            await _dialog.ShowMessageAsync("Lỗi", $"Không xuất được {Definition.Title}: {ex.Message}");
        }
    }
}
```
Lưu ý khi biên dịch: `FileNames.Sanitize("Dự án A")` phải ra `Dự_án_A` để test `DefaultFileName.ShouldStartWith("ThongTri_Dự_án_A")` đúng — nếu `FileNames.Sanitize` đang cho kết quả khác (xem `FileNames.cs`), sửa kỳ vọng của TEST theo hàm có sẵn, không sửa hàm.

- [ ] **Step 5: Viết `FormTabPageViewModel`**

```csharp file=src/ConstructionFinance.Desktop/ViewModels/Finance/Forms/FormTabPageViewModel.cs
using System.ComponentModel;
using ConstructionFinance.Application.Printing;

namespace ConstructionFinance.Desktop.ViewModels.Finance.Forms;

/// Một tab biểu mẫu trong dải tab Vị trí. Thay cho ba lớp ThongTri/KsChi/GdnttFormPageViewModel: tab tự đánh dấu
/// bẩn (bullet •) khi chữ của chính nó đổi, và khai nội dung in là tờ đã trộn đang hiện — cùng một tờ với màn
/// hình và nút "Xuất Excel", không dựng riêng cho bản in. Tên sheet là tên trong Mẫu.xlsx; độc lập với chữ trên tab.
public sealed class FormTabPageViewModel : WorkbookPageViewModel
{
    // PrintSheetName đã báo gần nhất, để chỉ báo khi tên thật sự đổi (xem OnInnerPropertyChanged).
    private string? _reportedPrintSheetName;

    public FormTabPageViewModel(FormPageViewModel inner)
    {
        Inner = inner;
        Title = inner.Definition.Title;
        Inner.InputsChanged += (_, _) => IsDirty = true;
        // Tên đơn vị lưu ở Cài đặt chứ không theo dự án, nhưng sửa nó cũng là một thay đổi chưa lưu của tab này.
        Inner.OrgEdited += (_, _) => IsDirty = true;

        _reportedPrintSheetName = PrintSheetName;
        Inner.PropertyChanged += OnInnerPropertyChanged;
    }

    public FormPageViewModel Inner { get; }
    public override object Content => Inner;

    // Tên theo CanExport chứ không cố định: PrintSheetName khác null luôn nghĩa là BuildPrintSheet() có sheet (hợp
    // đồng của IPrintablePage). GĐNTT chưa chọn gói thầu thì chưa có giấy để in.
    public override string? PrintSheetName => Inner.CanExport ? Inner.Definition.Template.SheetName : null;

    public override PrintSheet? BuildPrintSheet() =>
        Inner.CanExport
            ? new FormTemplatePrintSheet(Inner.Definition.Template.SheetName, Inner.Definition.PrintProfile, Inner.Sheet)
            : null;

    public override string? PrintUnavailableReason => Inner.CanExport ? null : Inner.PrintBlockReason;

    // CanExport đổi ngay trên tab này (người dùng tích/bỏ tích đợt) chứ không kèm lần đổi tab nào, nên phải báo cho
    // nơi đang nghe (nút In ở khung chung). Inner báo CanExport ở MỌI lần dựng, kể cả khi giá trị không đổi: chuyển tiếp
    // nguyên xi thì nút In cập nhật liên tục — nên chỉ báo khi tên thật sự khác lần trước.
    private void OnInnerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FormPageViewModel.CanExport)) return;

        var current = PrintSheetName;
        if (current == _reportedPrintSheetName) return;

        _reportedPrintSheetName = current;
        OnPropertyChanged(nameof(PrintSheetName));
        OnPropertyChanged(nameof(PrintUnavailableReason));
    }
}
```
Nếu `WorkbookPageViewModel.OnPropertyChanged` không truy cập được (`protected`), dùng đúng cách lớp cũ `GdnttFormPageViewModel` đã dùng (cùng lớp cha) — chép nguyên.

- [ ] **Step 6: Chạy test `FormPageViewModelTests` + picker**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --nologo -v q --filter "FormPageViewModelTests|FormDotPickerViewModelTests" 2>&1 | tail -15`
Expected: xanh.

- [ ] **Step 7: Nối vào `FinanceWorkbookViewModel`**

Các sửa (đánh số theo thứ tự xuất hiện trong tệp):
1. Ba trường: `private FormTabPageViewModel? _thongTriPage; … _ksChiPage; … _gdnttPage;`. Thêm
```csharp
    // Tên đơn vị sửa trên biểu mẫu nhưng chưa lưu vào Cài đặt.
    private bool _orgDirty;

    // Lần nạp cây của dự án đang chọn. OnSelectedProjectChanged không async nên chỉ giữ lại Task để
    // OpenFormTabAsync chờ được.
    private Task _treeLoad = Task.CompletedTask;
```
2. `OnSelectedProjectChanged`: `_ = LoadSelectedProjectTreeAsync(value);` → `_treeLoad = LoadSelectedProjectTreeAsync(value);`.
3. Hai chỗ dựng ba trang (trong `LoadSelectedProjectTreeAsync` và `AddProject`) — thay khối `new ThongTriFormPageViewModel(…)/new KsChi…/new Gdntt…` + ba dòng `DeclarationChanged += …` bằng `CreateFormPages();` rồi giữ nguyên ba dòng `ViTriPages.Add(_thongTriPage)…`. Hai chỗ `_thongTriPage = null; …` ở đầu `LoadAsync`/`LoadSelectedProjectTreeAsync` giữ nguyên. Thêm `_orgDirty = false;` cạnh `_formDeclarationDirty = false;` ở cả `LoadAsync` và `LoadSelectedProjectTreeAsync`. Thêm hàm:
```csharp
    /// Dựng ba tab biểu mẫu cho dự án đang chọn (cây đã nằm ở _selectedProjectTree): nạp chữ đã gõ từ cây và nghe
    /// thay đổi của từng tab.
    private void CreateFormPages()
    {
        _thongTriPage = NewFormPage(FormDefinitions.ThongTri);
        _ksChiPage = NewFormPage(FormDefinitions.KsChi);
        _gdnttPage = NewFormPage(FormDefinitions.Gdntt);
    }

    private FormTabPageViewModel NewFormPage(FormDefinition definition)
    {
        var page = new FormTabPageViewModel(new FormPageViewModel(definition, _formExporter, _dialog));
        page.Inner.InputsChanged += OnFormDeclarationChanged;
        page.Inner.OrgEdited += OnOrgEdited;
        if (_selectedProjectTree is not null) page.Inner.LoadInputs(_selectedProjectTree.FormInputs);
        return page;
    }

    private IEnumerable<FormTabPageViewModel> FormPages() =>
        new[] { _thongTriPage, _ksChiPage, _gdnttPage }.OfType<FormTabPageViewModel>();

    /// Tên đơn vị sửa trên MỘT biểu mẫu phải hiện ngay trên mọi biểu mẫu và ở Cài đặt (lưu cùng Ctrl+S).
    private void OnOrgEdited(object? sender, OrgEditedEventArgs e)
    {
        _orgInfo = _orgInfo.With(e.Key, e.Value);
        _orgDirty = true;
        _formDeclarationDirty = true;
        OnPropertyChanged(nameof(IsDirty));
        RefreshAggregateTabs();
    }

    /// Mở thẳng một tab biểu mẫu ("thongtri" | "kschi" | "gdntt") của dự án đang chọn. Chờ nạp cây xong rồi mới chọn.
    public async Task OpenFormTabAsync(string formId)
    {
        await _treeLoad;
        var page = formId switch
        {
            "thongtri" => _thongTriPage,
            "kschi" => _ksChiPage,
            "gdntt" => _gdnttPage,
            _ => null,
        };
        if (page is not null) SelectedViTriPage = page;
    }
```
4. `RefreshFormTabs`: bỏ ba dòng `_selectedProjectTree.ThongTri ??= …`; ba lời gọi thành
```csharp
        _thongTriPage.Inner.Refresh(rows, sources, _orgInfo, projectName);
        _ksChiPage.Inner.Refresh(rows, sources, _orgInfo, projectName);
        _gdnttPage.Inner.Refresh(rows, sources, _orgInfo, projectName);
```
5. `OnSelectedViTriPageChanged`: `or ThongTriFormPageViewModel or KsChiFormPageViewModel or GdnttFormPageViewModel` → `or FormTabPageViewModel`.
6. `SaveViTriTreeAsync`: bỏ ba dòng `ThongTri = …, KsChi = …, Gdntt = …` khỏi bộ khởi tạo `toSave`; sau vòng lặp `ledgerPages` thêm
```csharp
        foreach (var page in FormPages())
            foreach (var input in page.Inner.ToFormInputs())
                toSave.FormInputs.Add(input);
```
và trong `try`, TRƯỚC `await _service.ReplaceTreeAsync(toSave);` thêm
```csharp
            if (_orgDirty)
            {
                // Tên đơn vị lưu ở Cài đặt: đọc-sửa-ghi bản ghi công ty, chỉ đổi hai trường này.
                var info = await _companyInfo.GetAsync();
                await _companyInfo.SaveAsync(info with { DonViCapTren = _orgInfo.DonViCapTren, DonViCoSo = _orgInfo.DonViCoSo });
                _orgDirty = false;
            }
```
7. Dùng `using ConstructionFinance.Application.Forms;` đã có. Xoá `using` thừa nếu trình biên dịch báo.

- [ ] **Step 8: Sửa test workbook cũ và bổ sung test mới**

`FinanceWorkbookFormTabsTests.cs` — đổi kiểu và thuộc tính theo API mới (mỗi chỗ `OfType<ThongTriFormPageViewModel>().Single().Inner.X` → `FormPage(vm, "thongtri").Inner…`). Thêm helper ở đầu lớp:
```csharp
    internal static FormTabPageViewModel FormPage(FinanceWorkbookViewModel vm, string id) =>
        vm.ViTriPages.OfType<FormTabPageViewModel>().Single(p => p.Inner.Definition.Id == id);
```
và sửa từng test: `Editing_a_form_field…` dùng `.Inner.SetInput("SoThongTri", "12/TT")`; `SaveAll_persists_the_three_declarations_with_the_tree` đổi tên thành `SaveAll_persists_the_typed_text_as_FormInputs` và kỳ vọng `saved.FormInputs` chứa `("thongtri","SoThongTri","12/TT")`, `("kschi","ThueGTGT","2000000")`, `("gdntt","TaiNganHang","Vietcombank")`; `Stored_declarations_load_into_the_form_tabs` đổi thành `Stored_inputs_load_into_the_form_tabs` (cây có `FormInputs = { new FormInput { Form="thongtri", Key="SoThongTri", Value="9/TT" } }`, kiểm `Segment value`); `Form_tabs_see_the_dots_filtered_by_their_fund` dùng `.Inner.Picker.Items.Count`. Thêm các test mới:
```csharp
    [Fact]
    public async Task Editing_the_org_on_one_form_shows_on_the_others_and_is_saved_to_company_settings()
    {
        var (vm, service, companyInfo) = CreateSutWithCompanyInfo();
        service.Setup(s => s.ReplaceTreeAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        await vm.LoadAsync();

        FormPage(vm, "thongtri").Inner.SetInput("don_vi_co_so", "Ban mới");

        FormPage(vm, "gdntt").Inner.Sheet.Cells.Single(c => c.Row == 2 && c.Col == 1).DisplayText.ShouldBe("Ban mới");
        vm.IsDirty.ShouldBeTrue();
        await vm.SaveAllCommand.ExecuteAsync(null);
        companyInfo.Verify(c => c.SaveAsync(It.Is<CompanyInfoDto>(i => i.DonViCoSo == "Ban mới" && i.DonViCapTren == "Sở Tài chính"),
            It.IsAny<CancellationToken>()), Times.Once);
        vm.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public async Task OpenFormTabAsync_waits_for_the_tree_and_selects_the_tab()
    {
        var (vm, _) = CreateSut();
        await vm.LoadAsync();

        await vm.OpenFormTabAsync("kschi");

        vm.SelectedViTriPage.ShouldBeSameAs(FormPage(vm, "kschi"));
    }

    [Fact]
    public async Task A_new_project_gets_three_empty_form_tabs()
    {
        var (vm, _) = CreateSut();
        await vm.LoadAsync();

        vm.AddProjectCommand.Execute(null);

        vm.ViTriPages.OfType<FormTabPageViewModel>().Count().ShouldBe(3);
        FormPage(vm, "thongtri").Inner.ToFormInputs().ShouldBeEmpty();
    }
```
trong đó `CreateSutWithCompanyInfo` là biến thể của `CreateSut` trả thêm `Mock<ICompanyInfoService>` (tách `companyInfo` ra tham số). Sửa `PrintWorkbookTests.cs` theo cùng cách: thay `OfType<GdnttFormPageViewModel>()` → `FormPage(workbook, "gdntt")` (cùng helper, đặt `internal static` ở `FinanceWorkbookFormTabsTests` rồi gọi qua tên lớp); `NewGdntt/NewThongTri/NewKsChi` dựng bằng `new FormTabPageViewModel(new FormPageViewModel(FormDefinitions.X, Mock.Of<IFormExcelExporter>(), new FakeDialogService()))` + `Refresh(...)`; `(ThongTriPrintSheet)…BuildPrintSheet()` → `(FormTemplatePrintSheet)…`, và kiểm `.Sheet.SheetName`; `nameof(GdnttFormViewModel.CanExport)` → `nameof(FormPageViewModel.CanExport)`. Các test về hành vi nút In/PrintUnavailableReason giữ nguyên ý nghĩa, đổi cách dựng.

- [ ] **Step 9: Chạy cả bộ Desktop.Tests**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --nologo -v q 2>&1 | grep -E "Passed!|Failed!|error" | head`
Expected: `Passed!`. Mọi test của ba VM/Views cũ (`ThongTriFormViewModelTests`…) vẫn xanh vì lớp cũ còn nguyên tới Task 11.

- [ ] **Step 10: Commit**

```bash
git add src/ConstructionFinance.Desktop tests/ConstructionFinance.Desktop.Tests
git commit -m "feat(forms): FormPageViewModel chung cho ba tab, hộp thoại chọn đợt, chữ đã gõ lưu cùng Ctrl+S

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Màn hình — lưới y hệt tờ Excel, tab chỉ còn biểu mẫu

**Files:**
- Create: `src/ConstructionFinance.Desktop/Views/Finance/Forms/FormSheetControl.cs`
- Create: `src/ConstructionFinance.Desktop/Views/Finance/Forms/FormPageView.axaml` + `FormPageView.axaml.cs`
- Modify: `src/ConstructionFinance.Desktop/Views/Finance/Forms/FormDotPickerView.axaml` (rộng đủ, dùng làm hộp thoại)
- Test: `tests/ConstructionFinance.Desktop.Tests/Finance/Forms/FormSheetControlTests.cs`

**Interfaces:**
- Consumes: Task 8 (`FormPageViewModel`, `IFormSheetEditor`), Task 1 (`ResolvedSheet`…).
- Produces: `FormSheetControl.Attach(FormPageViewModel?)`; `FormPageView` (ViewLocator tự tìm cho `FormPageViewModel`). Hằng hình học `PixelsPerChar = 7`, `CellPaddingPixels = 5`, `PixelsPerPoint = 96/72` — cùng công thức với `ExcelFitScale` (px = w×7+5).

- [ ] **Step 1: Đọc kiểu có sẵn trước khi viết**

Mở `src/ConstructionFinance.Desktop/Styles/Theme.axaml` dòng ~185–205 (`TextBox.gridCell`) để biết màu `GridLineBrush`/`BrandPrimaryBrush`; control mới dùng màu cố định (xem mã) nên không phụ thuộc chúng, nhưng giữ class `gridCell` để ô nhập có cùng phản ứng focus với lưới Vị trí.

- [ ] **Step 2: Viết test headless (đỏ)**

```csharp file=tests/ConstructionFinance.Desktop.Tests/Finance/Forms/FormSheetControlTests.cs
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Desktop.ViewModels.Finance.Forms;
using ConstructionFinance.Desktop.Views.Finance.Forms;
using ConstructionFinance.Domain.Finance;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance.Forms;

public class FormSheetControlTests
{
    private static readonly FormOrgInfo Org = new("Sở Tài chính", "Ban QLDA số 1");

    private static FormDotRow Dot(FormDotKey key, Fund fund, decimal advance, string content, string package = "Gói A") =>
        new(key, "Vị trí 1", "CHI PHÍ XÂY DỰNG", package, "Cty XL A", "123456789",
            50_000_000_000m, 0m, new DateOnly(2026, 3, 1), content, "CT01", fund, advance, 0m, 0m, advance);

    private sealed record Host(Window Window, FormPageView View, FormPageViewModel Vm, FormSheetControl Sheet);

    private static Host Show(FormDefinition definition, bool select = true)
    {
        var vm = new FormPageViewModel(definition, Mock.Of<IFormExcelExporter>(), new FakeDialogService()) { Year = 2026 };
        vm.Refresh(new[]
        {
            Dot(new(0, 0, 0, 0), Fund.NSNN, 10_000_000m, "Tạm ứng lần 1"),
            Dot(new(0, 0, 0, 1), Fund.NgoaiHoi, 30_000_000m, "Ngoại hối 1"),
        }, Array.Empty<LedgerRowViewModel>(), Org, "Dự án A");
        if (select) foreach (var item in vm.Picker.Items) item.IsSelected = true;

        var view = new FormPageView { DataContext = vm };
        var window = new Window { Content = view, Width = 1100, Height = 1000 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return new Host(window, view, vm, view.FindControl<FormSheetControl>("SheetHost")!);
    }

    private static List<TextBox> Boxes(Host host) => host.Sheet.GetVisualDescendants().OfType<TextBox>().ToList();

    private static TextBox Box(Host host, string key) =>
        Boxes(host).Single(box => (string?)box.Tag == key);

    private static void Run(Action action) => HeadlessTestSession.Run(action);

    // ===== hình học =====

    [Theory]
    [InlineData("thongtri")]
    [InlineData("kschi")]
    [InlineData("gdntt")]
    public void Sheet_size_matches_the_template_column_widths_and_row_heights(string id)
    {
        Run(() =>
        {
            var host = Show(FormDefinitions.All.Single(d => d.Id == id));
            var sheet = host.Vm.Sheet;

            var expectedWidth = sheet.ColumnWidths.Sum(w => w * 7.0 + 5.0);
            var expectedHeight = sheet.RowHeights.Sum() * 96.0 / 72.0;
            var canvas = (Canvas)host.Sheet.Child!;

            canvas.Bounds.Width.ShouldBe(expectedWidth, 0.5);
            canvas.Bounds.Height.ShouldBe(expectedHeight, 0.5);
            host.Window.Close();
        });
    }

    [Fact]
    public void Sheet_is_left_aligned_not_stretched_into_a_half_empty_card()
    {
        Run(() =>
        {
            var host = Show(FormDefinitions.ThongTri);

            host.Sheet.Bounds.X.ShouldBeLessThan(40);
            host.Sheet.Bounds.Width.ShouldBeLessThan(900);   // 183mm ≈ 692px + lề giấy
            host.Window.Close();
        });
    }

    // ===== ô nào gõ được =====

    [Fact]
    public void Every_input_segment_has_one_text_box_and_computed_cells_have_none()
    {
        Run(() =>
        {
            var host = Show(FormDefinitions.ThongTri);

            var inputKeys = host.Vm.Sheet.Cells.SelectMany(c => c.Segments)
                .Where(s => s.Kind is SegmentKind.Input or SegmentKind.InputNumber or SegmentKind.SettingInput or SegmentKind.Overridable)
                .Select(s => s.Key!).OrderBy(k => k).ToList();

            Boxes(host).Select(b => (string)b.Tag!).OrderBy(k => k).ToList().ShouldBe(inputKeys);
            inputKeys.ShouldContain("don_vi_cap_tren");     // tên đơn vị trên cùng bên trái: sửa được
            inputKeys.ShouldNotContain("Total");
            host.Window.Close();
        });
    }

    [Fact]
    public void Org_name_cell_shows_the_current_value_and_is_editable()
    {
        Run(() =>
        {
            var host = Show(FormDefinitions.ThongTri);

            Box(host, "don_vi_cap_tren").Text.ShouldBe("Sở Tài chính");
            host.Window.Close();
        });
    }

    [Fact]
    public void Empty_input_shows_the_Mau_placeholder_as_a_dim_watermark()
    {
        Run(() =>
        {
            var host = Show(FormDefinitions.ThongTri);

            var box = Box(host, "SoThongTri");
            box.Text.ShouldBeNullOrEmpty();
            box.Watermark.ShouldBe(".............");
            host.Window.Close();
        });
    }

    // ===== gõ =====

    [Fact]
    public void Typing_into_a_cell_reaches_the_view_model_and_keeps_the_caret_box()
    {
        Run(() =>
        {
            var host = Show(FormDefinitions.ThongTri);
            var box = Box(host, "SoThongTri");

            box.Text = "12/TT";
            Dispatcher.UIThread.RunJobs();

            host.Vm.ToFormInputs().ShouldContain(i => i.Key == "SoThongTri" && i.Value == "12/TT");
            Box(host, "SoThongTri").ShouldBeSameAs(box);          // không dựng lại cả lưới giữa chừng gõ
            host.Window.Close();
        });
    }

    [Fact]
    public void Typing_a_garbage_number_does_not_throw_and_keeps_the_old_value()
    {
        Run(() =>
        {
            var host = Show(FormDefinitions.KsChi);
            var box = Box(host, "ThueGTGT");
            box.Text = "1.500.000";
            Dispatcher.UIThread.RunJobs();

            box.Text = "abc";
            Dispatcher.UIThread.RunJobs();

            host.Vm.Sheet.Cells.SelectMany(c => c.Segments).First(s => s.Key == "ThueGTGT").Number.ShouldBe(1_500_000m);
            host.Window.Close();
        });
    }

    // ===== tự tính =====

    [Fact]
    public void Computed_cells_update_in_place_when_the_selection_changes()
    {
        Run(() =>
        {
            // KS chi có cấu trúc cố định (không vùng lặp): đổi lựa chọn chỉ đổi chữ ô tự tính, không dựng lại ô nhập.
            var host = Show(FormDefinitions.KsChi, select: false);
            var boxesBefore = Boxes(host);

            host.Vm.Picker.Items[0].IsSelected = true;
            Dispatcher.UIThread.RunJobs();

            var texts = host.Sheet.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            texts.ShouldContain("10.000.000");
            Boxes(host).ShouldBe(boxesBefore, ignoreOrder: false);   // cùng các ô nhập (cùng đối tượng)
            host.Window.Close();
        });
    }

    [Fact]
    public void More_lines_than_the_Mau_region_rebuild_the_grid_with_extra_rows()
    {
        Run(() =>
        {
            var host = Show(FormDefinitions.ThongTri, select: false);
            var heightBefore = ((Canvas)host.Sheet.Child!).Height;
            var rows = Enumerable.Range(0, 14)
                .Select(i => Dot(new(0, 0, 0, i), Fund.NSNN, 1_000_000m, $"Đợt {i}")).ToList();
            host.Vm.Refresh(rows, Array.Empty<LedgerRowViewModel>(), Org, "Dự án A");

            foreach (var item in host.Vm.Picker.Items) item.IsSelected = true;
            Dispatcher.UIThread.RunJobs();

            ((Canvas)host.Sheet.Child!).Height.ShouldBeGreaterThan(heightBefore);
            host.Window.Close();
        });
    }

    // ===== gõ đè =====

    [Fact]
    public void Overridden_cell_shows_a_marker_and_a_reset_button_that_returns_to_automatic()
    {
        Run(() =>
        {
            var host = Show(FormDefinitions.KsChi);
            var box = Box(host, "GhiChu");
            box.Text.ShouldBe("Tạm ứng lần 1");

            box.Text = "Ghi chú của tôi";
            Dispatcher.UIThread.RunJobs();

            var reset = host.Sheet.GetVisualDescendants().OfType<Button>().Single(b => (string?)b.Tag == "GhiChu");
            reset.IsVisible.ShouldBeTrue();
            box.BorderThickness.Left.ShouldBeGreaterThan(0);

            host.Vm.ResetOverride("GhiChu");
            Dispatcher.UIThread.RunJobs();

            reset.IsVisible.ShouldBeFalse();
            Box(host, "GhiChu").Text.ShouldBe("Tạm ứng lần 1");
            host.Window.Close();
        });
    }

    // ===== GĐNTT bị chặn =====

    [Fact]
    public void Blocked_gdntt_still_draws_the_sheet_with_editable_cells_and_a_disabled_export()
    {
        Run(() =>
        {
            var host = Show(FormDefinitions.Gdntt, select: false);

            Boxes(host).ShouldNotBeEmpty();
            host.Vm.CanExport.ShouldBeFalse();
            var export = host.View.GetVisualDescendants().OfType<Button>().Single(b => (string?)b.Content == "Xuất Excel");
            export.IsEffectivelyEnabled.ShouldBeFalse();
            host.Window.Close();
        });
    }

    // ===== bảng đợt không còn nằm trong tab =====

    [Fact]
    public void The_dot_table_is_not_on_the_tab_only_the_choose_button_is()
    {
        Run(() =>
        {
            var host = Show(FormDefinitions.ThongTri);

            host.View.GetVisualDescendants().OfType<DataGrid>().ShouldBeEmpty();
            host.View.GetVisualDescendants().OfType<Button>()
                .Any(b => ((string?)b.Content)?.StartsWith("Chọn đợt (") == true).ShouldBeTrue();
            host.Window.Close();
        });
    }
}
```

- [ ] **Step 3: Viết control lưới**

```csharp file=src/ConstructionFinance.Desktop/Views/Finance/Forms/FormSheetControl.cs
using System.ComponentModel;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Desktop.ViewModels.Finance.Forms;

namespace ConstructionFinance.Desktop.Views.Finance.Forms;

/// Vẽ một ResolvedSheet thành lưới y hệt tờ Excel: cùng hàng, cột, ô gộp, bề rộng, chiều cao, cỡ chữ, đường kẻ.
/// Ô tự nhập là TextBox; ô tự tính là chữ trên nền xám nhạt; ô tự tính cho gõ đè là TextBox có dấu nhận biết
/// khi đã gõ đè và nút ↺ "Trở về tự động".
///
/// Hình học dùng cùng công thức với ExcelFitScale: bề rộng cột = w×7+5 px; hàng pt → px nhân 96/72.
///
/// Cấu trúc (hàng, cột, ô, loại đoạn) KHÔNG đổi giữa hai lần dựng thì chỉ cập nhật chữ tại chỗ — dựng lại cả lưới
/// mỗi lần gõ một phím sẽ làm mất con trỏ. Cấu trúc đổi (Thông tri thêm dòng đợt) thì dựng lại.
public sealed class FormSheetControl : Border
{
    internal const double PixelsPerChar = 7.0;
    internal const double CellPaddingPixels = 5.0;
    internal const double PixelsPerPoint = 96.0 / 72.0;

    private static readonly IBrush Ink = Brushes.Black;
    private static readonly IBrush ComputedFill = new SolidColorBrush(Color.Parse("#F2F2F2"));
    private static readonly IBrush OverriddenFill = new SolidColorBrush(Color.Parse("#FFF1D6"));
    private static readonly IBrush InputFill = new SolidColorBrush(Color.Parse("#EEF4FF"));
    private static readonly IBrush OverrideMarker = new SolidColorBrush(Color.Parse("#E08A00"));
    private static readonly IBrush PaperEdge = new SolidColorBrush(Color.Parse("#B0B0B0"));
    private static readonly FontFamily Serif = new("Times New Roman, Liberation Serif, Tinos, Serif");

    // Canvas chứ không Grid: Grid chia phần dư của ô gộp vào các cột pixel (đo được +2…+4px ở vài cột), làm tờ lệch Mẫu.
    // Mỗi ô đặt tại toạ độ tuyệt đối tính từ bề rộng/chiều cao Mẫu nên hình học không thể trôi.
    private readonly Canvas _canvas = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private double[] _columnX = Array.Empty<double>();
    private double[] _rowY = Array.Empty<double>();
    private readonly Dictionary<string, TextBox> _inputs = new();
    private readonly Dictionary<string, Button> _resets = new();
    private readonly Dictionary<(int Row, int Col), TextBlock> _texts = new();
    private readonly Dictionary<(int Row, int Col), Border> _fills = new();

    private FormPageViewModel? _vm;
    private string? _signature;
    private bool _updating;

    public FormSheetControl()
    {
        Background = Brushes.White;
        BorderBrush = PaperEdge;
        BorderThickness = new Thickness(1);
        Padding = new Thickness(24);
        // Bề rộng cột lẻ (8,43×7+5 = 64,01px) phải giữ đúng: làm tròn từng cột lên sẽ đẩy cả tờ lệch khỏi Mẫu vài px.
        UseLayoutRounding = false;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        Child = _canvas;
    }

    public void Attach(FormPageViewModel? vm)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = vm;
        if (vm is not null) vm.PropertyChanged += OnVmPropertyChanged;
        _signature = null;
        Render();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FormPageViewModel.Sheet)) Render();
    }

    private void Render()
    {
        var sheet = _vm?.Sheet;
        if (sheet is null)
        {
            _canvas.Children.Clear();
            _signature = null;
            return;
        }

        var signature = SignatureOf(sheet);
        if (signature == _signature) Update(sheet);
        else
        {
            Build(sheet);
            _signature = signature;
        }
    }

    // ===== dựng =====

    private static string SignatureOf(ResolvedSheet sheet)
    {
        var text = new StringBuilder();
        foreach (var width in sheet.ColumnWidths) text.Append(width).Append(',');
        text.Append('|');
        foreach (var height in sheet.RowHeights) text.Append(height).Append(',');
        foreach (var cell in sheet.Cells)
        {
            text.Append('|').Append(cell.Row).Append(',').Append(cell.Col).Append(',')
                .Append(cell.RowSpan).Append(',').Append(cell.ColSpan).Append(',').Append(cell.TextColSpan);
            foreach (var segment in cell.Segments) text.Append(';').Append((int)segment.Kind).Append(segment.Key);
        }
        return text.ToString();
    }

    private void Build(ResolvedSheet sheet)
    {
        _canvas.Children.Clear();
        _inputs.Clear();
        _resets.Clear();
        _texts.Clear();
        _fills.Clear();

        _columnX = new double[sheet.ColumnWidths.Count + 1];
        for (var i = 0; i < sheet.ColumnWidths.Count; i++)
            _columnX[i + 1] = _columnX[i] + sheet.ColumnWidths[i] * PixelsPerChar + CellPaddingPixels;
        _rowY = new double[sheet.RowHeights.Count + 1];
        for (var i = 0; i < sheet.RowHeights.Count; i++)
            _rowY[i + 1] = _rowY[i] + sheet.RowHeights[i] * PixelsPerPoint;
        _canvas.Width = _columnX[^1];
        _canvas.Height = _rowY[^1];

        // Mỗi ô kẻ cạnh trái/trên của chính nó; cạnh phải/dưới chỉ kẻ khi ô bên cạnh KHÔNG kẻ cạnh trái/trên
        // trùng chỗ — nếu không, hai ô liền nhau cùng kẻ làm đường dày gấp đôi so với Excel.
        var leftEdges = new HashSet<(int Row, int Col)>();
        var topEdges = new HashSet<(int Row, int Col)>();
        foreach (var cell in sheet.Cells)
        {
            if (cell.Style.Borders.Left != BorderLine.None)
                for (var row = cell.Row; row < cell.Row + cell.RowSpan; row++) leftEdges.Add((row, cell.Col));
            if (cell.Style.Borders.Top != BorderLine.None)
                for (var col = cell.Col; col < cell.Col + cell.ColSpan; col++) topEdges.Add((cell.Row, col));
        }

        foreach (var cell in sheet.Cells) AddFillAndBorders(cell, leftEdges, topEdges);
        foreach (var cell in sheet.Cells) AddContent(cell);   // thứ tự hàng-rồi-cột: Tab đi theo chiều đọc
    }

    private void AddFillAndBorders(ResolvedCell cell, HashSet<(int, int)> leftEdges, HashSet<(int, int)> topEdges)
    {
        var borders = cell.Style.Borders;
        var rightShared = Enumerable.Range(cell.Row, cell.RowSpan).All(row => leftEdges.Contains((row, cell.Col + cell.ColSpan)));
        var bottomShared = Enumerable.Range(cell.Col, cell.ColSpan).All(col => topEdges.Contains((cell.Row + cell.RowSpan, col)));

        var frame = new Border
        {
            BorderBrush = Ink,
            BorderThickness = new Thickness(
                Thickness(borders.Left),
                Thickness(borders.Top),
                rightShared ? 0 : Thickness(borders.Right),
                bottomShared ? 0 : Thickness(borders.Bottom)),
            IsHitTestVisible = false,
        };

        // Ô chữ tự tính tràn sang ô bên cạnh ("Bằng chữ…") không tô xám: nền xám chỉ phủ ô đầu sẽ cắt ngang chữ.
        if (cell.IsComputed && cell.TextColSpan <= cell.ColSpan)
        {
            frame.Background = cell.Segments.Any(s => s.IsOverridden) ? OverriddenFill : ComputedFill;
            _fills[(cell.Row, cell.Col)] = frame;
        }

        Place(frame, cell.Row, cell.Col, cell.RowSpan, cell.ColSpan);
    }

    private static double Thickness(BorderLine line) => line switch
    {
        BorderLine.Thin => 1.0,
        BorderLine.Hair => 0.8,
        _ => 0.0,
    };

    private void AddContent(ResolvedCell cell)
    {
        if (cell.Segments.Count == 0) return;

        Control content;
        if (!cell.IsEditable)
        {
            var block = NewText(cell, cell.DisplayText);
            block.HorizontalAlignment = HorizontalAlignment.Stretch;
            block.TextAlignment = TextAlignmentOf(cell);
            _texts[(cell.Row, cell.Col)] = block;
            content = block;
        }
        else
        {
            content = BuildStrip(cell);
        }

        content.VerticalAlignment = cell.Style.VAlign switch
        {
            CellVAlign.Top => VerticalAlignment.Top,
            CellVAlign.Center => VerticalAlignment.Center,
            _ => VerticalAlignment.Bottom,
        };

        // Chữ tràn: nội dung chiếm cả dải TextColSpan, còn đường kẻ ở lớp dưới vẫn chỉ trong ColSpan.
        var span = Math.Min(Math.Max(cell.ColSpan, cell.TextColSpan), _columnX.Length - cell.Col);
        content.HorizontalAlignment = content is TextBlock or TextBox ? HorizontalAlignment.Stretch : content.HorizontalAlignment;

        // Khung cố định bằng đúng dải ô: nội dung dài hơn bị cắt như Excel cắt chữ ở biên ô, và không thể đẩy ô khác.
        var host = new Grid { ClipToBounds = true };
        host.Children.Add(content);
        Place(host, cell.Row, cell.Col, cell.RowSpan, span);
    }

    private Control BuildStrip(ResolvedCell cell)
    {
        var segments = cell.Segments;
        var pixels = cell.Style.FontSize * PixelsPerPoint;
        var align = cell.Style.Align;

        // Một đoạn duy nhất và nó là chỗ điền: ô nhập trải kín ô.
        if (segments.Count == 1)
            return segments[0].Kind == SegmentKind.Overridable
                ? BuildOverridable(cell, segments[0], pixels)
                : BuildInput(cell, segments[0], pixels, fill: true);

        var centered = align is CellAlign.Center or CellAlign.Right;
        // Ô có chữ tràn sang ô bên cạnh (nhãn + chỗ điền ngắn như " + Mục …, tiểu mục …"): chỗ điền cuối chỉ rộng bằng
        // chữ chấm chấm, không trải hết dải tràn — nếu không, ô nhập phủ lên cả các cột số bên dưới.
        var overflowing = cell.TextColSpan > cell.ColSpan;
        var lastIsInput = segments[^1].Kind != SegmentKind.Fixed && !overflowing;

        if (centered)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = align == CellAlign.Center ? HorizontalAlignment.Center : HorizontalAlignment.Right,
            };
            foreach (var segment in segments) panel.Children.Add(StripPart(cell, segment, pixels, fill: false));
            return panel;
        }

        // Canh trái: nhãn đứng yên, chỗ điền cuối cùng chiếm phần còn lại của ô (đúng Excel — chữ nối đuôi nhãn).
        var dock = new DockPanel { LastChildFill = lastIsInput };
        for (var i = 0; i < segments.Count; i++)
        {
            var isLast = i == segments.Count - 1;
            var part = StripPart(cell, segments[i], pixels, fill: lastIsInput && isLast);
            DockPanel.SetDock(part, Dock.Left);
            dock.Children.Add(part);
        }
        return dock;
    }

    private Control StripPart(ResolvedCell cell, ResolvedSegment segment, double pixels, bool fill) =>
        segment.Kind switch
        {
            SegmentKind.Fixed or SegmentKind.Computed => NewText(cell, segment.Text),
            SegmentKind.Overridable => BuildOverridable(cell, segment, pixels),
            _ => BuildInput(cell, segment, pixels, fill),
        };

    private TextBlock NewText(ResolvedCell cell, string text) => new()
    {
        Text = text,
        FontFamily = Serif,
        FontSize = cell.Style.FontSize * PixelsPerPoint,
        FontWeight = cell.Style.Bold ? FontWeight.Bold : FontWeight.Normal,
        FontStyle = cell.Style.Italic ? FontStyle.Italic : FontStyle.Normal,
        TextDecorations = cell.Style.Underline ? TextDecorations.Underline : null,
        Foreground = Ink,
        TextWrapping = cell.Style.Wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(2, 0),
        IsHitTestVisible = false,
    };

    private TextBox BuildInput(ResolvedCell cell, ResolvedSegment segment, double pixels, bool fill)
    {
        var box = new TextBox
        {
            Tag = segment.Key,
            Text = segment.Value,
            Watermark = segment.Hint,
            FontFamily = Serif,
            FontSize = pixels,
            FontWeight = cell.Style.Bold ? FontWeight.Bold : FontWeight.Normal,
            FontStyle = cell.Style.Italic ? FontStyle.Italic : FontStyle.Normal,
            Foreground = Ink,
            Background = InputFill,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(2, 0),
            MinHeight = 0,
            VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = fill ? HorizontalAlignment.Stretch : HorizontalAlignment.Left,
            MinWidth = fill ? 0 : EstimateWidth(segment, pixels),
            TextAlignment = segment.Kind == SegmentKind.InputNumber ? TextAlignment.Right : InputAlignmentOf(cell),
            AcceptsReturn = false,
        };
        box.Classes.Add("gridCell");
        box.BorderThickness = new Thickness(0);
        box.Background = InputFill;

        var key = segment.Key!;
        box.TextChanged += (_, _) =>
        {
            if (!_updating) _vm?.SetInput(key, box.Text);
        };
        // Rời ô: chữ số được định dạng lại có dấu chấm phân nghìn (lúc đang gõ không đụng tới để khỏi nhảy con trỏ).
        box.LostFocus += (_, _) =>
        {
            if (_vm?.Sheet is { } current) Update(current);
        };

        _inputs[key] = box;
        return box;
    }

    private Control BuildOverridable(ResolvedCell cell, ResolvedSegment segment, double pixels)
    {
        var box = BuildInput(cell, segment, pixels, fill: true);
        box.TextAlignment = segment.Number is not null ? TextAlignment.Right : InputAlignmentOf(cell);

        var reset = new Button
        {
            Tag = segment.Key,
            Content = "↺",
            Padding = new Thickness(4, 0),
            MinHeight = 0,
            MinWidth = 0,
            FontSize = pixels * 0.9,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = segment.IsOverridden,
        };
        ToolTip.SetTip(reset, "Trở về tự động");
        var key = segment.Key!;
        reset.Click += (_, _) => _vm?.ResetOverride(key);
        _resets[key] = reset;

        var dock = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(reset, Dock.Right);
        dock.Children.Add(reset);
        dock.Children.Add(box);
        return dock;
    }

    /// Chấm chấm và dấu … hẹp hơn chữ thường nhiều; ước lượng bề rộng ô nhập theo từng loại ký tự.
    private static double EstimateWidth(ResolvedSegment segment, double pixels)
    {
        var sample = segment.Text.Length >= (segment.Hint?.Length ?? 0) ? segment.Text : segment.Hint ?? "";
        var width = sample.Sum(ch => ch is '.' or '…' or ' ' ? 0.3 : 0.55) * pixels;
        return Math.Max(40, width);
    }

    /// Ô nhập một dòng: canh đều hai bên (Justify) của ô nhiều dòng biến chữ mờ thành những khoảng trắng lớn.
    private static TextAlignment InputAlignmentOf(ResolvedCell cell) =>
        cell.Style.Align == CellAlign.Justify ? TextAlignment.Left : TextAlignmentOf(cell);

    private static TextAlignment TextAlignmentOf(ResolvedCell cell) => cell.Style.Align switch
    {
        CellAlign.Center => TextAlignment.Center,
        CellAlign.Right => TextAlignment.Right,
        CellAlign.Justify => TextAlignment.Justify,
        CellAlign.Left => TextAlignment.Left,
        _ => cell.Segments.Any(s => s.Number is not null) ? TextAlignment.Right : TextAlignment.Left,
    };

    private void Place(Control element, int row, int col, int rowSpan, int colSpan)
    {
        var lastColumn = Math.Min(col - 1 + colSpan, _columnX.Length - 1);
        var lastRow = Math.Min(row - 1 + rowSpan, _rowY.Length - 1);
        element.Width = _columnX[lastColumn] - _columnX[col - 1];
        element.Height = _rowY[lastRow] - _rowY[row - 1];
        Canvas.SetLeft(element, _columnX[col - 1]);
        Canvas.SetTop(element, _rowY[row - 1]);
        _canvas.Children.Add(element);
    }

    // ===== cập nhật tại chỗ =====

    private void Update(ResolvedSheet sheet)
    {
        _updating = true;
        try
        {
            foreach (var cell in sheet.Cells)
            {
                if (_texts.TryGetValue((cell.Row, cell.Col), out var block) && block.Text != cell.DisplayText)
                    block.Text = cell.DisplayText;
                if (_fills.TryGetValue((cell.Row, cell.Col), out var fill))
                    fill.Background = cell.Segments.Any(s => s.IsOverridden) ? OverriddenFill : ComputedFill;

                foreach (var segment in cell.Segments)
                {
                    if (segment.Key is null) continue;
                    if (_inputs.TryGetValue(segment.Key, out var box))
                    {
                        // Ô đang có con trỏ giữ nguyên chữ người dùng đang gõ; rời ô mới chuẩn hoá.
                        if (!box.IsFocused && box.Text != segment.Value) box.Text = segment.Value;
                        box.Watermark = segment.Hint;
                        box.BorderBrush = OverrideMarker;
                        box.BorderThickness = new Thickness(segment.IsOverridden ? 3 : 0, 0, 0, 0);
                    }
                    if (_resets.TryGetValue(segment.Key, out var reset)) reset.IsVisible = segment.IsOverridden;
                }
            }
        }
        finally { _updating = false; }
    }
}
```
Lưu ý: thuộc tính `Tag` của `Button` đặt khoá (test tìm nút ↺ theo `Tag`); nút "Xuất Excel" trong `FormPageView.axaml` KHÔNG có `Tag` nên không lẫn.

- [ ] **Step 4: Viết view của tab**

```xml file=src/ConstructionFinance.Desktop/Views/Finance/Forms/FormPageView.axaml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ConstructionFinance.Desktop.ViewModels.Finance.Forms"
             xmlns:v="using:ConstructionFinance.Desktop.Views.Finance.Forms"
             x:Class="ConstructionFinance.Desktop.Views.Finance.Forms.FormPageView"
             x:DataType="vm:FormPageViewModel">
    <DockPanel>
        <Border DockPanel.Dock="Top" Classes="toolbarStrip" Margin="0,0,0,6">
            <StackPanel Orientation="Horizontal">
                <Button Classes="toolbar" Command="{Binding ChooseDotsCommand}"
                        Content="{Binding ChooseDotsText}" Margin="0,0,8,0"/>
                <Button Classes="toolbar accent" Command="{Binding ExportCommand}"
                        Content="Xuất Excel" Margin="0,0,12,0"/>
                <TextBlock Text="{Binding StatusText}" Classes="muted" VerticalAlignment="Center"
                           IsVisible="{Binding !HasError}"/>
                <TextBlock Text="{Binding StatusText}" Foreground="#B00020" FontWeight="SemiBold"
                           VerticalAlignment="Center" IsVisible="{Binding HasError}"/>
            </StackPanel>
        </Border>

        <ScrollViewer HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Auto" Background="#E7E7E7">
            <v:FormSheetControl x:Name="SheetHost" Margin="12"/>
        </ScrollViewer>
    </DockPanel>
</UserControl>
```

```csharp file=src/ConstructionFinance.Desktop/Views/Finance/Forms/FormPageView.axaml.cs
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ConstructionFinance.Desktop.ViewModels.Finance.Forms;

namespace ConstructionFinance.Desktop.Views.Finance.Forms;

public partial class FormPageView : UserControl
{
    public FormPageView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += (_, _) =>
            this.FindControl<FormSheetControl>("SheetHost")!.Attach(DataContext as FormPageViewModel);
    }
}
```
`x:DataType` + `{Binding !HasError}`: nếu trình biên dịch binding của Avalonia phàn nàn về toán tử `!`, thay bằng hai thuộc tính `ShowStatus`/`HasError` trong VM — đừng bỏ `x:DataType`.

- [ ] **Step 5: Hộp thoại chọn đợt rộng đủ**

Sửa `FormDotPickerView.axaml`: thêm `Width="1120"` vào `<UserControl>`; đổi `Height="180"` của DataGrid thành `Height="360"`; đổi bề rộng cột: Chọn 55, Ngày 105, Vị trí 100, Gói thầu 110, Nội dung 200, Nguồn 130, bốn cột tiền 115 mỗi cột (tổng 1160 — cột Nội dung dùng `Width="*"` để co giãn thay vì 200 cố định, nên không vượt 1120). KHÔNG đổi ràng buộc (binding) hay tên thuộc tính. (Giờ chỉ còn dùng làm hộp thoại — tab không còn nhúng nó nữa; các view cũ vẫn nhúng cho tới Task 11 nên chiều rộng cố định này chỉ ảnh hưởng chúng tạm thời.)

- [ ] **Step 6: Chạy test headless**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --nologo -v q --filter FormSheetControlTests 2>&1 | tail -30`
Expected: xanh. Hay lỗi gặp: (a) `Tag` của `TextBox` bị `TextBox` template nuốt — dùng `box.Tag` như trên là hợp lệ; (b) `box.IsFocused` luôn false trong headless nên `Update` ghi đè chữ đang gõ — test `Typing_into_a_cell…` bắt lỗi này nếu có vòng phản hồi (guard `_updating`); (c) `FindControl<FormSheetControl>` null → kiểm `x:Name`. Nếu số đo `grid.Bounds` lệch ±1px do làm tròn layout, nới dung sai test lên 2.0 — KHÔNG đổi công thức hình học.

- [ ] **Step 7: Chụp màn hình thật để so với Mẫu (xác minh bằng mắt)**

Làm theo "RUN THE APP for review" trong bộ nhớ dự án — launch bằng `setsid dotnet <dll>` (KHÔNG `dotnet run`), `CONSTRUCTIONFINANCE_DB=<tệp tạm>`, `DISPLAY=:1`, đưa cửa sổ lên trước bằng `_NET_WM_STATE_ABOVE`, chụp bằng `mss`. Gieo dữ liệu vào tệp tạm bằng `sqlite3` (tiền lưu TEXT, ngày ISO TEXT, `Fund` 0 = NSNN / 1 = NgoạiHối): 1 dự án, 2 vị trí, vài đợt cả hai nguồn. Chụp ba tab Thông tri / KS chi / GĐNTT; đặt cạnh số đo trong dump Mẫu (`scratchpad/dump_*.txt`). Lưu ảnh ở `.superpowers/sdd/2026-10-15-bieu-mau-dung-mau/` (không commit). Kiểm bằng mắt: (1) ba tab là MỘT tờ giấy trắng, canh trái, không còn bảng đợt chồng lên; (2) đường kẻ và ô gộp khớp Mẫu; (3) tên đơn vị góc trên bên trái gõ được; (4) ô xám khoá, ô xanh nhạt gõ được; (5) chữ không bị cắt ở cột số.

- [ ] **Step 8: Commit**

```bash
git add src/ConstructionFinance.Desktop tests/ConstructionFinance.Desktop.Tests
git commit -m "feat(forms): lưới biểu mẫu y hệt tờ Excel — ô tự nhập gõ được, ô tự tính khoá, gõ đè có nút trở về tự động

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Mục "Thông tri" ở khung bên trái mở thẳng tab Thông tri

**Files:**
- Modify: `src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs` (`case "ThongTri"`)
- Test: `tests/ConstructionFinance.Desktop.Tests/MainWindowNavigationTests.cs` (mới)

**Interfaces:**
- Consumes: Task 8 (`FinanceWorkbookViewModel.OpenFormTabAsync`).
- Produces: nav tag `ThongTri` → workbook + tab Thông tri. Màn cũ `ThongTriViewModel` không còn lối vào (mã xoá ở 7E).

- [ ] **Step 1: Viết test (đỏ)**

```csharp file=tests/ConstructionFinance.Desktop.Tests/MainWindowNavigationTests.cs
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Desktop.ViewModels.Finance.Forms;
using ConstructionFinance.Domain.Finance;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests;

public class MainWindowNavigationTests
{
    private static (MainWindowViewModel Main, FakeNavigationService Nav) Create()
    {
        var project = new Project { Id = 7, Name = "Dự án A", ViTris = { new ViTri { Name = "Vị trí 1" } } };
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.GetAllSummariesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<FinanceProjectSummary>());
        service.Setup(s => s.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Project> { new() { Id = 7, Name = "Dự án A" } });
        service.Setup(s => s.GetTreeAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(project);
        var companyInfo = new Mock<ICompanyInfoService>();
        companyInfo.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyInfoDto(null, null, null, null, null, null, null, null));

        var services = new ServiceCollection();
        services.AddTransient(_ => new FinanceWorkbookViewModel(
            service.Object,
            new SummaryPageViewModel(new FinanceSummaryViewModel(
                service.Object, Mock.Of<IFinanceImportService>(), Mock.Of<IFinanceExportService>(), Mock.Of<IDialogService>())),
            Mock.Of<IDialogService>(), new ServiceCollection().BuildServiceProvider(),
            Mock.Of<IFormExcelExporter>(), companyInfo.Object));
        var nav = new FakeNavigationService();
        return (new MainWindowViewModel(services.BuildServiceProvider(), nav), nav);
    }

    [Fact]
    public async Task ThongTri_item_opens_the_workbook_on_the_ThongTri_tab()
    {
        var (main, nav) = Create();

        await main.NavigateToAsync("ThongTri");

        var workbook = nav.CurrentContent.ShouldBeOfType<FinanceWorkbookViewModel>();
        workbook.SelectedViTriPage.ShouldBeOfType<FormTabPageViewModel>()
            .Inner.Definition.Id.ShouldBe("thongtri");
    }

    [Fact]
    public async Task ThongTri_item_while_already_on_the_workbook_only_switches_the_tab()
    {
        var (main, nav) = Create();
        await main.NavigateToAsync("FinanceWorkbook");
        var workbook = nav.CurrentContent.ShouldBeOfType<FinanceWorkbookViewModel>();
        await workbook.OpenFormTabAsync("kschi");

        await main.NavigateToAsync("ThongTri");

        nav.CurrentContent.ShouldBeSameAs(workbook);
        workbook.SelectedViTriPage.ShouldBeOfType<FormTabPageViewModel>().Inner.Definition.Id.ShouldBe("thongtri");
    }
}
```
(Nếu `FakeNavigationService` không có `CurrentContent` hoặc `Reset/NavigateTo` kiểu khác, xem `tests/…/Common/FakeNavigationService.cs` và dùng đúng thành viên có sẵn.)

- [ ] **Step 2: Chạy test, thấy đỏ.**

- [ ] **Step 3: Sửa `MainWindowViewModel.NavigateToAsync`**

Thay khối `case "ThongTri": …` bằng:
```csharp
            case "ThongTri":
                // Màn Thông tri cũ (từ dữ liệu import) không còn lối vào: mục này mở tab Thông tri của dự án đang
                // chọn trong sổ. Đang ở sổ rồi thì chỉ chuyển tab, không nạp lại (giữ dự án và chữ chưa lưu).
                if (_nav.CurrentContent is not ViewModels.Finance.FinanceWorkbookViewModel)
                    await NavigateToAsync("FinanceWorkbook");
                if (_nav.CurrentContent is ViewModels.Finance.FinanceWorkbookViewModel workbook)
                    await workbook.OpenFormTabAsync("thongtri");
                break;
```
Bỏ `using ConstructionFinance.Desktop.ViewModels.ThongTri;` ở đầu tệp nếu không còn dùng (`ThongTriViewModel` vẫn được đăng ký DI ở `HostBuilder`; đừng gỡ — xoá ở 7E).

- [ ] **Step 4: Chạy test, thấy xanh; chạy cả Desktop.Tests**

Run: `dotnet test tests/ConstructionFinance.Desktop.Tests --nologo -v q 2>&1 | grep -E "Passed!|Failed!"`

- [ ] **Step 5: Commit**

```bash
git add src/ConstructionFinance.Desktop/ViewModels/MainWindowViewModel.cs tests/ConstructionFinance.Desktop.Tests/MainWindowNavigationTests.cs
git commit -m "feat(forms): mục Thông tri ở khung trái mở thẳng tab Thông tri của sổ

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Chuyển hẳn sang bản gốc mới, xoá ba bản chép cũ, đo lại khổ in, nghiệm thu

**Files:**
- Delete (mã): `Forms/ThongTriFormViewModel.cs`, `KsChiFormViewModel.cs`, `GdnttFormViewModel.cs`, `FormPageViewModels.cs`, `FormLineRow.cs` (Desktop `ViewModels/Finance/Forms/`); `ThongTriFormView.axaml(.cs)`, `KsChiFormView.axaml(.cs)`, `GdnttFormView.axaml(.cs)` (Desktop `Views/Finance/Forms/`); `Printing/MigraDocFormSheets.cs`, `Printing/FormGrid.cs` (Application)
- Delete (test): `Finance/Forms/{ThongTri,KsChi,Gdntt}FormViewModelTests.cs`, `Application.Tests/Printing/{FormGridTests,MigraDocFormSheetTests,PrintFormSheetTests}.cs` (và mọi thứ chỉ phục vụ chúng)
- Modify: `Application/Forms/FormExcelExporter.cs`, `FormExcelExporter.KsChiGdntt.cs` (xoá hàm vẽ cũ), `Services/Interfaces/IFormExcelExporter.cs`, `Printing/PrintModels.cs`, `Printing/PrintWorkbookWriter.cs`, `Printing/MigraDocBuilder.cs`, `Printing/PrintPageProfile.cs`, `Domain/Finance/Project.cs`, `Domain/Finance/{ThongTri,KsChi,Gdntt}Declaration.cs`, `Data/Configurations/Finance/FinanceProjectConfiguration.cs`
- Docs: `docs/superpowers/specs/2026-10-15-bieu-mau-dung-mau-design.md` (trạng thái + §5.8 các chỗ lệch), `docs/superpowers/specs/2026-10-01-7e-teardown-design.md` (kiểm đã có ghi chú)

**Interfaces:** không thêm; chỉ xoá và đổi hồ sơ in.

- [ ] **Step 1: Xoá mã cũ theo thứ tự "dưới lên" để mỗi bước biên dịch được**

1. Xoá ba View + ba ViewModel + `FormPageViewModels.cs` + `FormLineRow.cs` + các test của chúng (danh sách ở "Files").
2. `PrintModels.cs`: xoá `PrintFormSheet`, `ThongTriPrintSheet`, `KsChiPrintSheet`, `GdnttPrintSheet` (và chú thích của chúng); `PrintWorkbookWriter.cs`: xoá `case PrintFormSheet` và hàm `WriteForm`; `MigraDocBuilder.cs`: xoá ba `case …PrintSheet`, nhánh `PrintFormSheet` trong `FontScaleOf`; xoá `MigraDocFormSheets.cs`, `FormGrid.cs`, `FormGridTests`, `MigraDocFormSheetTests`, `PrintFormSheetTests`. Test nào trong các tệp bị xoá đang ghim một hành vi VẪN CÒN ĐÚNG với bản mới (chân trang nằm gọn dưới lề dưới, in từng biểu mẫu một trang, hướng giấy) thì ĐÃ có bản tương đương ở `MigraDocFormRendererTests` — đối chiếu từng test trước khi xoá và thêm vào `MigraDocFormRendererTests` bất cứ ý nào chưa có (đặc biệt `Chan_trang_nam_gon_duoi_le_duoi` cho ba biểu mẫu: tạo `FormTemplatePrintSheet` từ `FormDefinitions.X.Build(...)`).
3. `FormExcelExporter.cs`/`.KsChiGdntt.cs`: xoá `ExportThongTriAsync/KsChiAsync/GdnttAsync`, `WriteThongTri/KsChi/Gdntt`, `WriteThongTriSheet/KsChiSheet/GdnttSheet`, `WriteAmountRow`, `SetMoneyMerged`, `Blank`; giữ `MoneyFormat`? (xoá nếu không còn ai dùng), `FontName`, `FontSize`, `SetMoney` (nếu `PrintWorkbookWriter` còn dùng), `ApplyPaper`, `ApplyPageDefaults`. Xoá `FormExcelExporter.KsChiGdntt.cs` nếu rỗng. `IFormExcelExporter`: chỉ còn `ExportAsync(ResolvedSheet…)`. Xoá các test `FormExcelExporter*Tests` cũ (`FormExcelExporterThongTriTests`, `FormExcelExporterKsChiGdnttTests`) — hành vi nay do `FormExcelExporterTemplateTests` + `TemplateMatchesMauTests` giữ.
4. Domain: xoá `Project.ThongTri/KsChi/Gdntt`; `FinanceProjectConfiguration`: xoá ba `Ignore`. Ba lớp `*Declaration`: `GdnttDeclaration` vẫn được `GdnttDefinition`/`GdnttBuilder` dùng, `ThongTriDeclaration`/`KsChiDeclaration` vẫn nằm trong chữ ký của `ThongTriBuilder`/`KsChiBuilder` (cố ý không đổi). Gỡ `: BaseEntity`, `ProjectId`, `Project` khỏi cả ba (thành lớp thuần) và sửa chú thích đầu lớp: "Gói tham số đưa vào builder; KHÔNG phải thực thể lưu — chữ tự nhập lưu ở FormInput." Sửa test builder/Domain nào đặt `ProjectId`/`Id` trên chúng (biên dịch báo).
5. `FormDotPickerView.axaml`: bỏ chú thích "vẫn nhúng"; ghi rõ "hộp thoại".

- [ ] **Step 2: Đo lại hồ sơ in (spec §5.4, rủi ro §7)**

Trong `PrintPageProfile.cs`:
```csharp
    // Ba biểu mẫu đều A4 DỌC như Mẫu.xlsx. Thông tri dài tuỳ số đợt nên chỉ ép vừa bề ngang (nhiều trang được);
    // KS chi và GĐNTT có chiều cao cố định theo Mẫu nên ép luôn vừa MỘT trang — một giấy đề nghị tràn sang trang
    // hai là giấy không ký được.
    public static PrintPageProfile ThongTri { get; } = new(PrintOrientation.Portrait, 1, 0, 0, 0);
    public static PrintPageProfile KsChi { get; } = new(PrintOrientation.Portrait, 1, 1, 0, 0);
    public static PrintPageProfile Gdntt { get; } = new(PrintOrientation.Portrait, 1, 1, 0, 0);
```
và sửa chú thích dài phía trên chúng (nói về "Excel tự thu nhỏ xuống 10%") cho đúng: Thông tri không ép chiều dọc vì số dòng không cố định; hai tờ kia ép vì chiều cao cố định. Sửa mọi test hồ sơ (`PrintPageProfiles…Tests`, `PrintWorkbookWriterTests`) ghim `Gdntt` ngang hoặc `FitToPagesTall == 0` cho KS chi/GĐNTT. Chạy `MigraDocFormRendererTests` — `Gdntt_fits_one_portrait_page_even_with_long_values` là phép đo lại đã nêu ở spec: nó đỏ nếu GĐNTT dọc không vừa một trang. Cập nhật `docs/superpowers/specs/2026-10-07-in-thang-ra-may-in-design.md` §12: thêm một mục ngắn "2026-10-15: GĐNTT chuyển sang khổ DỌC theo Mẫu; các số đo khổ ngang (dòng 6–16 xuống dòng, lề 0,4″) bị thay bởi phép ép một trang theo chiều dọc của MigraDocFormRenderer — xem plan 2026-10-15".

- [ ] **Step 3: Xây cả hai đích với cảnh báo thành lỗi**

Run:
```bash
dotnet build ConstructionFinance.sln --nologo -v q -warnaserror 2>&1 | grep -E "error|Warn|Build succeeded" | head
dotnet build src/ConstructionFinance.Desktop -f net8.0-windows --nologo -v q -warnaserror 2>&1 | grep -E "error|Build succeeded" | head
```
Expected: `Build succeeded` ở cả hai, 0 cảnh báo. (Nếu máy này không dựng được `net8.0-windows` offline, ghi rõ trong báo cáo cuối thay vì bỏ qua.)

- [ ] **Step 4: Kiểm tính năng In vẫn sống trong bản .exe (bài học `NET8_0_WINDOWS`)**

Run:
```bash
dotnet build src/ConstructionFinance.Desktop -f net8.0-windows -c Release --nologo -v q 2>&1 | tail -2
strings -e l src/ConstructionFinance.Desktop/bin/Release/net8.0-windows/ConstructionFinance.dll 2>/dev/null | grep -c WindowsPrinterPort
strings src/ConstructionFinance.Desktop/bin/Release/net8.0-windows/ConstructionFinance.dll | grep -c WindowsPrinterPort
```
Expected: số đếm ≥ 1 ở đích Windows (bài học 2026-10-07: tính năng từng nằm chết trong .exe mà không gì báo). Plan này không thêm `#if` nên số phải giữ nguyên như trước plan.

- [ ] **Step 5: Chạy TOÀN BỘ bộ test**

Run: `dotnet test ConstructionFinance.sln --nologo -v q 2>&1 | grep -E "Passed!|Failed!"`
Expected: tất cả `Passed!`, không `Failed!`. Tổng số test KHÁC 1019 (đã xoá nhiều test cũ, thêm nhiều test mới) — ghi cả hai con số vào báo cáo cuối. Chạy THÊM bộ Application.Tests ba lần liền để chắc test ClosedXML không chớp (`for i in 1 2 3; do dotnet test tests/ConstructionFinance.Application.Tests --nologo -v q --no-build | grep -E "Passed!|Failed!"; done`).

- [ ] **Step 6: Nghiệm thu bằng mắt trên app thật với DB tạm**

Lặp lại Step 7 của Task 9 trên bản đã xoá mã cũ; thêm: (a) gõ vào tên đơn vị ở Thông tri, mở GĐNTT thấy đổi theo, Ctrl+S, mở Cài đặt thấy đổi; (b) chọn đợt qua hộp thoại, Hủy không đổi lựa chọn; (c) GĐNTT chọn hai gói thấy dòng đỏ và nút Xuất mờ; (d) bấm mục "Thông tri" khung trái từ màn khác mở đúng tab; (e) Xuất Excel ra một tệp, mở bằng ClosedXML/LibreOffice-nếu-có so với Mẫu. Ghi vào báo cáo cuối những gì CHƯA kiểm được (in thử ra giấy thật trên Windows vẫn treo từ 2026-10-07; chưa ai chạy app trên Windows).

- [ ] **Step 7: Cập nhật tài liệu**

- Spec `2026-10-15-bieu-mau-dung-mau-design.md`: đổi dòng "Trạng thái" thành "đã duyệt 2026-10-15, đã triển khai theo plan 2026-10-15-bieu-mau-dung-mau.md"; thêm §5.8 "Chỗ lệch có chủ ý so với Mẫu" chép chín mục ở "Chỗ plan chốt thêm so với spec" của plan.
- Spec 7E (`2026-10-01-7e-teardown-design.md`): xác nhận đầu tệp đã ghi "quyết định 1 đổi: màn Thông tri cũ không còn lối vào" (spec 2026-10-15 §5.7 đã nhắc); nếu chưa có thì thêm; thêm vào danh sách xoá ở 7E: `ThongTriViewModel` + `ThongTriView` + đăng ký DI + ba bảng `import_batches`/`project_summaries`/`settlement_by_years`.
- Bộ nhớ dự án (`project_new_workbook_model.md`): thêm một khối "2026-10-15: ba biểu mẫu đúng Mẫu — XONG" ghi kiến trúc (bản gốc → bộ trộn → ba bộ vẽ), bài học (Mẫu có định dạng số làm "10" hiện "010"; hair border; underline; NFD; ClosedXML đọc/openpyxl cho hai chiều kiểm độc lập; Avalonia FormSheetControl cập nhật tại chỗ để không mất con trỏ), các chỗ lệch có chủ ý, và việc còn lại (7E; KH vốn/Vốn/QĐ; in thử thật trên Windows). Cập nhật dòng chỉ mục trong `MEMORY.md`.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "refactor(forms): bỏ ba bản chép tay cũ của biểu mẫu, khổ in GĐNTT chuyển sang dọc, đo lại một trang

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Self-Review

**1. Spec coverage** — mỗi mục spec và task sở hữu:
- §1/§3 nguyên tắc và quyết định: Task 2–9 (lưới y hệt, một bản gốc, hộp thoại riêng, tên đơn vị dùng chung, năm hiện tại, `Hải phòng` sửa được, ghi chú không in, bảng tối thiểu 9 dòng, nav trỏ sang tab).
- §4 phạm vi / giữ nguyên: Task 4 (builder không đổi), Task 11 Step 1 (không đụng 7E ngoài ghi chú).
- §5.1 bản gốc: Task 1–2. §5.2 năm loại ô + tách nhãn/ô tự nhập + "số đã tự tính thì vẫn tự tính": Task 2 (chú giải) + Task 3–4.
- §5.3 lưu chữ + migration + ô trống in chấm chấm: Task 7, Task 3 (`Empty_input_prints_the_Mau_placeholder…`), Task 5.
- §5.4 ba bộ vẽ + GĐNTT dọc + đo lại: Task 5, 6, 11 Step 2.
- §5.5 hộp thoại chọn đợt (Hủy khôi phục, lọc theo nguồn): Task 8 (`ChooseDots_cancel_restores…`), Task 9 Step 5.
- §5.6 bảng Thông tri tối thiểu 9 dòng: Task 3 (vùng lặp). §5.7 nav: Task 10.
- §6 kiểm chứng 1–5: so Mẫu (Task 2), ba đầu ra là một (Task 6 `Printed_text_matches…`), số liệu không đổi (Task 4 chạy builder tests), thao tác màn hình (Task 8–9), migration (Task 7).
- §7 rủi ro: sao lưu (Task 7 Step 10), đo lại khổ dọc (Task 6 + 11), viết lại test cũ không nới (Task 11 Step 1 yêu cầu đối chiếu từng test).
Khoảng trống đã biết và ghi rõ: KS chi C8:E12 không là ô nhập (plan §chốt thêm #7); lề giấy Mẫu 0,45/0 không theo (#5).

**2. Placeholder scan** — không còn "TBD/TODO"; các chỗ "xem tệp rồi dùng đúng thành viên có sẵn" (Task 8 Step 4 `FileNames.Sanitize`, Task 8 Step 5 `OnPropertyChanged`, Task 10 `FakeNavigationService`) là chỗ plan KHÔNG thể biết chắc chữ ký từ ngoài và đã nêu cách xử lý cụ thể.

**3. Nhất quán kiểu** — `FormBuildResult.CanOutput` (Task 4) khác `FormPageViewModel.CanExport` (Task 8) có chủ ý (kết quả dựng vs thuộc tính VM); `PrintBlockReason` có ở cả hai; `Tpl.Cell/Merged` khớp với script (Task 2); khoá `don_vi_cap_tren`/`don_vi_co_so` khớp `FormOrgKeys` (Task 1) và script; `FormTemplatePrintSheet(SheetName, Page, Sheet)` khớp ở Task 5/6/8; `FormTabPageViewModel.Inner.Definition.Id` khớp test Task 8–10.

**4. Review Focus** — năm dòng đều có test được nêu tên ở Task 3, 4, 5, 7, 8 (xem mục Review Focus đầu plan).

---

## Execution Handoff

Plan đã lưu ở `docs/superpowers/plans/2026-10-15-bieu-mau-dung-mau.md`. Người dùng đã yêu cầu "viết plan rồi thực thi", nên thực thi ngay trong phiên này bằng **superpowers:executing-plans** (tự làm từng task, không spawn agent con — khung làm việc của phiên này chỉ cho phép spawn khi được yêu cầu rõ), kết thúc bằng một lượt review toàn nhánh.
