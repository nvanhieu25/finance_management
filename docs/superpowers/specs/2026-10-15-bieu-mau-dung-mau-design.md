# Ba biểu mẫu dựng đúng Mẫu.xlsx — Thiết kế

**Ngày:** 2026-10-15 · **Trạng thái:** đã duyệt; đã triển khai theo `docs/superpowers/plans/2026-10-15-bieu-mau-dung-mau.md` (chờ user nghiệm thu bằng mắt)
**Thay thế:** phần *trình bày* của `2026-08-13-ba-bieu-mau-design.md`. Phần *cách tính số* của spec đó giữ nguyên.

## 1. Vì sao có đợt này

User mở thử ba tab Thông tri, KS chi, GĐNTT (2026-10-15) và nói:

> Mục thông tri rất khó thao tác, thậm chí còn không thể sửa tên đơn vị ở góc trên cùng bên trái. Tôi đề nghị bạn hãy dựa đúng theo mẫu mà tôi đã gửi, chúng ta chỉ cần tự động hóa phần nhập số liệu, còn lại không tự biên tự diễn. Cả hai phần sau cũng thế, giao diện mất đối xứng và rất khó thao tác.

**Nguyên tắc của cả đợt:** số liệu tự động; mọi thứ khác **y hệt `reference/Mẫu.xlsx`**.

## 2. App đã lệch mẫu ở đâu (đo 2026-10-15)

| Mẫu.xlsx | App trước đợt này |
|---|---|
| Thông tri A1, A2 và GĐNTT A1, A2: ô để tự gõ tên đơn vị | Lấy cứng từ Cài đặt, không sửa được trên biểu mẫu |
| KS chi dòng 4: `Đơn vị…..` · `Tạm ứng` · `TTKLHT` · `theo nội dung sau:` | Bỏ mất cả dòng |
| GĐNTT dòng 14: `- Số tiền đề nghị:` · `Tạm ứng` · `TTKLHT` · `đến kỳ này:` | Viết lại thành `(tạm ứng, thanh toán KLHT) đến kỳ này` |
| GĐNTT D3: `Hải phòng, ngày … tháng … năm` | Bỏ địa danh |
| Cả ba sheet in **A4 khổ dọc** | GĐNTT in khổ **ngang** |
| Mỗi ô một cỡ chữ: 10 · 10,5 · 11 · 12 · 13 · 14pt, Times New Roman | Ép mọi ô về 13pt |
| Bề rộng cột riêng (Thông tri A=6,7 · G=35,7 · H=17,1; GĐNTT A=13 · B=11,3 · H=0,9 · I=18,6; KS chi B=23,7 · C=19,4 · D=20,1 · E=18,6) | Tự đặt: 9/52/18, 26+16×8, 34/14/18 |
| Chiều cao từng dòng (vd Thông tri dòng đợt 47,2pt, GĐNTT dòng 18 81,8pt) | Bỏ qua |
| Màn hình: một tờ biểu mẫu | Bảng chọn đợt chiếm nửa trên, biểu mẫu bị đẩy xuống và lệch sang trái |

**Gốc rễ:** mỗi biểu mẫu được vẽ ba lần bằng ba bộ mã viết tay riêng — màn hình (Avalonia), xuất Excel (`FormExcelExporter`), in (`MigraDocFormSheets`). Ba biểu mẫu × ba đầu ra = **chín bản chép tay** của cùng một mẫu. Lệch là hệ quả tất yếu.

## 3. Quyết định đã chốt (2026-10-15)

| Vấn đề | Chốt |
|---|---|
| Hình dạng màn hình | **Lưới y hệt sheet Excel**: cùng hàng, cột, ô gộp, bề rộng, cỡ chữ, đường kẻ |
| Ô nào sửa được | **Theo đúng ghi chú trong mẫu** — xem §5.2 |
| Chọn đợt | **Hộp thoại riêng**; tab chỉ còn biểu mẫu |
| Tên đơn vị | **Dùng chung**: sửa trên biểu mẫu nào cũng đổi ở Cài đặt, mọi dự án, mọi biểu mẫu |
| Cách dựng | **Một bản gốc cho mỗi biểu mẫu**, ba bộ vẽ (màn hình / Excel / in) cùng đọc |
| Năm in sẵn trong mẫu ("năm 2026") | Lấy **năm hiện tại** |
| `Hải phòng` ở GĐNTT D3 | Là địa danh thật — chữ có sẵn, **sửa được** |
| Ghi chú của user trong mẫu (`Tự nhập liệu`, `( có thể gõ…)`…) | Là hướng dẫn, **không in** |
| Bảng nội dung giữa Thông tri | **Tối thiểu bằng số dòng của mẫu** (dòng 15–23); nhiều đợt thì thêm dòng |
| Mục "Thông tri" ở khung bên trái | **Mở thẳng tab Thông tri của dự án đang chọn** |

