# Thiết kế: Workbook kiểu Excel + hợp nhất một khái niệm "dự án"

**Goal:** Gộp mọi màn hình tài chính về **một workbook kiểu Excel** — tab dưới đáy, tab `TH` là tổng hợp, mỗi tab `DA` quản lý **một dự án** — và xóa bỏ thế giới dữ liệu cũ để trong app chỉ còn **một khái niệm "dự án" duy nhất**, gồm cả dự án import từ Excel lẫn dự án nhập tay.

**Architecture:** Clean Architecture sẵn có (Domain / Data / Application / Infrastructure / Desktop) + MVVM. Không thêm tầng mới; tái dùng `ProjectCalculator` làm nguồn tính toán duy nhất.

**Tech Stack:** .NET 8, Avalonia 11.2 + FluentAvalonia, CommunityToolkit.Mvvm, EF Core 8 + SQLite, ClosedXML, xUnit + Shouldly + Moq.

## Global Constraints
- Giấy phép **miễn phí** (cơ quan nhà nước) — không control/thư viện thương mại.
- Desktop offline, 1 người dùng, UI tiếng Việt, chỉ VND.
- Quy ước code: camelCase biến/tham số, `_camelCase` field private, PascalCase type/thành viên.
- Số tiền `decimal`, `HasPrecision(18,2)`.

---

## 1. Bối cảnh & vấn đề

Người dùng phát hiện (2026-07-17, khi chạy app trên `main` sau khi merge phase 1–5):

> "Hai mục **Bảng tổng hợp** và **Tổng hợp dự án** hoàn toàn khác nhau, như thế rất khó quản lý."
> "Hãy thiết kế giống Excel, tạo ra các page, mỗi page quản lý 1 dự án."
> "Mục tổng hợp dự án phải gồm **cả dự án từ Excel lẫn dự án nhập trên app**."

**Nguyên nhân gốc — hai bảng dữ liệu song song** (đo trên `app.db` thật):

| Bảng | Số dòng | Hiện ở màn |
|---|---|---|
| `project_summaries` (cũ) | **15** = DA1–DA5 × **3 lần import** | "Bảng tổng hợp" |
| `finance_projects` (mới) | **1** (`TA`, gõ thử) | "Tổng hợp dự án" |

Đây là trạng thái dở dang của chiến lược **strangler**: model mới đã dựng xong (phase 1–5) nhưng model cũ chưa gỡ. Việc gỡ vốn đã nằm trong kế hoạch ("phase cuối: xóa bảng cũ") — người dùng chạm đúng vào nó.

Hai quan sát dẫn dắt thiết kế:

1. **Bảng cũ nghèo hơn bảng mới.** `project_summaries` chỉ lưu **số tổng đã làm phẳng**, không có chi tiết từng đợt. Model mới có `finance_project_lines` đầy đủ. → **Không migrate cũ→mới**; **import lại file Excel gốc** bằng importer mới (Phase 5a) cho dữ liệu giàu hơn. File Excel là nguồn thật.
2. **Import trùng đã xảy ra thật.** 15 dòng = 5 dự án × 3 lần import. Trước đây "chống trùng" bị để ngoài phạm vi; khi tổng hợp gộp về một nguồn thì nó thành lỗi nghiệp vụ thật sự.

Ngoài ra còn một cặp trùng lặp **cùng bản chất** mà người dùng chưa nêu: nav **"Dự án"** (cây cũ `Dự án → Hợp đồng → Đầu mục chi phí → Đợt thanh toán`) tồn tại song song với dự án của model mới → **hai khái niệm "dự án"** trong một app.

---

## 2. Quyết định đã chốt (brainstorm 2026-07-17)

