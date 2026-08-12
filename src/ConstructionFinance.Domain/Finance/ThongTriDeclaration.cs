using ConstructionFinance.Domain.Common;
namespace ConstructionFinance.Domain.Finance;

/// Các ô Mẫu số CQ06-HD để trống cho người dùng điền tay. Một bản cho mỗi dự án.
public class ThongTriDeclaration : BaseEntity
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string? SoThongTri { get; set; }
    public string? NgayGhiSo { get; set; }
    public string? TenCTMT { get; set; }
    public string? MaSo { get; set; }
    public string? VeThangNam { get; set; }
    public string? TaiKhoan1 { get; set; }
    public string? DonVi1 { get; set; }
    public string? SoChungTu1 { get; set; }
    public string? TaiKhoan2 { get; set; }
    public string? DonVi2 { get; set; }
    public string? SoChungTu2 { get; set; }

    // Khối Mục lục ngân sách (cột A–F của Mẫu).
    public string? LoaiNganSach { get; set; }
    public string? Khoan { get; set; }
    public string? Muc { get; set; }
    public string? TieuMuc { get; set; }
    public string? TietMuc { get; set; }
    public string? Nganh { get; set; }
}
