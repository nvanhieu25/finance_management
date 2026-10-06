# In thẳng ra máy in — Thiết kế

**Ngày:** 2026-10-07 · **Trạng thái:** chờ user duyệt
**Thay thế một phần:** `2026-09-10-in-qua-phan-mem-design.md` (đường `.xlsx` vẫn giữ, xem §3)

## 1. Vì sao có đợt này

Spec 10/9 cố ý để **ngoài phạm vi** hai thứ: hộp thoại máy in trong app, và xuất PDF. Lý do ghi
trong chính spec đó: *"User chốt hoãn sang đợt sau để cầm bản in `.xlsx` thật rồi mới góp ý."*

Người dùng đã cầm bản in thật, và góp ý là: **tưởng bấm In thì chọn máy in rồi in luôn, như
Windows+P**, chứ không phải ra tệp `.xlsx` rồi tự Ctrl+P trong Excel. Đợt này làm đúng điều đó.

## 2. Quyết định đã chốt

| Vấn đề | Chốt |
|---|---|
| Bấm In thì xảy ra gì | Hiện hộp thoại chọn máy in, bấm OK là giấy chạy ra. Không sinh tệp. |
| Đường xuất `.xlsx` hiện có | **Giữ**, thành mục riêng trong cùng menu |
| Hộp thoại máy in | **Tự dựng bằng Avalonia**, liệt kê mọi máy in đã cài — không kéo WinForms, không P/Invoke |
| Xem trước trong app | Không làm đợt này |

Hai phương án bị loại và lý do: `PrintDialog` của **WinForms** cho hộp thoại nguyên bản nhưng bắt
app Avalonia nuôi thêm một framework giao diện chạy vòng lặp modal riêng; **P/Invoke `PrintDlgEx`**
tránh được WinForms nhưng phải tự viết interop `DEVMODE`/`DEVNAMES` — đoạn dễ sai nhất của cả tính
năng, lại nằm đúng trong vùng không chạy thử được trên máy phát triển.

## 3. Phạm vi

**Trong phạm vi:** in thẳng ra máy in cho **đúng sáu tài liệu** mà đường `.xlsx` đang phục vụ — Sổ
theo Vị trí, Tổng hợp (một dự án), Quyết toán năm, Thông tri, KS chi, GĐNTT. "In toàn bộ" in mọi
sheet của dự án đang chọn trong **một lệnh in**, theo thứ tự của Mẫu.xlsx.

**Ngoài phạm vi, có chủ ý:**

- **Bỏ đường `.xlsx`.** Giữ nguyên: In là ra giấy, Xuất Excel là ra tệp để sửa/lưu/gửi. Bộ test
  của đường đó vẫn còn giá trị và đợt này không đụng tới.
- **Xem trước từng trang trong app.** `PreviewPrintController` của .NET cho sẵn đường làm việc này
  về sau nếu cần; đợt này không.
- **PDF như một tính năng người dùng.** Bộ dựng PDF *có* được viết, nhưng là công cụ kiểm chứng
  (§8), không gắn vào menu. Muốn biến thành tính năng sau này thì gần như đã có sẵn.
- Khay giấy, in hai mặt, khoảng trang — hộp thoại tự dựng không có. Cần thì dùng đường `.xlsx`.

## 4. Kiến trúc

### 4.1 Mô hình nội dung giữ nguyên

`IPrintablePage.BuildPrintSheet()` vẫn trả `PrintSheet` (`PrintTableSheet` · ba record biểu mẫu).
**Không sửa dòng nào** ở sáu trang đang khai báo nội dung in. Đây đúng là thứ spec 10/9 đã chuẩn
bị: *"tách model nội dung khỏi bộ dựng tệp chính là để đợt sau chỉ phải viết thêm một bộ dựng"*.

### 4.2 Một bộ dàn trang, hai đầu ra

```
PrintSheet[]  ──►  MigraDocBuilder  ──►  MigraDoc.Document
                                              │
                        ┌─────────────────────┴─────────────────────┐
                        ▼                                           ▼
            PdfRenderer (đa nền)                       WindowsPrinterPort (Windows)
            → tệp .pdf, dùng để KIỂM CHỨNG             → PrintDocument → giấy
```

`MigraDocBuilder` nằm ở `Application/Printing`, thuần dữ liệu: dựng bảng, bề rộng cột, lặp dòng
tiêu đề, nền, viền, khổ giấy. Không biết GDI, không biết máy in, không biết Avalonia.

Hai đầu ra dùng **chung một bộ dàn trang** của MigraDoc, nên những gì soi được trên PDF cũng đúng
trên giấy — trừ phần trình điều khiển máy in tự xoay xở.

### 4.3 Chia dự án để không giết bộ test

Gói GDI nhắm `net8.0-windows7.0`. Nếu nó lọt vào `Application` hay `Desktop` thì cả 468 test
Desktop không chạy được trên máy phát triển Linux nữa. Nên:

