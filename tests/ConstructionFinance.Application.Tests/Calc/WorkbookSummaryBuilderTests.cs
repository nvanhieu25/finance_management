using ConstructionFinance.Application.Calc;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Calc;

public class WorkbookSummaryBuilderTests
{
    // One advance đợt (NSNN) so aggregates are non-zero and predictable.
    private static ViTri ViTriWithAdvance(string name, int sort, decimal advance, decimal contract) =>
        new()
        {
            Name = name,
            SortOrder = sort,
            CostGroups =
            {
                new CostGroup
                {
                    Name = "CHI PHÍ XÂY DỰNG",
                    SortOrder = 0,
                    Packages =
                    {
                        new Package
                        {
                            Name = "Gói A",
                            ContractorName = "Cty A",
                            ContractValue = contract,
                            ContractAnnexValue = 0m,
                            SortOrder = 0,
                            Dots =
                            {
                                new Dot
                                {
                                    Date = new DateOnly(2026, 3, 1),
                                    Fund = Fund.NSNN,
                                    Advance = advance,
                                    PaymentKLHT = 0m,
                                    RecoveredAdvance = 0m,
                                    SortOrder = 0
                                }
                            }
                        }
                    }
                }
            }
        };

    [Fact]
    public void BuildViTriRows_returns_one_row_per_vitri_plus_a_total()
    {
        var v1 = ViTriWithAdvance("Vị trí 1", 0, 10_000_000m, 50_000_000_000m);
        var v2 = ViTriWithAdvance("Vị trí 2", 1, 20_000_000m, 0m);

        var (rows, total) = WorkbookSummaryBuilder.BuildViTriRows(new[] { v1, v2 });

        rows.Count.ShouldBe(2);
        rows[0].Kind.ShouldBe(SummaryRowKind.ViTri);
        rows[0].Ordinal.ShouldBe("1");
        rows[0].Label.ShouldBe("Vị trí 1");
        rows[0].Contractor.ShouldBe("Cty A");
        rows[0].ContractValue.ShouldBe(50_000_000_000m);
        rows[0].Aggregate.DisbursedNSNN.ShouldBe(10_000_000m);
        rows[1].Ordinal.ShouldBe("2");

        total.Kind.ShouldBe(SummaryRowKind.GrandTotal);
        total.Label.ShouldBe("TỔNG CỘNG");
        total.Aggregate.DisbursedNSNN.ShouldBe(30_000_000m);
        total.ContractValue.ShouldBe(50_000_000_000m);
    }

    [Fact]
    public void BuildAll_groups_each_project_with_roman_ordinal_then_a_grand_total()
    {
        var p1 = new Project { Id = 1, Name = "Dự án 1", ViTris = { ViTriWithAdvance("Vị trí 1", 0, 10_000_000m, 0m) } };
        var p2 = new Project { Id = 2, Name = "Dự án 2", ViTris = { ViTriWithAdvance("Vị trí 1", 0, 5_000_000m, 0m) } };

        var rows = WorkbookSummaryBuilder.BuildAll(new[] { p1, p2 });

        // I(project) + 1 vị trí, II(project) + 1 vị trí, grand total = 5 rows
        rows.Count.ShouldBe(5);
        rows[0].Kind.ShouldBe(SummaryRowKind.Project);
        rows[0].Ordinal.ShouldBe("I");
        rows[0].Label.ShouldBe("Dự án 1");
        rows[0].ProjectId.ShouldBe(1);
        rows[0].Aggregate.DisbursedNSNN.ShouldBe(10_000_000m);
        rows[1].Kind.ShouldBe(SummaryRowKind.ViTri);
        rows[2].Ordinal.ShouldBe("II");
        rows[2].ProjectId.ShouldBe(2);
        rows[^1].Kind.ShouldBe(SummaryRowKind.GrandTotal);
        rows[^1].Label.ShouldBe("Tổng cộng");
        rows[^1].Aggregate.DisbursedNSNN.ShouldBe(15_000_000m);
    }

