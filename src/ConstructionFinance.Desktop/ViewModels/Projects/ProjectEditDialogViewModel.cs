using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.ViewModels.Projects;

public partial class ProjectEditDialogViewModel : ViewModelBase, IHasDialogTitle
{
    public bool IsEditMode { get; }
    public int ContractId { get; }
    public string DialogTitle => IsEditMode ? "Sửa Dự án" : "Thêm Dự án";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProjectCodeError), nameof(IsValid))]
    private string _projectCode = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProjectNameError), nameof(IsValid))]
    private string _projectName = "";

    [ObservableProperty] private string? _projectType;
    [ObservableProperty] private string? _location;
    [ObservableProperty] private DateOnly? _startDate;
    [ObservableProperty] private DateOnly? _endDate;
    [ObservableProperty] private decimal _totalBudget;
    [ObservableProperty] private string? _notes;

    public ProjectEditDialogViewModel(ProjectDto? initial, int contractId)
    {
        ContractId = contractId;
        IsEditMode = initial is not null;
        if (initial is not null)
        {
            ProjectCode = initial.ProjectCode;
            ProjectName = initial.ProjectName;
            ProjectType = initial.ProjectType;
            Location = initial.Location;
            StartDate = initial.StartDate;
            EndDate = initial.EndDate;
            TotalBudget = initial.TotalBudget;
            Notes = initial.Notes;
        }
    }

    public string? ProjectCodeError =>
        string.IsNullOrWhiteSpace(ProjectCode) ? "Mã Dự án là bắt buộc." : null;

    public string? ProjectNameError =>
        string.IsNullOrWhiteSpace(ProjectName) ? "Tên Dự án là bắt buộc." : null;

    public bool IsValid => ProjectCodeError is null && ProjectNameError is null;

    public CreateProjectDto ToCreateDto() =>
        new(ContractId, ProjectCode, ProjectName, ProjectType, Location, StartDate, EndDate, TotalBudget, Notes);

    public UpdateProjectDto ToUpdateDto(int id) =>
        new(id, ContractId, ProjectCode, ProjectName, ProjectType, Location, StartDate, EndDate, TotalBudget, Notes);
}
