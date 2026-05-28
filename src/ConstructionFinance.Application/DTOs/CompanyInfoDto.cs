namespace ConstructionFinance.Application.DTOs;

public record CompanyInfoDto(
    string? CompanyName,
    string? CompanyAddress,
    string? CompanyPhone,
    string? CompanyEmail,
    string? LogoPath,
    string? DefaultSignerName,
    string? DefaultSignerTitle,
    string? DefaultRecipients);
