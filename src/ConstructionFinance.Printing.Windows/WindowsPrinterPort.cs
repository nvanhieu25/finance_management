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
    /// ct chỉ có tác dụng TRƯỚC khi lệnh bắt đầu; một khi đã đẩy vào hàng đợi in thì không huỷ
    /// giữa chừng được. Chữ ký hứa nhiều hơn điều nó làm được.
    public Task PrintAsync(PrinterJob job, CancellationToken ct = default) =>
        Task.Run(() => Print(job), ct);

    private static void Print(PrinterJob job)
    {
        var renderer = new DocumentRenderer(job.Document);
        renderer.PrepareDocument();

        var formatted = renderer.FormattedDocument
            ?? throw new InvalidOperationException("MigraDoc chưa dàn trang xong tài liệu.");

        // Hai bộ đếm riêng vì hai sự kiện: QueryPageSettings bắn TRƯỚC mỗi PrintPage, nên nếu
        // dùng chung một bộ đếm thì số trang sẽ lệch một.
        var queriedPage = 0;   // chỉ QueryPageSettings tăng
        var page = 0;          // chỉ PrintPage tăng
        using var printDocument = new PrintDocument();
        printDocument.PrinterSettings.PrinterName = job.PrinterName;
        printDocument.PrinterSettings.Copies = (short)job.Copies;   // PrinterJob đã kẹp 1..99
        // Một PrintDocument không mang được một hướng giấy cho cả lệnh, mà "In toàn bộ" trộn trang
        // ngang lẫn dọc (Sổ Vị trí, Tổng hợp, GĐNTT là ngang). Nên đặt hướng TỪNG TRANG ở đây.
        printDocument.QueryPageSettings += (_, e) =>
        {
            queriedPage++;
            // Vòng in của .NET bắn đúng một QueryPageSettings mỗi trang. Nếu một trình điều khiển
            // bắn thừa, GetPageInfo sẽ ném, lỗi bị gói thành PrintJobFailedException, và người dùng
            // thấy "Máy in báo lỗi" ở MỌI lần in trên máy đó. Chỗ này không chạy thử được ở máy phát
            // triển, nên một dòng phòng thủ đáng giá hơn bình thường.
            if (queriedPage > formatted.PageCount) return;

            e.PageSettings.Landscape =
                formatted.GetPageInfo(queriedPage).Orientation == PdfSharp.PageOrientation.Landscape;

            // Mọi tài liệu của ứng dụng đều là A4; máy in mặc định khổ Letter sẽ cắt mất nội dung.
            // Không có A4 trong danh sách thì giữ mặc định của máy in, không ném.
            foreach (PaperSize size in printDocument.PrinterSettings.PaperSizes)
            {
                if (size.Kind == PaperKind.A4)
                {
                    e.PageSettings.PaperSize = size;
                    break;
                }
            }
        };

        // ---- CHƯA KIỂM CHỨNG ĐƯỢC: đây là chỗ duy nhất không test được trên máy phát triển. ----
        // Hai điều về hình học trang chưa ai chạy thử. Cả hai đều theo đúng mẫu in MigraDoc chính
        // thức của PDFsharp nên chưa sửa; nếu bản in thử đầu tiên sai thì tìm triệu chứng dưới đây:
        //
        // 1) ĐƠN VỊ: args.PageBounds tính bằng 1/100 inch, còn XSize mặc định là point.
        //    Triệu chứng: mọi thứ in to gấp ~1,39 lần và tràn ra khỏi trang.
        //    Cách chỉnh: cho XSize mang đúng đơn vị, hoặc đổi PageBounds từ 1/100 inch sang point
        //    (nhân 0,72) trước khi dựng XSize.
        //
        // 2) GỐC TOẠ ĐỘ: e.Graphics bắt đầu ở vùng in được chứ không phải góc tờ giấy, nên nội
        //    dung có thể lệch đúng bằng lề cứng của máy in.
        //    Triệu chứng: cả trang dịch xuống dưới và sang phải khoảng 3-5 mm.
        //    Cách chỉnh: dịch gốc toạ độ đi args.PageSettings.HardMarginX / HardMarginY.
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
        catch (Exception ex)
        {
            // Trình điều khiển máy in ném đủ loại (InvalidPrinterException khi máy in vừa bị
            // xoá, Win32Exception khi hết giấy / mất kết nối...). Gói lại một kiểu mang theo
            // TÊN MÁY IN, để câu thông báo nói đúng chuyện vừa xảy ra.
            throw new PrintJobFailedException(job.PrinterName, ex);
        }
    }
}
