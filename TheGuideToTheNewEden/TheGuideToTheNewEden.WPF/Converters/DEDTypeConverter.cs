using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// DED 类型值（0 无人机 / 1 天蛇 / 2 血袭者 / 3 萨沙 / 4 天使 / 5 古斯塔斯，见 Core <c>DED.Type</c>）
/// → 本地化名称。对应 WinUI 的 <c>DEDTypeConverter</c>。
/// <para>
/// 用转换器而不是让 VM 存好字符串：转换器在每次求值时查资源，配合 <c>LanguageService</c> 换语言后
/// 统一重算绑定（<c>UpdateTarget()</c>）即可跟着切语言，不会出现"烤进对象"的旧语言文案（§9 第 63 条）。
/// </para>
/// </summary>
public sealed class DEDTypeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            int index => index switch
            {
                0 => "DEDPage_Drone",
                1 => "DEDPage_Serpentis",
                2 => "DEDPage_BloodRaider",
                3 => "DEDPage_Sansha",
                4 => "DEDPage_Angel",
                5 => "DEDPage_Guristas",
                _ => null,
            },
            _ => null,
        };

        return key is null
            ? value?.ToString() ?? string.Empty
            : Application.Current?.TryFindResource(key) as string ?? key;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
