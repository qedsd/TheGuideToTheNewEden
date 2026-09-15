using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>null → Collapsed，非 null → Visible（可在参数传 "Inverse" 反转）。</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var notNull = value is not null;
        if (string.Equals(parameter as string, "Inverse", StringComparison.OrdinalIgnoreCase))
        {
            notNull = !notNull;
        }
        return notNull ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
