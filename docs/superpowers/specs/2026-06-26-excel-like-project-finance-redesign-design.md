# Thiết kế: Quản lý tài chính dự án kiểu Excel (tính sống + truy vấn theo tên)

**Goal:** Biến phần mềm thành nơi người dùng **nhập số liệu như Excel** và **tự sinh thông tin tổng hợp**, thay cho việc import-từ-Excel hiện tại. Người dùng gõ tên dự án → ra ngay dòng tổng hợp cuối cùng.

**Architecture:** Clean Architecture sẵn có (Domain / Data / Application / Infrastructure / Desktop) + MVVM. Thêm một **lớp công thức thuần (`ProjectCalculator`)** ở tầng Application làm nguồn tính toán duy nhất, dùng chung cho nhập liệu, tổng hợp và xuất Excel.

**Tech Stack:** .NET 8, Avalonia 11.2 + FluentAvalonia, CommunityToolkit.Mvvm, EF Core 8 + SQLite, ClosedXML, xUnit + Shouldly + Moq.

## Global Constraints
- Giấy phép **miễn phí** (cơ quan nhà nước) — không dùng control/thư viện thương mại; không nhúng Excel.
- Desktop offline, 1 người dùng.
- Quy ước code: camelCase biến/tham số, `_camelCase` field private, PascalCase type/thành viên.
- Số tiền `decimal`, `HasPrecision(18,2)`.

---

## 1. Bối cảnh & vấn đề

App hiện có **hai thế giới dữ liệu tách rời**:
1. **Nhập có cấu trúc** (cũ): `Quyết định → Hợp đồng → Dự án → CostItem(cây) → Payment`. Cứng, không sinh ra bảng Tổng hợp.
2. **Tổng hợp Excel**: `ImportBatch → ProjectSummary`, chỉ đọc, đến từ import. Phần "tính tổng hợp" là **C# đọc cứng từng cột** trong `ExcelImportService` — tức dịch ngược công thức Excel bằng tay (giòn, đã sinh nhiều lỗi: export lệch cột, tên dự án sai, nhà thầu trống).

Phân tích **công thức thật trong Excel** (Hiêu_sample.xlsx) cho thấy chúng là **một tập nhỏ, có quy luật**: `SUM` theo dải, số học đơn giản (`J4=E4−G4`, `H7=F7−G7`), tham chiếu chéo sheet (`'TH'!D5='DA1'!E4`), và hàm số→chữ (`VND()`). → Hoàn toàn tái tạo được bằng một lớp tính toán trong code; không cần engine Excel tổng quát.

---

## 2. Quyết định đã chốt (brainstorm)

| # | Quyết định |
|---|-----------|
| Đ1 | **Lưới có cấu trúc + app tự tính** (calc layer trong code). Không dựng engine bảng tính tổng quát; người dùng KHÔNG gõ công thức. |
| Đ2 | **App là nguồn dữ liệu chính** — nhập trực tiếp, lưu DB. Import Excel chỉ để **nạp dữ liệu cũ 1 lần**. Vẫn xuất Excel/in. |
| Đ3 | **Dự án là trung tâm, phẳng**: mỗi dự án = một lưới các *đợt* phẳng (thay cây chi phí). Quyết định liên kết **n–n**. Hợp đồng hạ thành thuộc tính/ô text. |
| Đ4 | Schema cột + công thức lấy từ **Hiêu_sample.xlsx**. |
| Đ5 | **Truy vấn theo tên → một dòng tổng hợp đã tính**. |
| Đ6 | Số dẫn xuất **không lưu**, tính sống từ ô gốc (một nguồn sự thật). Chỉ "đóng băng" khi in báo cáo. |
| Đ7 | Mỗi đợt nhập **2 số tuyệt đối** GN NSNN + GN Ngoại hối; có **kiểm tra chéo** với `KLHT − Thu hồi`. |
| Đ8 | Lưới nhập **giống Excel**: kẻ ô, bàn phím, **dán khối từ Excel**, ghim tiêu đề + dòng tổng. |
| Đ9 | **Xóa hẳn** các bảng cũ: Contract, CostItem, Payment, ImportBatch, ProjectSummary. Giữ Vendor, Decision. |
| Đ10 | **Thông tri tách spec riêng**, làm sau (chờ mẫu chuẩn). |

---

## 3. Mục 1 — Mô hình dữ liệu

