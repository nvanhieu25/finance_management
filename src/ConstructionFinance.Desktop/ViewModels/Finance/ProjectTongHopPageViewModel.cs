using System.Collections.Immutable;
using ConstructionFinance.Application.Printing;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// The pinned per-project "Tổng hợp" tab (first in the Vị trí tab strip).
public sealed class ProjectTongHopPageViewModel : WorkbookPageViewModel
{
    // Tên sheet theo Mẫu.xlsx. Cùng chữ với tiêu đề tab nhưng là hai thứ độc lập: đổi chữ trên tab
    // không được làm đổi tên sheet trong bản in.
    private const string SheetName = "Tổng hợp";

    // 13 cột, đúng thứ tự cột của lưới trên màn hình. ImmutableArray vì danh sách được trao nguyên cho mọi
    // PrintTableSheet (xem ViTriPageViewModel.PrintColumns).
    private static readonly ImmutableArray<PrintColumn> PrintColumns =
    [
        new("STT", 5, PrintAlign.Center),
        new("Tên vị trí", 30, PrintAlign.Left),
        new("Đơn vị thi công", 24, PrintAlign.Left),
        new("Giá trị HĐ, PL", 16, PrintAlign.Right),
        new("Tạm ứng", 16, PrintAlign.Right),
        new("TT KLHT", 16, PrintAlign.Right),
        new("Thu hồi", 16, PrintAlign.Right),   // 16 = trần 99 tỷ; xem ViTriPageViewModel.MoneyWidth
        new("TT cho nhà thầu", 16, PrintAlign.Right),
        new("Tạm ứng chưa thu hồi", 16, PrintAlign.Right),
        new("GN Dự phòng NSNN", 16, PrintAlign.Right),
        new("GN Quỹ DTNH", 16, PrintAlign.Right),
        new("Tổng đã giải ngân", 16, PrintAlign.Right),
        new("Quyết toán năm", 28, PrintAlign.Left),
    ];

    public ProjectTongHopPageViewModel(ProjectTongHopViewModel inner)
    {
        Inner = inner;
        Title = "Tổng hợp";
    }

    public ProjectTongHopViewModel Inner { get; }
    public override object Content => Inner;

    public override string? PrintSheetName => SheetName;

    public override PrintSheet? BuildPrintSheet() => new PrintTableSheet(
        SheetName,
        PrintPageProfiles.ProjectTongHop,
        PrintColumns,
        Inner.Rows.Select(ToPrintRow).ToList());

    // Bốn cột chữ (STT, Tên vị trí, Đơn vị thi công, Quyết toán năm) giữ nguyên chuỗi của màn hình. Chín
    // cột tiền là SỐ thật lấy từ Source (cùng nguồn mà SummaryDisplayRow.From dùng để định dạng chuỗi),
    // để chọn một vùng trong Excel là thấy tổng; 0 ra ô trống như màn hình. "TT cho nhà thầu" và
    // "Tổng đã giải ngân" cùng đọc TotalDisbursed — đúng như màn hình, bản in giữ nguyên.
    private static PrintRow ToPrintRow(SummaryDisplayRow row)
    {
        var source = row.Source;
        var aggregate = source?.Aggregate;
        return new PrintRow(
            new[]
            {
                PrintCell.OfText(row.Ordinal),
                PrintCell.OfText(row.Label),
                PrintCell.OfText(row.Contractor),
                Money(source?.ContractValue),
                Money(aggregate?.SumAdvance),
                Money(aggregate?.SumKLHT),
                Money(aggregate?.SumRecovered),
                Money(aggregate?.TotalDisbursed),
                Money(aggregate?.UnrecoveredAdvance),
                Money(aggregate?.DisbursedNSNN),
                Money(aggregate?.DisbursedNgoaiHoi),
                Money(aggregate?.TotalDisbursed),
                PrintCell.OfText(row.YearlyText),
            },
            IsBold: row.IsBold);
    }

    /// Số 0 ra ô trống như màn hình (PrintCell.OfMoney); dòng dựng tay không có Source thì không có số
    /// để ghi, cũng ra ô trống.
    private static PrintCell Money(decimal? value) => value is { } v ? PrintCell.OfMoney(v) : PrintCell.Empty;
}
