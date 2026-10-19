using ConstructionFinance.Application.Forms;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Forms;

public class FormSheetResolverTests
{
    private static readonly FormOrgInfo Org = new("Sở Tài chính", "Ban QLDA số 1");

    private static FormValues ThongTriValues(decimal total = 0m) =>
        new FormValues().Number("Total", total).Text("TotalInWords", "Không đồng.");

    private static ResolvedSheet ResolveThongTri(
        FormInputSet? inputs = null, IReadOnlyList<ThongTriLine>? lines = null, int year = 2031, FormOrgInfo? org = null) =>
        FormSheetResolver.Resolve(ThongTriTemplate.Create(), ThongTriValues(), inputs ?? new FormInputSet(),
            org ?? Org, year, lines ?? new[] { new ThongTriLine(0, "Dự án A", 0m) });

    internal static ResolvedCell Cell(ResolvedSheet sheet, int row, int col) =>
        sheet.Cells.Single(c => c.Row == row && c.Col == col);

    private static IReadOnlyList<ThongTriLine> Lines(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new ThongTriLine(i == 0 ? 0 : i % 2 == 1 ? 1 : 2, $"Dòng {i}", 1_000m * i))
            .ToList();

    // ===== chữ, năm, ô trống =====

    [Fact]
    public void Resolve_substitutes_the_year_in_template_text()
    {
        var sheet = ResolveThongTri(year: 2031);

        Cell(sheet, 9, 1).DisplayText.ShouldBe("Về tháng      năm 2031");
    }

    [Fact]
    public void User_text_containing_the_year_token_is_not_substituted()
    {
        var inputs = new FormInputSet();
        inputs.Set("VeThangNam", "{năm}");

        var sheet = ResolveThongTri(inputs, year: 2031);

        Cell(sheet, 9, 1).DisplayText.ShouldBe("Về{năm}");
    }

    [Fact]
    public void Empty_input_prints_the_Mau_placeholder_and_is_flagged_as_placeholder()
    {
        var sheet = ResolveThongTri();
        var cell = Cell(sheet, 3, 1);

        cell.DisplayText.ShouldBe("Số      .............");
        var input = cell.Segments.Single(s => s.Kind == SegmentKind.Input);
        input.IsPlaceholder.ShouldBeTrue();
        input.Value.ShouldBe("");
        input.Hint.ShouldBe(".............");
    }

    [Fact]
    public void Typed_input_replaces_the_placeholder()
    {
        var inputs = new FormInputSet();
        inputs.Set("SoThongTri", "12/TT");

        var sheet = ResolveThongTri(inputs);
        var input = Cell(sheet, 3, 1).Segments.Single(s => s.Kind == SegmentKind.Input);

        Cell(sheet, 3, 1).DisplayText.ShouldBe("Số      12/TT");
        input.IsPlaceholder.ShouldBeFalse();
        input.Value.ShouldBe("12/TT");
    }

    [Fact]
    public void Default_is_used_until_the_user_types_and_an_explicit_empty_clears_it()
    {
        string KhoanText(FormInputSet inputs) =>
            Cell(ResolveThongTri(inputs), 15, 2).Segments.Single().Text;

        KhoanText(new FormInputSet()).ShouldBe("011");

        var cleared = new FormInputSet();
        cleared.Set("Khoan", "");
        KhoanText(cleared).ShouldBe("");

        var typed = new FormInputSet();
        typed.Set("Khoan", "012");
        KhoanText(typed).ShouldBe("012");
    }

    [Fact]
    public void Setting_input_reads_the_org_value_and_falls_back_to_the_Mau_text()
    {
        var withOrg = Cell(ResolveThongTri(), 1, 1);
        withOrg.DisplayText.ShouldBe("Sở Tài chính");

        var blankOrg = Cell(ResolveThongTri(org: new FormOrgInfo(null, "  ".Trim())), 1, 1);
        blankOrg.DisplayText.ShouldBe("Đơn vị cấp trên");
        blankOrg.Segments.Single().IsPlaceholder.ShouldBeTrue();
    }

    // ===== số =====

    [Fact]
    public void Number_input_formats_with_dots_and_stays_blank_until_typed()
    {
        var kschi = KsChiTemplate.Create();
        var values = new FormValues { BlankMissing = true };

        ResolvedCell Cell14(FormInputSet inputs) => Cell(
            FormSheetResolver.Resolve(kschi, values, inputs, Org, 2026), 14, 3);

        Cell14(new FormInputSet()).DisplayText.ShouldBe("");
        var typed = new FormInputSet();
        typed.Set("ThuHoiCacNamTruoc", FormInputSet.ToStored(1_500_000m));
        var cell = Cell14(typed);
        cell.DisplayText.ShouldBe("1.500.000");
        cell.Segments.Single().Number.ShouldBe(1_500_000m);
        cell.Segments.Single().Value.ShouldBe("1.500.000");
    }

