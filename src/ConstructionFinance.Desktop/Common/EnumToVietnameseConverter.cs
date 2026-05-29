using System.Globalization;
using Avalonia.Data.Converters;

namespace ConstructionFinance.Desktop.Common;

/// XAML converter: maps a domain enum (or known string) to its Vietnamese display name.
/// Use as: <c>Converter={x:Static common:EnumToVietnameseConverter.Instance}</c>
public class EnumToVietnameseConverter : IValueConverter
{
    public static readonly EnumToVietnameseConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => EnumDisplay.Localize(value);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Avalonia.Data.BindingOperations.DoNothing;
}

/// Specialization for the backup type strings persisted in <c>BackupFileInfo.BackupType</c>.
public class BackupTypeToVietnameseConverter : IValueConverter
{
    public static readonly BackupTypeToVietnameseConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => EnumDisplay.BackupType(value as string);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Avalonia.Data.BindingOperations.DoNothing;
}
