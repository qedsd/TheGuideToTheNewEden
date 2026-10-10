using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// 任务等级（1-5，见 Core <c>Mission.Level</c>）→ 本地化文本（<c>Mission_Level_1..5</c>）。
/// 对应 WinUI 的 <c>MissionLevelConverter</c>。
/// </summary>
public sealed class MissionLevelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int level || level <= 0)
        {
            return string.Empty;
        }

        var key = $"Mission_Level_{level}";
        return Application.Current?.TryFindResource(key) as string ?? key;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
