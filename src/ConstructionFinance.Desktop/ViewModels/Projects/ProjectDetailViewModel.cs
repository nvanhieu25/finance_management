using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Desktop.ViewModels.Projects;

public partial class ProjectDetailViewModel : ViewModelBase
{
    private readonly IProjectService _service;
    private readonly IDialogService _dialog;
    private readonly INavigationService _nav;

    public ProjectDetailViewModel(ProjectDto project, IProjectService service, IDialogService dialog, INavigationService nav)
    {
        _service = service;
        _dialog = dialog;
        _nav = nav;
        Project = project;
    }

    [ObservableProperty] private ProjectDto _project;

    public IReadOnlyList<ProjectStatus> AvailableStatuses { get; } =
        Enum.GetValues<ProjectStatus>().ToList();

    public async Task ChangeStatusAsync(ProjectStatus newStatus)
    {
        try
        {
            Project = await _service.UpdateStatusAsync(Project.Id, newStatus);
        }
        catch (Exception ex)
        {
            await _dialog.ShowMessageAsync("Lỗi", ex.Message);
        }
    }

    [RelayCommand]
    private void GoBack() => _nav.GoBack();
}
