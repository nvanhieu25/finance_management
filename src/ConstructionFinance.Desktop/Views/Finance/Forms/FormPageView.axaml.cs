using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ConstructionFinance.Desktop.ViewModels.Finance.Forms;

namespace ConstructionFinance.Desktop.Views.Finance.Forms;

public partial class FormPageView : UserControl
{
    public FormPageView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += (_, _) =>
            this.FindControl<FormSheetControl>("SheetHost")!.Attach(DataContext as FormPageViewModel);
    }
}
