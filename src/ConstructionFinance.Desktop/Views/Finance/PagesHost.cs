using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Controls;
using ConstructionFinance.Desktop.ViewModels.Finance;

namespace ConstructionFinance.Desktop.Views.Finance;

/// Vùng nội dung của dải tab sổ Vị trí: dựng view của mỗi tab MỘT lần (lúc tab được chọn lần đầu) rồi chỉ bật/tắt
/// `IsVisible` khi chuyển tab.
///
/// TabControl mặc định bỏ view của tab vừa rời và dựng lại view của tab vừa chọn. Với sổ Vị trí nhiều dòng (mỗi dòng
/// cả chục ô nhập) mỗi lần chuyển tab tốn hàng giây (đo: ~3–5 giây cho 300 đợt, cả khi quay lại), dù KHÔNG đọc lại DB.
/// Giữ view trong cây hình ảnh và chỉ ẩn thì lúc hiện lại kết quả đo bố cục vẫn còn nguyên. TabControl vẫn dùng làm dải
/// tiêu đề; nội dung của nó để trống.
public sealed class PagesHost : Panel
{
    private readonly Dictionary<WorkbookPageViewModel, Control> _views = new();
    private FinanceWorkbookViewModel? _workbook;

    public void Attach(FinanceWorkbookViewModel? workbook)
    {
        if (_workbook is not null)
        {
            _workbook.PropertyChanged -= OnWorkbookPropertyChanged;
            _workbook.ViTriPages.CollectionChanged -= OnPagesChanged;
        }
        _workbook = workbook;
        if (workbook is not null)
        {
            workbook.PropertyChanged += OnWorkbookPropertyChanged;
            workbook.ViTriPages.CollectionChanged += OnPagesChanged;
        }
        Sync();
    }

    private void OnWorkbookPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FinanceWorkbookViewModel.SelectedViTriPage)) Sync();
    }

    private void OnPagesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Sync();

    private void Sync()
    {
        var pages = _workbook?.ViTriPages;
        var selected = _workbook?.SelectedViTriPage;

        // Trang đã bị xoá (đổi dự án, xoá Vị trí) thì bỏ view của nó.
        foreach (var gone in _views.Keys.Where(page => pages is null || !pages.Contains(page)).ToList())
        {
            Children.Remove(_views[gone]);
            _views.Remove(gone);
        }

        if (selected is not null && pages is not null && pages.Contains(selected) && !_views.ContainsKey(selected))
        {
            var view = new ContentControl { Content = selected.Content };
            _views[selected] = view;
            Children.Add(view);
        }

        foreach (var (page, view) in _views) view.IsVisible = ReferenceEquals(page, selected);
    }
}