    [Fact]
    public void Computed_number_shows_zero_as_zero_like_the_Mau()
    {
        var sheet = ResolveThongTri();

        Cell(sheet, 24, 8).DisplayText.ShouldBe("0");
        Cell(sheet, 24, 8).Segments.Single().Number.ShouldBe(0m);
    }

    [Fact]
    public void Computed_number_without_a_cell_format_uses_the_default_grouping()
    {
        var kschi = KsChiTemplate.Create();
        var values = new FormValues { BlankMissing = true }
            .Number("Accepted.NgoaiHoi", 2_000_000m);

        var sheet = FormSheetResolver.Resolve(kschi, values, new FormInputSet(), Org, 2026);

        Cell(sheet, 7, 5).DisplayText.ShouldBe("2.000.000");
    }

    // ===== tự tính cho gõ đè =====

    [Fact]
    public void Overridable_text_shows_the_computed_value_until_typed_over()
    {
        var gdntt = GdnttTemplate.Create();
        var values = new FormValues { BlankMissing = true }.Text("TenDuAnCongTrinh", "Dự án A");

        ResolvedSegment Segment(FormInputSet inputs) =>
            Cell(FormSheetResolver.Resolve(gdntt, values, inputs, Org, 2026), 8, 3).Segments.Single();

        var automatic = Segment(new FormInputSet());
        automatic.Text.ShouldBe("Dự án A");
        automatic.IsOverridden.ShouldBeFalse();

        var typed = new FormInputSet();
        typed.Set("TenDuAnCongTrinh", "Tên tôi tự gõ");
        var overridden = Segment(typed);
        overridden.Text.ShouldBe("Tên tôi tự gõ");
        overridden.IsOverridden.ShouldBeTrue();
    }

    [Fact]
    public void Overridable_number_parses_the_stored_text_and_ignores_garbage()
    {
        var gdntt = GdnttTemplate.Create();
        var values = new FormValues { BlankMissing = true }.Number("UnrecoveredAdvanceBefore", 6_000_000m);

        ResolvedSegment Segment(FormInputSet inputs) =>
            Cell(FormSheetResolver.Resolve(gdntt, values, inputs, Org, 2026), 13, 8).Segments.Single();

        Segment(new FormInputSet()).Number.ShouldBe(6_000_000m);

        var typed = new FormInputSet();
        typed.Set("UnrecoveredAdvanceBefore", FormInputSet.ToStored(15_000_000m));
        var overridden = Segment(typed);
        overridden.Number.ShouldBe(15_000_000m);
        overridden.IsOverridden.ShouldBeTrue();
        overridden.Text.ShouldBe("15.000.000 đ");

        var garbage = new FormInputSet();
        garbage.Set("UnrecoveredAdvanceBefore", "abc");
        var fallback = Segment(garbage);
        fallback.Number.ShouldBe(6_000_000m);
        fallback.IsOverridden.ShouldBeFalse();
    }

    // ===== khoá thiếu =====

    [Fact]
    public void Missing_computed_key_throws_unless_the_values_blank_missing_keys()
    {
        var template = ThongTriTemplate.Create();
        var lines = new[] { new ThongTriLine(0, "DA", 0m) };

        Should.Throw<InvalidOperationException>(() =>
            FormSheetResolver.Resolve(template, new FormValues(), new FormInputSet(), Org, 2026, lines))
            .Message.ShouldContain("Total");

        var sheet = FormSheetResolver.Resolve(template, new FormValues { BlankMissing = true },
            new FormInputSet(), Org, 2026, lines);
        Cell(sheet, 24, 8).DisplayText.ShouldBe("");
    }

    // ===== vùng lặp của Thông tri =====

    [Fact]
    public void Resolve_with_one_project_line_fills_the_rest_of_the_region_with_blank_rows()
    {
        var sheet = ResolveThongTri();

        Cell(sheet, 15, 7).DisplayText.ShouldBe("Dự án A");
        sheet.RowHeights[14].ShouldBe(24.95, 0.001);           // hàng 15
        for (var row = 16; row <= 23; row++)
            sheet.RowHeights[row - 1].ShouldBe(24.95, 0.001);  // hàng trống
        Cell(sheet, 24, 7).DisplayText.ShouldBe("Cộng");
    }

    [Fact]
    public void Resolve_with_exactly_nine_lines_keeps_Cong_on_row_24()
    {
        var sheet = ResolveThongTri(lines: Lines(9));

        Cell(sheet, 24, 7).DisplayText.ShouldBe("Cộng");
        Cell(sheet, 25, 1).DisplayText.ShouldBe("Bằng chữ: ");
    }

