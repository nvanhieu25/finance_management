using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.ViewModels.Common;

public partial class ConfirmDialogViewModel : ViewModelBase
{
    [ObservableProperty] private string _title = "Xác nhận";
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private string _confirmText = "Đồng ý";
    [ObservableProperty] private string _cancelText = "Hủy";
}
