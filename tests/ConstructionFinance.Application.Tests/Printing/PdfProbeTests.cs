using MigraDoc.DocumentObjectModel;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Printing;

/// Bản thân đòn kiểm chứng cũng phải được kiểm: nếu PdfProbe im lặng trả sai thì mọi test
/// dàn trang sau nó đều vô nghĩa.
public class PdfProbeTests
{
    [Fact]
    public void Render_ra_PDF_mo_lai_duoc_va_doc_dung_kho_giay()
    {
        var document = new Document();
        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.Orientation = Orientation.Landscape;
        section.AddParagraph("Sổ Vị trí");

        var result = PdfProbe.Render(document);

        result.PageCount.ShouldBe(1);
        result.Pages[0].WidthMm.ShouldBe(297.0, 0.5);
        result.Pages[0].HeightMm.ShouldBe(210.0, 0.5);
        result.Pages[0].IsLandscape.ShouldBeTrue();
    }

    /// "In toàn bộ" đưa mọi sheet vào MỘT tài liệu, mỗi sheet một Section mang hướng giấy
    /// riêng. Test này ghim rằng MigraDoc đổi hướng giấy GIỮA các Section — nếu không,
    /// "In toàn bộ" phải chia thành nhiều lệnh in và cả thiết kế đổi.
    [Fact]
    public void Moi_Section_giu_huong_giay_rieng_trong_cung_mot_tai_lieu()
    {
        var document = new Document();
        foreach (var orientation in new[] { Orientation.Landscape, Orientation.Portrait, Orientation.Landscape })
        {
            var section = document.AddSection();
            section.PageSetup.PageFormat = PageFormat.A4;
            section.PageSetup.Orientation = orientation;
            section.AddParagraph("sheet");
        }

        var result = PdfProbe.Render(document);

        result.PageCount.ShouldBe(3);
        result.Pages.Select(page => page.IsLandscape).ShouldBe(new[] { true, false, true });
    }
}
