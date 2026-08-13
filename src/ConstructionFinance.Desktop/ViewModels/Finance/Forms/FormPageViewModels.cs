namespace ConstructionFinance.Desktop.ViewModels.Finance.Forms;

/// Ba tab biểu mẫu đứng cuối dải tab Vị trí. Chúng chỉ xem — không có IsDirty riêng;
/// phần bẩn do ô nhập tay được workbook theo dõi qua sự kiện DeclarationChanged.
public sealed class ThongTriFormPageViewModel : WorkbookPageViewModel
{
    public ThongTriFormPageViewModel(ThongTriFormViewModel inner)
    {
        Inner = inner;
        Title = "Thông tri";
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
    }

    public GdnttFormViewModel Inner { get; }
    public override object Content => Inner;
}
