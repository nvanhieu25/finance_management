using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.ViewModels.CostItems;

public partial class CostItemEditDialogViewModel : ViewModelBase, IHasDialogTitle
{
    public string DialogTitle => $"Sửa đầu mục: {_originalName}";

    public ObservableCollection<CostUnitTypeDto> Units { get; }
    public ObservableCollection<VendorDto> Vendors { get; }

    [ObservableProperty] private string _itemName = string.Empty;
    [ObservableProperty] private CostUnitTypeDto? _selectedUnit;
    [ObservableProperty] private VendorDto? _selectedVendor;
    [ObservableProperty] private string? _bidPackage;
    [ObservableProperty] private string? _notes;
    [ObservableProperty] private bool _isLeaf;

    private readonly string _originalName;

    public CostItemEditDialogViewModel(CostTreeNode node, ObservableCollection<CostUnitTypeDto> units, ObservableCollection<VendorDto> vendors)
    {
        _originalName = node.ItemName;
        Units = units;
        Vendors = vendors;

        ItemName = node.ItemName;
        BidPackage = node.BidPackage;
        Notes = node.Notes;
        IsLeaf = node.IsLeaf;
        SelectedUnit = units.FirstOrDefault(u => u.Id == node.UnitTypeId);
        SelectedVendor = vendors.FirstOrDefault(v => v.Id == node.VendorId);
    }

    public void ApplyTo(CostTreeNode node)
    {
        node.ItemName = ItemName;
        node.BidPackage = BidPackage;
        node.Notes = Notes;
        node.IsLeaf = IsLeaf;
        node.UnitTypeId = SelectedUnit?.Id;
        node.VendorId = SelectedVendor?.Id;
    }
}
