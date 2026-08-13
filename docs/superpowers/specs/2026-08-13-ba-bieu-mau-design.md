# Thiết kế: Ba biểu mẫu tự sinh (Thông tri · KS chi · GĐNTT) + số thành chữ — Pha 7D

Ngày: 2026-08-13
Trạng thái: Đã duyệt thiết kế (chờ rà soát spec) → chuyển sang writing-plans
Nguồn tham chiếu: `reference/Mẫu.xlsx`, sheet `Thông tri` / `KS chi` / `GĐNTT` / `Vị trí 1`
Spec cha: `2026-07-28-vitri-hierarchy-forms-redesign-design.md` (§6 và §7 · pha 7D)
Nền đã có: 7A (mô hình + `TreeCalculator`), 7B (lưới Sổ theo dõi), 7C (Tổng hợp + Quyết toán năm)

## 1. Mục tiêu

Sinh ba chứng từ nhà nước thẳng từ dữ liệu đợt đã nhập trong Sổ theo dõi, kèm số tiền
bằng chữ tiếng Việt, và xuất ra `.xlsx` in được:

- **Thông tri** (Mẫu số CQ06-HD) — thu hồi tạm ứng, kinh phí NSNN.
- **KS chi** — phần ghi của cơ quan kiểm soát, thanh toán; cả hai nguồn.
- **GĐNTT** — Giấy đề nghị thanh toán vốn đầu tư; nguồn Quỹ dự trữ ngoại hối.

Spec cha để ngỏ hai điểm, chốt tại đây: **phạm vi đợt đưa vào form** và **cách lưu các ô
nhập tay**.

## 2. Quyết định đã chốt

| Vấn đề | Chốt |
|---|---|
| Đợt nào vào form | Người dùng **tích chọn đợt** trong tab form; có lọc nhanh theo tháng/năm; danh sách đã lọc sẵn theo Nguồn của từng form |
| Ô nhập tay | **Một bộ khai cho mỗi loại form, theo dự án**, lưu trong DB, không quản lý lịch sử phiếu |
| Xuất Excel | Dựng layout **từ code** bằng ClosedXML, **mỗi form một file** |
| Xem trước | Tab dạng **"tờ giấy"** gần giống bản in, ô nhập tay nằm ngay trong bố cục |
| Số thành chữ | Kiểu văn bản nhà nước: **nghìn / linh / mốt / lăm / tư** |
| Phạm vi GĐNTT | **Theo gói thầu**: lũy kế và số dư tạm ứng tính trên chính gói đó |
| Tổ chức code | Builder thuần ở tầng Application; Desktop chỉ hiển thị và thu ô nhập tay |

## 3. Kiến trúc

### 3.1 Application — thư mục mới `Forms/`

| Tệp | Vai trò |
|---|---|
| `VietnameseNumberToWords.cs` | `static string ToWords(decimal)` |
| `FormModels.cs` | `FormDotKey`, `FormDotRow`, `FormOrgInfo`, ba record `*Form` (dữ liệu đã tính sẵn cho bản in). Ô nhập tay **không** khai lại ở đây: builder nhận thẳng ba thực thể `*Declaration` ở §5, giống cách builder hiện nhận `ViTri` |
| `FormRowFlattener.cs` | `IReadOnlyList<ViTri>` → `IReadOnlyList<FormDotRow>` |
| `ThongTriBuilder.cs`, `KsChiBuilder.cs`, `GdnttBuilder.cs` | `Build(all, selectedKeys, declaration)` → `*Form` |
| `FormExcelExporter.cs` + `IFormExcelExporter` | `*Form` → tệp `.xlsx` |

`FormDotRow` mang đủ ngữ cảnh của một đợt để không builder nào phải đi lại cây:

```
FormDotKey(int ViTriIndex, int GroupIndex, int PackageIndex, int DotIndex)

FormDotRow(
    FormDotKey Key,
    string ViTriName, string CostGroupName, string PackageName,
    string? ContractorName, string? BankAccount,
    decimal ContractValue, decimal ContractAnnexValue,
    DateOnly Date, string? Content, string? DocumentNo, Fund Fund,
    decimal Advance, decimal PaymentKLHT, decimal RecoveredAdvance,
    decimal Disbursed)
```

`Disbursed` do `TreeCalculator.Disbursed` tính, không tính lại — một nguồn công thức duy nhất.

Builder nhận **cả** danh sách đầy đủ (`all`) lẫn tập khóa đã chọn (`selectedKeys`) vì GĐNTT
phải nhìn được các đợt *trước kỳ này* của cùng gói thầu.

### 3.2 Desktop — `ViewModels/Finance/Forms/`

