using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.Import;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// Project-centric Tổng hợp: instant name filter → live aggregate rows + grand
/// total; activate a row to jump to that project's workbook tab.
public partial class FinanceSummaryViewModel : ViewModelBase
{
    private readonly IFinanceProjectService _service;
    private readonly IFinanceImportService _importer;
    private readonly IFinanceExportService _exporter;
    private readonly IDialogService _dialog;
    private List<FinanceProjectSummary> _all = new();

    public FinanceSummaryViewModel(
        IFinanceProjectService service,
        IFinanceImportService importer,
        IFinanceExportService exporter,
        IDialogService dialog)
    {
        _service = service;
        _importer = importer;
        _exporter = exporter;
        _dialog = dialog;
    }

    public ObservableCollection<FinanceSummaryRowVm> Rows { get; } = new();

    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private FinanceSummaryRowVm? _selectedRow;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    [ObservableProperty] private decimal _grandContractValue;
    [ObservableProperty] private decimal _grandSumAdvance;
    [ObservableProperty] private decimal _grandSumRecovered;
    [ObservableProperty] private decimal _grandUnrecovered;
    [ObservableProperty] private decimal _grandSumNSNN;
    [ObservableProperty] private decimal _grandSumNgoaiHoi;
    [ObservableProperty] private decimal _grandTotalDisbursed;

    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            _all = (await _service.GetAllSummariesAsync()).ToList();
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
            : _all.Where(s => s.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)).ToList();

        Rows.Clear();
        foreach (var s in matches) Rows.Add(FinanceSummaryRowVm.FromSummary(s));

        var total = ProjectCalculator.Combine(matches.Select(s => s.Aggregate).ToList());
        GrandContractValue = matches.Sum(s => s.ContractValue);
        GrandSumAdvance = total.SumAdvance;
        GrandSumRecovered = total.SumRecovered;
        GrandUnrecovered = total.UnrecoveredAdvance;
        GrandSumNSNN = total.SumNSNN;
        GrandSumNgoaiHoi = total.SumNgoaiHoi;
        GrandTotalDisbursed = total.TotalDisbursed;
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    /// Raised when the user activates a row (double-click). The workbook listens
    /// and switches to that project's tab — the page never reaches up to its host.
    public event EventHandler<int>? ProjectActivationRequested;

    public void ActivateProject(int projectId) => ProjectActivationRequested?.Invoke(this, projectId);

    [RelayCommand]
    private async Task ImportAsync()
    {
        var path = await _dialog.PickOpenFileAsync("Chọn file Excel cũ", new[] { ".xlsx" });
        if (string.IsNullOrEmpty(path)) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var result = await _importer.ParseWorkbookAsync(path);
            if (result.Projects.Count == 0)
            {
                await _dialog.ShowMessageAsync("Nhập Excel", "Không tìm thấy dự án (sheet DA) nào trong file.");
                return;
            }

            var existing = await _service.ListAsync();
            var candidates = ImportMerge.Classify(result.Projects, existing);

            if (result.Warnings.Count > 0)
                await _dialog.ShowMessageAsync("Cảnh báo khi đọc file",
                    $"{result.Warnings.Count} cảnh báo:\n- " + string.Join("\n- ", result.Warnings.Take(10)));

            var review = new ImportResolutionDialogViewModel(candidates);
            if (await _dialog.ShowDialogAsync(review) != true) return;

            var saved = await _service.SaveMergedAsync(review.ToCandidates());
            await LoadAsync();
            await _dialog.ShowMessageAsync("Nhập Excel", $"Đã nhập {saved} dự án.");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            await _dialog.ShowMessageAsync("Lỗi nhập Excel", ex.Message);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (Rows.Count == 0)
        {
            await _dialog.ShowMessageAsync("Xuất Excel", "Không có dự án nào để xuất.");
            return;
        }

        var path = await _dialog.PickSaveFileAsync("Xuất Excel", "TongHop.xlsx", new[] { ".xlsx" });
        if (string.IsNullOrEmpty(path)) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var projects = new List<ConstructionFinance.Domain.Finance.Project>();
            foreach (var row in Rows)
            {
                var p = await _service.GetWithLinesAsync(row.ProjectId);
                if (p is not null) projects.Add(p);
            }

            await _exporter.ExportAsync(projects, path);
            await _dialog.ShowMessageAsync("Xuất Excel", $"Đã xuất {projects.Count} dự án ra:\n{path}");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            await _dialog.ShowMessageAsync("Lỗi xuất Excel", ex.Message);
        }
        finally { IsBusy = false; }
    }
}
