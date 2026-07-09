using System.Text.RegularExpressions;
using ClosedXML.Excel;
using ConstructionFinance.Application.Import;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Finance;

namespace ConstructionFinance.Application.Services.Implementations;

public class FinanceImportService : IFinanceImportService
{
    private static readonly Regex DaSheetPattern =
        new(@"^(da|dự\s*án|project)\s*\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public Task<FinanceImportResult> ParseWorkbookAsync(string filePath, CancellationToken ct = default) =>
        Task.Run(() => Parse(filePath), ct);

    private static FinanceImportResult Parse(string filePath)
    {
        var warnings = new List<string>();
        var projects = new List<Project>();

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var wb = new XLWorkbook(fs);

        var thSheet = wb.Worksheets.FirstOrDefault(w =>
            w.Name.Equals("TH", StringComparison.OrdinalIgnoreCase) ||
            w.Name.Equals("Tổng hợp", StringComparison.OrdinalIgnoreCase));

        var daSheets = wb.Worksheets
            .Where(w => DaSheetPattern.IsMatch(w.Name.Trim()))
            .ToList();
        if (daSheets.Count == 0)
            warnings.Add("Không tìm thấy sheet DA nào trong file.");

        foreach (var sheet in daSheets)
        {
            var totalRow = FindTotalRow(sheet);
            if (totalRow is null)
            {
                warnings.Add($"Sheet '{sheet.Name}': không thấy dòng 'Tổng cộng' — bỏ qua.");
                continue;
            }

            var dataStart = totalRow.Value + 1;
            var lastRow = Math.Min(sheet.LastRowUsed()?.RowNumber() ?? dataStart, totalRow.Value + 40);

            var project = new Project { Name = sheet.Name.Trim() };
            var contractCaptured = false;
            var sort = 0;

            for (var r = dataStart; r <= lastRow; r++)
            {
                var row = sheet.Row(r);
                var c = GetDecimal(row.Cell(3), warnings, sheet.Name);   // C Hợp đồng
                var d = GetDecimal(row.Cell(4), warnings, sheet.Name);   // D Phụ lục HĐ
                var e = GetDecimal(row.Cell(5), warnings, sheet.Name);   // E Tạm ứng
                var f = GetDecimal(row.Cell(6), warnings, sheet.Name);   // F TT KLHT
                var g = GetDecimal(row.Cell(7), warnings, sheet.Name);   // G Thu ứng
                var h = GetDecimal(row.Cell(8), warnings, sheet.Name);   // H NSNN (cached value)
                var i = GetDecimal(row.Cell(9), warnings, sheet.Name);   // I Ngoại hối
                var b = row.Cell(2).GetString().Trim();                  // B name / contract ref
                var date = GetDateOnly(row.Cell(1));                     // A Ngày

                var isContractRow = !contractCaptured && (c != 0m || d != 0m) && e == 0m && f == 0m && g == 0m;
                if (isContractRow)
                {
                    project.ContractValue = c;
                    project.ContractAnnexValue = d;
                    project.Notes = string.IsNullOrWhiteSpace(b) ? null : b;
                    contractCaptured = true;
                    continue;
                }

                var hasAmount = e != 0m || f != 0m || g != 0m || h != 0m || i != 0m;
                if (!hasAmount && date is null) continue; // blank row

                project.Lines.Add(new ProjectLineItem
                {
                    SortOrder = ++sort,
                    Date = date ?? default,
                    PackageName = string.IsNullOrWhiteSpace(b) ? null : b,
                    Advance = e,
                    PaymentKLHT = f,
                    RecoveredAdvance = g,
                    DisbursedNSNN = h,
                    DisbursedNgoaiHoi = i
                });
            }

            project.ContractorName = LookupContractor(thSheet, sheet.Name);
            projects.Add(project);
        }

        return new FinanceImportResult(projects, warnings);
    }

    private static int? FindTotalRow(IXLWorksheet sheet)
    {
        static bool IsTotal(string s)
        {
            var t = s.Trim().TrimEnd(':').Trim();
            return t.Equals("Tổng cộng", StringComparison.OrdinalIgnoreCase);
        }
        for (var r = 2; r <= 8; r++)
            if (IsTotal(sheet.Cell(r, 1).GetString()) || IsTotal(sheet.Cell(r, 2).GetString()))
                return r;
        return null;
    }

    private static string? LookupContractor(IXLWorksheet? th, string daSheetName)
    {
        if (th is null) return null;
        var lastRow = Math.Min(th.LastRowUsed()?.RowNumber() ?? 0, 60);
        for (var r = 2; r <= lastRow; r++)
        {
            if (th.Cell(r, 2).GetString().Trim().Equals(daSheetName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                var c = th.Cell(r, 3).GetString().Trim();
                return string.IsNullOrWhiteSpace(c) ? null : c;
            }
        }
        return null;
    }

    private static DateOnly? GetDateOnly(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;
        try
        {
            if (cell.DataType == XLDataType.DateTime || cell.TryGetValue<DateTime>(out _))
                return DateOnly.FromDateTime(cell.GetDateTime());
        }
        catch { /* not a date */ }
        return null;
    }

    private static decimal GetDecimal(IXLCell cell, List<string> warnings, string sheetName)
    {
        if (cell.IsEmpty()) return 0m;
        if (cell.HasFormula && cell.Value.IsError)
        {
            warnings.Add($"Sheet '{sheetName}' ô {cell.Address}: công thức lỗi — tính 0.");
            return 0m;
        }
        try { return (decimal)cell.GetDouble(); }
        catch { return 0m; } // text/blank cells in a numeric column → 0
    }
}
