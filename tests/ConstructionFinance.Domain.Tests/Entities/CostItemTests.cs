using ConstructionFinance.Domain.Common;
using ConstructionFinance.Domain.Entities;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Domain.Tests.Entities;

public class CostItemTests
{
    private static CostItem CreateMinimal() => new()
    {
        ProjectId = 1,
        ItemName = "Móng",
        Path = "/1/",
        Level = 0,
        IsLeaf = true
    };

    [Fact]
    public void CostItem_is_soft_deletable_with_required_props()
    {
        var item = CreateMinimal();
        item.ShouldBeAssignableTo<SoftDeletableEntity>();
        item.ItemName.ShouldBe("Móng");
        item.Path.ShouldBe("/1/");
        item.IsLeaf.ShouldBeTrue();
    }

    [Fact]
    public void CostItem_supports_self_referencing_tree()
    {
        var root = CreateMinimal();
        var child = new CostItem
        {
            ProjectId = 1,
            ItemName = "Phần thô",
            Path = "/1/2/",
            Level = 1,
            IsLeaf = false,
            ParentId = 1,
            Parent = root
        };
        root.Children.Add(child);

        child.Parent.ShouldBe(root);
        child.ParentId.ShouldBe(1);
        root.Children.ShouldContain(child);
    }

    [Fact]
    public void CostItem_has_optional_unit_and_vendor()
    {
        var item = CreateMinimal();
        item.UnitTypeId.ShouldBeNull();
        item.VendorId.ShouldBeNull();
        item.UnitType.ShouldBeNull();
        item.Vendor.ShouldBeNull();
    }

    [Fact]
    public void CostItem_has_Payments_collection_empty()
    {
        CreateMinimal().Payments.ShouldBeEmpty();
    }

    [Fact]
    public void CostItem_money_fields_default_to_zero()
    {
        var item = CreateMinimal();
        item.Quantity.ShouldBe(0m);
        item.UnitPrice.ShouldBe(0m);
        item.EstimatedAmount.ShouldBe(0m);
    }
}
