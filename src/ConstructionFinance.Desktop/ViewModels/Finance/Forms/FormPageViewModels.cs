using System.ComponentModel;
using ConstructionFinance.Application.Printing;

namespace ConstructionFinance.Desktop.ViewModels.Finance.Forms;

/// Ba tab biểu mẫu đứng cuối dải tab Vị trí. Mỗi tab tự đánh dấu bẩn (bullet •) khi
/// ô nhập tay của chính nó đổi qua sự kiện DeclarationChanged; workbook lưu chung
/// cũng theo dõi cùng sự kiện đó để bật IsDirty tổng (xem OnFormDeclarationChanged).
///
/// Cả ba cũng khai báo nội dung in: một PrintFormSheet bọc đúng model biểu mẫu mà view-model bên trong
/// dựng (BuildForm) — cùng model với bản xem trước và nút "Xuất Excel", không dựng riêng cho bản in.
/// Tên sheet theo Mẫu.xlsx; cùng chữ với tiêu đề tab nhưng là hai thứ độc lập: đổi chữ trên tab không
/// được làm đổi tên sheet trong bản in.
public sealed class ThongTriFormPageViewModel : WorkbookPageViewModel
{
    private const string SheetName = "Thông tri";

    public ThongTriFormPageViewModel(ThongTriFormViewModel inner)
    {
        Inner = inner;
        Title = "Thông tri";
        Inner.DeclarationChanged += (_, _) => IsDirty = true;
    }

    public ThongTriFormViewModel Inner { get; }
    public override object Content => Inner;

    public override string? PrintSheetName => SheetName;

    public override PrintSheet? BuildPrintSheet() => new PrintFormSheet(
        SheetName,
        PrintPageProfiles.ThongTri,
        new FormPayload(ThongTri: Inner.BuildForm()));
}

public sealed class KsChiFormPageViewModel : WorkbookPageViewModel
{
    private const string SheetName = "KS chi";

    public KsChiFormPageViewModel(KsChiFormViewModel inner)
    {
        Inner = inner;
        Title = "KS chi";
        Inner.DeclarationChanged += (_, _) => IsDirty = true;
    }

    public KsChiFormViewModel Inner { get; }
    public override object Content => Inner;

    public override string? PrintSheetName => SheetName;

    public override PrintSheet? BuildPrintSheet() => new PrintFormSheet(
        SheetName,
        PrintPageProfiles.KsChi,
        new FormPayload(KsChi: Inner.BuildForm()));
}

public sealed class GdnttFormPageViewModel : WorkbookPageViewModel
{
    private const string SheetName = "GĐNTT";

    // PrintSheetName đã báo gần nhất, để chỉ báo khi tên thật sự đổi (xem OnInnerPropertyChanged).
    private string? _reportedPrintSheetName;

    public GdnttFormPageViewModel(GdnttFormViewModel inner)
    {
        Inner = inner;
        Title = "GĐNTT";
        Inner.DeclarationChanged += (_, _) => IsDirty = true;

        _reportedPrintSheetName = PrintSheetName;
        Inner.PropertyChanged += OnInnerPropertyChanged;
    }

    public GdnttFormViewModel Inner { get; }
    public override object Content => Inner;

    // Chưa chọn gói thầu (hoặc chọn đợt của nhiều gói) thì chưa có giấy để in — đúng lúc nút "Xuất Excel" mờ.
    // Tên theo CanExport chứ không cố định, để PrintSheetName khác null luôn nghĩa là BuildPrintSheet() có sheet.
    public override string? PrintSheetName => Inner.CanExport ? SheetName : null;

    public override PrintSheet? BuildPrintSheet() =>
        Inner.BuildForm() is { } form
            ? new PrintFormSheet(SheetName, PrintPageProfiles.Gdntt, new FormPayload(Gdntt: form))
            : null;

    // Tên sheet đổi theo CanExport, và CanExport đổi ngay trên tab này (người dùng tích/bỏ tích đợt) chứ không kèm lần
    // đổi tab nào, nên phải báo cho nơi đang nghe — nút In ở khung chung. GdnttFormViewModel.Rebuild() bắn CanExport
    // ở MỌI lần dựng (mỗi lần tích, mỗi lần gõ một ô), kể cả khi giá trị không đổi: chuyển tiếp nguyên xi thì nút In
    // cập nhật liên tục. Nên chỉ báo khi tên thật sự khác lần báo trước.
    private void OnInnerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(GdnttFormViewModel.CanExport)) return;

        var current = PrintSheetName;
        if (current == _reportedPrintSheetName) return;

        _reportedPrintSheetName = current;
        OnPropertyChanged(nameof(PrintSheetName));
    }
}
