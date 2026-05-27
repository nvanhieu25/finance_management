using System.Collections.ObjectModel;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.ViewModels.Contracts;

// Stub — full implementation in Task 6.
public partial class ContractListViewModel : ViewModelBase
{
    public ContractListViewModel(IContractService service, IDialogService dialog, INavigationService nav, IServiceProvider services)
    {
        // service/dialog/nav/services parameters reserved for full impl in Task 6
        _ = service; _ = dialog; _ = nav; _ = services;
    }

    public int DecisionId { get; set; }
    public ObservableCollection<ContractDto> Contracts { get; } = new();

    public void SetDecisionId(int decisionId) => DecisionId = decisionId;

    public Task LoadAsync() => Task.CompletedTask;
}
