using ConstructionFinance.Application.Forms;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Forms;

public class ThongTriBuilderTests
{
    private static readonly FormOrgInfo Org = new("Sở Tài chính", "Ban QLDA số 1");

    private static FormDotRow Row(FormDotKey key, string viTri, Fund fund, decimal disbursed, string content) =>
        new(key, viTri, "CHI PHÍ XÂY DỰNG", "Gói A", "Cty XL A", "123456789",
            50_000_000_000m, 0m, new DateOnly(2026, 3, 1), content, "CT01", fund,
            Advance: disbursed, PaymentKLHT: 0m, RecoveredAdvance: 0m, Disbursed: disbursed);

    private static IReadOnlyList<FormDotRow> TwoViTris() => new[]
    {
        Row(new FormDotKey(0, 0, 0, 0), "Vị trí 1", Fund.NSNN, 10_000_000m, "Tạm ứng lần 1"),
        Row(new FormDotKey(0, 0, 0, 1), "Vị trí 1", Fund.NSNN, 5_000_000m, "Tạm ứng lần 2"),
        Row(new FormDotKey(1, 0, 0, 0), "Vị trí 2", Fund.NSNN, 20_000_000m, "Tạm ứng lần 1"),
        Row(new FormDotKey(2, 0, 0, 0), "Vị trí 3", Fund.NgoaiHoi, 99_000_000m, "Ngoại hối")
    };

    private static HashSet<FormDotKey> All(IReadOnlyList<FormDotRow> rows) =>
        rows.Select(r => r.Key).ToHashSet();

    [Fact]
    public void Build_nests_project_then_vitri_then_dot_lines()
    {
        var rows = TwoViTris();

        var form = ThongTriBuilder.Build(rows, All(rows), null, Org, "Dự án A");

        form.Lines.Select(l => (l.Level, l.Content, l.Amount)).ShouldBe(new[]
        {
            (0, "Dự án A", 35_000_000m),
            (1, "Vị trí 1", 15_000_000m),
            (2, "Tạm ứng lần 1", 10_000_000m),
            (2, "Tạm ứng lần 2", 5_000_000m),
            (1, "Vị trí 2", 20_000_000m),
            (2, "Tạm ứng lần 1", 20_000_000m)
        });
    }

    [Fact]
    public void Build_drops_dots_from_the_foreign_currency_fund()
    {
        var rows = TwoViTris();

        var form = ThongTriBuilder.Build(rows, All(rows), null, Org, "Dự án A");

        form.Lines.ShouldNotContain(l => l.Content == "Ngoại hối");
        form.Total.ShouldBe(35_000_000m);
    }

    [Fact]
    public void Build_only_includes_the_selected_dots()
    {
        var rows = TwoViTris();
        var selected = new HashSet<FormDotKey> { new(0, 0, 0, 0) };

        var form = ThongTriBuilder.Build(rows, selected, null, Org, "Dự án A");

        form.Total.ShouldBe(10_000_000m);
        form.Lines.Count.ShouldBe(3);           // dự án + vị trí 1 + một đợt
        form.Lines.ShouldNotContain(l => l.Content == "Vị trí 2");
    }

    [Fact]
    public void Build_reads_the_total_in_words()
    {
        var rows = TwoViTris();

        var form = ThongTriBuilder.Build(rows, All(rows), null, Org, "Dự án A");

        form.TotalInWords.ShouldBe("Ba mươi lăm triệu đồng.");
    }

    [Fact]
    public void Build_copies_the_declaration_and_org_fields_through()
    {
        var rows = TwoViTris();
        var declaration = new ThongTriDeclaration
        {
            SoThongTri = "12/TT", NgayGhiSo = "05/03/2026", TenCTMT = "CTMT X", MaSo = "A1",
            VeThangNam = "tháng 3 năm 2026", TaiKhoan1 = "TK1", DonVi1 = "ĐV1", SoChungTu1 = "CT1",
            TaiKhoan2 = "TK2", DonVi2 = "ĐV2", SoChungTu2 = "CT2",
            LoaiNganSach = "10", Khoan = "11", Muc = "9300", TieuMuc = "9301", TietMuc = "0", Nganh = "0"
        };

        var form = ThongTriBuilder.Build(rows, All(rows), declaration, Org, "Dự án A");

        form.DonViCapTren.ShouldBe("Sở Tài chính");
        form.DonViCoSo.ShouldBe("Ban QLDA số 1");
        form.SoThongTri.ShouldBe("12/TT");
        form.VeThangNam.ShouldBe("tháng 3 năm 2026");
        form.Khoan.ShouldBe("11");
        form.TieuMuc.ShouldBe("9301");
        form.SoChungTu2.ShouldBe("CT2");
    }

    [Fact]
    public void Build_with_nothing_selected_yields_a_zero_project_line_only()
    {
        var rows = TwoViTris();

        var form = ThongTriBuilder.Build(rows, new HashSet<FormDotKey>(), null, Org, "Dự án A");

        form.Total.ShouldBe(0m);
        form.Lines.Count.ShouldBe(1);
        form.Lines[0].Level.ShouldBe(0);
        form.TotalInWords.ShouldBe("Không đồng.");
    }

    [Fact]
    public void Build_uses_a_placeholder_when_a_dot_has_no_content()
    {
        var rows = new[] { Row(new FormDotKey(0, 0, 0, 0), "Vị trí 1", Fund.NSNN, 1_000m, "") };

        var form = ThongTriBuilder.Build(rows, All(rows), null, Org, "Dự án A");

        form.Lines[2].Content.ShouldBe("(chưa có nội dung)");
    }

    [Fact]
    public void Build_prints_distinct_vitri_separately_even_if_names_collide()
    {
        // Three Vị trí: first and third share the same name but are distinct nodes.
        var rows = new[]
        {
            Row(new FormDotKey(0, 0, 0, 0), "Vị trí 1", Fund.NSNN, 10_000_000m, "Đợt 1 của Vị trí 1"),
            Row(new FormDotKey(1, 0, 0, 0), "Vị trí 2", Fund.NSNN, 20_000_000m, "Đợt 1 của Vị trí 2"),
            Row(new FormDotKey(2, 0, 0, 0), "Vị trí 1", Fund.NSNN, 15_000_000m, "Đợt 1 của Vị trí 3")
        };

        var form = ThongTriBuilder.Build(rows, All(rows), null, Org, "Dự án A");

        // Expected: dự án, vị trí 1, đợt, vị trí 2, đợt, vị trí 1 (again), đợt
        form.Lines.Select(l => (l.Level, l.Content, l.Amount)).ShouldBe(new[]
        {
            (0, "Dự án A", 45_000_000m),
            (1, "Vị trí 1", 10_000_000m),
            (2, "Đợt 1 của Vị trí 1", 10_000_000m),
            (1, "Vị trí 2", 20_000_000m),
            (2, "Đợt 1 của Vị trí 2", 20_000_000m),
            (1, "Vị trí 1", 15_000_000m),
            (2, "Đợt 1 của Vị trí 3", 15_000_000m)
        });
    }
}
