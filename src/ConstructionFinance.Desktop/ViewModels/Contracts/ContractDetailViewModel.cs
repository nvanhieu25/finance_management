using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.ViewModels.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructionFinance.Desktop.ViewModels.Contracts;

public partial class ContractDetailViewModel : ViewModelBase
{
    private readonly INavigationService _nav;

    public ContractDetailViewModel(ContractDto contract, INavigationService nav, IServiceProvider services)
    {
        _nav = nav;
        Contract = contract;
        Projects = services.GetRequiredService<ProjectListViewModel>();
        Projects.SetContractId(contract.Id);
    }

    public ContractDto Contract { get; }
    public ProjectListViewModel Projects { get; }

    public Task InitializeAsync() => Projects.LoadAsync();

    [RelayCommand]
    private void GoBack() => _nav.GoBack();
}
