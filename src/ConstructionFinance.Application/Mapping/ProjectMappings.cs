using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Application.Mapping;

public static class ProjectMappings
{
    public static ProjectDto ToDto(this Project e) =>
        new(e.Id, e.ContractId, e.ProjectCode, e.ProjectName, e.ProjectType, e.Location, e.StartDate, e.EndDate, e.Status, e.TotalBudget, e.TotalActual, e.Notes);

    public static Project ToEntity(this CreateProjectDto d) =>
        new()
        {
            ContractId = d.ContractId,
            ProjectCode = d.ProjectCode,
            ProjectName = d.ProjectName,
            ProjectType = d.ProjectType,
            Location = d.Location,
            StartDate = d.StartDate,
            EndDate = d.EndDate,
            Status = ProjectStatus.Planning,
            TotalBudget = d.TotalBudget,
            Notes = d.Notes
        };

    public static void Apply(this UpdateProjectDto d, Project e)
    {
        e.ContractId = d.ContractId;
        e.ProjectCode = d.ProjectCode;
        e.ProjectName = d.ProjectName;
        e.ProjectType = d.ProjectType;
        e.Location = d.Location;
        e.StartDate = d.StartDate;
        e.EndDate = d.EndDate;
        e.TotalBudget = d.TotalBudget;
        e.Notes = d.Notes;
    }
}
