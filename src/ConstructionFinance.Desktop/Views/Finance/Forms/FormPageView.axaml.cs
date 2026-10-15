using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using ConstructionFinance.Desktop.ViewModels.Finance.Forms;

namespace ConstructionFinance.Desktop.Views.Finance.Forms;

public partial class FormPageView : UserControl
{
    public const double MinZoom = 0.5, MaxZoom = 3.0, ZoomStep = 0.1;

    private double _zoom = 1.0;

    public FormPageView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += (_, _) =>
            this.FindControl<FormSheetControl>("SheetHost")!.Attach(DataContext as FormPageViewModel);

        this.FindControl<Button>("ZoomOutButton")!.Click += (_, _) => Zoom = _zoom - ZoomStep;
        this.FindControl<Button>("ZoomInButton")!.Click += (_, _) => Zoom = _zoom + ZoomStep;
        this.FindControl<Button>("ZoomResetButton")!.Click += (_, _) => Zoom = 1.0;
        // Ctrl + lăn chuột như Excel. Chặn ở giai đoạn Tunnel để ScrollViewer không cuộn theo.
        this.FindControl<ScrollViewer>("SheetScroll")!.AddHandler(
            PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
    }

    /// Cỡ phóng của tờ (1,0 = đúng cỡ Mẫu). Kẹp trong [MinZoom, MaxZoom].
    public double Zoom
    {
        get => _zoom;
        set
        {
            _zoom = Math.Round(Math.Clamp(value, MinZoom, MaxZoom), 2);
            this.FindControl<LayoutTransformControl>("ZoomHost")!.LayoutTransform = new ScaleTransform(_zoom, _zoom);
            this.FindControl<Button>("ZoomResetButton")!.Content = $"{_zoom:P0}";
        }
    }

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        Zoom = _zoom + (e.Delta.Y > 0 ? ZoomStep : -ZoomStep);
        e.Handled = true;
    }
}
