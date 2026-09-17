using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using TheGuideToTheNewEden.WPF.Views.UserControls.Map;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// 主权分组号 → 刷子。直接复用星图画布的分组配色算法，保证设置窗的色块预览与星图上的实际颜色一致。
/// </summary>
public sealed class SovGroupColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var groupId = value switch
        {
            long longValue => longValue,
            int intValue => intValue,
            double doubleValue => (long)doubleValue,
            string text when long.TryParse(text, out var parsed) => parsed,
            _ => 0L,
        };

        var color = StarMapCanvas.SovGroupColor(groupId, 0);
        return new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}
