using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.Tests.Common;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Domain.Finance;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Finance;

public class FinanceProjectGridViewModelTests
{
    private static (FinanceProjectGridViewModel vm, Mock<IFinanceProjectService> svc) Create()
    {
        var svc = new Mock<IFinanceProjectService>(MockBehavior.Strict);
        var dlg = new FakeDialogService();
        return (new FinanceProjectGridViewModel(svc.Object, dlg), svc);
    }

    [Fact]
    public void AddRow_appends_a_row_and_keeps_totals_live()
    {
        var (vm, _) = Create();
        vm.AddRowCommand.Execute(null);
        vm.Lines.Count.ShouldBe(1);

        vm.Lines[0].Advance = 500m;
        vm.Lines[0].DisbursedNSNN = 500m;

        vm.SumAdvance.ShouldBe(500m);
        vm.TotalDisbursed.ShouldBe(500m);
        vm.UnrecoveredAdvance.ShouldBe(500m);
    }

    [Fact]
    public void DeleteRow_removes_selection_and_updates_totals()
    {
        var (vm, _) = Create();
        vm.AddRowCommand.Execute(null);
        vm.Lines[0].DisbursedNSNN = 300m;
        vm.SelectedRow = vm.Lines[0];

        vm.DeleteRowCommand.Execute(null);

        vm.Lines.ShouldBeEmpty();
        vm.TotalDisbursed.ShouldBe(0m);
    }

    [Fact]
    public async Task Save_new_project_calls_CreateAsync_and_sets_current_id()
    {
        var (vm, svc) = Create();
        svc.Setup(s => s.CreateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        vm.ProjectName = "DA1";
        vm.ContractValue = 1_000m;
        vm.AddRowCommand.Execute(null);
        vm.Lines[0].DisbursedNSNN = 500m;

        await vm.SaveCommand.ExecuteAsync(null);

        svc.Verify(s => s.CreateAsync(It.Is<Project>(p => p.Name == "DA1" && p.Lines.Count == 1), It.IsAny<CancellationToken>()), Times.Once);
        vm.CurrentProjectId.ShouldBe(42);
    }

    [Fact]
    public async Task Save_existing_project_calls_UpdateAsync()
    {
        var (vm, svc) = Create();
        svc.Setup(s => s.GetWithLinesAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Project { Id = 7, Name = "DA7", ContractValue = 2_000m });
        svc.Setup(s => s.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await vm.LoadProjectAsync(7);
        vm.CurrentProjectId.ShouldBe(7);

        await vm.SaveCommand.ExecuteAsync(null);
        svc.Verify(s => s.UpdateAsync(It.Is<Project>(p => p.Id == 7), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Save_new_project_includes_ContractAnnexValue()
    {
        var (vm, svc) = Create();
        svc.Setup(s => s.CreateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>())).ReturnsAsync(5);

        vm.ProjectName = "DA1";
        vm.ContractValue = 1_000m;
        vm.ContractAnnexValue = 1_100m;
        vm.AddRowCommand.Execute(null);

        await vm.SaveCommand.ExecuteAsync(null);

        svc.Verify(s => s.CreateAsync(It.Is<Project>(p => p.ContractAnnexValue == 1_100m), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void InitializeNew_starts_dirty_with_one_blank_row()
    {
        var sut = new FinanceProjectGridViewModel(Mock.Of<IFinanceProjectService>(), Mock.Of<IDialogService>());

        sut.InitializeNew();

        sut.CurrentProjectId.ShouldBe(0);
        sut.Lines.Count.ShouldBe(1);
        sut.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public async Task LoadProjectAsync_leaves_the_grid_clean()
    {
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.GetWithLinesAsync(7, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Project { Id = 7, Name = "DA1" });
        var sut = new FinanceProjectGridViewModel(service.Object, Mock.Of<IDialogService>());

        await sut.LoadProjectAsync(7);

        sut.ProjectName.ShouldBe("DA1");
        sut.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public async Task LoadProjectAsync_with_unknown_id_leaves_dirty_state_untouched()
    {
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.GetWithLinesAsync(999, It.IsAny<CancellationToken>()))
               .ReturnsAsync((Project?)null);
        var sut = new FinanceProjectGridViewModel(service.Object, Mock.Of<IDialogService>());
        sut.InitializeNew();          // dirty = true
        sut.IsDirty.ShouldBeTrue();

        await sut.LoadProjectAsync(999);

        sut.IsDirty.ShouldBeTrue();   // unchanged — nothing was loaded
    }

    [Fact]
    public async Task Editing_a_header_field_marks_the_grid_dirty()
    {
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.GetWithLinesAsync(7, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Project { Id = 7, Name = "DA1" });
        var sut = new FinanceProjectGridViewModel(service.Object, Mock.Of<IDialogService>());
        await sut.LoadProjectAsync(7);

        sut.ContractorName = "Nhà thầu X";

        sut.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public async Task Editing_a_row_marks_the_grid_dirty()
    {
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.GetWithLinesAsync(7, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Project
               {
                   Id = 7,
                   Name = "DA1",
                   Lines = { new ProjectLineItem { SortOrder = 1, Advance = 10m } }
               });
        var sut = new FinanceProjectGridViewModel(service.Object, Mock.Of<IDialogService>());
        await sut.LoadProjectAsync(7);

        sut.Lines[0].PaymentKLHT = 50m;

        sut.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveAsync_clears_dirty_and_returns_true()
    {
        var service = new Mock<IFinanceProjectService>();
        service.Setup(s => s.CreateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(42);
        var sut = new FinanceProjectGridViewModel(service.Object, Mock.Of<IDialogService>());
        sut.InitializeNew();
        sut.ProjectName = "DA moi";

        var ok = await sut.SaveAsync();

        ok.ShouldBeTrue();
        sut.CurrentProjectId.ShouldBe(42);
        sut.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveAsync_refuses_a_blank_name_and_stays_dirty()
    {
        var sut = new FinanceProjectGridViewModel(Mock.Of<IFinanceProjectService>(), Mock.Of<IDialogService>());
        sut.InitializeNew();
        sut.ProjectName = "   ";

        var ok = await sut.SaveAsync();

        ok.ShouldBeFalse();
        sut.IsDirty.ShouldBeTrue();
    }
}
