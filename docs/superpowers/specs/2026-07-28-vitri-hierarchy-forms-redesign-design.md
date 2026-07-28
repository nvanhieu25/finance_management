# Thiết kế: Cây phân cấp Vị trí + 3 biểu mẫu tự sinh (theo Mẫu.xlsx mới)

Ngày: 2026-07-28
Trạng thái: Đã duyệt thiết kế (chờ rà soát spec) → sẽ chuyển sang writing-plans
Nguồn tham chiếu: `reference/Mẫu.xlsx` (9 sheet)
Liên quan: `2026-07-17-excel-workbook-unified-project-design.md` (Phase 6A đã merge, commit `7cb1fe4`)

## 1. Bối cảnh & mục tiêu

App quản lý tài chính công trình (desktop, .NET 8 + Avalonia 11 + EF Core 8 + SQLite,
100% free-license — ràng buộc cơ quan nhà nước). Phase 6A đã đưa app về mô hình
**workbook kiểu Excel** với mô hình phẳng **Dự án → Đợt (line item)**.

File mẫu mới `Mẫu.xlsx` mô tả một nghiệp vụ **giàu hơn nhiều**: một dự án được chia
thành nhiều **Vị trí** (các thành phần), mỗi Vị trí có sổ theo dõi ứng/thanh toán
phân theo **nhóm Chi phí → Gói thầu (1 nhà thầu) → Đợt**; đồng thời tự sinh 3 biểu mẫu
nhà nước (Thông tri, Kiểm soát chi, Giấy đề nghị thanh toán) và bảng Quyết toán theo
năm tài chính.

Mục tiêu đợt này: **đào sâu mô hình từ 2 cấp lên 6 cấp**, dựng lưới Sổ theo dõi phân cấp,
tính Quyết toán năm, và tự sinh 3 biểu mẫu. Các sheet kế hoạch vốn (KH vốn / Vốn / QĐ)
để **spec riêng sau**.

## 2. Cây phân cấp đích (6 cấp)

```
Dự án (Project)
 └─ Vị trí (ViTri)                     ← MỚI — "các thành phần" của dự án; mỗi Vị trí = 1 sheet ledger
     └─ Chi phí (CostGroup, tự tạo)    ← MỚI — nhóm: "CHI PHÍ XÂY DỰNG" / "CP TƯ VẤN ĐTXD" / "Khác"…
         └─ Gói thầu (Package)         ← MỚI — mỗi gói: Giá trị HĐ + Phụ lục HĐ + TK ngân hàng + 1 Nhà thầu
             └─ Đợt (LineItem)         ← như hiện tại — Tạm ứng / TT KLHT + Thu hồi; chọn Nguồn NSNN|Ngoại hối
```

**Phạm vi workbook (đã chốt):** 1 workbook = **nhiều Dự án**, mỗi Dự án nhiều Vị trí.

## 3. Mô hình dữ liệu (namespace `ConstructionFinance.Domain.Finance`)

Nguyên tắc giữ nguyên từ 6A: **mỗi thực thể chỉ lưu số liệu nhập thô**; mọi số dẫn xuất
(giải ngân, số dư tạm ứng, tổng cộng, quyết toán năm) do `ProjectCalculator` tính sống.

### 3.1 Thực thể

| Thực thể | Trường nhập (thô) | Quan hệ con |
|---|---|---|
| **Project** (Dự án) | `Code?`, `Name`, `Notes?` | `List<ViTri> ViTris` |
| **ViTri** (Vị trí) | `Name`, `NguonVon?` (dòng "Nguồn vốn"), `SortOrder` | `List<CostGroup> CostGroups` |
| **CostGroup** (nhóm Chi phí, *người dùng tự tạo*) | `Name`, `SortOrder` | `List<Package> Packages` |
| **Package** (Gói thầu) | `Name`, `ContractorName?`, `BankAccount?`, `ContractValue`, `ContractAnnexValue`, `SortOrder` | `List<LineItem> LineItems` |
| **LineItem** (Đợt) | `Date`, `Content?`, `DocumentNo?`, `Fund`, `Advance`, `PaymentKLHT`, `RecoveredAdvance`, `SortOrder` | — |

