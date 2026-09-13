using System.Globalization;
using System.Windows.Data;
using TheGuideToTheNewEden.WPF.Services.GamePreview;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// <see cref="System.Drawing.Color"/> 与十六进制文本互转（非法输入回退到原值）。
/// 不透明色显示/接受 <c>#RRGGBB</c>，带透明度时显示/接受 <c>#AARRGGBB</c>。
/// </summary>
public sealed class ColorHexConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is System.Drawing.Color color ? PreviewColorHelper.ToHex(color) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var fallback = System.Drawing.Color.Green;
        if (value is string text && !string.IsNullOrWhiteSpace(text))
        {
            return PreviewColorHelper.FromHex(text, fallback);
        }

        return Binding.DoNothing;
    }
}
