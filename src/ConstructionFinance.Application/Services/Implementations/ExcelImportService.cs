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
    private const int MaxScannedColumns = 100;

    private static readonly Regex DaSheetPattern =
        new(@"^(da|dự\s*án|project)\s*\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static readonly Regex YearPattern =
        new(@"quyết\s*toán\s*(?:đến\s*)?năm\s+(\d{4})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private enum DaTemplate { H_xlsx, Hieu_xlsx }

    public async Task<ImportResultDto> ParseAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Excel file not found", filePath);

        var hash = await ComputeSha256Async(filePath, ct);
        var fileName = Path.GetFileName(filePath);

        return await Task.Run(() =>
        {
            // FileShare.ReadWrite so user can keep the .xlsx open in Excel/LibreOffice during import.
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var wb = new XLWorkbook(fs);
            var warnings = new List<string>();
            var summaries = new List<ProjectSummaryDto>();
            var daSheets = wb.Worksheets.Where(w => DaSheetPattern.IsMatch(NormalizeSheetName(w.Name))).ToList();

            foreach (var sheet in daSheets)
            {
                try
                {
                    ct.ThrowIfCancellationRequested();
                    var summary = ParseDaSheet(sheet, warnings);
                    if (summary is not null) summaries.Add(summary);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    warnings.Add($"Sheet '{sheet.Name}': {ex.Message}");
                }
            }

            var tongHopSheet = wb.Worksheets.FirstOrDefault(w =>
                w.Name.Trim().Equals("Tổng hợp", StringComparison.OrdinalIgnoreCase) ||
                w.Name.Trim().Equals("TH", StringComparison.OrdinalIgnoreCase));
            if (tongHopSheet is not null)
                summaries = RefineFundingFromTongHop(tongHopSheet, summaries, warnings);

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

        var template = DetectDaTemplate(sheet, totalRowIdx.Value);
        var row = sheet.Row(totalRowIdx.Value);
        var sheetName = sheet.Name;

        decimal ReadCol(string col) => GetDecimal(row.Cell(col), warnings, sheetName);

        var (contractValue, advance, paymentKLHT, recoveredAdvance, paymentToContractor,
             unrecoveredAdvance, totalDisbursed, disbursedNSNN, disbursedDTNH) = template switch
        {
            DaTemplate.Hieu_xlsx => (
                // Cột C = Giá trị HĐ gốc, D = Phụ lục HĐ (giá trị sau điều chỉnh).
                // Phụ lục thay thế chứ không cộng dồn → ưu tiên D, fallback C nếu D trống.
                ContractValue: ReadCol("D") != 0m ? ReadCol("D") : ReadCol("C"),
                Advance: ReadCol("E"),
                PaymentKLHT: ReadCol("F"),
                RecoveredAdvance: ReadCol("G"),
                PaymentToContractor: 0m,
                UnrecoveredAdvance: ReadCol("J"),
                TotalDisbursed: ReadCol("K"),
                DisbursedNSNN: ReadCol("H"),
                DisbursedDTNH: ReadCol("I")),
            _ => (
                ContractValue: ReadCol("D"),
                Advance: ReadCol("F"),
                PaymentKLHT: ReadCol("G"),
                RecoveredAdvance: ReadCol("H"),
                PaymentToContractor: ReadCol("I"),
                UnrecoveredAdvance: ReadCol("J"),
                TotalDisbursed: ReadCol("K"),
                DisbursedNSNN: ReadCol("K"),
                DisbursedDTNH: 0m)
        };

        var (headerRow, subRow) = template == DaTemplate.Hieu_xlsx ? (2, 3) : (4, 5);
        var yearCols = DetectYearColumns(sheet, headerRow, subRow, warnings);
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

    private static DaTemplate DetectDaTemplate(IXLWorksheet sheet, int totalRowIdx)
    {
        var hieuScore = 0;
        var hScore = 0;
        var a2 = sheet.Cell(2, 1).GetString().Trim();
        var b2 = sheet.Cell(2, 2).GetString().Trim();
        var c2 = sheet.Cell(2, 3).GetString().Trim();

        if (a2.Contains("Ngày tháng", StringComparison.OrdinalIgnoreCase)) hieuScore++;
        if (b2.Contains("Tên gói thầu", StringComparison.OrdinalIgnoreCase)) hieuScore++;
        if (c2.Contains("Hợp đồng", StringComparison.OrdinalIgnoreCase)) hieuScore++;
        if (totalRowIdx == 4) hieuScore++;

        var a4 = sheet.Cell(4, 1).GetString().Trim();
        var b4 = sheet.Cell(4, 2).GetString().Trim();
        if (a4.Equals("TT", StringComparison.OrdinalIgnoreCase)) hScore++;
        if (b4.Contains("Nội dung", StringComparison.OrdinalIgnoreCase)) hScore++;
        if (totalRowIdx == 6) hScore++;

        return hieuScore > hScore ? DaTemplate.Hieu_xlsx : DaTemplate.H_xlsx;
    }

    private static int? FindTotalRow(IXLWorksheet sheet)
    {
        static bool IsTotal(string s)
        {
            var t = s.Trim().TrimEnd(':').Trim();
            return t.Equals("TỔNG CỘNG", StringComparison.OrdinalIgnoreCase) ||
                   t.Equals("Tổng cộng", StringComparison.OrdinalIgnoreCase);
        }

        for (var r = 3; r <= 12; r++)
        {
            if (IsTotal(sheet.Cell(r, 1).GetString()) ||
                IsTotal(sheet.Cell(r, 2).GetString()))
                return r;
        }
        return null;
    }

    private static Dictionary<int, Dictionary<string, string>> DetectYearColumns(
        IXLWorksheet sheet, int headerRow, int subHeaderRow, List<string> warnings)
    {
        var result = new Dictionary<int, Dictionary<string, string>>();
        var rowHeader = sheet.Row(headerRow);
        var rowSub = sheet.Row(subHeaderRow);
        var lastCol = Math.Min(sheet.LastColumnUsed()?.ColumnNumber() ?? 0, MaxScannedColumns);

        for (var c = 1; c <= lastCol; c++)
        {
            var headerText = rowHeader.Cell(c).GetString().Trim();
            var match = YearPattern.Match(headerText);
            if (!match.Success) continue;

            var year = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var spanStart = c;
            var spanEnd = c;
            var merged = rowHeader.Cell(c).MergedRange();
            if (merged is not null) spanEnd = merged.LastColumn().ColumnNumber();

            var subCols = new Dictionary<string, string>();
            for (var cc = spanStart; cc <= spanEnd; cc++)
            {
                var label = rowSub.Cell(cc).GetString().Trim().ToLowerInvariant();
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

    private static readonly Regex SheetRefPattern =
        new(@"'?([^'!]+?)'?!", RegexOptions.CultureInvariant);

    private static List<ProjectSummaryDto> RefineFundingFromTongHop(
        IXLWorksheet tongHopSheet, List<ProjectSummaryDto> summaries, List<string> warnings)
    {
        // Detect TH layout: H.xlsx has data rows 9+ with NSNN at col J(10), DTNH at K(11).
        // Hiệu.xlsx has data rows 5+ with NSNN at col G(7), DTNH at H(8). Probe row 2 header.
        var (firstDataRow, nsnnCol, dtnhCol) = DetectTongHopLayout(tongHopSheet);

        var bySheetRef = new Dictionary<string, (decimal nsnn, decimal dtnh)>(StringComparer.OrdinalIgnoreCase);
        var byName = new Dictionary<string, (decimal nsnn, decimal dtnh)>(StringComparer.OrdinalIgnoreCase);

        decimal SafeRead(IXLCell cell)
        {
            if (cell.IsEmpty()) return 0m;
            if (cell.HasFormula && cell.Value.IsError) return 0m;
            try { return (decimal)cell.GetDouble(); } catch { return 0m; }
        }

        var lastRow = Math.Min(tongHopSheet.LastRowUsed()?.RowNumber() ?? 0, firstDataRow + 50);
        for (var r = firstDataRow; r <= lastRow; r++)
        {
            var name = tongHopSheet.Cell(r, 2).GetString().Trim();
            if (string.IsNullOrEmpty(name)) continue;
            if (name.Equals("Tổng cộng", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Tổng cộng:", StringComparison.OrdinalIgnoreCase)) break;

            var nsnn = SafeRead(tongHopSheet.Cell(r, nsnnCol));
            var dtnh = SafeRead(tongHopSheet.Cell(r, dtnhCol));
            byName[name] = (nsnn, dtnh);

            // Also index by referenced sheet name extracted from any formula in the row.
            for (var c = 4; c <= 15; c++)
            {
                var cell = tongHopSheet.Cell(r, c);
                if (!cell.HasFormula) continue;
                var match = SheetRefPattern.Match(cell.FormulaA1);
                if (match.Success)
                {
                    bySheetRef[match.Groups[1].Value.Trim()] = (nsnn, dtnh);
                    break;
                }
            }
        }

        var refined = new List<ProjectSummaryDto>(summaries.Count);
        foreach (var s in summaries)
        {
            var updated = TryRefine(s, bySheetRef, byName);
            if (updated is null && s.TotalDisbursed > 0)
                warnings.Add($"Dự án '{s.ProjectName}' (sheet '{s.SheetName}'): không tìm thấy phân tách NSNN/DTNH.");
            refined.Add(updated ?? s);
        }
        return refined;
    }

    private static ProjectSummaryDto? TryRefine(
        ProjectSummaryDto s,
        Dictionary<string, (decimal nsnn, decimal dtnh)> bySheetRef,
        Dictionary<string, (decimal nsnn, decimal dtnh)> byName)
    {
        // Project already has both funding sources — leave it as-is, no warning.
        if (s.DisbursedDTNH != 0m || s.DisbursedNSNN != s.TotalDisbursed) return s;
        if (bySheetRef.TryGetValue(s.SheetName, out var a) && a.nsnn + a.dtnh > 0)
            return s with { DisbursedNSNN = a.nsnn, DisbursedDTNH = a.dtnh };
        if (byName.TryGetValue(s.ProjectName, out var b) && b.nsnn + b.dtnh > 0)
            return s with { DisbursedNSNN = b.nsnn, DisbursedDTNH = b.dtnh };
        return null;
    }

    private static (int firstDataRow, int nsnnCol, int dtnhCol) DetectTongHopLayout(IXLWorksheet sheet)
    {
        // Hiệu.xlsx TH: row 2 = headers, NSNN at col G(7), Ngoại hối at H(8), data from row 5.
        // H.xlsx Tổng hợp: row 7-8 = headers, NSNN at col J(10), DTNH at K(11), data from row 9.
        var r2a = sheet.Cell(2, 1).GetString().Trim();
        if (r2a.Equals("STT", StringComparison.OrdinalIgnoreCase) ||
            sheet.Cell(2, 2).GetString().Trim().Equals("Dự án", StringComparison.OrdinalIgnoreCase))
            return (firstDataRow: 5, nsnnCol: 7, dtnhCol: 8);
        return (firstDataRow: 9, nsnnCol: 10, dtnhCol: 11);
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
