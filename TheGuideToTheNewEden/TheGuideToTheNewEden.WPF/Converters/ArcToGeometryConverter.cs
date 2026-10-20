using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// 把一个 0..1 的占比（<c>double</c>）转换成一段**从正上方起、顺时针扫过 fraction*360°**的圆弧几何，
/// 供 <c>Path.Data</c> 画"环形用量盘"（阶段 149，像 Cursor/Continue 那种圆圈）用。
/// <para>
/// 纯函数、无依赖，探针可直接断言：fraction ≤ 0 → null（不画）；≥ 1 → 整圆；介于之间 → 圆弧。
/// 几何画在一个 26×26 的坐标空间里（中心 13、半径 11），使用处的 <c>Path</c> 也应是 26×26。
/// </para>
/// </summary>
public sealed class ArcToGeometryConverter : IValueConverter
{
    private const double Size = 26;
    private const double Center = Size / 2;
    private const double Radius = Center - 2;

    /// <summary>按占比生成圆弧（顶部起点、顺时针）。</summary>
    public static Geometry? ToGeometry(double fraction)
    {
        var clamped = Math.Clamp(fraction, 0, 1);
        if (clamped <= 0)
        {
            return null;
        }

        // 整圆：ArcSegment 扫满 360° 会退化成一条线，用整圆几何代替
        if (clamped >= 1)
        {
            var full = new EllipseGeometry(new Point(Center, Center), Radius, Radius);
            full.Freeze();
            return full;
        }

        var start = PointAt(0);                       // 正上方
        var end = PointAt(clamped * 2 * Math.PI);     // 顺时针扫到 fraction
        var segment = new ArcSegment(
            end,
            new Size(Radius, Radius),
            0,
            clamped > 0.5,                           // 超过半圈才是大弧
            SweepDirection.Clockwise,
            true);                                   // IsStroked

        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments.Add(segment);

        var path = new PathGeometry();
        path.Figures.Add(figure);
        path.Freeze();
        return path;
    }

    /// <summary>圆上一点：θ=0 在顶部，θ 增大顺时针（屏幕坐标 y 向下）。</summary>
    private static Point PointAt(double theta)
        => new(Center + Radius * Math.Sin(theta), Center - Radius * Math.Cos(theta));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is double fraction ? ToGeometry(fraction) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}