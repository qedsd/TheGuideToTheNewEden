using System.Globalization;
using System.Windows.Data;
using TheGuideToTheNewEden.WPF.Helpers;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>ISK 数值 → 缩写文本（K/M/B/T），用于击杀列表"估价"列。</summary>
public sealed class IskConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var number = value switch
        {
            double d => d,
            long l => l,
            int i => i,
            float f => f,
            _ => 0d,
        };

        return IskFormatHelper.Format(number);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
