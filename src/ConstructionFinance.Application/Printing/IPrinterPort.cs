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

    /// Tên dùng khi bên gọi không biết tên dự án. Vẫn phải khác "document" — mặc định của
    /// PrintDocument — vì đó chính là cái tên vô nghĩa cần tránh.
    public const string DefaultDocumentName = "Quản lý Tài chính Công trình";

    public PrinterJob(Document document, string printerName, int copies, string? documentName = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrWhiteSpace(printerName))
            throw new ArgumentException("Lệnh in phải có tên máy in.", nameof(printerName));

        Document = document;
        PrinterName = printerName;
        Copies = Math.Clamp(copies, MinCopies, MaxCopies);
        DocumentName = string.IsNullOrWhiteSpace(documentName)
            ? DefaultDocumentName
            : documentName.Trim();
    }

    public Document Document { get; }
    public string PrinterName { get; }

    /// Luôn nằm trong [MinCopies, MaxCopies].
    public int Copies { get; }

    /// Tên lệnh in hiện trong hàng đợi máy in của Windows và trong nhật ký in. Không bao giờ
    /// rỗng: để trống thì PrintDocument đặt tên là "document", nên trên một máy in dùng chung
    /// mọi lệnh của mọi người trùng tên nhau và nhân viên không tìm được bản của mình.
    public string DocumentName { get; }
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
