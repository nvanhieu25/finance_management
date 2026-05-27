using System.Collections.ObjectModel;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.ViewModels.Projects;

// Stub — full implementation in Task 9.
public partial class ProjectListViewModel : ViewModelBase
{
    public ProjectListViewModel(IProjectService service, IDialogService dialog, INavigationService nav, IServiceProvider services)
    {
        _ = service; _ = dialog; _ = nav; _ = services;
    }

    public int ContractId { get; set; }
    public ObservableCollection<ProjectDto> Projects { get; } = new();

    public void SetContractId(int contractId) => ContractId = contractId;
    public Task LoadAsync() => Task.CompletedTask;
}