| Dự án | Đích | Giữ gì |
|---|---|---|
| `ConstructionFinance.Application` | `net8.0` | `MigraDocBuilder`, `PdfRenderer`, interface `IPrinterPort` + `IPrinterCatalog` |
| `ConstructionFinance.Printing.Windows` *(mới)* | `net8.0-windows` | đúng một adapter: `PrintDocument` + `XGraphics.FromGraphics` |
| `ConstructionFinance.Desktop` | `net8.0;net8.0-windows` | đích `net8.0` cho test chạy Linux; đích `net8.0-windows` cho bản phát hành, và chỉ đích này tham chiếu dự án Windows |

Đa đích là chỗ tốn công nhất về cấu trúc. **Task đầu tiên của plan phải chứng minh nó bằng một lần
build xanh trên Linux và 817 test vẫn chạy đủ** (đo lúc viết spec: Domain 38 · Infrastructure 10 ·
Data 54 · Application 247 · Desktop 468) — không chứng minh được thì dừng, báo, và bàn lại.

*Số đo sau khi làm xong (Task 13): **1011** test — Domain 38 · Infrastructure 10 · Data 54 ·
Application 411 · Desktop 498 — không hỏng, không bỏ qua; build `-warnaserror` sạch ở cả hai đích.*

### 4.4 Nối vào giao diện: bốn lệnh thay vì hai

`MainWindowViewModel` hiện có `PrintCurrentSheetAsync` và `PrintAllAsync`, cả hai đi qua
`GuardPrintAsync` (bắt mọi lỗi để một `AsyncRelayCommand` không làm sập app khi người dùng còn sổ
chưa lưu) rồi vào đường chung *chọn nơi lưu → ghi → mở bằng ứng dụng mặc định*.

Đổi thành:

| Lệnh | Làm gì |
|---|---|
| `ExportCurrentSheetAsync`, `ExportAllAsync` | **đúng hai lệnh hiện có, chỉ đổi tên.** Đường ghi tệp không sửa gì |
| `PrintCurrentSheetAsync`, `PrintAllAsync` (mới) | dựng sheet → `MigraDocBuilder` → hộp thoại Chọn máy in → `IPrinterPort` |

Cả bốn vẫn bọc trong `GuardPrintAsync` — lý do nó tồn tại không đổi, và đường in có thêm nguồn lỗi
mới (trình điều khiển máy in) nên càng cần.

**Một chỗ cố ý làm khác đường tệp.** "In toàn bộ" hôm nay báo *"đã bỏ qua sheet nào"*
(`DescribeSkipped`) **sau khi ghi tệp**, trước khi mở — hợp lý, vì tệp đã nằm trên đĩa, đọc cảnh báo
rồi vẫn xử lý được. Giấy thì không rút lại được. Nên ở đường in, cảnh báo đó hiện **trước hộp thoại
chọn máy in**, để người dùng còn kịp hủy. Hành vi đã có test ghim của đường tệp giữ nguyên, không
đụng.

**Trên đích `net8.0` không có máy in.** Đích đó chỉ để bộ test chạy trên Linux; `IPrinterPort`
không có bản cài đặt nào được đăng ký, nên hai lệnh In mới **không dùng được** ở đó và phải mờ
đi — không được để chúng ném `NullReference` giữa một lần chạy test. Bản phát hành luôn là
`net8.0-windows`. Plan phải có test ghim đúng điều này.

## 5. Dàn trang: tự làm việc Excel đang làm hộ

Hồ sơ in hiện đặt `FitToPages(1 ngang, 0 dọc)` và **Excel tự thu nhỏ cho vừa bề ngang**. MigraDoc
không có khái niệm đó: bề rộng cột khai bằng mm, rộng quá là tràn.

`MigraDocBuilder` phải tự tính, theo đúng thứ tự:

1. Bề rộng mỗi cột ra pixel theo đúng công thức của Excel: `px = WidthChars × 7 + 5`. Số 7 là bề
   rộng chữ `0` của Calibri 11 @96dpi; **+5 là phần đệm hai bên ô, cộng một lần cho mỗi cột** —
   bỏ nó là tính thiếu, và càng nhiều cột càng thiếu nhiều.
2. Cộng lại rồi quy ra mm (`mm = px ÷ 96 × 25,4`).
3. Bề ngang giấy dùng được = khổ A4 theo hướng của hồ sơ, trừ lề trái/phải 0,5 inch mỗi bên
   (đúng lề `FormExcelExporter.ApplyPageDefaults` đang đặt) → **271,6mm** với A4 ngang.
4. Tỉ lệ co = (3) ÷ (2), áp cho **cả bề rộng cột lẫn cỡ chữ**.

Số thật của cả sáu tài liệu, tính theo đúng công thức trên (2026-10-07):

| Tài liệu | Hướng | Cột | Đơn vị | Rộng (mm) | Giấy (mm) | Tỉ lệ | Chữ in ra |
|---|---|---|---|---|---|---|---|
| Sổ Vị trí | ngang | 14 | 237 | 457 | 271,6 | **0,59** | 7,7pt |
| Tổng hợp | ngang | 13 | 231 | 445 | 271,6 | **0,61** | 7,9pt |
| Quyết toán năm | dọc | 4 | 66 | 128 | 184,6 | **1,45** | — xem dưới |
| Thông tri | dọc | 8 | 124 | 240 | 184,6 | **0,77** | 10,0pt |
| KS chi | dọc | 5 | 102 | 196 | 184,6 | **0,94** | 12,3pt |
| GĐNTT | ngang | 9 | 154 | 297 | 271,6 | **0,91** | 11,9pt |

