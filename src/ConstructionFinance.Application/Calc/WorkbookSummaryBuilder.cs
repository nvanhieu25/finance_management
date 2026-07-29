using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Calc;

/// Pure: turns Vị trí trees into ordered Tổng hợp rows (Project subtotal → its
/// Vị trí rows → grand total), mirroring the Mẫu.xlsx "Tổng hợp" sheet. No DB/UI.
public static class WorkbookSummaryBuilder
{
    /// One row per vị trí (arabic ordinal) plus a "TỔNG CỘNG" grand-total row.
    public static (IReadOnlyList<WorkbookSummaryRow> Rows, WorkbookSummaryRow Total)
        BuildViTriRows(IReadOnlyList<ViTri> viTris)
    {
        var ordered = viTris.OrderBy(v => v.SortOrder).ToList();
        var rows = new List<WorkbookSummaryRow>();
        var aggregates = new List<TreeAggregate>();

        for (var i = 0; i < ordered.Count; i++)
        {
            var v = ordered[i];
            var agg = TreeCalculator.AggregateViTri(v);
            aggregates.Add(agg);
            rows.Add(new WorkbookSummaryRow(
                Kind: SummaryRowKind.ViTri,
                Ordinal: (i + 1).ToString(),
                Label: v.Name,
                ProjectId: v.ProjectId,
                Contractor: FirstContractor(v),
                ContractValue: ContractValueOf(v),
                Aggregate: agg));
        }

        var total = new WorkbookSummaryRow(
            Kind: SummaryRowKind.GrandTotal,
            Ordinal: "",
            Label: "TỔNG CỘNG",
            ProjectId: 0,
            Contractor: null,
            ContractValue: rows.Sum(r => r.ContractValue),
            Aggregate: TreeCalculator.Roll(aggregates));

        return (rows, total);
    }

    /// For each project: a roman-ordinal Project subtotal row then its vị trí
    /// rows; finally a "Tổng cộng" grand total across all projects.
    public static IReadOnlyList<WorkbookSummaryRow> BuildAll(IReadOnlyList<Project> projects)
    {
        var rows = new List<WorkbookSummaryRow>();
        var projectAggregates = new List<TreeAggregate>();
        var grandContract = 0m;

        var index = 0;
        foreach (var p in projects)
        {
            index++;
            var (viTriRows, viTriTotal) = BuildViTriRows(p.ViTris);
            projectAggregates.Add(viTriTotal.Aggregate);
            grandContract += viTriTotal.ContractValue;

            rows.Add(new WorkbookSummaryRow(
                Kind: SummaryRowKind.Project,
                Ordinal: Roman(index),
                Label: p.Name,
                ProjectId: p.Id,
                Contractor: null,
                ContractValue: viTriTotal.ContractValue,
                Aggregate: viTriTotal.Aggregate));
            rows.AddRange(viTriRows);
        }

        rows.Add(new WorkbookSummaryRow(
            Kind: SummaryRowKind.GrandTotal,
            Ordinal: "",
            Label: "Tổng cộng",
            ProjectId: 0,
            Contractor: null,
            ContractValue: grandContract,
            Aggregate: TreeCalculator.Roll(projectAggregates)));

        return rows;
    }

    private static decimal ContractValueOf(ViTri v) =>
        v.CostGroups.SelectMany(g => g.Packages).Sum(p => p.ContractValue + p.ContractAnnexValue);

    private static string? FirstContractor(ViTri v) =>
        v.CostGroups.OrderBy(g => g.SortOrder)
            .SelectMany(g => g.Packages.OrderBy(p => p.SortOrder))
            .Select(p => p.ContractorName)
            .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));

    // Enough for the practical project counts here (1..~30).
    private static string Roman(int n)
    {
        var map = new (int V, string S)[]
        {
            (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"),
            (90, "XC"), (50, "L"), (40, "XL"), (10, "X"),
            (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
        };
        var sb = new System.Text.StringBuilder();
        foreach (var (v, s) in map)
            while (n >= v) { sb.Append(s); n -= v; }
        return sb.ToString();
    }
}
