namespace ConstructionFinance.Desktop.ViewModels.Finance.Forms;

/// Ba tab biểu mẫu đứng cuối dải tab Vị trí. Mỗi tab tự đánh dấu bẩn (bullet •) khi
/// ô nhập tay của chính nó đổi qua sự kiện DeclarationChanged; workbook lưu chung
/// cũng theo dõi cùng sự kiện đó để bật IsDirty tổng (xem OnFormDeclarationChanged).
public sealed class ThongTriFormPageViewModel : WorkbookPageViewModel
{
    public ThongTriFormPageViewModel(ThongTriFormViewModel inner)
    {
        Inner = inner;
        Title = "Thông tri";
        Inner.DeclarationChanged += (_, _) => IsDirty = true;
    }

    public ThongTriFormViewModel Inner { get; }
    public override object Content => Inner;
}

public sealed class KsChiFormPageViewModel : WorkbookPageViewModel
{
    public KsChiFormPageViewModel(KsChiFormViewModel inner)
    {
        Inner = inner;
        Title = "KS chi";
        Inner.DeclarationChanged += (_, _) => IsDirty = true;
    }

    public KsChiFormViewModel Inner { get; }
    public override object Content => Inner;
}

public sealed class GdnttFormPageViewModel : WorkbookPageViewModel
{
    public GdnttFormPageViewModel(GdnttFormViewModel inner)
    {
        Inner = inner;
        Title = "GĐNTT";
        Inner.DeclarationChanged += (_, _) => IsDirty = true;
    }

    public GdnttFormViewModel Inner { get; }
    public override object Content => Inner;
}
