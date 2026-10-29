# Kế hoạch Cải tổ Giao diện (UI/UX Modernization Plan)

**Ngày lập:** 29/10/2026  
**Mục tiêu:** Lột xác giao diện `ConstructionFinance.Desktop` từ phong cách "phần mềm hành chính thập niên 2010" sang phong cách **Modern Enterprise SaaS / Fintech Desktop** (gọn gàng, thoáng đãng, phân cấp thị giác rõ ràng, hiện đại như Linear / Stripe / Notion / Windows 11 Fluent v2) — **nhưng tuyệt đối không làm hỏng các bất biến hình học và toàn bộ bộ test Headless hiện có.**

---

## 1. Chẩn đoán hiện trạng: Vì sao giao diện hiện tại trông "xấu và cổ"?

Qua khảo sát trực tiếp mã nguồn (`Theme.axaml`, `MainWindow.axaml`, `FinanceWorkbookView.axaml`, `VitriLedgerView.axaml`, `WorkbookSummaryView.axaml`, `FormPageView.axaml`), có **6 nguyên nhân cốt lõi**:

1. **Bảng màu "Xanh Navy hành chính + Vàng cam hổ phách" (`#15355A` + `#E9A23B`):**
   - Sidebar màu xanh biển đậm đặc kết hợp nút "In" màu vàng cam tạo cảm giác giống các phần mềm kế toán WinForms đời 2008–2012.
2. **"Ngợp" vì quá nhiều tầng Header và Toolbar chồng chất:**
   - Khi mở màn *Tài chính dự án*, mắt người dùng phải đi qua **6 tầng** từ trên xuống dưới: Tiêu đề Dự án -> Thanh chọn/sửa Dự án (2 dòng) -> Tiêu đề Sổ Vị trí -> Card Tên vị trí/Nguồn vốn -> Thanh nút Thêm nhóm/gói/đợt + dòng chữ Mẹo dài -> Bảng dữ liệu -> Thanh Tab đáy.
   - Diện tích thực tế cho bảng số liệu bị ép nhỏ lại, nhiều khung viền (`Border`) lồng nhau gây rối mắt.
3. **Bảng dữ liệu (`DataGrid` và `VitriLedgerView`) mang nét bảng tính thô cứng:**
   - Đường kẻ lưới màu xám đậm (`#C8C8C8`), các nút nội dòng (`＋ gói`, `＋ đợt`) dùng khung button xám mặc định nhét chật cứng vào cột "Ngày".
   - Ở các màn *Tổng hợp* và *Quyết toán năm*, `DataGrid` bật `GridLinesVisibility="All"` và `HeadersVisibility="All"` (lộ cột header rỗng bên trái cùng của DataGrid cổ điển), cột số tiền chưa canh phải đồng bộ.
4. **Nút bấm lộn xộn giữa ký tự Unicode và Icon, thiếu phân cấp Primary / Secondary / Danger:**
   - Dùng ký tự chữ `＋`, `−`, `+` ghép thẳng vào chuỗi (`Content="＋ Thêm dự án"`) thay vì `SymbolIcon` đồng bộ.
   - Nút `Xóa dự án`, `Xóa Vị trí` nằm ngay cạnh nút `Thêm` với cùng kiểu khung viền, vừa kém thẩm mỹ vừa dễ bấm nhầm.
5. **Typography thiếu chiều sâu:**
   - Phụ thuộc hoàn toàn vào font mặc định mà chưa tinh chỉnh `FontFeatureSettings="tnum"` (chữ số thẳng hàng dọc cho kế toán), thiếu badge/pill màu cho các khái niệm như `NSNN` / `Ngoại hối`.
6. **Khung xem Biểu mẫu (`FormPageView`) trông như trình xem văn bản cũ:**
   - Tờ giấy biểu mẫu đặt trên nền xám xanh `#E4E9F1` với viền đơn `#B0B0B0` mà thiếu đổ bóng trang giấy (`BoxShadow` kiểu trình xem PDF hiện đại) và thanh công cụ Zoom thiết kế rời rạc.

---

## 2. Nguyên tắc "Bất khả xâm phạm" (Bảo toàn Test & Nghiệp vụ)

Dự án hiện có bộ test Headless Avalonia kiểm tra rất chặt phần hình học. Khi cải tổ UI, **BẮT BUỘC giữ nguyên các bất biến sau**:

