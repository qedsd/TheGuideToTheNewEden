using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using TheGuideToTheNewEden.WPF.Services.GamePreview;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary><see cref="System.Drawing.Color"/> → 画刷（颜色预览色块用）。</summary>
public sealed class ColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is System.Drawing.Color color ? PreviewColorHelper.ToBrush(color) : Brushes.Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