| # | Quyết định | Lý do |
|---|-----------|-------|
| 1 | **Bỏ hẳn** "Bảng tổng hợp" + luồng import cũ + các bảng cũ; **import lại Excel gốc** bằng importer mới | Bảng cũ nghèo hơn; Excel là nguồn thật; một nguồn dữ liệu duy nhất |
| 2 | **Bỏ luôn cả cây cũ** Dự án/Hợp đồng/Chi phí/Thanh toán/Nhà cung cấp | Triệt tiêu khái niệm "dự án" thứ hai — đúng gốc điều người dùng than phiền |
| 3 | Bố cục **workbook, dải tab dưới đáy** (giống Excel nhất) | Khớp file Excel gốc `TH + DA1..DA5`; đúng yêu cầu "giống như Excel" |
| 4 | **Lưu tất cả kiểu Excel**: dấu `•` báo tab bẩn, Ctrl+S ghi hết, đóng app còn bẩn thì hỏi | Giống Excel; an toàn hơn auto-save (gõ nhầm không ghi đè ngay) |
| 5 | Import trùng tên → **hỏi từng dự án**: Ghi đè / Bỏ qua / Thêm bản mới | Người dùng toàn quyền; không bao giờ tự nhân bản như bảng cũ |
| 6 | Dựng tab bằng **`TabControl` chuẩn của Avalonia** | `TabStripPlacement="Bottom"` có sẵn; tái dùng nguyên 2 view đã có; ít code, ít rủi ro |
| 7 | Tách **2 đợt 6A (workbook) / 6B (teardown)** | Migration DROP là **không thể hoàn tác** — phải sau khi người dùng xác nhận dữ liệu đã sang model mới |

**Giữ nguyên (không đụng):** `ProjectCalculator` và toàn bộ lớp tính toán; `finance_*`; `approval_decisions` (Quyết định); `app_settings`; Thông tri là đợt riêng sau.

---

## 3. Kiến trúc màn hình

Nav sau khi dọn — **4 mục**:

```
Tài chính dự án   ← workbook (MỚI, thay 3 mục cũ)
Thông tri         ← giữ, làm sau (đợt riêng)
Quyết định        ← giữ
Cài đặt           ← giữ
```

`FinanceWorkbookView` = `TabControl` với `TabStripPlacement="Bottom"`:

```
┌────────────┬──────────────────────────────────┐
│ Tài chính  │  Dự án: DA1 — Cầu Xyz            │
│ Thông tri  ├──────────────────────────────────┤
│ Quyết định │  (nội dung tab = view sẵn có)     │
│ Cài đặt    │                                   │
│            ├──────────────────────────────────┤
│            │ ▐TH▌ DA1 │•DA2│ DA3 │ DA4 │ + │  │
└────────────┴──────────────────────────────────┘
                ↑ dải tab dưới đáy giống Excel
```

- **Tab `TH`** — vị trí đầu, **ghim, không đóng được**. Nội dung = `FinanceSummaryView` **hiện có**.
- **Tab `DA1..DAn`** — mỗi tab **một dự án**. Nội dung = `FinanceProjectGridView` **hiện có**.
- **Tab `+`** — tạo dự án mới, mở tab mới, focus vào ô tên.
- **Chuột phải tab DA** → `Đổi tên` / `Xóa dự án` (Xóa có ConfirmDialog).
- Nhiều tab → dải tab tự cuộn ngang (`TabControl` xử lý sẵn).

**Ràng buộc:** `TabStripPlacement="Bottom"` phải được xác minh render đúng trên Avalonia 11.2 + FluentAvalonia ngay task đầu; nếu theme ghi đè, fallback là style lại `TabItem` (không đổi sang control tự vẽ).

### DataTemplate chọn view theo loại trang

`TabControl.ItemsSource` = `ObservableCollection<WorkbookPageViewModel>`; `ContentTemplate` dùng `DataTemplates` khớp kiểu:
- `SummaryPageViewModel` → `FinanceSummaryView`
- `ProjectPageViewModel` → `FinanceProjectGridView`

Header tab bind `Title` (kèm `•` khi bẩn).