**Chốt: tỉ lệ bị chặn trên ở 1,0 — co thì co, phóng to thì không.** Quyết toán năm chỉ có 4 cột nên
hẹp hơn giấy; nó in ở đúng 13pt với một dải trắng bên phải, chứ không bị kéo giãn thành chữ 18,8pt.
Lý do: cỡ chữ 13pt là cỡ của biểu mẫu gốc, phóng to lên là tự ý đổi hình thức văn bản hành chính.

Chỗ này **có thể khác bản in Excel hôm nay** ở đúng một sheet: `FitToPagesWide = 1` của Excel được
phép phóng to để lấp trang. Nếu bản in Quyết toán năm từ Excel đang to hơn 13pt thì sau đợt này nó
nhỏ lại. Đây là khác biệt có chủ ý, ghi ra để người dùng biết trước chứ không phải lỗi.

**Phải nói thẳng:** bản in sẽ nhỏ đúng như hôm nay, vì hôm nay Excel cũng đang thu còn 59% — chỉ
là con số đó chưa ai nhìn thấy. Đợt này không làm nó đẹp hơn, chỉ làm nó không phải đi vòng qua Excel.

Hệ quả dễ chịu: MigraDoc **không có `####`**. Chữ không vừa thì xuống dòng hoặc bị cắt, chứ không
hóa thành dấu thăng. Bề rộng cột tiền 16 ký tự (chốt 2026-10-06, trần dưới 100 tỷ) vẫn giữ nguyên
ý nghĩa — chỉ là rủi ro đổi từ "mất hẳn con số" thành "chữ bé lại".

### 5.1 Ba biểu mẫu không phải bảng

Ba sheet dạng bảng (Sổ Vị trí, Tổng hợp, Quyết toán năm) là tiêu đề + nhiều dòng giống nhau — dựng
một `Table` của MigraDoc là xong. **Ba biểu mẫu thì không.** Thông tri, KS chi, GĐNTT là bản mẫu
cứng: từng ô một, chữ đặt đúng vị trí, và hiện đang dùng **26 lệnh gộp ô** trong 351 dòng mã của
`FormExcelExporter`.

Cách dựng: mỗi biểu mẫu là **một `Table` của MigraDoc đúng số cột và bề rộng cột mà bộ vẽ Excel
đang đặt** (Thông tri 8 cột, KS chi 5, GĐNTT 9), các dòng thêm theo đúng thứ tự cũ, và mỗi lệnh
`Range(...).Merge()` của Excel thành một `MergeRight`/`MergeDown` trên ô tương ứng.

Đây là **phần việc nội dung lớn nhất của đợt này** — không phải vì khó, mà vì nhiều và dễ lệch một
ô mà không ai thấy. Plan phải tách **mỗi biểu mẫu một task riêng**; gộp ba vào một là tự đặt bẫy.

Và không nghiệm thu bằng mắt, mà bằng **test so khớp hai đầu ra**: cùng một `PrintSheet` đưa qua
`PrintWorkbookWriter` (ra `XLWorkbook` trong bộ nhớ) và qua `MigraDocBuilder` (ra `Document` trong
bộ nhớ), rồi khẳng định **mọi ô có chữ trong sheet Excel đều có đúng chữ đó, ở đúng dòng–cột, trong
bảng MigraDoc**. Test này chạy trên Linux, không cần PDF, không cần máy in, và còn bắt được chuyện
sau này ai sửa một bộ vẽ mà quên bộ kia.

Nó **không** bắt được: bề rộng cột, độ đậm viền, mức nền, chữ có bị cắt hay không, vị trí thật trên
giấy. Những thứ đó thuộc phần render — soi bằng PDF (§8), và chốt cuối bằng giấy thật.

## 6. Giao diện

### 6.1 Menu của nút In

```
In sheet này
In toàn bộ
─────────────────────
Xuất Excel sheet này
Xuất Excel toàn bộ
```

Hai mục cuối làm đúng việc hai mục hiện tại đang làm (ghi `.xlsx` rồi mở bằng ứng dụng mặc định).

**"In toàn bộ" là một lệnh in, một hộp thoại.** Mọi sheet của dự án đang chọn vào cùng một tài liệu
MigraDoc, theo thứ tự của Mẫu.xlsx, mỗi sheet bắt đầu ở trang mới và mang hướng giấy của hồ sơ
riêng nó (MigraDoc cho đổi hướng giữa các `Section`). Người dùng chọn máy in **một lần**.

Đồng thời sửa lỗi tìm ra khi soi việc 3: `MenuFlyout` hiện **mở xuống dưới** từ một nút nằm sát đáy
khung trái, chỉ còn khoảng 10px dư trên màn 1080px và che nút "Import Excel". Đặt `Placement="Top"`
để menu mọc lên trên, tránh mép màn hình — thêm một mục nữa là tràn.

