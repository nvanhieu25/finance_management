using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// Project-centric Tổng hợp: instant name filter → live aggregate rows + grand
/// total; open a row to edit that project in the Phase-3 entry grid.
public partial class FinanceSummaryViewModel : ViewModelBase
{
    private readonly IFinanceProjectService _service;
    private readonly INavigationService _nav;
    private readonly IServiceProvider _services;
    private List<FinanceProjectSummary> _all = new();

    public FinanceSummaryViewModel(IFinanceProjectService service, INavigationService nav, IServiceProvider services)
    {
        _service = service;
        _nav = nav;
        _services = services;
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

    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        if (SelectedRow is null) return;
        var grid = _services.GetRequiredService<FinanceProjectGridViewModel>();
        await grid.LoadProjectAsync(SelectedRow.ProjectId);
        _nav.NavigateTo(grid);
    }
}
