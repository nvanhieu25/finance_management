using ClosedXML.Excel;

namespace ConstructionFinance.Application.Forms;

public partial class FormExcelExporter
{
    public Task ExportKsChiAsync(KsChiForm form, string targetPath, CancellationToken ct = default) =>
        Task.Run(() => WriteKsChi(form, targetPath), ct);

    public Task ExportGdnttAsync(GdnttForm form, string targetPath, CancellationToken ct = default) =>
        Task.Run(() => WriteGdntt(form, targetPath), ct);

    private static void WriteKsChi(KsChiForm form, string targetPath)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("KS chi");
        WriteKsChiSheet(ws, form);
        wb.SaveAs(targetPath);
    }

    /// Như WriteThongTriSheet: vẽ trọn biểu mẫu (cả font, khổ giấy, lề) lên worksheet có sẵn.
    internal static void WriteKsChiSheet(IXLWorksheet ws, KsChiForm form)
    {
        ApplyPageDefaults(ws, XLPageOrientation.Portrait);

        ws.Cell(1, 1).Value = "PHẦN GHI CỦA CƠ QUAN KIỂM SOÁT, THANH TOÁN";
        ws.Range(1, 1, 1, 5).Merge();
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Cell(3, 1).Value = "Ngày nhận giấy đề nghị thanh toán vốn đầu tư:";
        ws.Cell(3, 4).Value = $"Ngày {Blank(form.NgayNhanGiayDeNghi)}";
        ws.Cell(5, 5).Value = "Đơn vị tính: đồng";

        ws.Cell(6, 1).Value = "Nội dung";
        ws.Range(6, 1, 6, 2).Merge();
        ws.Cell(6, 3).Value = "Tổng số";
        ws.Cell(6, 4).Value = "Vốn trong nước";
        ws.Cell(6, 5).Value = "Vốn nước ngoài";
        ws.Range(6, 1, 6, 5).Style.Font.Bold = true;
        ws.Range(6, 1, 6, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        WriteAmountRow(ws, 7, "Số vốn chấp nhận", form.Accepted);

        // Bốn dòng "+ Mục ..., tiểu mục ..." — dòng nào bỏ trống thì in mẫu chấm.
        for (var i = 0; i < 4; i++)
        {
            var text = form.MucTieuMucLines[i];
            ws.Cell(8 + i, 1).Value = string.IsNullOrWhiteSpace(text)
                ? " + Mục ........, tiểu mục ........"
                : $" + {text}";
        }

        ws.Cell(12, 1).Value = "Trong đó:";
        WriteAmountRow(ws, 13, " + Số thu hồi tạm ứng", form.Recovered);
        ws.Cell(14, 1).Value = "  Các năm trước";
        SetMoney(ws.Cell(14, 3), form.ThuHoiCacNamTruoc);
        ws.Cell(15, 1).Value = "  Năm nay";
        SetMoney(ws.Cell(15, 3), form.ThuHoiNamNay);
        ws.Cell(16, 1).Value = " + Thuế giá trị gia tăng";
        SetMoney(ws.Cell(16, 3), form.ThueGTGT);
        WriteAmountRow(ws, 17, " + Số trả đơn vị thụ hưởng", form.PaidToBeneficiary);
        ws.Row(17).Style.Font.Bold = true;

        ws.Cell(18, 1).Value = form.PaidInWords;
        ws.Range(18, 1, 18, 5).Merge();

        ws.Cell(19, 1).Value = "Số từ chối:";
        SetMoney(ws.Cell(19, 3), form.SoTuChoi);
        ws.Cell(19, 4).Value = "đồng";
        ws.Cell(20, 1).Value = $"Lý do từ chối: {form.LyDoTuChoi ?? ""}";
        ws.Range(20, 1, 20, 5).Merge();

        ws.Cell(22, 1).Value = "Ghi chú:";
        ws.Cell(22, 2).Value = form.GhiChu;
        ws.Range(22, 2, 22, 5).Merge();

        ws.Range(6, 1, 17, 5).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(6, 1, 17, 5).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        ws.Column(1).Width = 34;
        ws.Column(2).Width = 14;
        ws.Columns(3, 5).Width = 18;
    }

    private static void WriteAmountRow(IXLWorksheet ws, int row, string label, KsChiAmounts amounts)
    {
        ws.Cell(row, 1).Value = label;
        ws.Range(row, 1, row, 2).Merge();
        SetMoney(ws.Cell(row, 3), amounts.Total);
        SetMoney(ws.Cell(row, 4), amounts.NSNN);
        SetMoney(ws.Cell(row, 5), amounts.NgoaiHoi);
    }

    /// Writes a money value into the first column of a merged span — the GĐNTT summary
    /// table merges 4:5 / 6:7 / 8:9 on both the package row and the CỘNG row below it.
    private static void SetMoneyMerged(IXLWorksheet ws, int row, int fromCol, int toCol, decimal value)
    {
        SetMoney(ws.Cell(row, fromCol), value);
        ws.Range(row, fromCol, row, toCol).Merge();
    }

    private static void WriteGdntt(GdnttForm form, string targetPath)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("GĐNTT");
        WriteGdnttSheet(ws, form);
        wb.SaveAs(targetPath);
    }

    /// Như WriteThongTriSheet: vẽ trọn biểu mẫu (cả font, khổ giấy, lề) lên worksheet có sẵn.
    internal static void WriteGdnttSheet(IXLWorksheet ws, GdnttForm form)
    {
        ApplyPageDefaults(ws, XLPageOrientation.Landscape);

        ws.Cell(1, 1).Value = form.DonViCapTren ?? "";
        ws.Cell(2, 1).Value = form.DonViCoSo ?? "";
        ws.Cell(3, 1).Value = $"Số: {Blank(form.So)}";
        ws.Cell(1, 4).Value = "CỘNG HOÀ XÃ HỘI CHỦ NGHĨA VIỆT NAM";
        ws.Cell(2, 4).Value = "Độc lập - Tự do - Hạnh phúc";
        ws.Cell(3, 4).Value = $"Ngày {Blank(form.NgayThangNam)}";
        foreach (var r in new[] { 1, 2, 3 })
        {
            ws.Range(r, 4, r, 9).Merge();
            ws.Cell(r, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        ws.Cell(1, 4).Style.Font.Bold = true;
        ws.Cell(2, 4).Style.Font.Bold = true;

        ws.Cell(4, 1).Value = "GIẤY ĐỀ NGHỊ THANH TOÁN VỐN ĐẦU TƯ";
        ws.Range(4, 1, 4, 9).Merge();
        ws.Cell(4, 1).Style.Font.Bold = true;
        ws.Cell(4, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Cell(6, 1).Value = $"Kính gửi: {form.KinhGui ?? ""}";
        ws.Cell(8, 1).Value = $" - Tên dự án công trình: {form.TenDuAnCongTrinh ?? ""}";
        ws.Cell(9, 1).Value = $" - Chủ đầu tư/Ban QLDA: {form.ChuDauTu ?? ""}";
        ws.Cell(10, 1).Value = $" - Căn cứ Quyết định số: {form.CanCuQuyetDinh ?? ""}";
        ws.Cell(11, 1).Value = $" - Căn cứ Hợp đồng số: {form.CanCuHopDong ?? ""}";
        ws.Cell(12, 1).Value = $" - Căn cứ giấy đề nghị thanh toán vốn đầu tư số: {form.CanCuGiayDeNghi ?? ""}";
        ws.Cell(13, 1).Value = " - Số dư tạm ứng của hạng mục đề nghị thanh toán:";
        SetMoney(ws.Cell(13, 4), form.UnrecoveredAdvanceBefore);
        ws.Cell(14, 1).Value = " - Số tiền đề nghị (tạm ứng, thanh toán KLHT) đến kỳ này:";
        ws.Cell(15, 1).Value = $" - Thuộc nguồn vốn: {form.NguonVon}";
        ws.Cell(16, 1).Value = $" - Thuộc kế hoạch: {form.ThuocKeHoach ?? ""}";
        foreach (var r in Enumerable.Range(6, 11)) ws.Range(r, 1, r, 3).Merge();

        ws.Cell(17, 1).Value = "NỘI DUNG";
        ws.Range(17, 1, 17, 3).Merge();
        ws.Cell(17, 4).Value = "Dự toán duyệt hoặc giá trị trúng thầu hoặc giá trị HĐ";
        ws.Range(17, 4, 17, 5).Merge();
        ws.Cell(17, 6).Value = "Lũy kế số vốn đã T/Toán từ K/công đến cuối kỳ trước (Gồm cả tạm ứng)";
        ws.Range(17, 6, 17, 7).Merge();
        ws.Cell(17, 8).Value = "Số đề nghị tạm ứng, thanh toán KLHT kỳ này (Gồm cả tạm ứng)";
        ws.Range(17, 8, 17, 9).Merge();
        ws.Row(17).Style.Font.Bold = true;
        ws.Row(17).Style.Alignment.WrapText = true;
        ws.Range(17, 1, 17, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Cell(18, 1).Value = form.PackageName;
        ws.Range(18, 1, 18, 3).Merge();
        SetMoneyMerged(ws, 18, 4, 5, form.ContractTotal);
        SetMoneyMerged(ws, 18, 6, 7, form.CumulativeBefore);
        SetMoneyMerged(ws, 18, 8, 9, form.RequestedThisPeriod);

        ws.Cell(19, 1).Value = "CỘNG";
        ws.Range(19, 1, 19, 3).Merge();
        // Same 4:5 / 6:7 / 8:9 merge as row 18 — inside the same border block, the CỘNG
        // row's columns must line up with the package row above it.
        SetMoneyMerged(ws, 19, 4, 5, form.ContractTotal);
        SetMoneyMerged(ws, 19, 6, 7, form.CumulativeBefore);
        SetMoneyMerged(ws, 19, 8, 9, form.RequestedThisPeriod);
        ws.Row(19).Style.Font.Bold = true;

        ws.Range(17, 1, 19, 9).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(17, 1, 19, 9).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        ws.Cell(20, 1).Value = " - Tổng số tiền đề nghị tạm ứng, thanh toán bằng số:";
        SetMoney(ws.Cell(20, 8), form.RequestedThisPeriod);
        ws.Cell(21, 1).Value = " - Bằng chữ:";
        ws.Cell(21, 2).Value = form.RequestedInWords;
        ws.Range(21, 2, 21, 9).Merge();
        ws.Cell(22, 1).Value = "Trong đó:";
        ws.Cell(23, 2).Value = " - Thanh toán để thu hồi tạm ứng:";
        SetMoney(ws.Cell(23, 8), form.RecoveredThisPeriod);
        ws.Cell(24, 2).Value = " - Chuyển tiền bảo hành:";
        SetMoney(ws.Cell(24, 8), form.ChuyenTienBaoHanh);
        ws.Cell(25, 2).Value = " - Chuyển tiền tạm giữ chờ quyết toán:";
        SetMoney(ws.Cell(25, 8), form.ChuyenTienTamGiu);
        ws.Cell(26, 2).Value = " - Số trả nhà thầu:";
        SetMoney(ws.Cell(26, 8), form.PaidToContractor);
        ws.Row(26).Style.Font.Bold = true;

        ws.Cell(27, 1).Value = " - Tên đơn vị thụ hưởng:";
        ws.Cell(27, 3).Value = form.ContractorName ?? "";
        ws.Cell(28, 1).Value = " - Tài khoản:";
        ws.Cell(28, 4).Value = form.BankAccount ?? "";
        ws.Cell(29, 1).Value = " - Tại:";
        ws.Cell(29, 2).Value = form.TaiNganHang ?? "";
        ws.Range(29, 2, 29, 9).Merge();

        ws.Cell(31, 1).Value = "Trưởng phòng Tài chính";
        ws.Cell(31, 5).Value = "Chủ đầu tư";
        ws.Range(31, 1, 31, 3).Merge();
        ws.Range(31, 5, 31, 9).Merge();
        ws.Range(31, 1, 31, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Column(1).Width = 26;
        ws.Columns(2, 9).Width = 16;
    }
}