    [Fact]
    public void Resolve_inserts_rows_before_Cong_when_there_are_more_than_nine_lines()
    {
        var sheet = ResolveThongTri(lines: Lines(30));

        Cell(sheet, 15 + 29, 7).DisplayText.ShouldBe("Dòng 29");
        Cell(sheet, 45, 7).DisplayText.ShouldBe("Cộng");       // 24 + 21 dòng thêm
        Cell(sheet, 46, 1).DisplayText.ShouldBe("Bằng chữ: ");
        sheet.RowHeights.Count.ShouldBeGreaterThanOrEqualTo(54); // 33 + 21: các hàng chữ ký cũng dời
        sheet.Cells.Any(c => c.Row == 24 && c.DisplayText == "Cộng").ShouldBeFalse();
    }

    [Fact]
    public void Region_rows_take_the_prototype_of_their_level()
    {
        var lines = new[]
        {
            new ThongTriLine(0, "DA", 300m), new ThongTriLine(1, "Vị trí 1", 300m), new ThongTriLine(2, "Đợt 1", 300m)
        };

        var sheet = ResolveThongTri(lines: lines);

        sheet.RowHeights[15].ShouldBe(24.95, 0.001);   // hàng 16: vị trí
        sheet.RowHeights[16].ShouldBe(47.25, 0.001);   // hàng 17: đợt
        Cell(sheet, 17, 7).DisplayText.ShouldBe("Đợt 1");
        Cell(sheet, 17, 8).Segments.Single().Number.ShouldBe(300m);
        Cell(sheet, 16, 7).Style.Bold.ShouldBeTrue();
        Cell(sheet, 17, 7).Style.Bold.ShouldBeFalse();
    }

    [Fact]
    public void Project_row_has_the_default_budget_codes_and_every_vitri_and_dot_row_is_typable_but_blank()
    {
        var sheet = ResolveThongTri(lines: new[]
        {
            new ThongTriLine(0, "DA", 300m),
            new ThongTriLine(1, "Vị trí 1", 300m, "v0"),
            new ThongTriLine(2, "Đợt 1", 300m, "d0.0.0.0"),
        });

        Cell(sheet, 15, 1).DisplayText.ShouldBe("010");
        Cell(sheet, 16, 1).IsEditable.ShouldBeTrue();
        Cell(sheet, 16, 1).DisplayText.ShouldBe("");
        Cell(sheet, 17, 6).IsEditable.ShouldBeTrue();
        Cell(sheet, 16, 3).Segments[0].Key.ShouldBe("Muc@v0");
        Cell(sheet, 17, 3).Segments[0].Key.ShouldBe("Muc@d0.0.0.0");
    }

    [Fact]
    public void Budget_codes_typed_on_a_row_stay_on_that_row_only()
    {
        var lines = new[]
        {
            new ThongTriLine(0, "DA", 300m),
            new ThongTriLine(1, "Vị trí 1", 300m, "v0"),
            new ThongTriLine(2, "Đợt 1", 200m, "d0.0.0.0"),
            new ThongTriLine(2, "Đợt 2", 100m, "d0.0.0.1"),
        };
        var inputs = new FormInputSet();
        inputs.Set("Muc@d0.0.0.1", "9302");

        var sheet = FormSheetResolver.Resolve(ThongTriTemplate.Create(), ThongTriValues(), inputs, Org, 2026, lines);

        Cell(sheet, 17, 3).DisplayText.ShouldBe("");
        Cell(sheet, 18, 3).DisplayText.ShouldBe("9302");
        Cell(sheet, 15, 3).DisplayText.ShouldBe("9300");
    }

    [Fact]
    public void Line_scoped_keys_map_back_to_the_template_key()
    {
        FormSheetResolver.TemplateKey("Muc@d0.0.0.1").ShouldBe("Muc@line");
        FormSheetResolver.TemplateKey("Muc").ShouldBe("Muc");
        FormSheetResolver.StoredKey("Muc@line", new ThongTriLine(2, "x", 1m, "d1")).ShouldBe("Muc@d1");
        FormSheetResolver.StoredKey("Muc@line", new ThongTriLine(0, "x", 1m)).ShouldBe("Muc");
    }

    [Fact]
    public void Zero_lines_leaves_a_blank_region_without_throwing()
    {
        var sheet = FormSheetResolver.Resolve(ThongTriTemplate.Create(), ThongTriValues(), new FormInputSet(),
            Org, 2026, Array.Empty<ThongTriLine>());

        sheet.Cells.Any(c => c.Row == 15 && c.Col == 1 && c.IsEditable).ShouldBeFalse();
        Cell(sheet, 24, 7).DisplayText.ShouldBe("Cộng");
    }

    // ===== chữ tràn sang ô trống bên phải =====