- `FormDotPickerViewModel` — danh sách đợt kèm checkbox, tự lọc theo Nguồn của form. Ô lọc
  tháng/năm **thu hẹp danh sách hiển thị**, không tự tích chọn; nút Chọn tất cả / Bỏ chọn chỉ
  tác động lên các dòng đang hiển thị. Đợt đang tích mà bị bộ lọc ẩn đi thì **vẫn nằm trong
  form** — số dòng đang chọn luôn hiện trên đầu tab để không chọn nhầm mà không biết.
- `ThongTriFormViewModel`, `KsChiFormViewModel`, `GdnttFormViewModel` — mỗi VM gồm picker,
  các ô nhập tay, `*Form` hiện hành và lệnh `XuấtExcel`.
- Ba `*FormPageViewModel` (kế thừa `WorkbookPageViewModel`) + ba View `.axaml` bố cục tờ giấy.

Dải tab của dự án thành: `[Tổng hợp][Vị trí…][Quyết toán năm][Thông tri][KS chi][GĐNTT]`.

### 3.3 Luồng tính sống

`FinanceWorkbookViewModel.RefreshAggregateTabs()` hiện gom `ViTriPages → ToViTri()` rồi nạp
hai tab tổng hợp; ba tab form nối vào đúng chỗ đó. Hệ quả: **form ăn cả dữ liệu chưa lưu**,
giống Tổng hợp và Quyết toán năm hôm nay.

Ô tích chọn giữ **tham chiếu tới `LedgerRowViewModel`**, không giữ chỉ số.
`VitriLedgerViewModel.RebuildRows()` không tạo lại object dòng, nên thêm/xóa đợt ở chỗ khác
không làm lệch lựa chọn; lúc dựng form mới quy tham chiếu ra `FormDotKey` theo vị trí hiện tại.

## 4. Công thức từng biểu mẫu

Ký hiệu: `TU` = Tạm ứng, `KLHT` = thanh toán khối lượng hoàn thành, `TH` = Thu hồi tạm ứng,
`GN` = giải ngân đợt = `TU` (đợt tạm ứng) hoặc `KLHT − TH` (đợt thanh toán).

### 4.1 Thông tri — lọc `Fund = NSNN`

Bảng in ba cấp như Mẫu:

| Cấp | Cột NỘI DUNG | Cột SỐ TIỀN |
|---|---|---|
| Dự án | tên dự án | Σ mọi dòng Vị trí |
| Vị trí | tên Vị trí | Σ các đợt đã chọn của Vị trí đó |
| Đợt | `Content` của đợt | `GN` của đợt |

Dòng **Cộng** = tổng dòng Dự án. **Bằng chữ** đọc dòng Cộng. Vị trí không có đợt nào được
chọn thì không in ra. Khối Mục lục ngân sách (Loại NS, Khoản, Mục, Tiểu mục, Tiết mục,
Ngành) là ô nhập tay, in ở cột A–F.

### 4.2 KS chi — cả hai nguồn

Ba cột: **Tổng số**, **Vốn trong nước** (`Fund = NSNN`), **Vốn nước ngoài** (`Fund = NgoaiHoi`).

| Dòng | Nguồn số |
|---|---|
| Số vốn chấp nhận | `Σ(TU + KLHT)` |
| + Số thu hồi tạm ứng | `Σ TH` |
| &nbsp;&nbsp;Các năm trước / Năm nay | nhập tay |
| + Thuế giá trị gia tăng | nhập tay |
| + Số trả đơn vị thụ hưởng | **Chấp nhận − Thu hồi** (bằng đúng `Σ GN`) |
| Số từ chối, Lý do từ chối | nhập tay |
| Ghi chú | ô khai để trống → in `Content` của đợt được chọn đầu tiên theo thứ tự sổ; gõ vào ô khai thì in đúng cái đã gõ |
| Bốn dòng "+ Mục …, tiểu mục …" | nhập tay |

**Bằng chữ** đọc dòng *Số trả đơn vị thụ hưởng* (theo Mẫu: `A18` đọc `D17`).

### 4.3 GĐNTT — lọc `Fund = NgoaiHoi`, phạm vi một gói thầu

**Kỳ trước** = các đợt của chính gói đó đứng **trước đợt được chọn sớm nhất theo thứ tự sổ**
(thứ tự `SortOrder` trong sổ, không theo ngày — sổ mới là thứ tự người dùng thấy).

