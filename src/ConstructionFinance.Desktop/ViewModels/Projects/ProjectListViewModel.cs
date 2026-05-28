using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructionFinance.Desktop.ViewModels.Projects;

public partial class ProjectListViewModel : ViewModelBase
{
    private readonly IProjectService _service;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;
    private readonly IServiceProvider _services;

    public ProjectListViewModel(IProjectService service, IDialogService dialog, INavigationService nav, IServiceProvider services)
    {
        _service = service;
        _dialog = dialog;
        _nav = nav;
        _services = services;
    }

    public int ContractId { get; private set; }
    public ObservableCollection<ProjectDto> Projects { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenDetailCommand))]
    private ProjectDto? _selectedProject;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public void SetContractId(int contractId)
    {
        ContractId = contractId;
        OnPropertyChanged(nameof(IsGlobalView));
        AddCommand.NotifyCanExecuteChanged();
        EditCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    public bool IsGlobalView => ContractId == 0;

    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var list = ContractId == 0
                ? await _service.GetAllAsync()
                : await _service.GetByContractAsync(ContractId);
            Projects.Clear();
            foreach (var p in list) Projects.Add(p);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanWrite))]
    private async Task AddAsync()
    {
        var editVm = new ProjectEditDialogViewModel(initial: null, contractId: ContractId);
        var ok = await _dialog.ShowDialogAsync(editVm);
        if (ok != true) return;
        try
        {
            var created = await _service.CreateAsync(editVm.ToCreateDto());
            Projects.Add(created);
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(CanWriteAndHasSelection))]
    private async Task EditAsync()
    {
        if (SelectedProject is null) return;
        var editVm = new ProjectEditDialogViewModel(initial: SelectedProject, contractId: ContractId);
        var ok = await _dialog.ShowDialogAsync(editVm);
        if (ok != true) return;
        try
        {
            var updated = await _service.UpdateAsync(editVm.ToUpdateDto(SelectedProject.Id));
            var idx = Projects.IndexOf(SelectedProject);
            if (idx >= 0) Projects[idx] = updated;
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(CanWriteAndHasSelection))]
    private async Task DeleteAsync()
    {
        if (SelectedProject is null) return;
        var ok = await _dialog.ConfirmAsync("Xóa Dự án",
            $"Bạn có chắc muốn xóa Dự án \"{SelectedProject.ProjectCode}\"?");
        if (!ok) return;
        try
        {
            await _service.DeleteAsync(SelectedProject.Id);
            Projects.Remove(SelectedProject);
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task OpenDetailAsync()
    {
        if (SelectedProject is null) return Task.CompletedTask;
        var detail = new ProjectDetailViewModel(SelectedProject, _service, _dialog, _nav, _services);
        _nav.NavigateTo(detail);
        return detail.InitializeAsync();
    }

    private bool HasSelection() => SelectedProject is not null;
    private bool CanWrite() => ContractId != 0;
    private bool CanWriteAndHasSelection() => ContractId != 0 && SelectedProject is not null;
}
