# In qua phần mềm — Thiết kế

**Ngày:** 2026-09-10 · **Trạng thái:** chờ user duyệt

## 1. Mục tiêu

Người dùng bấm **một nút In duy nhất, luôn ở cùng một chỗ**, chọn *In sheet này* hoặc *In toàn bộ*, và nhận ngay một tệp `.xlsx` **đã căn sẵn trang in** — mở bằng Excel là Ctrl+P ra giấy, không phải chỉnh khổ giấy, lề, hay ngắt trang.

Hôm nay muốn in thì phải vào từng tab biểu mẫu bấm "Xuất Excel", và ba màn số liệu (Sổ Vị trí, Tổng hợp, Quyết toán năm) **không có đường xuất nào cả**.

## 2. Phạm vi

**Trong phạm vi** — sáu loại tài liệu:

| Tài liệu | Nguồn hiện có | Việc phải làm |
|---|---|---|
| Sổ theo Vị trí | `VitriLedgerViewModel.Rows` | Dựng mới bộ xuất |
| Tổng hợp (một dự án) | `ProjectTongHopViewModel.Rows` | Dựng mới bộ xuất |
| Quyết toán năm | `YearSettlementViewModel.Rows` | Dựng mới bộ xuất |
| Thông tri | `FormExcelExporter.ExportThongTriAsync` | Bổ sung căn trang |
| KS chi | `FormExcelExporter.ExportKsChiAsync` | Bổ sung căn trang |
| GĐNTT | `FormExcelExporter.ExportGdnttAsync` | Bổ sung căn trang |

**Ngoài phạm vi, có chủ ý:**

- **Xuất PDF.** User chốt hoãn sang đợt sau để cầm bản in `.xlsx` thật rồi mới góp ý. Thiết kế dưới đây tách model nội dung khỏi bộ dựng tệp chính là để đợt PDF chỉ phải viết thêm một bộ dựng, không phải viết lại nội dung. Ghi chú đã khảo sát cho đợt sau: PDFsharp + MigraDoc (MIT) là hướng khuyến nghị; font phải là **Liberation Serif** (OFL, khớp kích thước với Times New Roman) vì Times New Roman **không được nhúng** vào PDF theo giấy phép Monotype.
- **Hộp thoại máy in trong app / xem trước từng trang A4 trong app.** User chọn đường qua Excel.
- Màn "Tổng hợp" toàn bộ dự án (`WorkbookSummaryViewModel`), màn Quyết định, màn Cài đặt — nút In để mờ.
- Nhóm màn cũ đã chết (`FinanceProjectGridViewModel`, `ViewModels/TongHop/`, `ViewModels/ThongTri/`) — 7E sẽ xóa, không gắn gì vào.

## 3. Trải nghiệm người dùng

Nút **"In"** đặt ở `NavigationView.PaneFooter` của [MainWindow.axaml](../../../src/ConstructionFinance.Desktop/Views/MainWindow.axaml), ngay **trên** nút "Import Excel" đã có. Chọn chỗ này vì đó là chỗ duy nhất trên khung giao diện luôn hiển thị ở mọi màn, và đã sẵn một nút cùng loại nên trông đồng bộ.

Bấm vào mở `MenuFlyout` hai mục:

- **In sheet này** — xuất đúng tab/màn đang mở.
- **In toàn bộ** — xuất mọi sheet của **dự án đang chọn**, theo thứ tự của Mẫu.xlsx: `Tổng hợp` → `Vị trí 1..N` → `Quyết toán năm` → `Thông tri` → `KS chi` → `GĐNTT`.

Sau khi ghi tệp, app **mở tệp bằng ứng dụng mặc định** của hệ điều hành. Nếu mở được, không hiện thông báo gì thêm. Nếu không mở được, hiện đường dẫn tệp để người dùng tự mở.

Khi màn đang mở không in được, nút In **mờ đi** (`IsEnabled=false`) kèm `ToolTip` "Màn hình này không có nội dung để in". Không ẩn nút — vị trí nút phải cố định để người dùng quen tay.