| Dòng | Nguồn số |
|---|---|
| Số dư tạm ứng của hạng mục | `Σ TU − Σ TH` của gói, tính đến hết kỳ trước |
| Dự toán duyệt / giá trị trúng thầu / giá trị HĐ | `ContractValue + ContractAnnexValue` của gói |
| Lũy kế đã T/toán đến cuối kỳ trước | `Σ(TU + KLHT)` các đợt kỳ trước |
| Số đề nghị TU, thanh toán KLHT kỳ này | `Σ(TU + KLHT)` các đợt đã chọn |
| Trong đó: thanh toán để thu hồi tạm ứng | `Σ TH` các đợt đã chọn |
| Chuyển tiền bảo hành / tạm giữ chờ quyết toán | nhập tay, mặc định 0 |
| Số trả nhà thầu | Đề nghị − Thu hồi − Bảo hành − Tạm giữ |
| Tên đơn vị thụ hưởng / Tài khoản | `ContractorName` / `BankAccount` của gói |
| Tại (ngân hàng) | nhập tay |
| Thuộc nguồn vốn | cố định "Quỹ dự trữ ngoại hối" |

**Bằng chữ** đọc *Số đề nghị kỳ này*. Nếu các đợt đã tích thuộc **nhiều gói thầu**: hiện dải
cảnh báo trong tab và **chặn xuất Excel** cho tới khi chọn lại.

> **Lệch Mẫu có chủ ý (đã duyệt):** cột "Dự toán duyệt / giá trị trúng thầu / giá trị HĐ"
> cộng cả Phụ lục HĐ, trong khi Mẫu chỉ lấy cột Giá trị HĐ (`E9`). Lý do: phụ lục làm thay đổi
> giá trị hợp đồng nên giá trị đề nghị thanh toán phải soi vào tổng.
>
> **Lệch Mẫu có chủ ý:** Mẫu tính "Lũy kế đến cuối kỳ trước" bằng tổng KLHT của **cả Vị trí**
> (`'Vị trí 1'!I7`) trong khi "Số đề nghị kỳ này" chỉ là một đợt — sai khi Vị trí có nhiều gói
> hoặc nhiều nhà thầu. Ở đây lũy kế tính **theo gói thầu**.

### 4.4 Xác nhận tại gate (2026-08-14)

Ba biểu mẫu đã được dựng từ đúng bộ số của sheet `Vị trí 1` trong `Mẫu.xlsx`, xuất ra
`.xlsx` và đối chiếu từng ô (báo cáo: `.superpowers/sdd/2026-08-13-phase7d-ba-bieu-mau/gate-comparison.md`).
KS chi khớp 15/15; Thông tri khớp 8/9 (ô còn lại là nhãn tĩnh "Dự án….." trong Mẫu, không
nối vào ô nào); GĐNTT lệch 6/12 và **mọi chỗ lệch đều giải thích được** — không có lỗi nào
thuộc về code.

Hai chỗ lệch không phải do ta mà do **chính Mẫu**: ô `D28` ("Tài khoản") là chuỗi văn bản
`="Vị trí 1"!D9` gõ nhầm thành text nên không bao giờ được tính; và `H18` lấy số từ đợt
**NSNN** ở dòng 11 trong khi nhãn `A18` của chính nó trỏ sang đợt dòng 12.

Hai câu để ngỏ khi thiết kế, người dùng đã chốt tại gate — **giữ nguyên hành vi đang có**:

- **GĐNTT vẫn lọc `Fund = NgoaiHoi`.** Tiêu đề form ghi rõ "Thuộc nguồn vốn: Quỹ dự trữ
  ngoại hối", nên việc Mẫu kéo một đợt NSNN vào đó là lỗi của Mẫu, không phải căn cứ để bỏ lọc.
- **"Số dư tạm ứng của hạng mục" tính đến hết kỳ trước**, không gồm chính đợt đang đề nghị.
  Với bộ số của Mẫu: 5.000.000 (đến hết kỳ trước) so với 15.000.000 (cả gói, gồm đợt kỳ này —
  bằng cột M của sổ). Hai cách chỉ lệch nhau đúng phần `Tạm ứng − Thu hồi` của đợt được chọn.
  Chọn cách thứ nhất vì kho bạc đọc ô này để đối chiếu thu hồi, tức là số dư *mang sang*.

## 5. Lưu các ô nhập tay

Ba thực thể 1–1 với `Project` trong `Domain.Finance`, mỗi thực thể một bảng, một migration:

| Thực thể / bảng | Trường |
|---|---|
| `ThongTriDeclaration` · `finance_thongtri_decl` | `SoThongTri`, `NgayGhiSo`, `TenCTMT`, `MaSo`, `VeThangNam`, `TaiKhoan1`, `DonVi1`, `SoChungTu1`, `TaiKhoan2`, `DonVi2`, `SoChungTu2`, `LoaiNganSach`, `Khoan`, `Muc`, `TieuMuc`, `TietMuc`, `Nganh` |
| `KsChiDeclaration` · `finance_kschi_decl` | `NgayNhanGiayDeNghi`, `MucTieuMuc1`…`MucTieuMuc4`, `ThuHoiCacNamTruoc`, `ThuHoiNamNay`, `ThueGTGT`, `SoTuChoi`, `LyDoTuChoi`, `GhiChu` |
| `GdnttDeclaration` · `finance_gdntt_decl` | `So`, `NgayThangNam`, `KinhGui`, `TenDuAnCongTrinh`, `ChuDauTu`, `CanCuQuyetDinh`, `CanCuHopDong`, `CanCuGiayDeNghi`, `ThuocKeHoach`, `TaiNganHang`, `ChuyenTienBaoHanh`, `ChuyenTienTamGiu` |

