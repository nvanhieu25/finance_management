using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// Excel-like entry grid for exactly ONE project — the workbook tab that hosts
/// this instance IS the project selection, so there is no project picker here.
/// Totals are computed live by ProjectCalculator; nothing derived is persisted.
public partial class FinanceProjectGridViewModel : ViewModelBase
{
    private readonly IFinanceProjectService _service;
    private readonly IDialogService _dialog;
    private bool _loading;

    public FinanceProjectGridViewModel(IFinanceProjectService service, IDialogService dialog)
    {
        _service = service;
        _dialog = dialog;
        Lines.CollectionChanged += OnLinesCollectionChanged;
    }

    public ObservableCollection<LineItemRowViewModel> Lines { get; } = new();

    [ObservableProperty] private LineItemRowViewModel? _selectedRow;

    [ObservableProperty] private int _currentProjectId;
    [ObservableProperty] private string _projectName = "";
    [ObservableProperty] private string? _projectCode;
    [ObservableProperty] private string? _contractorName;
    [ObservableProperty] private decimal _contractValue;
    [ObservableProperty] private decimal _contractAnnexValue;
    [ObservableProperty] private string? _notes;

    [ObservableProperty] private decimal _sumAdvance;
    [ObservableProperty] private decimal _sumKLHT;
    [ObservableProperty] private decimal _sumRecovered;
    [ObservableProperty] private decimal _sumNSNN;
    [ObservableProperty] private decimal _sumNgoaiHoi;
    [ObservableProperty] private decimal _unrecoveredAdvance;
    [ObservableProperty] private decimal _totalDisbursed;

    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    /// Raised whenever IsDirty flips, so the hosting page can bubble it to the workbook.
    public event EventHandler? DirtyChanged;

    // Header edits mark the project unsaved. Derived Sum* props deliberately do
    // NOT mark dirty — they are recomputed output, not user input.
    partial void OnProjectNameChanged(string value) => MarkDirty();
    partial void OnProjectCodeChanged(string? value) => MarkDirty();
    partial void OnContractorNameChanged(string? value) => MarkDirty();
    partial void OnContractValueChanged(decimal value) => MarkDirty();
    partial void OnContractAnnexValueChanged(decimal value) => MarkDirty();
    partial void OnNotesChanged(string? value) => MarkDirty();

    partial void OnIsDirtyChanged(bool value) => DirtyChanged?.Invoke(this, EventArgs.Empty);

    private void MarkDirty()
    {
        if (_loading) return;
        IsDirty = true;
    }

    /// Blank project with one starter row. Unsaved by definition → starts dirty.
    public void InitializeNew()
    {
        _loading = true;
        try
        {
            CurrentProjectId = 0;
            ProjectName = "";
            ProjectCode = null;
            ContractorName = null;
            ContractValue = 0m;
            ContractAnnexValue = 0m;
            Notes = null;
            ClearLines();
            Lines.Add(new LineItemRowViewModel());
            RecomputeTotals();
        }
        finally { _loading = false; }
        IsDirty = true;
    }

    public async Task LoadProjectAsync(int id)
    {
        IsBusy = true;
        ErrorMessage = null;
        _loading = true;
        try
        {
            var project = await _service.GetWithLinesAsync(id);
            if (project is null) return;

            CurrentProjectId = project.Id;
            ProjectName = project.Name;
            ProjectCode = project.Code;
            ContractorName = project.ContractorName;
            ContractValue = project.ContractValue;
            ContractAnnexValue = project.ContractAnnexValue;
            Notes = project.Notes;

            ClearLines();
            foreach (var line in project.Lines.OrderBy(l => l.SortOrder))
                Lines.Add(new LineItemRowViewModel(line));
            RecomputeTotals();
            IsDirty = false;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally
        {
            _loading = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AddRow()
    {
        Lines.Add(new LineItemRowViewModel());
        RecomputeTotals();
    }

    [RelayCommand]
    private void DeleteRow()
    {
        if (SelectedRow is null) return;
        Lines.Remove(SelectedRow);
        RecomputeTotals();
    }

    // The toolbar binds SaveCommand; the workbook calls SaveAsync directly for
    // its bool result. [RelayCommand] cannot wrap a Task<bool> method, so the
    // command is a thin void-ish wrapper over the real one.
    [RelayCommand]
    private async Task Save() => await SaveAsync();

    /// Persist this project. Returns true on success. The workbook's Save-All
    /// relies on the bool: a failed page must stay dirty so it is not lost.
    public async Task<bool> SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            await _dialog.ShowMessageAsync("Thiếu thông tin", "Tên dự án không được để trống.");
            return false;
        }

        var project = new Project
        {
            Id = CurrentProjectId,
            Name = ProjectName.Trim(),
            Code = ProjectCode,
            ContractorName = ContractorName,
            ContractValue = ContractValue,
            ContractAnnexValue = ContractAnnexValue,
            Notes = Notes
        };
        for (var i = 0; i < Lines.Count; i++)
        {
            Lines[i].SortOrder = i + 1;
            project.Lines.Add(Lines[i].ToEntity());
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (CurrentProjectId == 0)
                CurrentProjectId = await _service.CreateAsync(project);
            else
                await _service.UpdateAsync(project);

            IsDirty = false;
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return false;
        }
        finally { IsBusy = false; }
    }

    /// Paste a TSV block (rows of cells) starting at the given cell; new rows are
    /// appended as needed. Only editable columns (0–6) are written.
    public void ApplyPaste(int startRow, int startCol, string[][] block)
    {
        for (var r = 0; r < block.Length; r++)
        {
            var targetIndex = startRow + r;
            while (Lines.Count <= targetIndex)
                Lines.Add(new LineItemRowViewModel());

            var cells = block[r];
            for (var c = 0; c < cells.Length; c++)
                Lines[targetIndex].SetCell(startCol + c, cells[c]);
        }
        RecomputeTotals();
    }

    public void RecomputeTotals()
    {
        var inputs = Lines.Select(l => l.ToInput()).ToList();
        var agg = ProjectCalculator.Aggregate(inputs);
        SumAdvance = agg.SumAdvance;
        SumKLHT = agg.SumKLHT;
        SumRecovered = agg.SumRecovered;
        SumNSNN = agg.SumNSNN;
        SumNgoaiHoi = agg.SumNgoaiHoi;
        UnrecoveredAdvance = agg.UnrecoveredAdvance;
        TotalDisbursed = agg.TotalDisbursed;
    }

    private void OnLinesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (LineItemRowViewModel row in e.OldItems) row.InputChanged -= OnRowInputChanged;
        if (e.NewItems is not null)
            foreach (LineItemRowViewModel row in e.NewItems) row.InputChanged += OnRowInputChanged;
        MarkDirty();
    }

    private void OnRowInputChanged(object? sender, EventArgs e)
    {
        RecomputeTotals();
        MarkDirty();
    }

    private void ClearLines()
    {
        foreach (var row in Lines) row.InputChanged -= OnRowInputChanged;
        Lines.Clear();
    }
}