## 4. Phạm vi

**Trong đợt này:**
- Dựng lại ba tab theo bản gốc.
- Hộp thoại Chọn đợt.
- Xuất Excel và In ra giấy của ba biểu mẫu từ cùng bản gốc.
- Tên đơn vị nối Cài đặt; chỗ lưu cho mọi ô tự nhập.
- Mục nav "Thông tri" bên trái trỏ sang tab mới.

**Giữ nguyên, không đụng:**
- Toàn bộ cách tính số: `ThongTriBuilder`, `KsChiBuilder`, `GdnttBuilder`, `FormRowFlattener`, `VietnameseNumberToWords`, kể cả các lệch mẫu đã duyệt ở 7D: lũy kế theo gói thầu; giá trị HĐ cộng phụ lục; GĐNTT một gói thầu, chỉ Quỹ dự trữ ngoại hối.
- Mỗi dự án một bộ biểu mẫu, không lưu lịch sử phiếu.
- Cách nhớ các đợt đã tick, như hiện nay.

**Ngoài phạm vi, có chủ ý:**
- Sổ Vị trí, Tổng hợp, Quyết toán năm.
- Lỗi cắt chữ số cuối ở cột tiền tab Tổng hợp (ghi nhận 2026-10-15, sửa riêng).
- 7E; KH vốn / Vốn / QĐ.

## 5. Thiết kế

### 5.1 Bản gốc: mỗi biểu mẫu một định nghĩa

Mỗi sheet trong Mẫu.xlsx được chép **một lần, bằng tay, trong mã** thành một định nghĩa thuần dữ liệu ở `Application/Forms`. Định nghĩa gồm:
- khổ giấy (A4 dọc cho cả ba);
- bề rộng từng cột theo đơn vị Excel;
- chiều cao từng dòng (pt);
- danh sách ô: địa chỉ, vùng gộp, loại ô (§5.2), chữ, kiểu (cỡ chữ, đậm, canh ngang/dọc, xuống dòng, viền);
- với Thông tri, một **vùng lặp** cho bảng nội dung (§5.6).

Không đọc tệp mẫu lúc chạy: Mẫu.xlsx có ghi chú nội bộ, tham chiếu ngoài hỏng (`[3]!VND`), và năm viết cứng.

### 5.2 Năm loại ô, lấy từ chính ghi chú trong mẫu

| Loại | Nhận diện trong mẫu | Trên màn hình |
|---|---|---|
| **Chữ cố định** | chữ không phải chỗ điền (`THÔNG TRI`, tiêu đề cột, `Cộng`…) | khoá |
| **Tự nhập — chữ** | chấm chấm, hoặc có ghi `Tự nhập liệu` / `Nhập liệu` / `(Tự nhập)` | bấm vào gõ |
| **Tự nhập — số** | ô trống trong cột số, không công thức, và app **không** tự tính (KS chi C14 Các năm trước, C15 Năm nay, C16 Thuế GTGT, C19 Số từ chối; GĐNTT H24 Chuyển tiền bảo hành, H25 Chuyển tiền tạm giữ) | gõ số, hiện dấu chấm phân nghìn |
| **Tự tính** | ô có công thức | nền xám nhạt, khoá |
| **Tự tính, cho gõ đè** | ô ghi `hoặc tự nhập liệu`: GĐNTT tên dự án (C8) và Số dư tạm ứng (H13) | điền sẵn; gõ đè được; ô đã gõ đè có dấu nhận biết và lệnh **"Trở về tự động"** |

Một số ô trong mẫu gộp *nhãn* và *ghi chú* trong cùng một chữ. Bản gốc tách thành nhãn (in ra) và ô tự nhập (ghi chú không in):