---

## 4. ViewModel

### 4.1 MỚI — `FinanceWorkbookViewModel`

| Thành viên | Vai trò |
|---|---|
| `ObservableCollection<WorkbookPageViewModel> Pages` | `[0]` = TH (ghim), `[1..n]` = dự án |
| `WorkbookPageViewModel? SelectedPage` | Tab đang mở |
| `bool IsDirty` | `Pages.OfType<ProjectPageViewModel>().Any(p => p.IsDirty)` |
| `SaveAllCommand` | Ghi mọi trang bẩn → TH tính lại |
| `AddPageCommand` | Tạo dự án mới + tab mới |
| `ClosePageCommand(page)` | Xóa dự án (confirm) + đóng tab |
| `RenamePageCommand(page)` | Đổi tên dự án |
| `LoadAsync()` | Nạp dự án qua `ListWithLinesAsync` (đã có) → dựng tab |
| `GoToProjectAsync(id)` | TH double-click dòng → nhảy tới tab tương ứng |

### 4.2 Lớp trang

```
abstract WorkbookPageViewModel        // Title, IsPinned, IsDirty
├── SummaryPageViewModel              // bọc FinanceSummaryViewModel (đã có)
└── ProjectPageViewModel              // bọc FinanceProjectGridViewModel (đã có)
```

### 4.3 REFACTOR — `FinanceProjectGridViewModel`

Hiện VM này tự ôm danh sách dự án + dropdown chọn (`Projects`, `SelectedProject`, `OnSelectedProjectChanged`). Trong workbook, **tab chính là phép chọn dự án** → dropdown thành thừa và là nguồn nhầm lẫn.

- **Bỏ:** `Projects`, `SelectedProject`, `OnSelectedProjectChanged`, `NewProjectCommand`, `LoadAsync` (danh sách).
- **Thêm:** khởi tạo cho **đúng một dự án**; `bool IsDirty` bật khi `InputChanged`, tắt sau save/load.
- **Giữ nguyên:** toàn bộ lưới, parse ô, dán TSV, tính sống, cross-check ✓/✕, `SaveAsync`.

⚠️ Đây là refactor có rủi ro nhất về hồi quy — 145 test Desktop hiện tại phủ VM này, phải cập nhật đồng thời.

### 4.4 REFACTOR — `FinanceSummaryViewModel`

- **Giữ:** lọc theo tên tức thì, dòng Tổng cộng sống trên tập đã lọc, **Nhập Excel cũ**, **Xuất Excel**.
- **Bỏ:** nút `Mở dự án` (giờ bấm tab).
- **Thêm:** double-click dòng → `GoToProjectAsync(id)` của workbook (qua callback/interface, **không** cho page tham chiếu ngược workbook một cách vòng tròn).

---

## 5. Lưu kiểu Excel

- `ProjectPageViewModel.IsDirty` ← `FinanceProjectGridViewModel.InputChanged`.
- Header tab bẩn hiển thị `• DA2`.
- **Lưu tất cả** (nút + `Ctrl+S` ở cấp workbook): lặp các trang bẩn → `SaveAsync` → clear dirty → TH `RefreshAsync`.
- Một trang lỗi → **không chặn** các trang khác; gom lỗi báo cuối, trang lỗi **giữ nguyên trạng thái bẩn**.
- Đóng cửa sổ còn tab bẩn → `ConfirmDialog`: `Lưu và thoát` / `Thoát không lưu` / `Hủy`.

---

## 6. Import chống trùng

Luồng ở tab TH, nút **Nhập Excel cũ**:

