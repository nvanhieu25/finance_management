using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ConstructionFinance.Desktop.ViewModels.Finance;

namespace ConstructionFinance.Desktop.Views.Finance;

public partial class FinanceWorkbookView : UserControl
{
    public FinanceWorkbookView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += (_, _) =>
            this.FindControl<PagesHost>("PageHost")!.Attach(DataContext as FinanceWorkbookViewModel);
    }
}
