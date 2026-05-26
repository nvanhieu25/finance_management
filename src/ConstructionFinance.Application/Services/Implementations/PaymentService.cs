using ConstructionFinance.Application.Common.Exceptions;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Mapping;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Abstractions;
using ConstructionFinance.Domain.Entities;
using FluentValidation;

namespace ConstructionFinance.Application.Services.Implementations;

public class PaymentService : IPaymentService
{
    private readonly IRepository<Payment> _payments;
    private readonly IRepository<CostItem> _costItems;
    private readonly IUnitOfWork _uow;
    private readonly IValidator<Payment> _validator;

    public PaymentService(
        IRepository<Payment> payments,
        IRepository<CostItem> costItems,
        IUnitOfWork uow,
        IValidator<Payment> validator)
    {
        _payments = payments;
        _costItems = costItems;
        _uow = uow;
        _validator = validator;
    }

    public async Task<IReadOnlyList<PaymentDto>> GetByCostItemAsync(int costItemId, CancellationToken ct = default)
    {
        var list = await _payments.FindAsync(p => p.CostItemId == costItemId, ct);
        return list.OrderBy(p => p.PaymentNo).Select(p => p.ToDto()).ToList();
    }

    public async Task<PaymentDto> AddAsync(CreatePaymentDto dto, CancellationToken ct = default)
    {
        var costItem = await _costItems.GetByIdAsync(dto.CostItemId, ct)
            ?? throw new NotFoundException(nameof(CostItem), dto.CostItemId);

        var existing = await _payments.FindAsync(p => p.CostItemId == dto.CostItemId, ct);
        var nextNo = existing.Count == 0 ? 1 : existing.Max(p => p.PaymentNo) + 1;

        var percentage = dto.Percentage ?? ComputePercentage(dto.Amount, costItem.EstimatedAmount);

        var entity = new Payment
        {
            CostItemId = dto.CostItemId,
            PaymentNo = nextNo,
            PaymentType = dto.PaymentType,
            PaymentDate = dto.PaymentDate,
            Amount = dto.Amount,
            Percentage = percentage,
            InvoiceNo = dto.InvoiceNo,
            InvoiceDate = dto.InvoiceDate,
            Description = dto.Description,
            Notes = dto.Notes
        };

        await _validator.ValidateAndThrowAsync(entity, ct);
        await _payments.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<PaymentDto> UpdateAsync(UpdatePaymentDto dto, CancellationToken ct = default)
    {
        var entity = await _payments.GetByIdAsync(dto.Id, ct) ?? throw new NotFoundException(nameof(Payment), dto.Id);
        var costItem = await _costItems.GetByIdAsync(entity.CostItemId, ct)
            ?? throw new NotFoundException(nameof(CostItem), entity.CostItemId);

        entity.PaymentType = dto.PaymentType;
        entity.PaymentDate = dto.PaymentDate;
        entity.Amount = dto.Amount;
        entity.Percentage = dto.Percentage ?? ComputePercentage(dto.Amount, costItem.EstimatedAmount);
        entity.InvoiceNo = dto.InvoiceNo;
        entity.InvoiceDate = dto.InvoiceDate;
        entity.Description = dto.Description;
        entity.Notes = dto.Notes;

        await _validator.ValidateAndThrowAsync(entity, ct);
        _payments.Update(entity);
        await _uow.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _payments.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Payment), id);
        _payments.HardDelete(entity);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<PaymentSummaryDto> GetSummaryAsync(int costItemId, CancellationToken ct = default)
    {
        var costItem = await _costItems.GetByIdAsync(costItemId, ct)
            ?? throw new NotFoundException(nameof(CostItem), costItemId);
        var payments = await _payments.FindAsync(p => p.CostItemId == costItemId, ct);
        var totalPaid = payments.Sum(p => p.Amount);
        var remaining = costItem.EstimatedAmount - totalPaid;
        var pct = ComputePercentage(totalPaid, costItem.EstimatedAmount);
        return new PaymentSummaryDto(costItemId, costItem.EstimatedAmount, totalPaid, remaining, pct);
    }

    private static decimal ComputePercentage(decimal amount, decimal estimated) =>
        estimated > 0 ? Math.Round(amount * 100m / estimated, 2) : 0m;
}
