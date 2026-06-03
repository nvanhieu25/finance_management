using ClosedXML.Excel;
using ConstructionFinance.Application.DTOs;
using ConstructionFinance.Application.Services.Interfaces;

namespace ConstructionFinance.Application.Services.Implementations;

public class ExcelExportService : IExcelExportService
{
    public async Task<string> ExportTongHopAsync(
        string templatePath,
        string targetPath,
        IReadOnlyList<ProjectSummaryDto> summaries,
        CancellationToken ct = default)
    {
        if (!File.Exists(templatePath))
            throw new FileNotFoundException("Template file not found", templatePath);

        File.Copy(templatePath, targetPath, overwrite: true);

        await Task.Run(() =>
        {
            using var wb = new XLWorkbook(targetPath);
            var sheet = wb.Worksheets.FirstOrDefault(w =>
                w.Name.Equals("Tổng hợp", StringComparison.OrdinalIgnoreCase) ||
                w.Name.Equals("TH", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("Template không có sheet 'Tổng hợp' hoặc 'TH'.");

            const int firstDataRow = 9;
            var rowIdx = firstDataRow;

            var totalRow = sheet.RowsUsed()
                .Where(r => r.Cell(2).GetString().Trim().Equals("Tổng cộng", StringComparison.OrdinalIgnoreCase))
                .Select(r => r.RowNumber())
                .FirstOrDefault();

            if (totalRow > firstDataRow)
            {
                for (var r = firstDataRow; r < totalRow; r++)
                {
                    for (var c = 1; c <= 20; c++)
                        sheet.Cell(r, c).Clear(XLClearOptions.Contents);
                }
            }

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

            wb.Save();
        }, ct);

        return targetPath;
    }
}