1. **Hình học lưới Sổ Vị trí (`VitriLedgerCellLayoutTests` & `VitriLedgerViewTests`):**
   - Giữ nguyên `ItemsControl#RowsHost` với `ItemContainerTheme` (`Padding=0, Margin=0, HorizontalAlignment=Left`).
   - Giữ nguyên cơ chế 14 cột `ColumnWidths` chia sẻ giữa `HeaderColumnsGrid`, dòng `TỔNG CỘNG`, các dòng `Group`/`Package`/`Entry`, và 2 lớp overlay `Border.colDivider` (`Width=1, HorizontalAlignment=Right`).
   - Giữ nguyên vùng bấm `ContentPresenter` phủ kín 100% chiều rộng cột của mọi `TextBox` nhập liệu và cơ chế `Padding="10,4,6,4"` / `26,4,6,4"` / `42,4,6,4` cho cột `Nội dung`.
2. **Hình học tờ Biểu mẫu (`FormSheetControlTests`):**
   - Không thay đổi công thức tính tọa độ `Canvas` (`w * 7.0 + 5.0` và `pt * 96.0 / 72.0`), không đổi tên `x:Name="SheetScroll"`, `x:Name="ZoomHost"`, `x:Name="SheetHost"`, và giữ nguyên cách đặt các `TextBox` / `Thumb` bên trong tờ giấy.
3. **Tên Control & Command phục vụ Test:**
   - Giữ nguyên các `x:Name` đang được gọi trong test (`Nav`, `NavFinanceWorkbook`, `PrintButton`, `ViTriTabs`, `PageHost`, `GridRoot`, `HeaderColumnsGrid`, `RowsHost`, `ResetLayoutButton`, `ZoomOutButton`, `ZoomResetButton`, `ZoomInButton`).

---

## 3. Lộ trình Cải tổ Giao diện (Chia làm 5 Giai đoạn)

### Giai đoạn 1: Hệ thống Thiết kế & Bảng màu mới (`Styles/Theme.axaml` & `App.axaml`)
Chuyển từ tông "Xanh hành chính + Cam đất" sang tông **Slate & Indigo/Royal Blue hiện đại**:

* **Bảng màu (Color Palette):**
  * **Nền ứng dụng (`AppBackgroundBrush`):** `#F8FAFC` (Slate 50 — sáng, sạch, dịu mắt hơn `#F4F6FA`).
  * **Nền Card / Bề mặt (`CardBackgroundBrush`):** `#FFFFFF` với viền mảnh `#E2E8F0` (Slate 200) và bóng đổ mềm `0 1 3 0 #0D0F172A, 0 4 12 0 #080F172A`.
  * **Thanh điều hướng bên trái (`NavPaneBrush`):** Chuyển sang **Dark Slate hiện đại (`#0F172A`)** (hoặc tùy chọn Sidebar sáng `#FFFFFF` tách biệt bằng viền `#E2E8F0`). Điểm nhấn mục đang chọn dùng xanh dương sáng `#38BDF8` hoặc `#3B82F6` thay cho màu vàng cam `#E9A23B`.
  * **Màu chủ đạo (`BrandPrimaryColor`):** `#2563EB` (Modern Royal Blue) / Hover `#1D4ED8`, nền chọn nhạt (`AccentSoftBrush`) `#EFF6FF`.
  * **Màu ngữ nghĩa (Semantic Colors):**
    * Danger: Chữ `#DC2626`, nền nút phụ `#FEF2F2`, viền `#FECACA`.
    * Success: `#16A34A` / nền `#DCFCE7`.
    * Warning: `#D97706` / nền `#FEF3C7`.
* **Hệ thống Nút bấm (Button Hierarchy):**
  * `Button.toolbar` (Secondary): Nền trắng `#FFFFFF`, viền `#CBD5E1`, chữ `#334155`, bo góc `6px`, khi hover nền `#F8FAFC`.
  * `Button.toolbar.accent` (Primary): Nền `#2563EB`, chữ trắng, bóng nhẹ, nổi bật rõ cho hành động chính (`Lưu tất cả`, `Xuất Excel`).
  * `Button.toolbar.danger` (Destructive): Nền trong suốt hoặc `#FFF5F5`, chữ `#DC2626`, khi hover mới hiện nền `#FEE2E2` — tránh gây nhiễu thị giác khi đứng cạnh nút chính.
  * `Button.inlineChip` (Dành cho nút `+ gói`, `+ đợt` trong lưới): Nhỏ gọn như một chiếc nhãn (pill), viền nét đứt hoặc nền `#F1F5F9` nhẹ nhàng, chỉ đậm lên khi rê chuột vào dòng.

---

