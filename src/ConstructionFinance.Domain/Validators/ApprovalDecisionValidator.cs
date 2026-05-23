using ConstructionFinance.Domain.Entities;
using FluentValidation;

namespace ConstructionFinance.Domain.Validators;

public class ApprovalDecisionValidator : AbstractValidator<ApprovalDecision>
{
    public ApprovalDecisionValidator()
    {
        RuleFor(x => x.DecisionCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.DecisionTitle).NotEmpty().MaximumLength(255);
        RuleFor(x => x.IssuingAuthority).NotEmpty().MaximumLength(255);
        RuleFor(x => x.DecisionDate).NotEqual(default(DateOnly));
        RuleFor(x => x.TotalApprovedAmount).GreaterThanOrEqualTo(0);
    }
}
