using System.Collections.Immutable;
using System.ComponentModel;
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.Printing;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// One Vị trí tab within the selected project's Vị trí tab strip: wraps a VitriLedgerViewModel,
/// tracks its dirty state for the tab bullet, and follows its Name for the tab title.
public sealed class ViTriPageViewModel : WorkbookPageViewModel
{
    public ViTriPageViewModel(VitriLedgerViewModel ledger)
    {
        Ledger = ledger;
        Title = DisplayName(ledger.Name);
        IsDirty = ledger.IsDirty;

        Ledger.DirtyChanged += (_, _) => IsDirty = Ledger.IsDirty;
        Ledger.PropertyChanged += OnLedgerPropertyChanged;
    }

    public VitriLedgerViewModel Ledger { get; }

    public override object Content => Ledger;

    private void OnLedgerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VitriLedgerViewModel.Name))
        {
            Title = DisplayName(Ledger.Name);
            // Tên sheet in là chính tên này (xem PrintSheetName): đổi tên Vị trí là đổi tên sheet, nên báo cho nơi đang
            // nghe (nút In ở khung chung có thể hiện tên đó).
            OnPropertyChanged(nameof(PrintSheetName));
        }
    }

    private static string DisplayName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "(vị trí mới)" : name.Trim();

    // ===== In =====

    // Sổ Vị trí in 14 cột, đúng thứ tự và tiêu đề như lưới trên màn hình. Bề rộng tính bằng KÝ TỰ (đơn vị
    // của Excel) và là bộ riêng của bản in, cố ý không lấy từ LedgerColumnWidths (pixel màn hình).
    // ImmutableArray vì danh sách này được trao nguyên cho mọi PrintTableSheet: một mảng thường thì người
    // nhận ép kiểu về PrintColumn[] rồi sửa được, làm hỏng mọi lần in sau.
    /// Bề rộng MỌI cột tiền của bản in. Ô số không xuống dòng được: hẹp hơn chuỗi là Excel in "####",
    /// nên con số này là một ràng buộc chứ không phải thẩm mỹ. Đo (2026-10-06): bề rộng cột tính bằng
    /// chữ "0" của Calibri 11 (7px @96dpi, kiểm bằng hằng số đã biết 8.43 ký tự = 64px), còn ô lại in
    /// Times New Roman 13 — rộng hơn, nên sức chứa THỰC nhỏ hơn bề rộng danh nghĩa. 16 ký tự chứa tới
    /// 99.999.999.999 (dưới 100 tỷ); trần này do người dùng chốt. Ép vừa trang KHÔNG cứu được "####"
    /// vì nó thu nhỏ cột và chữ cùng tỉ lệ. Muốn trần cao hơn: 18 cho dưới 1.000 tỷ, 19 cho dưới 10.000 tỷ.
    private const int MoneyWidth = 16;

    private static readonly ImmutableArray<PrintColumn> PrintColumns =
    [
        new("Ngày", 11, PrintAlign.Center),
        new("Nội dung", 38, PrintAlign.Left),
        new("Nhà thầu / TK", 22, PrintAlign.Left),
        new("Giá trị HĐ", MoneyWidth, PrintAlign.Right),
        new("Phụ lục", MoneyWidth, PrintAlign.Right),
        new("Số CT", 11, PrintAlign.Center),
        new("Tạm ứng", MoneyWidth, PrintAlign.Right),
        new("KLHT", MoneyWidth, PrintAlign.Right),
        new("Thu hồi", MoneyWidth, PrintAlign.Right),
        new("Nguồn", 11, PrintAlign.Center),
        new("GN NSNN", MoneyWidth, PrintAlign.Right),
        new("GN Ngoại hối", MoneyWidth, PrintAlign.Right),
        new("Số dư TU", MoneyWidth, PrintAlign.Right),
        new("Tổng đã GN", MoneyWidth, PrintAlign.Right),
    ];

    // Chỉ số cột trong PrintColumns, để dựng ô theo tên cột thay vì số trơ trọi.
    private const int ColNgay = 0, ColNoiDung = 1, ColNhaThauTk = 2, ColGiaTriHd = 3, ColPhuLuc = 4,
        ColSoCt = 5, ColTamUng = 6, ColKlht = 7, ColThuHoi = 8, ColNguon = 9,
        ColGnNsnn = 10, ColGnNgoaiHoi = 11, ColSoDuTu = 12, ColTongDaGn = 13;

    public override string? PrintSheetName => DisplayName(Ledger.Name);

    public override PrintSheet? BuildPrintSheet() => new PrintTableSheet(
        DisplayName(Ledger.Name),
        PrintPageProfiles.ViTriLedger,
        PrintColumns,
        Ledger.Rows.Select(ToPrintRow).ToList());

    /// Mỗi dòng hiển thị là một dòng in, ô nào dòng đó không có dữ liệu thì PrintCell.Empty.
    private static PrintRow ToPrintRow(LedgerRowViewModel row)
    {
        var cells = new PrintCell[PrintColumns.Length];
        Array.Fill(cells, PrintCell.Empty);

        switch (row.Kind)
        {
            case RowKind.Total:
                // Cùng hằng mà lưới trên màn hình bind tới (xem VitriLedgerViewModel.TotalLabel).
                cells[ColNoiDung] = PrintCell.OfText(VitriLedgerViewModel.TotalLabel);
                FillComputedColumns(cells, row.Aggregate);
                return new PrintRow(cells, IndentLevel: 0, IsBold: true, Shade: PrintShade.Light);

            case RowKind.Group:
                cells[ColNoiDung] = PrintCell.OfText(row.Name);
                FillComputedColumns(cells, row.Aggregate);
                return new PrintRow(cells, IndentLevel: 0, IsBold: true, Shade: PrintShade.VeryLight);

            case RowKind.Package:
                cells[ColNoiDung] = PrintCell.OfText(row.Name);
                cells[ColNhaThauTk] = ContractorCell(row);
                cells[ColGiaTriHd] = PrintCell.OfMoney(row.ContractValue);
                cells[ColPhuLuc] = PrintCell.OfMoney(row.ContractAnnexValue);
                FillComputedColumns(cells, row.Aggregate);
                return new PrintRow(cells, IndentLevel: 1, IsBold: true);

            case RowKind.Entry:
                cells[ColNgay] = PrintCell.OfDate(row.Date);
                cells[ColNoiDung] = PrintCell.OfText(row.Content);
                cells[ColSoCt] = PrintCell.OfText(row.DocumentNo);
                cells[ColTamUng] = PrintCell.OfMoney(row.Advance);
                cells[ColKlht] = PrintCell.OfMoney(row.PaymentKLHT);
                cells[ColThuHoi] = PrintCell.OfMoney(row.RecoveredAdvance);
                cells[ColNguon] = PrintCell.OfText(row.FundText);
                return new PrintRow(cells, IndentLevel: 2);

            default:
                throw new NotSupportedException($"Chưa có cách in dòng loại {row.Kind}.");
        }
    }

    /// Bốn cột tính sẵn (GN NSNN, GN Ngoại hối, Số dư TU, Tổng đã GN) lấy từ Aggregate: SỐ thật chứ
    /// không phải chuỗi đã định dạng trên màn hình. Chưa có Aggregate thì màn hình để trống bốn ô này,
    /// bản in cũng vậy.
    private static void FillComputedColumns(PrintCell[] cells, TreeAggregate? aggregate)
    {
        if (aggregate is null) return;

        cells[ColGnNsnn] = PrintCell.OfMoney(aggregate.DisbursedNSNN);
        cells[ColGnNgoaiHoi] = PrintCell.OfMoney(aggregate.DisbursedNgoaiHoi);
        cells[ColSoDuTu] = PrintCell.OfMoney(aggregate.UnrecoveredAdvance);
        cells[ColTongDaGn] = PrintCell.OfMoney(aggregate.TotalDisbursed);
    }

    /// Nhà thầu và số TK ghép trong một ô, cách nhau bằng ngắt dòng (bộ ghi bật WrapText cho ô chữ).
    /// Phần nào trống thì bỏ; cả hai trống thì ô trống.
    private static PrintCell ContractorCell(LedgerRowViewModel row)
    {
        var parts = new[] { row.ContractorName, row.BankAccount }.Where(p => !string.IsNullOrWhiteSpace(p));
        var text = string.Join("\n", parts);
        return text.Length == 0 ? PrintCell.Empty : PrintCell.OfText(text);
    }
}
