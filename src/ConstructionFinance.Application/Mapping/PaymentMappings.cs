using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Domain.Entities;

namespace ConstructionFinance.Application.Mapping;

public static class PaymentMappings
{
    public static PaymentDto ToDto(this Payment e) =>
        new(e.Id, e.CostItemId, e.PaymentNo, e.PaymentType, e.PaymentDate, e.Amount, e.Percentage, e.InvoiceNo, e.InvoiceDate, e.Description, e.Notes);
}
