using CommunityToolkit.Mvvm.ComponentModel;
using ConstructionFinance.Application.Printing;
using ConstructionFinance.Desktop.Common;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// One page (tab) of the finance workbook. The tab strip binds Header; the tab
/// body binds Content, which the app-wide ViewLocator resolves to the existing
/// view of the wrapped view-model — so the workbook reuses the Phase-3/4 views
/// as-is instead of re-implementing them.
public abstract partial class WorkbookPageViewModel : ViewModelBase, IPrintablePage
{
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private bool _isDirty;

    /// Tab caption; unsaved pages get an Excel-style bullet.
    public string Header => IsDirty ? $"• {Title}" : Title;

    /// Inner view-model rendered in the tab body (resolved by ViewLocator).
    public abstract object Content { get; }

    /// Mặc định: trang không in được. Trang nào in được thì override cả hai.
    public virtual string? PrintSheetName => null;
    public virtual PrintSheet? BuildPrintSheet() => null;

    /// Trang có lúc in được, có lúc không (vd. biểu mẫu GĐNTT cần chọn gói thầu) thì override để nói vì sao lúc này chưa in được.
    /// Mặc định null: không có gì để nói.
    public virtual string? PrintUnavailableReason => null;

    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(Header));
    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(Header));
}
