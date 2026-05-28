using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Desktop.ViewModels.Payments;

// Stub — full implementation in Task 8.
public partial class PaymentEditDialogViewModel : ViewModelBase
{
    private readonly int _costItemId;

    public PaymentEditDialogViewModel(PaymentDto? initial, int costItemId, decimal costItemEstimatedAmount)
    {
        _ = initial; _ = costItemEstimatedAmount;
        _costItemId = costItemId;
    }

    public CreatePaymentDto ToCreateDto() =>
        new(_costItemId, PaymentType.Installment, DateOnly.FromDateTime(DateTime.Today), 0m, null, null, null, null, null);

    public UpdatePaymentDto ToUpdateDto(int id) =>
        new(id, PaymentType.Installment, DateOnly.FromDateTime(DateTime.Today), 0m, null, null, null, null, null);
}
