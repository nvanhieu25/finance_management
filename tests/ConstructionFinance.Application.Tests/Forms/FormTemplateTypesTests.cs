using ConstructionFinance.Application.Forms;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Application.Tests.Forms;

public class FormTemplateTypesTests
{
    [Fact]
    public void Borders_parse_lowercase_is_thin_and_uppercase_is_hair()
    {
        var borders = CellBorders.Parse("lrTB");

        borders.Left.ShouldBe(BorderLine.Thin);
        borders.Right.ShouldBe(BorderLine.Thin);
        borders.Top.ShouldBe(BorderLine.Hair);
        borders.Bottom.ShouldBe(BorderLine.Hair);
    }

    [Fact]
    public void Borders_parse_empty_string_is_no_border()
    {
        var borders = CellBorders.Parse("");

        borders.Left.ShouldBe(BorderLine.None);
        borders.Any.ShouldBeFalse();
    }

    [Fact]
    public void Borders_parse_rejects_an_unknown_flag()
    {
        Should.Throw<ArgumentException>(() => CellBorders.Parse("lx"));
    }

    [Theory]
    [InlineData(1234567.0, null, "1.234.567")]
    [InlineData(0.0, "#,##0", "0")]
    [InlineData(5000000.0, "#,##0\\ \"đ\"", "5.000.000 đ")]
    [InlineData(5000000.0, "#,##0\"đ\"", "5.000.000đ")]
    [InlineData(-1500.0, "#,##0_);(#,##0)", "(1.500)")]
    [InlineData(1500.0, "#,##0_);(#,##0)", "1.500")]
    public void Format_groups_with_dots_and_honours_the_suffix_and_parentheses(
        double value, string? format, string expected)
    {
        FormNumberFormat.Format((decimal)value, format).ShouldBe(expected);
    }

    [Fact]
    public void InputSet_number_round_trips_through_the_stored_text()
    {
        var inputs = new FormInputSet();
        inputs.Set("ThueGTGT", FormInputSet.ToStored(1_500_000.5m));

        inputs.GetNumber("ThueGTGT").ShouldBe(1_500_000.5m);
        inputs.TryGetNumber("ChuaCo", out _).ShouldBeFalse();
        inputs.GetNumber("ChuaCo").ShouldBe(0m);
    }

    [Fact]
    public void InputSet_distinguishes_a_missing_key_from_an_explicit_empty_value()
    {
        var inputs = new FormInputSet();
        inputs.Set("A", "");

        inputs.Has("A").ShouldBeTrue();
        inputs.Get("A").ShouldBe("");
        inputs.Has("B").ShouldBeFalse();
        inputs.Get("B").ShouldBeNull();
    }

    [Fact]
    public void OrgKeys_read_and_write_the_two_settings()
    {
        var org = new FormOrgInfo("Sở", "Ban");

        org.Get(FormOrgKeys.DonViCapTren).ShouldBe("Sở");
        org.Get(FormOrgKeys.DonViCoSo).ShouldBe("Ban");
        org.With(FormOrgKeys.DonViCoSo, "Mới").DonViCoSo.ShouldBe("Mới");
        org.With(FormOrgKeys.DonViCoSo, "Mới").DonViCapTren.ShouldBe("Sở");
        Should.Throw<ArgumentException>(() => org.Get("khong_co"));
    }
}
