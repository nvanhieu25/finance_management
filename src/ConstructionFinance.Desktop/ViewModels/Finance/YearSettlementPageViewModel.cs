using System.Collections.Immutable;
using ConstructionFinance.Application.Printing;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// The per-project "Quyết toán năm" tab (last in the Vị trí tab strip).
public sealed class YearSettlementPageViewModel : WorkbookPageViewModel
{
    // Tên sheet theo Mẫu.xlsx. Cùng chữ với tiêu đề tab nhưng là hai thứ độc lập: đổi chữ trên tab
    // không được làm đổi tên sheet trong bản in.
    private const string SheetName = "Quyết toán năm";

    // ImmutableArray vì danh sách được trao nguyên cho mọi PrintTableSheet (xem ViTriPageViewModel.PrintColumns).
    private static readonly ImmutableArray<PrintColumn> PrintColumns =
    [
        new("Năm", 12, PrintAlign.Center),
        new("NSNN", 18, PrintAlign.Right),
        new("Ngoại hối", 18, PrintAlign.Right),
        new("Tổng", 18, PrintAlign.Right),
    ];

    public YearSettlementPageViewModel(YearSettlementViewModel inner)
    {
        Inner = inner;
        Title = "Quyết toán năm";
    }

    public YearSettlementViewModel Inner { get; }
    public override object Content => Inner;

    public override string? PrintSheetName => SheetName;

    public override PrintSheet? BuildPrintSheet() => new PrintTableSheet(
        SheetName,
        PrintPageProfiles.YearSettlement,
        PrintColumns,
        Inner.Rows.Select(ToPrintRow).ToList());

    // Nhãn năm giữ nguyên chuỗi của màn hình. Ba cột tiền là SỐ thật (cột Tổng là tổng của hai cột kia, như
    // trên màn hình) để chọn một vùng trong Excel là thấy tổng; 0 ra ô trống như màn hình.
    private static PrintRow ToPrintRow(YearSettlementRow row) => new(
        new[]
        {
            PrintCell.OfText(row.YearLabel),
            Money(row.NsnnAmount),
            Money(row.NgoaiHoiAmount),
            Money(row.NsnnAmount + row.NgoaiHoiAmount),
        },
        IsBold: row.IsBold);

    /// Số 0 ra ô trống như màn hình (PrintCell.OfMoney); dòng dựng tay không có số để ghi, cũng ra ô trống.
    private static PrintCell Money(decimal? value) => value is { } v ? PrintCell.OfMoney(v) : PrintCell.Empty;
}
