using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructionFinance.Desktop.ViewModels.Decisions;

public partial class DecisionListViewModel : ViewModelBase
{
    private readonly IDecisionService _service;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;
    private readonly IServiceProvider _services;

    public DecisionListViewModel(
        IDecisionService service,
        IDialogService dialog,
        INavigationService nav,
        IServiceProvider services)
    {
        _service = service;
        _dialog = dialog;
        _nav = nav;
        _services = services;
    }

    public ObservableCollection<DecisionDto> Decisions { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenDetailCommand))]
    private DecisionDto? _selectedDecision;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var list = await _service.GetAllAsync();
            Decisions.Clear();
            foreach (var d in list) Decisions.Add(d);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var editVm = new DecisionEditDialogViewModel(initial: null);
        var ok = await _dialog.ShowDialogAsync(editVm);
        if (ok != true) return;
        try
        {
            var created = await _service.CreateAsync(editVm.ToCreateDto());
            Decisions.Add(created);
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync()
    {
        if (SelectedDecision is null) return;
        var editVm = new DecisionEditDialogViewModel(initial: SelectedDecision);
        var ok = await _dialog.ShowDialogAsync(editVm);
        if (ok != true) return;
        try
        {
            var updated = await _service.UpdateAsync(editVm.ToUpdateDto(SelectedDecision.Id));
            var idx = Decisions.IndexOf(SelectedDecision);
            if (idx >= 0) Decisions[idx] = updated;
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        if (SelectedDecision is null) return;
        var ok = await _dialog.ConfirmAsync("Xóa Quyết định",
            $"Bạn có chắc muốn xóa Quyết định \"{SelectedDecision.DecisionCode}\"? Có thể khôi phục trong 30 ngày.");
        if (!ok) return;
        try
        {
            await _service.DeleteAsync(SelectedDecision.Id);
            Decisions.Remove(SelectedDecision);
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task OpenDetailAsync()
    {
        if (SelectedDecision is null) return Task.CompletedTask;
        var detail = new DecisionDetailViewModel(SelectedDecision);
        _nav.NavigateTo(detail);
        return Task.CompletedTask;
    }

    private bool HasSelection() => SelectedDecision is not null;
}
