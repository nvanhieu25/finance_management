using System.ComponentModel;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// One Vị trí tab within the selected project's Vị trí tab strip. Mirrors
/// ProjectPageViewModel's role for the old flat grid: wraps a VitriLedgerViewModel,
/// tracks its dirty state for the tab bullet, and follows its Name for the tab title.
public sealed class ViTriPageViewModel : WorkbookPageViewModel
{
    public ViTriPageViewModel(VitriLedgerViewModel ledger)
    {
        Ledger = ledger;
        Title = DisplayName(ledger.Name);
        IsDirty = ledger.IsDirty;

        Ledger.DirtyChanged += (_, _) => IsDirty = Ledger.IsDirty;
        Ledger.PropertyChanged += OnLedgerPropertyChanged;
    }

    public VitriLedgerViewModel Ledger { get; }

    public override object Content => Ledger;

    private void OnLedgerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VitriLedgerViewModel.Name))
            Title = DisplayName(Ledger.Name);
    }

    private static string DisplayName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "(vị trí mới)" : name.Trim();
}
