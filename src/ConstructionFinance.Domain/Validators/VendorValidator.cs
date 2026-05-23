using ConstructionFinance.Domain.Entities;
using FluentValidation;

namespace ConstructionFinance.Domain.Validators;

public class VendorValidator : AbstractValidator<Vendor>
{
    public VendorValidator()
    {
        RuleFor(x => x.VendorName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.VendorCode).MaximumLength(50);
        RuleFor(x => x.TaxCode).MaximumLength(50);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Phone).MaximumLength(50);
    }
}