| Ô mẫu | In ra | Ô tự nhập |
|---|---|---|
| GĐNTT A6 `Kính gửi: Tự nhập liệu` | `Kính gửi:` | nội dung kính gửi |
| GĐNTT A9 `- Chủ đầu tư/Ban QLDA:  Nhập liệu` | `- Chủ đầu tư/Ban QLDA:` | tên chủ đầu tư |
| GĐNTT A10 `- Căn cứ Quyết định số (nằm trong nội dung Quyết định);` | `- Căn cứ Quyết định số` | số quyết định |
| GĐNTT A11, A12 `… (tự nhập liệu)` | phần trước ngoặc | số hợp đồng / số giấy đề nghị |
| GĐNTT B29 `Ngân hàng A ( Tự nhập)` | — | tên ngân hàng |
| GĐNTT C8, H13; M13 | — | ghi chú, không in |

**Số app đã tự tính thì vẫn tự tính, kể cả khi mẫu để trống ô đó.** Đây đúng là "tự động hoá phần nhập số liệu": ví dụ GĐNTT H23 *Thanh toán để thu hồi tạm ứng* trong mẫu để trống, nhưng app biết số này từ các đợt nên ô là **tự tính**. Quy tắc phân loại vì vậy là: có công thức trong mẫu **hoặc** app đang tính sẵn → tự tính; còn lại trong cột số → tự nhập số.

Ô **Tự tính** lấy giá trị từ các builder hiện có qua một bảng ánh xạ khoá → giá trị. Các ô chữ tự tính trong mẫu: tên dự án ở dòng Dự án Thông tri (G15), tên gói thầu (GĐNTT A18), tên nhà thầu (C27), số tài khoản (D28), số tiền bằng chữ. KS chi B22 (Ghi chú) trong mẫu là công thức `='Thông tri'!G17` — nội dung dòng đợt đầu tiên của Thông tri. Ô này là **tự tính, cho gõ đè**.

Mục lục ngân sách Thông tri (A15–F15) là **tự nhập**, điền sẵn đúng giá trị mẫu: `10 · 11 · 9300 · 9301 · 0 · 0`.

### 5.3 Lưu chữ tự nhập

- **Tên đơn vị** (Thông tri A1/A2, GĐNTT A1/A2) đọc/ghi thẳng khoá `don_vi_cap_tren` / `don_vi_co_so` ở `app_settings`, dùng chung với màn Cài đặt.
- **Mọi ô tự nhập khác**, kể cả số tự nhập và giá trị gõ đè, lưu vào một bảng mới theo **(dự án, biểu mẫu, khoá ô)**. Khoá là tên ổn định trong bản gốc (vd `SoThongTri`), không phải địa chỉ ô, để dời ô không mất chữ. Thêm ô mới vào bản gốc không cần migration.
- Lưu cùng Ctrl+S như hiện nay.
- **Migration:** tạo bảng mới, chép mọi dòng có sẵn của `finance_thongtri_decl` / `finance_kschi_decl` / `finance_gdntt_decl` sang, rồi xoá ba bảng cũ. Ngày 2026-10-15 cả ba bảng có **0 dòng** trong `app.db` thật và trong bản sao lưu 01/10. `App.axaml.cs` chạy `MigrateAsync` mỗi lần khởi động nên việc xoá bảng không đảo ngược được: **sao lưu `app.db` ngay trước lần chạy đầu**.
- **Ô tự nhập còn trống:** màn hình hiện chữ của mẫu dạng mờ; khi in và xuất Excel thì **in đúng chữ chấm chấm của mẫu** để điền tay. Các chỗ `năm 2026` thay bằng năm hiện tại.

### 5.4 Ba bộ vẽ, một bản gốc

| Đầu ra | Thay cho | Ghi chú |
|---|---|---|
| Màn hình | `ThongTriFormView` / `KsChiFormView` / `GdnttFormView` | một điều khiển lưới chung, vẽ được mọi bản gốc |
| Xuất Excel | `FormExcelExporter.Write*Sheet` | ghi đúng bề rộng, chiều cao, gộp, cỡ chữ của bản gốc |
| In ra giấy | `MigraDocFormSheets` | dựng `FormGrid` từ bản gốc; cỡ chữ từng ô × tỉ lệ co |

