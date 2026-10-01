# Phase 7E — Dọn thế hệ mã đã bị thay thế — Thiết kế

**Ngày:** 2026-10-01 · **Trạng thái:** chờ user duyệt

## 1. Mục tiêu

Xóa thế hệ mã **lưới phẳng 6A** đã bị mô hình cây 7A–7D thay thế, cộng với đường **nhập Excel cũ** mà mô hình mới không còn dùng, sao cho sau mỗi bước build xanh và test xanh, và **không một tính năng nào người dùng đang thấy bị mất ngoài đúng một thứ đã được chấp thuận** (nút "Import Excel").

Lợi ích: `FinanceWorkbookViewModel` ngắn lại đáng kể, `IFinanceProjectService` còn 5 thành viên thay vì 13, và người đọc mã thôi phải tự hỏi "nửa nào còn sống".

## 2. Cơ sở: bản kiểm kê đã kiểm chứng đối kháng

Thiết kế này **không** dựa trên ghi chú cũ. Ngày 2026-10-01 đã chạy một đợt kiểm kê 29 tác nhân (5 lăng kính quét độc lập + 11 nhóm mã, mỗi nhóm qua lập-bản-đồ rồi **thẩm định đối kháng** với yêu cầu mặc định giữ lại khi không chắc, rồi dò mối nối, rồi soát sót). Kết quả lật lại ghi chú cũ ở ba điểm:

| Ghi chú cũ nói | Sự thật đo được |
|---|---|
| "Hai nhóm đã chết hẳn, xóa cả cụm" | 11 nhóm: **1** xóa sạch được, **8** chết một phần, **2** đang sống |
| Nhóm B là mã chết | Là **hai tính năng bấm được** trong app đang chạy, không có gì thay thế |
| 3 mối nối cần gỡ | **6 mối nối "xóa xong hỏng ngầm"** + 14 mối "xóa xong vỡ build" |

**Chỗ nguy hiểm nhất** — `FinanceWorkbookViewModel.SaveAllAsync` (dòng 539–566) là đường **Ctrl+S duy nhất** của mô hình cây mới, nhưng thân nó trộn lẫn mã phẳng và mã cây:

```csharp
var dirtyPages = Pages.OfType<ProjectPageViewModel>()...   // CHẾT
foreach (var page in dirtyPages) await page.Grid.SaveAsync();   // CHẾT
if (saveViTriTree) await SaveViTriTreeAsync();                  // SỐNG (7B)
await SummaryPage.Summary.LoadAsync();                          // CHẾT
```

Xóa cả phương thức — điều ghi chú cũ ngụ ý — sẽ **mất Ctrl+S và mất đường lưu cây**, mà trình biên dịch không kêu. Phải mổ từng dòng.

## 3. Quyết định của user (2026-10-01)

1. **Giữ mục "Thông tri"**, **xóa nút "Import Excel"**.
2. **Làm tính năng in trước, 7E sau.** Kế hoạch in viết theo mã hiện tại và Task 1 của nó tách sẵn `UniqueSheetName` — một trong các mối nối 7E cần. Làm 7E trước thì kế hoạch in hỏng (nó ghi `FinanceWorkbookViewModel` nhận sáu service, trong đó có `SummaryPageViewModel`).

Quyết định 1 chia được một cách sạch sẽ, đã kiểm chứng: `ThongTriViewModel` chỉ phụ thuộc `IProjectSummaryService` và chỉ gọi **2** trong 9 thành viên của nó (`GetLatestBatchAsync` ở `ThongTriViewModel.cs:30`, `ListByBatchAsync` ở `:37`). Bảy thành viên còn lại chỉ phục vụ `TongHopViewModel` và `ImportWizardViewModel` — cả hai chết theo nút Import Excel. `TongHopViewModel` **không có mục nav nào trỏ tới**, chỉ tới được qua luồng import, nên nó chết theo.

## 4. Phạm vi

