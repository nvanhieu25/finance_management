using ConstructionFinance.Domain.Entities;
using FluentValidation;

namespace ConstructionFinance.Domain.Validators;

public class PaymentValidator : AbstractValidator<Payment>
{
    public PaymentValidator()
    {
        RuleFor(x => x.CostItemId).GreaterThan(0);
        RuleFor(x => x.PaymentNo).GreaterThan(0);
        RuleFor(x => x.PaymentDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Percentage).InclusiveBetween(0, 100);
    }
}
