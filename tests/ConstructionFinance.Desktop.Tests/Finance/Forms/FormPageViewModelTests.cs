using ConstructionFinance.Application.Forms;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Desktop.ViewModels.Finance.Forms;
using ConstructionFinance.Domain.Finance;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance.Forms;

public class FormPageViewModelTests
{
    private static readonly FormOrgInfo Org = new("Sở Tài chính", "Ban QLDA số 1");

    private static FormDotRow Dot(FormDotKey key, Fund fund, decimal advance, string content, string package = "Gói A") =>
        new(key, "Vị trí 1", "CHI PHÍ XÂY DỰNG", package, "Cty XL A", "123456789",
            50_000_000_000m, 0m, new DateOnly(2026, 3, 1), content, "CT01", fund, advance, 0m, 0m, advance);

    private static IReadOnlyList<FormDotRow> Rows() => new[]
    {
        Dot(new(0, 0, 0, 0), Fund.NSNN, 10_000_000m, "Tạm ứng lần 1"),
        Dot(new(0, 0, 0, 1), Fund.NgoaiHoi, 30_000_000m, "Ngoại hối 1"),
        Dot(new(1, 0, 0, 0), Fund.NgoaiHoi, 5_000_000m, "Ngoại hối 2", package: "Gói B"),
    };

    private static (FormPageViewModel Vm, FakeDialogService Dialog, Mock<IFormExcelExporter> Exporter) Create(
        FormDefinition definition)
    {
        var dialog = new FakeDialogService();
        var exporter = new Mock<IFormExcelExporter>();
        var vm = new FormPageViewModel(definition, exporter.Object, dialog) { Year = 2026 };
        vm.Refresh(Rows(), Array.Empty<LedgerRowViewModel>(), Org, "Dự án A");
        return (vm, dialog, exporter);
    }

    private static ResolvedSegment Segment(FormPageViewModel vm, string key) =>
        vm.Sheet.Cells.SelectMany(c => c.Segments).First(s => s.Key == key);

    private static void SelectAll(FormPageViewModel vm)
    {
        foreach (var item in vm.Picker.Items) item.IsSelected = true;
    }

    // ===== dựng tờ =====

    [Fact]
    public void Sheet_is_available_before_anything_is_selected()
    {
        var (vm, _, _) = Create(FormDefinitions.ThongTri);

        vm.Sheet.ShouldNotBeNull();
        vm.CanExport.ShouldBeTrue();
        vm.ChooseDotsText.ShouldBe("Chọn đợt (0)…");
    }

    [Fact]
    public void Selecting_dots_rebuilds_the_sheet()
    {
        var (vm, _, _) = Create(FormDefinitions.ThongTri);

        SelectAll(vm);

        vm.Sheet.Cells.Single(c => c.Row == 15 && c.Col == 8).Segments.Single().Number.ShouldBe(10_000_000m);
        vm.ChooseDotsText.ShouldBe("Chọn đợt (1)…");
        vm.StatusText.ShouldBe("Đang chọn 1 đợt");
    }

    [Fact]
    public void Picker_lists_only_the_dots_of_the_form_fund()
    {
        Create(FormDefinitions.ThongTri).Vm.Picker.Items.Count.ShouldBe(1);
        Create(FormDefinitions.Gdntt).Vm.Picker.Items.Count.ShouldBe(2);
        Create(FormDefinitions.KsChi).Vm.Picker.Items.Count.ShouldBe(3);
    }

    // ===== gõ chữ =====

    [Fact]
    public void Typing_text_updates_the_sheet_and_raises_InputsChanged_once()
    {
        var (vm, _, _) = Create(FormDefinitions.ThongTri);
        var raised = 0;
        vm.InputsChanged += (_, _) => raised++;

        vm.SetInput("SoThongTri", "12/TT");

        Segment(vm, "SoThongTri").Value.ShouldBe("12/TT");
        raised.ShouldBe(1);

        vm.SetInput("SoThongTri", "12/TT");     // không đổi → không báo
        raised.ShouldBe(1);
    }

