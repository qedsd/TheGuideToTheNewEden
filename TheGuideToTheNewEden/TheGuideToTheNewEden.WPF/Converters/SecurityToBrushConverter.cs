using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using TheGuideToTheNewEden.WPF.Views.UserControls.Map;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// 星系安全等级 → 前景色。直接复用星图画布的 11 档安全色板
/// （与市场 / WinUI 的 SystemSecurityForeground 色阶一致）。
/// </summary>
public sealed class SecurityToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var security = value switch
        {
            double d => d,
            float f => f,
            int i => i,
            _ => 0d,
        };

        var color = StarMapCanvas.SecurityColor(security);
        var brush = new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue));
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
