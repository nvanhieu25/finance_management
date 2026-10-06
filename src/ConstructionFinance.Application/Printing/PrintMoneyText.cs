using System.Globalization;

namespace ConstructionFinance.Application.Printing;

/// Tiền thành chữ cho bản in MigraDoc. Đường .xlsx không cần lớp này: nó ghi số thật vào ô
/// rồi đặt định dạng "#,##0" để Excel tự hiển thị theo máy người dùng. MigraDoc chỉ nhận
/// chuỗi, nên quy ước phải chốt ở đây: dấu chấm phân nghìn, không phần thập phân.
/// Số 0 ra "0" (không phải ô trống) — đúng như Excel hiển thị ô SetMoney(cell, 0m) của ba
/// biểu mẫu. Ô tiền của sheet bảng không đi qua đây khi bằng 0: PrintCell.OfMoney(0) đã trả
/// PrintCell.Empty từ trước.
public static class PrintMoneyText
{
    private static readonly NumberFormatInfo VietnameseGrouping = new()
    {
        NumberGroupSeparator = ".",
        NumberDecimalDigits = 0,
        NumberGroupSizes = new[] { 3 },
        NegativeSign = "-",
    };

    public static string Format(decimal value) => value.ToString("N", VietnameseGrouping);
}
