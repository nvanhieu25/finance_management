using ConstructionFinance.Application.Forms;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace ConstructionFinance.Application.Printing;

/// Ba biểu mẫu vẽ bằng MigraDoc. Mỗi hàm ở đây là bản chép của hàm cùng tên trong
/// FormExcelExporter, GIỮ NGUYÊN THỨ TỰ DÒNG của bản gốc để người review đối chiếu được từng
/// dòng giữa hai bộ vẽ. Đừng "dọn gọn" lại thứ tự — nó là tài sản của bản chép này.
///
/// MigraDocFormSheetTests so từng ô với bản .xlsx của cùng dữ liệu, nên hai bộ vẽ trôi khỏi
/// nhau là đỏ ngay.
internal static class MigraDocFormSheets
{
    /// Chép FormExcelExporter.WriteThongTriSheet. Bề rộng cột ở cuối bản gốc
    /// (Columns(1,6)=9, Column(7)=52, Column(8)=18) phải khai TRƯỚC ở đây, vì FormGrid cần
    /// biết số cột lúc dựng.
    public static Table WriteThongTri(Section section, ThongTriForm form)
    {
        var grid = new FormGrid(new double[] { 9, 9, 9, 9, 9, 9, 52, 18 });

        grid.Text(1, 1, form.DonViCapTren ?? "");
        grid.Text(1, 7, "Mẫu số CQ06-HD");
        grid.Text(2, 1, form.DonViCoSo ?? "");
        grid.Text(3, 1, $"Số      {FormExcelExporter.Blank(form.SoThongTri)}");
        grid.Text(3, 7, $"Ghi sổ ngày {FormExcelExporter.Blank(form.NgayGhiSo)}");

        grid.Text(4, 1, "THÔNG TRI");
        grid.Text(5, 1, "Thu hồi tạm ứng");
        grid.Text(6, 1, "Kinh phí: NSNN");
        foreach (var row in new[] { 4, 5, 6 })
        {
            grid.Merge(row, 1, row, 8);
            grid.Center(row, 1, row, 1);
        }
        grid.Bold(4, 1, 4, 1);

        grid.Text(8, 1, "Đơn vị:");
        grid.Text(8, 7, $" TK{FormExcelExporter.Blank(form.TaiKhoan1)}  Đơn vị{FormExcelExporter.Blank(form.DonVi1)}  Số CT{FormExcelExporter.Blank(form.SoChungTu1)}");
        grid.Text(9, 1, $"Về {FormExcelExporter.Blank(form.VeThangNam)}");
        grid.Text(9, 7, $" TK{FormExcelExporter.Blank(form.TaiKhoan2)}  Đơn vị{FormExcelExporter.Blank(form.DonVi2)}  Số CT{FormExcelExporter.Blank(form.SoChungTu2)}");
        grid.Text(10, 1, $"Tên CTMT, DA: {FormExcelExporter.Blank(form.TenCTMT)}    Mã số: {FormExcelExporter.Blank(form.MaSo)}");
        grid.Text(11, 7, "Đơn vị tính: Đồng");

        // Đầu bảng: hai dòng 12–13 gộp dọc, dòng 14 là dòng chữ cái A..G như Mẫu.
        grid.Text(12, 1, "Loại ngân sách");
        grid.Merge(12, 1, 13, 1);
        grid.Text(12, 2, "Mục lục ngân sách");
        grid.Merge(12, 2, 12, 6);
        grid.Text(13, 2, "Khoản");
        grid.Text(13, 3, "Mục");
        grid.Text(13, 4, "Tiểu mục");
        grid.Text(13, 5, "Tiết mục");
        grid.Text(13, 6, "Ngành");
        grid.Text(12, 7, "NỘI DUNG");
        grid.Merge(12, 7, 13, 7);
        grid.Text(12, 8, "SỐ TIỀN");
        grid.Merge(12, 8, 13, 8);
        grid.Bold(12, 1, 13, 8);
        grid.Center(12, 1, 13, 8);

        var letters = new[] { "A", "B", "C", "D", "E", "F", "G", "1" };
        for (var column = 0; column < letters.Length; column++)
        {
            grid.Text(14, column + 1, letters[column]);
            grid.Center(14, column + 1, 14, column + 1);
        }

        // Khối Mục lục ngân sách nằm trên dòng nội dung đầu tiên (dòng 15), như Mẫu.
        grid.Text(15, 1, form.LoaiNganSach ?? "");
        grid.Text(15, 2, form.Khoan ?? "");
        grid.Text(15, 3, form.Muc ?? "");
        grid.Text(15, 4, form.TieuMuc ?? "");
        grid.Text(15, 5, form.TietMuc ?? "");
        grid.Text(15, 6, form.Nganh ?? "");

        var currentRow = 15;
        foreach (var line in form.Lines)
        {
            grid.Text(currentRow, 7, line.Content);
            grid.Indent(currentRow, 7, line.Level);
            grid.Money(currentRow, 8, line.Amount);
            // Chỉ đậm hai ô Nội dung/Số tiền, không cả dòng: dòng 15 còn mang các giá trị
            // Mục lục ngân sách ở cột 1–6, phải để thường.
            if (line.Level == 0) grid.Bold(currentRow, 7, currentRow, 8);
            currentRow++;
        }

        grid.Text(currentRow, 7, "Cộng");
        grid.Bold(currentRow, 7, currentRow, 7);
        grid.Money(currentRow, 8, form.Total);
        grid.Bold(currentRow, 8, currentRow, 8);

        grid.Border(12, 1, currentRow, 8);

        currentRow++;
        grid.Text(currentRow, 1, $"Bằng chữ: {form.TotalInWords}");
        grid.Merge(currentRow, 1, currentRow, 8);

        return grid.Render(section, PrintOrientation.Portrait);
    }
}
