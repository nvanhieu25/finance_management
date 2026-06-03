using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;

namespace ConstructionFinance.Application.Services.Implementations;

public class ExcelImportService : IExcelImportService
{
    private static readonly Regex DaSheetPattern =
        new(@"^(da|dự\s*án|project)\s*\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex YearPattern =
        new(@"quyết\s*toán\s*(đến\s*)?năm\s*(\d{4})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public async Task<ImportResultDto> ParseAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Excel file not found", filePath);

        var hash = await ComputeSha256Async(filePath, ct);
        var fileName = Path.GetFileName(filePath);

        return await Task.Run(() =>
        {
            using var wb = new XLWorkbook(filePath);
            var warnings = new List<string>();
            var summaries = new List<ProjectSummaryDto>();
            var daSheets = wb.Worksheets.Where(w => DaSheetPattern.IsMatch(NormalizeSheetName(w.Name))).ToList();

            foreach (var sheet in daSheets)
            {
                try
                {
                    var summary = ParseDaSheet(sheet, warnings);
                    if (summary is not null) summaries.Add(summary);
                }
                catch (Exception ex)
                {
                    warnings.Add($"Sheet '{sheet.Name}': {ex.Message}");
                }
            }

            return new ImportResultDto(
                SourceFileName: fileName,
                SourceFileHash: hash,
                SheetsFound: wb.Worksheets.Count,
                Summaries: summaries,
                Warnings: warnings);
        }, ct);
    }

    private static string NormalizeSheetName(string name) =>
        name.Trim().Normalize(NormalizationForm.FormD);

    private ProjectSummaryDto? ParseDaSheet(IXLWorksheet sheet, List<string> warnings)
    {
        var totalRowIdx = FindTotalRow(sheet);
        if (totalRowIdx is null)
        {
            warnings.Add($"Sheet '{sheet.Name}': không tìm thấy dòng TỔNG CỘNG.");
            return null;
        }

        var row = sheet.Row(totalRowIdx.Value);
        var sheetName = sheet.Name;

        decimal ReadCol(string col) => GetDecimal(row.Cell(col), warnings, sheetName);

        var contractValue = ReadCol("D");
        var advance = ReadCol("F");
        var paymentKLHT = ReadCol("G");
        var recoveredAdvance = ReadCol("H");
        var paymentToContractor = ReadCol("I");
        var unrecoveredAdvance = ReadCol("J");
        var totalDisbursed = ReadCol("K");
        var disbursedNSNN = totalDisbursed;
        var disbursedDTNH = 0m;

        var yearCols = DetectYearColumns(sheet, warnings);
        var yearlySettlements = new List<SettlementByYearDto>();
        foreach (var (year, cols) in yearCols)
        {
            yearlySettlements.Add(new SettlementByYearDto(
                Year: year,
                KLHT: cols.TryGetValue("klht", out var klhtCol) ? ReadCol(klhtCol) : null,
                Advance: cols.TryGetValue("ứng", out var advCol) ? ReadCol(advCol) : null,
                NSNN: cols.TryGetValue("nsnn", out var nsnnCol) ? ReadCol(nsnnCol) : null,
                DTNH: cols.TryGetValue("dtnh", out var dtnhCol) ? ReadCol(dtnhCol) : null));
        }

        var projectName = TryReadProjectName(sheet) ?? sheet.Name;
        var contractorName = TryReadContractorName(sheet);

        return new ProjectSummaryDto(
            SheetName: sheet.Name,
            ProjectName: projectName,
            ContractorName: contractorName,
            ContractValue: contractValue,
            Advance: advance,
            PaymentKLHT: paymentKLHT,
            RecoveredAdvance: recoveredAdvance,
            PaymentToContractor: paymentToContractor,
            UnrecoveredAdvance: unrecoveredAdvance,
            DisbursedNSNN: disbursedNSNN,
            DisbursedDTNH: disbursedDTNH,
            TotalDisbursed: totalDisbursed,
            YearlySettlements: yearlySettlements);
    }

