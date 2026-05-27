using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.ViewModels.Contracts;

public partial class ContractEditDialogViewModel : ViewModelBase, IHasDialogTitle
{
    public bool IsEditMode { get; }
    public int DecisionId { get; }
    public string DialogTitle => IsEditMode ? "Sửa Hợp đồng" : "Thêm Hợp đồng";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContractCodeError), nameof(IsValid))]
    private string _contractCode = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContractNameError), nameof(IsValid))]
    private string _contractName = "";

    [ObservableProperty] private DateOnly _contractDate = DateOnly.FromDateTime(DateTime.Today);
    [ObservableProperty] private DateOnly? _signingDate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContractValueError), nameof(IsValid))]
    private decimal _contractValue;

    [ObservableProperty] private int? _contractorVendorId;
    [ObservableProperty] private string? _notes;

    public ContractEditDialogViewModel(ContractDto? initial, int decisionId)
    {
        DecisionId = decisionId;
        IsEditMode = initial is not null;
        if (initial is not null)
        {
            ContractCode = initial.ContractCode;
            ContractName = initial.ContractName;
            ContractDate = initial.ContractDate;
            SigningDate = initial.SigningDate;
            ContractValue = initial.ContractValue;
            ContractorVendorId = initial.ContractorVendorId;
            Notes = initial.Notes;
        }
    }

    public string? ContractCodeError =>
        string.IsNullOrWhiteSpace(ContractCode) ? "Mã Hợp đồng là bắt buộc." : null;

    public string? ContractNameError =>
        string.IsNullOrWhiteSpace(ContractName) ? "Tên Hợp đồng là bắt buộc." : null;

    public string? ContractValueError =>
        ContractValue < 0 ? "Giá trị Hợp đồng không thể âm." : null;

    public bool IsValid =>
        ContractCodeError is null && ContractNameError is null && ContractValueError is null;

    public CreateContractDto ToCreateDto() =>
        new(DecisionId, ContractorVendorId, ContractCode, ContractName, ContractDate, SigningDate, ContractValue, Notes);

    public UpdateContractDto ToUpdateDto(int id) =>
        new(id, DecisionId, ContractorVendorId, ContractCode, ContractName, ContractDate, SigningDate, ContractValue, Notes);
}
