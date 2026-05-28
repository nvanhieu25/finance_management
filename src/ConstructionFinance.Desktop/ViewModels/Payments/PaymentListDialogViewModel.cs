using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.ViewModels.Payments;

public partial class PaymentListDialogViewModel : ViewModelBase, IHasDialogTitle
{
    private readonly IPaymentService _service;
    private readonly IDialogService _dialog;

    public PaymentListDialogViewModel(IPaymentService service, IDialogService dialog)
    {
        _service = service;
        _dialog = dialog;
    }

    public int CostItemId { get; private set; }
    public string CostItemName { get; private set; } = "";
    public string DialogTitle => $"Đợt thanh toán — {CostItemName}";

    public ObservableCollection<PaymentDto> Payments { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private PaymentDto? _selectedPayment;

    [ObservableProperty] private PaymentSummaryDto _summary = new(0, 0m, 0m, 0m, 0m);
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public void SetCostItem(int costItemId, string costItemName)
    {
        CostItemId = costItemId;
        CostItemName = costItemName;
        OnPropertyChanged(nameof(DialogTitle));
    }

    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var list = await _service.GetByCostItemAsync(CostItemId);
            Payments.Clear();
            foreach (var p in list) Payments.Add(p);
            Summary = await _service.GetSummaryAsync(CostItemId);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var editVm = new PaymentEditDialogViewModel(initial: null, costItemId: CostItemId, costItemEstimatedAmount: Summary.EstimatedAmount);
        var ok = await _dialog.ShowDialogAsync(editVm);
        if (ok != true) return;
        try
        {
            var created = await _service.AddAsync(editVm.ToCreateDto());
            Payments.Add(created);
            Summary = await _service.GetSummaryAsync(CostItemId);
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync()
    {
        if (SelectedPayment is null) return;
        var editVm = new PaymentEditDialogViewModel(initial: SelectedPayment, costItemId: CostItemId, costItemEstimatedAmount: Summary.EstimatedAmount);
        var ok = await _dialog.ShowDialogAsync(editVm);
        if (ok != true) return;
        try
        {
            var updated = await _service.UpdateAsync(editVm.ToUpdateDto(SelectedPayment.Id));
            var idx = Payments.IndexOf(SelectedPayment);
            if (idx >= 0) Payments[idx] = updated;
            Summary = await _service.GetSummaryAsync(CostItemId);
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        if (SelectedPayment is null) return;
        var ok = await _dialog.ConfirmAsync("Xóa đợt thanh toán",
            $"Xóa đợt #{SelectedPayment.PaymentNo} ({SelectedPayment.Amount:#,##0}đ)?");
        if (!ok) return;
        try
        {
            await _service.DeleteAsync(SelectedPayment.Id);
            Payments.Remove(SelectedPayment);
            Summary = await _service.GetSummaryAsync(CostItemId);
        }
        catch (Exception ex) { await _dialog.ShowMessageAsync("Lỗi", ex.Message); }
    }

    private bool HasSelection() => SelectedPayment is not null;
}
