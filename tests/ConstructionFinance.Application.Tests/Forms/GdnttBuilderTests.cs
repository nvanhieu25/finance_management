using ConstructionFinance.Application.Forms;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Forms;

public class GdnttBuilderTests
{
    private static readonly FormOrgInfo Org = new("Sở Tài chính", "Ban QLDA số 1");

    private static FormDotRow Dot(
        FormDotKey key, decimal advance, decimal klht, decimal recovered,
        Fund fund = Fund.NgoaiHoi, string packageName = "Gói A") =>
        new(key, "Vị trí 1", "CHI PHÍ XÂY DỰNG", packageName, "Cty XL A", "123456789",
            50_000_000_000m, 2_000_000_000m, new DateOnly(2026, 3, 1), "Đợt", "CT01",
            fund, advance, klht, recovered,
            // Same formula as TreeCalculator.Disbursed: a đợt may carry both Tạm ứng and KLHT.
            Disbursed: advance + klht - recovered);

    /// Gói A: đợt 0 tạm ứng 10tr, đợt 1 thanh toán KLHT 200tr thu hồi 4tr,
    /// đợt 2 tạm ứng 30tr (kỳ này).
    private static IReadOnlyList<FormDotRow> OnePackage() => new[]
    {
        Dot(new FormDotKey(0, 0, 0, 0), 10_000_000m, 0m, 0m),
        Dot(new FormDotKey(0, 0, 0, 1), 0m, 200_000_000m, 4_000_000m),
        Dot(new FormDotKey(0, 0, 0, 2), 30_000_000m, 0m, 0m)
    };

    [Fact]
    public void Build_sums_this_period_as_advance_plus_klht_of_the_selected_dots()
    {
        var rows = OnePackage();
        var selected = new HashSet<FormDotKey> { new(0, 0, 0, 2) };

        var form = GdnttBuilder.Build(rows, selected, null, Org).Form;

        form!.RequestedThisPeriod.ShouldBe(30_000_000m);
        form.RequestedInWords.ShouldBe("Ba mươi triệu đồng.");
    }

    [Fact]
    public void Build_takes_the_cumulative_from_the_dots_before_the_earliest_selected_one()
    {
        var rows = OnePackage();
        var selected = new HashSet<FormDotKey> { new(0, 0, 0, 2) };

        var form = GdnttBuilder.Build(rows, selected, null, Org).Form;

        // đợt 0 (10tr tạm ứng) + đợt 1 (200tr KLHT) = 210tr, "gồm cả tạm ứng"
        form!.CumulativeBefore.ShouldBe(210_000_000m);
        // số dư tạm ứng đến hết kỳ trước = 10tr − 4tr
        form.UnrecoveredAdvanceBefore.ShouldBe(6_000_000m);
    }

    [Fact]
    public void Build_has_no_prior_period_when_the_first_dot_is_selected()
    {
        var rows = OnePackage();
        var selected = new HashSet<FormDotKey> { new(0, 0, 0, 0) };

        var form = GdnttBuilder.Build(rows, selected, null, Org).Form;

        form!.CumulativeBefore.ShouldBe(0m);
        form.UnrecoveredAdvanceBefore.ShouldBe(0m);
    }

    [Fact]
    public void Build_adds_the_contract_annex_to_the_contract_value()
    {
        var rows = OnePackage();
        var selected = new HashSet<FormDotKey> { new(0, 0, 0, 2) };

        var form = GdnttBuilder.Build(rows, selected, null, Org).Form;

        form!.ContractTotal.ShouldBe(52_000_000_000m);
    }

