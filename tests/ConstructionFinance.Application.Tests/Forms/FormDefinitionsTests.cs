using ConstructionFinance.Application.Forms;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Forms;

public class FormDefinitionsTests
{
    private static readonly FormOrgInfo Org = new("Sở Tài chính", "Ban QLDA số 1");

    private static FormDotRow Dot(
        FormDotKey key, string viTri, Fund fund, decimal advance, decimal klht, decimal recovered,
        string content, string package = "Gói A") =>
        new(key, viTri, "CHI PHÍ XÂY DỰNG", package, "Cty XL A", "123456789",
            50_000_000_000m, 2_000_000_000m, new DateOnly(2026, 3, 1), content, "CT01", fund,
            advance, klht, recovered, Disbursed: advance + klht - recovered);

    private static IReadOnlyList<FormDotRow> Rows() => new[]
    {
        Dot(new(0, 0, 0, 0), "Vị trí 1", Fund.NSNN, 10_000_000m, 0m, 0m, "Tạm ứng lần 1"),
        Dot(new(0, 0, 0, 1), "Vị trí 1", Fund.NSNN, 0m, 200_000_000m, 4_000_000m, "Thanh toán KLHT"),
        Dot(new(0, 0, 0, 2), "Vị trí 1", Fund.NgoaiHoi, 30_000_000m, 0m, 0m, "Ngoại hối 1"),
        Dot(new(1, 0, 0, 0), "Vị trí 2", Fund.NgoaiHoi, 5_000_000m, 0m, 0m, "Ngoại hối 2", package: "Gói B"),
    };

    private static FormBuildContext Context(
        IReadOnlySet<FormDotKey> selected, FormInputSet? inputs = null, string project = "Dự án A") =>
        new(Rows(), selected, inputs ?? new FormInputSet(), Org, project, 2026);

    private static IReadOnlySet<FormDotKey> Keys(params (int, int, int, int)[] keys) =>
        keys.Select(k => new FormDotKey(k.Item1, k.Item2, k.Item3, k.Item4)).ToHashSet();

    private static ResolvedCell Cell(ResolvedSheet sheet, int row, int col) =>
        FormSheetResolverTests.Cell(sheet, row, col);

    [Fact]
    public void The_three_definitions_have_stable_ids_titles_and_fund_filters()
    {
        FormDefinitions.All.Select(d => (d.Id, d.Title, d.FundFilter)).ShouldBe(new (string, string, Fund?)[]
        {
            ("thongtri", "Thông tri", Fund.NSNN),
            ("kschi", "KS chi", null),
            ("gdntt", "GĐNTT", Fund.NgoaiHoi),
        });
    }

    // ===== Thông tri =====

    [Fact]
    public void ThongTri_fills_the_project_row_the_lines_and_the_total()
    {
        var result = FormDefinitions.ThongTri.Build(Context(Keys((0, 0, 0, 0), (0, 0, 0, 1))));

        result.Error.ShouldBeNull();
        var sheet = result.Sheet;
        Cell(sheet, 15, 7).DisplayText.ShouldBe("Dự án A");
        Cell(sheet, 15, 8).Segments.Single().Number.ShouldBe(206_000_000m);   // 10tr + (200tr − 4tr)
        Cell(sheet, 16, 7).DisplayText.ShouldBe("Vị trí 1");
        Cell(sheet, 17, 7).DisplayText.ShouldBe("Tạm ứng lần 1");
        Cell(sheet, 24, 8).Segments.Single().Number.ShouldBe(206_000_000m);
        Cell(sheet, 25, 3).DisplayText.ShouldNotBeNullOrWhiteSpace();         // bằng chữ
    }

    [Fact]
    public void ThongTri_defaults_to_NSNN_and_says_so_in_the_Kinh_phi_line()
    {
        var sheet = FormDefinitions.ThongTri.Build(Context(Keys((0, 0, 0, 0), (0, 0, 0, 2)))).Sheet;

        Cell(sheet, 6, 1).DisplayText.ShouldBe("Kinh phí: NSNN");
        Cell(sheet, 15, 8).Segments.Single().Number.ShouldBe(10_000_000m);     // đợt ngoại hối đã tích vẫn bị bỏ
    }

