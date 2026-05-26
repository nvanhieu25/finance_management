namespace ConstructionFinance.Application.DTOs;

public record ContractDto(
    int Id,
    int ApprovalDecisionId,
    int? ContractorVendorId,
    string ContractCode,
    string ContractName,
    DateOnly ContractDate,
    DateOnly? SigningDate,
    decimal ContractValue,
    string? Notes);

public record CreateContractDto(
    int ApprovalDecisionId,
    int? ContractorVendorId,
    string ContractCode,
    string ContractName,
    DateOnly ContractDate,
    DateOnly? SigningDate,
    decimal ContractValue,
    string? Notes);

public record UpdateContractDto(
    int Id,
    int ApprovalDecisionId,
    int? ContractorVendorId,
    string ContractCode,
    string ContractName,
    DateOnly ContractDate,
    DateOnly? SigningDate,
    decimal ContractValue,
    string? Notes);
