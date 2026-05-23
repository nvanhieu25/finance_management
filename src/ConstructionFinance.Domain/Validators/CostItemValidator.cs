using ConstructionFinance.Domain.Entities;
using FluentValidation;

namespace ConstructionFinance.Domain.Validators;

public class CostItemValidator : AbstractValidator<CostItem>
{
    public CostItemValidator()
    {
        RuleFor(x => x.ProjectId).GreaterThan(0);
        RuleFor(x => x.ItemName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Path).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Level).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EstimatedAmount).GreaterThanOrEqualTo(0);
    }
}