Chuỗi để `null` được, tiền là `decimal` mặc định 0. `GetTreeAsync` nạp kèm ba bảng,
`ReplaceTreeAsync` ghi kèm — **sửa ô nhập tay cũng làm workbook bẩn và Ctrl+S lưu chung**,
không có nút Lưu riêng cho form.

*Đơn vị cấp trên* và *Đơn vị cơ sở* xuất hiện ở cả Thông tri lẫn GĐNTT và giống nhau ở mọi
dự án → hai khóa `AppSetting` mới (`don_vi_cap_tren`, `don_vi_co_so`), thêm hai ô vào màn
Cài đặt › Thông tin đơn vị đã có, gõ một lần dùng chung.

## 6. Số thành chữ

`VietnameseNumberToWords.ToWords(decimal amount)`:

- Làm tròn tới đồng (`MidpointRounding.AwayFromZero`).
- `0` → `"Không đồng."`; số âm → `"Âm … đồng."`.
- Viết hoa chữ cái đầu, kết thúc bằng `" đồng."`.
- Nhóm ba chữ số với hậu tố nghìn / triệu / tỷ; từ 10¹² trở lên đọc "nghìn tỷ".
- Nhóm ở giữa thiếu hàng trăm → "không trăm"; hàng chục 0 mà hàng đơn vị khác 0 → "linh".
- Hàng chục bằng 1 → "mười"; sau "mươi": 1 → "mốt", 4 → "tư", 5 → "lăm"; "mười lăm".

Ví dụ chốt: `10.000.000` → "Mười triệu đồng."; `105.000` → "Một trăm linh năm nghìn đồng.";
`1.234.567.021` → "Một tỷ hai trăm ba mươi tư triệu năm trăm sáu mươi bảy nghìn không trăm
hai mươi mốt đồng."

Bộ test khoảng 30 ca: 0, 1, 10, 11, 15, 21, 24, 25, 100, 101, 105, 1.000, 10.000.000,
15.000.000.000, 1.234.567.021, 10¹², số lẻ được làm tròn, số âm.

## 7. Xuất Excel

`FormExcelExporter` dựng layout từ code: gộp ô, khung viền, Times New Roman 13, định dạng số
`#,##0`, khổ A4 — Thông tri và KS chi in dọc, GĐNTT in ngang (13 cột).

Nút **Xuất Excel** dùng `IDialogService.PickSaveFileAsync` đã có; tên tệp mặc định
`ThongTri_<TênDA>_<yyyyMMdd>.xlsx` (tương tự cho hai form kia), ký tự không hợp lệ bị loại.

## 8. Kiểm thử

- **Builder**: một bộ test cho mỗi công thức ở §4, gồm các ca biên — không đợt nào được chọn,
  chỉ đợt tạm ứng, chỉ đợt thanh toán, đợt trải nhiều Vị trí (Thông tri), đợt trải nhiều gói
  (GĐNTT phải báo lỗi), gói không có kỳ trước (lũy kế = 0).
- **Số thành chữ**: bộ ca ở §6.
- **Xuất Excel**: ghi ra tệp tạm rồi **đọc ngược bằng ClosedXML và so từng ô**, theo đúng cách
  `ExcelExportServiceTests` đang làm.
- **Repo/service**: round-trip ba bảng khai qua `ReplaceTreeAsync` / `GetTreeAsync`.
- Không test UI tự động cho bố cục tờ giấy; kiểm bằng gate ở §9.

## 9. Gate hoàn thành

Người dùng chạy app, tích đợt trong ba tab form, xuất ba tệp `.xlsx` và đối chiếu với
`reference/Mẫu.xlsx` — đúng như bảng pha của spec cha.

## 10. Ngoài phạm vi

- Lịch sử phiếu (nhiều Thông tri theo tháng, xem/in lại) — pha sau nếu cần.
- Xuất PDF — spec cha đã để ngoài phạm vi.
- `ViewModels/ThongTri/ThongTriViewModel.cs` + View của nó (chạy theo `ImportBatch`, có từ
  trước 6A) **không đụng tới** ở 7D; dọn cùng đống legacy ở 7E.
- Sheet KH vốn / Vốn / QĐ — spec riêng về sau.
