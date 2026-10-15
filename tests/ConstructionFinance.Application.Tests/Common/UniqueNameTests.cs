using ConstructionFinance.Application.Common;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Common;

public class UniqueNameTests
{
    [Fact]
    public void NextFree_appends_2_then_keeps_counting_until_free()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "DA1", "DA1 (2)", "DA1 (3)" };

        UniqueName.NextFree("DA1", taken).ShouldBe("DA1 (4)");
    }

    [Fact]
    public void NextFree_returns_the_base_name_when_it_is_free()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "DA2" };

        UniqueName.NextFree("DA1", taken).ShouldBe("DA1");
    }

    [Fact]
    public void NextFree_trims_the_base_name()
    {
        UniqueName.NextFree("  DA1 ", new HashSet<string>()).ShouldBe("DA1");
    }
}
