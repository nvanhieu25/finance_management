using ConstructionFinance.Application.Forms;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace ConstructionFinance.Application.Printing;

/// Ba biểu mẫu vẽ bằng MigraDoc. Mỗi hàm ở đây là bản chép của hàm cùng tên trong
/// FormExcelExporter, GIỮ NGUYÊN THỨ TỰ DÒNG của bản gốc để người review đối chiếu được từng
/// dòng giữa hai bộ vẽ. Đừng "dọn gọn" lại thứ tự — nó là tài sản của bản chép này.
///
/// Hai chỗ bản chép KHÔNG có dòng tương ứng: bề rộng cột (hoist lên đầu, xem FormLayout) và
/// ApplyPageDefaults(ws, Portrait) — câu đầu tiên của bản Excel. Việc đó (khổ A4, lề, font,
/// hướng giấy) do MigraDocBuilder.ApplyPage làm TRƯỚC khi gọi vào đây.
///
/// MigraDocFormSheetTests so từng ô với bản .xlsx của cùng dữ liệu, nên hai bộ vẽ trôi khỏi
/// nhau là đỏ ngay.
internal static class MigraDocFormSheets
{
    /// Bố cục cột của MỘT biểu mẫu: bề rộng cột (đơn vị "ký tự" của Excel) và hướng giấy.
    ///
    /// Hoist lên đây vì HAI nơi cần cùng ba con số ấy: hàm vẽ dựng FormGrid, và
    /// MigraDocBuilder.FontScaleOf tính cỡ chữ CHÂN TRANG. Khi chỉ hàm vẽ biết, FontScaleOf
    /// rơi xuống 1.0 cho mọi biểu mẫu: chân trang in 13pt trong khi FormGrid.Render co thân
    /// còn 0,768 / 0,944 / 0,914 — đúng ba tờ người dùng ký và gửi Kho bạc, và đúng điều
    /// spec §12.1 nói là KHÔNG xảy ra.
    internal sealed record FormLayout(IReadOnlyList<double> ColumnWidthChars, PrintOrientation Orientation)
    {
        /// Tỉ lệ ExcelFitScale co cả bề rộng cột lẫn cỡ chữ của biểu mẫu này. Tính từ ĐÚNG
        /// hai trường mà FormGrid.Render tính lại, nên chân trang không lệch khỏi thân được.
        public double FontScale => ExcelFitScale.ScaleFor(ColumnWidthChars, Orientation);
    }

    /// Bề rộng cột ở cuối FormExcelExporter.WriteThongTriSheet (Columns(1,6)=9, Column(7)=52,
    /// Column(8)=18) — phải khai TRƯỚC ở đây, vì FormGrid cần biết số cột lúc dựng.
    internal static readonly FormLayout ThongTriLayout =
        new(new double[] { 9, 9, 9, 9, 9, 9, 52, 18 }, PrintOrientation.Portrait);

    /// Bề rộng cột ở cuối bản gốc KS chi (Column(1)=34, Column(2)=14, Columns(3,5)=18).
    internal static readonly FormLayout KsChiLayout =
        new(new double[] { 34, 14, 18, 18, 18 }, PrintOrientation.Portrait);

    /// GĐNTT: chín cột, 26 rồi tám cột 16 (Column(1)=26, Columns(2,9)=16). Giấy NGANG.
    internal static readonly FormLayout GdnttLayout =
        new(new double[] { 26, 16, 16, 16, 16, 16, 16, 16, 16 }, PrintOrientation.Landscape);

    /// Bố cục của một sheet biểu mẫu. Ném cho record con chưa khai bố cục: thêm biểu mẫu thứ
    /// tư (KH vốn / Vốn / QĐ...) mà quên khai thì vỡ ở lần dựng đầu, chứ không in ra một tờ
    /// có chân trang sai cỡ chữ.
    internal static FormLayout LayoutOf(PrintFormSheet sheet) => sheet switch
    {
        ThongTriPrintSheet => ThongTriLayout,
        KsChiPrintSheet => KsChiLayout,
        GdnttPrintSheet => GdnttLayout,
        _ => throw new NotSupportedException(
            $"Sheet \"{sheet.SheetName}\": chưa khai bố cục cột MigraDoc cho {sheet.GetType().Name}."),
    };

