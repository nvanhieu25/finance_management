using System.ComponentModel;
using ConstructionFinance.Application.Printing;

namespace ConstructionFinance.Desktop.ViewModels.Finance.Forms;

/// Một tab biểu mẫu trong dải tab Vị trí. Thay cho ba lớp ThongTri/KsChi/GdnttFormPageViewModel: tab tự đánh dấu
/// bẩn (bullet •) khi chữ của chính nó đổi, và khai nội dung in là tờ đã trộn đang hiện — cùng một tờ với màn
/// hình và nút "Xuất Excel", không dựng riêng cho bản in. Tên sheet là tên trong Mẫu.xlsx; độc lập với chữ trên tab.
public sealed class FormTabPageViewModel : WorkbookPageViewModel
{
    // PrintSheetName đã báo gần nhất, để chỉ báo khi tên thật sự đổi (xem OnInnerPropertyChanged).
    private string? _reportedPrintSheetName;

    public FormTabPageViewModel(FormPageViewModel inner)
    {
        Inner = inner;
        Title = inner.Definition.Title;
        Inner.InputsChanged += (_, _) => IsDirty = true;
        // Tên đơn vị lưu ở Cài đặt chứ không theo dự án, nhưng sửa nó cũng là một thay đổi chưa lưu của tab này.
        Inner.OrgEdited += (_, _) => IsDirty = true;

        _reportedPrintSheetName = PrintSheetName;
        Inner.PropertyChanged += OnInnerPropertyChanged;
    }

    public FormPageViewModel Inner { get; }
    public override object Content => Inner;

    // Tên theo CanExport chứ không cố định: PrintSheetName khác null luôn nghĩa là BuildPrintSheet() có sheet (hợp
    // đồng của IPrintablePage). GĐNTT chưa chọn gói thầu thì chưa có giấy để in.
    public override string? PrintSheetName => Inner.CanExport ? Inner.Definition.Template.SheetName : null;

    public override PrintSheet? BuildPrintSheet() =>
        Inner.CanExport
            ? new FormTemplatePrintSheet(Inner.Definition.Template.SheetName, Inner.Definition.PrintProfile, Inner.Sheet)
            : null;

    public override string? PrintUnavailableReason => Inner.CanExport ? null : Inner.PrintBlockReason;

    // CanExport đổi ngay trên tab này (người dùng tích/bỏ tích đợt) chứ không kèm lần đổi tab nào, nên phải báo cho
    // nơi đang nghe (nút In ở khung chung). Inner báo CanExport ở MỌI lần dựng, kể cả khi giá trị không đổi: chuyển tiếp
    // nguyên xi thì nút In cập nhật liên tục — nên chỉ báo khi tên thật sự khác lần trước.
    private void OnInnerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FormPageViewModel.CanExport)) return;

        var current = PrintSheetName;
        if (current == _reportedPrintSheetName) return;

        _reportedPrintSheetName = current;
        OnPropertyChanged(nameof(PrintSheetName));
        OnPropertyChanged(nameof(PrintUnavailableReason));
    }
}
