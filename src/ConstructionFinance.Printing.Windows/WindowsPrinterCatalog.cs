using System.Drawing.Printing;
using ConstructionFinance.Application.Printing;

namespace ConstructionFinance.Printing.Windows;

/// Danh sách máy in lấy từ System.Drawing.Printing. CHỈ CHẠY TRÊN WINDOWS — trên Linux các
/// thành viên này ném PlatformNotSupportedException, nên lớp này không có test chạy; nó chỉ
/// được biên dịch.
public sealed class WindowsPrinterCatalog : IPrinterCatalog
{
    public IReadOnlyList<string> InstalledPrinters
    {
        get
        {
            var names = new List<string>();
            foreach (string? name in PrinterSettings.InstalledPrinters)
                if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
            return names;
        }
    }

    /// PrinterSettings mới mang tên máy in mặc định của hệ điều hành; chuỗi rỗng nghĩa là máy
    /// chưa cài máy in nào.
    public string? DefaultPrinterName
    {
        get
        {
            var name = new PrinterSettings().PrinterName;
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
    }
}