    /// Chép FormExcelExporter.WriteThongTriSheet. Bề rộng cột và hướng giấy ở ThongTriLayout
    /// (hoist lên đầu lớp vì chân trang cũng cần, xem FormLayout).
    public static Table WriteThongTri(Section section, ThongTriForm form)
    {
        var grid = new FormGrid(ThongTriLayout.ColumnWidthChars);

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

        return grid.Render(section, ThongTriLayout.Orientation);
    }

    /// Chép FormExcelExporter.WriteKsChiSheet. Bề rộng cột và hướng giấy ở KsChiLayout.
    public static Table WriteKsChi(Section section, KsChiForm form)
    {
        var grid = new FormGrid(KsChiLayout.ColumnWidthChars);

        grid.Text(1, 1, "PHẦN GHI CỦA CƠ QUAN KIỂM SOÁT, THANH TOÁN");
        grid.Merge(1, 1, 1, 5);
        grid.Bold(1, 1, 1, 1);
        grid.Center(1, 1, 1, 1);

        grid.Text(3, 1, "Ngày nhận giấy đề nghị thanh toán vốn đầu tư:");
        grid.Text(3, 4, $"Ngày {FormExcelExporter.Blank(form.NgayNhanGiayDeNghi)}");
        grid.Text(5, 5, "Đơn vị tính: đồng");

        grid.Text(6, 1, "Nội dung");
        grid.Merge(6, 1, 6, 2);
        grid.Text(6, 3, "Tổng số");
        grid.Text(6, 4, "Vốn trong nước");
        grid.Text(6, 5, "Vốn nước ngoài");
        grid.Bold(6, 1, 6, 5);
        grid.Center(6, 1, 6, 5);

        WriteKsChiAmountRow(grid, 7, "Số vốn chấp nhận", form.Accepted);

        // Bốn dòng "+ Mục ..., tiểu mục ..." — dòng nào bỏ trống thì in mẫu chấm.
        for (var index = 0; index < 4; index++)
        {
            var text = form.MucTieuMucLines[index];
            grid.Text(8 + index, 1, string.IsNullOrWhiteSpace(text)
                ? " + Mục ........, tiểu mục ........"
                : $" + {text}");
        }

        grid.Text(12, 1, "Trong đó:");
        WriteKsChiAmountRow(grid, 13, " + Số thu hồi tạm ứng", form.Recovered);
        grid.Text(14, 1, "  Các năm trước");
        grid.Money(14, 3, form.ThuHoiCacNamTruoc);
        grid.Text(15, 1, "  Năm nay");
        grid.Money(15, 3, form.ThuHoiNamNay);
        grid.Text(16, 1, " + Thuế giá trị gia tăng");
        grid.Money(16, 3, form.ThueGTGT);
        WriteKsChiAmountRow(grid, 17, " + Số trả đơn vị thụ hưởng", form.PaidToBeneficiary);
        grid.Bold(17, 1, 17, 5);

        grid.Text(18, 1, form.PaidInWords);
        grid.Merge(18, 1, 18, 5);

        grid.Text(19, 1, "Số từ chối:");
        grid.Money(19, 3, form.SoTuChoi);
        grid.Text(19, 4, "đồng");
        grid.Text(20, 1, $"Lý do từ chối: {form.LyDoTuChoi ?? ""}");
        grid.Merge(20, 1, 20, 5);

        grid.Text(22, 1, "Ghi chú:");
        grid.Text(22, 2, form.GhiChu);
        grid.Merge(22, 2, 22, 5);

        grid.Border(6, 1, 17, 5);

        return grid.Render(section, KsChiLayout.Orientation);
    }

