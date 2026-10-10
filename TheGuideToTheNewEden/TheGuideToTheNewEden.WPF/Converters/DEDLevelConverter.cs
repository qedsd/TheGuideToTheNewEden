using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// DED 等级（1-10 表示 <c>n/10</c>，0 表示未定级，见 Core <c>DED.Level</c>）→ 显示文本。
/// 对应 WinUI 的 <c>DEDLevelConverter</c>（同样保留"未定级"这一档）。
/// </summary>
public sealed class DEDLevelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int level || level <= 0)
        {
            return Application.Current?.TryFindResource("DEDPage_UnkownLevel") as string ?? "DEDPage_UnkownLevel";
        }

        return $"{level}/10";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
