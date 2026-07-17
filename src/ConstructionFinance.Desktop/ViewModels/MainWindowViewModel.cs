using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.ViewModels.Decisions;
using ConstructionFinance.Desktop.ViewModels.ThongTri;
using ConstructionFinance.Desktop.ViewModels.TongHop;
using ConstructionFinance.Desktop.ViewModels.Vendors;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ConstructionFinance.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;
    private readonly INavigationService _nav;

    public MainWindowViewModel(IServiceProvider services, INavigationService nav)
    {
        _services = services;
        _nav = nav;
        _nav.CurrentContentChanged += (_, content) => CurrentContent = content;
    }

    public async Task InitializeAsync()
    {
        try { await NavigateToAsync("FinanceWorkbook"); }
        catch (Exception ex) { Log.Warning(ex, "Initial navigation to FinanceWorkbook failed"); }
    }

    [ObservableProperty] private string _title = "Quản lý Tài chính Công trình";
    [ObservableProperty] private string _statusText = $"DB: {AppPaths.DatabaseFile} | Sẵn sàng";
    [ObservableProperty] private object? _currentContent;

    public async Task NavigateToAsync(string tag)
    {
        switch (tag)
        {
            case "TongHop":
                var tvm = _services.GetRequiredService<TongHopViewModel>();
                _nav.Reset();
                _nav.NavigateTo(tvm);
                await tvm.LoadAsync();
                break;
            case "ThongTri":
                var ttvm = _services.GetRequiredService<ThongTriViewModel>();
                _nav.Reset();
                _nav.NavigateTo(ttvm);
                await ttvm.LoadAsync();
                break;
            case "Decisions":
                var dvm = _services.GetRequiredService<DecisionListViewModel>();
                _nav.Reset();
                _nav.NavigateTo(dvm);
                await dvm.LoadAsync();
                break;
            case "Vendors":
                var vvm = _services.GetRequiredService<VendorListViewModel>();
                _nav.Reset();
                _nav.NavigateTo(vvm);
                await vvm.LoadAsync();
                break;
            case "Projects":
                var pvm = _services.GetRequiredService<ViewModels.Projects.ProjectListViewModel>();
                pvm.SetContractId(0);
                _nav.Reset();
                _nav.NavigateTo(pvm);
                await pvm.LoadAsync();
                break;
            case "FinanceGrid":
                var fgvm = _services.GetRequiredService<ViewModels.Finance.FinanceProjectGridViewModel>();
                _nav.Reset();
                _nav.NavigateTo(fgvm);
                await fgvm.LoadAsync();
                break;
            case "FinanceSummary":
                var fsvm = _services.GetRequiredService<ViewModels.Finance.FinanceSummaryViewModel>();
                _nav.Reset();
                _nav.NavigateTo(fsvm);
                await fsvm.LoadAsync();
                break;
            case "FinanceWorkbook":
                var fwvm = _services.GetRequiredService<ViewModels.Finance.FinanceWorkbookViewModel>();
                _nav.Reset();
                _nav.NavigateTo(fwvm);
                await fwvm.LoadAsync();
                break;
            case "Settings":
                var svm = _services.GetRequiredService<ViewModels.Settings.SettingsViewModel>();
                _nav.Reset();
                _nav.NavigateTo(svm);
                await svm.InitializeAsync();
                break;
            default:
                _nav.Reset();
                _nav.NavigateTo(new PlaceholderViewModel { Tag = tag });
                break;
        }
    }

    [RelayCommand]
    private async Task OpenImportWizardAsync()
    {
        var wizard = _services.GetRequiredService<ImportWizardViewModel>();
        var dialog = _services.GetRequiredService<IDialogService>();
        var ok = await dialog.ShowDialogAsync(wizard);
        if (ok != true) return;

        var batchId = await wizard.SaveAndGetBatchIdAsync();
        if (batchId is null) return;

        await NavigateToAsync("TongHop");
        if (_nav.CurrentContent is TongHopViewModel tvm)
        {
            await tvm.LoadAsync();
            tvm.SelectedBatch = tvm.Batches.FirstOrDefault(b => b.Id == batchId.Value) ?? tvm.SelectedBatch;
        }
    }
}

public class PlaceholderViewModel : ViewModelBase
{
    public string Tag { get; set; } = "";
}
