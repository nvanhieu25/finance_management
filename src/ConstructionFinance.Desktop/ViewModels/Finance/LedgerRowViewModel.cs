using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// Which kind of row this is in the flattened Vị trí ledger outline grid.
/// Determines which cells are meaningful/editable for a given row.
public enum RowKind { Total, Group, Package, Entry }

/// One flattened row in the Vị trí ledger outline grid (Total / Group / Package / Entry).
/// Holds facades for every editable cell across all row kinds; the grid only shows the
/// cells relevant to a row's Kind. Derived/subtotal display columns (Total/Group/Package
/// rows) are plain settable strings the parent VM computes and assigns — this VM does not
/// compute them itself.
public partial class LedgerRowViewModel : ObservableObject
{
    public RowKind Kind { get; }

    [ObservableProperty] private int _depth;
    [ObservableProperty] private int _sortOrder;

    // --- Shared "Name" cell: Group row name, Package row name (see also PACKAGE section) ---
    [ObservableProperty] private string? _name;

    // --- Entry row raw inputs ---
    [ObservableProperty] private DateOnly _date = DateOnly.FromDateTime(DateTime.Today);
    [ObservableProperty] private string? _content;
    [ObservableProperty] private string? _documentNo;
    [ObservableProperty] private Fund _fund;
    [ObservableProperty] private decimal _advance;
    [ObservableProperty] private decimal _paymentKLHT;
    [ObservableProperty] private decimal _recoveredAdvance;

    // --- Package row fields ---
    [ObservableProperty] private string? _contractorName;
    [ObservableProperty] private string? _bankAccount;
    [ObservableProperty] private decimal _contractValue;
    [ObservableProperty] private decimal _contractAnnexValue;

    // --- Read-only computed display columns (Total/Group/Package rows); parent VM sets these ---
    [ObservableProperty] private string _disbursedNsnnText = "";
    [ObservableProperty] private string _disbursedNgoaiHoiText = "";
    [ObservableProperty] private string _unrecoveredAdvanceText = "";
    [ObservableProperty] private string _totalDisbursedText = "";

    /// Bản gốc của bốn cột tính sẵn ở trên, giữ lại để bản in ghi được SỐ thật thay vì
    /// chuỗi đã định dạng. Null với dòng Đợt (đợt không có số tổng hợp riêng).
    public TreeAggregate? Aggregate { get; internal set; }

    /// Pure display flag driven by VitriLedgerViewModel.SelectedRow (the single source of
    /// truth — this is NOT a second selection model, just a per-row mirror of it so the
    /// row's own DataTemplate can paint a highlight without a converter/multi-binding).
    /// Parent VM sets this; it does not raise InputChanged.
    [ObservableProperty] private bool _isSelected;

    /// Raised whenever an EDITABLE cell facade changes (entry date/content/document/fund/money,
    /// package name/contractor/bank/contract-value facades). Read-only computed display props do
    /// NOT raise this — the parent VM sets them, it doesn't need to be told about its own writes.
    public event EventHandler? InputChanged;

    public LedgerRowViewModel(RowKind kind) => Kind = kind;

    /// String facade for the Date cell (Entry rows). Editable TextBoxes must NOT combine a
    /// TwoWay binding with StringFormat on a typed (DateOnly/decimal) source — Avalonia writes
    /// the formatted string back, re-raising PropertyChanged in an infinite loop (StackOverflow).
    /// The grid binds the date cell to this plain string instead.
    public string DateText
    {
        get => Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        set
        {
            var parsed = FinanceCellParsing.ParseDate(value);
            if (parsed is { } d) Date = d;   // leaves Date unchanged on invalid input
        }
    }

    /// String facade for Fund (Entry rows): "NSNN" ↔ Fund.NSNN, "Ngoại hối" ↔ Fund.NgoaiHoi.
    public string FundText
    {
        get => Fund == Fund.NgoaiHoi ? "Ngoại hối" : "NSNN";
        set
        {
            Fund = value?.Trim() switch
            {
                "Ngoại hối" => Fund.NgoaiHoi,
                _ => Fund.NSNN
            };
        }
    }

