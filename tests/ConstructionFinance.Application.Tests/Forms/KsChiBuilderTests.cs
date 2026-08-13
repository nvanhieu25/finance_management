using ConstructionFinance.Application.Forms;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Forms;

public class KsChiBuilderTests
{
    private static FormDotRow Dot(
        int index, Fund fund, decimal advance, decimal klht, decimal recovered, string content = "Đợt") =>
        new(new FormDotKey(0, 0, 0, index), "Vị trí 1", "CHI PHÍ XÂY DỰNG", "Gói A",
            "Cty XL A", "123456789", 50_000_000_000m, 0m, new DateOnly(2026, 3, 1), content, "CT01",
            fund, advance, klht, recovered,
            Disbursed: advance > 0m && klht == 0m ? advance : klht - recovered);

    // Một đợt tạm ứng NSNN 10tr, một đợt thanh toán NSNN (KLHT 200tr, thu hồi 5tr),
    // một đợt tạm ứng ngoại hối 30tr.
    private static IReadOnlyList<FormDotRow> Sample() => new[]
    {
        Dot(0, Fund.NSNN, 10_000_000m, 0m, 0m, "Tạm ứng lần 1"),
        Dot(1, Fund.NSNN, 0m, 200_000_000m, 5_000_000m, "Thanh toán KLHT"),
        Dot(2, Fund.NgoaiHoi, 30_000_000m, 0m, 0m, "Tạm ứng ngoại hối")
    };

    private static HashSet<FormDotKey> All(IReadOnlyList<FormDotRow> rows) =>
        rows.Select(r => r.Key).ToHashSet();

    [Fact]
    public void Build_sums_accepted_capital_as_advance_plus_klht_split_by_fund()
    {
        var rows = Sample();

        var form = KsChiBuilder.Build(rows, All(rows), null);

        form.Accepted.Total.ShouldBe(240_000_000m);
        form.Accepted.NSNN.ShouldBe(210_000_000m);
        form.Accepted.NgoaiHoi.ShouldBe(30_000_000m);
    }

    [Fact]
    public void Build_sums_recovered_advance_split_by_fund()
    {
        var rows = Sample();

        var form = KsChiBuilder.Build(rows, All(rows), null);

        form.Recovered.Total.ShouldBe(5_000_000m);
        form.Recovered.NSNN.ShouldBe(5_000_000m);
        form.Recovered.NgoaiHoi.ShouldBe(0m);
    }

    [Fact]
    public void Build_pays_beneficiary_accepted_minus_recovered_which_equals_total_disbursed()
    {
        var rows = Sample();

        var form = KsChiBuilder.Build(rows, All(rows), null);

        form.PaidToBeneficiary.Total.ShouldBe(235_000_000m);
        form.PaidToBeneficiary.NSNN.ShouldBe(205_000_000m);
        form.PaidToBeneficiary.NgoaiHoi.ShouldBe(30_000_000m);
        form.PaidToBeneficiary.Total.ShouldBe(rows.Sum(r => r.Disbursed));
    }

    [Fact]
    public void Build_reads_the_beneficiary_amount_in_words()
    {
        var rows = Sample();

        var form = KsChiBuilder.Build(rows, All(rows), null);

        form.PaidInWords.ShouldBe(
            "Hai trăm ba mươi lăm triệu đồng.");
    }

    [Fact]
    public void Build_only_counts_the_selected_dots()
    {
        var rows = Sample();
        var selected = new HashSet<FormDotKey> { new(0, 0, 0, 0) };

        var form = KsChiBuilder.Build(rows, selected, null);

        form.Accepted.Total.ShouldBe(10_000_000m);
        form.PaidToBeneficiary.Total.ShouldBe(10_000_000m);
    }

    [Fact]
    public void Build_falls_back_to_the_first_selected_dot_content_for_ghi_chu()
    {
        var rows = Sample();

        var form = KsChiBuilder.Build(rows, All(rows), new KsChiDeclaration { GhiChu = "  " });

        form.GhiChu.ShouldBe("Tạm ứng lần 1");
    }

    [Fact]
    public void Build_keeps_a_typed_ghi_chu()
    {
        var rows = Sample();

        var form = KsChiBuilder.Build(rows, All(rows), new KsChiDeclaration { GhiChu = "Ghi tay" });

        form.GhiChu.ShouldBe("Ghi tay");
    }

    [Fact]
    public void Build_copies_the_manual_fields_through()
    {
        var rows = Sample();
        var declaration = new KsChiDeclaration
        {
            NgayNhanGiayDeNghi = "05/03/2026",
            MucTieuMuc1 = "Mục 9300, tiểu mục 9301",
            MucTieuMuc3 = "Mục 9400, tiểu mục 9401",
            ThuHoiCacNamTruoc = 1_000_000m,
            ThuHoiNamNay = 4_000_000m,
            ThueGTGT = 2_000_000m,
            SoTuChoi = 500_000m,
            LyDoTuChoi = "thiếu hồ sơ"
        };

        var form = KsChiBuilder.Build(rows, All(rows), declaration);

        form.NgayNhanGiayDeNghi.ShouldBe("05/03/2026");
        form.MucTieuMucLines.Count.ShouldBe(4);
        form.MucTieuMucLines[0].ShouldBe("Mục 9300, tiểu mục 9301");
        form.MucTieuMucLines[1].ShouldBe("");
        form.MucTieuMucLines[2].ShouldBe("Mục 9400, tiểu mục 9401");
        form.ThuHoiCacNamTruoc.ShouldBe(1_000_000m);
        form.ThuHoiNamNay.ShouldBe(4_000_000m);
        form.ThueGTGT.ShouldBe(2_000_000m);
        form.SoTuChoi.ShouldBe(500_000m);
        form.LyDoTuChoi.ShouldBe("thiếu hồ sơ");
    }

    [Fact]
    public void Build_with_nothing_selected_is_all_zero_and_has_an_empty_ghi_chu()
    {
        var rows = Sample();

        var form = KsChiBuilder.Build(rows, new HashSet<FormDotKey>(), null);

        form.Accepted.Total.ShouldBe(0m);
        form.PaidToBeneficiary.Total.ShouldBe(0m);
        form.PaidInWords.ShouldBe("Không đồng.");
        form.GhiChu.ShouldBe("");
    }
}
