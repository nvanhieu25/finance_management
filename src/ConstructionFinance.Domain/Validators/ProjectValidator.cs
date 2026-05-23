using ConstructionFinance.Domain.Entities;
using FluentValidation;

namespace ConstructionFinance.Domain.Validators;

public class ProjectValidator : AbstractValidator<Project>
{
    public ProjectValidator()
    {
        RuleFor(x => x.ContractId).GreaterThan(0);
        RuleFor(x => x.ProjectCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ProjectName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.TotalBudget).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TotalActual).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate!.Value)
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue)
            .WithMessage("Ngày kết thúc phải sau ngày bắt đầu");
    }
}
