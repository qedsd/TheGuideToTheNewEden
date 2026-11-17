using System.Globalization;
using System.Windows;
using System.Windows.Data;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Enums;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// 战斗站点 → 列表里那枚短徽标（等级 / 档位 / 类别提示）。
/// <para>
/// 口径：
/// <list type="bullet">
/// <item>DED 评级复合体 → <c>6/10</c></item>
/// <item>无评级复合体 → <c>未定级</c>；但<b>舰队集结点</b>按玩家习惯显示 <c>9/10</c>（游戏里就这么叫）</item>
/// <item>战斗异常 → <c>T8·3</c>（难度档 · 档内等级）</item>
/// <item>远征 → <c>远征</c>；化学实验室 / 遗迹 / 三神裔 / 特殊 → 类别名</item>
/// </list>
/// </para>
/// </summary>
public sealed class CombatSiteTagConverter : IValueConverter
{
    private static string S(string key, string fallback)
        => Application.Current?.TryFindResource(key) as string ?? fallback;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is CombatSite site ? TagOf(site) : string.Empty;

    /// <summary>徽标文案的**唯一口径**（VM 里拼文本也走这里，别在两处各写一份）。</summary>
    public static string TagOf(CombatSite site)
    {
        switch ((CombatSiteCategory)site.Category)
        {
            case CombatSiteCategory.DedRated:
                return $"{site.Rating}/10";

            case CombatSiteCategory.Unrated:
                return site.Rating > 0 ? $"{site.Rating}/10" : S("DEDPage_UnkownLevel", "未定级");

            case CombatSiteCategory.Anomaly:
                return site.Level > 0 ? $"T{site.Tier}·{site.Level}" : $"T{site.Tier}";

            case CombatSiteCategory.CapitalExpedition:
                return S("DEDPage_CapTag", "旗舰");

            default:
                // 远征 / 化学实验室 / 遗迹 / 三神裔 / 特殊：列表里左边那行**已经是分类名**，
                // 再在右边放一个同样的徽标就是重复（实机截图反馈过一次），所以这里留空。
                return string.Empty;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
