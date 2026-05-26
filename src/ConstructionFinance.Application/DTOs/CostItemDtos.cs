namespace ConstructionFinance.Application.DTOs;

public record CostItemDto(
    int Id,
    int ProjectId,
    int? ParentId,
    int? UnitTypeId,
    int? VendorId,
    string? ItemCode,
    string ItemName,
    int Level,
    string Path,
    int SortOrder,
    bool IsLeaf,
    decimal Quantity,
    decimal UnitPrice,
    decimal EstimatedAmount,
    string? BidPackage,
    string? Notes);

public record CreateCostItemDto(
    int ProjectId,
    int? ParentId,
    int? UnitTypeId,
    string ItemName,
    bool IsLeaf,
    int? VendorId,
    decimal Quantity,
    decimal UnitPrice,
    decimal EstimatedAmount,
    string? BidPackage,
    string? Notes);

public record UpdateCostItemDto(
    int Id,
    int? UnitTypeId,
    int? VendorId,
    string ItemName,
    bool IsLeaf,
    string? BidPackage,
    decimal Quantity,
    decimal UnitPrice,
    decimal EstimatedAmount,
    int SortOrder,
    string? Notes);
