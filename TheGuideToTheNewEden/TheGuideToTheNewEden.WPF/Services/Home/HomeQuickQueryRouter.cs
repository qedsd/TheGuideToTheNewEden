using System.Windows;
using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WPF.Services.Assistant;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Services.Home;

/// <summary>
/// 主页快捷输入的**快速命令路由**：本地确定性判定，**不调 LLM**。
/// <list type="bullet">
///   <item>简单意图（物品价格 / KB / 星系定位 / 角色）：本地解析成功后**直接跳转**对应页面（零成本、即时）；</item>
///   <item>其余（复杂问题、名称歧义、查不到）：一律 <see cref="Navigation.NavigateToAssistant"/> 转 AI 助手页并自动发送。</item>
/// </list>
/// <para>
/// 判定原则：先剥意图关键词（"价格/市场/KB/定位…"）再剥语气词（"查/帮我/看看…"），剩余文本才当实体名去解析；
/// **实体多命中或未命中一律不跳转**（绝不"张冠李戴"跳错页），交给助手处理模糊输入。
/// </para>
/// </summary>
public static class HomeQuickQueryRouter
{
    public enum RouteKind
    {
        /// <summary>空输入，不做任何事。</summary>
        None,

        /// <summary>已直接跳转到对应页面。</summary>
        Jumped,

        /// <summary>已转 AI 助手页。</summary>
        ToAssistant,
    }

    public sealed record RouteResult(RouteKind Kind, string? Message);

    private static readonly string[] MarketKeywords =
        ["价格", "多少钱", "值多少", "市场", "卖单", "买单", "卖价", "买价", "行情", "price", "market", "cost"];

    private static readonly string[] KbKeywords = ["kb", "击杀", "战绩", "zkb", "kill"];

    private static readonly string[] SystemKeywords = ["定位", "在哪", "星系", "system", "locate", "where"];

    private static readonly string[] Fillers =
        ["查一下", "查询", "查", "看看", "看一下", "帮我", "给我", "一下", "请", "搜一下", "搜", "找", "的", "怎么样", "多少",
         "？", "?", "，", ",", "。", "！", "!", "：", ":"];

    /// <summary>路由一条查询（KB 名称解析可能走一次 ESI，其余全本地）。</summary>
    public static async Task<RouteResult> RouteAsync(string? query)
    {
        var text = (query ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return new RouteResult(RouteKind.None, null);
        }

        // ① 物品价格 → 市场页
        if (TryStripKeyword(text, MarketKeywords, out var itemText) && itemText.Length > 0)
        {
            var item = ToolNameResolver.ResolveItem(itemText);
            if (item is not null)
            {
                Navigation.NavigateToMarket(item.TypeID);
                return new RouteResult(RouteKind.Jumped, Format("Home.Quick.JumpedMarket", item.TypeName));
            }

            return ToAssistant(text);
        }

        // ② KB / 击杀 → 实体统计
        if (TryStripKeyword(text, KbKeywords, out var entityText) && entityText.Length > 0)
        {
            var entities = await IDNameService.GetByNames([entityText]);
            var entity = entities?.FirstOrDefault(e => string.Equals(e.Name, entityText, StringComparison.OrdinalIgnoreCase))
                         ?? entities?.FirstOrDefault();
            if (entity is not null)
            {
                KbNavigation.OpenEntity(entity);
                return new RouteResult(RouteKind.Jumped, Format("Home.Quick.JumpedKb", entity.Name));
            }

            return ToAssistant(text);
        }

        // ③ 星系定位 → 星图
        if (TryStripKeyword(text, SystemKeywords, out var systemText) && systemText.Length > 0)
        {
            var system = MapSolarSystemService.Query(systemText);
            if (system is not null)
            {
                Navigation.NavigateToSystem(system.SolarSystemID);
                return new RouteResult(RouteKind.Jumped, Format("Home.Quick.JumpedSystem", system.SolarSystemName));
            }

            return ToAssistant(text);
        }

        // ④ 角色全名 → 角色工作区
        var character = CharacterStore.Characters.FirstOrDefault(c =>
            string.Equals(c.CharacterName, text, StringComparison.OrdinalIgnoreCase));
        if (character is not null)
        {
            Navigation.NavigateToCharacter(character.CharacterID);
            return new RouteResult(RouteKind.Jumped, Format("Home.Quick.JumpedCharacter", character.CharacterName));
        }

        // ⑤ 兜底：复杂问题转助手
        return ToAssistant(text);
    }

    private static RouteResult ToAssistant(string text)
    {
        Navigation.NavigateToAssistant(text);
        return new RouteResult(RouteKind.ToAssistant, Resolve("Home.Quick.ToAssistant"));
    }

    /// <summary>剥掉第一个命中的意图关键词，并清掉语气词；返回剩余文本（可能是空串）。</summary>
    private static bool TryStripKeyword(string text, string[] keywords, out string remainder)
    {
        foreach (var keyword in keywords)
        {
            var index = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                continue;
            }

            remainder = StripFillers(text.Remove(index, keyword.Length));
            return true;
        }

        remainder = string.Empty;
        return false;
    }

    private static string StripFillers(string text)
    {
        var result = text;
        foreach (var filler in Fillers)
        {
            result = result.Replace(filler, string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return result.Trim().Trim('"', '\'', '|', '（', '）', '(', ')').Trim();
    }

    private static string Format(string key, string value) => string.Format(Resolve(key), value);

    private static string Resolve(string key) => Application.Current?.TryFindResource(key) as string ?? key;
}
