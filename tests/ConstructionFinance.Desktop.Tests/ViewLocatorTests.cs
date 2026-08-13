using Avalonia.Controls;
using ConstructionFinance.Application.Services.Interfaces;
using ConstructionFinance.Desktop;
using ConstructionFinance.Desktop.Services;
using ConstructionFinance.Desktop.Tests.Finance;
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
        // Routed through the shared headless session (see Finance/HeadlessTestSupport.cs):
        // this test builds a REAL Avalonia View (Activator.CreateInstance inside
        // ViewLocator.Build), which requires the calling thread to be whichever thread
        // Avalonia's dispatcher considers "the" UI thread. Once ANY test in this assembly
        // starts a headless Avalonia platform (VitriLedgerViewTests does), that thread
        // affinity becomes strict process-wide — constructing a View on a plain xunit
        // worker thread now throws "Call from invalid thread". Running on the same headless
        // dispatcher thread keeps this test independent of xunit's parallel scheduling
        // between test classes.
        HeadlessTestSession.Run(() =>
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
        });
    }
}
