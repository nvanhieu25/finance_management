using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Application.Mapping;

public static class ApprovalDecisionMappings
{
    public static DecisionDto ToDto(this ApprovalDecision e) =>
        new(e.Id, e.DecisionCode, e.DecisionTitle, e.DecisionDate, e.IssuingAuthority, e.TotalApprovedAmount, e.Notes);

    public static ApprovalDecision ToEntity(this CreateDecisionDto d) =>
        new()
        {
            DecisionCode = d.DecisionCode,
            DecisionTitle = d.DecisionTitle,
            DecisionDate = d.DecisionDate,
            IssuingAuthority = d.IssuingAuthority,
            TotalApprovedAmount = d.TotalApprovedAmount,
            Notes = d.Notes
        };

    public static void Apply(this UpdateDecisionDto d, ApprovalDecision e)
    {
        e.DecisionCode = d.DecisionCode;
        e.DecisionTitle = d.DecisionTitle;
        e.DecisionDate = d.DecisionDate;
        e.IssuingAuthority = d.IssuingAuthority;
        e.TotalApprovedAmount = d.TotalApprovedAmount;
        e.Notes = d.Notes;
    }
}