### 4.1 Xóa — thế hệ lưới phẳng 6A (nhóm A)

`FinanceProjectGridViewModel` + View · `LineItemRowViewModel` · `BalancedConverters.cs` · `FinanceSummaryViewModel` + View · `FinanceSummaryRowVm` · `ProjectPageViewModel` · `SummaryPageViewModel` · `ImportResolutionDialogViewModel` + View · `ProjectCalculator` · `LineItemMapping` · `CalcModels.cs` · `FinanceExportService` + interface · `FinanceImportService` + interface · `FinanceImportModels` · phần còn lại của `ImportMerge` · `ProjectLineItem` · `ProjectDecision` + cấu hình EF · các trường hợp đồng cũ trên `Project` · bảng `finance_project_lines`, `finance_project_decisions`.

### 4.2 Xóa — đường nhập Excel cũ

`ImportWizardViewModel` + View · `TongHopViewModel` + `TongHopRow` + View · `ExcelImportService` + `ExcelExportService` + interface · nút "Import Excel" ở `MainWindow.axaml:48-59` · `MainWindowViewModel.OpenImportWizardAsync` và nhánh `case "TongHop"`.

### 4.3 Xóa — mã chết audit tìm thêm, không có trong ghi chú cũ

Bốn nhánh điều hướng không chuỗi Tag nào tới được (`Vendors`, `Projects`, `FinanceGrid`, `FinanceSummary` trong `MainWindowViewModel.cs:57-81`) · màn Vendors (`VendorListViewModel`, `VendorEditDialogViewModel`, Views, tests) — **nhưng giữ `IVendorService`/`VendorService`** vì `CostTreeViewModel.cs:21` tiêm nó · `IAuditService` + `AuditService` (không consumer nào; nhật ký thật do `AuditInterceptor` ghi).

### 4.4 Giữ — dứt khoát không đụng

- **Mục "Thông tri"** và toàn chuỗi sau nó: `ThongTriViewModel` + View, `IProjectSummaryService` (**gọt còn 2 thành viên**), `ProjectSummaryService`, `ProjectSummaryRepository`, entity `ImportBatch`/`ProjectSummary`/`SettlementByYear`, ba bảng `import_batches`/`project_summaries`/`settlement_by_years`. 15 dòng dữ liệu của user vẫn xem được.
- `Common/ClipboardGridParser.cs` — lưới Vị trí dùng (`VitriLedgerView.axaml.cs:101`).
- `Common/FinanceCellParsing.ParseDate` — lưới Vị trí dùng. `ParseMoney` thì rà lại, có thể chết.
- `Styles/Theme.axaml` — **toàn bộ**. Mọi selector `gridCell*`/`tableCard`/`toolbarStrip` dùng chung với `VitriLedgerView`.
- `WorkbookPageViewModel` (lớp cha) — `ViTriPageViewModel` và các trang 7C/7D kế thừa.
- `Calc/DotMapping.cs`, `TreeModels.cs`, `TreeCalculator.cs`, `WorkbookSummary.cs`, `WorkbookSummaryBuilder.cs`.
- `AppSetting` (ba biểu mẫu 7D đọc), `ApprovalDecision`, `AuditLog`, `AuditInterceptor`.
- Nhóm Dự án/Quyết định/Nhà thầu/Hợp đồng/Chi phí/Thanh toán — xem §8.

## 5. Bất biến: thứ phải còn chạy sau mỗi bước

