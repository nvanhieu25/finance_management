using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Application.Mapping;

public static class CostItemMappings
{
    public static CostItemDto ToDto(this CostItem e) =>
        new(e.Id, e.ProjectId, e.ParentId, e.UnitTypeId, e.VendorId, e.ItemCode, e.ItemName, e.Level, e.Path, e.SortOrder, e.IsLeaf, e.Quantity, e.UnitPrice, e.EstimatedAmount, e.BidPackage, e.Notes);
}
