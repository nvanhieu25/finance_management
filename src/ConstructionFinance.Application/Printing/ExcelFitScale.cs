namespace ConstructionFinance.Application.Printing;

/// Làm bằng tay đúng việc Excel đang làm hộ: hồ sơ in đặt FitToPages(1 ngang, 0 dọc) và Excel
/// tự thu nhỏ cho vừa bề ngang giấy. MigraDoc khai bề rộng cột bằng mm và rộng quá là tràn,
/// nên bộ dựng MigraDoc phải tự tính tỉ lệ đó. Xem spec §5.
public static class ExcelFitScale
{
    /// Một đơn vị bề rộng cột của Excel = bề ngang chữ '0' của phông Normal (Calibri 11) ở 96dpi.
    private const double PixelsPerWidthUnit = 7.0;

    /// Phần đệm hai bên trong một ô, cộng MỘT LẦN cho MỖI cột — công thức đủ là px = w*7 + 5.
    /// Bỏ nó đi thì càng nhiều cột càng tính thiếu: Sổ Vị trí 14 cột thiếu 70px ≈ 18,5mm.
    private const double CellPaddingPixels = 5.0;

    private const double PixelsPerInch = 96.0;
    private const double MillimetersPerInch = 25.4;

    /// Lề trái/phải, đúng giá trị FormExcelExporter.ApplyPageDefaults đang đặt cho đường .xlsx.
    public const double SideMarginInches = 0.5;

    private const double A4ShortSideMm = 210.0;
    private const double A4LongSideMm = 297.0;

    /// Bề cao giấy còn dùng được: khổ A4 theo hướng giấy, trừ lề trên và dưới của bản giấy (MigraDocBuilder).
    public static double UsableHeightMm(PrintOrientation orientation)
    {
        var pageHeight = orientation == PrintOrientation.Landscape ? A4ShortSideMm : A4LongSideMm;
        return pageHeight - 2 * MigraDocBuilder.VerticalMarginInches * MillimetersPerInch;
    }

    /// Tổng bề ngang nội dung, tính bằng mm theo đúng công thức của Excel.
    public static double ContentWidthMm(IEnumerable<double> widthChars)
    {
        var pixels = 0.0;
        foreach (var width in widthChars)
            pixels += width * PixelsPerWidthUnit + CellPaddingPixels;
        return pixels * MillimetersPerInch / PixelsPerInch;
    }

    /// Bề ngang giấy còn dùng được: khổ A4 theo hướng giấy, trừ lề hai bên.
    public static double UsableWidthMm(PrintOrientation orientation)
    {
        var pageWidth = orientation == PrintOrientation.Landscape ? A4LongSideMm : A4ShortSideMm;
        return pageWidth - 2 * SideMarginInches * MillimetersPerInch;
    }

    /// Tỉ lệ lớn nhất ScaleFor có thể trả về (cỡ chữ gốc). Chân trang cao nhất ở tỉ lệ này.
    internal const double MaxScale = 1.0;

    /// Tỉ lệ áp cho CẢ bề rộng cột lẫn cỡ chữ. Chặn trên ở 1,0: nội dung hẹp hơn giấy thì in
    /// đúng cỡ gốc với dải trắng bên phải, chứ không kéo giãn — 13pt là cỡ của biểu mẫu gốc,
    /// phóng to lên là tự ý đổi hình thức văn bản hành chính (spec §5).
    public static double ScaleFor(IEnumerable<double> widthChars, PrintOrientation orientation)
    {
        var content = ContentWidthMm(widthChars);
        if (content <= 0) return 1.0;
        return Math.Min(MaxScale, UsableWidthMm(orientation) / content);
    }
}
