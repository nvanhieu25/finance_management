# Kế hoạch Cải tổ Giao diện (UI/UX Modernization Plan)

**Ngày lập:** 29/10/2026  
**Mục tiêu:** Lột xác giao diện `ConstructionFinance.Desktop` từ phong cách "phần mềm hành chính thập niên 2010" sang phong cách **Modern Enterprise SaaS / Fintech Desktop** (gọn gàng, thoáng đãng, phân cấp thị giác rõ ràng, hiện đại như MISA AMIS / Linear / Stripe / Windows 11 Fluent v2) — **nhưng tuyệt đối không làm hỏng các bất biến hình học và toàn bộ bộ test Headless hiện có.**

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
   - Ở các màn *Tổng hợp* và *Quyết toán năm*, `DataGrid` bật `GridLinesVisibility="All"` và `HeadersVisibility="All"` (lộ cột header rỗng bên trái cùng của DataGrid cổ điển).
4. **Nút bấm lộn xộn giữa ký tự Unicode và Icon, thiếu phân cấp Primary / Secondary / Danger:**
   - Dùng ký tự chữ `＋`, `−`, `+` ghép thẳng vào chuỗi (`Content="＋ Thêm dự án"`) thay vì `SymbolIcon` đồng bộ.
   - Nút `Xóa dự án`, `Xóa Vị trí` nằm ngay cạnh nút `Thêm` với cùng kiểu khung viền, vừa kém thẩm mỹ vừa dễ bấm nhầm.
5. **Typography thiếu chiều sâu:**
   - Phụ thuộc hoàn toàn vào font mặc định mà chưa tinh chỉnh `FontFeatures="tnum"` (chữ số thẳng hàng dọc cho kế toán).
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
Chuyển từ tông "Xanh hành chính + Cam đất" sang tông **Slate & Royal Blue hiện đại**:

* **Bảng màu (Color Palette):**
  * **Nền ứng dụng (`AppBackgroundBrush`):** `#F8FAFC` (Slate 50 — sáng, sạch, dịu mắt hơn `#F4F6FA`).
  * **Nền Card / Bề mặt (`CardBackgroundBrush`):** `#FFFFFF` với viền mảnh `#E2E8F0` (Slate 200) và bóng đổ mềm.
  * **Thanh điều hướng bên trái (`NavPaneBrush`):** Chuyển sang **Dark Slate hiện đại (`#0F172A`)**. Điểm nhấn mục đang chọn dùng xanh dương sáng `#38BDF8` thay cho màu vàng cam `#E9A23B`.
  * **Màu chủ đạo (`BrandPrimaryColor`):** `#2563EB` (Modern Royal Blue) / Hover `#1D4ED8`, nền chọn nhạt (`AccentSoftBrush`) `#EFF6FF`.
  * **Màu ngữ nghĩa (Semantic Colors):**
    * Danger: Chữ `#DC2626`, nền nút phụ `#FEF2F2`, viền `#FECACA`.
    * Success: `#16A34A`.
    * Warning: `#D97706`.
* **Hệ thống Nút bấm (Button Hierarchy):**
  * `Button.toolbar` (Secondary): Nền trắng `#FFFFFF`, viền `#CBD5E1`, chữ `#334155`, bo góc `6px`, khi hover nền `#F8FAFC`.
  * `Button.toolbar.accent` (Primary): Nền `#2563EB`, chữ trắng, nổi bật rõ cho hành động chính (`Lưu tất cả`, `Xuất Excel`, `Thêm nhóm`).
  * `Button.toolbar.danger` (Destructive): Nền trắng `#FFFFFF`, viền hồng nhạt `#FECACA`, chữ `#DC2626`, khi hover hiện nền `#FEF2F2`.
  * `Button.inlineAction` (Dành cho nút `＋ Gói`, `＋ Đợt` trong lưới): Nhỏ gọn dạng chip, viền mảnh `#CBD5E1`, chữ xanh `#2563EB`.

---

### Giai đoạn 2: Tinh gọn Khung cửa sổ chính & Thanh điều hướng (`MainWindow.axaml`)
* **Sidebar (`NavigationView`):**
  * Thêm `PaneHeader` thương hiệu ứng dụng (`TÀI CHÍNH DỰ ÁN — Quản lý & Quyết toán`) với icon đại diện.
  * Tăng độ thoáng của các mục menu.
* **Nút In & Xuất báo cáo (`PrintButton`):**
  * Thay màu vàng cam nặng nề bằng thiết kế nút Primary Royal Blue (`#2563EB`) đồng bộ thương hiệu ở đáy Sidebar.
* **Thanh trạng thái đáy (`StatusBar`):**
  * Thu gọn thanh trạng thái dưới đáy cửa sổ, thêm chấm tròn trạng thái màu xanh lá khi sẵn sàng và gợi ý phím tắt nhanh (`Ctrl+S`, `Ctrl+V`) bên góc phải.

---

### Giai đoạn 3: Thu gọn "6 tầng Header" ở Màn Tài chính dự án (`FinanceWorkbookView` & `VitriLedgerView`)
Giải phóng ~130px chiều cao màn hình cho bảng số liệu:

