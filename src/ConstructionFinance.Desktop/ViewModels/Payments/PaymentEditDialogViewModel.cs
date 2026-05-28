using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Domain.Enums;

namespace ConstructionFinance.Desktop.ViewModels.Payments;

public partial class PaymentEditDialogViewModel : ViewModelBase, IHasDialogTitle
{
    public bool IsEditMode { get; }
    public int CostItemId { get; }
    public decimal CostItemEstimatedAmount { get; }
    public string DialogTitle => IsEditMode ? "Sửa đợt thanh toán" : "Thêm đợt thanh toán";

    public IReadOnlyList<PaymentType> AvailableTypes { get; } = Enum.GetValues<PaymentType>().ToList();

    [ObservableProperty] private PaymentType _paymentType = PaymentType.Installment;
    [ObservableProperty] private DateOnly _paymentDate = DateOnly.FromDateTime(DateTime.Today);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AmountError), nameof(IsValid))]
    private decimal _amount;

    [ObservableProperty] private decimal? _percentage;
    [ObservableProperty] private string? _invoiceNo;
    [ObservableProperty] private DateOnly? _invoiceDate;
    [ObservableProperty] private string? _description;
    [ObservableProperty] private string? _notes;

    public PaymentEditDialogViewModel(PaymentDto? initial, int costItemId, decimal costItemEstimatedAmount)
    {
        CostItemId = costItemId;
        CostItemEstimatedAmount = costItemEstimatedAmount;
        IsEditMode = initial is not null;
        if (initial is not null)
        {
            PaymentType = initial.PaymentType;
            PaymentDate = initial.PaymentDate;
            Amount = initial.Amount;
            Percentage = initial.Percentage;
            InvoiceNo = initial.InvoiceNo;
            InvoiceDate = initial.InvoiceDate;
            Description = initial.Description;
            Notes = initial.Notes;
        }
    }

    public string? AmountError => Amount <= 0 ? "Số tiền phải lớn hơn 0." : null;

    public bool IsValid => AmountError is null;

    public CreatePaymentDto ToCreateDto() =>
        new(CostItemId, PaymentType, PaymentDate, Amount, Percentage, InvoiceNo, InvoiceDate, Description, Notes);

    public UpdatePaymentDto ToUpdateDto(int id) =>
        new(id, PaymentType, PaymentDate, Amount, Percentage, InvoiceNo, InvoiceDate, Description, Notes);
}