    [Fact]
    public void Setting_the_text_that_is_already_showing_is_not_a_change_even_when_empty()
    {
        var (vm, _, _) = Create(FormDefinitions.ThongTri);
        var raised = 0;
        vm.InputsChanged += (_, _) => raised++;

        vm.SetInput("SoThongTri", "");     // ô còn trống, TextBox báo ""
        vm.SetInput("Khoan", "011");       // đúng giá trị điền sẵn
        vm.SetInput("SoThongTri", null);

        raised.ShouldBe(0);
        vm.ToFormInputs().ShouldBeEmpty();
    }

    [Fact]
    public void Typing_a_number_keeps_it_as_a_number_and_ignores_garbage()
    {
        var (vm, _, _) = Create(FormDefinitions.KsChi);

        vm.SetInput("ThueGTGT", "1.500.000");
        Segment(vm, "ThueGTGT").Number.ShouldBe(1_500_000m);

        vm.SetInput("ThueGTGT", "abc");
        Typing_garbage_in_a_number_cell_keeps_the_old_value(vm);

        vm.SetInput("ThueGTGT", "");
        Segment(vm, "ThueGTGT").Number.ShouldBeNull();
        vm.ToFormInputs().Any(i => i.Key == "ThueGTGT").ShouldBeFalse();
    }

    private static void Typing_garbage_in_a_number_cell_keeps_the_old_value(FormPageViewModel vm) =>
        Segment(vm, "ThueGTGT").Number.ShouldBe(1_500_000m);

    [Fact]
    public void Typing_a_negative_number_is_accepted()
    {
        var (vm, _, _) = Create(FormDefinitions.KsChi);

        vm.SetInput("SoTuChoi", "-500");

        Segment(vm, "SoTuChoi").Number.ShouldBe(-500m);
    }

    [Fact]
    public void Setting_input_edits_the_org_and_reports_it_instead_of_storing_it_per_project()
    {
        var (vm, _, _) = Create(FormDefinitions.ThongTri);
        OrgEditedEventArgs? seen = null;
        vm.OrgEdited += (_, e) => seen = e;
        var inputsRaised = 0;
        vm.InputsChanged += (_, _) => inputsRaised++;

        vm.SetInput(FormOrgKeys.DonViCapTren, "Bộ Tài chính");

        seen.ShouldBe(new OrgEditedEventArgs(FormOrgKeys.DonViCapTren, "Bộ Tài chính"));
        vm.Sheet.Cells.Single(c => c.Row == 1 && c.Col == 1).DisplayText.ShouldBe("Bộ Tài chính");
        vm.ToFormInputs().ShouldBeEmpty();
        inputsRaised.ShouldBe(0);
    }

    // ===== tự tính cho gõ đè =====

    [Fact]
    public void Overridable_text_is_overridden_when_typed_and_reverts_when_cleared_or_equal_to_automatic()
    {
        var (vm, _, _) = Create(FormDefinitions.KsChi);
        SelectAll(vm);

        Segment(vm, "GhiChu").IsOverridden.ShouldBeFalse();
        Segment(vm, "GhiChu").Value.ShouldBe("Tạm ứng lần 1");

        vm.SetInput("GhiChu", "Ghi chú của tôi");
        Segment(vm, "GhiChu").IsOverridden.ShouldBeTrue();

        vm.SetInput("GhiChu", "");
        Segment(vm, "GhiChu").IsOverridden.ShouldBeFalse();

        vm.SetInput("GhiChu", "Ghi chú của tôi");
        vm.SetInput("GhiChu", "Tạm ứng lần 1");     // gõ đúng giá trị tự động → bỏ gõ đè
        Segment(vm, "GhiChu").IsOverridden.ShouldBeFalse();
        vm.ToFormInputs().Any(i => i.Key == "GhiChu").ShouldBeFalse();
    }

