using ConstructionFinance.Application.Common.Exceptions;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Mapping;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Entities;
using FluentValidation;

namespace ConstructionFinance.Application.Services.Implementations;

public class ContractService : IContractService
{
    private readonly IRepository<Contract> _contracts;
    private readonly IRepository<ApprovalDecision> _decisions;
    private readonly IRepository<Project> _projects;
    private readonly IUnitOfWork _uow;
    private readonly IValidator<Contract> _validator;

    public ContractService(
        IRepository<Contract> contracts,
        IRepository<ApprovalDecision> decisions,
        IRepository<Project> projects,
        IUnitOfWork uow,
        IValidator<Contract> validator)
    {
        _contracts = contracts;
        _decisions = decisions;
        _projects = projects;
        _uow = uow;
        _validator = validator;
    }

    public async Task<ContractDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var e = await _contracts.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Contract), id);
        return e.ToDto();
    }

    public async Task<IReadOnlyList<ContractDto>> GetByDecisionAsync(int decisionId, CancellationToken ct = default)
    {
        var list = await _contracts.FindAsync(c => c.ApprovalDecisionId == decisionId, ct);
        return list.Select(x => x.ToDto()).ToList();
    }

    public async Task<ContractDto> CreateAsync(CreateContractDto dto, CancellationToken ct = default)
    {
        if (await _decisions.GetByIdAsync(dto.ApprovalDecisionId, ct) is null)
            throw new NotFoundException(nameof(ApprovalDecision), dto.ApprovalDecisionId);

        var entity = dto.ToEntity();
        await _validator.ValidateAndThrowAsync(entity, ct);
        await _contracts.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<ContractDto> UpdateAsync(UpdateContractDto dto, CancellationToken ct = default)
    {
        var entity = await _contracts.GetByIdAsync(dto.Id, ct) ?? throw new NotFoundException(nameof(Contract), dto.Id);
        dto.Apply(entity);
        await _validator.ValidateAndThrowAsync(entity, ct);
        _contracts.Update(entity);
        await _uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _contracts.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Contract), id);
        if (await _projects.ExistsAsync(p => p.ContractId == id, ct))
            throw new BusinessRuleException("Không thể xóa Hợp đồng khi còn Dự án trực thuộc. Hãy xóa Dự án trước.");
        await _contracts.SoftDeleteAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
    }
}