    [Fact]
    public void BuildAll_on_empty_input_is_just_a_zero_grand_total()
    {
        var rows = WorkbookSummaryBuilder.BuildAll(Array.Empty<Project>());

        rows.Count.ShouldBe(1);
        rows[0].Kind.ShouldBe(SummaryRowKind.GrandTotal);
        rows[0].Aggregate.TotalDisbursed.ShouldBe(0m);
    }

    [Fact]
    public void BuildViTriRows_includes_ContractAnnexValue_in_contract_value_sum()
    {
        // Fixture with one package having both ContractValue and ContractAnnexValue
        var viTri = new ViTri
        {
            Name = "Vị trí with annex",
            SortOrder = 0,
            CostGroups =
            {
                new CostGroup
                {
                    Name = "CHI PHÍ XÂY DỰNG",
                    SortOrder = 0,
                    Packages =
                    {
                        new Package
                        {
                            Name = "Gói A",
                            ContractorName = "Cty A",
                            ContractValue = 50_000_000_000m,
                            ContractAnnexValue = 2_000_000_000m,
                            SortOrder = 0,
                            Dots =
                            {
                                new Dot
                                {
                                    Date = new DateOnly(2026, 3, 1),
                                    Fund = Fund.NSNN,
                                    Advance = 10_000_000m,
                                    PaymentKLHT = 0m,
                                    RecoveredAdvance = 0m,
                                    SortOrder = 0
                                }
                            }
                        }
                    }
                }
            }
        };

        var (rows, _) = WorkbookSummaryBuilder.BuildViTriRows(new[] { viTri });

        rows[0].ContractValue.ShouldBe(52_000_000_000m);
    }

    [Fact]
    public void FirstContractor_skips_blank_and_picks_next_non_blank_contractor()
    {
        // Two packages in the same cost group, ordered by SortOrder
        // First package has blank contractor, second has "Cty B"
        var viTri = new ViTri
        {
            Name = "Vị trí multi-contractor",
            SortOrder = 0,
            CostGroups =
            {
                new CostGroup
                {
                    Name = "CHI PHÍ XÂY DỰNG",
                    SortOrder = 0,
                    Packages =
                    {
                        new Package
                        {
                            Name = "Gói A",
                            ContractorName = "",  // blank
                            ContractValue = 0m,
                            ContractAnnexValue = 0m,
                            SortOrder = 0,
                            Dots = { }
                        },
                        new Package
                        {
                            Name = "Gói B",
                            ContractorName = "Cty B",  // first non-blank
                            ContractValue = 0m,
                            ContractAnnexValue = 0m,
                            SortOrder = 1,
                            Dots = { }
                        }
                    }
                }
            }
        };

        var (rows, _) = WorkbookSummaryBuilder.BuildViTriRows(new[] { viTri });

        rows[0].Contractor.ShouldBe("Cty B");
    }

    [Fact]
    public void BuildAll_generates_roman_numerals_including_subtractive_cases()
    {
        // Build with 4 projects to generate IV (subtractive case: 5 - 1 = IV)
        var projects = new[]
        {
            new Project { Id = 1, Name = "P1", ViTris = { ViTriWithAdvance("V1", 0, 1_000_000m, 0m) } },
            new Project { Id = 2, Name = "P2", ViTris = { ViTriWithAdvance("V1", 0, 1_000_000m, 0m) } },
            new Project { Id = 3, Name = "P3", ViTris = { ViTriWithAdvance("V1", 0, 1_000_000m, 0m) } },
            new Project { Id = 4, Name = "P4", ViTris = { ViTriWithAdvance("V1", 0, 1_000_000m, 0m) } }
        };

        var rows = WorkbookSummaryBuilder.BuildAll(projects);

        // Find project rows and verify roman ordinals
        rows[0].Ordinal.ShouldBe("I");
        rows[2].Ordinal.ShouldBe("II");
        rows[4].Ordinal.ShouldBe("III");
        rows[6].Ordinal.ShouldBe("IV");  // subtractive case: 5-1
    }
}
