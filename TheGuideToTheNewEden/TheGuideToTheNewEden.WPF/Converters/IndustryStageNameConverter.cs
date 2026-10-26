using System;
using System.Globalization;
using System.Windows.Data;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// 排产「阶段」代码（Core 的中性标记）→ 当前语言文案。
/// 用于 <c>CollectionViewGroup.Name</c>：分组按代码分，显示按语言，切语言后绑定重算即可更新。
/// 兼容旧的已保存结果里残留的中文阶段名。
/// </summary>
public sealed class IndustryStageNameConverter : IValueConverter
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
            "invention" or "发明" => "IndustrySchedule_StageInvention",
            "reaction" or "反应" => "IndustrySchedule_StageReaction",
            "planetary" or "行星" => "IndustrySchedule_StagePlanetary",
            "target" or "总装" => "IndustrySchedule_StageTarget",
            "component" or "组件" => "IndustrySchedule_StageComponent",
            "inprogress" or "在制" => "IndustrySchedule_StageInProgress",
            _ => null,
        };

        return key is null ? code : LanguageService.Get(key);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
