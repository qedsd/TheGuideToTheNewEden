using System.Windows;
using System.Windows.Media;

namespace TheGuideToTheNewEden.WPF.Controls;

/// <summary>
/// 轻量进度条：直接在 <see cref="OnRender"/> 里画"轨道 + 进度"两条圆角矩形。
/// </summary>
/// <remarks>
/// 用自绘而不是 <see cref="System.Windows.Controls.ProgressBar"/> 的原因：
/// WPF-UI 为 ProgressBar 提供的隐式样式在本项目角色页的组合下会触发进程级栈溢出
/// （实测：卡片模板里带上 ProgressBar 时，切到角色页进程即以 <c>0xC00000FD</c> 退出；
/// 移除后恢复正常）。这里只需要一条几像素的细条，自绘完全绕开模板/样式系统，也更可控。
/// </remarks>
public sealed class RatioBar : FrameworkElement
{
    public static readonly DependencyProperty RatioProperty = DependencyProperty.Register(
        nameof(Ratio),
        typeof(double),
        typeof(RatioBar),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BarBrushProperty = DependencyProperty.Register(
        nameof(BarBrush),
        typeof(Brush),
        typeof(RatioBar),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush),
        typeof(Brush),
        typeof(RatioBar),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>进度值，0~100（超出范围会被裁剪）。</summary>
    public double Ratio
    {
        get => (double)GetValue(RatioProperty);
        set => SetValue(RatioProperty, value);
    }

    /// <summary>已完成部分的颜色；不设置则只画轨道。</summary>
    public Brush? BarBrush
    {
        get => (Brush?)GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    /// <summary>轨道颜色；不设置则只画进度部分。</summary>
    public Brush? TrackBrush
    {
        get => (Brush?)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = double.IsNaN(Height) ? 3 : Height;
        var width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        return new Size(width, height);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var radius = height / 2;

        if (TrackBrush is { } track)
        {
            drawingContext.DrawRoundedRectangle(track, null, new Rect(0, 0, width, height), radius, radius);
        }

        var ratio = Math.Clamp(Ratio, 0, 100);
        if (ratio <= 0 || BarBrush is not { } bar)
        {
            return;
        }

        var valueWidth = width * ratio / 100;
        drawingContext.DrawRoundedRectangle(bar, null, new Rect(0, 0, valueWidth, height), radius, radius);
    }
}
