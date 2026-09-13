using System.Globalization;
using System.Windows.Data;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// 把"承载页面的 ScrollViewer 视口高度"换算成内容可用的 MaxHeight。
/// <para>
/// 用途：页面若被外层 ScrollViewer 以<b>无限高度</b>测量，页面内那些"撑满高度的卡片"会把内容
/// 全部撑开，于是卡片内部的 ScrollViewer 永远拿不到受限高度、不滚动，滚轮而是滚动整个页面。
/// 给页面根元素绑上 <c>MaxHeight = 视口高度 - 内边距</c> 后，页面不再被撑高，
/// 滚动就回到卡片内容自己身上。
/// </para>
/// 视口尚未测量（0 或 NaN）时返回 <see cref="double.PositiveInfinity"/>，即不限制，避免首帧把页面压成 0。
/// </summary>
public sealed class ViewportMaxHeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double viewport || double.IsNaN(viewport) || viewport <= 0)
        {
            return double.PositiveInfinity;
        }

        var inset = 0.0;
        if (parameter is string text
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            inset = parsed;
        }

        var available = viewport - inset;
        return available > 0 ? available : double.PositiveInfinity;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
