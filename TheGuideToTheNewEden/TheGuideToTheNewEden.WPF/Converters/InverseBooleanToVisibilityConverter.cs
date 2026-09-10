using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>bool → Visibility 取反（true 折叠、false 显示）。</summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Visibility.Collapsed;
    }
}