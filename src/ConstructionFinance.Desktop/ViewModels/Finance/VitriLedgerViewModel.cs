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

    /// Nhãn của dòng TỔNG CỘNG. Nguồn DUY NHẤT cho cả màn hình (VitriLedgerView.axaml lấy bằng {x:Static}) lẫn bản in
    /// (ViTriPageViewModel): hai nơi phải viết cùng một chữ, không thì bản in thôi khớp màn hình. Không lấy từ chính dòng
    /// được vì Name của dòng TỔNG CỘNG là null.
    public const string TotalLabel = "TỔNG CỘNG";

    private readonly LedgerRowViewModel _totalRow = new(RowKind.Total);
    private readonly List<GroupNode> _groups = new();
    private bool _loading;

    public ObservableCollection<LedgerRowViewModel> Rows { get; } = new();

    /// The frozen TỔNG CỘNG row (also Rows[0] — exposed directly so the View can bind
    /// the frozen top bar without an indexer path). The reference never changes.
    public LedgerRowViewModel TotalRow => _totalRow;

    /// Shared column-width model for the outline grid (Excel-style manual column
    /// resize) — ONE instance per Vị trí tab, bound by the header, the frozen TỔNG CỘNG
    /// row, and every RowKind row template, so dragging a header grip resizes all of
    /// them together. See LedgerColumnWidths for why this keeps content from ever
    /// growing/shifting a column.
    public LedgerColumnWidths ColumnWidths { get; } = new();

    /// Excel-style "wrap text" toggle for the outline grid's long display cells (Nội
    /// dung / Group·Package Name). Off (default): a cell that overflows shows past its
    /// column into empty neighbors (Excel-style) and clips under the next occupied one.
    /// On: text wraps within the column and the row grows taller instead of overflowing.
    [ObservableProperty] private bool _wrapText;

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

    /// Mirrors SelectedRow onto each row's own IsSelected flag so the View's row templates
    /// can paint a highlight (7B review, Important #1 — the user had no visible feedback
    /// that a row was selected at all). SelectedRow itself stays the single source of truth.
    partial void OnSelectedRowChanged(LedgerRowViewModel? value)
    {
        foreach (var row in Rows)
            row.IsSelected = ReferenceEquals(row, value);
    }

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

    /// Adds a Package under the Group resolved from `hint` — typically SelectedRow (from
    /// the toolbar button) or the specific Group row an inline "＋ gói" button was clicked
    /// on. See ResolveTargetGroup for the resolution rule. No-op (nothing added) if no
    /// Group can be resolved — a Package must live under a Group.
    [RelayCommand]
    private void AddPackage(LedgerRowViewModel? hint)
    {
        var groupNode = ResolveTargetGroup(hint);
        if (groupNode is null) return;

        var row = new LedgerRowViewModel(RowKind.Package) { Name = "Gói thầu mới", Depth = 1 };
        Track(row);
        groupNode.Packages.Add(new PackageNode(row));

        RebuildRows();
        RecomputeSubtotals();
        SelectedRow = row;
        MarkDirty();
    }

    /// Adds an Entry (Dot) under the Package resolved from `hint` — typically SelectedRow
    /// (from the toolbar button) or the specific Package row an inline "＋ đợt" button was
    /// clicked on. See ResolveTargetPackage for the resolution rule. No-op (nothing added)
    /// if no Package can be resolved.
    [RelayCommand]
    private void AddEntry(LedgerRowViewModel? hint)
    {
        var packageNode = ResolveTargetPackage(hint);
        if (packageNode is null) return;

        var row = new LedgerRowViewModel(RowKind.Entry) { Depth = 2 };
        Track(row);
        packageNode.Entries.Add(row);

        RebuildRows();
        RecomputeSubtotals();
        SelectedRow = row;
        MarkDirty();
    }

    /// Resolves which Group a "Thêm gói" action should target: a Group hint resolves
    /// directly; a Package/Entry hint resolves to its owning Group; anything else (no
    /// hint, a Total hint, or a hint this VM doesn't recognize) falls back to the SOLE
    /// group in the Vị trí when there is exactly one — otherwise the action is genuinely
    /// ambiguous (or there's nothing to attach to yet) and this returns null (no-op).
    private GroupNode? ResolveTargetGroup(LedgerRowViewModel? hint)
    {
        if (hint is not null)
        {
            if (hint.Kind == RowKind.Group)
            {
                var direct = _groups.FirstOrDefault(g => ReferenceEquals(g.Row, hint));
                if (direct is not null) return direct;
            }
            else if (hint.Kind is RowKind.Package or RowKind.Entry)
            {
                var owner = FindOwningGroup(hint);
                if (owner is not null) return owner;
            }
        }

        return _groups.Count == 1 ? _groups[0] : null;
    }

    /// Resolves which Package a "Thêm đợt" action should target: a Package hint resolves
    /// directly; an Entry hint resolves to its owning Package; anything else falls back to
    /// the SOLE package across the whole Vị trí when there is exactly one — otherwise null.
    private PackageNode? ResolveTargetPackage(LedgerRowViewModel? hint)
    {
        if (hint is not null)
        {
            if (hint.Kind == RowKind.Package)
            {
                var direct = FindPackage(hint).package;
                if (direct is not null) return direct;
            }
            else if (hint.Kind == RowKind.Entry)
            {
                var owner = FindPackageContainingEntry(hint);
                if (owner is not null) return owner;
            }
        }

        var allPackages = _groups.SelectMany(g => g.Packages).ToList();
        return allPackages.Count == 1 ? allPackages[0] : null;
    }

    /// Finds the Group that owns a Package or Entry row (Package → its group directly;
    /// Entry → the group of its owning Package). Null for anything else / not found.
    private GroupNode? FindOwningGroup(LedgerRowViewModel row)
    {
        if (row.Kind == RowKind.Package)
            return FindPackage(row).group;

        if (row.Kind == RowKind.Entry)
        {
            foreach (var g in _groups)
                foreach (var p in g.Packages)
                    if (p.Entries.Contains(row))
                        return g;
        }

        return null;
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
        row.Aggregate = agg;
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
