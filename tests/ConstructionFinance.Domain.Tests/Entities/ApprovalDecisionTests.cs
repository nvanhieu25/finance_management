using ConstructionFinance.Domain.Common;
using ConstructionFinance.Domain.Entities;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Domain.Tests.Entities;

public class ApprovalDecisionTests
{
    [Fact]
    public void ApprovalDecision_is_soft_deletable()
    {
        new ApprovalDecision
        {
            DecisionCode = "1234/QD",
            DecisionTitle = "Phê duyệt dự án X",
            IssuingAuthority = "UBND",
            DecisionDate = new DateOnly(2026, 5, 1),
            TotalApprovedAmount = 50_000_000_000m
        }.ShouldBeAssignableTo<SoftDeletableEntity>();
    }

    [Fact]
    public void ApprovalDecision_has_navigation_to_contracts_empty_by_default()
    {
        var decision = new ApprovalDecision
        {
            DecisionCode = "1234/QD",
            DecisionTitle = "X",
            IssuingAuthority = "UBND",
            DecisionDate = new DateOnly(2026, 5, 1),
            TotalApprovedAmount = 100m
        };

        decision.Contracts.ShouldNotBeNull();
        decision.Contracts.ShouldBeEmpty();
    }
}
