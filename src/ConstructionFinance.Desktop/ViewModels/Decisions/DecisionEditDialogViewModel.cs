using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.ViewModels.Decisions;

public partial class DecisionEditDialogViewModel : ViewModelBase, IHasDialogTitle
{
    public bool IsEditMode { get; }
    public string DialogTitle => IsEditMode ? "Sửa Quyết định phê duyệt" : "Thêm Quyết định phê duyệt";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DecisionCodeError), nameof(IsValid))]
    private string _decisionCode = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DecisionTitleError), nameof(IsValid))]
    private string _decisionTitle = "";

    [ObservableProperty] private DateOnly _decisionDate = DateOnly.FromDateTime(DateTime.Today);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IssuingAuthorityError), nameof(IsValid))]
    private string _issuingAuthority = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalApprovedAmountError), nameof(IsValid))]
    private decimal _totalApprovedAmount;

    [ObservableProperty] private string? _notes;

    public DecisionEditDialogViewModel(DecisionDto? initial)
    {
        IsEditMode = initial is not null;
        if (initial is not null)
        {
            DecisionCode = initial.DecisionCode;
            DecisionTitle = initial.DecisionTitle;
            DecisionDate = initial.DecisionDate;
            IssuingAuthority = initial.IssuingAuthority;
            TotalApprovedAmount = initial.TotalApprovedAmount;
            Notes = initial.Notes;
        }
    }

    public string? DecisionCodeError =>
        string.IsNullOrWhiteSpace(DecisionCode) ? "Mã Quyết định là bắt buộc." : null;

    public string? DecisionTitleError =>
        string.IsNullOrWhiteSpace(DecisionTitle) ? "Trích yếu là bắt buộc." : null;

    public string? IssuingAuthorityError =>
        string.IsNullOrWhiteSpace(IssuingAuthority) ? "Cơ quan ban hành là bắt buộc." : null;

    public string? TotalApprovedAmountError =>
        TotalApprovedAmount < 0 ? "Tổng giá trị không thể âm." : null;

    public bool IsValid =>
        DecisionCodeError is null &&
        DecisionTitleError is null &&
        IssuingAuthorityError is null &&
        TotalApprovedAmountError is null;

    public CreateDecisionDto ToCreateDto() =>
        new(DecisionCode, DecisionTitle, DecisionDate, IssuingAuthority, TotalApprovedAmount, Notes);

    public UpdateDecisionDto ToUpdateDto(int id) =>
        new(id, DecisionCode, DecisionTitle, DecisionDate, IssuingAuthority, TotalApprovedAmount, Notes);
}
