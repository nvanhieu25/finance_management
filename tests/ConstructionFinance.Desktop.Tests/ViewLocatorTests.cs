using Avalonia.Controls;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.ViewModels.TongHop;
using Moq;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests;

public class ViewLocatorTests
{
    [Fact]
    public void ViewLocator_resolves_ImportWizardViewModel_to_ImportWizardView()
    {
        var locator = new ViewLocator();
        var vm = new ImportWizardViewModel(
            new Mock<IExcelImportService>().Object,
            new Mock<IProjectSummaryService>().Object,
            new Mock<IDialogService>().Object);

        var view = locator.Build(vm);

        view.ShouldNotBeNull();
        view.ShouldNotBeOfType<TextBlock>(
            "ViewLocator returned 'View not found' TextBlock — check ImportWizardView.axaml exists and x:Class matches");
        view.GetType().Name.ShouldBe("ImportWizardView");
    }
}
