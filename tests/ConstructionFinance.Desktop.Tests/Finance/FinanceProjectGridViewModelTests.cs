using ConstructionFinance.Application.Services.Interfaces;
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
        svc.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project>());
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
        svc.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project> { new() { Id = 42, Name = "DA1" } });

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
        svc.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project> { new() { Id = 7, Name = "DA7" } });

        await vm.LoadProjectAsync(7);
        vm.CurrentProjectId.ShouldBe(7);

        await vm.SaveCommand.ExecuteAsync(null);
        svc.Verify(s => s.UpdateAsync(It.Is<Project>(p => p.Id == 7), It.IsAny<CancellationToken>()), Times.Once);
    }
}
