using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ConstructionFinance.Desktop.ViewModels.Finance;

/// true → "✓", false → "✕" for the cross-check column.
public class BalancedGlyphConverter : IValueConverter
{
    public static readonly BalancedGlyphConverter Instance = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "✓" : "✕";
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// true → green, false → red, for the cross-check column.
public class BalancedBrushConverter : IValueConverter
{
    public static readonly BalancedBrushConverter Instance = new();
    private static readonly IBrush Ok = new SolidColorBrush(Color.Parse("#2E7D32"));
    private static readonly IBrush Bad = new SolidColorBrush(Color.Parse("#C0392B"));
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Ok : Bad;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
