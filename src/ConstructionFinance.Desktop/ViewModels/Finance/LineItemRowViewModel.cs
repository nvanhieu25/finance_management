using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// One đợt row in the entry grid. Holds only raw input cells; derived cells
/// (Disbursed/Expected/IsBalanced/KindLabel) are recomputed live by ProjectCalculator
/// on every input change. Raises InputChanged so the grid VM can refresh totals.
public partial class LineItemRowViewModel : ObservableObject
{
    [ObservableProperty] private int _sortOrder;
    [ObservableProperty] private DateOnly _date = DateOnly.FromDateTime(DateTime.Today);
    [ObservableProperty] private string? _packageName;
    [ObservableProperty] private string? _contractRef;
    [ObservableProperty] private decimal _advance;
    [ObservableProperty] private decimal _paymentKLHT;
    [ObservableProperty] private decimal _recoveredAdvance;
    [ObservableProperty] private decimal _disbursedNSNN;
    [ObservableProperty] private decimal _disbursedNgoaiHoi;

    [ObservableProperty] private decimal _disbursed;
    [ObservableProperty] private decimal _expected;
    [ObservableProperty] private bool _isBalanced = true;
    [ObservableProperty] private string _kindLabel = "Thanh toán KLHT";

    public event EventHandler? InputChanged;

    public LineItemRowViewModel() => Recompute();

    public LineItemRowViewModel(ProjectLineItem entity)
    {
        _sortOrder = entity.SortOrder;
        _date = entity.Date;
        _packageName = entity.PackageName;
        _contractRef = entity.ContractRef;
        _advance = entity.Advance;
        _paymentKLHT = entity.PaymentKLHT;
        _recoveredAdvance = entity.RecoveredAdvance;
        _disbursedNSNN = entity.DisbursedNSNN;
        _disbursedNgoaiHoi = entity.DisbursedNgoaiHoi;
        Recompute();
    }

    // Any raw input change → recompute derived cells + notify parent.
    partial void OnDateChanged(DateOnly value) => OnInputChanged();
    partial void OnAdvanceChanged(decimal value) => OnInputChanged();
    partial void OnPaymentKLHTChanged(decimal value) => OnInputChanged();
    partial void OnRecoveredAdvanceChanged(decimal value) => OnInputChanged();
    partial void OnDisbursedNSNNChanged(decimal value) => OnInputChanged();
    partial void OnDisbursedNgoaiHoiChanged(decimal value) => OnInputChanged();

    private void OnInputChanged()
    {
        Recompute();
        InputChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Recompute()
    {
        var computed = ProjectCalculator.ComputeLine(ToInput());
        Disbursed = computed.Disbursed;
        Expected = computed.Expected;
        IsBalanced = computed.IsBalanced;
        KindLabel = computed.Kind == LineItemKind.Advance ? "Ứng tiền" : "Thanh toán KLHT";
    }

    /// Assign one editable cell (columns 0–6) from raw clipboard/typed text.
    public void SetCell(int columnIndex, string raw)
    {
        switch (columnIndex)
        {
            case 0: Date = FinanceCellParsing.ParseDate(raw) ?? Date; break;
            case 1: PackageName = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim(); break;
            case 2: Advance = FinanceCellParsing.ParseMoney(raw); break;
            case 3: PaymentKLHT = FinanceCellParsing.ParseMoney(raw); break;
            case 4: RecoveredAdvance = FinanceCellParsing.ParseMoney(raw); break;
            case 5: DisbursedNSNN = FinanceCellParsing.ParseMoney(raw); break;
            case 6: DisbursedNgoaiHoi = FinanceCellParsing.ParseMoney(raw); break;
            // 7+ are derived/read-only — ignore.
        }
    }

    public LineItemInput ToInput() =>
        new(Date, PackageName, Advance, PaymentKLHT, RecoveredAdvance, DisbursedNSNN, DisbursedNgoaiHoi);

    public ProjectLineItem ToEntity() => new()
    {
        SortOrder = SortOrder,
        Date = Date,
        PackageName = PackageName,
        ContractRef = ContractRef,
        Advance = Advance,
        PaymentKLHT = PaymentKLHT,
        RecoveredAdvance = RecoveredAdvance,
        DisbursedNSNN = DisbursedNSNN,
        DisbursedNgoaiHoi = DisbursedNgoaiHoi
    };
}
