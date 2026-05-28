using ConstructionFinance.Application.Common.Exceptions;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Mapping;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Entities;
using ConstructionFinance.Domain.Enums;
using FluentValidation;

namespace ConstructionFinance.Application.Services.Implementations;

public class ProjectService : IProjectService
{
    private static readonly Dictionary<ProjectStatus, ProjectStatus[]> AllowedTransitions = new()
    {
        [ProjectStatus.Planning] = new[] { ProjectStatus.InProgress, ProjectStatus.Cancelled },
        [ProjectStatus.InProgress] = new[] { ProjectStatus.OnHold, ProjectStatus.Completed, ProjectStatus.Cancelled },
        [ProjectStatus.OnHold] = new[] { ProjectStatus.InProgress },
        [ProjectStatus.Completed] = Array.Empty<ProjectStatus>(),
        [ProjectStatus.Cancelled] = Array.Empty<ProjectStatus>()
    };

    private readonly IRepository<Project> _projects;
    private readonly IRepository<Contract> _contracts;
    private readonly IRepository<CostItem> _costItems;
    private readonly IUnitOfWork _uow;
    private readonly IValidator<Project> _validator;

    public ProjectService(
        IRepository<Project> projects,
        IRepository<Contract> contracts,
        IRepository<CostItem> costItems,
        IUnitOfWork uow,
        IValidator<Project> validator)
    {
        _projects = projects;
        _contracts = contracts;
        _costItems = costItems;
        _uow = uow;
        _validator = validator;
    }

    public async Task<ProjectDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var e = await _projects.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Project), id);
        return e.ToDto();
    }

    public async Task<IReadOnlyList<ProjectDto>> GetByContractAsync(int contractId, CancellationToken ct = default)
    {
        var list = await _projects.FindAsync(p => p.ContractId == contractId, ct);
        return list.Select(x => x.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<ProjectDto>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _projects.ListAsync(ct);
        return list.Select(x => x.ToDto()).ToList();
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectDto dto, CancellationToken ct = default)
    {
        if (await _contracts.GetByIdAsync(dto.ContractId, ct) is null)
            throw new NotFoundException(nameof(Contract), dto.ContractId);

        var entity = dto.ToEntity();
        await _validator.ValidateAndThrowAsync(entity, ct);
        await _projects.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<ProjectDto> UpdateAsync(UpdateProjectDto dto, CancellationToken ct = default)
    {
        var entity = await _projects.GetByIdAsync(dto.Id, ct) ?? throw new NotFoundException(nameof(Project), dto.Id);
        dto.Apply(entity);
        await _validator.ValidateAndThrowAsync(entity, ct);
        _projects.Update(entity);
        await _uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<ProjectDto> UpdateStatusAsync(int id, ProjectStatus newStatus, CancellationToken ct = default)
    {
        var entity = await _projects.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Project), id);
        if (entity.Status == newStatus)
            return entity.ToDto();
        if (!AllowedTransitions[entity.Status].Contains(newStatus))
            throw new BusinessRuleException($"Không thể chuyển trạng thái từ {entity.Status} sang {newStatus}.");

        entity.Status = newStatus;
        _projects.Update(entity);
        await _uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _projects.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Project), id);
        if (await _costItems.ExistsAsync(c => c.ProjectId == id, ct))
            throw new BusinessRuleException("Không thể xóa Dự án khi còn Đầu mục chi phí. Hãy xóa cây chi phí trước.");
        await _projects.SoftDeleteAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
    }
}
