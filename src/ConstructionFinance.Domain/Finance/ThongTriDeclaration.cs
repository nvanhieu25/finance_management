namespace ConstructionFinance.Domain.Finance;

/// Gói tham số đưa vào builder — KHÔNG phải thực thể lưu: chữ người dùng tự gõ lưu ở FormInput (finance_form_inputs).
/// Giữ lại vì chữ ký của builder không đổi (cách tính số giữ nguyên).
public class ThongTriDeclaration
{
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
