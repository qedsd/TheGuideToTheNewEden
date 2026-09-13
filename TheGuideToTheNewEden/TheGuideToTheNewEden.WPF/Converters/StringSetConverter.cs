using System.Globalization;
using System.Windows.Data;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// 字符串集合 ↔ 逗号分隔文本（分组快捷键的"角色名单"用）。
/// 绑定的集合类型是 <see cref="HashSet{T}"/>，只读展示 + 失焦回写。
/// </summary>
public sealed class StringSetConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is IEnumerable<string> names ? string.Join(",", names) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string text)
        {
            return Binding.DoNothing;
        }

        return text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
