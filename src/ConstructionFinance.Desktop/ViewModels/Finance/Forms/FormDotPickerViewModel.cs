using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Desktop.ViewModels.Finance.Forms;

/// Panel chọn đợt dùng chung cho ba tab biểu mẫu. Danh sách đã lọc sẵn theo Nguồn
/// của form; ô lọc tháng/năm chỉ THU HẸP phần hiển thị - đợt đang tích mà bị ẩn vẫn
/// nằm trong form, nên SelectionSummary luôn hiện số đang chọn.
public partial class FormDotPickerViewModel : ViewModelBase, IHasDialogTitle, IHasPrimaryButtonText
{
    private readonly Fund? _fundFilter;

    // Danh tính lựa chọn theo Token của từng dòng (xem FormDotItemViewModel.Token).
    private readonly HashSet<object> _selectedTokens = new();

    private List<FormDotItemViewModel> _allItems = new();

    public FormDotPickerViewModel(Fund? fundFilter) => _fundFilter = fundFilter;

    /// Tiêu đề hộp thoại (ghi theo biểu mẫu: "Chọn đợt — Thông tri").
    public string Title { get; init; } = "Chọn đợt";

    string IHasDialogTitle.DialogTitle => Title;
    string IHasPrimaryButtonText.PrimaryButtonText => "OK";

    /// Chụp lựa chọn trước khi mở hộp thoại, để Hủy trả lại đúng như cũ.
    public IReadOnlySet<object> SaveSelection() => _selectedTokens.ToHashSet();

    public void RestoreSelection(IReadOnlySet<object> tokens)
    {
        InBatch(() =>
        {
            foreach (var item in _allItems) item.IsSelected = tokens.Contains(item.Token);
        });
    }

    /// Các dòng đang hiển thị (sau bộ lọc tháng/năm).
    public ObservableCollection<FormDotItemViewModel> Items { get; } = new();

    [ObservableProperty] private string? _monthFilter;
    [ObservableProperty] private string? _yearFilter;

    public int SelectedCount => _allItems.Count(i => i.IsSelected);
    public string SelectionSummary => $"Đang chọn {SelectedCount} đợt";

    public IReadOnlySet<FormDotKey> SelectedKeys =>
        _allItems.Where(i => i.IsSelected).Select(i => i.Data.Key).ToHashSet();

    /// Bắn mỗi khi tập đợt đã chọn đổi, để tab form dựng lại bản xem trước.
    public event EventHandler? SelectionChanged;

    /// Dựng lại danh sách từ cây hiện tại. `sources` là các dòng đợt của lưới theo
    /// ĐÚNG thứ tự của `rows` (workbook truyền vào); truyền mảng rỗng thì lựa chọn
    /// bám theo khóa thay vì theo đối tượng dòng.
    public void Refresh(IReadOnlyList<FormDotRow> rows, IReadOnlyList<LedgerRowViewModel> sources)
    {
        var usable = sources.Count == rows.Count ? sources : Array.Empty<LedgerRowViewModel>();

        var items = new List<FormDotItemViewModel>();
        for (var i = 0; i < rows.Count; i++)
        {
            if (_fundFilter is not null && rows[i].Fund != _fundFilter) continue;

            object token = usable.Count == rows.Count ? usable[i] : rows[i].Key;
            var item = new FormDotItemViewModel(rows[i], token) { IsSelected = _selectedTokens.Contains(token) };
            item.PropertyChanged += OnItemPropertyChanged;
            items.Add(item);
        }

        foreach (var old in _allItems) old.PropertyChanged -= OnItemPropertyChanged;
        _allItems = items;

        // Đợt đã bị xóa khỏi sổ thì bỏ khỏi lựa chọn, đừng để nó âm thầm ở lại.
        var live = items.Select(i => i.Token).ToHashSet();
        _selectedTokens.RemoveWhere(t => !live.Contains(t));

        ApplyFilter();
        RaiseSelectionState();
    }

    partial void OnMonthFilterChanged(string? value) => ApplyFilter();
    partial void OnYearFilterChanged(string? value) => ApplyFilter();

    private void OnItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FormDotItemViewModel.IsSelected)) return;
        if (sender is not FormDotItemViewModel item) return;

        if (item.IsSelected) _selectedTokens.Add(item.Token);
        else _selectedTokens.Remove(item.Token);

        RaiseSelectionState();
    }

    private void ApplyFilter()
    {
        var month = ParseFilter(MonthFilter);
        var year = ParseFilter(YearFilter);

        Items.Clear();
        foreach (var item in _allItems)
        {
            if (month is not null && item.Data.Date.Month != month) continue;
            if (year is not null && item.Data.Date.Year != year) continue;
            Items.Add(item);
        }
    }

    private static int? ParseFilter(string? raw) =>
        int.TryParse(raw?.Trim(), out var value) ? value : null;

    [RelayCommand]
    private void SelectAll() => InBatch(() =>
    {
        foreach (var item in Items) item.IsSelected = true;
    });

    [RelayCommand]
    private void ClearSelection() => InBatch(() =>
    {
        foreach (var item in _allItems) item.IsSelected = false;
    });

    // Đổi nhiều ô tích một lượt (Chọn tất cả / Bỏ chọn / Hủy khôi phục) chỉ báo SelectionChanged MỘT lần: mỗi lần báo là
    // một lần dựng lại cả biểu mẫu, nên báo từng ô thì 300 đợt thành 300 lần dựng (đo được 16 giây).
    private int _batchDepth;

    private void InBatch(Action change)
    {
        _batchDepth++;
        try { change(); }
        finally
        {
            _batchDepth--;
            if (_batchDepth == 0) RaiseSelectionState();
        }
    }

    private void RaiseSelectionState()
    {
        if (_batchDepth > 0) return;
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(SelectedKeys));
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
