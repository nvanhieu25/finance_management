using System.Globalization;
using Avalonia.Data.Converters;

namespace ConstructionFinance.Desktop.Common;

public class DateOnlyConverter : IValueConverter
{
    public static readonly DateOnlyConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateOnly d) return new DateTimeOffset(d.Year, d.Month, d.Day, 0, 0, 0, TimeSpan.Zero);
        if (value is DateOnly?) return null;
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTimeOffset dto) return DateOnly.FromDateTime(dto.DateTime);
        return DateOnly.FromDateTime(DateTime.Today);
    }
}

public class NullableDateOnlyConverter : IValueConverter
{
    public static readonly NullableDateOnlyConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateOnly d) return new DateTimeOffset(d.Year, d.Month, d.Day, 0, 0, 0, TimeSpan.Zero);
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTimeOffset dto) return (DateOnly?)DateOnly.FromDateTime(dto.DateTime);
        return null;
    }
}
