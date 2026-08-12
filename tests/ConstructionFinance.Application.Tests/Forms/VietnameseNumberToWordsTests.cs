using ConstructionFinance.Application.Forms;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Forms;

public class VietnameseNumberToWordsTests
{
    [Theory]
    [InlineData(0, "Không đồng.")]
    [InlineData(1, "Một đồng.")]
    [InlineData(10, "Mười đồng.")]
    [InlineData(11, "Mười một đồng.")]
    [InlineData(15, "Mười lăm đồng.")]
    [InlineData(20, "Hai mươi đồng.")]
    [InlineData(21, "Hai mươi mốt đồng.")]
    [InlineData(24, "Hai mươi tư đồng.")]
    [InlineData(25, "Hai mươi lăm đồng.")]
    [InlineData(100, "Một trăm đồng.")]
    [InlineData(101, "Một trăm linh một đồng.")]
    [InlineData(105, "Một trăm linh năm đồng.")]
    [InlineData(110, "Một trăm mười đồng.")]
    [InlineData(1_000, "Một nghìn đồng.")]
    [InlineData(1_005, "Một nghìn không trăm linh năm đồng.")]
    [InlineData(10_000_000, "Mười triệu đồng.")]
    [InlineData(200_000_000, "Hai trăm triệu đồng.")]
    public void ToWords_reads_common_amounts(long amount, string expected) =>
        VietnameseNumberToWords.ToWords(amount).ShouldBe(expected);

    [Fact]
    public void ToWords_reads_a_billion_with_a_zero_hundreds_group() =>
        VietnameseNumberToWords.ToWords(1_234_567_021m).ShouldBe(
            "Một tỷ hai trăm ba mươi tư triệu năm trăm sáu mươi bảy nghìn không trăm hai mươi mốt đồng.");

    [Fact]
    public void ToWords_reads_fifteen_billion() =>
        VietnameseNumberToWords.ToWords(15_000_000_000m).ShouldBe("Mười lăm tỷ đồng.");

    [Fact]
    public void ToWords_reads_above_a_thousand_billion_as_nghin_ty() =>
        VietnameseNumberToWords.ToWords(1_000_000_000_000m).ShouldBe("Một nghìn tỷ đồng.");

    [Fact]
    public void ToWords_rounds_to_the_nearest_dong_away_from_zero() =>
        VietnameseNumberToWords.ToWords(10_000_000.5m).ShouldBe("Mười triệu không trăm linh một đồng.");

    [Fact]
    public void ToWords_prefixes_negative_amounts_with_am() =>
        VietnameseNumberToWords.ToWords(-1_000m).ShouldBe("Âm một nghìn đồng.");
}