    [Fact]
    public void ResetOverride_returns_to_automatic_and_marks_changed()
    {
        var (vm, _, _) = Create(FormDefinitions.KsChi);
        SelectAll(vm);
        vm.SetInput("GhiChu", "Ghi chú của tôi");
        var raised = 0;
        vm.InputsChanged += (_, _) => raised++;

        vm.ResetOverride("GhiChu");

        Segment(vm, "GhiChu").IsOverridden.ShouldBeFalse();
        Segment(vm, "GhiChu").Value.ShouldBe("Tạm ứng lần 1");
        raised.ShouldBe(1);

        vm.ResetOverride("GhiChu");      // đã tự động → không báo thừa
        raised.ShouldBe(1);
    }

    [Fact]
    public void Overridable_number_override_can_be_a_different_number_than_the_computed_one()
    {
        var (vm, _, _) = Create(FormDefinitions.Gdntt);
        foreach (var item in vm.Picker.Items.Where(i => i.Data.PackageName == "Gói A")) item.IsSelected = true;
        Segment(vm, "UnrecoveredAdvanceBefore").Number.ShouldBe(10_000_000m);   // đợt 0 (tạm ứng 10tr) đứng trước đợt được chọn

        vm.SetInput("UnrecoveredAdvanceBefore", "15.000.000");

        Segment(vm, "UnrecoveredAdvanceBefore").Number.ShouldBe(15_000_000m);
        Segment(vm, "UnrecoveredAdvanceBefore").IsOverridden.ShouldBeTrue();
    }

    // ===== GĐNTT bị chặn =====

    [Fact]
    public void Gdntt_blocked_still_accepts_typing()
    {
        var (vm, _, _) = Create(FormDefinitions.Gdntt);
        SelectAll(vm);   // hai gói → bị chặn

        vm.CanExport.ShouldBeFalse();
        vm.HasError.ShouldBeTrue();
        vm.StatusText.ShouldBe(vm.ErrorMessage);
        vm.PrintBlockReason.ShouldBe("đã chọn đợt của nhiều gói thầu");

        vm.SetInput("KinhGui", "Kho bạc Nhà nước");

        Segment(vm, "KinhGui").Value.ShouldBe("Kho bạc Nhà nước");
        vm.ExportCommand.CanExecute(null).ShouldBeFalse();
    }

    // ===== xuất =====

    [Fact]
    public async Task Export_writes_the_current_sheet_through_the_exporter()
    {
        var (vm, dialog, exporter) = Create(FormDefinitions.ThongTri);
        dialog.PickSaveFileResult = "/tmp/out.xlsx";

        await vm.ExportCommand.ExecuteAsync(null);

        exporter.Verify(e => e.ExportAsync(vm.Sheet, "/tmp/out.xlsx", It.IsAny<CancellationToken>()), Times.Once);
        dialog.SaveFileRequests.Single().DefaultFileName.ShouldStartWith("ThongTri_Dự_án_A");
        dialog.ShownMessages.ShouldContain(m => m.Contains("Đã xuất"));
    }

