using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.ViewModels.Settings;

public partial class CompanyInfoViewModel : ViewModelBase
{
    private readonly ICompanyInfoService _service;
    private readonly IDialogService _dialog;

    public CompanyInfoViewModel(ICompanyInfoService service, IDialogService dialog)
    {
        _service = service;
        _dialog = dialog;
    }

    [ObservableProperty] private string? _companyName;
    [ObservableProperty] private string? _companyAddress;
    [ObservableProperty] private string? _companyPhone;
    [ObservableProperty] private string? _companyEmail;
    [ObservableProperty] private string? _logoPath;
    [ObservableProperty] private string? _defaultSignerName;
    [ObservableProperty] private string? _defaultSignerTitle;
    [ObservableProperty] private string? _defaultRecipients;
    [ObservableProperty] private bool _isBusy;

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var info = await _service.GetAsync();
            CompanyName = info.CompanyName;
            CompanyAddress = info.CompanyAddress;
            CompanyPhone = info.CompanyPhone;
            CompanyEmail = info.CompanyEmail;
            LogoPath = info.LogoPath;
            DefaultSignerName = info.DefaultSignerName;
            DefaultSignerTitle = info.DefaultSignerTitle;
            DefaultRecipients = info.DefaultRecipients;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            await _service.SaveAsync(new CompanyInfoDto(
                CompanyName, CompanyAddress, CompanyPhone, CompanyEmail,
                LogoPath, DefaultSignerName, DefaultSignerTitle, DefaultRecipients));
            await _dialog.ShowMessageAsync("Cài đặt", "Đã lưu thông tin cơ quan thành công.");
        }
        catch (Exception ex)
        {
            await _dialog.ShowMessageAsync("Lỗi", ex.Message);
        }
    }
}