### Giai đoạn 2: Tinh gọn Khung cửa sổ chính & Thanh điều hướng (`MainWindow.axaml`)
* **Sidebar (`NavigationView`):**
  * Tăng độ thoáng của các mục menu (`Margin`, `CornerRadius="8"` cho từng `NavigationViewItem`).
  * Định dạng lại các tiêu đề nhóm (`Nghiệp vụ`, `Hệ thống`) chữ in hoa nhỏ (`FontSize="11"`, `LetterSpacing`, màu `#64748B`).
* **Nút In & Xuất dữ liệu (`PrintButton`):**
  * Thay màu vàng cam nặng nề bằng thiết kế nút hành động hiện đại ở đáy Sidebar (nền `#1E293B` viền `#334155` với icon sáng, hoặc xanh `#2563EB` đồng bộ thương hiệu).
* **Thanh trạng thái đáy (`StatusBar`):**
  * Thu gọn thanh trạng thái dưới đáy cửa sổ (cao `28px`), thêm chấm tròn trạng thái màu xanh lá nhẹ khi sẵn sàng, font `11.5px`.

---

### Giai đoạn 3: Thu gọn "6 tầng Header" ở Màn Tài chính dự án (`FinanceWorkbookView` & `VitriLedgerView`)
Đây là thay đổi mang lại hiệu quả thị giác lớn nhất — **giải phóng 120px–150px chiều cao màn hình** cho bảng số liệu:

1. **Gộp Header Dự án & Thanh chọn Dự án (`FinanceWorkbookView.axaml`):**
   - Thay vì 1 tiêu đề lớn + 1 khung 2 dòng rời rạc, thiết kế lại thành **1 dải Project Context Bar duy nhất, gọn và sang**:
     - Bên trái: Chọn Dự án (`ComboBox` lớn, chữ đậm) + Ô sửa nhanh `Tên dự án` & `Mã dự án` nằm gọn trên cùng một hàng (hoặc tích hợp tinh tế).
     - Bên phải: Nhóm nút quản lý Dự án / Vị trí được gom cụm hợp lý (`Thêm dự án`, `Thêm Vị trí`, các nút `Xóa` đặt gọn với icon thùng rác thay vì chữ dài, và nút chính `Lưu tất cả (Ctrl+S)` nổi bật ở ngoài cùng bên phải).
     - Thay toàn bộ ký tự chữ `＋` bằng `<ui:SymbolIcon Symbol="Add"/>` chuẩn.
2. **Gộp thông tin Vị trí vào cùng thanh công cụ của Sổ Vị trí (`VitriLedgerView.axaml`):**
   - **Bỏ tiêu đề lặp lại** *"Sổ theo Vị trí — Nhóm chi phí › Gói thầu › Đợt"* (vì người dùng đã biết mình đang ở tab Vị trí nào).
   - Đưa 2 ô `Tên vị trí` và `Nguồn vốn` nằm chung trên một thanh Header/Toolbar hiện đại cùng với các nút `Thêm nhóm`, `Thêm gói`, `Thêm đợt`, `Xóa dòng`, `Xuống dòng`.
   - Biến dòng chữ *"Mẹo: chọn 1 dòng đợt rồi Ctrl+V..."* dài ngoằng thành một biểu tượng trợ giúp nhỏ (`?` hoặc icon bóng đèn có `ToolTip`), giúp thanh công cụ thoáng sạch.
3. **Làm mới dải Tab đáy (`TabControl#ViTriTabs`):**
   - Thiết kế lại dải tab đáy theo phong cách **Modern Segmented / Pill Tabs** (như Airtable / Notion Database tabs): nền thanh tab `#F1F5F9`, tab đang chọn nổi lên như một thẻ trắng bo góc nhẹ có bóng mờ và chấm màu phân biệt giữa *Tab Vị trí*, *Tab Tổng hợp/Quyết toán*, và *Tab Biểu mẫu (Thông tri, KS chi, GĐNTT)*.

---

### Giai đoạn 4: Làm đẹp Lưới Sổ Vị trí (`VitriLedgerView.axaml`) mà không chạm vào hình học cột
Giữ nguyên 100% cấu trúc `ColumnWidths` và `Bounds` để pass mọi test trong `VitriLedgerCellLayoutTests`, chỉ nâng cấp lớp áo thị giác:

