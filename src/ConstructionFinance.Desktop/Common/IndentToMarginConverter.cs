using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace ConstructionFinance.Desktop.Common;

/// FormLineRow.Indent (0 = dự án, 1 = vị trí, 2 = đợt) → a left Margin, so the biểu mẫu
/// preview table shows the same three-level structure the Excel export renders via
/// cell Alignment.Indent.
public sealed class IndentToMarginConverter : IValueConverter
{
    public static readonly IndentToMarginConverter Instance = new();

    private const double StepWidth = 16;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int level ? new Thickness(level * StepWidth, 0, 0, 0) : new Thickness(0);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
