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
}
