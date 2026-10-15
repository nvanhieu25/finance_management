using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Desktop.ViewModels.Finance.Forms;

/// Một dòng trong panel chọn đợt: dữ liệu đợt đã trải phẳng + ô tích + các chuỗi
/// hiển thị (không bind thẳng decimal/DateOnly - vì Avalonia chặn cú OnPropertyChanged cùng-thuộc-tính).
public partial class FormDotItemViewModel : ViewModelBase
{
    public FormDotItemViewModel(FormDotRow data, object token)
    {
        Data = data;
        Token = token;
    }

    public FormDotRow Data { get; }

    /// Danh tính bền của dòng qua các lần Refresh: là chính đối tượng dòng lưới khi
    /// có (thêm/xóa đợt chỗ khác không làm trượt lựa chọn), nếu không thì là khóa.
    public object Token { get; }

    [ObservableProperty] private bool _isSelected;

    public string DateText => Data.Date.ToString("dd/MM/yyyy");
    public string ViTriName => Data.ViTriName;
    public string PackageName => Data.PackageName;
    public string ContentText => Data.Content ?? "";
    public string FundText => Data.Fund == Fund.NSNN ? "Dự phòng NSNN" : "Quỹ dự trữ ngoại hối";
    public string AdvanceText => MoneyText.Format(Data.Advance);
    public string KlhtText => MoneyText.Format(Data.PaymentKLHT);
    public string RecoveredText => MoneyText.Format(Data.RecoveredAdvance);
    public string DisbursedText => MoneyText.Format(Data.Disbursed);
}