Phép so từng ô giữa bản Excel và bản in (`SheetTextMap`) vẫn chạy cho ba biểu mẫu.

**Kéo theo cho tính năng In:** `PrintPageProfiles.Gdntt` chuyển sang **khổ dọc**, đúng mẫu. Các quyết định riêng cho GĐNTT khổ ngang trong `2026-10-07-in-thang-ra-may-in-design.md` §12 (đo dòng 6–16 xuống dòng, lề 0,4″ để giữ một tờ) phải **đo lại** với khổ dọc và bề rộng cột mới. Theo bề rộng mẫu, GĐNTT rộng ~171mm, vừa 184,6mm của A4 dọc mà không cần co.

### 5.5 Hộp thoại Chọn đợt

- Dùng lại `FormDotPickerViewModel`, mở qua `IDialogService.ShowDialogAsync`.
- Đủ rộng để không cắt chữ ở cột Ngày và cột tiền.
- Lọc tháng/năm, Chọn tất cả, Bỏ chọn.
- Chỉ hiện đợt đúng nguồn vốn: NSNN cho Thông tri; Quỹ dự trữ ngoại hối cho GĐNTT; cả hai cho KS chi.
- **OK** áp dụng và cập nhật biểu mẫu. **Hủy** trả về các đợt đã chọn trước khi mở.
- Thanh nút của tab: **[Chọn đợt (N)…]** · **[Xuất Excel]** · dòng trạng thái (vd `Đã chọn 3 đợt`, hoặc thông báo GĐNTT cần đúng một gói thầu như hiện nay).

### 5.6 Bảng nội dung Thông tri

Mẫu dành dòng 15–23 cho bảng nội dung rồi tới `Cộng` ở dòng 24. Dòng 15 là dòng Dự án, mang Mục lục ngân sách. Mỗi Vị trí là một dòng nhãn (cao 24,9pt). Mỗi đợt là một dòng nội dung (cao 47,2pt).

Bản gốc giữ phần trên dòng 15 và phần từ `Cộng` trở xuống y như mẫu. Vùng giữa sinh ra theo các đợt đã chọn, **tối thiểu 9 dòng** (15–23); ít dòng thì phần còn lại để trống đúng kiểu mẫu, nhiều dòng thì chèn thêm trước `Cộng`.

### 5.7 Mục "Thông tri" ở khung bên trái

Bấm vào thì chuyển sang **Tài chính dự án** và chọn tab **Thông tri** của dự án đang chọn. Màn cũ `ThongTriViewModel` không còn lối vào; xoá mã và các bảng `import_batches` / `project_summaries` / `settlement_by_years` thuộc về 7E.

**Spec 7E phải sửa theo:** quyết định 1 (§3) đổi từ "giữ màn Thông tri" sang "trỏ sang tab Thông tri mới, xoá màn cũ"; bỏ tiêu chí nghiệm thu "màn Thông tri vẫn hiện 15 dòng cũ"; ba bảng trên được xoá thay vì giữ. Tôi đã ghi chú việc này ở đầu spec 7E.

### 5.8 Chỗ lệch có chủ ý so với Mẫu, và chỗ thực tế khác spec