1. **Ctrl+S lưu được cây Vị trí.** Ba chỗ gắn `SaveAllCommand`: `FinanceWorkbookView.axaml:8` (KeyBinding), `:74` (nút 7B), `:26` (thanh cũ — chết). Giữ `SaveAllAsync` + `SaveViTriTreeAsync`.
2. **Hai chốt chặn "chưa lưu"** ở `MainWindow.axaml.cs:49` (đóng cửa sổ) và `:85` (rời màn) vẫn hoạt động — tức `FinanceWorkbookViewModel.IsDirty` còn sống, chỉ bỏ vế thứ nhất của nó.
3. **"Thêm dự án" vẫn đặt tên "Dự án mới", "Dự án mới (2)"** — hành vi bị ghim bởi test `FinanceWorkbookViewModelTests.cs:620` và `:635`.
4. **"Xóa dự án"** (`FinanceWorkbookViewModel.DeleteProjectAsync`) vẫn chạy.
5. **Bốn mục nav còn lại bấm được**: Tài chính dự án · Tổng hợp · Thông tri · Quyết định · Cài đặt.
6. **`ViewLocator` còn test**: `ViewLocatorTests.cs` hiện lấy `ImportWizardViewModel` làm mẫu — mẫu đó chết, phải **đổi sang cặp còn sống** (`VitriLedgerViewModel` → `VitriLedgerView`), **không được xóa tệp test**. Đây là test duy nhất của quy ước phân giải View mà mọi tab 7B/7C/7D phụ thuộc, và `ViewLocator` hỏng thì hỏng âm thầm.
7. Bốn khóa ngoại con của `finance_projects` (`finance_vitris`, ba bảng `*_decl`) và ba index UNIQUE trên chúng còn nguyên sau migration.

## 6. Mối nối phải gỡ TRƯỚC khi xóa

| # | Mối nối | Cách gỡ |
|---|---|---|
| 1 | `ImportMerge.NextFreeName` — gọi từ `FinanceWorkbookViewModel.cs:416` (lệnh "Thêm dự án" của 7B) và `FinanceProjectService.cs:111` | Chuyển **nguyên văn** sang `Application/Common/UniqueName.cs`; dời 2 test `NextFreeName` sang tệp test mới |
| 2 | `FinanceExportService.UniqueSheetName` (private, `:132`) — tính năng in cần | **Task 1 của kế hoạch in đã làm** — đây là lý do in chạy trước |
| 3 | `IFinanceProjectService` — một interface cõng hai thế hệ | Gọt còn 5 thành viên: `ListAsync`, `GetTreeAsync`, `ListTreesAsync`, `ReplaceTreeAsync`, `DeleteAsync`. **Tám** cái kia xóa (audit đếm lại: 8, không phải 6 như ghi chú cũ) |
| 4 | `IProjectSummaryService` — 9 thành viên, chỉ 2 còn sống | Gọt còn `GetLatestBatchAsync`, `ListByBatchAsync` |
| 5 | `FinanceWorkbookViewModel` ctor nhận `SummaryPageViewModel` | Bỏ tham số, và sửa 5 tệp test dựng ctor này |
| 6 | `EnumDisplay.ImportResolution` — `EnumDisplay` là tệp **sống** (4 View dùng qua `EnumToVietnameseConverter`) | Chỉ xóa nhánh `ImportResolution`, giữ tệp |
| 7 | `FinanceProjectServiceTreeTests.FakeRepo` — fake viết tay, grep không thấy | Sửa cùng commit gọt `IFinanceProjectRepository` |

## 7. Thứ tự và cổng kiểm

Mỗi bước là **một commit**, và sau mỗi bước `dotnet build ConstructionFinance.sln` + toàn bộ suite phải xanh trước khi sang bước sau.

