using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Desktop.ViewModels.Finance.Forms;
using ConstructionFinance.Desktop.Views.Finance.Forms;
using ConstructionFinance.Domain.Finance;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance.Forms;

public class FormSheetControlTests
{
    private static readonly FormOrgInfo Org = new("Sở Tài chính", "Ban QLDA số 1");

    private static FormDotRow Dot(FormDotKey key, Fund fund, decimal advance, string content, string package = "Gói A") =>
        new(key, "Vị trí 1", "CHI PHÍ XÂY DỰNG", package, "Cty XL A", "123456789",
            50_000_000_000m, 0m, new DateOnly(2026, 3, 1), content, "CT01", fund, advance, 0m, 0m, advance);

    private sealed record Host(Window Window, FormPageView View, FormPageViewModel Vm, FormSheetControl Sheet);

    private static Host Show(FormDefinition definition, bool select = true)
    {
        var vm = new FormPageViewModel(definition, Mock.Of<IFormExcelExporter>(), new FakeDialogService()) { Year = 2026 };
        vm.Refresh(new[]
        {
            Dot(new(0, 0, 0, 0), Fund.NSNN, 10_000_000m, "Tạm ứng lần 1"),
            Dot(new(0, 0, 0, 1), Fund.NgoaiHoi, 30_000_000m, "Ngoại hối 1"),
        }, Array.Empty<LedgerRowViewModel>(), Org, "Dự án A");
        if (select) foreach (var item in vm.Picker.Items) item.IsSelected = true;

        var view = new FormPageView { DataContext = vm };
        var window = new Window { Content = view, Width = 1100, Height = 1000 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return new Host(window, view, vm, view.FindControl<FormSheetControl>("SheetHost")!);
    }

    private static List<TextBox> Boxes(Host host) => host.Sheet.GetVisualDescendants().OfType<TextBox>().ToList();

    private static TextBox Box(Host host, string key) =>
        Boxes(host).Single(box => (string?)box.Tag == key);

    private static void Run(Action action) => HeadlessTestSession.Run(action);

    // ===== hình học =====

    [Theory]
    [InlineData("thongtri")]
    [InlineData("kschi")]
    [InlineData("gdntt")]
    public void Sheet_size_matches_the_template_column_widths_and_row_heights(string id)
    {
        Run(() =>
        {
            var host = Show(FormDefinitions.All.Single(d => d.Id == id));
            var sheet = host.Vm.Sheet;

            var expectedWidth = sheet.ColumnWidths.Sum(w => w * 7.0 + 5.0);
            var expectedHeight = sheet.RowHeights.Sum() * 96.0 / 72.0;
            var canvas = (Canvas)host.Sheet.Child!;

            canvas.Bounds.Width.ShouldBe(expectedWidth, 0.5);
            canvas.Bounds.Height.ShouldBe(expectedHeight, 0.5);
            host.Window.Close();
        });
    }

    [Fact]
    public void Sheet_is_left_aligned_not_stretched_into_a_half_empty_card()
    {
        Run(() =>
        {
            var host = Show(FormDefinitions.ThongTri);

            host.Sheet.Bounds.X.ShouldBeLessThan(40);
            host.Sheet.Bounds.Width.ShouldBeLessThan(900);   // 183mm ≈ 692px + lề giấy
            host.Window.Close();
        });
    }

    // ===== ô nào gõ được =====

    [Fact]
    public void Every_input_segment_has_one_text_box_and_computed_cells_have_none()
    {
        Run(() =>
        {
            var host = Show(FormDefinitions.ThongTri);

            var inputKeys = host.Vm.Sheet.Cells.SelectMany(c => c.Segments)
                .Where(s => s.Kind is SegmentKind.Input or SegmentKind.InputNumber or SegmentKind.SettingInput or SegmentKind.Overridable)
                .Select(s => s.Key!).OrderBy(k => k).ToList();

            Boxes(host).Select(b => (string)b.Tag!).OrderBy(k => k).ToList().ShouldBe(inputKeys);
            inputKeys.ShouldContain("don_vi_cap_tren");     // tên đơn vị trên cùng bên trái: sửa được
            inputKeys.ShouldNotContain("Total");
            host.Window.Close();
        });
    }

    [Fact]
    public void Org_name_cell_shows_the_current_value_and_is_editable()
    {
        Run(() =>
        {
            var host = Show(FormDefinitions.ThongTri);

            Box(host, "don_vi_cap_tren").Text.ShouldBe("Sở Tài chính");
            host.Window.Close();
        });
    }

    [Fact]
    public void Empty_input_shows_the_Mau_placeholder_as_a_dim_watermark()
    {
        Run(() =>
        {
            var host = Show(FormDefinitions.ThongTri);

            var box = Box(host, "SoThongTri");
            box.Text.ShouldBeNullOrEmpty();
            box.Watermark.ShouldBe(".............");
            host.Window.Close();
        });
    }

    // ===== gõ =====

    [Fact]
    public void Typing_into_a_cell_reaches_the_view_model_and_keeps_the_caret_box()
    {
        Run(() =>
        {
            var host = Show(FormDefinitions.ThongTri);
            var box = Box(host, "SoThongTri");

            box.Text = "12/TT";
            Dispatcher.UIThread.RunJobs();

            host.Vm.ToFormInputs().ShouldContain(i => i.Key == "SoThongTri" && i.Value == "12/TT");
            Box(host, "SoThongTri").ShouldBeSameAs(box);          // không dựng lại cả lưới giữa chừng gõ
            host.Window.Close();
        });
    }

    [Fact]
    public void Typing_a_garbage_number_does_not_throw_and_keeps_the_old_value()
    {
        Run(() =>
        {
            var host = Show(FormDefinitions.KsChi);
            var box = Box(host, "ThueGTGT");
            box.Text = "1.500.000";
            Dispatcher.UIThread.RunJobs();

            box.Text = "abc";
            Dispatcher.UIThread.RunJobs();

            host.Vm.Sheet.Cells.SelectMany(c => c.Segments).First(s => s.Key == "ThueGTGT").Number.ShouldBe(1_500_000m);
            host.Window.Close();
        });
    }

    // ===== tự tính =====

    [Fact]
    public void Computed_cells_update_in_place_when_the_selection_changes()
    {
        Run(() =>
        {
            // KS chi có cấu trúc cố định (không vùng lặp): đổi lựa chọn chỉ đổi chữ ô tự tính, không dựng lại ô nhập.
            var host = Show(FormDefinitions.KsChi, select: false);
            var boxesBefore = Boxes(host);

            host.Vm.Picker.Items[0].IsSelected = true;
            Dispatcher.UIThread.RunJobs();

            var texts = host.Sheet.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            texts.ShouldContain("10.000.000");
            Boxes(host).ShouldBe(boxesBefore, ignoreOrder: false);   // cùng các ô nhập (cùng đối tượng)
            host.Window.Close();
        });
    }

    [Fact]
    public void More_lines_than_the_Mau_region_rebuild_the_grid_with_extra_rows()
    {
        Run(() =>
        {
            var host = Show(FormDefinitions.ThongTri, select: false);
            var heightBefore = ((Canvas)host.Sheet.Child!).Height;
            var rows = Enumerable.Range(0, 14)
                .Select(i => Dot(new(0, 0, 0, i), Fund.NSNN, 1_000_000m, $"Đợt {i}")).ToList();
            host.Vm.Refresh(rows, Array.Empty<LedgerRowViewModel>(), Org, "Dự án A");

            foreach (var item in host.Vm.Picker.Items) item.IsSelected = true;
            Dispatcher.UIThread.RunJobs();

            ((Canvas)host.Sheet.Child!).Height.ShouldBeGreaterThan(heightBefore);
            host.Window.Close();
        });
    }

    // ===== gõ đè =====

    [Fact]
    public void Overridden_cell_shows_a_marker_and_a_reset_button_that_returns_to_automatic()
    {
        Run(() =>
        {
            var host = Show(FormDefinitions.KsChi);
            var box = Box(host, "GhiChu");
            box.Text.ShouldBe("Tạm ứng lần 1");

            box.Text = "Ghi chú của tôi";
            Dispatcher.UIThread.RunJobs();

            var reset = host.Sheet.GetVisualDescendants().OfType<Button>().Single(b => (string?)b.Tag == "GhiChu");
            reset.IsVisible.ShouldBeTrue();
            box.BorderThickness.Left.ShouldBeGreaterThan(0);

            host.Vm.ResetOverride("GhiChu");
            Dispatcher.UIThread.RunJobs();

            reset.IsVisible.ShouldBeFalse();
            Box(host, "GhiChu").Text.ShouldBe("Tạm ứng lần 1");
            host.Window.Close();
        });
    }

    // ===== GĐNTT bị chặn =====

    [Fact]
    public void Blocked_gdntt_still_draws_the_sheet_with_editable_cells_and_a_disabled_export()
    {
        Run(() =>
        {
            var host = Show(FormDefinitions.Gdntt, select: false);

            Boxes(host).ShouldNotBeEmpty();
            host.Vm.CanExport.ShouldBeFalse();
            var export = host.View.GetVisualDescendants().OfType<Button>().Single(b => (string?)b.Content == "Xuất Excel");
            export.IsEffectivelyEnabled.ShouldBeFalse();
            host.Window.Close();
        });
    }

    // ===== bảng đợt không còn nằm trong tab =====

    [Fact]
    public void The_dot_table_is_not_on_the_tab_only_the_choose_button_is()
    {
        Run(() =>
        {
            var host = Show(FormDefinitions.ThongTri);

            host.View.GetVisualDescendants().OfType<DataGrid>().ShouldBeEmpty();
            host.View.GetVisualDescendants().OfType<Button>()
                .Any(b => ((string?)b.Content)?.StartsWith("Chọn đợt (") == true).ShouldBeTrue();
            host.Window.Close();
        });
    }
}