    private static int? FindTotalRow(IXLWorksheet sheet)
    {
        for (var r = 4; r <= 12; r++)
        {
            var a = sheet.Cell(r, 1).GetString().Trim();
            var b = sheet.Cell(r, 2).GetString().Trim();
            if (a.Equals("TỔNG CỘNG", StringComparison.OrdinalIgnoreCase) ||
                b.Equals("Tổng cộng", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("Tổng cộng", StringComparison.OrdinalIgnoreCase))
                return r;
        }
        return null;
    }

    private static Dictionary<int, Dictionary<string, string>> DetectYearColumns(IXLWorksheet sheet, List<string> warnings)
    {
        var result = new Dictionary<int, Dictionary<string, string>>();
        var row4 = sheet.Row(4);
        var row5 = sheet.Row(5);
        var lastCol = Math.Min(sheet.LastColumnUsed()?.ColumnNumber() ?? 0, 40);

        for (var c = 1; c <= lastCol; c++)
        {
            var header4 = row4.Cell(c).GetString().Trim();
            var match = YearPattern.Match(header4);
            if (!match.Success) continue;

            var year = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var spanStart = c;
            var spanEnd = c;
            var merged = row4.Cell(c).MergedRange();
            if (merged is not null) spanEnd = merged.LastColumn().ColumnNumber();

            var subCols = new Dictionary<string, string>();
            for (var cc = spanStart; cc <= spanEnd; cc++)
            {
                var label = row5.Cell(cc).GetString().Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(label)) continue;
                string? key = label switch
                {
                    var s when s.Contains("klht") || s.Contains("khối lượng") => "klht",
                    var s when s.Contains("ứng") || s.Contains("tạm ứng") => "ứng",
                    var s when s.Contains("nsnn") || s.Contains("dự phòng") => "nsnn",
                    var s when s.Contains("dtnh") || s.Contains("ngoại hối") || s.Contains("qdtnh") => "dtnh",
                    _ => null
                };
                if (key is not null) subCols[key] = XLHelper.GetColumnLetterFromNumber(cc);
            }

            if (subCols.Count > 0) result[year] = subCols;
            else warnings.Add($"Sheet '{sheet.Name}': năm {year} không tìm thấy cột phụ.");

            c = spanEnd;
        }

        return result;
    }

    private static decimal GetDecimal(IXLCell cell, List<string> warnings, string sheetName)
    {
        if (cell.IsEmpty()) return 0m;
        if (cell.HasFormula && cell.Value.IsError)
        {
            warnings.Add($"Sheet '{sheetName}' ô {cell.Address}: công thức lỗi — giá trị tính 0.");
            return 0m;
        }
        try
        {
            return (decimal)cell.GetDouble();
        }
        catch
        {
            warnings.Add($"Sheet '{sheetName}' ô {cell.Address}: không đọc được số — giá trị tính 0.");
            return 0m;
        }
    }

    private static string? TryReadProjectName(IXLWorksheet sheet)
    {
        foreach (var addr in new[] { "A2", "B2", "A1", "B1" })
        {
            var text = sheet.Cell(addr).GetString().Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (text.StartsWith("Tên dự án", StringComparison.OrdinalIgnoreCase))
            {
                var idx = text.IndexOf(':');
                if (idx >= 0 && idx < text.Length - 1)
                    return text[(idx + 1)..].Trim();
            }
            if (!text.Contains("dự án", StringComparison.OrdinalIgnoreCase) && text.Length > 3)
                return text;
        }
        return null;
    }

    private static string? TryReadContractorName(IXLWorksheet sheet)
    {
        for (var r = 7; r <= 30; r++)
        {
            var v = sheet.Cell(r, 3).GetString().Trim();
            if (string.IsNullOrWhiteSpace(v)) continue;
            if (v.Equals("Đơn vị Thi công (Nhà thầu)", StringComparison.OrdinalIgnoreCase)) continue;
            return v;
        }
        return null;
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        await using var fs = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(fs, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