### 6.2 Hộp thoại Chọn máy in

Dialog Avalonia, dựng bằng đúng cơ chế sẵn có: một `ChonMayInViewModel` + View tương ứng, mở qua
`IDialogService.ShowDialogAsync<T>` (trả `bool?` — `true` là In, `false`/`null` là Hủy). Nội dung:

- Danh sách máy in đã cài, **máy mặc định chọn sẵn**.
- Ô **số bản** (1–99), đặt vào `PrinterSettings.Copies` để trình điều khiển tự nhân bản — app
  không in lặp nhiều lượt.
- Nút **In** / **Hủy**.

Danh sách lấy qua `IPrinterCatalog` được tiêm vào, nên test headless giả lập được danh sách mà
không cần máy in thật.

## 7. Lỗi và trường hợp biên

Theo đúng bài học của vòng duyệt trước — câu lỗi phải **đúng sự thật với việc người dùng vừa làm**,
không gộp nhiều nguyên nhân vào một câu:

| Tình huống | App nói gì |
|---|---|
| Máy chưa cài máy in nào | "Máy này chưa cài máy in nào." + gợi ý dùng Xuất Excel |
| Người dùng bấm Hủy | Không nói gì, không in |
| Máy in báo lỗi giữa chừng | Nêu tên máy in và thông báo của hệ thống, kèm việc nên làm |
| Màn đang mở không có gì để in | Nút In mờ sẵn (hành vi hiện có, giữ nguyên) |

## 8. Kiểm chứng, và ranh giới thành thật của nó

| Phần | Cách kiểm | Chạy ở đâu |
|---|---|---|
| `MigraDocBuilder` dựng tài liệu | test đơn vị trên cấu trúc `Document` | Linux ✅ |
| **Nội dung khớp đường `.xlsx`** | **so từng ô `XLWorkbook` ↔ `Document` (§5.1)** | Linux ✅ |
| Dàn trang, phân trang, lặp dòng tiêu đề, hướng giấy | render ra PDF rồi mở lại soi số trang / chữ / hướng | Linux ✅ |
| Tỉ lệ co ở §5 | test số học trên hàm tính, không qua render | Linux ✅ |
| Hộp thoại Chọn máy in | test headless Avalonia (`Avalonia.Headless` đã có sẵn) | Linux ✅ |
| **Vẽ lên mặt giấy máy in** | **chỉ biên dịch; chạy phải trên Windows** | Windows ⚠️ |

Ô cuối là **vùng mù duy nhất**, và thiết kế cố ý ép nó nhỏ: adapter Windows chỉ chứa vòng lặp
`PrintPage` khoảng 20 dòng, không có logic nội dung nào. Mọi quyết định về *in cái gì, nằm ở đâu*
đều nằm phía Linux kiểm được.

Nghiệm thu cuối cùng vẫn là **người dùng in thử trên máy Windows có máy in thật**.

## 9. Gói và giấy phép

| Gói | Bản | Giấy phép | Vào đâu |
|---|---|---|---|
| `PDFsharp-MigraDoc` | 6.2.4 | MIT | Application (`net8.0`) |
| `PDFsharp-MigraDoc-GDI` | 6.2.4 | MIT | `Printing.Windows` (`net8.0-windows`) |
| `System.Drawing.Common` | 8.0.10 | MIT (Microsoft) | `Printing.Windows` |

Hợp ràng buộc "100% giấy phép tự do" của dự án. Không có Syncfusion/QuestPDF/EPPlus v5+.

**Lưu ý phông chữ cho đường PDF:** Times New Roman **không được nhúng vào PDF** theo giấy phép
Monotype. Bộ dựng PDF dùng **Liberation Serif** (OFL, trùng khít metric với Times New Roman) — đúng
khuyến nghị đã ghi ở spec 10/9. Đường in ra giấy không vướng chuyện này vì phông do máy tính Windows
cung cấp, không nhúng vào đâu cả.

## 10. Bằng chứng khả thi đã kiểm (2026-10-07)

Ghi lại để plan khỏi phải điều tra lại:

- **Avalonia 11.2.1 không có API in nào.** Quét toàn bộ assembly: chỉ có `PrintMembers` (record sinh
  tự động) và `PrintScreen` (phím). Không có `PrintDialog`/`PrintDocument`.
- **`System.Drawing.Printing` có** `PrintDocument`, `PrinterSettings`, `PrinterSettings.InstalledPrinters`,
  `PreviewPrintController`, `StandardPrintController` — **không có `PrintDialog`** (liệt kê bằng
  reflection trên `System.Drawing.Common` 8.0.10). Đó là lý do §2 phải chọn giữa WinForms, P/Invoke,
  hoặc tự dựng.
- **`DocumentRenderer.RenderPage(XGraphics, int)` có thật** (reflection trên bản đa nền) — tức MigraDoc
  dàn trang xong thì vẽ được lên bất kỳ mặt vẽ nào.
- **`XGraphics.FromGraphics` có trong `PdfSharp-gdi.dll`** (soi metadata; không nạp được trên Linux nên
  đây là bằng chứng gián tiếp).
