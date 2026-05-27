using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.ViewModels.Contracts;

// Stub — full implementation in Task 8.
public partial class ContractDetailViewModel : ViewModelBase
{
    public ContractDetailViewModel(ContractDto contract) { Contract = contract; }
    public ContractDto Contract { get; }
}
