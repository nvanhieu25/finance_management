# Tổng hợp: hai cột tiền khác tên nhưng lấy cùng một số

**Phát hiện:** review tổng cuối của tính năng In (2026-10-02)
**Trạng thái:** CHƯA sửa — chờ quyết định nghiệp vụ của người dùng
**Mức độ:** nghi là lỗi thật, nhưng là lỗi CÓ SẴN trên màn hình, không do tính năng In sinh ra

## Hiện tượng

Trên màn **Tổng hợp**, hai cột có tiêu đề khác nhau luôn hiển thị **cùng một con số**:

| Cột trên màn hình | Thuộc tính | Nguồn |
|---|---|---|
| `TT cho nhà thầu` | `SummaryDisplayRow.DisbursedToContractor` | `a.TotalDisbursed` |
| `Tổng đã giải ngân` | `SummaryDisplayRow.TotalDisbursed` | `a.TotalDisbursed` |

Cả hai gán ở [SummaryDisplayRow.cs](../../src/ConstructionFinance.Desktop/ViewModels/Finance/SummaryDisplayRow.cs) trong `From(WorkbookSummaryRow)`, và hai tiêu đề nằm ở [ProjectTongHopView.axaml:24](../../src/ConstructionFinance.Desktop/Views/Finance/ProjectTongHopView.axaml#L24) và [:28](../../src/ConstructionFinance.Desktop/Views/Finance/ProjectTongHopView.axaml#L28).

## Vì sao nghi là lỗi

"Thanh toán cho nhà thầu" và "Tổng đã giải ngân" là **hai đại lượng khác nhau** trong nghiệp vụ xây dựng cơ bản. Hiện `TotalDisbursed` được tính là số **ròng**:

- `Disbursed(đợt) = Tạm ứng + Thanh toán KLHT − Thu hồi tạm ứng` ([TreeCalculator.cs:18](../../src/ConstructionFinance.Application/Calc/TreeCalculator.cs#L18))
- `TotalDisbursed = phần NSNN + phần Ngoại hối` ([TreeCalculator.cs:34](../../src/ConstructionFinance.Application/Calc/TreeCalculator.cs#L34))

Nếu "TT cho nhà thầu" có ý nghĩa là số **gộp** nhà thầu đã nhận (`SumAdvance + SumKLHT`, chưa trừ thu hồi) thì hai cột phải khác nhau mỗi khi có thu hồi tạm ứng.

**Chỉ người dùng mới trả lời được** hai cột này phải mang nghĩa gì — đây là quy tắc nghiệp vụ, không phải suy luận từ mã.

## Vì sao tính năng In không tự sửa

Bản in phải **khớp màn hình**. Nếu bản in tự tính khác đi thì tờ giấy và màn hình nói hai điều khác nhau — tệ hơn là cả hai cùng sai giống nhau. Nên tính năng In **cố ý giữ nguyên**, và ghi chú điều đó tại chỗ ([ProjectTongHopPageViewModel.cs](../../src/ConstructionFinance.Desktop/ViewModels/Finance/ProjectTongHopPageViewModel.cs)).

Hệ quả mới: trước đây hai cột trùng nhau chỉ hiện trên màn hình; **từ nay chúng được in ra báo cáo chính thức**. Người duyệt hoặc kiểm toán nhìn hai cột tiền giống hệt nhau trong một báo cáo sẽ hỏi.

## Phải sửa gì khi quyết định xong

1. `SummaryDisplayRow.From` — đổi nguồn của `DisbursedToContractor`.
2. `WorkbookSummaryRow` / `TreeAggregate` — nếu đại lượng mới chưa có sẵn thì phải thêm.
3. **`AssertTongHopMoney` trong `PrintablePageTests.cs` đang GHIM cả hai cột về cùng `6_750_000m`** — tức test hiện tại đang *bảo vệ* sự trùng lặp này. Sửa nguồn mà không sửa test thì test đỏ, và đó là đúng.
4. Kiểm lại `WorkbookSummaryView.axaml` — cũng bind cùng bộ thuộc tính.

## Liên quan

- Tính năng In: [kế hoạch](../superpowers/plans/2026-09-10-in-qua-phan-mem.md), [thiết kế](../superpowers/specs/2026-09-10-in-qua-phan-mem-design.md)