* **Màu đường kẻ lưới (`LedgerGridLineBrush`):** Đổi từ xám đục `#C8C8C8` sang xám lam mảnh hiện đại `#E2E8F0` (biên ngang) và `#E8EEF5` (biên dọc) — bảng lập tức bớt cảm giác "nhốt trong chuồng sắt".
* **Phân cấp màu sắc 4 loại dòng rõ ràng, dễ đọc:**
  * **Header cột:** Nền `#F8FAFC`, chữ `#475569` in đậm vừa phải, chiều cao thoáng (`38px`), vạch kéo cột (`Thumb.colGrip`) hiện vạch xanh `#2563EB` mượt khi rê chuột.
  * **Dòng `TỔNG CỘNG` (Frozen):** Nền `#EFF6FF` (xanh rất nhạt) hoặc `#F1F5F9` với viền dưới `2px` màu `#94A3B8`, chữ số đậm nổi bật.
  * **Dòng `Nhóm chi phí` (`Group`):** Nền `#F1F5F9`, chữ đậm màu `#0F172A`, nút `+ gói` thiết kế dạng chip nhỏ tinh tế.
  * **Dòng `Gói thầu` (`Package`):** Nền `#FAFBFD`, ô *Nhà thầu / Số TK* hiển thị gọn như 2 dòng thông tin có icon/nhãn mờ tinh tế, nút `+ đợt` dạng chip.
  * **Dòng `Đợt thanh toán` (`Entry`):** Nền trắng `#FFFFFF`, khi chọn dòng (`rowSelectedOverlay`) phủ màu `#2563EB` với `Opacity="0.08"` dịu mắt.
* **Canh lề và Số học:** Bật `FontFeatureSettings="tnum"` cho toàn bộ các ô số tiền để các hàng đơn vị, chục, trăm, nghìn luôn thẳng tắp từ trên xuống dưới.

---

### Giai đoạn 5: Chuẩn hóa các màn Tổng hợp, Quyết toán, Biểu mẫu & Quyết định
1. **Màn Tổng hợp (`WorkbookSummaryView`, `ProjectTongHopView`) & Quyết toán năm (`YearSettlementView`):**
   - Bọc tất cả `DataGrid` vào trong `Border.tableCard` bo góc, có bóng đổ nhẹ đồng bộ với màn Quyết định.
   - Đổi `HeadersVisibility="All"` thành `HeadersVisibility="Column"` (xóa bỏ cột ô trống thừa thãi bên trái cùng của DataGrid cổ điển) và đổi `GridLinesVisibility="All"` thành `Horizontal` (hoặc kẻ dọc cực nhạt).
   - Canh phải (`TextAlignment="Right"`) toàn bộ các cột số tiền (`Giá trị HĐ`, `Tạm ứng`, `TT KLHT`, `Thu hồi`, `NSNN`, `Ngoại hối`, `Tổng đã giải ngân`) để đúng chuẩn kế toán.
2. **Màn Biểu mẫu (`FormPageView.axaml` & `FormSheetControl.cs`):**
   - Trong `FormPageView.axaml`: Gom cụm 3 nút Zoom (`−`, `100%`, `+`) thành một cụm **Segmented Control** liền mạch; dùng `SymbolIcon` cho nút Chọn đợt / Xuất Excel / Đặt lại cỡ ô.
   - Tạo hiệu ứng **"Tờ giấy A4 đặt trên bàn làm việc"** (배경 `#E2E8F0`, tờ giấy trắng có `BoxShadow="0 4 20 0 #1F0F172A"` sang trọng như Adobe Reader / Word hiện đại).
   - *Lưu ý:* Không đổi `Padding="24"` hay `BorderThickness="1"` của `FormSheetControl` nếu làm ảnh hưởng đến test `Sheet_is_centered_in_the_viewer`, chỉ làm mượt màu nền ô nhập (`#F0F7FF`) và thanh toolbar.
3. **Các hộp thoại (`ConfirmDialog`, `FormDotPickerView`, `DecisionEditDialog`, ...):**
   - Đồng bộ padding, bo góc `12px`, thanh nút hành động ở đáy hộp thoại tách biệt bằng nền `#F8FAFC` và viền trên `#E2E8F0`.

---

## 4. Kiểm chứng & Nghiệm thu (Verification Gate)

Sau mỗi giai đoạn thực thi:
1. Chạy `dotnet build ConstructionFinance.sln -warnaserror` -> **0 Warning, 0 Error**.
2. Chạy `dotnet test ConstructionFinance.sln` -> **Toàn bộ test xanh 100%** (đặc biệt là `VitriLedgerCellLayoutTests`, `VitriLedgerViewTests`, `FormSheetControlTests`, `MainWindowNavigationTests`).
3. Chạy thử ứng dụng thực tế và kiểm tra trực quan từng màn hình.