    [Fact]
    public void ThongTri_for_NgoaiHoi_takes_only_the_NgoaiHoi_dots()
    {
        var inputs = new FormInputSet();
        inputs.Set(ThongTriDefinition.FundKey, ThongTriDefinition.FundStoredValue(Fund.NgoaiHoi));

        var sheet = FormDefinitions.ThongTri.Build(Context(Keys((0, 0, 0, 0), (0, 0, 0, 2), (1, 0, 0, 0)), inputs)).Sheet;

        FormDefinitions.ThongTri.FundFor(inputs).ShouldBe(Fund.NgoaiHoi);
        Cell(sheet, 6, 1).DisplayText.ShouldBe("Kinh phí: Quỹ dự trữ ngoại hối");
        Cell(sheet, 15, 8).Segments.Single().Number.ShouldBe(35_000_000m);     // 30tr + 5tr, bỏ đợt NSNN 10tr
    }

    [Fact]
    public void ThongTri_signature_block_shows_titles_and_the_signer_names_from_settings()
    {
        var context = Context(Keys()) with { Org = Org with { NguoiLap = "Nguyễn Văn A", ThuTruongDonVi = "Trần Văn B" } };

        var sheet = FormDefinitions.ThongTri.Build(context).Sheet;

        Cell(sheet, 27, 1).DisplayText.ShouldBe("Người lập");
        Cell(sheet, 27, 7).DisplayText.ShouldBe("Thủ trưởng đơn vị");
        Cell(sheet, 32, 1).DisplayText.ShouldBe("Nguyễn Văn A");
        Cell(sheet, 32, 7).DisplayText.ShouldBe("Trần Văn B");
    }

    [Fact]
    public void ThongTri_with_nothing_selected_still_resolves_the_whole_sheet()
    {
        var result = FormDefinitions.ThongTri.Build(Context(Keys()));

        result.Error.ShouldBeNull();
        Cell(result.Sheet, 15, 7).DisplayText.ShouldBe("Dự án A");
        Cell(result.Sheet, 24, 8).Segments.Single().Number.ShouldBe(0m);
    }

    [Fact]
    public void ThongTri_keeps_typed_text_in_the_cells_the_Mau_leaves_open()
    {
        var inputs = new FormInputSet();
        inputs.Set("SoThongTri", "07/TT");

        var result = FormDefinitions.ThongTri.Build(Context(Keys(), inputs));

        Cell(result.Sheet, 3, 1).DisplayText.ShouldBe("Số      07/TT");
    }

    // ===== KS chi =====

    [Fact]
    public void KsChi_splits_the_amounts_by_fund_and_defaults_the_note_to_the_first_dot_content()
    {
        var result = FormDefinitions.KsChi.Build(
            Context(Keys((0, 0, 0, 0), (0, 0, 0, 2))));

        var sheet = result.Sheet;
        Cell(sheet, 7, 3).Segments.Single().Number.ShouldBe(40_000_000m);     // chấp nhận: tổng
        Cell(sheet, 7, 4).Segments.Single().Number.ShouldBe(10_000_000m);     // NSNN
        Cell(sheet, 7, 5).Segments.Single().Number.ShouldBe(30_000_000m);     // ngoại hối
        Cell(sheet, 17, 3).Segments.Single().Number.ShouldBe(40_000_000m);    // số trả = chấp nhận − thu hồi
        Cell(sheet, 22, 2).DisplayText.ShouldBe("Tạm ứng lần 1");
        Cell(sheet, 22, 2).Segments.Single().IsOverridden.ShouldBeFalse();
    }

    [Fact]
    public void KsChi_note_override_wins_over_the_first_dot()
    {
        var inputs = new FormInputSet();
        inputs.Set("GhiChu", "Ghi chú tự gõ");

        var result = FormDefinitions.KsChi.Build(Context(Keys((0, 0, 0, 0)), inputs));

        Cell(result.Sheet, 22, 2).DisplayText.ShouldBe("Ghi chú tự gõ");
        Cell(result.Sheet, 22, 2).Segments.Single().IsOverridden.ShouldBeTrue();
    }

    [Fact]
    public void KsChi_typed_numbers_do_not_change_the_computed_amounts()
    {
        var inputs = new FormInputSet();
        inputs.Set("ThueGTGT", FormInputSet.ToStored(5_000_000m));

        var sheet = FormDefinitions.KsChi.Build(Context(Keys((0, 0, 0, 0)), inputs)).Sheet;

        Cell(sheet, 16, 3).Segments.Single().Number.ShouldBe(5_000_000m);
        Cell(sheet, 17, 3).Segments.Single().Number.ShouldBe(10_000_000m);
    }

