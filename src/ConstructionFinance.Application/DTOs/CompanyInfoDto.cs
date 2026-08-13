namespace ConstructionFinance.Application.DTOs;

public record CompanyInfoDto(
    string? CompanyName,
    string? CompanyAddress,
    string? CompanyPhone,
    string? CompanyEmail,
    string? LogoPath,
    string? DefaultSignerName,
    string? DefaultSignerTitle,
    string? DefaultRecipients,
    // Hai tên in trên đầu Thông tri và GĐNTT. Tham số tùy chọn ở cuối để mọi
    // lời gọi 8 tham số đang có vẫn biên dịch nguyên vẹn.
    string? DonViCapTren = null,
    string? DonViCoSo = null);