```
Chọn file → ParseWorkbookAsync (đã có)
          → đối chiếu Name với dự án đã có (so sánh không phân biệt hoa/thường, trim)
          → KHÔNG trùng  → thêm mới
          → CÓ trùng     → dialog cho chọn từng dự án:
                             Ghi đè      (thay dự án cũ + replace toàn bộ lines)
                             Bỏ qua      (giữ nguyên dự án cũ)
                             Thêm bản mới(tạo dự án riêng, tên "<Tên> (2)";
                                          nếu "(2)" cũng trùng thì tăng dần
                                          "(3)", "(4)"… tới khi không trùng)
          → lưu → workbook nạp lại tab
```

- Dialog liệt kê **cả** dự án mới (chỉ để xem) và dự án trùng (có lựa chọn), kèm cảnh báo parser nếu có.
- Mặc định cho mọi dòng trùng = **Ghi đè** (ý định thường gặp khi import lại file mới hơn), người dùng đổi được từng dòng.
- Hủy dialog → **không ghi gì**.

`FinanceImportService` nhận thêm hàm phân loại trùng; `SaveImportedAsync` nhận quyết định theo từng dự án. Logic đối chiếu là **hàm thuần** để test không cần DB.

---

## 7. Dọn cây cũ (đợt 6B)

**Xóa Views + ViewModels:** `TongHop/*` (gồm `ImportWizard*`), `Projects/*`, `Contracts/*`, `CostItems/*`, `Payments/*`, `Vendors/*`.

**Xóa Services:** `IContractService`, `ICostService`, `ICostUnitTypeService`, `IExcelExportService`, `IExcelImportService`, `IPaymentService`, `IProjectService`, `IProjectSummaryService`, `IVendorService` (+ implementations + đăng ký DI).

**Xóa Repos/Abstractions:** `ICostRepository`, `ICostUnitTypeRepository`, `IProjectSummaryRepository` (+ implementations).

**Xóa Entities + Validators + EF configs:** `Contract`, `CostItem`, `CostTemplate`, `CostTemplateItem`, `CostUnitType`, `ImportBatch`, `Payment`, `Project` (cũ, `Domain/Entities/Project.cs`), `ProjectSummary`, `SettlementByYear`, `Vendor`; `ContractValidator`, `CostItemValidator`, `PaymentValidator`, `ProjectValidator`, `VendorValidator`; enum `PaymentType`, `ProjectStatus` nếu hết tham chiếu.

**Sửa (không xóa):** `ApprovalDecision.Contracts` — gỡ nav property `ICollection<Contract>` (Quyết định vẫn sống, `finance ProjectDecision → ApprovalDecision` vẫn nguyên).

**Migration DROP:** `project_summaries`, `settlement_by_years`, `import_batches`, `projects`, `contracts`, `cost_items`, `payments`, `vendors`, `cost_templates`, `cost_template_items`, `cost_unit_types`.

**Giữ:** `approval_decisions`, `app_settings`, `finance_projects`, `finance_project_lines`, `finance_project_decisions`, `audit_logs`, `attachments`.

**Cần kiểm khi làm:** `Attachment`/`AuditLog`/`AttachmentEntityType` có tham chiếu entity bị xóa không; `CostUnitType` seed data; backup `.cfb` có serialize bảng cũ không (nếu có thì phải sửa định dạng backup).

---

## 8. ⚠️ Thứ tự bắt buộc — dữ liệu không hoàn tác được

Migration DROP **xóa vĩnh viễn** DA1–DA5 trong bảng cũ. Bắt buộc:

> **① Backup `app.db`** → **② Import lại `reference/Hieu_sample.xlsx` bằng importer mới** → **③ Người dùng xác nhận số liệu đủ/đúng** → **④ Mới chạy migration DROP**

**Không được đảo thứ tự.** Đợt 6B **không được bắt đầu** trước khi người dùng xác nhận bước ③. Đây là lý do tách đợt.

---

## 9. Phân đợt

