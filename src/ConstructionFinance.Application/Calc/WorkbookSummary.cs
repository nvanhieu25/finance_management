namespace ConstructionFinance.Application.Calc;

public enum SummaryRowKind { Project, ViTri, GrandTotal }

/// One flat row of a Tổng hợp table. Ordinal is roman for a project subtotal,
/// arabic for a vị trí; ContractValue is Σ (Giá trị HĐ + Phụ lục HĐ) of the
/// packages under it; Aggregate carries every money column (and Yearly).
public record WorkbookSummaryRow(
    SummaryRowKind Kind,
    string Ordinal,
    string Label,
    int ProjectId,
    string? Contractor,
    decimal ContractValue,
    TreeAggregate Aggregate);
