using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Domain.Finance;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class ClipboardGridParserTests
{
    [Fact]
    public void Parse_splits_rows_and_tab_separated_cells()
    {
        var raw = "05/03/2026\tGói A\t1.000\r\n06/03/2026\tGói B\t2.000\r\n";
        var block = ClipboardGridParser.Parse(raw);

        block.Length.ShouldBe(2);
        block[0].ShouldBe(new[] { "05/03/2026", "Gói A", "1.000" });
        block[1][1].ShouldBe("Gói B");
    }

    [Fact]
    public void Parse_handles_single_cell_and_blank()
    {
        ClipboardGridParser.Parse("hello").ShouldBe(new[] { new[] { "hello" } });
        ClipboardGridParser.Parse("").Length.ShouldBe(0);
        ClipboardGridParser.Parse(null).Length.ShouldBe(0);
    }
}
