using ConstructionFinance.Desktop.Common;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class VitriLedgerViewModelTests
{
    private static ViTri BuildOneGroupOnePackageTwoDots()
    {
        var viTri = new ViTri { Name = "Vị trí 1", NguonVon = "NSNN", SortOrder = 1 };
        var group = new CostGroup { Name = "Nhóm chi phí 1", SortOrder = 1 };
        var package = new Package
        {
            Name = "Gói thầu A",
            ContractorName = "Cty X",
            BankAccount = "12345",
            ContractValue = 1_000_000m,
            ContractAnnexValue = 0m,
            SortOrder = 1
        };
        // Advance dot: Advance > 0, PaymentKLHT == 0 -> disbursed = Advance
        package.Dots.Add(new Dot
        {
            SortOrder = 1,
            Date = new DateOnly(2026, 3, 1),
            Content = "Tạm ứng",
            Fund = Fund.NSNN,
            Advance = 500_000m,
            PaymentKLHT = 0m,
            RecoveredAdvance = 0m
        });
        // Payment dot: disbursed = PaymentKLHT - RecoveredAdvance
        package.Dots.Add(new Dot
        {
            SortOrder = 2,
            Date = new DateOnly(2026, 4, 1),
            Content = "Thanh toán KLHT",
            Fund = Fund.NSNN,
            Advance = 0m,
            PaymentKLHT = 300_000m,
            RecoveredAdvance = 200_000m
        });
        group.Packages.Add(package);
        viTri.CostGroups.Add(group);
        return viTri;
    }

    [Fact]
    public void Rows_flattens_in_Total_Group_Package_Entry_order()
    {
        var vm = new VitriLedgerViewModel(BuildOneGroupOnePackageTwoDots());

        vm.Rows.Count.ShouldBe(5);
        vm.Rows[0].Kind.ShouldBe(RowKind.Total);
        vm.Rows[1].Kind.ShouldBe(RowKind.Group);
        vm.Rows[1].Name.ShouldBe("Nhóm chi phí 1");
        vm.Rows[2].Kind.ShouldBe(RowKind.Package);
        vm.Rows[2].Name.ShouldBe("Gói thầu A");
        vm.Rows[3].Kind.ShouldBe(RowKind.Entry);
        vm.Rows[4].Kind.ShouldBe(RowKind.Entry);
    }

    [Fact]
    public void Subtotals_are_computed_for_Package_and_Total_rows_on_load()
    {
        var vm = new VitriLedgerViewModel(BuildOneGroupOnePackageTwoDots());

        // sumAdvance=500_000, sumRecovered=200_000 -> unrecovered = 300_000
        var packageRow = vm.Rows[2];
        packageRow.UnrecoveredAdvanceText.ShouldBe(MoneyText.Format(300_000m));

        // disbursed: advance dot -> 500_000 (NSNN); payment dot -> 300_000-200_000=100_000 (NSNN)
        // total disbursed = 600_000
        var totalRow = vm.Rows[0];
        totalRow.TotalDisbursedText.ShouldBe(MoneyText.Format(600_000m));
    }

    [Fact]
    public void AddEntry_then_editing_it_updates_subtotals_live_and_marks_dirty()
    {
        var vm = new VitriLedgerViewModel(BuildOneGroupOnePackageTwoDots());
        vm.IsDirty.ShouldBeFalse();

        var packageRow = vm.Rows[2];
        vm.AddEntryCommand.Execute(packageRow);

        vm.Rows.Count.ShouldBe(6);
        var newEntryRow = vm.Rows[5];
        newEntryRow.Kind.ShouldBe(RowKind.Entry);

        newEntryRow.Fund = Fund.NSNN;
        newEntryRow.AdvanceText = "1.000.000";

        // total disbursed should now include the new advance: 600_000 + 1_000_000
        vm.Rows[0].TotalDisbursedText.ShouldBe(MoneyText.Format(1_600_000m));
        vm.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void AddPackage_without_a_group_is_a_no_op()
    {
        var blank = new ViTri { Name = "Vị trí trống", SortOrder = 1 };
        var vm = new VitriLedgerViewModel(blank);

        vm.Rows.Count.ShouldBe(1); // just the Total row, no group exists

        // No group row exists to pass, so pass a group row that isn't part of this VM's tree.
        var foreignGroupRow = new LedgerRowViewModel(RowKind.Group);
        vm.AddPackageCommand.Execute(foreignGroupRow);

        vm.Rows.Count.ShouldBe(1);
        vm.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void AddEntry_without_a_package_is_a_no_op()
    {
        var blank = new ViTri { Name = "Vị trí trống", SortOrder = 1 };
        var vm = new VitriLedgerViewModel(blank);

        var foreignPackageRow = new LedgerRowViewModel(RowKind.Package);
        vm.AddEntryCommand.Execute(foreignPackageRow);

        vm.Rows.Count.ShouldBe(1);
        vm.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void ToViTri_round_trips_shape_and_values()
    {
        var original = BuildOneGroupOnePackageTwoDots();
        var vm = new VitriLedgerViewModel(original);

        var rebuilt = vm.ToViTri();

        rebuilt.Name.ShouldBe("Vị trí 1");
        rebuilt.NguonVon.ShouldBe("NSNN");
        rebuilt.CostGroups.Count.ShouldBe(1);

        var group = rebuilt.CostGroups[0];
        group.Name.ShouldBe("Nhóm chi phí 1");
        group.Packages.Count.ShouldBe(1);

        var package = group.Packages[0];
        package.Name.ShouldBe("Gói thầu A");
        package.ContractorName.ShouldBe("Cty X");
        package.BankAccount.ShouldBe("12345");
        package.ContractValue.ShouldBe(1_000_000m);
        package.Dots.Count.ShouldBe(2);

        package.Dots[0].Advance.ShouldBe(500_000m);
        package.Dots[0].Fund.ShouldBe(Fund.NSNN);
        package.Dots[1].PaymentKLHT.ShouldBe(300_000m);
        package.Dots[1].RecoveredAdvance.ShouldBe(200_000m);
    }

    [Fact]
    public void AddGroup_then_AddPackage_then_DeleteSelected_removes_the_package_and_its_entries()
    {
        var blank = new ViTri { Name = "Vị trí trống", SortOrder = 1 };
        var vm = new VitriLedgerViewModel(blank);

        vm.AddGroupCommand.Execute(null);
        vm.Rows.Count.ShouldBe(2); // Total, Group

        var groupRow = vm.Rows[1];
        vm.AddPackageCommand.Execute(groupRow);
        vm.Rows.Count.ShouldBe(3); // Total, Group, Package

        var packageRow = vm.Rows[2];
        vm.AddEntryCommand.Execute(packageRow);
        vm.Rows.Count.ShouldBe(4); // Total, Group, Package, Entry
        vm.IsDirty.ShouldBeTrue();

        vm.SelectedRow = packageRow;
        vm.DeleteSelectedCommand.Execute(null);

        vm.Rows.Count.ShouldBe(2); // Total, Group only
    }

    [Fact]
    public void TotalRow_is_the_same_instance_as_Rows_zero()
    {
        var vm = new VitriLedgerViewModel(BuildOneGroupOnePackageTwoDots());

        vm.TotalRow.ShouldBeSameAs(vm.Rows[0]);
    }

    [Fact]
    public void ApplyPaste_writes_into_existing_entries_of_the_same_package()
    {
        var vm = new VitriLedgerViewModel(BuildOneGroupOnePackageTwoDots());
        var firstEntry = vm.Rows[3]; // first Dot under the package
        firstEntry.Kind.ShouldBe(RowKind.Entry);

        // col 0=Date, 1=Content, 5=DocumentNo, 6=Advance, 7=KLHT, 8=Recovered, 9=Fund
        var block = new[]
        {
            new[] { "05/05/2026", "Dán 1", "", "", "", "", "700.000" },
            new[] { "06/05/2026", "Dán 2", "", "", "", "", "800.000" }
        };

        vm.ApplyPaste(firstEntry, 0, block);

        vm.Rows[3].Content.ShouldBe("Dán 1");
        vm.Rows[3].AdvanceText.ShouldBe("700.000");
        vm.Rows[4].Content.ShouldBe("Dán 2");
        vm.Rows[4].AdvanceText.ShouldBe("800.000");
        vm.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void ApplyPaste_appends_new_entries_when_the_block_extends_past_the_last_row()
    {
        var vm = new VitriLedgerViewModel(BuildOneGroupOnePackageTwoDots());
        var lastEntry = vm.Rows[4]; // second (last) Dot under the package

        var block = new[]
        {
            new[] { "01/06/2026" },  // overwrites the last existing entry's date
            new[] { "10/06/2026" }   // extends past the last row -> appends a new Entry
        };

        vm.ApplyPaste(lastEntry, 0, block);

        vm.Rows.Count.ShouldBe(6); // Total, Group, Package, Entry, Entry, new Entry
        vm.Rows[4].DateText.ShouldBe("01/06/2026");
        vm.Rows[5].Kind.ShouldBe(RowKind.Entry);
        vm.Rows[5].DateText.ShouldBe("10/06/2026");
    }

    [Fact]
    public void ApplyPaste_on_a_non_entry_row_is_a_no_op()
    {
        var vm = new VitriLedgerViewModel(BuildOneGroupOnePackageTwoDots());
        var groupRow = vm.Rows[1];

        vm.ApplyPaste(groupRow, 0, new[] { new[] { "x" } });

        vm.Rows.Count.ShouldBe(5);
        vm.IsDirty.ShouldBeFalse();
    }

    // ===== Smart Thêm gói / Thêm đợt resolution (toolbar + inline ＋ buttons) =====

    [Fact]
    public void AddEntry_resolves_the_owning_package_from_a_selected_Entry_row()
    {
        var vm = new VitriLedgerViewModel(BuildOneGroupOnePackageTwoDots());
        var entryRow = vm.Rows[3]; // an existing Entry under the sole package

        vm.AddEntryCommand.Execute(entryRow); // as if bound to SelectedRow via the toolbar

        vm.Rows.Count.ShouldBe(6);
        vm.Rows[5].Kind.ShouldBe(RowKind.Entry);
        vm.SelectedRow.ShouldBe(vm.Rows[5]); // newly-added row is selected
    }

    [Fact]
    public void AddEntry_resolves_the_package_directly_from_a_selected_Package_row()
    {
        var vm = new VitriLedgerViewModel(BuildOneGroupOnePackageTwoDots());
        var packageRow = vm.Rows[2];

        vm.AddEntryCommand.Execute(packageRow); // as if the inline "＋ đợt" on this Package row

        vm.Rows.Count.ShouldBe(6);
        vm.Rows[5].Kind.ShouldBe(RowKind.Entry);
    }

    [Fact]
    public void AddEntry_auto_uses_the_sole_package_when_the_hint_is_not_a_package_or_entry()
    {
        var vm = new VitriLedgerViewModel(BuildOneGroupOnePackageTwoDots());
        var groupRow = vm.Rows[1]; // a Group row is not a package/entry hint

        vm.AddEntryCommand.Execute(groupRow);

        vm.Rows.Count.ShouldBe(6); // fell back to the sole package in the Vị trí
        vm.Rows[5].Kind.ShouldBe(RowKind.Entry);
    }

    [Fact]
    public void AddEntry_with_no_hint_at_all_auto_uses_the_sole_package()
    {
        var vm = new VitriLedgerViewModel(BuildOneGroupOnePackageTwoDots());

        vm.AddEntryCommand.Execute(null);

        vm.Rows.Count.ShouldBe(6);
        vm.Rows[5].Kind.ShouldBe(RowKind.Entry);
    }

    [Fact]
    public void AddEntry_is_a_no_op_when_ambiguous_between_two_packages()
    {
        var viTri = BuildOneGroupOnePackageTwoDots();
        viTri.CostGroups[0].Packages.Add(new Package { Name = "Gói thầu B", SortOrder = 1 });
        var vm = new VitriLedgerViewModel(viTri);
        var before = vm.Rows.Count;

        vm.AddEntryCommand.Execute(null); // no hint + 2 packages -> genuinely ambiguous

        vm.Rows.Count.ShouldBe(before);
        vm.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void AddPackage_resolves_the_owning_group_from_a_selected_Entry_row()
    {
        var vm = new VitriLedgerViewModel(BuildOneGroupOnePackageTwoDots());
        var entryRow = vm.Rows[3];

        vm.AddPackageCommand.Execute(entryRow);

        vm.Rows.Count.ShouldBe(6);
        vm.Rows[5].Kind.ShouldBe(RowKind.Package);
        vm.SelectedRow.ShouldBe(vm.Rows[5]);
    }

    [Fact]
    public void AddPackage_resolves_the_group_directly_from_a_selected_Group_row()
    {
        var vm = new VitriLedgerViewModel(BuildOneGroupOnePackageTwoDots());
        var groupRow = vm.Rows[1];

        vm.AddPackageCommand.Execute(groupRow); // as if the inline "＋ gói" on this Group row

        vm.Rows.Count.ShouldBe(6);
        vm.Rows[5].Kind.ShouldBe(RowKind.Package);
    }

    [Fact]
    public void AddPackage_auto_uses_the_sole_group_when_nothing_useful_is_selected()
    {
        var vm = new VitriLedgerViewModel(BuildOneGroupOnePackageTwoDots());

        vm.AddPackageCommand.Execute(null);

        vm.Rows.Count.ShouldBe(6);
        vm.Rows[5].Kind.ShouldBe(RowKind.Package);
    }

    [Fact]
    public void AddPackage_is_a_no_op_when_ambiguous_between_two_groups()
    {
        var viTri = BuildOneGroupOnePackageTwoDots();
        viTri.CostGroups.Add(new CostGroup { Name = "Nhóm 2", SortOrder = 1 });
        var vm = new VitriLedgerViewModel(viTri);
        var before = vm.Rows.Count;

        vm.AddPackageCommand.Execute(null); // no hint + 2 groups -> genuinely ambiguous

        vm.Rows.Count.ShouldBe(before);
        vm.IsDirty.ShouldBeFalse();
    }

    // ===== Excel-style column resize + wrap toggle =====

    [Fact]
    public void ColumnWidths_defaults_match_the_grid_header_layout()
    {
        var widths = new LedgerColumnWidths();

        widths.NgayWidth.Value.ShouldBe(100);
        widths.NoiDung.Value.ShouldBe(340);
        widths.NhaThauTk.Value.ShouldBe(170);
        widths.GiaTriHd.Value.ShouldBe(130);
        widths.PhuLuc.Value.ShouldBe(110);
        widths.SoCt.Value.ShouldBe(90);
        widths.TamUng.Value.ShouldBe(120);
        widths.Klht.Value.ShouldBe(120);
        widths.ThuHoi.Value.ShouldBe(110);
        widths.Nguon.Value.ShouldBe(110);
        widths.GnNsnn.Value.ShouldBe(120);
        widths.GnNgoaiHoi.Value.ShouldBe(120);
        widths.SoDuTu.Value.ShouldBe(120);
        widths.TongDaGn.Value.ShouldBe(120);
    }

    [Fact]
    public void Resize_adds_the_delta_to_the_column_at_the_given_index()
    {
        var widths = new LedgerColumnWidths();

        widths.Resize(LedgerColumnWidths.NoiDungIndex, 25);

        widths.NoiDung.Value.ShouldBe(365);
        // Nothing else moved.
        widths.NgayWidth.Value.ShouldBe(100);
    }

    [Fact]
    public void Resize_shrinking_clamps_to_the_minimum_column_width()
    {
        var widths = new LedgerColumnWidths();

        widths.Resize(LedgerColumnWidths.SoCtIndex, -1000); // Số CT starts at 90

        widths.SoCt.Value.ShouldBe(LedgerColumnWidths.MinColumnWidth);
    }

    [Fact]
    public void Resize_with_an_out_of_range_index_is_a_no_op()
    {
        var widths = new LedgerColumnWidths();

        widths.Resize(-1, 50);
        widths.Resize(999, 50);

        widths.NgayWidth.Value.ShouldBe(100);
        widths.TongDaGn.Value.ShouldBe(120);
    }

    [Fact]
    public void GetWidth_returns_the_column_at_the_given_index()
    {
        var widths = new LedgerColumnWidths();

        widths.GetWidth(LedgerColumnWidths.Ngay).Value.ShouldBe(100);
        widths.GetWidth(LedgerColumnWidths.TongDaGnIndex).Value.ShouldBe(120);
    }

    [Fact]
    public void Two_ledgers_have_independent_ColumnWidths_instances()
    {
        var vm1 = new VitriLedgerViewModel(new ViTri { Name = "V1" });
        var vm2 = new VitriLedgerViewModel(new ViTri { Name = "V2" });

        vm1.ColumnWidths.Resize(LedgerColumnWidths.NoiDungIndex, 50);

        vm1.ColumnWidths.NoiDung.Value.ShouldBe(390);
        vm2.ColumnWidths.NoiDung.Value.ShouldBe(340); // untouched — separate tab, separate instance
    }

    [Fact]
    public void WrapText_defaults_to_off_and_can_be_toggled()
    {
        var vm = new VitriLedgerViewModel(new ViTri { Name = "V1" });

        vm.WrapText.ShouldBeFalse();

        vm.WrapText = true;

        vm.WrapText.ShouldBeTrue();
    }
}
