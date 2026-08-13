using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Forms;

/// Dựng Giấy đề nghị thanh toán vốn đầu tư từ các đợt Ngoại hối đã tích chọn.
/// Một giấy = một gói thầu: đơn vị thụ hưởng, tài khoản, giá trị hợp đồng và lũy kế
/// đều là của gói đó (Mẫu lấy lũy kế theo cả Vị trí — xem spec §4.3, chỗ lệch có chủ ý).
public static class GdnttBuilder
{
    private const string NguonVonNgoaiHoi = "Quỹ dự trữ ngoại hối";

    public static GdnttBuildResult Build(
        IReadOnlyList<FormDotRow> all,
        IReadOnlySet<FormDotKey> selected,
        GdnttDeclaration? declaration,
        FormOrgInfo org)
    {
        var rows = all.Where(r => selected.Contains(r.Key) && r.Fund == Fund.NgoaiHoi).ToList();
        if (rows.Count == 0)
            return new GdnttBuildResult(null,
                $"Chưa chọn đợt nào thuộc {NguonVonNgoaiHoi}. Tích ít nhất một đợt để dựng giấy đề nghị.");

        var packages = rows.Select(PackageOf).Distinct().ToList();
        if (packages.Count > 1)
            return new GdnttBuildResult(null,
                "Các đợt đã chọn thuộc nhiều gói thầu. Một giấy đề nghị chỉ dựng cho một gói thầu — " +
                "hãy bỏ chọn các đợt của gói khác.");

        var package = packages[0];
        // FormRowFlattener denormalizes a package's header fields (PackageName, ContractorName,
        // BankAccount, ContractValue, ContractAnnexValue, ...) identically onto every FormDotRow
        // of that package, and the guard above has already proven `rows` spans a single package —
        // so reading them off the first row is safe and equivalent to reading any other row.
        var first = rows[0];

        // "Kỳ trước" = các đợt của CHÍNH gói này đứng trước đợt được chọn sớm nhất
        // theo thứ tự sổ. Không lọc theo nguồn: số dư tạm ứng và lũy kế là trạng thái
        // của gói thầu, cả hai nguồn đều đổ vào đó.
        var firstSelectedIndex = rows.Min(r => r.Key.DotIndex);
        var before = all
            .Where(r => PackageOf(r) == package && r.Key.DotIndex < firstSelectedIndex)
            .ToList();

        var requested = rows.Sum(r => r.Advance + r.PaymentKLHT);
        var recovered = rows.Sum(r => r.RecoveredAdvance);
        var baoHanh = declaration?.ChuyenTienBaoHanh ?? 0m;
        var tamGiu = declaration?.ChuyenTienTamGiu ?? 0m;

        var form = new GdnttForm(
            DonViCapTren: org.DonViCapTren,
            DonViCoSo: org.DonViCoSo,
            So: declaration?.So,
            NgayThangNam: declaration?.NgayThangNam,
            KinhGui: declaration?.KinhGui,
            TenDuAnCongTrinh: declaration?.TenDuAnCongTrinh,
            ChuDauTu: declaration?.ChuDauTu,
            CanCuQuyetDinh: declaration?.CanCuQuyetDinh,
            CanCuHopDong: declaration?.CanCuHopDong,
            CanCuGiayDeNghi: declaration?.CanCuGiayDeNghi,
            ThuocKeHoach: declaration?.ThuocKeHoach,
            PackageName: first.PackageName,
            ContractorName: first.ContractorName,
            BankAccount: first.BankAccount,
            TaiNganHang: declaration?.TaiNganHang,
            UnrecoveredAdvanceBefore: before.Sum(r => r.Advance) - before.Sum(r => r.RecoveredAdvance),
            // Phụ lục HĐ làm thay đổi giá trị hợp đồng nên cộng vào (spec §4.3).
            ContractTotal: first.ContractValue + first.ContractAnnexValue,
            CumulativeBefore: before.Sum(r => r.Advance + r.PaymentKLHT),
            RequestedThisPeriod: requested,
            RequestedInWords: VietnameseNumberToWords.ToWords(requested),
            RecoveredThisPeriod: recovered,
            ChuyenTienBaoHanh: baoHanh,
            ChuyenTienTamGiu: tamGiu,
            PaidToContractor: requested - recovered - baoHanh - tamGiu,
            NguonVon: NguonVonNgoaiHoi);

        return new GdnttBuildResult(form, null);
    }

    private static (int ViTri, int Group, int Package) PackageOf(FormDotRow row) =>
        (row.Key.ViTriIndex, row.Key.GroupIndex, row.Key.PackageIndex);
}
