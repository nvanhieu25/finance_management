using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.ViewModels.Vendors;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructionFinance.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;

    public MainWindowViewModel(IServiceProvider services)
    {
        _services = services;
    }

    [ObservableProperty] private string _title = "Quản lý Tài chính Công trình";
    [ObservableProperty] private string _statusText = $"DB: {AppPaths.DatabaseFile} | Sẵn sàng";
    [ObservableProperty] private object? _currentContent;

    public async Task NavigateToAsync(string tag)
    {
        switch (tag)
        {
            case "Vendors":
                var vm = _services.GetRequiredService<VendorListViewModel>();
                CurrentContent = vm;
                await vm.LoadAsync();
                break;
            default:
                CurrentContent = new PlaceholderViewModel { Tag = tag };
                break;
        }
    }
}

public class PlaceholderViewModel : ViewModelBase
{
    public string Tag { get; set; } = "";
}
