using ConstructionFinance.Application.Printing;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// The per-project "Quyết toán năm" tab (last in the Vị trí tab strip).
public sealed class YearSettlementPageViewModel : WorkbookPageViewModel
{
    // Tên sheet theo Mẫu.xlsx. Cùng chữ với tiêu đề tab nhưng là hai thứ độc lập: đổi chữ trên tab
    // không được làm đổi tên sheet trong bản in.
    private const string SheetName = "Quyết toán năm";

    private static readonly PrintColumn[] PrintColumns =
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

    // Cả 4 ô là chuỗi hiển thị sẵn của màn hình, giữ nguyên để bản in khớp màn hình từng ký tự.
    private static PrintRow ToPrintRow(YearSettlementRow row) => new(
        new[]
        {
            PrintCell.OfText(row.YearLabel),
            PrintCell.OfText(row.NSNN),
            PrintCell.OfText(row.NgoaiHoi),
            PrintCell.OfText(row.Total),
        },
        IsBold: row.IsBold);
}
