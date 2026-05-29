using ConstructionFinance.Application.Common.Exceptions;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Mapping;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Entities;
using FluentValidation;

namespace ConstructionFinance.Application.Services.Implementations;

public class CostService : ICostService
{
    private readonly ICostRepository _costs;
    private readonly IRepository<Payment> _payments;
    private readonly IUnitOfWork _uow;
    private readonly IValidator<CostItem> _validator;

    public CostService(
        ICostRepository costs,
        IRepository<Payment> payments,
        IUnitOfWork uow,
        IValidator<CostItem> validator)
    {
        _costs = costs;
        _payments = payments;
        _uow = uow;
        _validator = validator;
    }

    public async Task<IReadOnlyList<CostItemDto>> GetProjectTreeAsync(int projectId, CancellationToken ct = default)
    {
        var list = await _costs.GetTreeByProjectAsync(projectId, ct);
        return list.Select(x => x.ToDto()).ToList();
    }

    public async Task<CostItemDto> CreateNodeAsync(CreateCostItemDto dto, CancellationToken ct = default)
    {
        var node = new CostItem
        {
            ProjectId = dto.ProjectId,
            ItemName = dto.ItemName,
            IsLeaf = dto.IsLeaf,
            UnitTypeId = dto.UnitTypeId,
            VendorId = dto.VendorId,
            Quantity = dto.Quantity,
            UnitPrice = dto.UnitPrice,
            EstimatedAmount = ComputeLeafEstimate(dto.IsLeaf, dto.Quantity, dto.UnitPrice, dto.EstimatedAmount),
            BidPackage = dto.BidPackage,
            Notes = dto.Notes,
            Path = "/",
            Level = 0
        };

        // Validate BEFORE persisting. Path/Level here are placeholders that satisfy the
        // validator (Path NotEmpty, Level >= 0); AddNodeAsync overwrites them with the
        // real materialized path/level after the Id is assigned.
        await _validator.ValidateAndThrowAsync(node, ct);

        var saved = await _costs.AddNodeAsync(dto.ProjectId, dto.ParentId, node, ct);
        await _uow.SaveChangesAsync(ct);
        return saved.ToDto();
    }

    public async Task<CostItemDto> UpdateNodeAsync(UpdateCostItemDto dto, CancellationToken ct = default)
    {
        var entity = await _costs.GetByIdAsync(dto.Id, ct) ?? throw new NotFoundException(nameof(CostItem), dto.Id);

        entity.ItemName = dto.ItemName;
        entity.IsLeaf = dto.IsLeaf;
        entity.UnitTypeId = dto.UnitTypeId;
        entity.VendorId = dto.VendorId;
        entity.Quantity = dto.Quantity;
        entity.UnitPrice = dto.UnitPrice;
        entity.SortOrder = dto.SortOrder;
        entity.BidPackage = dto.BidPackage;
        entity.Notes = dto.Notes;
        entity.EstimatedAmount = ComputeLeafEstimate(dto.IsLeaf, dto.Quantity, dto.UnitPrice, dto.EstimatedAmount);

        await _validator.ValidateAndThrowAsync(entity, ct);
        _costs.Update(entity);
        await _uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task RecalculateRollupAsync(int projectId, CancellationToken ct = default)
    {
        var all = (await _costs.GetTrackedTreeByProjectAsync(projectId, ct)).ToList();
        var childSums = new Dictionary<int, decimal>();

        // Process deepest first so child sums are ready when we reach the parent.
        foreach (var node in all.OrderByDescending(x => x.Level))
        {
            decimal amount;
            if (node.IsLeaf)
            {
                amount = ComputeLeafEstimate(true, node.Quantity, node.UnitPrice, node.EstimatedAmount);
            }
            else if (childSums.TryGetValue(node.Id, out var s))
            {
                amount = s;
            }
            else
            {
                // Non-leaf without children — treat like leaf to preserve user input.
                amount = ComputeLeafEstimate(true, node.Quantity, node.UnitPrice, node.EstimatedAmount);
            }

            if (node.EstimatedAmount != amount)
            {
                node.EstimatedAmount = amount;
                _costs.Update(node);
            }

            if (node.ParentId is int parentId)
            {
                childSums[parentId] = (childSums.TryGetValue(parentId, out var existing) ? existing : 0m) + amount;
            }
        }

        await _uow.SaveChangesAsync(ct);
    }

    public async Task MoveNodeAsync(int nodeId, int? newParentId, CancellationToken ct = default)
    {
        var node = await _costs.GetByIdAsync(nodeId, ct) ?? throw new NotFoundException(nameof(CostItem), nodeId);

        string newParentPath;
        int newParentLevel;
        if (newParentId is null)
        {
            newParentPath = "/";
            newParentLevel = -1;
        }
        else
        {
            var newParent = await _costs.GetByIdAsync(newParentId.Value, ct)
                ?? throw new NotFoundException(nameof(CostItem), newParentId.Value);
            // Cannot move a node into itself or one of its descendants.
            if (newParent.Path.StartsWith(node.Path, StringComparison.Ordinal))
                throw new BusinessRuleException("Không thể di chuyển một đầu mục vào chính nó hoặc nhánh con của nó.");
            newParentPath = newParent.Path;
            newParentLevel = newParent.Level;
        }

        var subtree = (await _costs.GetSubtreeAsync(nodeId, ct)).ToList();
        var oldPrefix = node.Path;
        var newPrefix = $"{newParentPath.TrimEnd('/')}/{node.Id}/";
        var levelDelta = (newParentLevel + 1) - node.Level;

        foreach (var descendant in subtree)
        {
            var tracked = await _costs.GetByIdAsync(descendant.Id, ct);
            if (tracked is null) continue;
            tracked.Path = newPrefix + tracked.Path.Substring(oldPrefix.Length);
            tracked.Level += levelDelta;
            if (tracked.Id == nodeId)
                tracked.ParentId = newParentId;
            _costs.Update(tracked);
        }

        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeleteNodeAsync(int nodeId, CancellationToken ct = default)
    {
        _ = await _costs.GetByIdAsync(nodeId, ct) ?? throw new NotFoundException(nameof(CostItem), nodeId);
        var subtree = (await _costs.GetSubtreeAsync(nodeId, ct)).ToList();
        var subtreeIds = subtree.Select(x => x.Id).ToHashSet();

        if (await _payments.ExistsAsync(p => subtreeIds.Contains(p.CostItemId), ct))
            throw new BusinessRuleException("Không thể xóa: có đầu mục trong nhánh này đã có đợt thanh toán. Hãy xóa các đợt thanh toán trước.");

        foreach (var item in subtree)
        {
            var tracked = await _costs.GetByIdAsync(item.Id, ct);
            if (tracked is not null)
                await _costs.SoftDeleteAsync(tracked, ct);
        }

        await _uow.SaveChangesAsync(ct);
    }

    private static decimal ComputeLeafEstimate(bool isLeaf, decimal qty, decimal price, decimal manual)
    {
        if (isLeaf && qty > 0 && price > 0)
            return qty * price;
        return manual;
    }
}
