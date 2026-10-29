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

    /// `controller` chỉ để kiểm thử: PreviewPrintController vẽ từng trang ra ảnh qua ĐÚNG trình điều khiển
    /// của máy in, không tốn giấy. null = in thật.
    private static void Print(PrinterJob job, PrintController? controller = null)
    {
        // ---- RANH GIỚI try: TRÊN đây là MigraDoc, DƯỚI kia là trình điều khiển máy in. ----
        // Dàn trang NẰM NGOÀI try, cố ý. Một lỗi ở đây là lỗi của CHÚNG TA, không phải của máy
        // in; gói nó thành PrintJobFailedException sẽ đổ cho máy in cái nó không gây ra, và câu
        // "Hãy kiểm tra máy in đã bật, có giấy và còn kết nối" thành nói sai với người dùng.
        var renderer = new DocumentRenderer(job.Document);
        renderer.PrepareDocument();

        var formatted = renderer.FormattedDocument
            ?? throw new InvalidOperationException("MigraDoc chưa dàn trang xong tài liệu.");

        // Hai bộ đếm riêng vì hai sự kiện: QueryPageSettings bắn TRƯỚC mỗi PrintPage, nên nếu
        // dùng chung một bộ đếm thì số trang sẽ lệch một.
        var queriedPage = 0;   // chỉ QueryPageSettings tăng
        var page = 0;          // chỉ PrintPage tăng

        // TRONG try từ đây đến hết: mọi thứ chạm PrinterSettings/PageSettings có thể là một vòng
        // hỏi trình điều khiển, và trình điều khiển ném được ở BẤT KỲ chỗ nào trong số đó — máy
        // in vừa bị xoá, bị rút mạng, hàng đợi hỏng, giữa lúc người dùng chọn máy in và lúc lệnh
        // bắt đầu. Chắc chắn đi tới driver là PrinterSettings.PaperSizes (trong FindA4).
        //
        // Chỉ bọc riêng printDocument.Print() thì một cú ném ở phần dựng cấu hình LỌT RA NGOÀI:
        // app không sập (MainWindowViewModel.GuardPrintAsync bắt Exception chung), nhưng người
        // dùng nhận câu chung "Không in được do lỗi bất ngờ. Chi tiết lỗi đã được ghi vào nhật
        // ký ứng dụng." — không một chữ nào về máy in, trên đúng cái hỏng dễ xảy ra nhất ở phòng
        // dùng máy in chung. Spec §7: câu thông báo phải đúng với việc người dùng vừa làm.
        try
        {
            using var printDocument = new PrintDocument();
            if (controller is not null) printDocument.PrintController = controller;
            printDocument.PrinterSettings.PrinterName = job.PrinterName;
            printDocument.PrinterSettings.Copies = (short)job.Copies;   // PrinterJob đã kẹp 1..99
            // Hộp thoại cho gõ 1..99 bản. Mặc định của trình điều khiển có thể là KHÔNG ghép bộ,
            // tức "In toàn bộ × 3" của dự án bảy sheet ra 3×trang1, 3×trang2... và nhân viên phải
            // ngồi phân lại cả tập trước khi ký. "Số bản = 3" với người dùng nghĩa là ba BỘ.
            printDocument.PrinterSettings.Collate = true;
            // Không đặt thì mọi lệnh đứng trong hàng đợi Windows với tên "document": trên máy in
            // dùng chung, nhân viên không tìm được lệnh của mình, và nhật ký in ghi "document" cho
            // tất cả. Tên dự án là thứ phân biệt được.
            printDocument.DocumentName = job.DocumentName;

            // Khổ A4 tra MỘT LẦN, ngoài handler. Mỗi lần đọc PrinterSettings.PaperSizes là một
            // vòng hỏi trình điều khiển; để trong QueryPageSettings thì lệnh 50 trang hỏi 50 lần
            // cùng một câu. Phải tra SAU khi đã đặt PrinterName — danh sách khổ giấy là của máy in.
            var a4 = FindA4(printDocument.PrinterSettings);

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
                if (a4 is not null) e.PageSettings.PaperSize = a4;
            };

            printDocument.PrintPage += (_, args) =>
            {
                page++;
                var graphics = args.Graphics
                    ?? throw new InvalidOperationException("Hệ thống không cấp mặt vẽ cho trang in.");

                // Đây là cách in của chính mẫu MigraDoc (MigraDocPrintDocument). Bản trước khai
                // XGraphicsUnit.Inch, mà MigraDoc vẫn vẽ toạ độ bằng point: mọi thứ phóng to ~72 lần
                // và rơi hết ra ngoài tờ giấy — máy in thật in ra tờ TRẮNG TRƠN (người dùng báo
                // 2026-10-29 trên Brother HL-L2360D).
                //
                // Gốc của args.Graphics là góc VÙNG IN ĐƯỢC, thụt vào đúng bằng lề cứng của máy in;
                // MigraDoc dàn từ góc TỜ GIẤY, nên lùi gốc về đó. Đơn vị của DC máy in là 1/100 inch,
                // cùng đơn vị với HardMarginX/Y. GDI vẫn tự cắt phần ngoài vùng in được.
                graphics.TranslateTransform(-args.PageSettings.HardMarginX, -args.PageSettings.HardMarginY);
                // Rồi đổi 1/100 inch → point để MigraDoc vẽ đúng cỡ.
                const float pointsToDisplay = 100f / 72f;
                graphics.ScaleTransform(pointsToDisplay, pointsToDisplay);

                // PageBounds (1/100 inch, đã đổi chiều theo hướng giấy) → point.
                var size = new XSize(args.PageBounds.Width / 100.0 * 72, args.PageBounds.Height / 100.0 * 72);
                using var gfx = XGraphics.FromGraphics(graphics, size);
                renderer.RenderPage(gfx, page);

                args.HasMorePages = page < formatted.PageCount;
            };

            printDocument.Print();
        }
        catch (Exception ex)
        {
            // Trình điều khiển máy in ném đủ loại (InvalidPrinterException khi máy in vừa bị
            // xoá, Win32Exception khi hết giấy / mất kết nối...) và ném được ở cả bước dựng cấu
            // hình, không riêng lúc Print(). Gói lại một kiểu mang theo TÊN MÁY IN, để câu thông
            // báo nói đúng chuyện vừa xảy ra.
            throw new PrintJobFailedException(job.PrinterName, ex);
        }
    }

    /// Khổ A4 của máy in, hoặc null nếu trình điều khiển không khai khổ nào là A4.
    private static PaperSize? FindA4(PrinterSettings settings)
    {
        foreach (PaperSize size in settings.PaperSizes)
            if (size.Kind == PaperKind.A4)
                return size;
        return null;
    }
}
