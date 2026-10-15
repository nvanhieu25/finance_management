using ConstructionFinance.Desktop.Common;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class FinanceCellParsingTests
{
    [Theory]
    [InlineData("05/03/2026", 2026, 3, 5)]
    [InlineData("2026-03-05", 2026, 3, 5)]
    public void ParseDate_accepts_two_formats(string raw, int y, int m, int d) =>
        FinanceCellParsing.ParseDate(raw).ShouldBe(new System.DateOnly(y, m, d));

    [Theory]
    [InlineData("")]
    [InlineData("not a date")]
    public void ParseDate_returns_null_when_unparseable(string raw) =>
        FinanceCellParsing.ParseDate(raw).ShouldBeNull();
}
