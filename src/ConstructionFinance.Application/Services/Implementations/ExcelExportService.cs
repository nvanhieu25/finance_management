using ClosedXML.Excel;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;

namespace ConstructionFinance.Application.Services.Implementations;

public class ExcelExportService : IExcelExportService
{
    private enum ThTemplate { H_xlsx, Hieu_xlsx }

    public async Task<string> ExportTongHopAsync(
        string templatePath,
        string targetPath,
        IReadOnlyList<ProjectSummaryDto> summaries,
        CancellationToken ct = default)
    {
        if (!File.Exists(templatePath))
            throw new FileNotFoundException("Template file not found", templatePath);
        if (string.Equals(Path.GetFullPath(templatePath), Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Target path không được trùng template path.");

        File.Copy(templatePath, targetPath, overwrite: true);

        await Task.Run(() =>
        {
            // FileShare.ReadWrite so user can have template open elsewhere.
            using var fs = new FileStream(targetPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            using var wb = new XLWorkbook(fs);
            var sheet = wb.Worksheets.FirstOrDefault(w =>
                w.Name.Equals("Tổng hợp", StringComparison.OrdinalIgnoreCase) ||
                w.Name.Equals("TH", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("Template không có sheet 'Tổng hợp' hoặc 'TH'.");

            var template = DetectThTemplate(sheet);
            ClearAndWriteRows(sheet, summaries, template);
            wb.Save();
        }, ct);

        return targetPath;
    }

    private static ThTemplate DetectThTemplate(IXLWorksheet sheet)
    {
        // Hiệu.xlsx TH: row 2 has "STT" or "Dự án" headers, data from row 5.
        // H.xlsx Tổng hợp: row 7-8 are merged headers, data from row 9.
        var r2 = sheet.Cell(2, 1).GetString().Trim();
        var r2b = sheet.Cell(2, 2).GetString().Trim();
        if (r2.Equals("STT", StringComparison.OrdinalIgnoreCase) ||
            r2b.Equals("Dự án", StringComparison.OrdinalIgnoreCase))
            return ThTemplate.Hieu_xlsx;
        return ThTemplate.H_xlsx;
    }

    private static void ClearAndWriteRows(IXLWorksheet sheet, IReadOnlyList<ProjectSummaryDto> summaries, ThTemplate template)
    {
        var (firstDataRow, lastCol) = template == ThTemplate.Hieu_xlsx ? (5, 13) : (9, 17);

        // Find existing TỔNG CỘNG row to stop clearing before it.
        var totalRow = sheet.RowsUsed()
            .Where(r =>
            {
                var b = r.Cell(2).GetString().Trim().TrimEnd(':').Trim();
                return b.Equals("Tổng cộng", StringComparison.OrdinalIgnoreCase);
            })
            .Select(r => r.RowNumber())
            .FirstOrDefault();

        // For Hiệu.xlsx, Tổng cộng is at row 4 (BEFORE data). For H.xlsx, at row 28+ (AFTER data).
        // Clear in the appropriate range.
        var clearEnd = totalRow > firstDataRow ? totalRow - 1 : firstDataRow + 50;
        for (var r = firstDataRow; r <= clearEnd; r++)
            for (var c = 1; c <= lastCol; c++)
                sheet.Cell(r, c).Clear(XLClearOptions.Contents);

        if (template == ThTemplate.Hieu_xlsx)
            WriteHieuRows(sheet, summaries, firstDataRow);
        else
            WriteHRows(sheet, summaries, firstDataRow);
    }

    private static void WriteHieuRows(IXLWorksheet sheet, IReadOnlyList<ProjectSummaryDto> summaries, int firstDataRow)
    {
        // Hiệu TH cols: A=STT, B=Dự án, C=Nhà thầu, D=Tạm ứng,
        //   E=TT KLHT, F=Thu ứng, G=NSNN, H=Ngoại hối,
        //   I=Tạm ứng chưa TH, J=Tổng GT,
        //   K=Quyết toán 2026 NSNN, L=Quyết toán 2026 NH.
        var rowIdx = firstDataRow;
        var stt = 1;
        foreach (var s in summaries)
        {
            sheet.Cell(rowIdx, 1).Value = stt++;
            sheet.Cell(rowIdx, 2).Value = s.ProjectName;
            sheet.Cell(rowIdx, 3).Value = s.ContractorName ?? "";
            sheet.Cell(rowIdx, 4).Value = s.Advance;
            sheet.Cell(rowIdx, 5).Value = s.PaymentKLHT;
            sheet.Cell(rowIdx, 6).Value = s.RecoveredAdvance;
            sheet.Cell(rowIdx, 7).Value = s.DisbursedNSNN;
            sheet.Cell(rowIdx, 8).Value = s.DisbursedDTNH;
            sheet.Cell(rowIdx, 9).Value = s.UnrecoveredAdvance;
            sheet.Cell(rowIdx, 10).Value = s.TotalDisbursed;

            // Year settlement — Hiệu uses 1 year (2026): K=NSNN, L=NH (DTNH).
            var year2026 = s.YearlySettlements.FirstOrDefault(y => y.Year == 2026)
                           ?? s.YearlySettlements.OrderBy(y => y.Year).LastOrDefault();
            if (year2026 is not null)
            {
                if (year2026.NSNN.HasValue) sheet.Cell(rowIdx, 11).Value = year2026.NSNN.Value;
                if (year2026.DTNH.HasValue) sheet.Cell(rowIdx, 12).Value = year2026.DTNH.Value;
            }
            rowIdx++;
        }
    }

    private static void WriteHRows(IXLWorksheet sheet, IReadOnlyList<ProjectSummaryDto> summaries, int firstDataRow)
    {
        // H.xlsx Tổng hợp cols: A=STT, B=Dự án, C=NCC, D=GT HĐ, E=Tạm ứng,
        //   F=TT KLHT, G=Thu hồi TƯ, H=TT cho NT, I=Tạm ứng chưa TH,
        //   J=NSNN, K=DTNH, L=Tổng GN,
        //   M-O=Quyết toán 2025 (KLHT, Advance, DTNH),
        //   P-Q=Quyết toán 2026 (NSNN, DTNH).
        var rowIdx = firstDataRow;
        var stt = 1;
        foreach (var s in summaries)
        {
            sheet.Cell(rowIdx, 1).Value = stt++;
            sheet.Cell(rowIdx, 2).Value = s.ProjectName;
            sheet.Cell(rowIdx, 3).Value = s.ContractorName ?? "";
            sheet.Cell(rowIdx, 4).Value = s.ContractValue;
            sheet.Cell(rowIdx, 5).Value = s.Advance;
            sheet.Cell(rowIdx, 6).Value = s.PaymentKLHT;
            sheet.Cell(rowIdx, 7).Value = s.RecoveredAdvance;
            sheet.Cell(rowIdx, 8).Value = s.PaymentToContractor;
            sheet.Cell(rowIdx, 9).Value = s.UnrecoveredAdvance;
            sheet.Cell(rowIdx, 10).Value = s.DisbursedNSNN;
            sheet.Cell(rowIdx, 11).Value = s.DisbursedDTNH;
            sheet.Cell(rowIdx, 12).Value = s.TotalDisbursed;

            var years = s.YearlySettlements.OrderBy(y => y.Year).ToList();
            if (years.Count >= 1)
            {
                var y1 = years[0];
                if (y1.KLHT.HasValue) sheet.Cell(rowIdx, 13).Value = y1.KLHT.Value;
                if (y1.Advance.HasValue) sheet.Cell(rowIdx, 14).Value = y1.Advance.Value;
                if (y1.DTNH.HasValue) sheet.Cell(rowIdx, 15).Value = y1.DTNH.Value;
            }
            if (years.Count >= 2)
            {
                var y2 = years[1];
                if (y2.NSNN.HasValue) sheet.Cell(rowIdx, 16).Value = y2.NSNN.Value;
                if (y2.DTNH.HasValue) sheet.Cell(rowIdx, 17).Value = y2.DTNH.Value;
            }
            rowIdx++;
        }
    }
}
