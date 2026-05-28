using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Desktop.ViewModels.CostItems;

public partial class CostTreeViewModel : ViewModelBase
{
    private readonly ICostService _cost;
    private readonly IPaymentService _payment;
    private readonly ICostUnitTypeService _units;
    private readonly IDialogService _dialog;

    public CostTreeViewModel(ICostService cost, IPaymentService payment, ICostUnitTypeService units, IDialogService dialog)
    {
        _cost = cost;
        _payment = payment;
        _units = units;
        _dialog = dialog;
    }

    public int ProjectId { get; private set; }
    public ProjectStatus ProjectStatus { get; private set; } = ProjectStatus.Planning;
    public bool IsReadOnly => ProjectStatus == ProjectStatus.Completed;

    public ObservableCollection<CostTreeNode> RootNodes { get; } = new();
    public ObservableCollection<CostUnitTypeDto> Units { get; } = new();

    [ObservableProperty] private CostTreeNode? _selectedNode;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private decimal _grandTotal;

    public void SetProject(int projectId, ProjectStatus status)
    {
        ProjectId = projectId;
        ProjectStatus = status;
        OnPropertyChanged(nameof(IsReadOnly));
    }

    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var flat = await _cost.GetProjectTreeAsync(ProjectId);
            RootNodes.Clear();
            foreach (var root in BuildHierarchy(flat))
                RootNodes.Add(root);

            if (Units.Count == 0)
            {
                var units = await _units.GetAllAsync();
                foreach (var u in units) Units.Add(u);
            }

            GrandTotal = RootNodes.Sum(n => n.EstimatedAmount);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    private static List<CostTreeNode> BuildHierarchy(IEnumerable<CostItemDto> flat)
    {
        var byId = new Dictionary<int, CostTreeNode>();
        var roots = new List<CostTreeNode>();
        var snapshot = flat.ToList();
        foreach (var dto in snapshot)
            byId[dto.Id] = new CostTreeNode(dto);
        foreach (var dto in snapshot)
        {
            var node = byId[dto.Id];
            if (dto.ParentId is int pid && byId.TryGetValue(pid, out var parent))
                parent.Children.Add(node);
            else
                roots.Add(node);
        }
        return roots;
    }
}