**Thực thể lưu DB:**

### Project (Dự án)
`Id, Code?, Name (bắt buộc), ContractorName? (hoặc VendorId?), ContractValue (decimal), Type?, Location?, Status?, Notes?`

### ProjectLineItem (Đợt) — các dòng kiểu sheet DA
Ô **nhập gốc**: `Id, ProjectId, SortOrder, Date (DateOnly), PackageName?, ContractRef?, Advance, PaymentKLHT, RecoveredAdvance, DisbursedNSNN, DisbursedNgoaiHoi`
- `Kind` (Ứng tiền / Thanh toán KLHT) **không lưu** — suy ra: nếu `Advance>0 && PaymentKLHT==0` → Ứng tiền; ngược lại → Thanh toán KLHT (mặc định).
- `Year` **không lưu** — lấy từ `Date`.

### Decision (Quyết định) — giữ như hiện có
`Id, Code (…/TM-TCh), Title, Date, Authority, Amount, Notes`

### ProjectDecision — liên kết n–n
`ProjectId, DecisionId` (khóa chính ghép)

### Vendor — giữ như hiện có (danh mục nhà thầu, gắn tùy chọn vào Project)

**Bỏ khỏi mô hình:** `Contract`, `CostItem`, `Payment`, `ImportBatch`, `ProjectSummary`, `SettlementByYear`(entity) — thay bằng tính sống.

---

## 4. Mục 2 — Lớp công thức `ProjectCalculator` (tầng Application, hàm thuần)

Vào: `Project + IReadOnlyList<ProjectLineItem>`. Ra: DTO đã tính. Không phụ thuộc DB/UI. **Đây là nơi test dày nhất.**

### 4.1 Mỗi đợt (LineItemComputed)
| Giá trị | Công thức | Khớp Excel |
|---------|-----------|-----------|
| `GiaiNgan` | `DisbursedNSNN + DisbursedNgoaiHoi` | H+I |
| `Expected` (kiểm tra chéo) | Ứng tiền → `Advance`;  Thanh toán → `PaymentKLHT − RecoveredAdvance` | `H6=E6`, `H7=F7−G7` |
| `IsBalanced` | `GiaiNgan == Expected` | — |
| `Kind` | suy từ ô nhập (xem §3) | — |

Khi `IsBalanced == false` → UI cảnh báo (đỏ + tooltip) nhưng **vẫn cho lưu**.

### 4.2 Mỗi dự án (ProjectAggregate)
| Giá trị | Công thức | Khớp Excel |
|---------|-----------|-----------|
| `SumAdvance` | `Σ Advance` | `E4=SUM` |
| `SumRecovered` | `Σ RecoveredAdvance` | `G4=SUM` |
| `SumKLHT` | `Σ PaymentKLHT` | `F4=SUM` |
| `SumNSNN` | `Σ DisbursedNSNN` | `H4=SUM` |
| `SumNgoaiHoi` | `Σ DisbursedNgoaiHoi` | `I4=SUM` |
| `UnrecoveredAdvance` (TU chưa thu hồi) | `SumAdvance − SumRecovered` | `J4=E4−G4` |
| `TotalDisbursed` (Tổng giải ngân) | `SumNSNN + SumNgoaiHoi` | — |
| `YearlySettlements` | gom đợt theo `Year`, mỗi năm cộng `NSNN, NgoaiHoi, KLHT, Advance` | cột Quyết toán năm |

`ContractValue`: trường **nhập tay** ở cấp Project (người dùng nhập; importer đổ từ dòng hợp đồng của sheet DA). Không phải số dẫn xuất.

### 4.3 Toàn cục (TH) + truy vấn
- Một `ProjectAggregate` mỗi dự án; tổng cộng = cộng các dự án.
- Truy vấn theo tên = lọc lấy aggregate của đúng dự án.

**Một nguồn công thức duy nhất** dùng cho cả nhập / tổng hợp / xuất → số luôn nhất quán, triệt tiêu lớp lỗi "export viết lại công thức khác import".

---

## 5. Mục 3 — Lưới nhập kiểu Excel

**Bố cục master–detail:** chọn dự án (dropdown/list) → lưới các đợt của dự án đó. `[+ Dự án mới]`, `[Xóa dự án]`.

