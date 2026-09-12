using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// 值等于 ConverterParameter 时显示，否则折叠（用于"仅某种模式下显示"这类条件显隐）。
/// 用转换器而不是给控件设带 DataTrigger 的显式 Style——显式样式会整体顶掉 WPF-UI 的隐式样式（§9 第 18 条）。
/// </summary>
public sealed class EqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var expected = parameter?.ToString();
        var actual = value?.ToString();
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
