using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// Excel-like workbook: a pinned TH page plus one page per project.
public partial class FinanceWorkbookViewModel : ViewModelBase
{
    private readonly IFinanceProjectService _service;
    private readonly IDialogService _dialog;
    private readonly IServiceProvider _services;

    public FinanceWorkbookViewModel(
        IFinanceProjectService service,
        SummaryPageViewModel summaryPage,
        IDialogService dialog,
        IServiceProvider services)
    {
        _service = service;
        _dialog = dialog;
        _services = services;
        SummaryPage = summaryPage;
    }

    public ObservableCollection<WorkbookPageViewModel> Pages { get; } = new();

    public SummaryPageViewModel SummaryPage { get; }

    [ObservableProperty] private WorkbookPageViewModel? _selectedPage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            Pages.Clear();
            Pages.Add(SummaryPage);
            SelectedPage = SummaryPage;
            await SummaryPage.Summary.LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }
}