**Nơi lưu tệp:** dùng lại `IDialogService.PickSaveFileAsync(title, defaultFileName, extensions)` đã có — chính hàm đường "Xuất Excel" hiện dùng — với tên gợi ý sẵn:

- In sheet này: `<Mã dự án>-<Tên sheet>-<yyyyMMdd-HHmm>.xlsx`, ví dụ `DA-01-Vi tri 1-20260910-1432.xlsx`
- In toàn bộ: `<Mã dự án>-Toan bo-<yyyyMMdd-HHmm>.xlsx`

Tên tệp bỏ dấu tiếng Việt và loại ký tự Windows cấm (`\ / : * ? " < > |`) để không hỏng trên máy đích. Dự án chưa có mã thì dùng tên dự án.

## 4. Kiến trúc

Nguyên tắc: **nội dung tách khỏi việc dựng tệp.** Màn hình mô tả "tôi in ra cái gì" bằng model thuần; một bộ dựng ở tầng Application biến model đó thành `.xlsx`. Nhờ vậy ClosedXML không rò lên tầng Desktop (giữ đúng phân lớp hiện có), và đợt PDF sau chỉ cần thêm bộ dựng thứ hai.

```
Desktop VM  ──IPrintablePage──►  PrintJob (model thuần)  ──►  PrintWorkbookWriter  ──►  .xlsx
                                        │                     (Application, ClosedXML)
                                        └──────────────────►  (đợt sau: PdfWriter)
```

### 4.1 Hai loại sheet, không ép chung một khuôn

Ba màn số liệu là **bảng**; ba biểu mẫu là **tờ khai có nhãn ở vị trí cố định**. Ép chung một mô hình sẽ đẻ ra một khuôn vạn năng không dùng tốt cho bên nào. Vì vậy `PrintSheet` có đúng hai dạng:

```csharp
public abstract record PrintSheet(string SheetName, PrintPageProfile Page);

/// Bảng: Sổ Vị trí, Tổng hợp, Quyết toán năm.
public sealed record PrintTableSheet(
    string SheetName,
    PrintPageProfile Page,
    IReadOnlyList<PrintColumn> Columns,
    IReadOnlyList<PrintRow> Rows,
    int HeaderRowCount) : PrintSheet(SheetName, Page);

/// Biểu mẫu: dùng lại nguyên bộ ghi đã có trong FormExcelExporter.
public sealed record PrintFormSheet(
    string SheetName,
    PrintPageProfile Page,
    FormPayload Form) : PrintSheet(SheetName, Page);

public sealed record FormPayload
{
    public ThongTriForm? ThongTri { get; init; }
    public KsChiForm? KsChi { get; init; }
    public GdnttForm? Gdntt { get; init; }
}
```

### 4.2 Ô mang giá trị có kiểu, không phải chuỗi

Bản in phải khớp màn hình, nhưng tệp `.xlsx` cũng phải **cộng được trong Excel** — người dùng hay bấm chọn một dải số để kiểm tổng. Nên ô mang giá trị gốc kèm kiểu, bộ ghi tự đặt định dạng số:

```csharp
public enum PrintCellKind { Text, Money, Date, Number }

public sealed record PrintCell(PrintCellKind Kind, string? Text, decimal? Number, DateOnly? Date)
{
    public static PrintCell Empty { get; } = new(PrintCellKind.Text, null, null, null);
    public static PrintCell OfText(string? v)    => new(PrintCellKind.Text,  v, null, null);
    public static PrintCell OfMoney(decimal v)   => new(PrintCellKind.Money, null, v, null);
    public static PrintCell OfDate(DateOnly? v)  => new(PrintCellKind.Date,  null, null, v);
}

public enum PrintShade { None, Light, VeryLight }   // §6.1 cần BA mức nền, bool không đủ
public sealed record PrintRow(IReadOnlyList<PrintCell> Cells, int IndentLevel, bool IsBold, PrintShade Shade);
public sealed record PrintColumn(string Header, double WidthChars, PrintAlign Align);
public enum PrintAlign { Left, Center, Right }
```