**Cột lưới:** Ngày · Gói thầu · Tạm ứng · TT KLHT · Thu hồi · GN NSNN · GN Ngoại hối · *Giải ngân (tự tính, chỉ đọc)* · *✓ kiểm tra chéo*. Dòng **TỔNG** ghim dưới: Σ từng cột + TU chưa thu hồi + Tổng giải ngân.

**Hành vi giống Excel (bắt buộc cả 4):**
1. **Kẻ ô + giao diện bảng tính** — viền ô đầy đủ, ô đang chọn highlight.
2. **Bàn phím** — Tab/Enter/mũi tên nhảy ô, gõ đè trực tiếp.
3. **Dán khối từ Excel** — đọc clipboard dạng bảng (TSV), đổ vào vùng ô từ ô đang chọn. *(Phần nặng nhất.)*
4. **Ghim tiêu đề + dòng tổng** khi cuộn.

**Tính sống:** mỗi dòng là row-viewmodel observable; gõ ô → gọi `ProjectCalculator` cập nhật ô dẫn xuất của dòng + dòng tổng ngay. Số định dạng phân cách nghìn, căn phải. Dòng trống cuối: gõ vào là tạo đợt mới (kiểu Excel). Xóa đợt bằng Delete.

---

## 6. Mục 4 — Tổng hợp & truy vấn theo tên

Màn Tổng hợp có ô **"Gõ tên dự án…"** (lọc tức thì):
- Có tên → **dòng tổng hợp** của dự án đó (Giá trị HĐ, Σ Tạm ứng, Σ Thu hồi, TU chưa thu hồi, GN NSNN, GN Ngoại hối, Tổng giải ngân, Quyết toán theo năm) — **tính sống**.
- Trống → toàn bộ dự án + dòng Tổng cộng.
- Bấm một dự án → mở lưới nhập của dự án đó.

---

## 7. Mục 5 — Migration & nạp dữ liệu cũ

- **Migration mới**: bỏ bảng Contract/CostItem/Payment/ImportBatch/ProjectSummary; thêm Project(mới)/ProjectLineItem/ProjectDecision. App chưa có dữ liệu thật → không cần chuyển dữ liệu phức tạp.
- **Importer 1 lần**: đọc file Excel cũ, map **mỗi sheet DA → 1 Project + các đợt** (đổ ô nhập thô). Đơn giản hơn import hiện tại vì chỉ đọc ô gốc, không dịch ngược tổng hợp.

---

## 8. Mục 6 — Xuất Excel / Thông tri / Test

- **Xuất Excel:** giữ kiểu điền mẫu, **nguồn số = `ProjectCalculator`** → số xuất = số màn hình.
- **Thông tri:** spec riêng (chờ mẫu). Cần **hàm số→chữ tiếng Việt** thay `VND()`.
- **Test:**
  - `ProjectCalculator`: mỗi công thức + test "cả sheet DA mẫu → đúng dòng tổng" đối chiếu Excel.
  - Lưu/đọc round-trip (lưu → đọc → tính = số y hệt).
  - Import: file mẫu → đúng Project + đợt. Export: mở lại file → **đối chiếu giá trị** (không smoke).
  - ViewModel: tính live + cảnh báo lệch; (dán khối: test parse TSV → ô).

---

## 9. Chia pha thực thi (cho writing-plans)

1. **Model + `ProjectCalculator` + test** (không UI) — lõi an toàn.
2. **Lưu trữ + CRUD dự án** (repo, migration).
3. **Lưới nhập Excel-like** (4 hành vi, tính live, kiểm tra chéo).
4. **Tổng hợp + truy vấn theo tên**.
5. **Xuất Excel (từ calc) + importer dữ liệu cũ**.
6. **Thông tri** — *spec riêng, sau*.

---

## 10. Ngoài phạm vi / follow-up
- Thông tri (spec riêng).
- Defect kiến trúc F (vòng đời DbContext dùng chung) — nên xử lý kèm khi đụng tầng Data ở Pha 2.
- Báo cáo dự án, tài liệu đính kèm (các tab placeholder cũ).

## 11. Tiêu chí thành công
- Nhập một dự án hoàn toàn trong app (không cần Excel); số tổng hợp tính đúng công thức Excel.
- Gõ tên dự án → ra dòng tổng hợp đúng.
- Xuất Excel ra số khớp màn hình.
- `ProjectCalculator` phủ test cho mọi công thức; toàn bộ test xanh.