    [Fact]
    public void Left_text_overflows_into_empty_cells_and_stops_at_the_next_content()
    {
        var sheet = ResolveThongTri();

        Cell(sheet, 10, 1).TextColSpan.ShouldBe(8);   // Tên CTMT… tràn hết tới cột H
        Cell(sheet, 8, 3).TextColSpan.ShouldBe(4);    // "Đơn vị" ô C8 dừng trước G8:H8
    }

    [Fact]
    public void Wrapped_merged_centered_and_numeric_cells_do_not_overflow()
    {
        var sheet = ResolveThongTri();

        Cell(sheet, 4, 1).TextColSpan.ShouldBe(Cell(sheet, 4, 1).ColSpan);   // gộp
        Cell(sheet, 14, 4).TextColSpan.ShouldBe(1);                          // "D" giữa ô
        Cell(sheet, 24, 8).TextColSpan.ShouldBe(1);                          // số
    }

    [Fact]
    public void Row_heights_default_to_15_for_rows_without_an_explicit_height()
    {
        var sheet = ResolveThongTri();

        sheet.RowHeights[13].ShouldBe(15.0, 0.001);                          // hàng 14: Mẫu để mặc định
        sheet.RowCount.ShouldBe(33);
        sheet.ColumnCount.ShouldBe(8);
    }

    // ===== cỡ ô người dùng đã kéo =====

    [Fact]
    public void Without_layout_keys_the_sheet_has_exactly_the_Mau_sizes()
    {
        var sheet = ResolveThongTri();

        sheet.ColumnWidths.ShouldBe(ThongTriTemplate.Create().ColumnWidths);
        sheet.RowHeights[3].ShouldBe(ThongTriTemplate.Create().RowHeights[4]);   // hàng 4
    }

    [Fact]
    public void A_custom_column_width_overrides_the_Mau_and_garbage_or_extremes_are_handled()
    {
        var inputs = new FormInputSet();
        inputs.Set(FormLayoutKeys.Width(7), FormLayoutKeys.ToStored(48.5));
        inputs.Set(FormLayoutKeys.Width(1), "abc");                              // rác: giữ cỡ Mẫu
        inputs.Set(FormLayoutKeys.Width(2), "99999");                            // quá lớn: kẹp ở mức tối đa
        inputs.Set(FormLayoutKeys.Width(3), "-4");                               // âm: kẹp ở mức tối thiểu

        var sheet = ResolveThongTri(inputs);

        sheet.ColumnWidths[6].ShouldBe(48.5);
        sheet.ColumnWidths[0].ShouldBe(ThongTriTemplate.Create().ColumnWidths[0]);
        sheet.ColumnWidths[1].ShouldBe(FormLayoutKeys.MaxWidth);
        sheet.ColumnWidths[2].ShouldBe(FormLayoutKeys.MinWidth);
    }

    [Fact]
    public void A_custom_row_height_follows_its_template_row_when_the_region_grows()
    {
        var inputs = new FormInputSet();
        inputs.Set(FormLayoutKeys.Height("r24"), FormLayoutKeys.ToStored(40));   // hàng "Cộng" của Mẫu

        var few = ResolveThongTri(inputs);
        var many = ResolveThongTri(inputs, Lines(30));

        few.RowHeights[23].ShouldBe(40);                  // hàng 24
        many.RowHeights[44].ShouldBe(40);                 // "Cộng" nằm ở hàng 45 khi có 30 dòng
        many.RowKeys[44].ShouldBe("r24");
        many.RowHeights[43].ShouldNotBe(40);              // hàng ngay trên đó (dòng nội dung) không bị ảnh hưởng
    }

    [Fact]
    public void A_custom_height_for_a_region_kind_applies_to_every_row_of_that_kind()
    {
        var inputs = new FormInputSet();
        inputs.Set(FormLayoutKeys.Height("dot"), FormLayoutKeys.ToStored(70));

        var sheet = ResolveThongTri(inputs, Lines(7));    // dự án, vị trí, đợt, vị trí, đợt, vị trí, đợt

        var dotRows = sheet.RowKeys.Select((key, index) => (key, index)).Where(t => t.key == "dot").Select(t => t.index).ToList();
        dotRows.Count.ShouldBeGreaterThan(1);
        dotRows.ShouldAllBe(index => sheet.RowHeights[index] == 70);
    }

    [Fact]
    public void Layout_keys_are_recognised_and_do_not_collide_with_cell_keys()
    {
        FormLayoutKeys.IsLayoutKey("_w.3").ShouldBeTrue();
        FormLayoutKeys.IsLayoutKey("_h.dot").ShouldBeTrue();
        FormLayoutKeys.IsLayoutKey("SoThongTri").ShouldBeFalse();
    }
}
