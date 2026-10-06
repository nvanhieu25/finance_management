using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using PdfSharp.Pdf.IO;

namespace ConstructionFinance.Application.Tests.Printing;

/// Render một MigraDoc Document ra PDF rồi MỞ LẠI tệp để đọc số trang và khổ từng trang.
/// Đây là đòn kiểm chứng dàn trang duy nhất chạy được trên Linux: bản in ra giấy và bản PDF
/// dùng CHUNG một bộ dàn trang của MigraDoc, nên số trang và hướng giấy soi được ở đây cũng
/// đúng trên giấy.
internal static class PdfProbe
{
    public sealed record Page(double WidthMm, double HeightMm)
    {
        public bool IsLandscape => WidthMm > HeightMm;
    }

    public sealed record Result(int PageCount, IReadOnlyList<Page> Pages);

    private static readonly object FontGate = new();
    private static bool _fontsReady;

    /// GlobalFontSettings.FontResolver là trạng thái TĨNH của cả tiến trình; xUnit chạy các
    /// lớp test song song, nên gán đúng một lần dưới khóa.
    public static void EnsureFonts()
    {
        lock (FontGate)
        {
            if (_fontsReady) return;
            GlobalFontSettings.FontResolver = new LinuxFontResolver();
            _fontsReady = true;
        }
    }

    public static Result Render(Document document)
    {
        EnsureFonts();

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();

        // Ghi ra tệp tạm rồi mở lại, chứ không đọc đối tượng còn trong bộ nhớ: tệp mở lại
        // được mới chứng minh bản in là PDF hợp lệ, không chỉ là một cây đối tượng đẹp.
        var path = Path.Combine(Path.GetTempPath(), $"cf-print-{Guid.NewGuid():N}.pdf");
        try
        {
            renderer.PdfDocument.Save(path);

            using var reopened = PdfReader.Open(path, PdfDocumentOpenMode.Import);
            var pages = new List<Page>();
            for (var i = 0; i < reopened.PageCount; i++)
                pages.Add(new Page(
                    Math.Round(reopened.Pages[i].Width.Millimeter, 1),
                    Math.Round(reopened.Pages[i].Height.Millimeter, 1)));

            return new Result(reopened.PageCount, pages);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
