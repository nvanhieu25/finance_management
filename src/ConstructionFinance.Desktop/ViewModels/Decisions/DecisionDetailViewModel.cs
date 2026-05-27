using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.ViewModels.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructionFinance.Desktop.ViewModels.Decisions;

public partial class DecisionDetailViewModel : ViewModelBase
{
    private readonly INavigationService _nav;

    public DecisionDetailViewModel(DecisionDto decision, INavigationService nav, IServiceProvider services)
    {
        _nav = nav;
        Decision = decision;
        Contracts = services.GetRequiredService<ContractListViewModel>();
        Contracts.SetDecisionId(decision.Id);
    }

    public DecisionDto Decision { get; }
    public ContractListViewModel Contracts { get; }

    public Task InitializeAsync() => Contracts.LoadAsync();

    [RelayCommand]
    private void GoBack() => _nav.GoBack();
}
