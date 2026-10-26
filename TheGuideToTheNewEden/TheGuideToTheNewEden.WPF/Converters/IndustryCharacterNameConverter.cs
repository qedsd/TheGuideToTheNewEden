using System;
using System.Globalization;
using System.Windows.Data;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// 规划器/排产器里的"角色名"可能是代码：<c>auto:N</c>（自动生成的角色）、<c>auto:default</c>、
/// <c>unassigned</c>（行星位不足）。显示时按当前语言生成文案；真实角色名原样返回。
/// </summary>
public sealed class IndustryCharacterNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var name = value as string;
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        if (name == "auto:default")
        {
            return LanguageService.Get("IndustrySchedule_DefaultCharacter");
        }

        if (name == "unassigned")
        {
            return LanguageService.Get("IndustryPlanetary_RoleUnassigned");
        }

        if (name.StartsWith("auto:", StringComparison.Ordinal)
            && int.TryParse(name.AsSpan(5), out var index))
        {
            return LanguageService.Format("IndustryPlanetary_AutoCharacter", index);
        }

        return name;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
