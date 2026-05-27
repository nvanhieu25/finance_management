using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.ViewModels.Decisions;

// Stub — full implementation in Task 3.
public partial class DecisionEditDialogViewModel : ViewModelBase
{
    public DecisionEditDialogViewModel(DecisionDto? initial) { }
    public CreateDecisionDto ToCreateDto() =>
        new("stub", "stub", new DateOnly(2026, 1, 1), "stub", 0m, null);
    public UpdateDecisionDto ToUpdateDto(int id) =>
        new(id, "stub", "stub", new DateOnly(2026, 1, 1), "stub", 0m, null);
}
