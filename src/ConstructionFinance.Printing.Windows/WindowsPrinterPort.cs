using System.Drawing.Printing;
using ConstructionFinance.Application.Printing;
using MigraDoc.Rendering;
using PdfSharp.Drawing;

namespace ConstructionFinance.Printing.Windows;

/// VÙNG MÙ DUY NHẤT của tính năng in. Lớp này chỉ được BIÊN DỊCH trên máy phát triển Linux;
/// chạy thì phải Windows có máy in thật, và nghiệm thu cuối cùng là người dùng in thử.
///
/// Vì thế nó cố tình KHÔNG có logic nội dung nào: mọi quyết định về in cái gì, nằm ở đâu, cỡ
/// bao nhiêu đều nằm trong MigraDocBuilder và ExcelFitScale — hai lớp kiểm được trên Linux.
/// Ở đây chỉ còn: mỗi lần hệ điều hành đòi một trang thì vẽ trang đó.
///
/// Đừng thêm việc gì vào lớp này. Thêm vào đây là chuyển việc từ vùng kiểm được sang vùng mù.
public sealed class WindowsPrinterPort : IPrinterPort
{
    public Task PrintAsync(PrinterJob job, CancellationToken ct = default) =>
        Task.Run(() => Print(job), ct);

    private static void Print(PrinterJob job)
    {
        var renderer = new DocumentRenderer(job.Document);
        renderer.PrepareDocument();

        var formatted = renderer.FormattedDocument
            ?? throw new InvalidOperationException("MigraDoc chưa dàn trang xong tài liệu.");

        var page = 0;
        using var printDocument = new PrintDocument();
        printDocument.PrinterSettings.PrinterName = job.PrinterName;
        printDocument.PrinterSettings.Copies = (short)job.Copies;   // PrinterJob đã kẹp 1..99
        printDocument.PrintPage += (_, args) =>
        {
            page++;
            var graphics = args.Graphics
                ?? throw new InvalidOperationException("Hệ thống không cấp mặt vẽ cho trang in.");

            using var gfx = XGraphics.FromGraphics(
                graphics, new XSize(args.PageBounds.Width, args.PageBounds.Height));
            renderer.RenderPage(gfx, page);

            args.HasMorePages = page < formatted.PageCount;
        };

        try
        {
            printDocument.Print();
        }
        catch (Exception ex) when (ex is not PrintJobFailedException)
        {
            // Trình điều khiển máy in ném đủ loại (InvalidPrinterException khi máy in vừa bị
            // xoá, Win32Exception khi hết giấy / mất kết nối...). Gói lại một kiểu mang theo
            // TÊN MÁY IN, để câu thông báo nói đúng chuyện vừa xảy ra.
            throw new PrintJobFailedException(job.PrinterName, ex);
        }
    }
}