- **Đoạn mã in thật biên dịch sạch trên Linux.** Một dự án `net8.0-windows` chứa đúng vòng lặp
  `PrintPage` → `XGraphics.FromGraphics(e.Graphics, …)` → `renderer.RenderPage(gfx, trang)` →
  `e.HasMorePages = trang < renderer.FormattedDocument.PageCount` → `pd.Print()` build **0 lỗi**.
  Đây là bằng chứng mạnh nhất: vùng mù chỉ còn là *chạy*, không phải *viết đúng kiểu*.

## 11. Việc kéo theo

- Lỗi hướng mở `MenuFlyout` (§6.1) tìm ra khi soi "việc phải nhìn bằng mắt" số 3 của spec 10/9 — sửa
  trong đợt này vì cùng đụng một chỗ markup.
- Việc "nhìn bằng mắt" số 2 của spec 10/9 (độ đậm viền, hai mức nền trên giấy thật) **vẫn còn treo**
  và đợt này không giải quyết: nó là câu hỏi về máy in thật, không phải về mã.

## 12. Khác với thiết kế ban đầu

Mục này ghi chỗ nào mã đã ship khác với những gì các mục trên viết. Các mục trên **không** được sửa
lại cho giống mã: nơi nào thiết kế đoán sai thì người sau cần thấy nó đã sai ở đâu. Mỗi dòng: spec
nói gì, mã làm gì, và vì sao.

### 12.1 Dàn trang và dựng hình

- **Luật bù tràn chữ của `FormGrid`** (spec §5.1 không có). Excel cho chữ tràn sang các ô trống bên
  phải; ô MigraDoc thì xuống dòng trong lòng ô. Nên ô có chữ rộng hơn cột sẽ tự gộp sang phải qua
  các ô trông như trống, dừng ở ô đầu tiên có nội dung. Thiếu luật này cả ba mẫu in mất bố cục.
  Ô ghi `""` **không** là tường chắn (các bộ vẽ làm việc đó 21 lần).
- **Thụt lề dùng `LeftIndent`, không dùng `FirstLineIndent`**, ở cả `MigraDocBuilder` lẫn
  `FormGrid`. Thụt lề của ô Excel dịch mọi dòng; mã trong plan chỉ dịch dòng đầu, nên dòng bị xuống
  dòng sẽ mất phân cấp Nhóm/Gói/Đợt.
- **`Money` căn phải trong `FormGrid`.** Excel mặc định căn phải ô số nên bộ vẽ Excel không bao giờ
  nói ra; bản sao trung thực phải tự nói.
- **Chân trang có thêm khoảng cách chân trang và một tab stop căn phải.** Plan không đặt cái nào,
  nên chân trang đè lên dòng cuối mỗi trang và số trang không bao giờ chạm mép phải.
- **Cỡ chữ chân trang co theo thân**, không cố định 13pt — và từ 2026-10-07 thì ĐÚNG cho cả
  sáu tài liệu. Trước đó dòng này SAI ở đúng ba tờ người dùng ký: `MigraDocBuilder.FontScaleOf`
  chỉ xét `PrintTableSheet`, mọi sheet biểu mẫu rơi xuống `1.0`, nên chân trang in 13pt trong
  khi `FormGrid.Render` co thân còn 0,768 (Thông tri) / 0,944 (KS chi) / 0,914 (GĐNTT). Sửa
  bằng cách hoist bề rộng cột + hướng giấy của ba biểu mẫu thành `MigraDocFormSheets.FormLayout`
  để hai nơi đọc CÙNG một nguồn; ba test ghim `13 × tỉ lệ` (`Chan_trang_*_co_theo_than_*`).
- **Chân trang bị cắt ở `PrintWorkbookWriter.MaxFooterLeftChars` (120 ký tự).** Spec nói MigraDoc
  không cần cắt vì 255 là giới hạn của Excel. Đúng một nửa: con số 255 là của Excel, nhưng "đừng
  tràn khỏi tờ giấy" là luật của giấy. Cắt cùng một số ký tự để hai đường in ra giống hệt nhau. Một
  chuỗi liền không có khoảng trắng dài hơn trang vẫn tràn (MigraDoc không ngắt giữa từ); tên dự án
  thật có khoảng trắng.
- **Lề trên/dưới 0,4 inch** (đường `.xlsx` dùng 0,6), và **`FooterDistance` 0,18 inch**. Hai thứ này
  cùng giữ cho GĐNTT vừa một tờ khi các dòng xuống dòng.
- **Không tái tạo `CenterHorizontally`.** Spec nói tờ hẹp in lệch trái, chừa trắng bên phải; đường
  `.xlsx` thì căn giữa. Quyết toán năm là chỗ thấy rõ.
- **`RepeatColsLeft` bị bỏ qua, và đúng khi bỏ:** bảng MigraDoc không bao giờ ngắt trang theo chiều
  ngang.