Tiền `0` ghi ra **ô trống**, đúng như quy ước `MoneyText.Format(0m)` trên màn hình.

### 4.3 Cách khung chung hỏi màn hình

```csharp
public interface IPrintablePage
{
    /// Tên sheet, cũng dùng đặt tên tệp. Null nghĩa là màn này không in được.
    string? PrintSheetName { get; }
    PrintSheet? BuildPrintSheet();
}
```

`WorkbookPageViewModel` (lớp cha của mọi tab) khai báo mặc định "không in được"; sáu lớp con in được thì override. `FinanceWorkbookViewModel` **tự nó cũng implement `IPrintablePage`** và ủy quyền cho `SelectedViTriPage` — nhờ vậy khung chung chỉ cần biết một interface, không phải biết đến khái niệm tab.

Cho "In toàn bộ":

```csharp
public interface IPrintableWorkbook
{
    string PrintWorkbookName { get; }        // dùng đặt tên tệp
    bool CanPrintAll { get; }                // RẺ và KHÔNG tác dụng phụ: chỉ đọc tên sheet
    IReadOnlyList<PrintSheet> BuildAllSheets();   // đường HÀNH ĐỘNG: tự làm mới tab tổng hợp
}
```

`CanPrintAll` tách riêng vì `CanExecute` của lệnh bị gọi rất thường xuyên: `BuildAllSheets()` làm mới tab tổng hợp, nên gọi nó trong một phép hỏi trạng thái vừa đắt vừa gây tác dụng phụ (đo được: làm mới hai lần mỗi lần đổi tab).

Hợp đồng thông báo: `PrintSheetName` khác null PHẢI nghĩa là có sheet thật — tab GĐNTT chưa chọn gói thầu trả null ở CẢ HAI thành viên. Khi điều kiện đó lật, trang biểu mẫu bắn `PrintSheetName` và workbook bắn kèm `CanPrintAll`, nếu không nút In vẫn xám cho tới khi user đổi tab.

Chỉ `FinanceWorkbookViewModel` implement — nó là nơi duy nhất nắm đủ mọi tab của dự án. Màn nào không implement thì mục "In toàn bộ" mờ đi.

`MainWindowViewModel` giữ hai lệnh `PrintCurrentSheetCommand` / `PrintAllCommand`, bật/tắt theo việc `CurrentContent` có implement interface tương ứng hay không, và theo dõi đổi màn để cập nhật lại trạng thái.

## 5. Căn trang

Một hồ sơ căn trang cho mỗi loại tài liệu, gom về một chỗ để không trôi khỏi nhau:

```csharp
public sealed record PrintPageProfile(
    XLPageOrientation Orientation,
    int FitToPagesWide,      // 1 = ép vừa một trang bề ngang; 0 = không ép
    int FitToPagesTall,      // 0 = bao nhiêu trang dọc cũng được
    int RepeatRowsTop,       // số dòng đầu lặp lại ở mỗi trang
    int RepeatColsLeft);     // số cột trái lặp lại ở mỗi trang
```

| Tài liệu | Hướng | Ép ngang | Lặp dòng đầu | Lặp cột trái |
|---|---|---|---|---|
| Sổ theo Vị trí | Ngang | 1 trang | 2 (tiêu đề + TỔNG CỘNG) | 2 (Ngày, Nội dung) |
| Tổng hợp | Ngang | 1 trang | 1 | 2 (STT, Tên vị trí) |
| Quyết toán năm | Dọc | 1 trang | 1 | 0 |
| Thông tri | Dọc | 1 trang | 0 | 0 |
| KS chi | Dọc | 1 trang | 0 | 0 |
| GĐNTT | Ngang | 1 trang | 0 | 0 |

