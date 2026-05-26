using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Application.Mapping;

public static class ContractMappings
{
    public static ContractDto ToDto(this Contract e) =>
        new(e.Id, e.ApprovalDecisionId, e.ContractorVendorId, e.ContractCode, e.ContractName, e.ContractDate, e.SigningDate, e.ContractValue, e.Notes);

    public static Contract ToEntity(this CreateContractDto d) =>
        new()
        {
            ApprovalDecisionId = d.ApprovalDecisionId,
            ContractorVendorId = d.ContractorVendorId,
            ContractCode = d.ContractCode,
            ContractName = d.ContractName,
            ContractDate = d.ContractDate,
            SigningDate = d.SigningDate,
            ContractValue = d.ContractValue,
            Notes = d.Notes
        };

    public static void Apply(this UpdateContractDto d, Contract e)
    {
        e.ApprovalDecisionId = d.ApprovalDecisionId;
        e.ContractorVendorId = d.ContractorVendorId;
        e.ContractCode = d.ContractCode;
        e.ContractName = d.ContractName;
        e.ContractDate = d.ContractDate;
        e.SigningDate = d.SigningDate;
        e.ContractValue = d.ContractValue;
        e.Notes = d.Notes;
    }
}
