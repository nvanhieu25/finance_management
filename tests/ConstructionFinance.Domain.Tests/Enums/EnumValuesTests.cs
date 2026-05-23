using ConstructionFinance.Domain.Enums;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Domain.Tests.Enums;

public class EnumValuesTests
{
    [Fact]
    public void ProjectStatus_should_have_all_required_values()
    {
        Enum.GetNames<ProjectStatus>().ShouldBe(
            new[] { "Planning", "InProgress", "OnHold", "Completed", "Cancelled" },
            ignoreOrder: true);
    }

    [Fact]
    public void PaymentType_should_have_all_required_values()
    {
        Enum.GetNames<PaymentType>().ShouldBe(
            new[] { "Advance", "Installment", "FinalSettlement", "RetentionRelease" },
            ignoreOrder: true);
    }

    [Fact]
    public void AuditAction_should_have_create_update_delete()
    {
        Enum.GetNames<AuditAction>().ShouldBe(
            new[] { "Create", "Update", "Delete" },
            ignoreOrder: true);
    }

    [Fact]
    public void AttachmentEntityType_should_match_design_doc()
    {
        Enum.GetNames<AttachmentEntityType>().ShouldBe(
            new[] { "ApprovalDecision", "Contract", "Project", "CostItem", "Payment" },
            ignoreOrder: true);
    }
}