`Fund` = enum `{ NSNN, NgoaiHoi }` (Dự phòng NSNN / Quỹ dự trữ ngoại hối).

**Thay đổi so với 6A:**
- `Project` **bỏ** `ContractorName`, `ContractValue`, `ContractAnnexValue` (chuyển xuống `Package`).
- `ProjectLineItem` → gắn vào `Package` thay vì `Project`; **bỏ** `DisbursedNSNN`,
  `DisbursedNgoaiHoi` (2 cột giải ngân nhập tay) và cross-check; **thêm** `Fund`,
  `Content`, `DocumentNo`.
- `ProjectDecision` (bảng QĐ) tạm giữ nguyên/không dùng ở đợt này (thuộc pha KH vốn sau).

### 3.2 Quy tắc nhập Đợt (theo đúng công thức Mẫu.xlsx)

Người dùng **KHÔNG** nhập cột giải ngân (K/L trong Mẫu là công thức). Họ nhập:
- Đợt **tạm ứng**: `Advance` (H), chọn `Fund`.
- Đợt **thanh toán**: `PaymentKLHT` (I) + `RecoveredAdvance` (J), chọn `Fund`.

App tự tính **giải ngân đợt**:
```
disbursed(đợt) = Advance                      (nếu đợt tạm ứng)
               = PaymentKLHT − RecoveredAdvance (nếu đợt thanh toán)
```
rồi rót toàn bộ vào cột **GN NSNN** nếu `Fund=NSNN`, hoặc **GN Ngoại hối** nếu
`Fund=NgoaiHoi`. Chính `Fund` quyết định biểu mẫu (mục 6).

> Giả định: mỗi đợt rút từ **một** nguồn. Nếu sau này cần tách một đợt ra cả 2 nguồn,
> sẽ tách đợt thành 2 dòng — mô hình hiện không hỗ trợ split trong 1 đợt.

## 4. Tầng tính toán (`ProjectCalculator` mở rộng)

Tính gộp từ dưới lên; mỗi cấp một record aggregate (trong `Calc/CalcModels.cs`):

