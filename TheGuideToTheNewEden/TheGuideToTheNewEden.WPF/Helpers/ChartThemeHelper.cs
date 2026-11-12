using System.Windows;
using System.Windows.Media;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace TheGuideToTheNewEden.WPF.Helpers;

/// <summary>
/// LiveCharts 图表主题联动（阶段 303）：从 WPF 主题资源取色生成 SkiaSharp 画刷。
/// 用法：主题切换钩子里对每张图 <c>chart.LegendTextPaint = ChartThemeHelper.Make("TextFillColorSecondaryBrush");</c>
/// （坐标轴 LabelsPaint / SeparatorsPaint 亦可用同一方法）。资源缺失返回 null（保留 LiveCharts 默认）。
/// </summary>
public static class ChartThemeHelper
{
    public static SolidColorPaint? Make(string resourceKey, double thickness = 1)
    {
        if (Application.Current?.TryFindResource(resourceKey) is not SolidColorBrush brush)
        {
            return null;
        }

        var c = brush.Color;
        return new SolidColorPaint(new SKColor(c.R, c.G, c.B, c.A), (float)thickness);
    }
}
