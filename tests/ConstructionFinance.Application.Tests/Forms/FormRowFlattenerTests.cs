using ConstructionFinance.Application.Forms;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Forms;

public class FormRowFlattenerTests
{
    private static ViTri ViTri(string name, int sort, params Dot[] dots) => new()
    {
        Name = name,
        SortOrder = sort,
        CostGroups =
        {
            new CostGroup
            {
                Name = "CHI PHÍ XÂY DỰNG", SortOrder = 0,
                Packages =
                {
                    new Package
                    {
                        Name = "Gói A", ContractorName = "Cty XL A", BankAccount = "123456789",
                        ContractValue = 50_000_000_000m, ContractAnnexValue = 2_000_000_000m,
                        SortOrder = 0, Dots = dots.ToList()
                    }
                }
            }
        }
    };

    private static Dot Advance(int sort, decimal amount, Fund fund = Fund.NSNN) => new()
    {
        SortOrder = sort, Date = new DateOnly(2024, 11, 28), Content = $"Tạm ứng lần {sort + 1}",
        DocumentNo = "CT01", Fund = fund, Advance = amount
    };

    private static Dot Payment(int sort, decimal klht, decimal recovered, Fund fund = Fund.NSNN) => new()
    {
        SortOrder = sort, Date = new DateOnly(2025, 5, 14), Content = "Thanh toán KLHT",
        Fund = fund, PaymentKLHT = klht, RecoveredAdvance = recovered
    };

    [Fact]
    public void Flatten_emits_one_row_per_dot_in_ledger_order()
    {
        var rows = FormRowFlattener.Flatten(new[]
        {
            ViTri("Vị trí 1", 0, Advance(0, 10_000_000m), Payment(1, 200_000_000m, 5_000_000m)),
            ViTri("Vị trí 2", 1, Advance(0, 30_000_000m))
        });

        rows.Count.ShouldBe(3);
        rows[0].ViTriName.ShouldBe("Vị trí 1");
        rows[0].Content.ShouldBe("Tạm ứng lần 1");
        rows[2].ViTriName.ShouldBe("Vị trí 2");
    }

    [Fact]
    public void Flatten_keys_each_row_by_its_position_in_the_tree()
    {
        var rows = FormRowFlattener.Flatten(new[]
        {
            ViTri("Vị trí 1", 0, Advance(0, 10_000_000m), Payment(1, 200_000_000m, 5_000_000m)),
            ViTri("Vị trí 2", 1, Advance(0, 30_000_000m))
        });

        rows[0].Key.ShouldBe(new FormDotKey(0, 0, 0, 0));
        rows[1].Key.ShouldBe(new FormDotKey(0, 0, 0, 1));
        rows[2].Key.ShouldBe(new FormDotKey(1, 0, 0, 0));
    }

    [Fact]
    public void Flatten_carries_package_context_onto_every_row()
    {
        var row = FormRowFlattener.Flatten(new[] { ViTri("Vị trí 1", 0, Advance(0, 10_000_000m)) })[0];

        row.CostGroupName.ShouldBe("CHI PHÍ XÂY DỰNG");
        row.PackageName.ShouldBe("Gói A");
        row.ContractorName.ShouldBe("Cty XL A");
        row.BankAccount.ShouldBe("123456789");
        row.ContractValue.ShouldBe(50_000_000_000m);
        row.ContractAnnexValue.ShouldBe(2_000_000_000m);
        row.DocumentNo.ShouldBe("CT01");
    }

    [Fact]
    public void Flatten_computes_disbursed_the_same_way_the_ledger_does()
    {
        var rows = FormRowFlattener.Flatten(new[]
        {
            ViTri("Vị trí 1", 0, Advance(0, 10_000_000m), Payment(1, 200_000_000m, 5_000_000m))
        });

        rows[0].Disbursed.ShouldBe(10_000_000m);        // đợt tạm ứng → chính số tạm ứng
        rows[1].Disbursed.ShouldBe(195_000_000m);       // đợt thanh toán → KLHT − thu hồi
    }

    [Fact]
    public void Flatten_orders_vitris_groups_packages_and_dots_by_SortOrder()
    {
        var viTri = ViTri("Vị trí 1", 0, Payment(1, 200_000_000m, 0m), Advance(0, 10_000_000m));

        var rows = FormRowFlattener.Flatten(new[] { viTri });

        rows[0].Advance.ShouldBe(10_000_000m);          // SortOrder 0 trước, dù thêm sau
        rows[1].PaymentKLHT.ShouldBe(200_000_000m);
    }

    [Fact]
    public void Flatten_returns_empty_for_a_vitri_with_no_dots() =>
        FormRowFlattener.Flatten(new[] { ViTri("Vị trí 1", 0) }).ShouldBeEmpty();
}
