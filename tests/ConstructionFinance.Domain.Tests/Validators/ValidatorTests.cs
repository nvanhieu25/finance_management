using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using ConstructionFinance.Domain.Validators;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Domain.Tests.Validators;

public class ValidatorTests
{
    [Fact]
    public void ApprovalDecision_invalid_when_DecisionCode_empty()
    {
        var v = new ApprovalDecisionValidator();
        var entity = new ApprovalDecision
        {
            DecisionCode = "",
            DecisionTitle = "X",
            IssuingAuthority = "Y",
            DecisionDate = new DateOnly(2026, 5, 1),
            TotalApprovedAmount = 1000
        };

        var result = v.Validate(entity);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "DecisionCode");
    }

    [Fact]
    public void ApprovalDecision_invalid_when_TotalApprovedAmount_negative()
    {
        var v = new ApprovalDecisionValidator();
        var entity = new ApprovalDecision
        {
            DecisionCode = "X",
            DecisionTitle = "X",
            IssuingAuthority = "X",
            DecisionDate = new DateOnly(2026, 5, 1),
            TotalApprovedAmount = -1
        };

        v.Validate(entity).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void ApprovalDecision_valid_with_all_required()
    {
        var v = new ApprovalDecisionValidator();
        var entity = new ApprovalDecision
        {
            DecisionCode = "1234/QD",
            DecisionTitle = "Phê duyệt",
            IssuingAuthority = "UBND",
            DecisionDate = new DateOnly(2026, 5, 1),
            TotalApprovedAmount = 1_000_000_000m
        };

        v.Validate(entity).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Project_invalid_when_end_before_start()
    {
        var v = new ProjectValidator();
        var p = new Project
        {
            ContractId = 1,
            ProjectCode = "DA",
            ProjectName = "X",
            Status = ProjectStatus.Planning,
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 5, 1)
        };

        v.Validate(p).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Payment_invalid_when_Amount_zero_or_negative()
    {
        var v = new PaymentValidator();
        v.Validate(new Payment
        {
            CostItemId = 1,
            PaymentNo = 1,
            PaymentType = PaymentType.Advance,
            PaymentDate = new DateOnly(2026, 5, 1),
            Amount = 0
        }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Vendor_invalid_when_name_empty()
    {
        new VendorValidator()
            .Validate(new Vendor { VendorName = "" })
            .IsValid.ShouldBeFalse();
    }
}
