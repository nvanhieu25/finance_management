using System.Collections.Immutable;
using ConstructionFinance.Application.Printing;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// The per-project "Quyết toán năm" tab (last in the Vị trí tab strip).
public sealed class YearSettlementPageViewModel : WorkbookPageViewModel
{
    // Tên sheet theo Mẫu.xlsx. Cùng chữ với tiêu đề tab nhưng là hai thứ độc lập: đổi chữ trên tab
    // không được làm đổi tên sheet trong bản in.
    private const string SheetName = "Quyết toán năm";

    private const string DisbursedGroup = "Giá trị giải ngân";

    // Sheet "Quyết toán năm" của Mẫu.xlsx để trống, nên bố cục theo cùng kiểu với sheet Tổng hợp: tiêu đề tờ, tiêu đề cột
    // hai tầng, NSNN/DTNH cạnh nhau. ImmutableArray vì danh sách được trao nguyên cho mọi PrintTableSheet (xem
    // ViTriPageViewModel.PrintColumns).
    private static readonly ImmutableArray<PrintColumn> PrintColumns =
    [
        new("Số TT", 6, PrintAlign.Center),
        new("Năm tài chính", 22, PrintAlign.Left),
        new("Dự phòng NSNN", 18, PrintAlign.Right, DisbursedGroup),
        new("Quỹ dự trữ ngoại hối", 18, PrintAlign.Right, DisbursedGroup),
        new("Tổng cộng", 18, PrintAlign.Right),
    ];

    private static readonly IReadOnlyList<string> Titles =
        ["QUYẾT TOÁN THEO NĂM TÀI CHÍNH", "(Năm tài chính từ 01/02 đến 31/01 năm sau — Đơn vị tính: đồng)"];

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
        Inner.Rows.Select((row, index) => ToPrintRow(row, index)).ToList(),
        Titles);

    // Nhãn năm giữ nguyên chuỗi của màn hình. Ba cột tiền là SỐ thật (cột Tổng là tổng của hai cột kia, như
    // trên màn hình) để chọn một vùng trong Excel là thấy tổng; 0 ra ô trống như màn hình. Dòng TỔNG CỘNG không đánh số.
    private static PrintRow ToPrintRow(YearSettlementRow row, int index) => new(
        new[]
        {
            PrintCell.OfText(row.IsBold ? "" : (index + 1).ToString()),
            PrintCell.OfText(row.YearLabel),
            Money(row.NsnnAmount),
            Money(row.NgoaiHoiAmount),
            Money(row.NsnnAmount + row.NgoaiHoiAmount),
        },
        IsBold: row.IsBold);

    /// Số 0 ra ô trống như màn hình (PrintCell.OfMoney); dòng dựng tay không có số để ghi, cũng ra ô trống.
    private static PrintCell Money(decimal? value) => value is { } v ? PrintCell.OfMoney(v) : PrintCell.Empty;
}
