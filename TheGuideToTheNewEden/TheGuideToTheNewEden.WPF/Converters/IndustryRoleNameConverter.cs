using System;
using System.Globalization;
using System.Windows.Data;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// 殖民地 / 引脚角色代码 → 当前语言文案。
/// Core 规划结果与实时殖民数据里都是语言无关代码（extractor / factory / storage / facility / unassigned），
/// 显示时统一走这里；兼容旧快照里残留的中文角色名。
/// </summary>
public sealed class IndustryRoleNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var code = value as string;
        if (string.IsNullOrEmpty(code))
        {
            return string.Empty;
        }

        var key = code switch
        {
            "extractor" or "提取" or "提取器" => "IndustryPlanetary_RoleExtractor",
            "factory" or "工厂" => "IndustryPlanetary_RoleFactory",
            "storage" or "仓储" => "IndustryPlanetary_RoleStorage",
            "facility" or "设施" => "IndustryPlanetary_RoleFacility",
            "unassigned" or "未分配" => "IndustryPlanetary_RoleUnassigned",
            _ => null,
        };

        return key is null ? code : LanguageService.Get(key);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
