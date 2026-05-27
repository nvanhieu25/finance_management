using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.ViewModels.Projects;

// Stub — full implementation in Task 11.
public partial class ProjectDetailViewModel : ViewModelBase
{
    public ProjectDetailViewModel(ProjectDto project) { Project = project; }
    public ProjectDto Project { get; }
}
