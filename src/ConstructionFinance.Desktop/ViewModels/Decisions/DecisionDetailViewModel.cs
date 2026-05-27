using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.ViewModels.Decisions;

// Stub — full implementation in Task 4.
public partial class DecisionDetailViewModel : ViewModelBase
{
    public DecisionDetailViewModel(DecisionDto decision) { Decision = decision; }
    public DecisionDto Decision { get; }
}
