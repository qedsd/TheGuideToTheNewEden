using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using TheGuideToTheNewEden.WPF.Models.Tools;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// <see cref="PingQuality"/> → 主题语义色画刷：极佳 / 良好 = 成功色，正常 = 警示色，较差 / 糟糕 = 危险色，
/// 未知 = 次要文字色。
/// <para>
/// 每次转换都重新查资源（不缓存 Brush 实例），所以换主题后在下一次求值时就会用上新画刷；
/// VM 会在 <c>ThemeService.ThemeChanged</c> / <c>LanguageService.LanguageChanged</c> 时重发通知，
/// 于是"停在那儿不动"的结果也颜色正确（§9 第 18 条）。
/// </para>
/// </summary>
public sealed class PingQualityToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            PingQuality.Excellent or PingQuality.Good => "SystemFillColorSuccessBrush",
            PingQuality.Normal => "SystemFillColorCautionBrush",
            PingQuality.Poor or PingQuality.Bad => "SystemFillColorCriticalBrush",
            _ => "TextFillColorSecondaryBrush",
        };

        return Application.Current?.TryFindResource(key) as Brush ?? Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
