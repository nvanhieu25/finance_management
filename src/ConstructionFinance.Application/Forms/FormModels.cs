using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Forms;

/// Vị trí của một đợt trong cây, dùng làm khóa chọn. Chỉ số theo thứ tự sổ.
public readonly record struct FormDotKey(int ViTriIndex, int GroupIndex, int PackageIndex, int DotIndex);

/// Một đợt đã trải phẳng, mang đủ ngữ cảnh để builder không phải đi lại cây.
public record FormDotRow(
    FormDotKey Key,
    string ViTriName,
    string CostGroupName,
    string PackageName,
    string? ContractorName,
    string? BankAccount,
    decimal ContractValue,
    decimal ContractAnnexValue,
    DateOnly Date,
    string? Content,
    string? DocumentNo,
    Fund Fund,
    decimal Advance,
    decimal PaymentKLHT,
    decimal RecoveredAdvance,
    decimal Disbursed);

/// Tên đơn vị in trên đầu Thông tri và GĐNTT. Lưu ở Cài đặt (dùng chung mọi dự án),
/// không lưu theo dự án.
public record FormOrgInfo(string? DonViCapTren, string? DonViCoSo);

/// Một dòng trong bảng Thông tri. Level 0 = dự án, 1 = vị trí, 2 = đợt.
/// Key: định danh dòng (theo chỉ số trong sổ) để chữ gõ riêng cho dòng đó (Mục lục ngân sách) lưu đúng chỗ; dòng dự án để trống.
public record ThongTriLine(int Level, string Content, decimal Amount, string Key = "");

public record ThongTriForm(
    string? DonViCapTren,
    string? DonViCoSo,
    string? SoThongTri,
    string? NgayGhiSo,
    string? TenCTMT,
    string? MaSo,
    string? VeThangNam,
    string? TaiKhoan1,
    string? DonVi1,
    string? SoChungTu1,
    string? TaiKhoan2,
    string? DonVi2,
    string? SoChungTu2,
    string? LoaiNganSach,
    string? Khoan,
    string? Muc,
    string? TieuMuc,
    string? TietMuc,
    string? Nganh,
    IReadOnlyList<ThongTriLine> Lines,
    decimal Total,
    string TotalInWords);

/// Một dòng tiền của KS chi, tách theo nguồn.
public record KsChiAmounts(decimal Total, decimal NSNN, decimal NgoaiHoi);

public record KsChiForm(
    string? NgayNhanGiayDeNghi,
    IReadOnlyList<string> MucTieuMucLines,
    KsChiAmounts Accepted,
    KsChiAmounts Recovered,
    decimal ThuHoiCacNamTruoc,
    decimal ThuHoiNamNay,
    decimal ThueGTGT,
    KsChiAmounts PaidToBeneficiary,
    string PaidInWords,
    decimal SoTuChoi,
    string? LyDoTuChoi,
    string GhiChu);

public record GdnttForm(
    string? DonViCapTren,
    string? DonViCoSo,
    string? So,
    string? NgayThangNam,
    string? KinhGui,
    string? TenDuAnCongTrinh,
    string? ChuDauTu,
    string? CanCuQuyetDinh,
    string? CanCuHopDong,
    string? CanCuGiayDeNghi,
    string? ThuocKeHoach,
    string PackageName,
    string? ContractorName,
    string? BankAccount,
    string? TaiNganHang,
    decimal UnrecoveredAdvanceBefore,
    decimal ContractTotal,
    decimal CumulativeBefore,
    decimal RequestedThisPeriod,
    string RequestedInWords,
    decimal RecoveredThisPeriod,
    decimal ChuyenTienBaoHanh,
    decimal ChuyenTienTamGiu,
    decimal PaidToContractor,
    string NguonVon);

/// GĐNTT chỉ dựng được cho MỘT gói thầu, nên kết quả có thể là một lời từ chối
/// kèm lý do hiển thị cho người dùng thay vì ném ngoại lệ.
public record GdnttBuildResult(GdnttForm? Form, string? Error);
