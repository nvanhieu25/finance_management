using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Forms;

/// Dựng Thông tri (Mẫu số CQ06-HD) từ các đợt người dùng đã tích chọn, của MỘT nguồn kinh phí.
/// Bảng in ba cấp: dự án → vị trí → đợt, số tiền mỗi đợt là giải ngân của đợt.
public static class ThongTriBuilder
{
    private const string NoContent = "(chưa có nội dung)";

    public static ThongTriForm Build(
        IReadOnlyList<FormDotRow> all,
        IReadOnlySet<FormDotKey> selected,
        ThongTriDeclaration? declaration,
        FormOrgInfo org,
        string projectName,
        Fund fund = Fund.NSNN)
    {
        // Một tờ Thông tri nói về một nguồn ("Kinh phí: …"); người dùng chọn nguồn trên tab.
        var rows = all.Where(r => selected.Contains(r.Key) && r.Fund == fund).ToList();
        var total = rows.Sum(r => r.Disbursed);

        var lines = new List<ThongTriLine> { new(0, projectName, total) };
        // GroupBy node index (ViTriIndex) ensures two Vị trí with the same name print separately.
        // ViTri.Name has no uniqueness constraint, so grouping by name would merge distinct nodes.
        // GroupBy preserves ledger order since `all` is already in ledger sequence.
        foreach (var group in rows.GroupBy(r => r.Key.ViTriIndex))
        {
            var viTriName = group.First().ViTriName;
            lines.Add(new ThongTriLine(1, viTriName, group.Sum(r => r.Disbursed), $"v{group.Key}"));
            foreach (var row in group)
                lines.Add(new ThongTriLine(2,
                    string.IsNullOrWhiteSpace(row.Content) ? NoContent : row.Content!,
                    row.Disbursed,
                    $"d{row.Key.ViTriIndex}.{row.Key.GroupIndex}.{row.Key.PackageIndex}.{row.Key.DotIndex}"));
        }

        return new ThongTriForm(
            DonViCapTren: org.DonViCapTren,
            DonViCoSo: org.DonViCoSo,
            SoThongTri: declaration?.SoThongTri,
            NgayGhiSo: declaration?.NgayGhiSo,
            TenCTMT: declaration?.TenCTMT,
            MaSo: declaration?.MaSo,
            VeThangNam: declaration?.VeThangNam,
            TaiKhoan1: declaration?.TaiKhoan1,
            DonVi1: declaration?.DonVi1,
            SoChungTu1: declaration?.SoChungTu1,
            TaiKhoan2: declaration?.TaiKhoan2,
            DonVi2: declaration?.DonVi2,
            SoChungTu2: declaration?.SoChungTu2,
            LoaiNganSach: declaration?.LoaiNganSach,
            Khoan: declaration?.Khoan,
            Muc: declaration?.Muc,
            TieuMuc: declaration?.TieuMuc,
            TietMuc: declaration?.TietMuc,
            Nganh: declaration?.Nganh,
            Lines: lines,
            Total: total,
            TotalInWords: VietnameseNumberToWords.ToWords(total));
    }
}