Ghi chú: Quyết toán năm chỉ có 4 cột (Năm · NSNN · Ngoại hối · Tổng) nên để **dọc**, khác với bảng đã trình trước đó — ngang sẽ thừa giấy.

Áp dụng chung cho mọi sheet:

- Khổ **A4**, lề trái/phải `0.5"`, trên/dưới `0.6"`, **căn giữa theo chiều ngang**.
- Chân trang: trái = tên dự án · tên vị trí (nếu có), phải = `Trang &P/&N`.
- Đầu trang: bỏ trống — biểu mẫu đã tự có tiêu đề riêng.
- Font `Times New Roman` 13 (giữ nguyên hằng số `FormExcelExporter.FontName/FontSize` đang dùng).

Hồ sơ căn trang thay thế `FormExcelExporter.ApplyPageDefaults` hiện tại; hàm đó chuyển thành lời gọi hồ sơ tương ứng để ba biểu mẫu và ba bảng dùng **chung một đường**.

## 6. Nội dung từng sheet

### 6.1 Sổ theo Vị trí

14 cột đúng thứ tự và tiêu đề như lưới trên màn hình:

`Ngày` · `Nội dung` · `Nhà thầu / TK` · `Giá trị HĐ` · `Phụ lục` · `Số CT` · `Tạm ứng` · `KLHT` · `Thu hồi` · `Nguồn` · `GN NSNN` · `GN Ngoại hối` · `Số dư TU` · `Tổng đã GN`

Dòng theo đúng thứ tự `VitriLedgerViewModel.Rows`, và thể hiện đúng bậc như trên màn hình:

| Loại dòng | Thụt | Đậm | Nền |
|---|---|---|---|
| TỔNG CỘNG | 0 | có | xám nhạt |
| Nhóm chi phí | 0 | có | xám rất nhạt |
| Gói thầu | 1 | có | không |
| Đợt | 2 | không | không |

Cột "Nhà thầu / TK" của dòng Gói thầu ghép hai dòng trong một ô (`Nhà thầu`, xuống dòng, `Số TK`) bằng ngắt dòng trong ô + `WrapText`.

Bề rộng cột **không** lấy từ `LedgerColumnWidths` (đó là pixel màn hình); dùng bộ bề rộng riêng tính theo ký tự, đặt trong hồ sơ in, vì đơn vị của Excel là ký tự chứ không phải pixel.

### 6.2 Tổng hợp (một dự án)

Cột: `STT` · `Tên vị trí` · `Đơn vị thi công` · `Giá trị HĐ, PL` · `Tạm ứng` · `TT KLHT` · `Thu hồi` · `TT cho nhà thầu` · `Tạm ứng chưa thu hồi` · `GN Dự phòng NSNN` · `GN Quỹ DTNH` · `Tổng đã giải ngân` · `Quyết toán năm`.

Lấy thẳng từ `ProjectTongHopViewModel.Rows` (`SummaryDisplayRow`); dòng có `IsBold` in đậm.

### 6.3 Quyết toán năm

Cột: `Năm` · `NSNN` · `Ngoại hối` · `Tổng`. Lấy từ `YearSettlementViewModel.Rows`; `IsBold` in đậm.

### 6.4 Ba biểu mẫu

Không đổi nội dung. Bộ ghi hiện có trong `FormExcelExporter` được tách thành các hàm nhận sẵn một `IXLWorksheet` để `PrintWorkbookWriter` gọi được khi ghép nhiều sheet vào một workbook. Đường "Xuất Excel" cũ trên từng tab **giữ nguyên**, để người đang quen thao tác đó không bị hụt.

## 7. Mở tệp sau khi ghi

```csharp
public interface IFileLauncher
{
    /// Trả về false nếu hệ điều hành không mở được (không có ứng dụng liên kết,
    /// hoặc tiến trình không khởi động được) — người gọi hiện đường dẫn thay thế.
    bool TryOpen(string path);
}
```

Bản cài đặt dùng `Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })`. Tách interface để test không mở Excel thật.

