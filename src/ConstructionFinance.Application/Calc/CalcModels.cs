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

/// Per-year settlement breakdown (Quyết toán theo năm).
public record YearSettlement(int Year, decimal NSNN, decimal NgoaiHoi, decimal KLHT, decimal Advance);

/// Computed totals for one project (the DA "Tổng cộng" row).
public record ProjectAggregate(
    decimal SumAdvance,
    decimal SumRecovered,
    decimal SumKLHT,
    decimal SumNSNN,
    decimal SumNgoaiHoi,
    decimal UnrecoveredAdvance,
    decimal TotalDisbursed,
    IReadOnlyList<YearSettlement> YearlySettlements);
