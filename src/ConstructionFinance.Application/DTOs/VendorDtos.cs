namespace ConstructionFinance.Application.DTOs;

public record VendorDto(
    int Id,
    string? VendorCode,
    string VendorName,
    string? TaxCode,
    string? Address,
    string? Phone,
    string? Email,
    string? ContactPerson,
    string? Notes);

public record CreateVendorDto(
    string? VendorCode,
    string VendorName,
    string? TaxCode,
    string? Address,
    string? Phone,
    string? Email,
    string? ContactPerson,
    string? Notes);

public record UpdateVendorDto(
    int Id,
    string? VendorCode,
    string VendorName,
    string? TaxCode,
    string? Address,
    string? Phone,
    string? Email,
    string? ContactPerson,
    string? Notes);
