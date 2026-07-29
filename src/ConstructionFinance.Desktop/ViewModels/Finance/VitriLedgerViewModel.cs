using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// Headless view-model for ONE Vị trí's ledger: flattens the CostGroup/Package/Dot tree
/// into an outline-grid row list (Total, Group, Package, Entry rows), keeps subtotals live
/// via TreeCalculator as rows are edited, and round-trips edits back to a ViTri for saving.
/// No XAML/rendering dependency — fully unit-testable.
public partial class VitriLedgerViewModel : ViewModelBase
{
    // Package rows never exist outside a Group (user-confirmed rule), so the tree is modeled
    // as Group -> Package -> Entry. GroupNode/PackageNode carry both the flattened row AND its
    // children, so Rows can be rebuilt in outline order and the tree can be reassembled by ToViTri().
    private sealed class PackageNode
    {
        public LedgerRowViewModel Row { get; }
        public List<LedgerRowViewModel> Entries { get; } = new();
        public PackageNode(LedgerRowViewModel row) => Row = row;
    }

    private sealed class GroupNode
    {
        public LedgerRowViewModel Row { get; }
        public List<PackageNode> Packages { get; } = new();
        public GroupNode(LedgerRowViewModel row) => Row = row;
    }

    private readonly LedgerRowViewModel _totalRow = new(RowKind.Total);
    private readonly List<GroupNode> _groups = new();
    private bool _loading;

    public ObservableCollection<LedgerRowViewModel> Rows { get; } = new();

    /// The frozen TỔNG CỘNG row (also Rows[0] — exposed directly so the View can bind
    /// the frozen top bar without an indexer path). The reference never changes.
    public LedgerRowViewModel TotalRow => _totalRow;

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string? _nguonVon;
    [ObservableProperty] private int _sortOrder;

    [ObservableProperty] private LedgerRowViewModel? _selectedRow;

    [ObservableProperty] private bool _isDirty;

    /// Raised whenever IsDirty flips, so the hosting page can bubble it to the workbook.
    public event EventHandler? DirtyChanged;

    public VitriLedgerViewModel() : this(new ViTri { Name = "" })
    {
    }

    public VitriLedgerViewModel(ViTri viTri)
    {
        _loading = true;
        try
        {
            Name = viTri.Name;
            NguonVon = viTri.NguonVon;
            SortOrder = viTri.SortOrder;

            Track(_totalRow);

            foreach (var g in viTri.CostGroups.OrderBy(g => g.SortOrder))
            {
                var groupRow = new LedgerRowViewModel(RowKind.Group) { Name = g.Name, Depth = 0 };
                Track(groupRow);
                var groupNode = new GroupNode(groupRow);

                foreach (var p in g.Packages.OrderBy(p => p.SortOrder))
                {
                    var packageRow = new LedgerRowViewModel(RowKind.Package)
                    {
                        Name = p.Name,
                        ContractorName = p.ContractorName,
                        BankAccount = p.BankAccount,
                        ContractValue = p.ContractValue,
                        ContractAnnexValue = p.ContractAnnexValue,
                        Depth = 1
                    };
                    Track(packageRow);
                    var packageNode = new PackageNode(packageRow);

                    foreach (var d in p.Dots.OrderBy(d => d.SortOrder))
                    {
                        var entryRow = new LedgerRowViewModel(RowKind.Entry)
                        {
                            Date = d.Date,
                            Content = d.Content,
                            DocumentNo = d.DocumentNo,
                            Fund = d.Fund,
                            Advance = d.Advance,
                            PaymentKLHT = d.PaymentKLHT,
                            RecoveredAdvance = d.RecoveredAdvance,
                            Depth = 2
                        };
                        Track(entryRow);
                        packageNode.Entries.Add(entryRow);
                    }

                    groupNode.Packages.Add(packageNode);
                }

                _groups.Add(groupNode);
            }

            RebuildRows();
            RecomputeSubtotals();
        }
        finally { _loading = false; }
    }

    partial void OnNameChanged(string value) => MarkDirty();
    partial void OnNguonVonChanged(string? value) => MarkDirty();
    partial void OnSortOrderChanged(int value) => MarkDirty();

    partial void OnIsDirtyChanged(bool value) => DirtyChanged?.Invoke(this, EventArgs.Empty);

    private void MarkDirty()
    {
        if (_loading) return;
        IsDirty = true;
    }

    private void Track(LedgerRowViewModel row) => row.InputChanged += OnRowInputChanged;
    private void Untrack(LedgerRowViewModel row) => row.InputChanged -= OnRowInputChanged;

