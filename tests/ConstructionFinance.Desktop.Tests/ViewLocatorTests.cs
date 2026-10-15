using Avalonia.Controls;
using ConstructionFinance.Desktop;
using ConstructionFinance.Desktop.Tests.Finance;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Domain.Finance;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests;

public class ViewLocatorTests
{
    [Fact]
    public void ViewLocator_resolves_VitriLedgerViewModel_to_VitriLedgerView()
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
            var vm = new VitriLedgerViewModel(new ViTri { Name = "Vị trí test" });

            var view = locator.Build(vm);

            view.ShouldNotBeNull();
            view.ShouldNotBeOfType<TextBlock>(
                "ViewLocator returned 'View not found' TextBlock — check VitriLedgerView.axaml exists and x:Class matches");
            view.GetType().Name.ShouldBe("VitriLedgerView");
        });
    }
}
