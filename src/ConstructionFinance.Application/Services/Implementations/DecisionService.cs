using ConstructionFinance.Application.Common.Exceptions;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Mapping;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Entities;
using FluentValidation;

namespace ConstructionFinance.Application.Services.Implementations;

public class DecisionService : IDecisionService
{
    private readonly IRepository<ApprovalDecision> _decisions;
    private readonly IRepository<Contract> _contracts;
    private readonly IUnitOfWork _uow;
    private readonly IValidator<ApprovalDecision> _validator;

    public DecisionService(
        IRepository<ApprovalDecision> decisions,
        IRepository<Contract> contracts,
        IUnitOfWork uow,
        IValidator<ApprovalDecision> validator)
    {
        _decisions = decisions;
        _contracts = contracts;
        _uow = uow;
        _validator = validator;
    }

    public async Task<DecisionDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var e = await _decisions.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(ApprovalDecision), id);
        return e.ToDto();
    }

    public async Task<IReadOnlyList<DecisionDto>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _decisions.ListAsync(ct);
        return list.Select(x => x.ToDto()).ToList();
    }

    public async Task<DecisionDto> CreateAsync(CreateDecisionDto dto, CancellationToken ct = default)
    {
        var entity = dto.ToEntity();
        await _validator.ValidateAndThrowAsync(entity, ct);
        await _decisions.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<DecisionDto> UpdateAsync(UpdateDecisionDto dto, CancellationToken ct = default)
    {
        var entity = await _decisions.GetByIdAsync(dto.Id, ct) ?? throw new NotFoundException(nameof(ApprovalDecision), dto.Id);
        dto.Apply(entity);
        await _validator.ValidateAndThrowAsync(entity, ct);
        _decisions.Update(entity);
        await _uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _decisions.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(ApprovalDecision), id);
        if (await _contracts.ExistsAsync(c => c.ApprovalDecisionId == id, ct))
            throw new BusinessRuleException("Không thể xóa Quyết định khi còn Hợp đồng trực thuộc. Hãy xóa Hợp đồng trước.");
        await _decisions.SoftDeleteAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
    }
}