- **GĐNTT, hàng 6-16: MigraDoc xuống dòng còn Excel cắt cụt.** Đo trên giá trị thật: dòng tên dự án
  dài 64 ký tự, dòng "Căn cứ giấy đề nghị thanh toán vốn đầu tư số" dài 77, so với nhịp gộp 58 đơn
  vị. Tức đây là trường hợp thường, không phải biên. Excel lặng lẽ mất phần đuôi của số giấy ủy
  quyền thanh toán. Giữ trọn chữ được coi trọng hơn khớp với việc bị cắt.

### 12.2 Cấu trúc và công cụ

- **Bộ dựng PDF nằm ở dự án TEST, không ở `Application`** như bảng §4.3 ghi. §3 đã xếp PDF là
  công cụ kiểm chứng chứ không phải tính năng người dùng, nên để nó trong `Application` là đưa mã
  không ai gọi vào bản phát hành. Muốn biến PDF thành tính năng thật thì chuyển `PdfProbe` sang
  `Application` và đặt tên `PdfRenderer`.
- **Thêm `IHasPrimaryButtonText`** vì `ShowDialogAsync` gán cứng nút xác nhận là "Lưu".
- **Số bản bị kẹp ba lần**, không một lần: ở `NumericUpDown`, ở view-model, và ở constructor của
  `PrinterJob`. `PrinterSettings.Copies` là `short`, giá trị lớn tràn thành số âm.
- **`ConstructionFinance.Printing.Windows` loại các assembly PDFsharp và MigraDoc bản core**
  (`ExcludeAssets="all"`), vì `XGraphics`, `XSize` và `DocumentRenderer` có ở cả hai bản và đến
  qua phụ thuộc bắc cầu. Hai `MigraDoc.DocumentObjectModel.dll` của hai gói giống nhau từng byte
  nên `Document` vẫn là một kiểu duy nhất.
- **Vòng `PrintPage` có thêm handler `QueryPageSettings`** đặt hướng giấy từng trang theo
  `FormattedDocument.GetPageInfo(n).Orientation`, cộng ưu tiên khổ A4. Plan không đặt cái nào, nên
  mọi tài liệu nằm ngang (Sổ Vị trí, Tổng hợp, GĐNTT, tức một nửa tính năng) sẽ in ra giấy đứng và
  mất một phần ba bên phải.
- Vòng `QueryPageSettings` có một **chốt chặn biên** (`queriedPage > PageCount` thì thoát). Vòng in
  của .NET bắn đúng một lần mỗi trang, nhưng một trình điều khiển bắn thừa sẽ làm `GetPageInfo`
  ném và hỏng mọi lệnh in trên máy đó, mà đây là vùng mù không thử được.
- **Đăng ký `IPrinterPort`/`IPrinterCatalog` bằng một hàm `partial`, không bằng `#if`.** Bản
  đầu dùng `#if NET8_0_WINDOWS` trong `HostBuilder.cs` — một ký hiệu **.NET SDK không định
  nghĩa** (đích `net8.0-windows` cho `WINDOWS`, `WINDOWS7_0`, `WINDOWS7_0_OR_GREATER`, `NET`,
  `NET8_0`, `NET8_0_OR_GREATER`, `NETCOREAPP`; không có `NET8_0_WINDOWS`), và không chỗ nào
  trong repo định nghĩa nó. `#if` luôn sai thì **không sinh cảnh báo**, `ProjectReference` vẫn
  thỏa, còn bộ test chỉ chạy đích `net8.0` nơi hai lệnh đăng ký ĐÁNG LẼ phải vắng — nên cả 31
  commit của đợt này nằm chết trong bản `.exe` đã ship: `WindowsPrinterPort` xuất hiện **0 lần**
  trong `ConstructionFinance.dll` ở cả hai đích, `HasPrinterSupport` luôn `false`, hai mục In ra
  giấy mờ vĩnh viễn trên Windows. "Build xanh, 1011 test xanh" đúng sự thật và **không chứng minh
  gì**. Lỗi IM LẶNG — đúng loại lỗi mà đổi sang một ký hiệu khác (`#if WINDOWS`) sẽ lại gây ra
  lần nữa nếu ai đó sửa điều kiện của `ProjectReference`. Bản sửa: `static partial void
  AddPrinterServices(IServiceCollection)` khai ở `HostBuilder.cs` (không thân ⇒ ở `net8.0` lời
  gọi bị xoá khi biên dịch), thân nằm ở `HostBuilder.Printing.Windows.cs` **không có một dòng
  tiền xử lý nào**, và file đó được `Compile Include` trong ĐÚNG CÁI `ItemGroup` mang
  `ProjectReference` có điều kiện (cộng một `Compile Remove` ngoài điều kiện, vì glob mặc định
  của SDK không biết gì về đích). Mất tham chiếu mà còn file thì **build hỏng**, không ship một
  tính năng chết. Ghim bằng `strings`: 0 lần ở `net8.0`, >0 ở `net8.0-windows`.
- **`PrintDocument.DocumentName` mang tên dự án.** Không đặt thì mọi lệnh đứng trong hàng đợi
  Windows với tên `"document"`: trên máy in dùng chung nhân viên không tìm được bản của mình và
  nhật ký in ghi một cái tên cho tất cả. `PrinterJob` mang thêm `DocumentName` (nguồn: tên dự án
  của chân trang; rỗng thì về `PrinterJob.DefaultDocumentName`).
