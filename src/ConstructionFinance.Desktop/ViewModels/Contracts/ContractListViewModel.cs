using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructionFinance.Desktop.ViewModels.Contracts;

public partial class ContractListViewModel : ViewModelBase
{
    private readonly IContractService _service;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;
    private readonly IServiceProvider _services;

    public ContractListViewModel(
        IContractService service,
        IDialogService dialog,
        INavigationService nav,
        IServiceProvider services)
    {
        _service = service;
        _dialog = dialog;
        _nav = nav;
        _services = services;
    }

    public int DecisionId { get; private set; }
    public ObservableCollection<ContractDto> Contracts { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenDetailCommand))]
    private ContractDto? _selectedContract;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public void SetDecisionId(int decisionId) => DecisionId = decisionId;

    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var list = await _service.GetByDecisionAsync(DecisionId);
            Contracts.Clear();
            foreach (var c in list) Contracts.Add(c);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var editVm = new ContractEditDialogViewModel(initial: null, decisionId: DecisionId);
        var ok = await _dialog.ShowDialogAsync(editVm);
        if (ok != true) return;
        try
        {
            var created = await _service.CreateAsync(editVm.ToCreateDto());
            Contracts.Add(created);
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync()
    {
        if (SelectedContract is null) return;
        var editVm = new ContractEditDialogViewModel(initial: SelectedContract, decisionId: DecisionId);
        var ok = await _dialog.ShowDialogAsync(editVm);
        if (ok != true) return;
        try
        {
            var updated = await _service.UpdateAsync(editVm.ToUpdateDto(SelectedContract.Id));
            var idx = Contracts.IndexOf(SelectedContract);
            if (idx >= 0) Contracts[idx] = updated;
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        if (SelectedContract is null) return;
        var ok = await _dialog.ConfirmAsync("Xóa Hợp đồng",
            $"Bạn có chắc muốn xóa Hợp đồng \"{SelectedContract.ContractCode}\"?");
        if (!ok) return;
        try
        {
            await _service.DeleteAsync(SelectedContract.Id);
            Contracts.Remove(SelectedContract);
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task OpenDetailAsync()
    {
        if (SelectedContract is null) return Task.CompletedTask;
        var detail = new ContractDetailViewModel(SelectedContract);
        _nav.NavigateTo(detail);
        return Task.CompletedTask;
    }

    private bool HasSelection() => SelectedContract is not null;
}
