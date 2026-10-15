using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace ConstructionFinance.Application.Printing;

/// Ghi một tài liệu MigraDoc ra tệp .pdf. Bản dựng này là đường in chạy được trên MỌI hệ điều hành: mở tệp bằng trình
/// xem PDF mặc định rồi Ctrl+P — in ra máy in nào, hay "Lưu thành PDF", đều do trình xem lo. Đường in thẳng ra máy in
/// (IPrinterPort) chỉ có ở bản Windows.
public interface IPdfExporter
{
    /// Ném IOException (và con của nó) khi không ghi được tệp, như File.WriteAllBytes.
    Task WriteAsync(Document document, string targetPath, CancellationToken ct = default);
}

public sealed class PdfExporter : IPdfExporter
{
    public Task WriteAsync(Document document, string targetPath, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            EnsureFonts();

            var renderer = new PdfDocumentRenderer { Document = document };
            renderer.RenderDocument();

            // Dựng xong trong bộ nhớ rồi mới chạm đĩa: tài liệu lỗi thì không để lại tệp rỗng/hỏng, không đè mất bản cũ.
            using var buffer = new MemoryStream();
            renderer.PdfDocument.Save(buffer, closeStream: false);
            ct.ThrowIfCancellationRequested();
            File.WriteAllBytes(targetPath, buffer.ToArray());
        }, ct);

    private static readonly object FontGate = new();

    /// PDFsharp 6 không tự đọc phông của hệ điều hành trên Linux/macOS: không gán IFontResolver thì nó ném ngay khi dựng
    /// cả phông nội bộ của chính nó. Windows dùng thẳng thư mục phông của hệ điều hành. Chỉ gán khi chưa ai gán (test và
    /// bản Windows có thể đã gán), và đúng một lần.
    private static void EnsureFonts()
    {
        lock (FontGate)
        {
            if (OperatingSystem.IsWindows())
            {
                GlobalFontSettings.UseWindowsFontsUnderWindows = true;
                return;
            }
            GlobalFontSettings.FontResolver ??= new SystemFontResolver();
        }
    }
}

/// Phông cho PDF ngoài Windows: Liberation Serif (trùng metric Times New Roman nên bề rộng chữ khớp bản Excel/in).
/// Không có thì thử Tinos (cùng họ metric) rồi DejaVu Serif; không có cả ba thì báo rõ cách cài.
internal sealed class SystemFontResolver : IFontResolver
{
    private static readonly string[] Directories =
    {
        "/usr/share/fonts/truetype/liberation", "/usr/share/fonts/truetype/liberation2", "/usr/share/fonts/liberation",
        "/usr/share/fonts/truetype/tinos", "/usr/share/fonts/truetype/dejavu", "/usr/share/fonts/dejavu",
        "/usr/share/fonts/TTF", "/Library/Fonts", "/System/Library/Fonts/Supplemental",
    };

    private static readonly (string Family, string[] Files)[] Families =
    {
        ("Serif", new[] { "LiberationSerif-{0}.ttf", "Tinos-{0}.ttf" }),
        ("DejaVu", new[] { "DejaVuSerif{1}.ttf" }),
    };

    private readonly Dictionary<string, byte[]> _cache = new(StringComparer.Ordinal);

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var style = (isBold, isItalic) switch
        {
            (true, true) => "BoldItalic",
            (true, false) => "Bold",
            (false, true) => "Italic",
            _ => "Regular",
        };
        return new FontResolverInfo(style);
    }

    public byte[]? GetFont(string faceName)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(faceName, out var cached)) return cached;

            var dejavuSuffix = faceName switch { "Bold" => "-Bold", "Italic" => "-Italic", "BoldItalic" => "-BoldItalic", _ => "" };
            foreach (var (_, patterns) in Families)
            foreach (var pattern in patterns)
            {
                var file = string.Format(pattern, faceName, dejavuSuffix);
                foreach (var directory in Directories)
                {
                    var path = Path.Combine(directory, file);
                    if (!File.Exists(path)) continue;
                    return _cache[faceName] = File.ReadAllBytes(path);
                }
            }
        }

        throw new FileNotFoundException(
            "Không tìm thấy phông chữ để dựng PDF. Cài gói phông Liberation (Ubuntu/Debian: sudo apt install fonts-liberation) " +
            "rồi thử lại.");
    }
}
