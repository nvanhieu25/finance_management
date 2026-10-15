namespace ConstructionFinance.Domain.Finance;

/// Gói tham số đưa vào builder — KHÔNG phải thực thể lưu: chữ người dùng tự gõ lưu ở FormInput (finance_form_inputs).
/// Giữ lại vì chữ ký của builder không đổi (cách tính số giữ nguyên).
public class KsChiDeclaration
{
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
