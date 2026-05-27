using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.ViewModels.Projects;

// Stub — full implementation in Task 10.
public partial class ProjectEditDialogViewModel : ViewModelBase
{
    private readonly int _contractId;
    public ProjectEditDialogViewModel(ProjectDto? initial, int contractId)
    {
        _ = initial;
        _contractId = contractId;
    }
    public CreateProjectDto ToCreateDto() =>
        new(_contractId, "stub", "stub", null, null, null, null, 0m, null);
    public UpdateProjectDto ToUpdateDto(int id) =>
        new(id, _contractId, "stub", "stub", null, null, null, null, 0m, null);
}
