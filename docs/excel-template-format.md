# Định dạng file Excel (mẫu H.xlsx)

Ứng dụng đọc file `.xlsx` theo mẫu **H.xlsx** (bản chuẩn) hoặc file làm việc tương tự (ví dụ **Hiệu.xlsx**).

## Sheet bắt buộc / được đọc

| Sheet | Mục đích |
|-------|----------|
| `DA1`, `DA2`, … | Mỗi sheet = một dự án; app đọc **một dòng TỔNG CỘNG** |
| `Tổng hợp` hoặc `TH` | Dùng khi **xuất** dữ liệu tổng hợp từ app |

Sheet khác (`QĐ`, `CT1`, `CT2`, …) **không bị xóa** khi xuất — chỉ sheet Tổng hợp được ghi đè.

## Sheet DA (ví dụ DA1)

- **Dòng 4–5:** Tiêu đề nhóm cột, gồm các nhãn `Quyết toán năm YYYY` hoặc `Quyết toán đến năm YYYY`.
- **Dòng TỔNG CỘNG:** Thường dòng 6; cột A hoặc B chứa chữ `TỔNG CỘNG` / `Tổng cộng`.

### Ánh xạ cột (dòng TỔNG CỘNG)

| Trường trong app | Cột Excel |
|------------------|-----------|
| Giá trị hợp đồng | D |
| Tạm ứng | F |
| Thanh toán KLHT | G |
| Thu hồi tạm ứng | H |
| TT từ DP NSNN | I |
| Tạm ứng chưa thu hồi | J |
| Tổng giá trị đã giải ngân | K |
| Quyết toán theo năm | L trở đi (theo nhãn dòng 5) |

### Cột quyết toán theo năm

App tự nhận năm từ tiêu đề dòng 4. Dòng 5 mô tả cột phụ:

- KLHT / Khối lượng
- Tạm ứng / Ứng
- NSNN / Dự phòng
- DTNH / Ngoại hối / QDTNH

## Sheet Tổng hợp

- Dòng 7–8: Tiêu đề
- Dòng 9+: Dữ liệu từng dự án
- Dòng `Tổng cộng`: Tổng toàn bảng

Khi xuất, app ghi lại các dòng dự án từ snapshot đã import.

## Quy tắc đặt tên sheet dự án

Sheet được coi là dự án nếu tên khớp (không phân biệt hoa thường):

- `DA1`, `DA2`, …
- `Dự án1`, …
- `Project1`, …

## Lỗi thường gặp

| Vấn đề | Hành vi app |
|--------|-------------|
| Ô `#REF!` trên dòng TỔNG CỘNG | Cảnh báo; giá trị tính **0** |
| Không tìm thấy dòng TỔNG CỘNG | Bỏ qua sheet; cảnh báo |
| Sheet không đúng mẫu DA | Bỏ qua |

## Luồng sử dụng

1. **Import Excel** → chọn file → xem trước → Lưu lô import.
2. **Bảng tổng hợp** → xem / lọc theo lô.
3. **Thông tri** → chọn dự án xem snapshot.
4. **Xuất Excel** → chọn file mẫu + đường dẫn lưu → chỉ sheet Tổng hợp được cập nhật.
