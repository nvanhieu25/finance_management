namespace ConstructionFinance.Application.Calc;

public enum LineItemKind { Advance, Payment }

/// Raw inputs for one đợt (line item). All money is decimal đồng.
public record LineItemInput(
    DateOnly Date,
    string? PackageName,
    decimal Advance,
    decimal PaymentKLHT,
    decimal RecoveredAdvance,
    decimal DisbursedNSNN,
    decimal DisbursedNgoaiHoi);

/// Computed view of one đợt.
public record LineItemComputed(
    LineItemKind Kind,
    decimal Disbursed,
    decimal Expected,
    bool IsBalanced);
