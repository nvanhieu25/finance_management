using ConstructionFinance.Domain.Entities;
using FluentValidation;

namespace ConstructionFinance.Domain.Validators;

public class ContractValidator : AbstractValidator<Contract>
{
    public ContractValidator()
    {
        RuleFor(x => x.ApprovalDecisionId).GreaterThan(0);
        RuleFor(x => x.ContractCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ContractName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ContractDate).NotEqual(default(DateOnly));
        RuleFor(x => x.ContractValue).GreaterThanOrEqualTo(0);
    }
}
