using ConstructionFinance.Application.Printing;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// The pinned per-project "Tổng hợp" tab (first in the Vị trí tab strip).
public sealed class ProjectTongHopPageViewModel : WorkbookPageViewModel
{
    // Tên sheet theo Mẫu.xlsx. Cùng chữ với tiêu đề tab nhưng là hai thứ độc lập: đổi chữ trên tab
    // không được làm đổi tên sheet trong bản in.
    private const string SheetName = "Tổng hợp";

    // 13 cột, đúng thứ tự cột của lưới trên màn hình.
    private static readonly PrintColumn[] PrintColumns =
    [
        new("STT", 5, PrintAlign.Center),
        new("Tên vị trí", 30, PrintAlign.Left),
        new("Đơn vị thi công", 24, PrintAlign.Left),
        new("Giá trị HĐ, PL", 16, PrintAlign.Right),
        new("Tạm ứng", 16, PrintAlign.Right),
        new("TT KLHT", 16, PrintAlign.Right),
        new("Thu hồi", 15, PrintAlign.Right),
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

    // Cả 13 ô là chuỗi hiển thị sẵn của màn hình (tiền đã định dạng, 0 thành trống), giữ nguyên để bản
    // in khớp màn hình từng ký tự.
    private static PrintRow ToPrintRow(SummaryDisplayRow row) => new(
        new[]
        {
            PrintCell.OfText(row.Ordinal),
            PrintCell.OfText(row.Label),
            PrintCell.OfText(row.Contractor),
            PrintCell.OfText(row.ContractValue),
            PrintCell.OfText(row.Advance),
            PrintCell.OfText(row.KLHT),
            PrintCell.OfText(row.Recovered),
            PrintCell.OfText(row.DisbursedToContractor),
            PrintCell.OfText(row.Unrecovered),
            PrintCell.OfText(row.NSNN),
            PrintCell.OfText(row.NgoaiHoi),
            PrintCell.OfText(row.TotalDisbursed),
            PrintCell.OfText(row.YearlyText),
        },
        IsBold: row.IsBold);
}
