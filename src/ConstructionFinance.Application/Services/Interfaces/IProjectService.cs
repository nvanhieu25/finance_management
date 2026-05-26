using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IProjectService
{
    Task<ProjectDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectDto>> GetByContractAsync(int contractId, CancellationToken ct = default);
    Task<ProjectDto> CreateAsync(CreateProjectDto dto, CancellationToken ct = default);
    Task<ProjectDto> UpdateAsync(UpdateProjectDto dto, CancellationToken ct = default);
    Task<ProjectDto> UpdateStatusAsync(int id, ProjectStatus newStatus, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