### Đợt 6A — Workbook (không xóa dữ liệu)
1. Dựng `TabControl` đáy + xác minh render trên Avalonia 11.2/FluentAvalonia.
2. `WorkbookPageViewModel` + `SummaryPageViewModel` + `ProjectPageViewModel`.
3. Refactor `FinanceProjectGridViewModel` → một-dự-án + `IsDirty` (cập nhật test).
4. `FinanceWorkbookViewModel` (Pages/Selected/Add/Close/Rename/LoadAsync/GoToProject).
5. Lưu tất cả + `Ctrl+S` + `•` + confirm khi đóng.
6. TH: bỏ `Mở dự án`, double-click → nhảy tab.
7. Import chống trùng (service + dialog).
8. Nav: còn 4 mục (**gỡ mục cũ khỏi nav, chưa xóa code**) + DI + chạy thật.

**Kết đợt 6A:** người dùng import lại Excel, kiểm tra dữ liệu → chốt bước ③.

### Đợt 6B — Teardown (sau khi có xác nhận ③)
1. Xóa Views/VMs cũ.
2. Xóa Services/Repos cũ + DI.
3. Xóa Entities/Validators/EF configs + sửa `ApprovalDecision`.
4. Migration DROP + test trên DB tạm.
5. Chạy thật + toàn bộ suite xanh.

---

## 10. Kiểm thử

| Vùng | Test |
|---|---|
| `FinanceWorkbookViewModel` | thêm/đóng/đổi tên trang; TH luôn ở đầu & không đóng được; `IsDirty` gộp đúng; `SaveAll` chỉ ghi trang bẩn; một trang lỗi không chặn trang khác; `GoToProject` chọn đúng tab |
| `FinanceProjectGridViewModel` | test hiện có sửa cho một-dự-án; `IsDirty` bật khi sửa, tắt sau save/load |
| Import dedup | hàm phân loại trùng (thuần): trùng/không trùng/khác hoa-thường; Ghi đè thay lines; Bỏ qua giữ nguyên; Thêm bản mới sinh tên `(2)`; Hủy không ghi gì |
| Migration DROP | trên DB tạm (`CONSTRUCTIONFINANCE_DB`): bảng cũ biến mất, `finance_*` + `approval_decisions` còn nguyên |
| Hồi quy | toàn bộ suite (**đang 329**) phải xanh sau mỗi task |

**Bắt buộc chạy thật:** binding loop kiểu Phase 3 (TwoWay + StringFormat) và lỗi render **không** bị test bắt. Mỗi đợt phải chạy app trên `DISPLAY=:1` và người dùng soát mắt — đây là bài học đã trả giá ở Phase 3.

---

## 11. Rủi ro

| Rủi ro | Giảm thiểu |
|---|---|
| **DROP xóa vĩnh viễn dữ liệu cũ** | Thứ tự bắt buộc ở §8; backup `app.db`; 6B chờ người dùng xác nhận |
| Refactor grid VM gây hồi quy | Test đi cùng từng task; suite xanh mỗi bước |
| `TabStripPlacement=Bottom` bị theme ghi đè | Xác minh ngay task 1; fallback style `TabItem`, không tự vẽ control |
| Nhiều tab → chậm/tốn RAM | `ListWithLinesAsync` một lần; quy mô thực tế ~5–20 dự án; đo trước khi tối ưu (YAGNI) |
| Binding loop khi tái dùng view trong tab | Giữ nguyên bài học Phase 3: ô sửa được **không** TwoWay + StringFormat |
| Xóa entity làm vỡ Attachment/AuditLog/backup `.cfb` | Kiểm tham chiếu ở §7 trước khi xóa |

---

## 12. Ngoài phạm vi

- **Thông tri** (đợt riêng, cần `VND()` số→chữ tiếng Việt).
- Kéo thả đổi thứ tự tab (YAGNI — dùng `SortOrder` sẵn có).
- Undo/redo trong lưới.
- Migrate `project_summaries` → `finance_projects` (đã bác ở §2 — import lại Excel cho dữ liệu giàu hơn).
- Đa người dùng / đồng bộ.