    [Fact]
    public void Build_pays_the_contractor_after_recovery_retention_and_warranty()
    {
        var rows = OnePackage();
        var selected = new HashSet<FormDotKey> { new(0, 0, 0, 1) };
        var declaration = new GdnttDeclaration
        {
            ChuyenTienBaoHanh = 2_000_000m,
            ChuyenTienTamGiu = 1_000_000m
        };

        var form = GdnttBuilder.Build(rows, selected, declaration, Org).Form;

        form!.RequestedThisPeriod.ShouldBe(200_000_000m);
        form.RecoveredThisPeriod.ShouldBe(4_000_000m);
        form.PaidToContractor.ShouldBe(193_000_000m);   // 200 − 4 − 2 − 1
    }

    [Fact]
    public void Build_takes_beneficiary_and_account_from_the_package()
    {
        var rows = OnePackage();
        var selected = new HashSet<FormDotKey> { new(0, 0, 0, 2) };

        var form = GdnttBuilder.Build(rows, selected, new GdnttDeclaration
        {
            TaiNganHang = "Vietcombank Hải Phòng", KinhGui = "KBNN Hải Phòng"
        }, Org).Form;

        form!.PackageName.ShouldBe("Gói A");
        form.ContractorName.ShouldBe("Cty XL A");
        form.BankAccount.ShouldBe("123456789");
        form.TaiNganHang.ShouldBe("Vietcombank Hải Phòng");
        form.KinhGui.ShouldBe("KBNN Hải Phòng");
        form.DonViCapTren.ShouldBe("Sở Tài chính");
        form.NguonVon.ShouldBe("Quỹ dự trữ ngoại hối");
    }

    [Fact]
    public void Build_refuses_dots_spanning_more_than_one_package()
    {
        var rows = new[]
        {
            Dot(new FormDotKey(0, 0, 0, 0), 10_000_000m, 0m, 0m),
            Dot(new FormDotKey(0, 0, 1, 0), 20_000_000m, 0m, 0m, packageName: "Gói B")
        };
        var selected = rows.Select(r => r.Key).ToHashSet();

        var result = GdnttBuilder.Build(rows, selected, null, Org);

        result.Form.ShouldBeNull();
        result.Error!.ShouldContain("một gói thầu");
    }

    [Fact]
    public void Build_refuses_when_no_foreign_currency_dot_is_selected()
    {
        var rows = new[] { Dot(new FormDotKey(0, 0, 0, 0), 10_000_000m, 0m, 0m, Fund.NSNN) };

        var result = GdnttBuilder.Build(rows, rows.Select(r => r.Key).ToHashSet(), null, Org);

        result.Form.ShouldBeNull();
        result.Error!.ShouldContain("Quỹ dự trữ ngoại hối");
    }

    [Fact]
    public void Build_ignores_dots_of_other_packages_when_computing_the_prior_period()
    {
        var rows = new[]
        {
            Dot(new FormDotKey(0, 0, 0, 0), 10_000_000m, 0m, 0m),
            Dot(new FormDotKey(0, 0, 1, 0), 500_000_000m, 0m, 0m, packageName: "Gói B"),
            Dot(new FormDotKey(0, 0, 0, 1), 30_000_000m, 0m, 0m)
        };
        var selected = new HashSet<FormDotKey> { new(0, 0, 0, 1) };

        var form = GdnttBuilder.Build(rows, selected, null, Org).Form;

        form!.CumulativeBefore.ShouldBe(10_000_000m);   // không dính 500tr của Gói B
    }

    [Fact]
    public void Build_counts_prior_dots_of_both_funds_because_the_package_state_is_shared()
    {
        var rows = new[]
        {
            Dot(new FormDotKey(0, 0, 0, 0), 10_000_000m, 0m, 0m, Fund.NSNN),
            Dot(new FormDotKey(0, 0, 0, 1), 30_000_000m, 0m, 0m)
        };
        var selected = new HashSet<FormDotKey> { new(0, 0, 0, 1) };

        var form = GdnttBuilder.Build(rows, selected, null, Org).Form;

        form!.CumulativeBefore.ShouldBe(10_000_000m);
        form.UnrecoveredAdvanceBefore.ShouldBe(10_000_000m);
    }
}
