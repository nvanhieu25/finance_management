using ConstructionFinance.Domain.Common;
using ConstructionFinance.Domain.Entities;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Domain.Tests.Entities;

public class ContractTests
{
    [Fact]
    public void Contract_is_soft_deletable()
    {
        new Contract
        {
            ContractCode = "HD-001",
            ContractName = "HD nha thau X",
            ContractDate = new DateOnly(2026, 5, 1),
            ContractValue = 1_000_000_000m,
            ApprovalDecisionId = 1
        }.ShouldBeAssignableTo<SoftDeletableEntity>();
    }

    [Fact]
    public void Contract_has_required_FK_to_decision_and_optional_to_vendor()
    {
        var contract = new Contract
        {
            ContractCode = "HD-001",
            ContractName = "HD",
            ContractDate = new DateOnly(2026, 5, 1),
            ContractValue = 100m,
            ApprovalDecisionId = 5,
            ContractorVendorId = 10
        };

        contract.ApprovalDecisionId.ShouldBe(5);
        contract.ContractorVendorId.ShouldBe(10);
        contract.Projects.ShouldNotBeNull();
        contract.Projects.ShouldBeEmpty();
    }

    [Fact]
    public void Contract_ContractorVendorId_can_be_null()
    {
        var contract = new Contract
        {
            ContractCode = "HD",
            ContractName = "HD",
            ContractDate = new DateOnly(2026, 5, 1),
            ContractValue = 100m,
            ApprovalDecisionId = 1
        };

        contract.ContractorVendorId.ShouldBeNull();
    }
}
