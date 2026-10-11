using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using TheGuideToTheNewEden.WPF.Models.Tools;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// <see cref="SpanState"/> → 主题语义色：空闲 = 正文色，计时中 = 成功色，达标 = 危险色
/// （对应 WinUI 版那三个 <c>DefaultForegroundBrush</c>/<c>DefaultGreen</c>/<c>DefaultRed</c> 画刷）。
/// 每次转换重新查资源，不缓存实例（§9 第 18 条）。
/// </summary>
public sealed class SpanStateToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            SpanState.Counting => "SystemFillColorSuccessBrush",
            SpanState.Reached => "SystemFillColorCriticalBrush",
            _ => "TextFillColorPrimaryBrush",
        };

        return Application.Current?.TryFindResource(key) as Brush ?? Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
