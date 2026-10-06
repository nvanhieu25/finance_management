using MigraDoc.DocumentObjectModel;

namespace ConstructionFinance.Application.Printing;

/// Danh sách máy in của máy tính. Tách ra interface để hộp thoại chọn máy in test được trên
/// máy không có máy in nào.
public interface IPrinterCatalog
{
    IReadOnlyList<string> InstalledPrinters { get; }

    /// Máy in mặc định của hệ điều hành, hoặc null nếu máy chưa cài máy in nào.
    string? DefaultPrinterName { get; }
}

/// Một lệnh in. Số bản được KẸP ngay trong hàm dựng, không để lớp giao diện tự lo:
/// PrinterSettings.Copies là short, nên một con số to sẽ tràn âm trước khi tới trình điều khiển.
public sealed record PrinterJob
{
    public const int MinCopies = 1;
    public const int MaxCopies = 99;

    public PrinterJob(Document document, string printerName, int copies)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrWhiteSpace(printerName))
            throw new ArgumentException("Lệnh in phải có tên máy in.", nameof(printerName));

        Document = document;
        PrinterName = printerName;
        Copies = Math.Clamp(copies, MinCopies, MaxCopies);
    }

    public Document Document { get; }
    public string PrinterName { get; }

    /// Luôn nằm trong [MinCopies, MaxCopies].
    public int Copies { get; }
}

/// Máy in báo lỗi giữa lúc in. Mang tên máy in theo, để câu thông báo nói đúng sự thật về
/// việc người dùng vừa làm thay vì một câu chung chung.
public sealed class PrintJobFailedException : Exception
{
    public PrintJobFailedException(string printerName, Exception innerException)
        : base($"Máy in \"{printerName}\" báo lỗi: {innerException.Message}", innerException)
    {
        PrinterName = printerName;
    }

    public string PrinterName { get; }
}

/// Đưa một tài liệu MigraDoc ra giấy. Bản cài đặt duy nhất nằm ở
/// ConstructionFinance.Printing.Windows và chỉ có ở đích net8.0-windows; ở đích net8.0 (bộ
/// test) KHÔNG có bản nào được đăng ký, nên hai lệnh In ra giấy mờ đi.
public interface IPrinterPort
{
    /// Ném PrintJobFailedException khi máy in hoặc trình điều khiển báo lỗi.
    Task PrintAsync(PrinterJob job, CancellationToken ct = default);
}