## 8. Lỗi và trường hợp biên

| Tình huống | Xử lý |
|---|---|
| Chưa chọn dự án nào | Cả hai mục In đều mờ |
| Vị trí rỗng (không có nhóm/gói/đợt) | Vẫn in: có tiêu đề + dòng TỔNG CỘNG bằng 0. Không chặn |
| Tệp đích đang mở trong Excel | Ghi ném `IOException` → hiện "Tệp đang được mở trong chương trình khác. Đóng tệp rồi thử lại." |
| Người dùng bấm Hủy ở hộp thoại chọn tệp | Không làm gì, không báo lỗi |
| GĐNTT chưa chọn gói thầu | Sheet GĐNTT bị **bỏ qua** khi In toàn bộ, kèm cảnh báo cuối "Đã bỏ qua GĐNTT: chưa chọn gói thầu." In riêng GĐNTT thì mục In mờ, đúng như `CanExport` hiện tại |
| Dự án nhiều Vị trí | In toàn bộ ra mỗi Vị trí một sheet, tên sheet lấy tên Vị trí |
| Tên Vị trí trùng nhau / quá 31 ký tự / chứa ký tự Excel cấm | Dùng lại quy tắc `UniqueSheetName` (hiện là `private static` ở [FinanceExportService.cs:132](../../../src/ConstructionFinance.Application/Services/Implementations/FinanceExportService.cs#L132)) — **tách ra helper công khai dùng chung trước khi 7E xóa `FinanceExportService`** |

## 9. Kiểm thử

**Tầng Application (chính):** `PrintWorkbookWriter` là hàm thuần — ghi workbook ra `MemoryStream`, đọc lại bằng ClosedXML, kiểm:

- đúng số sheet, đúng tên sheet, đúng thứ tự;
- ô đúng vị trí và **đúng kiểu** (tiền là số có định dạng nghìn, không phải chuỗi; ngày là ngày);
- tiền bằng 0 ra ô trống;
- thụt bậc/đậm/nền đúng theo loại dòng;
- căn trang: `PageSetup.PaperSize`, `PageOrientation`, `PagesWide`, `RowsToRepeatAtTop`, `ColumnsToRepeatAtLeft`, chân trang chứa `&P/&N`;
- tên sheet trùng/quá dài được xử lý.

**Tầng Desktop:** mỗi view-model in được có test dựng `PrintSheet` từ dữ liệu mẫu và đối chiếu **số dòng và nội dung với chính `Rows` đang hiển thị** — đây là chốt chặn cho yêu cầu "bản in khớp màn hình". Test bật/tắt nút In theo màn hiện tại. `IFileLauncher` thay bằng bản giả, test không mở ứng dụng thật.

**Không test:** hộp thoại chọn tệp thật, và việc Excel in ra giấy.

## 10. Ràng buộc

- Không thêm gói NuGet nào — ClosedXML đã có sẵn.
- Đặt tên: biến cục bộ và tham số `camelCase`; kiểu, phương thức, thuộc tính `PascalCase`; field private `_camelCase`.
- Chữ hiển thị bằng tiếng Việt; tiền chỉ VNĐ.
- Avalonia ghim **11.2.1** — `Grid` chưa có `ColumnSpacing`/`RowSpacing`.
- ClosedXML **không được** xuất hiện trong tầng Desktop.
- Không đụng nhóm code đã chết đang chờ 7E xóa.

## 11. Việc phải làm trước, vì 7E sẽ xóa mất

`UniqueSheetName` hiện nằm trong `FinanceExportService` — thứ 7E sẽ xóa. Phải **tách ra một helper dùng chung** trong đợt này, nếu không 7E sẽ làm hỏng tính năng in. Đây là mối nối thứ tư cùng loại với ba mối đã ghi trong kế hoạch 7E (`ImportMerge.NextFreeName`, `IFinanceProjectService`, `FinanceWorkbookViewModel`) — cập nhật ghi chú 7E kèm theo.
