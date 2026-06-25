using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.ViewModels.TongHop;

public partial class ImportWizardViewModel : ViewModelBase, IHasDialogTitle
{
    private readonly IExcelImportService _import;
    private readonly IProjectSummaryService _summaries;
    private readonly IDialogService _dialog;

    private ImportResultDto? _parsed;

    public ImportWizardViewModel(IExcelImportService import, IProjectSummaryService summaries, IDialogService dialog)
    {
        _import = import;
        _summaries = summaries;
        _dialog = dialog;
    }

    public string DialogTitle => "Import Excel";

    public int? SavedBatchId { get; private set; }

    [ObservableProperty] private string? _selectedFilePath;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public ObservableCollection<ProjectSummaryDto> Preview { get; } = new();
    public ObservableCollection<string> Warnings { get; } = new();

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var path = await _dialog.PickOpenFileAsync("Chọn file Excel", new[] { ".xlsx", ".xlsm" });
        if (path is null) return;

        await LoadFileAsync(path);
    }

    public async Task LoadFileAsync(string path)
    {
        IsBusy = true;
        ErrorMessage = null;
        Preview.Clear();
        Warnings.Clear();
        _parsed = null;
        try
        {
            SelectedFilePath = path;
            _parsed = await _import.ParseAsync(path);
            foreach (var s in _parsed.Summaries) Preview.Add(s);
            foreach (var w in _parsed.Warnings) Warnings.Add(w);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally
        {
            IsBusy = false;
        }
    }

    /// Called by parent after dialog returns Primary — persists the parsed batch.
    /// If a batch with the same file name already exists, offers to overwrite it in place
    /// (refresh) instead of creating a duplicate lô.
    public async Task<int?> SaveAndGetBatchIdAsync()
    {
        if (_parsed is null || Preview.Count == 0) return null;
        try
        {
            var existing = await _summaries.FindBatchByFileNameAsync(_parsed.SourceFileName);
            if (existing is not null)
            {
                var overwrite = await _dialog.ConfirmAsync(
                    "Cập nhật lô import",
                    $"Đã có lô '{existing.SourceFileName}' (nhập {existing.ImportedAtUtc:dd/MM/yyyy HH:mm}).\n" +
                    "Cập nhật (ghi đè) dữ liệu lô này bằng file vừa chọn?");
                if (!overwrite) return null;

                SavedBatchId = await _summaries.ReplaceBatchAsync(existing.Id, _parsed);
                return SavedBatchId;
            }

            SavedBatchId = await _summaries.SaveBatchAsync(_parsed);
            return SavedBatchId;
        }
        catch (Exception ex)
        {
            await _dialog.ShowMessageAsync("Lỗi", ex.Message);
            return null;
        }
    }

}
