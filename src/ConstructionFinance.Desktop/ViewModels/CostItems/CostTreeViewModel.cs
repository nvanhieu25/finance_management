using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddChildCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddSiblingCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(IndentCommand))]
    [NotifyCanExecuteChangedFor(nameof(OutdentCommand))]
    private CostTreeNode? _selectedNode;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private decimal _grandTotal;

    public void SetProject(int projectId, ProjectStatus status)
    {
        ProjectId = projectId;
        ProjectStatus = status;
        OnPropertyChanged(nameof(IsReadOnly));
        AddRootCommand.NotifyCanExecuteChanged();
        AddChildCommand.NotifyCanExecuteChanged();
        AddSiblingCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        IndentCommand.NotifyCanExecuteChanged();
        OutdentCommand.NotifyCanExecuteChanged();
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

    [RelayCommand(CanExecute = nameof(CanWrite))]
    private async Task AddRootAsync()
    {
        await CreateNodeAsync(parentId: null, isLeaf: false, defaultName: "Đầu mục mới");
    }

    [RelayCommand(CanExecute = nameof(CanWriteAndHasSelection))]
    private async Task AddChildAsync()
    {
        if (SelectedNode is null) return;
        await CreateNodeAsync(parentId: SelectedNode.Id, isLeaf: true, defaultName: "Mục con");
    }

    [RelayCommand(CanExecute = nameof(CanWriteAndHasSelection))]
    private async Task AddSiblingAsync()
    {
        if (SelectedNode is null) return;
        await CreateNodeAsync(parentId: SelectedNode.ParentId, isLeaf: SelectedNode.IsLeaf, defaultName: "Mục mới");
    }

    [RelayCommand(CanExecute = nameof(CanWriteAndHasSelection))]
    private async Task DeleteAsync()
    {
        if (SelectedNode is null) return;
        var subtreeCount = CountSubtree(SelectedNode);
        var msg = subtreeCount > 1
            ? $"Xóa \"{SelectedNode.ItemName}\" và toàn bộ {subtreeCount - 1} đầu mục con? Hành động này có thể khôi phục trong 30 ngày."
            : $"Xóa đầu mục \"{SelectedNode.ItemName}\"?";
        var ok = await _dialog.ConfirmAsync("Xóa đầu mục chi phí", msg);
        if (!ok) return;

        try
        {
            await _cost.DeleteNodeAsync(SelectedNode.Id);
            await _cost.RecalculateRollupAsync(ProjectId);
            await LoadAsync();
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    private async Task CreateNodeAsync(int? parentId, bool isLeaf, string defaultName)
    {
        try
        {
            await _cost.CreateNodeAsync(new CreateCostItemDto(
                ProjectId, parentId, UnitTypeId: null, ItemName: defaultName, IsLeaf: isLeaf,
                VendorId: null, Quantity: 0, UnitPrice: 0, EstimatedAmount: 0, BidPackage: null, Notes: null));
            await _cost.RecalculateRollupAsync(ProjectId);
            await LoadAsync();
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    private static int CountSubtree(CostTreeNode node)
    {
        var count = 1;
        foreach (var c in node.Children) count += CountSubtree(c);
        return count;
    }

    [RelayCommand(CanExecute = nameof(CanIndent))]
    private async Task IndentAsync()
    {
        if (SelectedNode is null) return;
        var prevSibling = FindPreviousSibling(SelectedNode);
        if (prevSibling is null) return;

        try
        {
            await _cost.MoveNodeAsync(SelectedNode.Id, prevSibling.Id);
            await _cost.RecalculateRollupAsync(ProjectId);
            await LoadAsync();
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(CanOutdent))]
    private async Task OutdentAsync()
    {
        if (SelectedNode is null) return;
        var parent = FindParent(SelectedNode);
        if (parent is null) return; // already at root
        var grandparentId = parent.ParentId; // null → move to root

        try
        {
            await _cost.MoveNodeAsync(SelectedNode.Id, grandparentId);
            await _cost.RecalculateRollupAsync(ProjectId);
            await LoadAsync();
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    /// Persists cell-level edits on <paramref name="node"/>. Called by the View on
    /// commit boundaries (e.g. <c>TextBox.LostFocus</c>) rather than every keystroke,
    /// which keeps the round-trip cheap without needing an explicit debounce timer in the VM.
    public async Task ApplyEditAsync(CostTreeNode node)
    {
        if (IsReadOnly) return;
        try
        {
            await _cost.UpdateNodeAsync(new UpdateCostItemDto(
                Id: node.Id,
                UnitTypeId: node.UnitTypeId,
                VendorId: node.VendorId,
                ItemName: node.ItemName,
                IsLeaf: node.IsLeaf,
                BidPackage: node.BidPackage,
                Quantity: node.Quantity,
                UnitPrice: node.UnitPrice,
                EstimatedAmount: node.EstimatedAmount,
                SortOrder: 0,
                Notes: node.Notes));
            await _cost.RecalculateRollupAsync(ProjectId);
            // Reload so parent EstimatedAmount values reflect the rollup.
            await LoadAsync();
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    private bool CanWrite() => !IsReadOnly;
    private bool CanWriteAndHasSelection() => !IsReadOnly && SelectedNode is not null;

    private bool CanIndent() =>
        !IsReadOnly && SelectedNode is not null && FindPreviousSibling(SelectedNode) is not null;

    private bool CanOutdent() =>
        !IsReadOnly && SelectedNode is not null && FindParent(SelectedNode) is not null;

    private CostTreeNode? FindPreviousSibling(CostTreeNode node)
    {
        var siblings = node.ParentId is int pid
            ? FindNodeById(pid)?.Children
            : RootNodes;
        if (siblings is null) return null;
        var idx = siblings.IndexOf(node);
        return idx > 0 ? siblings[idx - 1] : null;
    }

    private CostTreeNode? FindParent(CostTreeNode node) =>
        node.ParentId is int pid ? FindNodeById(pid) : null;

    private CostTreeNode? FindNodeById(int id)
    {
        foreach (var root in RootNodes)
        {
            var found = FindRecursive(root, id);
            if (found is not null) return found;
        }
        return null;
    }

    private static CostTreeNode? FindRecursive(CostTreeNode node, int id)
    {
        if (node.Id == id) return node;
        foreach (var c in node.Children)
        {
            var found = FindRecursive(c, id);
            if (found is not null) return found;
        }
        return null;
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
