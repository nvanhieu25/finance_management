namespace ConstructionFinance.Domain.Finance;

/// Gói tham số đưa vào builder — KHÔNG phải thực thể lưu: chữ người dùng tự gõ lưu ở FormInput (finance_form_inputs).
/// Giữ lại vì chữ ký của builder không đổi (cách tính số giữ nguyên).
public class GdnttDeclaration
{
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
