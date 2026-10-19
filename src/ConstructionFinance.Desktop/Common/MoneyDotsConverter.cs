using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ConstructionFinance.Desktop.Common;

/// Số tiền hiển thị kiểu Việt ("10.000.000", số 0 vẫn hiện "0") — các cột tiền trong lưới chỉ đọc, không sửa ngược.
public sealed class MoneyDotsConverter : IValueConverter
{
    public static readonly MoneyDotsConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is decimal amount ? (amount == 0m ? "0" : MoneyText.Format(amount)) : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