    /// Money facade over Advance (Entry rows only — Group/Package rows ignore this cell).
    /// Leaves value unchanged on invalid input.
    public string AdvanceText
    {
        get => MoneyText.Format(Advance);
        set { if (Kind == RowKind.Entry && MoneyText.TryParse(value, out var v)) Advance = v; }
    }

    /// Money facade over PaymentKLHT (Entry rows only).
    public string KlhtText
    {
        get => MoneyText.Format(PaymentKLHT);
        set { if (Kind == RowKind.Entry && MoneyText.TryParse(value, out var v)) PaymentKLHT = v; }
    }

    /// Money facade over RecoveredAdvance (Entry rows only).
    public string RecoveredText
    {
        get => MoneyText.Format(RecoveredAdvance);
        set { if (Kind == RowKind.Entry && MoneyText.TryParse(value, out var v)) RecoveredAdvance = v; }
    }

    /// Money facade over ContractValue (Package rows only).
    public string ContractValueText
    {
        get => MoneyText.Format(ContractValue);
        set { if (Kind == RowKind.Package && MoneyText.TryParse(value, out var v)) ContractValue = v; }
    }

    /// Money facade over ContractAnnexValue (Package rows only).
    public string ContractAnnexText
    {
        get => MoneyText.Format(ContractAnnexValue);
        set { if (Kind == RowKind.Package && MoneyText.TryParse(value, out var v)) ContractAnnexValue = v; }
    }

    // Any raw EDITABLE input change → notify parent. Mirrors LineItemRowViewModel: only
    // editable-facade-backing properties raise InputChanged; the computed *Text display
    // props (DisbursedNsnnText etc.) deliberately have no partial On*Changed hook below.
    partial void OnDateChanged(DateOnly value)
    {
        OnPropertyChanged(nameof(DateText));
        OnInputChanged();
    }
    partial void OnContentChanged(string? value) => OnInputChanged();
    partial void OnDocumentNoChanged(string? value) => OnInputChanged();
    partial void OnFundChanged(Fund value)
    {
        OnPropertyChanged(nameof(FundText));
        OnInputChanged();
    }
    partial void OnAdvanceChanged(decimal value)
    {
        OnPropertyChanged(nameof(AdvanceText));
        OnInputChanged();
    }
    partial void OnPaymentKLHTChanged(decimal value)
    {
        OnPropertyChanged(nameof(KlhtText));
        OnInputChanged();
    }
    partial void OnRecoveredAdvanceChanged(decimal value)
    {
        OnPropertyChanged(nameof(RecoveredText));
        OnInputChanged();
    }
    partial void OnNameChanged(string? value) => OnInputChanged();
    partial void OnContractorNameChanged(string? value) => OnInputChanged();
    partial void OnBankAccountChanged(string? value) => OnInputChanged();
    partial void OnContractValueChanged(decimal value)
    {
        OnPropertyChanged(nameof(ContractValueText));
        OnInputChanged();
    }
    partial void OnContractAnnexValueChanged(decimal value)
    {
        OnPropertyChanged(nameof(ContractAnnexText));
        OnInputChanged();
    }

    private void OnInputChanged() => InputChanged?.Invoke(this, EventArgs.Empty);

    /// Builds a Dot from this row's typed values (Entry rows). Caller owns PackageId/Package
    /// association — this only carries the fields the row itself knows about.
    public Dot ToDot() => new()
    {
        SortOrder = SortOrder,
        Date = Date,
        Content = Content,
        DocumentNo = DocumentNo,
        Fund = Fund,
        Advance = Advance,
        PaymentKLHT = PaymentKLHT,
        RecoveredAdvance = RecoveredAdvance
    };
}
