using System.Collections.ObjectModel;
using ConstructionFinance.Desktop.ViewModels.Finance;
using ConstructionFinance.Desktop.ViewModels.Finance.Forms;

namespace ConstructionFinance.Desktop.Tests.Finance;

internal static class FormTabs
{
    /// Tab biểu mẫu theo mã: "thongtri" | "kschi" | "gdntt".
    public static FormTabPageViewModel FormTab(this ObservableCollection<WorkbookPageViewModel> pages, string id) =>
        pages.OfType<FormTabPageViewModel>().Single(page => page.Inner.Definition.Id == id);
}