| Bước | Nội dung |
|---|---|
| 0 | **Tiền đề, không có mã**: (a) user nhập thử một dự án thật qua lưới Vị trí mới và lưu được — hiện `finance_vitris` đang **0 dòng**; (b) tính năng in đã xong |
| 1 | Tách `UniqueName.NextFreeName` |
| 2 | Xóa 4 nhánh nav chết + màn Vendors (giữ `IVendorService`) |
| 3 | Xóa `IAuditService`/`AuditService` |
| 4 | **Commit nguyên khối**: toàn bộ UI phẳng 6A (A1+A2+A3+A4) + phẫu thuật test đi kèm. Không tách được: ctor `FinanceWorkbookViewModel` buộc `SummaryPageViewModel` → `FinanceSummaryViewModel` → `ImportResolutionDialogViewModel` |
| 5 | Tầng Application phẳng: `ProjectCalculator`, `LineItemMapping`, `CalcModels`, hai cặp import/export 6A, phần còn lại của `ImportMerge`, gọt hai interface |
| 6 | Domain phẳng **+ migration trong cùng commit**: `ProjectLineItem`, `ProjectDecision`, các trường HĐ cũ trên `Project`, drop 2 bảng. Kèm test mở DB tạm, `Migrate()`, khẳng định 4 FK con và 3 index UNIQUE còn nguyên |
| 7 | Đổi mẫu trong `ViewLocatorTests` sang cặp còn sống |
| 8 | Xóa đường nhập Excel cũ (nút, wizard, TongHop, 2 service), gọt `IProjectSummaryService` còn 2 thành viên |

Sau các bước **2, 4, 8** phải **chạy app thật và bấm hết các mục nav** — `MainWindowViewModel`, `HostBuilder` và nhánh `switch` điều hướng **không có một test nào**, nên suite xanh hoàn toàn im lặng về đúng những tệp các bước đó sửa nhiều nhất.

## 8. Ngoài phạm vi, có chủ ý

**Nhóm CRUD tiền-6A** (`Attachment`, `CostTemplate`, `Entities.Project`, `Contract`, `CostItem`, `Payment`, `Vendor`, `CostUnitType` + cấu hình/DbSet/bảng) **không** nằm trong 7E. Ba lý do: (a) `Contract`/`Project`/`CostItem`/`Payment` **tới được hôm nay** từ mục "Quyết định" đang sống, qua `DecisionDetailViewModel.cs:19` → `ContractDetailViewModel.cs:19` → `ProjectDetailViewModel.cs:25`, và nhóm KH vốn/Vốn/QĐ sắp làm chính là vùng đó; (b) drop `cost_unit_types` thì phải bỏ `App.axaml.cs:30` + `CostUnitTypeSeeder` nếu không app ném lúc khởi động; (c) 9 trong 12 tên ở `AppDbContextTests.cs:15-26` thuộc nhóm này.

Spec `2026-07-28-vitri-hierarchy-forms-redesign-design.md:171` có câu "drop bảng legacy Entities.*" — **câu đó sai và bị spec này ghi đè**: `AppSetting`, `ApprovalDecision`, `AuditLog` phải sống.

Cũng ngoài phạm vi: gom 7 bản sao định nghĩa cột trong `VitriLedgerView.axaml` về một mối (món nợ riêng, nên trả trước khi làm KH vốn/Vốn/QĐ).

## 9. Rủi ro dữ liệu

`App.axaml.cs:28` chạy `MigrateAsync` trên tệp thật **mỗi lần khởi động**, nên việc drop bảng là **không đảo ngược được** đối với user. Bản sao lưu `~/.local/share/ConstructionFinance/app.db.truoc-7E-20261001-005200` (27 bảng, đã kiểm chứng đọc được) là đường lui duy nhất.

Số dòng sẽ mất: `finance_project_lines` 2 · `finance_project_decisions` 0. Đã soi: dữ liệu duy nhất là mẫu thử — ba lượt import cùng một tệp `Hieu_sample.xlsx` (trùng hash) ra DA1–DA5. **Giữ nguyên** `project_summaries` 15 và `settlement_by_years` 15 vì màn Thông tri sống.

## 10. Cổng nghiệm thu

7E xong khi: build 0 cảnh báo · suite xanh · chạy app bấm hết 5 mục nav không lỗi · Ctrl+S lưu được cây Vị trí · thêm/xóa dự án chạy · màn Thông tri vẫn hiện 15 dòng cũ · nút "Import Excel" đã biến mất và không chỗ nào còn trỏ tới nó.
