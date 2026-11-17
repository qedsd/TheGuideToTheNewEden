using System.Globalization;
using System.Windows;
using System.Windows.Data;
using TheGuideToTheNewEden.Core.Enums;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// 战斗站点分类（<see cref="CombatSiteCategory"/> 的整数值）→ 本地化名称。
/// <para>
/// 与 <see cref="DEDTypeConverter"/> 同一套路：转换器在每次求值时查资源，
/// 换语言后由 <c>LanguageService</c> 统一 <c>UpdateTarget()</c> 重算，不会把旧语言文案烤进对象。
/// </para>
/// </summary>
public sealed class CombatSiteCategoryConverter : IValueConverter
{
    /// <summary>分类值 → 语言键。列表徽标与分类下拉共用同一套键。</summary>
    public static string? KeyOf(int category) => category switch
    {
        (int)CombatSiteCategory.DedRated => "DEDPage_Cat_DedRated",
        (int)CombatSiteCategory.Unrated => "DEDPage_Cat_Unrated",
        (int)CombatSiteCategory.Expedition => "DEDPage_Cat_Expedition",
        (int)CombatSiteCategory.Anomaly => "DEDPage_Cat_Anomaly",
        (int)CombatSiteCategory.ChemicalLab => "DEDPage_Cat_ChemicalLab",
        (int)CombatSiteCategory.RelicSite => "DEDPage_Cat_RelicSite",
        (int)CombatSiteCategory.Triglavian => "DEDPage_Cat_Triglavian",
        (int)CombatSiteCategory.Special => "DEDPage_Cat_Special",
        (int)CombatSiteCategory.CapitalExpedition => "DEDPage_Cat_CapitalExpedition",
        _ => null,
    };

    public static string NameOf(int category)
    {
        var key = KeyOf(category);
        return key is null
            ? category.ToString(CultureInfo.InvariantCulture)
            : Application.Current?.TryFindResource(key) as string ?? key;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int category)
        {
            return NameOf(category);
        }

        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
