using ClosedXML.Excel;
using ConstructionFinance.Application.Calc;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Services.Implementations;

public class FinanceExportService : IFinanceExportService
{
    private const string MoneyFormat = "#,##0";

    public Task ExportAsync(IReadOnlyList<Project> projects, string targetPath, CancellationToken ct = default) =>
        Task.Run(() => Write(projects, targetPath), ct);

    private static void Write(IReadOnlyList<Project> projects, string targetPath)
    {
        using var wb = new XLWorkbook();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in projects)
            WriteDaSheet(wb, p, used);
        wb.SaveAs(targetPath);
    }

    private static void WriteDaSheet(XLWorkbook wb, Project project, HashSet<string> usedNames)
    {
        var ws = wb.AddWorksheet(UniqueSheetName(project.Name, usedNames));
        var lines = project.Lines.OrderBy(l => l.SortOrder).ToList();
        var agg = ProjectCalculator.Aggregate(lines.Select(l => l.ToCalcInput()).ToList());

        ws.Cell(1, 1).Value = $"DỰ ÁN: {project.Name}";
        ws.Cell(1, 1).Style.Font.Bold = true;

        var headers = new[] { "Ngày", "Gói thầu", "Hợp đồng", "Phụ lục HĐ", "Tạm ứng", "TT KLHT", "Thu hồi", "GN NSNN", "GN Ngoại hối", "Giải ngân" };
        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(2, c + 1).Value = headers[c];
            ws.Cell(2, c + 1).Style.Font.Bold = true;
        }

        // contract row (row 3)
        ws.Cell(3, 2).Value = string.IsNullOrWhiteSpace(project.Notes) ? "Hợp đồng" : $"Hợp đồng: {project.Notes}";
        SetMoney(ws.Cell(3, 3), project.ContractValue);
        SetMoney(ws.Cell(3, 4), project.ContractAnnexValue);

        // đợt rows (row 4+)
        var r = 4;
        foreach (var line in lines)
        {
            var computed = ProjectCalculator.ComputeLine(line.ToCalcInput());
            ws.Cell(r, 1).Value = line.Date.ToString("dd/MM/yyyy");
            ws.Cell(r, 2).Value = line.PackageName ?? "";
            SetMoney(ws.Cell(r, 5), line.Advance);
            SetMoney(ws.Cell(r, 6), line.PaymentKLHT);
            SetMoney(ws.Cell(r, 7), line.RecoveredAdvance);
            SetMoney(ws.Cell(r, 8), line.DisbursedNSNN);
            SetMoney(ws.Cell(r, 9), line.DisbursedNgoaiHoi);
            SetMoney(ws.Cell(r, 10), computed.Disbursed);
            r++;
        }

        // TỔNG CỘNG row
        ws.Cell(r, 1).Value = "TỔNG CỘNG";
        ws.Cell(r, 1).Style.Font.Bold = true;
        SetMoney(ws.Cell(r, 5), agg.SumAdvance);
        SetMoney(ws.Cell(r, 6), agg.SumKLHT);
        SetMoney(ws.Cell(r, 7), agg.SumRecovered);
        SetMoney(ws.Cell(r, 8), agg.SumNSNN);
        SetMoney(ws.Cell(r, 9), agg.SumNgoaiHoi);
        SetMoney(ws.Cell(r, 10), agg.TotalDisbursed);
        ws.Row(r).Style.Font.Bold = true;
        ws.Cell(r + 1, 2).Value = $"TU chưa thu hồi: {agg.UnrecoveredAdvance:#,##0}";

        ws.Columns().AdjustToContents();
    }

    private static void SetMoney(IXLCell cell, decimal value)
    {
        cell.Value = value;
        cell.Style.NumberFormat.Format = MoneyFormat;
    }

    /// Excel sheet names ≤31 chars, no []:*?/\, unique within the workbook.
    private static string UniqueSheetName(string raw, HashSet<string> used)
    {
        var cleaned = new string((raw ?? "DA").Where(ch => !"[]:*?/\\".Contains(ch)).ToArray()).Trim();
        if (cleaned.Length == 0) cleaned = "DA";
        if (cleaned.Length > 31) cleaned = cleaned[..31];
        var name = cleaned;
        var i = 2;
        while (!used.Add(name))
        {
            var suffix = $" ({i++})";
            name = cleaned.Length + suffix.Length > 31 ? cleaned[..(31 - suffix.Length)] + suffix : cleaned + suffix;
        }
        return name;
    }
}
