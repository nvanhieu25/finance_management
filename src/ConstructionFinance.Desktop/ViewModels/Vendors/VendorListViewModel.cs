using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.ViewModels.Vendors;

public partial class VendorListViewModel : ViewModelBase
{
    private readonly IVendorService _service;
    private readonly IDialogService _dialog;

    public VendorListViewModel(IVendorService service, IDialogService dialog)
    {
        _service = service;
        _dialog = dialog;
    }

    public ObservableCollection<VendorDto> Vendors { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private VendorDto? _selectedVendor;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var list = await _service.GetAllAsync();
            Vendors.Clear();
            foreach (var v in list) Vendors.Add(v);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var editVm = new VendorEditDialogViewModel(initial: null);
        var ok = await _dialog.ShowDialogAsync(editVm);
        if (ok != true) return;
        try
        {
            var created = await _service.CreateAsync(editVm.ToCreateDto());
            Vendors.Add(created);
        }
        catch (Exception ex)
        {
            await _dialog.ShowMessageAsync("Lỗi", ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync()
    {
        if (SelectedVendor is null) return;
        var editVm = new VendorEditDialogViewModel(initial: SelectedVendor);
        var ok = await _dialog.ShowDialogAsync(editVm);
        if (ok != true) return;
        try
        {
            var updated = await _service.UpdateAsync(editVm.ToUpdateDto(SelectedVendor.Id));
            var idx = Vendors.IndexOf(SelectedVendor);
            if (idx >= 0) Vendors[idx] = updated;
        }
        catch (Exception ex)
        {
            await _dialog.ShowMessageAsync("Lỗi", ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        if (SelectedVendor is null) return;
        var ok = await _dialog.ConfirmAsync("Xóa nhà cung cấp",
            $"Bạn có chắc muốn xóa \"{SelectedVendor.VendorName}\"? Hành động này có thể khôi phục trong 30 ngày.");
        if (!ok) return;
        try
        {
            await _service.DeleteAsync(SelectedVendor.Id);
            Vendors.Remove(SelectedVendor);
        }
        catch (Exception ex)
        {
            await _dialog.ShowMessageAsync("Lỗi", ex.Message);
        }
    }

    private bool HasSelection() => SelectedVendor is not null;
}