1. **Gộp Header Dự án & Thanh chọn Dự án (`FinanceWorkbookView.axaml`):**
   - Thiết kế lại thành **1 dải Project Context Bar (`Border.contextBar`) duy nhất, gọn và sang**:
     - Hàng 1: Nhãn Dự án có vạch nhấn màu xanh + `ComboBox` chọn dự án + cụm nút `Dự án mới`, `Xóa DA`, `Thêm Vị trí`, `Xóa Vị trí` (với `SymbolIcon` đồng bộ) + nút chính `Lưu tất cả (Ctrl+S)` nổi bật ở ngoài cùng bên phải.
     - Hàng 2: Ngăn cách bằng đường kẻ mảnh `#F1F5F9`, chứa ô sửa nhanh `Tên dự án` và `Mã dự án`.
2. **Gộp thông tin Vị trí vào cùng thanh công cụ của Sổ Vị trí (`VitriLedgerView.axaml`):**
   - Bỏ tiêu đề lặp lại *"Sổ theo Vị trí — Nhóm chi phí › Gói thầu › Đợt"*.
   - Đưa 2 ô `Vị trí` và `Nguồn vốn` nằm chung trên một thanh `toolbarStrip` ngay đầu `tableCard` cùng với các nút `Thêm nhóm`, `Thêm gói`, `Thêm đợt`, `Xuống dòng`, `Xóa dòng`.
   - Biến dòng chữ *"Mẹo: chọn 1 dòng đợt rồi Ctrl+V..."* dài ngoằng thành một huy hiệu `? Mẹo` gọn gàng có `ToolTip` chi tiết.
3. **Làm mới dải Tab đáy (`TabControl#ViTriTabs`):**
   - Thiết kế lại dải tab đáy theo phong cách **Modern Segmented Pill Tabs**: nền khay `#F1F5F9` bo góc `8px`, tab đang chọn nổi lên như một thẻ trắng `#FFFFFF` bo góc `6px` có viền và đổ bóng nhẹ.

---

### Giai đoạn 4: Làm đẹp Lưới Sổ Vị trí (`VitriLedgerView.axaml`) mà không chạm vào hình học cột
Giữ nguyên 100% cấu trúc `ColumnWidths` và `Bounds` để pass mọi test trong `VitriLedgerCellLayoutTests`:

* **Màu đường kẻ lưới (`LedgerGridLineBrush`):** Đổi từ xám đục `#C8C8C8` sang xám lam mảnh hiện đại `#E2E8F0`.
* **Phân cấp màu sắc 4 loại dòng rõ ràng, dễ đọc:**
  * **Header cột:** Nền `#F8FAFC`, chữ `#475569` SemiBold, vạch kéo cột (`Thumb.colGrip`) hiện vạch xanh `#2563EB` khi rê chuột.
  * **Dòng `TỔNG CỘNG` (Frozen):** Nền `#F1F5F9` với viền dưới `2px` màu `#CBD5E1`, ô `Tổng đã GN` nhấn màu xanh chủ đạo `#2563EB`.
  * **Dòng `Nhóm chi phí` (`Group`):** Nền xanh dịu `#EFF6FF`, chữ đậm màu `#1E3A8A`, nút `＋ Gói` thiết kế dạng chip `inlineAction`.
  * **Dòng `Gói thầu` (`Package`):** Nền `#F8FAFC`, ô *Nhà thầu / Số tài khoản* nền trong suốt tinh tế, nút `＋ Đợt` dạng chip `inlineAction`.
  * **Dòng `Đợt thanh toán` (`Entry`):** Nền trắng `#FFFFFF`, khi chọn dòng (`rowSelectedOverlay`) phủ màu `#2563EB` với `Opacity="0.09"`.
* **Số học kế toán:** Bật `FontFeatures="tnum"` (tabular numerals) cho các ô số tiền để các con số thẳng hàng dọc.

---

### Giai đoạn 5: Chuẩn hóa các màn Tổng hợp, Quyết toán, Biểu mẫu
1. **Màn Tổng hợp (`WorkbookSummaryView`, `ProjectTongHopView`) & Quyết toán năm (`YearSettlementView`):**
   - Bọc tất cả `DataGrid` vào trong `Border.tableCard` bo góc có `toolbarStrip` đồng bộ.
   - Đổi `HeadersVisibility="All"` thành `HeadersVisibility="Column"` (xóa bỏ cột ô trống thừa bên trái cùng của DataGrid cổ điển).
2. **Màn Biểu mẫu (`FormPageView.axaml`, `FormDotPickerView.axaml` & `FormSheetControl.cs`):**
   - Trong `FormPageView.axaml`: Bọc vào `tableCard`, gom cụm 3 nút Zoom (`−`, `100%`, `+`) thành một cụm **Segmented Control** liền mạch.
   - Tạo hiệu ứng **"Tờ giấy A4 nổi trên mặt bàn"** (`BoxShadow` đa tầng trên `FormSheetControl`).
   - Giữ nguyên tuyệt đối `Padding="24"` và `BorderThickness="1"` để bảo toàn bộ test hình học.
