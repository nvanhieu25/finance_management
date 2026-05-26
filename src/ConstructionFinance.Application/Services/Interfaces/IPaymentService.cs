using ConstructionFinance.Application.DTOs;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IPaymentService
{
    Task<IReadOnlyList<PaymentDto>> GetByCostItemAsync(int costItemId, CancellationToken ct = default);
    Task<PaymentDto> AddAsync(CreatePaymentDto dto, CancellationToken ct = default);
    Task<PaymentDto> UpdateAsync(UpdatePaymentDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<PaymentSummaryDto> GetSummaryAsync(int costItemId, CancellationToken ct = default);
}
