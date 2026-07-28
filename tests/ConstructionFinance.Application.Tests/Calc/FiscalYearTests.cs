// FiscalYearTests.cs
using ConstructionFinance.Application.Calc;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Calc;

public class FiscalYearTests
{
    [Theory]
    [InlineData(2026, 2, 1, 2026)]   // 1/2/2026 → FY 2026 (start of window)
    [InlineData(2026, 12, 31, 2026)] // still FY 2026
    [InlineData(2027, 1, 31, 2026)]  // 31/1/2027 → still FY 2026 (end of window)
    [InlineData(2027, 2, 1, 2027)]   // rolls to FY 2027
    [InlineData(2026, 1, 15, 2025)]  // Jan belongs to the previous fiscal year
    public void FiscalYear_maps_by_feb_to_jan_window(int y, int m, int d, int expected)
    {
        TreeCalculator.FiscalYear(new DateOnly(y, m, d)).ShouldBe(expected);
    }
}