    // ===== GĐNTT =====

    [Fact]
    public void Gdntt_for_one_package_fills_every_computed_cell()
    {
        var result = FormDefinitions.Gdntt.Build(Context(Keys((0, 0, 0, 2))));

        result.Error.ShouldBeNull();
        var sheet = result.Sheet;
        Cell(sheet, 18, 1).DisplayText.ShouldBe("Gói A");
        Cell(sheet, 18, 4).Segments.Single().Number.ShouldBe(52_000_000_000m);   // giá trị HĐ + phụ lục
        Cell(sheet, 18, 8).Segments.Single().Number.ShouldBe(30_000_000m);
        Cell(sheet, 19, 9).Segments.Single().Number.ShouldBe(30_000_000m);
        Cell(sheet, 27, 3).DisplayText.ShouldBe("Cty XL A");
        Cell(sheet, 28, 4).DisplayText.ShouldBe("123456789");
        Cell(sheet, 8, 3).DisplayText.ShouldBe("Dự án A");                      // tên dự án điền sẵn
        Cell(sheet, 8, 3).Segments.Single().IsOverridden.ShouldBeFalse();
    }

    [Fact]
    public void Gdntt_paid_to_contractor_subtracts_the_typed_withholdings()
    {
        var inputs = new FormInputSet();
        inputs.Set("ChuyenTienBaoHanh", FormInputSet.ToStored(1_000_000m));
        inputs.Set("ChuyenTienTamGiu", FormInputSet.ToStored(2_000_000m));

        var sheet = FormDefinitions.Gdntt.Build(Context(Keys((0, 0, 0, 2)), inputs)).Sheet;

        Cell(sheet, 26, 9).Segments.Single().Number.ShouldBe(27_000_000m);
    }

    [Theory]
    [InlineData(false, "chưa chọn gói thầu")]
    [InlineData(true, "đã chọn đợt của nhiều gói thầu")]
    public void Gdntt_blocked_still_resolves_the_sheet(bool twoPackages, string reason)
    {
        var selected = twoPackages ? Keys((0, 0, 0, 2), (1, 0, 0, 0)) : Keys();
        var inputs = new FormInputSet();
        inputs.Set("KinhGui", "Kho bạc Nhà nước");

        var result = FormDefinitions.Gdntt.Build(Context(selected, inputs));

        result.Error.ShouldNotBeNullOrWhiteSpace();
        result.PrintBlockReason.ShouldBe(reason);
        result.CanOutput.ShouldBeFalse();
        Cell(result.Sheet, 6, 1).DisplayText.ShouldBe("Kính gửi: Kho bạc Nhà nước");   // vẫn gõ được, vẫn hiện
        Cell(result.Sheet, 18, 1).DisplayText.ShouldBe("");
        Cell(result.Sheet, 8, 3).DisplayText.ShouldBe("Dự án A");
    }

    // ===== chung =====

    [Fact]
    public void Every_definition_supplies_every_computed_key_of_its_template()
    {
        // Resolve ném nếu thiếu khoá tự tính; chạy với một lựa chọn hợp lệ cho từng biểu mẫu.
        var all = Keys((0, 0, 0, 0), (0, 0, 0, 1));
        FormDefinitions.ThongTri.Build(Context(all)).Error.ShouldBeNull();
        FormDefinitions.KsChi.Build(Context(all)).Error.ShouldBeNull();
        FormDefinitions.Gdntt.Build(Context(Keys((0, 0, 0, 2)))).Error.ShouldBeNull();
    }

    [Fact]
    public void Org_edit_reaches_both_forms_that_show_it()
    {
        var org = new FormOrgInfo("Đơn vị mới", "Ban mới");
        var context = Context(Keys()) with { Org = org };

        Cell(FormDefinitions.ThongTri.Build(context).Sheet, 1, 1).DisplayText.ShouldBe("Đơn vị mới");
        Cell(FormDefinitions.Gdntt.Build(context).Sheet, 2, 1).DisplayText.ShouldBe("Ban mới");
    }
}