    private void OnRowInputChanged(object? sender, EventArgs e)
    {
        RecomputeSubtotals();
        MarkDirty();
    }

    [RelayCommand]
    private void AddGroup()
    {
        var row = new LedgerRowViewModel(RowKind.Group) { Name = "Nhóm chi phí mới", Depth = 0 };
        Track(row);
        _groups.Add(new GroupNode(row));

        RebuildRows();
        RecomputeSubtotals();
        MarkDirty();
    }

    /// Adds a Package under the given Group row. No-op if groupRow doesn't resolve to a
    /// tracked Group (including "there is no group yet") — a Package must live under a Group.
    [RelayCommand]
    private void AddPackage(LedgerRowViewModel? groupRow)
    {
        if (groupRow is null) return;
        var groupNode = _groups.FirstOrDefault(g => ReferenceEquals(g.Row, groupRow));
        if (groupNode is null) return;

        var row = new LedgerRowViewModel(RowKind.Package) { Name = "Gói thầu mới", Depth = 1 };
        Track(row);
        groupNode.Packages.Add(new PackageNode(row));

        RebuildRows();
        RecomputeSubtotals();
        MarkDirty();
    }

    /// Adds an Entry (Dot) under the given Package row. No-op if packageRow doesn't resolve
    /// to a tracked Package (including "there is no package yet").
    [RelayCommand]
    private void AddEntry(LedgerRowViewModel? packageRow)
    {
        if (packageRow is null) return;
        var packageNode = FindPackage(packageRow).package;
        if (packageNode is null) return;

        var row = new LedgerRowViewModel(RowKind.Entry) { Depth = 2 };
        Track(row);
        packageNode.Entries.Add(row);

        RebuildRows();
        RecomputeSubtotals();
        MarkDirty();
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        var selected = SelectedRow;
        if (selected is null) return;

        switch (selected.Kind)
        {
            case RowKind.Total:
                return; // the Total row cannot be deleted

            case RowKind.Group:
            {
                var groupNode = _groups.FirstOrDefault(g => ReferenceEquals(g.Row, selected));
                if (groupNode is null) return;
                foreach (var p in groupNode.Packages)
                {
                    foreach (var e in p.Entries) Untrack(e);
                    Untrack(p.Row);
                }
                Untrack(groupNode.Row);
                _groups.Remove(groupNode);
                break;
            }

            case RowKind.Package:
            {
                var (groupNode, packageNode) = FindPackage(selected);
                if (packageNode is null || groupNode is null) return;
                foreach (var e in packageNode.Entries) Untrack(e);
                Untrack(packageNode.Row);
                groupNode.Packages.Remove(packageNode);
                break;
            }

            case RowKind.Entry:
            {
                var packageNode = _groups.SelectMany(g => g.Packages)
                    .FirstOrDefault(p => p.Entries.Contains(selected));
                if (packageNode is null) return;
                Untrack(selected);
                packageNode.Entries.Remove(selected);
                break;
            }
        }

        SelectedRow = null;
        RebuildRows();
        RecomputeSubtotals();
        MarkDirty();
    }

    private (GroupNode? group, PackageNode? package) FindPackage(LedgerRowViewModel packageRow)
    {
        foreach (var g in _groups)
        {
            var p = g.Packages.FirstOrDefault(x => ReferenceEquals(x.Row, packageRow));
            if (p is not null) return (g, p);
        }
        return (null, null);
    }

    /// Finds the Package node that owns a given Entry row (used by ApplyPaste).
    private PackageNode? FindPackageContainingEntry(LedgerRowViewModel entryRow)
    {
        foreach (var g in _groups)
            foreach (var p in g.Packages)
                if (p.Entries.Contains(entryRow))
                    return p;
        return null;
    }

