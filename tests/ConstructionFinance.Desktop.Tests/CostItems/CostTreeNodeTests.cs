using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.ViewModels.CostItems;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.CostItems;

public class CostTreeNodeTests
{
    private static CostItemDto Dto(int id, int? parentId, string name, bool isLeaf, decimal qty = 0, decimal unitPrice = 0, decimal estimated = 0) =>
        new(id, 1, parentId, null, null, null, name, parentId is null ? 0 : 1, "/", 0, isLeaf, qty, unitPrice, estimated, null, null);

    [Fact]
    public void Constructor_copies_dto_fields()
    {
        var dto = Dto(5, null, "Chi phí xây dựng", false, estimated: 1_000_000m);
        var node = new CostTreeNode(dto);

        node.Id.ShouldBe(5);
        node.ItemName.ShouldBe("Chi phí xây dựng");
        node.IsLeaf.ShouldBeFalse();
        node.EstimatedAmount.ShouldBe(1_000_000m);
        node.Children.ShouldNotBeNull();
        node.Children.Count.ShouldBe(0);
    }

    [Fact]
    public void ItemName_setter_raises_PropertyChanged()
    {
        var node = new CostTreeNode(Dto(1, null, "old", true));
        string? changed = null;
        node.PropertyChanged += (_, e) => changed = e.PropertyName;

        node.ItemName = "new";

        changed.ShouldBe(nameof(CostTreeNode.ItemName));
    }

    [Fact]
    public void Quantity_and_UnitPrice_setters_raise_PropertyChanged()
    {
        var node = new CostTreeNode(Dto(1, null, "X", true));
        var changes = new List<string>();
        node.PropertyChanged += (_, e) => changes.Add(e.PropertyName ?? "");

        node.Quantity = 10m;
        node.UnitPrice = 1000m;

        changes.ShouldContain(nameof(CostTreeNode.Quantity));
        changes.ShouldContain(nameof(CostTreeNode.UnitPrice));
    }
}