    [Fact]
    public async Task Export_cancelled_in_the_file_picker_does_nothing()
    {
        var (vm, dialog, exporter) = Create(FormDefinitions.ThongTri);
        dialog.PickSaveFileResult = null;

        await vm.ExportCommand.ExecuteAsync(null);

        exporter.Verify(e => e.ExportAsync(It.IsAny<ResolvedSheet>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Export_failure_is_reported_not_thrown()
    {
        var (vm, dialog, exporter) = Create(FormDefinitions.ThongTri);
        dialog.PickSaveFileResult = "/tmp/out.xlsx";
        exporter.Setup(e => e.ExportAsync(It.IsAny<ResolvedSheet>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("đĩa đầy"));

        await vm.ExportCommand.ExecuteAsync(null);

        dialog.ShownMessages.ShouldContain(m => m.Contains("đĩa đầy"));
    }

    // ===== hộp thoại chọn đợt =====

    [Fact]
    public async Task ChooseDots_ok_keeps_the_new_selection()
    {
        var (vm, dialog, _) = Create(FormDefinitions.ThongTri);
        dialog.DialogResult = true;
        dialog.OnDialog = picker => ((FormDotPickerViewModel)picker).Items[0].IsSelected = true;

        await vm.ChooseDotsCommand.ExecuteAsync(null);

        dialog.OpenedDialogs.Single().ShouldBeSameAs(vm.Picker);
        vm.Picker.SelectedCount.ShouldBe(1);
        vm.ChooseDotsText.ShouldBe("Chọn đợt (1)…");
    }

    [Fact]
    public async Task ChooseDots_cancel_restores_the_selection_from_before_the_dialog()
    {
        var (vm, dialog, _) = Create(FormDefinitions.KsChi);
        vm.Picker.Items[0].IsSelected = true;
        dialog.DialogResult = false;
        dialog.OnDialog = picker =>
        {
            var p = (FormDotPickerViewModel)picker;
            p.Items[0].IsSelected = false;
            p.Items[1].IsSelected = true;
            p.Items[2].IsSelected = true;
        };

        await vm.ChooseDotsCommand.ExecuteAsync(null);

        vm.Picker.Items.Select(i => i.IsSelected).ShouldBe(new[] { true, false, false });
        vm.ChooseDotsText.ShouldBe("Chọn đợt (1)…");
    }

    // ===== lưu =====

    [Fact]
    public void Inputs_round_trip_through_FormInput_entities()
    {
        var (vm, _, _) = Create(FormDefinitions.ThongTri);
        vm.SetInput("SoThongTri", "12/TT");
        vm.SetInput("Khoan", "");                 // xoá giá trị điền sẵn: phải lưu rỗng, không biến mất

        var saved = vm.ToFormInputs().ToList();

        saved.Select(i => (i.Form, i.Key, i.Value)).OrderBy(t => t.Key).ShouldBe(new (string, string, string?)[]
        {
            ("thongtri", "Khoan", ""),
            ("thongtri", "SoThongTri", "12/TT"),
        });

        var (fresh, _, _) = Create(FormDefinitions.ThongTri);
        fresh.LoadInputs(saved.Append(new FormInput { Form = "kschi", Key = "ThueGTGT", Value = "9" }));
        Segment(fresh, "SoThongTri").Value.ShouldBe("12/TT");
        Segment(fresh, "Khoan").Value.ShouldBe("");
        fresh.ToFormInputs().Count().ShouldBe(2);        // dòng của biểu mẫu khác bị bỏ qua
    }

    // ===== cỡ ô =====

    [Fact]
    public void Resizing_stores_layout_keys_marks_changed_and_reset_removes_only_them()
    {
        var (vm, _, _) = Create(FormDefinitions.ThongTri);
        vm.SetInput("SoThongTri", "07/TT");
        var raised = 0;
        vm.InputsChanged += (_, _) => raised++;

        vm.SetColumnWidth(7, 40);
        vm.SetColumnWidth(7, 40);                   // cùng cỡ: không báo thừa
        vm.SetRowHeight("dot", 55);

        raised.ShouldBe(2);
        vm.HasCustomLayout.ShouldBeTrue();
        vm.Sheet.ColumnWidths[6].ShouldBe(40);
        vm.ToFormInputs().Select(i => i.Key).ShouldBe(new[] { "SoThongTri", "_w.7", "_h.dot" }, ignoreOrder: true);

        vm.ResetLayoutCommand.Execute(null);

        vm.HasCustomLayout.ShouldBeFalse();
        vm.ToFormInputs().Select(i => i.Key).ShouldBe(new[] { "SoThongTri" });   // chữ đã gõ còn nguyên
        raised.ShouldBe(3);
    }

    [Fact]
    public void Layout_is_clamped_and_survives_save_and_load()
    {
        var (vm, _, _) = Create(FormDefinitions.KsChi);
        vm.SetColumnWidth(2, 5000);
        vm.SetRowHeight("r7", 0.1);
        var saved = vm.ToFormInputs().ToList();

        var (fresh, _, _) = Create(FormDefinitions.KsChi);
        fresh.LoadInputs(saved);

        fresh.Sheet.ColumnWidths[1].ShouldBe(FormLayoutKeys.MaxWidth);
        fresh.Sheet.RowHeights[6].ShouldBe(FormLayoutKeys.MinHeight);
        fresh.HasCustomLayout.ShouldBeTrue();
    }
}
