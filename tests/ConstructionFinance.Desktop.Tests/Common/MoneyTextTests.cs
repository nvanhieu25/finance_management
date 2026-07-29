using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.Tests.Common;

public class MoneyTextTests
{
    [Theory]
    [InlineData(0, "")]
    [InlineData(1000, "1.000")]
    [InlineData(10_000_000, "10.000.000")]
    [InlineData(10, "10")]
    [InlineData(100, "100")]
    [InlineData(-2500, "-2.500")]
    [InlineData(-1_000_000, "-1.000.000")]
    public void Format_ReturnsCorrectThousandGrouping(decimal value, string expected)
    {
        var result = MoneyText.Format(value);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("10.000.000", 10_000_000)]
    [InlineData("  10000 ", 10_000)]
    [InlineData("1.000", 1_000)]
    [InlineData("100", 100)]
    [InlineData("-2.500", -2_500)]
    public void TryParse_ValidInput_ReturnsTrueWithCorrectValue(string input, decimal expected)
    {
        var result = MoneyText.TryParse(input, out var value);
        Assert.True(result);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_EmptyOrWhitespace_ReturnsTrueWithZero(string? input)
    {
        var result = MoneyText.TryParse(input, out var value);
        Assert.True(result);
        Assert.Equal(0m, value);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("10.000.abc")]
    [InlineData("@#$")]
    public void TryParse_InvalidInput_ReturnsFalse(string input)
    {
        var result = MoneyText.TryParse(input, out var value);
        Assert.False(result);
        Assert.Equal(0m, value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(10_000_000)]
    [InlineData(-2500)]
    [InlineData(123_456_789)]
    public void RoundTrip_FormatThenParse_PreservesValue(decimal original)
    {
        var formatted = MoneyText.Format(original);
        var parseResult = MoneyText.TryParse(formatted, out var parsed);

        // Only test round-trip for non-zero values (0 formats to "", which parses back to 0)
        if (original == 0)
        {
            Assert.True(parseResult);
            Assert.Equal(0m, parsed);
        }
        else
        {
            Assert.True(parseResult);
            Assert.Equal(original, parsed);
        }
    }

    [Fact]
    public void Format_WithSpacesAndDots_ParsesCorrectly()
    {
        var formatted = MoneyText.Format(10_000_000);
        var trimmed = formatted.Replace(" ", "");
        var result = MoneyText.TryParse(trimmed, out var value);

        Assert.True(result);
        Assert.Equal(10_000_000, value);
    }
}
