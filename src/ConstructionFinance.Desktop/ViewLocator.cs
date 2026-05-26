using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is null) return null;
        var vmTypeName = data.GetType().FullName!;
        // Candidate 1: ViewModels.X.YViewModel → Views.X.YView
        var candidate1 = vmTypeName
            .Replace(".ViewModels.", ".Views.", StringComparison.Ordinal)
            .Replace("ViewModel", "View", StringComparison.Ordinal);
        // Candidate 2: ViewModels.X.YDialogViewModel → Views.X.YDialog
        var candidate2 = vmTypeName
            .Replace(".ViewModels.", ".Views.", StringComparison.Ordinal);
        if (candidate2.EndsWith("ViewModel", StringComparison.Ordinal))
            candidate2 = candidate2[..^"ViewModel".Length];

        var type = Type.GetType(candidate1) ?? Type.GetType(candidate2);
        return type is not null
            ? (Control)Activator.CreateInstance(type)!
            : new TextBlock { Text = $"View not found for {vmTypeName}" };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
