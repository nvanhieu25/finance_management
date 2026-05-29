using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.DTOs;

namespace ConstructionFinance.Desktop.ViewModels.CostItems;

/// Observable wrapper around <see cref="CostItemDto"/> for binding into TreeView/TreeDataGrid.
/// CostItemDto is a record (immutable); we need INotifyPropertyChanged on edits and
/// a mutable <see cref="Children"/> collection that the tree control can observe.
public partial class CostTreeNode : ObservableObject
{
    public int Id { get; }
    public int ProjectId { get; }
    public int? ParentId { get; }
    public int Level { get; }
    public string Path { get; }

    [ObservableProperty] private string? _itemCode;
    [ObservableProperty] private string _itemName;
    [ObservableProperty] private bool _isLeaf;
    [ObservableProperty] private int? _unitTypeId;
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private decimal _unitPrice;
    [ObservableProperty] private decimal _estimatedAmount;
    [ObservableProperty] private string? _bidPackage;
    [ObservableProperty] private string? _notes;
    [ObservableProperty] private int? _vendorId;
    [ObservableProperty] private bool _isExpanded = true;

    public ObservableCollection<CostTreeNode> Children { get; } = new();

    partial void OnQuantityChanged(decimal value) => RecomputeEstimated();
    partial void OnUnitPriceChanged(decimal value) => RecomputeEstimated();

    private void RecomputeEstimated() => EstimatedAmount = Quantity * UnitPrice;

    public CostTreeNode(CostItemDto dto)
    {
        Id = dto.Id;
        ProjectId = dto.ProjectId;
        ParentId = dto.ParentId;
        Level = dto.Level;
        Path = dto.Path;
        _itemCode = dto.ItemCode;
        _itemName = dto.ItemName;
        _isLeaf = dto.IsLeaf;
        _unitTypeId = dto.UnitTypeId;
        _quantity = dto.Quantity;
        _unitPrice = dto.UnitPrice;
        _estimatedAmount = dto.EstimatedAmount;
        _bidPackage = dto.BidPackage;
        _notes = dto.Notes;
        _vendorId = dto.VendorId;
    }
}
