using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// 服务器测速页的**页面底色**：完整布局用主题的不透明背景色，迷你形态用**透明**
/// （迷你形态所在工具窗是逐像素透明的，透明才能真正透出桌面；见 <c>IToolPage.NeedsTransparentWindow</c>）。
/// <para>
/// 多值绑定与 <see cref="PingCompactModeConverter"/> 完全一样（`[页面 ActualWidth, 页面 ActualHeight]`），
/// 阈值也共用它的常量，所以"换布局"和"换底色"永远同步。
/// </para>
/// <para>
/// 为什么必须给完整布局留一个不透明底色：窗口透明之后，卡片之间的缝隙/页面内边距本来是透明的，
/// 完整布局下会直接透出桌面（很难看）；迷你形态才需要透。
/// </para>
/// </summary>
public sealed class PingWindowBackgroundConverter : IMultiValueConverter
{
    /// <summary>完整布局用的底色资源键（WPF-UI 主题里已有的应用背景色）。</summary>
    public const string FullBackgroundResourceKey = "ApplicationBackgroundBrush";

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var width = values.Length > 0 && values[0] is double w && !double.IsNaN(w) ? w : double.PositiveInfinity;
        var height = values.Length > 1 && values[1] is double h && !double.IsNaN(h) ? h : double.PositiveInfinity;

        var compact = height < PingCompactModeConverter.CompactHeightThreshold
                      || width < PingCompactModeConverter.CompactWidthThreshold;

        if (compact)
        {
            return Brushes.Transparent;
        }

        // 每次转换都重新查（不缓存）：换主题后下一次求值就用上新画刷（§9 第 18 条）
        return Application.Current?.TryFindResource(FullBackgroundResourceKey) as Brush ?? Brushes.Black;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
