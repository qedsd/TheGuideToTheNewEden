using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// 站点中文名的**显示版**：去掉尾巴上的难度标注——`（难度1）`（SDE 官方名自带）
/// 与 `（T8·3）`（构建期为同名消重加的后缀）。
/// <para>
/// 为什么只在这一层去掉、不改库里的名字：库里的 <c>TitleCN</c> 是**游戏官方名**（与客户端一致，
/// `隐秘的天使藏身处（难度1）`），AI 工具、搜索、导出都应该拿原名；
/// 而列表/表格里的难度已经有独立的徽标（`T1·1`）和"难度"列，名字再带一遍纯属重复。
/// </para>
/// <para>
/// 评级后缀（`（6/10）`、`（无评级）`）**不动**：那是评级不是难度，且同名消重里也用到它。
/// </para>
/// </summary>
public sealed class CombatSiteNameConverter : IValueConverter
{
    private static readonly Regex DifficultySuffix = new(
        @"\s*[（(]\s*(?:难度\s*\d+|T\d+(?:·\d+)?)\s*[）)]\s*$",
        RegexOptions.Compiled);

    /// <summary>去掉尾部的「（难度N）」（中英文括号都认）。</summary>
    public static string? Strip(string? name)
        => string.IsNullOrEmpty(name) ? name : DifficultySuffix.Replace(name, string.Empty);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Strip(value as string) ?? string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