- **`PrinterSettings.Collate = true`.** Hộp thoại cho gõ 1–99 bản; mặc định của trình điều khiển
  có thể là không ghép bộ, tức "In toàn bộ × 3" của dự án bảy sheet ra 3×trang1, 3×trang2… và
  nhân viên phải phân lại cả tập trước khi ký. "Số bản = 3" với người dùng nghĩa là ba BỘ.
- **Khổ A4 tra một lần, ngoài `QueryPageSettings`.** Mỗi lần đọc `PrinterSettings.PaperSizes` là
  một vòng hỏi trình điều khiển; để trong handler thì lệnh 50 trang hỏi 50 lần cùng một câu.
- **`try` của `WindowsPrinterPort.Print` bọc cả bước dựng cấu hình máy in, không riêng
  `printDocument.Print()`.** Kéo theo từ dòng trên: lúc `PaperSizes` còn được đọc trong
  `QueryPageSettings` thì nó chạy *bên trong* `Print()` nên đã được bọc; tra một lần ở ngoài thì
  không. Mà `PaperSizes` chắc chắn đi tới trình điều khiển, và máy in có thể vừa bị xoá hoặc rút
  mạng giữa lúc người dùng chọn và lúc lệnh bắt đầu. Hậu quả KHÔNG phải sập app —
  `MainWindowViewModel.GuardPrintAsync` bắt `Exception` chung (có test
  `Loi_bat_ngo_thi_bao_mot_cau_chung_chu_khong_sap_app`) — mà là **câu thông báo**: người dùng
  nhận "Không in được do lỗi bất ngờ…" thay vì câu nêu tên máy in và lời của hệ thống, đúng trên
  cái hỏng dễ xảy ra nhất ở phòng dùng máy in chung. §7 coi tính đúng của câu thông báo là yêu
  cầu hạng nhất. Ranh giới `try` giờ là: **MigraDoc ở ngoài** (`DocumentRenderer`,
  `PrepareDocument` — lỗi ở đó là lỗi của ta, gói thành "máy in báo lỗi" là đổ oan cho máy in),
  **mọi thứ chạm trình điều khiển ở trong**.
- **Dòng tiêu đề lặp mỗi trang chỉ được ghim ở mức mô hình** (`Row.HeadingFormat == true`), không
  ghim bằng cách đọc chữ trên từng trang PDF như §8 ngụ ý. PDFsharp không trích được chữ.
- **`SheetTextMap` báo hỏng bằng `Assert.Fail` với chuỗi riêng**, không dùng `ShouldBeEmpty`, vì
  Shouldly cắt bớt phần tử của tập hợp, mà danh sách chỗ lệch là chẩn đoán duy nhất của ba bản sao
  mẫu in.
- **`SheetTextMap.Normalize` chỉ bỏ dấu phân cách hàng nghìn ở giá trị thực sự là số.** Bỏ ở mọi
  nơi đã xóa mất dòng điền tay của KS chi `" + Mục ........, tiểu mục ........"`, khiến tám dấu
  chấm và bốn dấu chấm bằng nhau.
- **`SheetTextMap.CellText` đọc cả `Character` lẫn `FormattedText`**, không chỉ `Text`. MigraDoc
  đổi `"\n"` thành `Character(LineBreak)`, và ô "Nhà thầu / TK" được dựng bằng một cái như vậy.
- **Đường `.xlsx` và bộ dựng MigraDoc dùng chung một hằng số lề** (`ExcelFitScale.SideMarginInches`;
  Task 13). Trước đó `FormExcelExporter.ApplyPageDefaults` viết thẳng 0,5 và hằng số là bản sao thứ
  hai; có test ghim.

## 13. Chưa kiểm được ở đây

Mỗi mục: triệu chứng để nhận ra, và cần gạt nào để chỉnh. Nếu lần in đầu ra xấu, tìm triệu chứng
của bạn ở đây trước.

- **In thử trên Windows có máy in thật.** `WindowsPrinterPort` chỉ được biên dịch trên máy phát
  triển, chưa từng chạy. Đây là nghiệm thu cuối của cả đợt và chỉ người dùng làm được.
- **VIỆC ĐẦU TIÊN CẦN NHÌN Ở BẢN IN THỬ: chân trang có ở đáy MỌI trang hay không?** Tức tên dự
  án bên trái và "Trang x/y" bên phải. Đây là mục đứng đầu danh sách, trước cả hai triệu chứng
  hình học bên dưới, vì chân trang mất đọc ra như một tính năng HỎNG chứ không như một độ lệch
  đã biết — mà với bản phụ lục nhiều trang gửi Kho bạc thì số trang chính là cách chứng minh hồ
  sơ đủ. `FooterDistance` 0,18" = 4,57mm từ mép giấy, trong khi dải không in được của máy laser
  thường là 4,2-5,0mm: nằm đúng ranh giới. *Nếu thiếu:* xem mục gốc toạ độ ngay dưới trước, rồi
  tới *cần gạt* của riêng mục này — cỡ chữ chân trang co theo thân và lên tới 13pt trên tờ tỉ lệ
  1,0, là to với chân trang; cố định ở 9pt sẽ lấy lại chỗ để đưa `FooterDistance` về khoảng
  0,25 inch.
