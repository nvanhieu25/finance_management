using ConstructionFinance.Domain.Common;
namespace ConstructionFinance.Domain.Finance;

/// Các ô người dùng điền tay trên phần ghi của cơ quan kiểm soát, thanh toán.
public class KsChiDeclaration : BaseEntity
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string? NgayNhanGiayDeNghi { get; set; }
    public string? MucTieuMuc1 { get; set; }
    public string? MucTieuMuc2 { get; set; }
    public string? MucTieuMuc3 { get; set; }
    public string? MucTieuMuc4 { get; set; }
    public decimal ThuHoiCacNamTruoc { get; set; }
    public decimal ThuHoiNamNay { get; set; }
    public decimal ThueGTGT { get; set; }
    public decimal SoTuChoi { get; set; }
    public string? LyDoTuChoi { get; set; }

    /// Để trống thì bản in lấy Nội dung của đợt được chọn đầu tiên.
    public string? GhiChu { get; set; }
}
