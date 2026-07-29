using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// All-projects Tổng hợp (tree model): every project's Vị trí aggregated, grouped
/// Project → Vị trí → grand total, with an instant name filter. Double-click a
/// project row to open its workbook tab.
public partial class WorkbookSummaryViewModel : ViewModelBase
{
    private readonly IFinanceProjectService _service;
    private List<Project> _all = new();

    public WorkbookSummaryViewModel(IFinanceProjectService service) => _service = service;

    public ObservableCollection<SummaryDisplayRow> Rows { get; } = new();

    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private SummaryDisplayRow? _selectedRow;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            _all = (await _service.ListTreesAsync()).ToList();
            ApplyFilter();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var needle = FilterText?.Trim() ?? "";
        var matches = string.IsNullOrEmpty(needle)
            ? _all
            : _all.Where(p => p.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)).ToList();

        Rows.Clear();
        foreach (var row in WorkbookSummaryBuilder.BuildAll(matches))
            Rows.Add(SummaryDisplayRow.From(row));
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    /// Raised on double-click; the host workbook switches to the project's tab.
    public event EventHandler<int>? ProjectActivationRequested;

    public void ActivateProject(int projectId) => ProjectActivationRequested?.Invoke(this, projectId);
}
