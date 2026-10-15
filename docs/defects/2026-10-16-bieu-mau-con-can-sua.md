# Ba biểu mẫu (Thông tri / KS chi / GĐNTT): còn cần sửa thêm

**Ghi lúc:** 2026-10-16, cuối phiên làm biểu mẫu đúng Mẫu + 7E + in/PDF + kéo cỡ ô
**Nhánh:** `refactor/vitri-hierarchy-7` (chưa merge vào `main`)
**Trạng thái:** CHƯA sửa — người dùng nhận xét "vẫn cần sửa thêm" sau khi thử bản có kéo cỡ ô; chưa nêu cụ thể. Mục A để người dùng bổ sung, các mục B–E là việc tôi biết còn treo.

## A. Điều người dùng thấy cần sửa (bổ sung khi mở lại)

- [ ] _(chưa ghi — người dùng sẽ tự nêu khi sửa tiếp)_

## B. Hàng Mục lục (010, 011, 9300, 9301, 00, 00) có lúc hiện trống

- Người dùng báo các ô này trống; **không tái hiện được** trong test (bấm ô, chọn đợt, đổi tab qua lại đều giữ nguyên giá trị).
- Đã làm: ô điền sẵn bị xoá sạch thì hiện **chữ mờ** nhắc giá trị Mẫu (`FormSheetResolver.ResolveText`). Chưa xác nhận đây có phải nguyên nhân thật.
- Nếu còn gặp: ghi lại các bước ngay trước khi ô trống (có chọn đợt không, có Ctrl+S / đóng mở lại không, tab nào) rồi tái hiện từ đó. Nghi vấn còn lại: giá trị rỗng được lưu vào `finance_form_inputs` rồi đè giá trị mặc định khi nạp lại.

## C. Kéo cột/hàng chưa được thử bằng tay

- Chỉ có test headless (`FormSheetControlTests`, `FormPageViewModelTests`). Chưa ai kéo thử trong app thật.
- Cần kiểm: con trỏ đổi hình đúng chỗ, vạch dẫn hiện, thả ra áp cỡ, Ctrl+S rồi mở lại còn giữ, "Đặt lại cỡ ô", Xuất Excel/PDF theo cỡ mới.
- Hàng trong vùng lặp của Thông tri được lưu **theo loại hàng** (`proj`/`vitri`/`dot`/`blank`), không theo từng dòng — kéo một dòng đợt là mọi dòng đợt đổi. Nếu người dùng muốn chỉnh riêng từng dòng thì phải đổi khoá lưu.

## D. In trực tiếp chưa thử trên Windows

- `IPrinterPort` chỉ có ở bản Windows (`WindowsPrinterPort`, GDI). Trên Linux nút in trực tiếp xám; dùng **Xuất PDF để in** thay thế.
- Cần chạy thử trên máy Windows có máy in: `dotnet publish -r win-x64`, kiểm khổ giấy, lề, tỷ lệ, ngắt trang của cả 3 biểu mẫu.

## E. Sổ cái nhiều dòng mở lần đầu chậm

- Sổ 300–400 dòng mở lần đầu ~4–5 giây (chuyển tab sau đó dưới 0,2 giây vì đã giữ view). Hướng sửa: ảo hoá hàng (virtualization). Chưa làm.

## F. Việc kỹ thuật kèm theo

- Các commit sau lần push đầu mới được đẩy lên cùng ghi chú này.
- Token GitHub người dùng dán trong phiên cũ **phải thu hồi** (Settings → Developer settings → Personal access tokens). Lần sau dùng SSH key hoặc token mới.
- `reference/TongHop.xlsx` còn untracked, chưa quyết định có đưa vào repo không.
