using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ConstructionFinance.Desktop.ViewModels.Finance;

namespace ConstructionFinance.Desktop.Views.Finance;

/// Picks the row DataTemplate by RowKind. The four templates are set from XAML
/// (VitriLedgerView.axaml) so the actual row layouts stay declarative; this class
/// is only the switch. All four sub-templates share x:DataType="vm:LedgerRowViewModel"
/// (every row in the outline grid is that one CLR type — only Kind differs), which
/// is exactly why an x:DataType-based DataTemplates collection can't discriminate
/// and a selector is needed instead.
public sealed class LedgerRowTemplateSelector : IDataTemplate
{
    public IDataTemplate? Total { get; set; }
    public IDataTemplate? Group { get; set; }
    public IDataTemplate? Package { get; set; }
    public IDataTemplate? Entry { get; set; }

    public Control? Build(object? param)
    {
        if (param is not LedgerRowViewModel row) return null;
        return row.Kind switch
        {
            RowKind.Total => Total?.Build(param),
            RowKind.Group => Group?.Build(param),
            RowKind.Package => Package?.Build(param),
            RowKind.Entry => Entry?.Build(param),
            _ => null
        };
    }

    public bool Match(object? data) => data is LedgerRowViewModel;
}