- **PackageAggregate**: `SumAdvance`, `SumKLHT`, `SumRecovered`, `DisbursedNSNN`,
  `DisbursedNgoaiHoi`, `TotalDisbursed`, và
  **`UnrecoveredAdvance` = ΣAdvance − ΣRecovered`** (= "Tạm ứng chưa thu hồi" = **Số dư
  tạm ứng**, dùng cho KS chi & GĐNTT).
- **CostGroupAggregate** = Σ PackageAggregate của nhóm.
- **ViTriAggregate** = Σ CostGroupAggregate → là **dòng TỔNG CỘNG** của Sổ theo dõi và
  là số mà bảng Tổng hợp kéo về.
- **ProjectAggregate** = Σ ViTriAggregate.
- **WorkbookSummary** (màn Tổng hợp nhiều dự án) = Σ ProjectAggregate; có dòng Tổng cộng
  sống trên tập đang lọc (như 6A).

**Quyết toán năm** — `YearSettlement(int FiscalYear, decimal NSNN, decimal NgoaiHoi)`:
gom **giải ngân từng đợt** theo **năm tài chính 1/2 → 31/1**:
```
đợt ngày d thuộc năm tài chính N  ⇔  1/2/N ≤ d ≤ 31/1/(N+1)
```
tách theo `Fund`. Tính tự động từ ngày đợt (không nhập tay).

Cross-check cũ của 6A (GN NSNN + GN Ngoại hối vs KLHT − Thu hồi) **được loại bỏ**: vì
giải ngân nay là số dẫn xuất từ `Fund`, không thể lệch.

## 5. Giao diện / Workbook (phương án A1)

Giữ metaphor Excel của 6A, thêm cấp chọn Dự án.

### 5.1 Màn "Tài chính dự án" (workbook)
- **Trên cùng:** thanh chọn **Dự án** (dropdown hoặc tab ngang) + nút Thêm / Đổi tên / Xóa dự án.
- **Dải tab dưới đáy** của dự án đang chọn:
  1. **Tổng hợp** — bảng gộp các Vị trí của dự án (read-only, kéo từ sổ).
  2. **1 tab mỗi Vị trí** → lưới **Sổ theo dõi** (nhập liệu chính) + nút Thêm/Xóa/Đổi tên Vị trí.
  3. **Quyết toán năm** — bảng theo năm tài chính 1/2→31/1.
  4. **Thông tri · KS chi · GĐNTT** — xem trước form + nút Xuất Excel.
- **IsDirty + Lưu tất cả** (Ctrl+S, dấu •, hỏi khi đóng/khi rời workbook bẩn) — tái dùng
  hạ tầng workbook 6A.

### 5.2 Lưới Sổ theo dõi (1 Vị trí) — lưới cây có outline
Mở rộng custom grid 6A (không dùng DataGrid/TreeDataGrid) thành lưới phân cấp:
- Dòng **TỔNG CỘNG** đông cứng trên đầu.
- Dòng **nhóm Chi phí** (I, II, III… in đậm, hiển thị subtotal) — nút "＋ Thêm nhóm".
- Dòng **Gói thầu** (Nhà thầu · TK · Giá trị HĐ · Phụ lục HĐ) — nút "＋ Thêm gói".
- Dòng **Đợt** — Ngày · Nội dung · Số CT · Tạm ứng · KLHT · Thu hồi · **Nguồn ▾**; cột
  tính sống: GN NSNN · GN Ngoại hối · Số dư tạm ứng · Tổng đã GN · (Quyết toán năm).
- Cột tiền hiển thị **nhóm nghìn bằng dấu "."** (10.000.000).

Đây là hạng mục UI nặng nhất (lưới phẳng 6A → lưới cây có subtotal). Đã chốt làm **bản
đầy đủ (outline)** để trung thành Mẫu.xlsx.

### 5.3 Màn "Tổng hợp" toàn bộ (nhiều dự án)
Mục nav riêng: gộp mọi dự án theo Vị trí, lọc theo tên tức thì, dòng Tổng cộng sống trên
tập đã lọc (như 6A).

## 6. Ba biểu mẫu tự sinh + số-thành-chữ + định dạng dấu "."

### 6.1 Số-thành-chữ tiếng Việt
Viết `VietnameseNumberToWords` thuần C# (thay add-in `[3]!VND()` của Mẫu — add-in không
free license). Xuất chuỗi kiểu "Mười triệu đồng.", "Hai trăm triệu đồng." (viết hoa chữ
đầu, kết thúc bằng " đồng."). Có bộ unit test đầy đủ (0, hàng tỷ, số lẻ, làm tròn đồng).

### 6.2 Định dạng dấu "." mỗi 3 chữ số
Ô tiền hiển thị nhóm nghìn bằng dấu "."; khi nhập gõ số thô, nhập xong (mất focus) tự
format lại; parse bỏ dấu ".". Mở rộng `Common/FinanceCellParsing.cs` của 6A.

### 6.3 Ba form — tab tính-sống (không cần bấm "generate")
Mỗi form là 1 tab **xem trước tính sống** từ dữ liệu đợt đã nhập; cập nhật ngay khi nhập
xong. Nút **Xuất Excel** ghi ra .xlsx bằng ClosedXML (đang dùng).

- **Thông tri (Mẫu số CQ06-HD):** tự gom các đợt **`Fund = NSNN`**, nhóm theo Vị trí →
  đợt; khối **Mục lục ngân sách** (Loại NS, Khoản, Mục, Tiểu mục, Tiết mục, Ngành) **nhập
  tay**; dòng Cộng; **Bằng chữ** từ số-thành-chữ. Nội dung/số tiền kéo từ đợt.
- **GĐNTT (Giấy đề nghị thanh toán vốn đầu tư):** tự gom các đợt **`Fund = NgoaiHoi`**;
  **Số dư tạm ứng = `UnrecoveredAdvance` của gói**; các cột Giá trị HĐ (Package), Lũy kế
  đã thanh toán, Số đề nghị kỳ này; Nguồn = "Quỹ dự trữ ngoại hối"; Bằng chữ.
- **KS chi (Kiểm soát chi):** cho **cả hai** nguồn; Số vốn chấp nhận, Số thu hồi tạm ứng,
  **Số trả đơn vị thụ hưởng = Chấp nhận − Thu hồi**; Bằng chữ.

Các ô mang tính thủ tục mà Mẫu để trống điền tay (số QĐ, ngày ghi sổ, Mục lục NS, tên/TK
đơn vị thụ hưởng, tên ngân hàng…) → **nhập tay & lưu kèm form** (bảng lưu phần khai form
theo dự án/vị trí — chi tiết chốt ở plan 7D).

## 7. Chia pha & migration

DB finance thực gần như trống (chỉ 1 dự án thử "TA"; gate §8 nhập liệu thật **chưa** chạy)
→ migration **reset các bảng `finance_*`** không đau. Dự án thử "TA" sẽ mất — chấp nhận.

| Pha | Nội dung | Kiểm tra hoàn thành |
|---|---|---|
| **7A · Nền: mô hình + calc** | Thêm `ViTri`, `CostGroup`, `Package`; sửa `Project` (bỏ trường HĐ, thêm `ViTris`), `LineItem` (thuộc `Package`, thêm `Fund`/`Content`/`DocumentNo`, bỏ 2 cột giải ngân + cross-check). EF migration reset finance_*. Repo/service load cả cây. `ProjectCalculator`: aggregate 4 cấp + `YearSettlement` (năm TC 1/2→31/1). | Unit/integration test xanh (chưa UI) |
| **7B · Lưới Sổ theo dõi + workbook Vị trí** | Lưới cây outline (nhóm/gói/đợt + subtotal + TỔNG CỘNG đông cứng), thêm/sửa/xóa nhóm-gói-đợt-vị trí, thanh chọn Dự án + tab Vị trí, định dạng dấu ".", IsDirty + Lưu tất cả. | **User chạy app xem lưới** (subagent không thấy UI) |
| **7C · Tổng hợp + Quyết toán năm** | Tab "Tổng hợp" mỗi dự án + màn "Tổng hợp" toàn bộ (lọc tên) + tab "Quyết toán năm". | User xem số khớp Mẫu |
| **7D · 3 form + số-thành-chữ** | `VietnameseNumberToWords` + test; tab xem-trước Thông tri/KS chi/GĐNTT tự lọc theo `Fund`, ô nhập tay, nút Xuất Excel. | User xuất & đối chiếu Mẫu |
| **7E · Dọn dẹp** | 6B teardown: drop bảng legacy `Entities.*` + xóa VM/View chết còn sót từ trước 6A. | Test xanh |

## 8. Ngoài phạm vi (spec riêng về sau)

- **KH vốn / Vốn / QĐ** — kế hoạch vốn theo năm (USD+VNĐ, rất rộng), tổng hợp quyết định
  phê duyệt. Bảng tham chiếu/quy hoạch, làm sau khi lõi ổn.
- **Xuất PDF** biểu mẫu (PdfSharpCore/MigraDoc) — nếu cần in đẹp; đợt này chỉ xuất Excel.
- **Inno Setup `.exe`** — dựng trên máy Windows (đã có `installer/inno-setup.iss`).
- **Bộ nhập Excel cũ (importer 6A):** đọc sheet DA **phẳng**; model mới sâu hơn nên
  importer **tạm ngừng dùng** ở đợt này — người dùng nhập tay qua UI mới. Làm importer/nhập
  từ `Mẫu.xlsx` (định dạng mới) ở pha sau. ⚠️ Ghi rõ để không tưởng nhầm importer còn chạy.

## 9. Ràng buộc kỹ thuật (giữ nguyên)

- 100% free-license: Avalonia (MIT), EF Core, SQLite, ClosedXML, Shouldly. **Không**
  Syncfusion/QuestPDF/EPPlus v5+/FluentAssertions v8+.
- Custom grid tự viết (không DataGrid/TreeDataGrid cho ô nhập); tránh TwoWay + StringFormat
  trên ô decimal/DateOnly (bài học StackOverflow 6A — dùng facade chuỗi cho ngày/tiền).
- Verify UI: chạy `dotnet run` trên DISPLAY=:1, đặt `_NET_WM_STATE_ABOVE` (không dời cửa sổ),
  chụp bằng `mss`; nav-event gate bằng input thật (bài học FA 6A).
- UI tiếng Việt, chỉ VND, single-user, đóng gói `.exe` Windows.
```
