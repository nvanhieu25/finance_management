using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructionFinance.Desktop.ViewModels.TongHop;

public partial class TongHopViewModel : ViewModelBase
{
    private readonly IProjectSummaryService _summaries;
    private readonly IExcelExportService _export;
    private readonly IDialogService _dialog;
    private readonly IServiceProvider _services;

    private IReadOnlyList<ProjectSummaryDto> _currentSummaries = Array.Empty<ProjectSummaryDto>();

    public TongHopViewModel(
        IProjectSummaryService summaries,
        IExcelExportService export,
        IDialogService dialog,
        IServiceProvider services)
    {
        _summaries = summaries;
        _export = export;
        _dialog = dialog;
        _services = services;
    }

    public ObservableCollection<ImportBatchDto> Batches { get; } = new();
    public ObservableCollection<TongHopRow> Rows { get; } = new();

    [ObservableProperty] private ImportBatchDto? _selectedBatch;
    [ObservableProperty] private string? _filterText;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryFooter))]
    private TongHopAggregateDto? _aggregate;

    public string SummaryFooter => Aggregate is null
        ? ""
        : $"Tổng {Aggregate.ProjectCount} dự án — Tổng giải ngân: {Aggregate.TotalDisbursed:#,##0}đ";

    partial void OnSelectedBatchChanged(ImportBatchDto? value) => _ = ReloadSummariesAsync();

    partial void OnFilterTextChanged(string? value) => ApplyFilter();

    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            Batches.Clear();
            var list = await _summaries.ListBatchesAsync();
            foreach (var b in list) Batches.Add(b);

            var latest = await _summaries.GetLatestBatchAsync();
            if (latest is not null && Batches.All(b => b.Id != latest.Id))
                Batches.Insert(0, latest);
            SelectedBatch = Batches.FirstOrDefault() ?? latest;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task ReloadSummariesAsync()
    {
        if (SelectedBatch is null)
        {
            _currentSummaries = Array.Empty<ProjectSummaryDto>();
            Rows.Clear();
            Aggregate = null;
            return;
        }

        IsBusy = true;
        try
        {
            _currentSummaries = await _summaries.ListByBatchAsync(SelectedBatch.Id);
            Aggregate = await _summaries.AggregateAsync(SelectedBatch.Id);
            ApplyFilter();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    private void ApplyFilter()
    {
        Rows.Clear();
        var filtered = string.IsNullOrWhiteSpace(FilterText)
            ? _currentSummaries
            : _currentSummaries.Where(s =>
                s.ProjectName.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                (s.ContractorName?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false));
        var stt = 1;
        foreach (var s in filtered)
            Rows.Add(TongHopRow.FromDto(stt++, s));
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var wizard = _services.GetRequiredService<ImportWizardViewModel>();
        var ok = await _dialog.ShowDialogAsync(wizard);
        if (ok == true)
        {
            var batchId = await wizard.SaveAndGetBatchIdAsync();
            if (batchId is int id)
            {
                await LoadAsync();
                SelectedBatch = Batches.FirstOrDefault(b => b.Id == id) ?? SelectedBatch;
            }
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (_currentSummaries.Count == 0)
        {
            await _dialog.ShowMessageAsync("Xuất Excel", "Không có dữ liệu để xuất.");
            return;
        }

        var template = await _dialog.PickOpenFileAsync("Chọn file mẫu Excel", new[] { ".xlsx", ".xlsm" });
        if (template is null) return;

        var target = await _dialog.PickSaveFileAsync("Lưu file xuất", "TongHop_export.xlsx", new[] { ".xlsx" });
        if (target is null) return;

        try
        {
            await _export.ExportTongHopAsync(template, target, _currentSummaries);
            await _dialog.ShowMessageAsync("Xuất Excel", $"Đã lưu: {target}");
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }
}
