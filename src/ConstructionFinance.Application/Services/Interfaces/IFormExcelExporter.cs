using ConstructionFinance.Application.Forms;

namespace ConstructionFinance.Application.Services.Interfaces;

public interface IFormExcelExporter
{
    Task ExportThongTriAsync(ThongTriForm form, string targetPath, CancellationToken ct = default);
    Task ExportKsChiAsync(KsChiForm form, string targetPath, CancellationToken ct = default);
    Task ExportGdnttAsync(GdnttForm form, string targetPath, CancellationToken ct = default);
}
