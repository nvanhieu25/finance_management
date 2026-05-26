using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.ViewModels.Vendors;

public partial class VendorEditDialogViewModel : ViewModelBase, IHasDialogTitle
{
    public bool IsEditMode { get; }
    public string DialogTitle => IsEditMode ? "Sửa nhà cung cấp" : "Thêm nhà cung cấp";

    [ObservableProperty] private string? _vendorCode;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VendorNameError), nameof(IsValid))]
    private string _vendorName = "";
    [ObservableProperty] private string? _taxCode;
    [ObservableProperty] private string? _address;
    [ObservableProperty] private string? _phone;
    [ObservableProperty] private string? _email;
    [ObservableProperty] private string? _contactPerson;
    [ObservableProperty] private string? _notes;

    public VendorEditDialogViewModel(VendorDto? initial)
    {
        IsEditMode = initial is not null;
        if (initial is not null)
        {
            VendorCode = initial.VendorCode;
            VendorName = initial.VendorName;
            TaxCode = initial.TaxCode;
            Address = initial.Address;
            Phone = initial.Phone;
            Email = initial.Email;
            ContactPerson = initial.ContactPerson;
            Notes = initial.Notes;
        }
    }

    public string? VendorNameError =>
        string.IsNullOrWhiteSpace(VendorName) ? "Tên nhà cung cấp là bắt buộc." : null;

    public bool IsValid => string.IsNullOrEmpty(VendorNameError);

    public CreateVendorDto ToCreateDto() =>
        new(VendorCode, VendorName, TaxCode, Address, Phone, Email, ContactPerson, Notes);

    public UpdateVendorDto ToUpdateDto(int id) =>
        new(id, VendorCode, VendorName, TaxCode, Address, Phone, Email, ContactPerson, Notes);
}