    /// Chép WriteAmountRow của FormExcelExporter.
    private static void WriteKsChiAmountRow(FormGrid grid, int row, string label, KsChiAmounts amounts)
    {
        grid.Text(row, 1, label);
        grid.Merge(row, 1, row, 2);
        grid.Money(row, 3, amounts.Total);
        grid.Money(row, 4, amounts.NSNN);
        grid.Money(row, 5, amounts.NgoaiHoi);
    }

    /// Chép FormExcelExporter.WriteGdnttSheet. Bề rộng cột và hướng giấy ở GdnttLayout.
    public static Table WriteGdntt(Section section, GdnttForm form)
    {
        var grid = new FormGrid(GdnttLayout.ColumnWidthChars);

        grid.Text(1, 1, form.DonViCapTren ?? "");
        grid.Text(2, 1, form.DonViCoSo ?? "");
        grid.Text(3, 1, $"Số: {FormExcelExporter.Blank(form.So)}");
        grid.Text(1, 4, "CỘNG HOÀ XÃ HỘI CHỦ NGHĨA VIỆT NAM");
        grid.Text(2, 4, "Độc lập - Tự do - Hạnh phúc");
        grid.Text(3, 4, $"Ngày {FormExcelExporter.Blank(form.NgayThangNam)}");
        foreach (var row in new[] { 1, 2, 3 })
        {
            grid.Merge(row, 4, row, 9);
            grid.Center(row, 4, row, 4);
        }
        grid.Bold(1, 4, 1, 4);
        grid.Bold(2, 4, 2, 4);

        grid.Text(4, 1, "GIẤY ĐỀ NGHỊ THANH TOÁN VỐN ĐẦU TƯ");
        grid.Merge(4, 1, 4, 9);
        grid.Bold(4, 1, 4, 1);
        grid.Center(4, 1, 4, 1);

        grid.Text(6, 1, $"Kính gửi: {form.KinhGui ?? ""}");
        grid.Text(8, 1, $" - Tên dự án công trình: {form.TenDuAnCongTrinh ?? ""}");
        grid.Text(9, 1, $" - Chủ đầu tư/Ban QLDA: {form.ChuDauTu ?? ""}");
        grid.Text(10, 1, $" - Căn cứ Quyết định số: {form.CanCuQuyetDinh ?? ""}");
        grid.Text(11, 1, $" - Căn cứ Hợp đồng số: {form.CanCuHopDong ?? ""}");
        grid.Text(12, 1, $" - Căn cứ giấy đề nghị thanh toán vốn đầu tư số: {form.CanCuGiayDeNghi ?? ""}");
        grid.Text(13, 1, " - Số dư tạm ứng của hạng mục đề nghị thanh toán:");
        grid.Money(13, 4, form.UnrecoveredAdvanceBefore);
        grid.Text(14, 1, " - Số tiền đề nghị (tạm ứng, thanh toán KLHT) đến kỳ này:");
        grid.Text(15, 1, $" - Thuộc nguồn vốn: {form.NguonVon}");
        grid.Text(16, 1, $" - Thuộc kế hoạch: {form.ThuocKeHoach ?? ""}");
        // Khác bản Excel: các dòng 6–16 này XUỐNG DÒNG chứ không bị cắt ở biên dải gộp 1→3 (Excel
        // làm mất đuôi dòng dài, vd số giấy đề nghị). Xem Gdntt_dong_dai_van_vua_mot_trang.
        foreach (var row in Enumerable.Range(6, 11)) grid.Merge(row, 1, row, 3);

        grid.Text(17, 1, "NỘI DUNG");
        grid.Merge(17, 1, 17, 3);
        grid.Text(17, 4, "Dự toán duyệt hoặc giá trị trúng thầu hoặc giá trị HĐ");
        grid.Merge(17, 4, 17, 5);
        grid.Text(17, 6, "Lũy kế số vốn đã T/Toán từ K/công đến cuối kỳ trước (Gồm cả tạm ứng)");
        grid.Merge(17, 6, 17, 7);
        grid.Text(17, 8, "Số đề nghị tạm ứng, thanh toán KLHT kỳ này (Gồm cả tạm ứng)");
        grid.Merge(17, 8, 17, 9);
        grid.Bold(17, 1, 17, 9);
        grid.Center(17, 1, 17, 9);

        grid.Text(18, 1, form.PackageName);
        grid.Merge(18, 1, 18, 3);
        WriteGdnttMoneyMerged(grid, 18, 4, 5, form.ContractTotal);
        WriteGdnttMoneyMerged(grid, 18, 6, 7, form.CumulativeBefore);
        WriteGdnttMoneyMerged(grid, 18, 8, 9, form.RequestedThisPeriod);

        grid.Text(19, 1, "CỘNG");
        grid.Merge(19, 1, 19, 3);
        // Cùng ba cặp gộp 4:5 / 6:7 / 8:9 như dòng 18 — trong cùng một khung kẻ, cột của
        // dòng CỘNG phải thẳng với dòng gói thầu bên trên.
        WriteGdnttMoneyMerged(grid, 19, 4, 5, form.ContractTotal);
        WriteGdnttMoneyMerged(grid, 19, 6, 7, form.CumulativeBefore);
        WriteGdnttMoneyMerged(grid, 19, 8, 9, form.RequestedThisPeriod);
        grid.Bold(19, 1, 19, 9);

        grid.Border(17, 1, 19, 9);

        grid.Text(20, 1, " - Tổng số tiền đề nghị tạm ứng, thanh toán bằng số:");
        grid.Money(20, 8, form.RequestedThisPeriod);
        grid.Text(21, 1, " - Bằng chữ:");
        grid.Text(21, 2, form.RequestedInWords);
        grid.Merge(21, 2, 21, 9);
        grid.Text(22, 1, "Trong đó:");
        grid.Text(23, 2, " - Thanh toán để thu hồi tạm ứng:");
        grid.Money(23, 8, form.RecoveredThisPeriod);
        grid.Text(24, 2, " - Chuyển tiền bảo hành:");
        grid.Money(24, 8, form.ChuyenTienBaoHanh);
        grid.Text(25, 2, " - Chuyển tiền tạm giữ chờ quyết toán:");
        grid.Money(25, 8, form.ChuyenTienTamGiu);
        grid.Text(26, 2, " - Số trả nhà thầu:");
        grid.Money(26, 8, form.PaidToContractor);
        grid.Bold(26, 1, 26, 9);

        grid.Text(27, 1, " - Tên đơn vị thụ hưởng:");
        grid.Text(27, 3, form.ContractorName ?? "");
        grid.Text(28, 1, " - Tài khoản:");
        grid.Text(28, 4, form.BankAccount ?? "");
        grid.Text(29, 1, " - Tại:");
        grid.Text(29, 2, form.TaiNganHang ?? "");
        grid.Merge(29, 2, 29, 9);

        grid.Text(31, 1, "Trưởng phòng Tài chính");
        grid.Text(31, 5, "Chủ đầu tư");
        grid.Merge(31, 1, 31, 3);
        grid.Merge(31, 5, 31, 9);
        grid.Center(31, 1, 31, 9);

        return grid.Render(section, GdnttLayout.Orientation);
    }

    /// Tiền ghi vào ô ĐẦU của một dải gộp — bảng tổng hợp GĐNTT gộp 4:5 / 6:7 / 8:9 trên cả
    /// dòng gói thầu và dòng CỘNG bên dưới.
    private static void WriteGdnttMoneyMerged(FormGrid grid, int row, int fromColumn, int toColumn, decimal value)
    {
        grid.Money(row, fromColumn, value);
        grid.Merge(row, fromColumn, row, toColumn);
    }
}
