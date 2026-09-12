using System.Globalization;
using System.Windows.Data;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// bool → bool 取反（用于 <c>IsEnabled="{Binding XxxRunning, Converter=...}"</c> 这类场景）。
/// 存在的意义：同一状态只维护一个属性，"未运行"不再需要一个单独的计算属性——
/// 否则 XAML 一旦绑定到不存在/漏通知的属性会<b>静默</b>保持控件默认值（常见表现为按钮常显、区域常可用）。
/// </summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;
}
