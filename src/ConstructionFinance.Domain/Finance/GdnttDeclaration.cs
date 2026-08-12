using ConstructionFinance.Domain.Common;
namespace ConstructionFinance.Domain.Finance;

/// Các ô người dùng điền tay trên Giấy đề nghị thanh toán vốn đầu tư.
public class GdnttDeclaration : BaseEntity
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string? So { get; set; }
    public string? NgayThangNam { get; set; }
    public string? KinhGui { get; set; }
    public string? TenDuAnCongTrinh { get; set; }
    public string? ChuDauTu { get; set; }
    public string? CanCuQuyetDinh { get; set; }
    public string? CanCuHopDong { get; set; }
    public string? CanCuGiayDeNghi { get; set; }
    public string? ThuocKeHoach { get; set; }
    public string? TaiNganHang { get; set; }
    public decimal ChuyenTienBaoHanh { get; set; }
    public decimal ChuyenTienTamGiu { get; set; }
}