    /// Paste a TSV block (rows of cells) starting at the given Entry row/column.
    /// Only Entry rows accept paste — no-op if startRow isn't a tracked Entry row.
    /// New Entry rows are appended to the SAME package as needed when the block
    /// extends past the existing entries. Column indices follow the grid's visible
    /// order: 0 Ngày, 1 Nội dung, 5 Số CT, 6 Tạm ứng, 7 KLHT, 8 Thu hồi, 9 Nguồn.
    /// Columns 2–4 (Nhà thầu/TK, Giá trị HĐ, Phụ lục) and 10–13 (computed) are
    /// Package/Total-only or read-only and are silently ignored.
    public void ApplyPaste(LedgerRowViewModel startRow, int startCol, string[][] block)
    {
        if (startRow.Kind != RowKind.Entry) return;
        var packageNode = FindPackageContainingEntry(startRow);
        if (packageNode is null) return;

        var startIndex = packageNode.Entries.IndexOf(startRow);
        if (startIndex < 0) return;

        for (var r = 0; r < block.Length; r++)
        {
            var targetIndex = startIndex + r;
            while (packageNode.Entries.Count <= targetIndex)
            {
                var newRow = new LedgerRowViewModel(RowKind.Entry) { Depth = 2 };
                Track(newRow);
                packageNode.Entries.Add(newRow);
            }

            var row = packageNode.Entries[targetIndex];
            var cells = block[r];
            for (var c = 0; c < cells.Length; c++)
                SetEntryCell(row, startCol + c, cells[c]);
        }

        RebuildRows();
        RecomputeSubtotals();
        MarkDirty();
    }

    private static void SetEntryCell(LedgerRowViewModel row, int col, string value)
    {
        switch (col)
        {
            case 0: row.DateText = value; break;
            case 1: row.Content = value; break;
            case 5: row.DocumentNo = value; break;
            case 6: row.AdvanceText = value; break;
            case 7: row.KlhtText = value; break;
            case 8: row.RecoveredText = value; break;
            case 9: row.FundText = value; break;
        }
    }

    private void RebuildRows()
    {
        Rows.Clear();
        Rows.Add(_totalRow);
        foreach (var g in _groups)
        {
            Rows.Add(g.Row);
            foreach (var p in g.Packages)
            {
                Rows.Add(p.Row);
                foreach (var e in p.Entries)
                    Rows.Add(e);
            }
        }
    }

    /// Rebuilds a transient ViTri from the current rows and runs it through TreeCalculator,
    /// pushing the computed columns onto the Total/Group/Package rows' display strings.
    public void RecomputeSubtotals()
    {
        var viTri = ToViTri();

        ApplyAggregate(_totalRow, TreeCalculator.AggregateViTri(viTri));

        for (var gi = 0; gi < _groups.Count; gi++)
        {
            var groupNode = _groups[gi];
            var groupEntity = viTri.CostGroups[gi];
            ApplyAggregate(groupNode.Row, TreeCalculator.AggregateGroup(groupEntity));

            for (var pi = 0; pi < groupNode.Packages.Count; pi++)
            {
                var packageNode = groupNode.Packages[pi];
                var packageEntity = groupEntity.Packages[pi];
                ApplyAggregate(packageNode.Row, TreeCalculator.AggregatePackageEntity(packageEntity));
            }
        }
    }

    private static void ApplyAggregate(LedgerRowViewModel row, TreeAggregate agg)
    {
        row.DisbursedNsnnText = MoneyText.Format(agg.DisbursedNSNN);
        row.DisbursedNgoaiHoiText = MoneyText.Format(agg.DisbursedNgoaiHoi);
        row.UnrecoveredAdvanceText = MoneyText.Format(agg.UnrecoveredAdvance);
        row.TotalDisbursedText = MoneyText.Format(agg.TotalDisbursed);
    }

    /// Rebuilds a ViTri from the current rows: SortOrder is assigned by position, Package rows
    /// carry their header fields, and Entry rows convert via LedgerRowViewModel.ToDot().
    public ViTri ToViTri()
    {
        var viTri = new ViTri { Name = Name, NguonVon = NguonVon, SortOrder = SortOrder };

        for (var gi = 0; gi < _groups.Count; gi++)
        {
            var groupNode = _groups[gi];
            var group = new CostGroup { Name = groupNode.Row.Name ?? "", SortOrder = gi };

            for (var pi = 0; pi < groupNode.Packages.Count; pi++)
            {
                var packageNode = groupNode.Packages[pi];
                var packageRow = packageNode.Row;
                var package = new Package
                {
                    Name = packageRow.Name ?? "",
                    ContractorName = packageRow.ContractorName,
                    BankAccount = packageRow.BankAccount,
                    ContractValue = packageRow.ContractValue,
                    ContractAnnexValue = packageRow.ContractAnnexValue,
                    SortOrder = pi
                };

                for (var ei = 0; ei < packageNode.Entries.Count; ei++)
                {
                    var dot = packageNode.Entries[ei].ToDot();
                    dot.SortOrder = ei;
                    package.Dots.Add(dot);
                }

                group.Packages.Add(package);
            }

            viTri.CostGroups.Add(group);
        }

        return viTri;
    }
}
