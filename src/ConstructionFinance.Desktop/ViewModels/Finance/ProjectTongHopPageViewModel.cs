using System.Collections.Immutable;
using ConstructionFinance.Application.Printing;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// The pinned per-project "Tổng hợp" tab (first in the Vị trí tab strip).
public sealed class ProjectTongHopPageViewModel : WorkbookPageViewModel
{
    // Tên sheet theo Mẫu.xlsx. Cùng chữ với tiêu đề tab nhưng là hai thứ độc lập: đổi chữ trên tab
    // không được làm đổi tên sheet trong bản in.
    private const string SheetName = "Tổng hợp";

    /// 16 là trần 99 tỷ (xem ViTriPageViewModel.MoneyWidth); thêm 1 cho số in ĐẬM của dòng tổng không chạm mép cột.
    private const double MoneyWidth = 17;

    private const string KlhtGroup = "Thanh toán khối lượng hoàn thành";

    // 12 cột cố định theo sheet "Tổng hợp" của Mẫu.xlsx, rồi mỗi năm tài chính có số thêm một cặp NSNN/DTNH dưới
    // "Quyết toán năm …" (Mẫu: M6:N6, O6:P6…). ImmutableArray vì danh sách được trao nguyên cho mọi PrintTableSheet
    // (xem ViTriPageViewModel.PrintColumns).
    private static readonly ImmutableArray<PrintColumn> FixedColumns =
    [
        new("Số TT", 5, PrintAlign.Center),
        new("Tên vị trí", 26, PrintAlign.Left),
        new("Đơn vị thi công xây lắp", 20, PrintAlign.Left),
        new("Giá trị HĐ, PLHĐ xây lắp", MoneyWidth, PrintAlign.Right),
        new("Tạm ứng", MoneyWidth, PrintAlign.Right),
        new("TT khối lượng hoàn thành", MoneyWidth, PrintAlign.Right, KlhtGroup),
        new("Thu hồi tạm ứng", MoneyWidth, PrintAlign.Right, KlhtGroup),
        new("TT tiền cho nhà thầu", MoneyWidth, PrintAlign.Right, KlhtGroup),
        new("Tạm ứng chưa thu hồi", MoneyWidth, PrintAlign.Right),
        new("Giải ngân từ Dự phòng NSNN", MoneyWidth, PrintAlign.Right),
        new("Giải ngân từ Quỹ dự trữ ngoại hối", MoneyWidth, PrintAlign.Right),
        new("Tổng giá trị đã giải ngân", MoneyWidth, PrintAlign.Right),
    ];

    private static readonly IReadOnlyList<string> Titles =
        ["NGUỒN KINH PHÍ ĐẦU TƯ XÂY DỰNG", "(Đơn vị tính: đồng)"];

    public ProjectTongHopPageViewModel(ProjectTongHopViewModel inner)
    {
        Inner = inner;
        Title = "Tổng hợp";
    }

    public ProjectTongHopViewModel Inner { get; }
    public override object Content => Inner;

    public override string? PrintSheetName => SheetName;

    public override PrintSheet? BuildPrintSheet()
    {
        var years = YearsWithMoney();
        var columns = FixedColumns.Concat(years.SelectMany(year => new PrintColumn[]
        {
            new("NSNN", MoneyWidth, PrintAlign.Right, $"Quyết toán năm {year}"),
            new("DTNH", MoneyWidth, PrintAlign.Right, $"Quyết toán năm {year}"),
        })).ToList();

        return new PrintTableSheet(
            SheetName,
            PrintPageProfiles.ProjectTongHop,
            columns,
            Inner.Rows.Select(row => ToPrintRow(row, years)).ToList(),
            Titles);
    }

    /// Các năm tài chính có số ở ít nhất một dòng, tăng dần — đúng những năm màn hình ghi trong cột "Quyết toán năm".
    private IReadOnlyList<int> YearsWithMoney() =>
        Inner.Rows
            .SelectMany(row => row.Source?.Aggregate.Yearly ?? [])
            .Where(y => y.NSNN != 0m || y.NgoaiHoi != 0m)
            .Select(y => y.FiscalYear)
            .Distinct()
            .Order()
            .ToList();

    // Ba cột chữ (STT, Tên vị trí, Đơn vị thi công) giữ nguyên chuỗi của màn hình. Cột tiền là SỐ thật lấy từ Source
    // (cùng nguồn mà SummaryDisplayRow.From dùng để định dạng chuỗi), để chọn một vùng trong Excel là thấy tổng; 0 ra
    // ô trống như màn hình. "TT tiền cho nhà thầu" và "Tổng giá trị đã giải ngân" cùng đọc TotalDisbursed — Mẫu cũng
    // trỏ cả hai về cùng một ô ('Vị trí 1'!N7).
    private static PrintRow ToPrintRow(SummaryDisplayRow row, IReadOnlyList<int> years)
    {
        var source = row.Source;
        var aggregate = source?.Aggregate;
        var cells = new List<PrintCell>
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
        };
        foreach (var year in years)
        {
            var settlement = aggregate?.Yearly.FirstOrDefault(y => y.FiscalYear == year);
            cells.Add(Money(settlement?.NSNN));
            cells.Add(Money(settlement?.NgoaiHoi));
        }
        return new PrintRow(cells, IsBold: row.IsBold);
    }

    /// Số 0 ra ô trống như màn hình (PrintCell.OfMoney); dòng dựng tay không có Source thì không có số
    /// để ghi, cũng ra ô trống.
    private static PrintCell Money(decimal? value) => value is { } v ? PrintCell.OfMoney(v) : PrintCell.Empty;
}