- **Hình học trang: một câu hỏi đã đóng, một đã thu hẹp.**
  - `PageBounds`/`XSize` — **ĐÓNG.** `PageBounds` tính bằng 1/100 inch, còn overload hai tham số
    của `XGraphics.FromGraphics` coi `XSize` là point. Nay dùng overload có `XGraphicsUnit`:
    `new XSize(PageBounds.Width / 100.0, PageBounds.Height / 100.0)` với `XGraphicsUnit.Inch`.
    Gần như chắc chắn không đổi gì lúc chạy, nhưng hết phải đoán. *Triệu chứng nếu vẫn sai:* mọi
    thứ in to khoảng 1,39 lần và tràn khỏi tờ giấy.
  - Gốc toạ độ — **ĐÃ XỬ LÝ, CÒN DƯ.** `e.Graphics` mặc định có gốc ở vùng in được, không ở góc
    tờ giấy, nên cả trang dịch xuống dưới và sang phải 4-5mm; mà cộng với `FooterDistance`
    4,57mm thì chân trang không còn "đúng ranh giới" nữa mà nằm HẲN trong dải không in được —
    mất tên dự án và số trang ở mọi trang của cả sáu tài liệu. Cách xử lý, không P/Invoke và
    không `GetDeviceCaps`: `printDocument.OriginAtMargins = true` cộng
    `DefaultPageSettings.Margins = new Margins(0, 0, 0, 0)`. `OriginAtMargins = true` khiến .NET
    dịch gốc đi `−hardMargin` rồi dịch tiếp theo `DefaultPageSettings.Margins`; cho lề đó bằng 0
    thì còn lại đúng gốc góc-tờ-giấy mà MigraDoc giả định, trong khi MigraDoc vẫn tự áp lề của
    nó. Cho lề 0 an toàn: GDI vẫn cắt theo vùng in được dù lề khai bao nhiêu.
    *Phần dư, nói thẳng:* .NET tính hai bước dịch ấy bằng đơn vị 1/100 inch
    (`GraphicsUnit.Display` của một DC máy in). Nếu PDFsharp đổi `PageUnit` của `Graphics` sang
    `Point` thì con số dịch bị đọc lại ở 1/72 inch mỗi đơn vị, tức lệch theo tỉ số 72/100 và còn
    dư **cỡ 1-2mm thay vì 4,5mm** — chưa chạy thử nên chưa biết dư về phía nào. Dù về phía nào
    thì cũng không xấu hơn hiện trạng: dư 1-2mm vẫn để chân trang ngoài dải không in được, còn
    không bù gì thì chắc chắn mất chân trang. *Triệu chứng nếu còn lệch:* cả trang dịch đều
    1-2mm. *Cần gạt:* đo lề cứng thật rồi bù tay bằng `args.PageSettings.HardMarginX/Y`.
- **Hệ số chiều cao dòng 1,15** trong test bất biến khoảng cách chân trang là ước lượng, không phải
  số đo. Chỉ còn khoảng 0,9pt dư. *Triệu chứng:* test vẫn xanh nhưng chân trang đè lên hàng cuối
  trên giấy. *Cần gạt:* đo chiều cao dòng thật của font serif và thay hệ số.
- **Độ đậm viền và hai mức nền trên giấy thật** (việc "nhìn bằng mắt" số 2 của spec 10/9): vẫn
  treo, đợt này không giải quyết. *Triệu chứng:* viền quá đậm hoặc quá mảnh, hai mức nền không
  phân biệt được. *Cần gạt:* độ dày viền và hai màu nền trong bộ dựng.
- **Cỡ chữ 7,7pt của Sổ Vị trí có đọc nổi trên giấy hay không.** Bản `.xlsx` hôm nay cũng đang thu
  còn 59% nên không phải lỗi mới, nhưng giờ con số đã hiện ra, nó là câu hỏi thật. *Triệu chứng:*
  chữ nhỏ khó đọc. *Cần gạt:* chấp nhận, bớt cột, hoặc sang A3.

## 14. Lỗi tìm được ở đường .xlsx, chưa sửa

- **`FormExcelExporter` thiếu dấu cách sau nhãn.** Nó ghi
  `$"...Đơn vị{Blank(form.DonVi1)}  Số CT{Blank(form.SoChungTu1)}"` mà không có khoảng trắng sau
  nhãn. Với trường trống thì dấu chấm placeholder đọc tự nhiên; với giá trị thật thì in ra
  "Đơn vịBQL" và "Số CTCT-01". Bộ dựng MigraDoc chép nguyên văn thay vì sửa, vì sửa sẽ đổi đầu ra
  `.xlsx` mà người dùng đã duyệt và phá phép so sánh lockstep giữa hai bộ dựng. **Quyết định là của
  người dùng.**
