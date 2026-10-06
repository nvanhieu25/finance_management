using ConstructionFinance.Application.Printing;
using MigraDoc.DocumentObjectModel;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Printing;

/// Lệnh in: số bản phải được kẹp TRƯỚC khi chạm System.Drawing.
public class PrinterJobTests
{
    private static Document AnyDocument()
    {
        var document = new Document();
        document.AddSection().AddParagraph("x");
        return document;
    }

    /// Review Focus 1: PrinterSettings.Copies là short. (short)100000 TRÀN ÂM, và một số bản
    /// âm thì trình điều khiển có thể in ngược hoặc ném. Kẹp ở đây, không ở lớp giao diện —
    /// lớp giao diện có thể bị người khác viết lại, còn lệnh in thì không ai đi vòng qua được.
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(99, 99)]
    [InlineData(100, 99)]
    [InlineData(100_000, 99)]
    [InlineData(int.MaxValue, 99)]
    [InlineData(int.MinValue, 1)]
    public void So_ban_bi_kep_trong_khoang_mot_den_chin_muoi_chin(int requested, int expected)
    {
        new PrinterJob(AnyDocument(), "Máy in A", requested).Copies.ShouldBe(expected);
    }

    [Fact]
    public void So_ban_sau_khi_kep_luon_vua_kieu_short()
    {
        var job = new PrinterJob(AnyDocument(), "Máy in A", int.MaxValue);

        job.Copies.ShouldBeLessThanOrEqualTo(short.MaxValue);
        job.Copies.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Ten_may_in_trong_la_loi_cua_ben_goi()
    {
        Should.Throw<ArgumentException>(() => new PrinterJob(AnyDocument(), "   ", 1));
    }

    [Fact]
    public void Loi_may_in_mang_theo_ten_may_in_de_cau_thong_bao_noi_dung_su_that()
    {
        var ex = new PrintJobFailedException("Máy in A", new InvalidOperationException("hết giấy"));

        ex.PrinterName.ShouldBe("Máy in A");
        ex.Message.ShouldContain("Máy in A");
        ex.Message.ShouldContain("hết giấy");
    }
}
