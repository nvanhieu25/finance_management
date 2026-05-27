using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.ViewModels.Contracts;

// Stub — full implementation in Task 7.
public partial class ContractEditDialogViewModel : ViewModelBase
{
    private readonly int _decisionId;
    public ContractEditDialogViewModel(ContractDto? initial, int decisionId)
    {
        _ = initial;
        _decisionId = decisionId;
    }
    public CreateContractDto ToCreateDto() =>
        new(_decisionId, null, "stub", "stub", new DateOnly(2026, 1, 1), null, 0m, null);
    public UpdateContractDto ToUpdateDto(int id) =>
        new(id, _decisionId, null, "stub", "stub", new DateOnly(2026, 1, 1), null, 0m, null);
}
