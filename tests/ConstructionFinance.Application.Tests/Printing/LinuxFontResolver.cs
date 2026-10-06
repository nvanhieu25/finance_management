using PdfSharp.Fonts;

namespace ConstructionFinance.Application.Tests.Printing;

/// CHỈ DÙNG CHO TEST. PDFsharp 6 không đọc phông của hệ điều hành: không gán IFontResolver
/// thì nó ném ngay khi dựng cả phông nội bộ của chính nó
/// ("The font 'Courier New' cannot be resolved for predefined error font"). Đã đo trên máy
/// phát triển Linux.
///
/// Bản phát hành KHÔNG cần lớp này: đường ra giấy dùng PDFsharp-MigraDoc-GDI, lấy phông qua
/// GDI+ của Windows, nên Times New Roman có sẵn.
///
/// Liberation Serif trùng khít metric với Times New Roman, nên bản PDF đo được bề rộng chữ
/// giống bản in trên Windows.
internal sealed class LinuxFontResolver : IFontResolver
{
    private static readonly string[] FontDirectories =
    {
        "/usr/share/fonts/truetype/liberation",
        "/usr/share/fonts/liberation",
        "/usr/share/fonts/truetype/liberation2",
    };

    private readonly Dictionary<string, byte[]> _cache = new(StringComparer.Ordinal);

    public byte[]? GetFont(string faceName)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(faceName, out var cached)) return cached;

            foreach (var directory in FontDirectories)
            {
                var path = Path.Combine(directory, faceName + ".ttf");
                if (!File.Exists(path)) continue;

                var bytes = File.ReadAllBytes(path);
                _cache[faceName] = bytes;
                return bytes;
            }
        }

        throw new FileNotFoundException(
            $"Không tìm thấy tệp phông cho \"{faceName}\". Cài gói phông Liberation " +
            $"(apt install fonts-liberation) rồi chạy lại test.");
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var monospaced = familyName.Contains("Courier", StringComparison.OrdinalIgnoreCase)
                      || familyName.Contains("Mono", StringComparison.OrdinalIgnoreCase);
        var family = monospaced ? "LiberationMono" : "LiberationSerif";
        var suffix = (isBold, isItalic) switch
        {
            (true, true) => "-BoldItalic",
            (true, false) => "-Bold",
            (false, true) => "-Italic",
            _ => "-Regular",
        };
        return new FontResolverInfo(family + suffix);
    }
}
