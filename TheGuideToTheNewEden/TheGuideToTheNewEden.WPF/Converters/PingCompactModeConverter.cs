using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// 判断服务器测速页是否应切到**迷你模式**：窗口（页面）小到一定程度就只显示"开始/停止 + 当前延迟"，
/// 供**长期置顶挂机**当时延监控用。
/// <para>
/// 多值绑定：`[页面 ActualWidth, 页面 ActualHeight]`；<c>ConverterParameter</c> 决定"命中迷你时返回什么"：
/// <c>"compact"</c> = 迷你布局可见（大布局收起），其它/缺省 = 大布局可见（迷你收起）。
/// 直接把结果做成 <see cref="Visibility"/>，省掉再串一个 Bool→Visibility 的转换。
/// </para>
/// <para>
/// 阈值取"任一边太小就切"：高度小于 <see cref="CompactHeightThreshold"/> 或宽度小于
/// <see cref="CompactWidthThreshold"/>。默认尺寸 820×560（页面客户区约 796×488）显然是大布局；
/// 阈值刻意取**比较小**的值——只有窗口小到接近"挂机监控条"时才切迷你，避免窗口还挺大就变极简
/// （用户反馈过"进入迷你模式的窗口尺寸太大"）。想挂机时把窗口拖到约 230 高以下即可变迷你。
/// 两个阈值都按**页面**尺寸算，不含标题栏。
/// </para>
/// </summary>
public sealed class PingCompactModeConverter : IMultiValueConverter
{
    /// <summary>页面高度低于此值 → 迷你模式（约对应窗口高 240）。</summary>
    public const double CompactHeightThreshold = 200;

    /// <summary>页面宽度低于此值 → 迷你模式（约对应窗口宽 440）。</summary>
    public const double CompactWidthThreshold = 420;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // 拿不到尺寸时按"大布局"处理（免得初始化瞬间闪一下迷你布局）
        var width = values.Length > 0 && values[0] is double w && !double.IsNaN(w) ? w : double.PositiveInfinity;
        var height = values.Length > 1 && values[1] is double h && !double.IsNaN(h) ? h : double.PositiveInfinity;

        var compact = height < CompactHeightThreshold || width < CompactWidthThreshold;
        var wantCompact = string.Equals(parameter as string, "compact", StringComparison.OrdinalIgnoreCase);

        return compact == wantCompact ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
