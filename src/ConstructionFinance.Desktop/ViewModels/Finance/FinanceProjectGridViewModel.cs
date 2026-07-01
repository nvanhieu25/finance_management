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

/// Master–detail entry grid: pick a project, edit its đợt rows Excel-style,
/// totals computed live by ProjectCalculator, persisted via the finance service.
public partial class FinanceProjectGridViewModel : ViewModelBase
{
    private readonly IFinanceProjectService _service;
    private readonly IDialogService _dialog;
    private bool _suppressSelectionLoad;

    public FinanceProjectGridViewModel(IFinanceProjectService service, IDialogService dialog)
    {
        _service = service;
        _dialog = dialog;
        Lines.CollectionChanged += OnLinesCollectionChanged;
    }

    public ObservableCollection<Project> Projects { get; } = new();
    public ObservableCollection<LineItemRowViewModel> Lines { get; } = new();

    [ObservableProperty] private Project? _selectedProject;
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

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var list = await _service.ListAsync();
            Projects.Clear();
            foreach (var p in list) Projects.Add(p);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    partial void OnSelectedProjectChanged(Project? value)
    {
        if (_suppressSelectionLoad || value is null) return;
        _ = LoadProjectAsync(value.Id);
    }

    public async Task LoadProjectAsync(int id)
    {
        IsBusy = true;
        ErrorMessage = null;
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
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void NewProject()
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

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            await _dialog.ShowMessageAsync("Thiếu thông tin", "Tên dự án không được để trống.");
            return;
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

            await LoadAsync();
            _suppressSelectionLoad = true;
            SelectedProject = Projects.FirstOrDefault(p => p.Id == CurrentProjectId);
            _suppressSelectionLoad = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            await _dialog.ShowMessageAsync("Lỗi", ex.Message);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteProjectAsync()
    {
        if (CurrentProjectId == 0) return;
        var ok = await _dialog.ConfirmAsync("Xóa dự án",
            $"Bạn có chắc muốn xóa dự án \"{ProjectName}\" và toàn bộ đợt của nó?");
        if (!ok) return;
        try
        {
            await _service.DeleteAsync(CurrentProjectId);
            await LoadAsync();
            NewProject();
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    /// Paste a TSV block (rows of cells) starting at the given cell; new rows are
    /// appended as needed. Only editable columns (0–6) are written. (Wired in Task 7.)
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
    }

    private void OnRowInputChanged(object? sender, EventArgs e) => RecomputeTotals();

    private void ClearLines()
    {
        foreach (var row in Lines) row.InputChanged -= OnRowInputChanged;
        Lines.Clear();
    }
}
