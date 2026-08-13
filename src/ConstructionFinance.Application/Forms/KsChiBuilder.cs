using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Forms;

/// Dựng phần ghi của cơ quan kiểm soát, thanh toán từ các đợt đã tích chọn.
/// Khác hai form kia, KS chi lấy CẢ hai nguồn và tách ba cột
/// Tổng số / Vốn trong nước (NSNN) / Vốn nước ngoài (Ngoại hối).
public static class KsChiBuilder
{
    public static KsChiForm Build(
        IReadOnlyList<FormDotRow> all,
        IReadOnlySet<FormDotKey> selected,
        KsChiDeclaration? declaration)
    {
        var rows = all.Where(r => selected.Contains(r.Key)).ToList();

        var accepted = Split(rows, r => r.Advance + r.PaymentKLHT);
        var recovered = Split(rows, r => r.RecoveredAdvance);
        var paid = new KsChiAmounts(
            accepted.Total - recovered.Total,
            accepted.NSNN - recovered.NSNN,
            accepted.NgoaiHoi - recovered.NgoaiHoi);

        return new KsChiForm(
            NgayNhanGiayDeNghi: declaration?.NgayNhanGiayDeNghi,
            MucTieuMucLines: new[]
            {
                declaration?.MucTieuMuc1 ?? "",
                declaration?.MucTieuMuc2 ?? "",
                declaration?.MucTieuMuc3 ?? "",
                declaration?.MucTieuMuc4 ?? ""
            },
            Accepted: accepted,
            Recovered: recovered,
            ThuHoiCacNamTruoc: declaration?.ThuHoiCacNamTruoc ?? 0m,
            ThuHoiNamNay: declaration?.ThuHoiNamNay ?? 0m,
            ThueGTGT: declaration?.ThueGTGT ?? 0m,
            PaidToBeneficiary: paid,
            PaidInWords: VietnameseNumberToWords.ToWords(paid.Total),
            SoTuChoi: declaration?.SoTuChoi ?? 0m,
            LyDoTuChoi: declaration?.LyDoTuChoi,
            GhiChu: ResolveGhiChu(rows, declaration));
    }

    private static KsChiAmounts Split(IReadOnlyList<FormDotRow> rows, Func<FormDotRow, decimal> pick) =>
        new(rows.Sum(pick),
            rows.Where(r => r.Fund == Fund.NSNN).Sum(pick),
            rows.Where(r => r.Fund == Fund.NgoaiHoi).Sum(pick));

    /// Ô Ghi chú để trống thì in nội dung của đợt được chọn đầu tiên theo thứ tự sổ
    /// (Mẫu trỏ ô này sang dòng nội dung của Thông tri).
    private static string ResolveGhiChu(IReadOnlyList<FormDotRow> rows, KsChiDeclaration? declaration)
    {
        if (!string.IsNullOrWhiteSpace(declaration?.GhiChu)) return declaration!.GhiChu!;
        return rows.FirstOrDefault()?.Content ?? "";
    }
}
