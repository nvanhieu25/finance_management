using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// One page (tab) of the finance workbook. The tab strip binds Header; the tab
/// body binds Content, which the app-wide ViewLocator resolves to the existing
/// view of the wrapped view-model — so the workbook reuses the Phase-3/4 views
/// as-is instead of re-implementing them.
public abstract partial class WorkbookPageViewModel : ViewModelBase
{
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private bool _isDirty;

    /// Tab caption; unsaved pages get an Excel-style bullet.
    public string Header => IsDirty ? $"• {Title}" : Title;

    /// The TH page is pinned: always first, never closed or renamed.
    public virtual bool IsPinned => false;

    /// Inner view-model rendered in the tab body (resolved by ViewLocator).
    public abstract object Content { get; }

    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(Header));
    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(Header));
}