1. **Gộp ô thêm ở GĐNTT: `H13:I13`, `H25:I25`, `D28:I28`.** Cột H của Mẫu rộng 0,14 ký tự nên số đặt ở H13/H25 chỉ hiện `###`; D28 (số tài khoản, căn giữa, rộng một cột) không chứa nổi 13–16 chữ số. Test so Mẫu cho phép đúng ba ngoại lệ này.
2. **Mục lục ngân sách hiện `010 · 011 · 9300 · 9301 · 00 · 00`**, không phải `10 · 11 · 9300 · 9301 · 0 · 0` (§5.2): định dạng ô của Mẫu (`"0"0`, `"0"\1\1`, `00`) mới là chữ hiện ra và in ra.
3. **Hai nét kẻ: thường và `hair`** (KS chi dòng 7–17 kẻ trên/dưới bằng `hair`); **có chữ gạch chân** (`GĐNTT!D2`, `KS chi!A22`). Kiểu ô mang đủ hai thứ này.
4. **Lề giấy giữ của ứng dụng** (0,5″ hai bên), không theo Mẫu (0,45″ / 0″ — lề phải 0 không in được).
5. **Số 0 ở ô tự tính hiện `0`** (đúng `#,##0` của Mẫu); ô số tự nhập còn trống để trống.
6. **KS chi C8:E12 (số theo từng mục) vẫn là ô trống khoá**; chỉ C14, C15, C16, C19 là ô số tự nhập (như §5.2 liệt kê). Muốn gõ số theo mục: thêm một dòng `Tpl.Number(...)` vào bản gốc — không cần migration.
7. **Chữ ghi chú của Mẫu** (`Tự nhập liệu`, `( có thể gõ…)`, `Ngân hàng A ( Tự nhập)`) là gợi ý mờ trên màn hình, không in.
8. **`ThuocKeHoach` (GĐNTT C16:D16)**: Mẫu có ô gộp trống ngay sau nhãn `- Thuộc kế hoạch:` — coi là ô tự nhập chữ.
9. **Bề rộng cột** lấy theo "ký tự" của Excel = (px − 5)/7 (openpyxl đọc bề rộng LƯU trong tệp, đã gồm 5px đệm; script sinh bản gốc đã quy đổi).
10. **Bản in GĐNTT ép MỘT trang theo chiều dọc** (co ~0,9 so với 13pt gốc) vì tờ cao 798pt > 277mm khả dụng; KS chi co nhẹ (~0,98). Đây là phép đo lại mà §5.4/§7 đòi: kết quả — GĐNTT dọc vừa một trang kể cả khi các ô chữ dài.
11. **Bản in: ô chữ tràn sang ô trống bên phải được gộp ô** (mất đường kẻ dọc nằm giữa dải đó), vì MigraDoc xuống dòng trong lòng ô chứ không tràn như Excel; màn hình giữ đường kẻ như Excel.

## 6. Kiểm chứng

**Máy tự kiểm:**
1. **So thẳng với Mẫu.xlsx.** Một test mở `reference/Mẫu.xlsx` và so với bản gốc của từng biểu mẫu:
   - từng chữ cố định;
   - từng vùng gộp;
   - bề rộng cột, chiều cao dòng;
   - phông và cỡ chữ, đậm, canh lề, xuống dòng, viền;
   - khổ giấy, hướng giấy.

   Chỉ bỏ qua đúng các khác biệt đã ghi:
   - ghi chú không in (§5.2);
   - năm hiện tại;
   - vùng lặp của Thông tri (§5.6);
   - ô công thức so theo vị trí và loại, không so chữ.

   Test **đỏ** nếu ai sửa bản gốc lệch khỏi mẫu, hoặc nếu mẫu đổi mà bản gốc không theo. **Điều kiện:** `reference/Mẫu.xlsx` phải được commit vào repo (hiện đang untracked).
2. **Ba đầu ra là một:** màn hình, Excel và bản in dựng từ cùng bản gốc, khớp nhau từng ô.
3. **Cách tính số không đổi:** mọi test số liệu hiện có giữ nguyên và phải xanh.
4. **Thao tác màn hình** (headless):
   - ô tự nhập gõ được, ô tự tính khoá;
   - gõ đè rồi "Trở về tự động" lấy lại giá trị tính;
   - sửa tên đơn vị ở Thông tri thì GĐNTT và Cài đặt thấy;
   - Hủy hộp thoại không đổi lựa chọn;
   - mục nav Thông tri mở đúng tab.
5. **Migration:** chép dữ liệu ba bảng cũ đúng khoá; chạy trên DB rỗng không lỗi.

**User nghiệm thu:** mở app, đặt cạnh Mẫu.xlsx, xem ba biểu mẫu có giống không.

## 7. Rủi ro

- **Xoá ba bảng khai biểu mẫu là không đảo ngược** khi app khởi động. Rủi ro dữ liệu ngày 2026-10-15 bằng 0 (0 dòng), vẫn phải sao lưu trước.
- **GĐNTT đổi sang khổ dọc** làm mất hiệu lực các số đo khổ ngang của đợt in. Đo lại là việc bắt buộc, không phải tuỳ chọn.
- **Nhiều test hiện có viết theo bố cục cũ**: `MigraDocFormSheetTests`, test `FormExcelExporter`, `PrintWorkbookTests`, test view-model 7D. Chúng sẽ được viết lại theo bản gốc, không được nới cho xanh.
