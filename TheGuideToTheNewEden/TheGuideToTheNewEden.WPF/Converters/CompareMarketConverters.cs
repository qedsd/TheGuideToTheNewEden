using System.Globalization;
using System.Windows;
using System.Windows.Data;
using TheGuideToTheNewEden.WPF.Services.Business;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// 百分比或破折号：<c>null</c> / <c>NaN</c> → "—"（表示"不足两个市场有价，算不出优势"），否则 "N2%"。
/// 绑定值保持数值型，因此列头排序仍按数值排（不要把百分比格式化成字符串存进模型）。
/// </summary>
public sealed class PercentOrDashConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is double d && !double.IsNaN(d) ? d.ToString("N2", culture) + "%" : "—";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>价格或破折号：<c>&lt;= 0</c>（该侧没有报价）→ "—"，否则 "N2"。</summary>
public sealed class PriceOrDashConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is double d && d > 0 ? d.ToString("N2", culture) : "—";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
