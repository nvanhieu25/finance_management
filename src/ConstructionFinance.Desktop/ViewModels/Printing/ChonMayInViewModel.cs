using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.Printing;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Services;

namespace ConstructionFinance.Desktop.ViewModels.Printing;

/// Hộp thoại "Chọn máy in". Danh sách máy in vào qua IPrinterCatalog nên test chạy được trên
/// máy không có máy in nào.
public sealed partial class ChonMayInViewModel : ViewModelBase, IHasDialogTitle, IHasPrimaryButtonText
{
    public ChonMayInViewModel(IPrinterCatalog catalog)
    {
        Printers = catalog.InstalledPrinters;

        // Máy in mặc định của hệ điều hành, nếu nó còn trong danh sách. Không còn (vừa bị xoá
        // giữa hai lần đọc) thì lấy máy đầu danh sách — hơn là để trống rồi bắt người dùng đoán.
        var defaultName = catalog.DefaultPrinterName;
        _selectedPrinter = defaultName is not null && Printers.Contains(defaultName)
            ? defaultName
            : Printers.FirstOrDefault();
    }

    public string DialogTitle => "Chọn máy in";

    public string PrimaryButtonText => "In";

    public IReadOnlyList<string> Printers { get; }

    /// Máy này chưa cài máy in nào thì không có gì để chọn. Lệnh In kiểm cờ này TRƯỚC khi mở
    /// hộp thoại, nên người dùng không bao giờ thấy một danh sách rỗng.
    public bool CoMayIn => Printers.Count > 0;

    [ObservableProperty]
    private string? _selectedPrinter;

    /// Bind cho NumericUpDown, kiểu decimal? đúng bằng kiểu của NumericUpDown.Value — KHÔNG
    /// đi qua chuỗi và KHÔNG StringFormat, vì bind hai chiều qua chuỗi trên kiểu số là chỗ
    /// dự án này đã trả giá một lần.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Copies))]
    private decimal? _copiesValue = PrinterJob.MinCopies;

    /// Số bản thật dùng để in: đã kẹp trong [1, 99] và đã về số nguyên. NumericUpDown có
    /// Minimum/Maximum, nhưng người dùng GÕ được chữ vào nó, nên không tin vào XAML.
    /// PrinterJob kẹp lần nữa — hai lần kẹp là có chủ ý.
    public int Copies
    {
        get
        {
            var value = CopiesValue ?? PrinterJob.MinCopies;
            var whole = decimal.Truncate(value);
            if (whole < PrinterJob.MinCopies) return PrinterJob.MinCopies;
            if (whole > PrinterJob.MaxCopies) return PrinterJob.MaxCopies;
            return (int)whole;
        }
    }
}
