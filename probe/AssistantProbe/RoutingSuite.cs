using TheGuideToTheNewEden.WPF.Services.Assistant;

namespace TheGuideToTheNewEden.Probe;

/// <summary>
/// 阶段 123：**按需声明**（工具套餐路由）的离线断言矩阵。
/// <para>
/// 路由是纯函数，所以这里全部是确定性断言：核心常驻、套餐命中、两组上限、看不出意图回退全量、
/// 会话/轮内粘性、用户关闭的工具永不声明、英文意图、顺序稳定（缓存契约），以及"到底省了多少字符"。
/// **不碰 settings.json、不发请求。**
/// </para>
/// </summary>
internal static class RoutingSuite
{
    /// <summary>典型"只命中一个套餐"的场景至少要省到这个比例（实测约 45–50%）。</summary>
    private const double SingleBundleMaxRatio = 0.55;

    /// <summary>命中两个套餐时仍应省下的比例（实测约 25–30%）。</summary>
    private const double DoubleBundleMaxRatio = 0.80;

    public static void Run(Check c)
    {
        c.Section("RoutingSuite · 按需声明（套餐路由，本地确定性）");

        var disabled = new HashSet<string>(StringComparer.Ordinal);
        var allNames = ToolRegistry.Product.Select(t => t.Name).ToList();
        var allSchemas = ToolRegistry.FilterSchemas(ToolRegistry.Product, disabled);
        var allChars = ToolRegistry.DeclarationChars(allSchemas);

        // ---------- ① 确定性 + 名字合法 + 顺序（缓存契约） ----------
        var first = ToolRouter.Resolve("巨神兵级在吉他多少钱", null, disabled);
        var second = ToolRouter.Resolve("巨神兵级在吉他多少钱", null, disabled);
        c.That(first.ToolNames.SequenceEqual(second.ToolNames), "同输入两次路由完全一致（前缀稳定 = 缓存可复用）");
        c.That(first.ToolNames.SequenceEqual(first.ToolNames.OrderBy(n => n, StringComparer.Ordinal)),
            "声明的工具名按字典序（与 ToolRegistry 一致的缓存契约）");
        c.That(first.ToolNames.All(allNames.Contains), "只会声明注册表里真实存在的产品工具");
        c.That(first.ToolNames.Distinct(StringComparer.Ordinal).Count() == first.ToolNames.Count, "没有重复名字");

        // ---------- ② 核心常驻 ----------
        c.That(ToolRegistry.CoreToolNames.All(first.ToolNames.Contains),
            $"任何路由都带上核心常驻工具（{string.Join("、", ToolRegistry.CoreToolNames)}）");
        c.That(first.Bundles.Contains("Business"), "「多少钱」命中商业套餐");
        c.That(!first.FallbackToAll, "有明确意图时不回退全量");

        // ---------- ③ 套餐上限与多意图 ----------
        var multi = ToolRouter.Resolve("我的订单被压单了吗", null, disabled);
        c.That(multi.Bundles.Count is >= 1 and <= ToolRouter.MaxBundles,
            $"套餐数在 1–{ToolRouter.MaxBundles} 之间（实际 {string.Join("+", multi.Bundles)}）");
        c.That(multi.Bundles.Contains("Business") && multi.Bundles.Contains("Character"),
            "「我的订单」同时命中角色与商业两组");

        // ---------- ④ 看不出意图 → 全量（只省有把握的地方） ----------
        var fallback = ToolRouter.Resolve("你能做什么", null, disabled);
        c.That(fallback.FallbackToAll, "没有意图词时回退全量");
        c.That(fallback.ToolNames.Count == allNames.Count, "回退时声明全部产品工具");

        // ---------- ⑤ 粘性（会话 + 轮内） ----------
        var sticky = new HashSet<string>(StringComparer.Ordinal) { "zkb_entity", "mission_info" };
        var withSticky = ToolRouter.Resolve("巨神兵级多少钱", sticky, disabled);
        c.That(withSticky.ToolNames.Contains("zkb_entity") && withSticky.ToolNames.Contains("mission_info"),
            "本对话用过的工具始终保留声明（哪怕与本次意图无关）");
        c.That(ToolRouter.Resolve("你能做什么", sticky, disabled).ToolNames.Count == allNames.Count,
            "回退全量时粘性不会让结果超出全量");

        // ---------- ⑥ 用户关闭的工具永不声明 ----------
        var off = new HashSet<string>(StringComparer.Ordinal) { "market_orders", "esi_write" };
        var routedOff = ToolRouter.Resolve("巨神兵级多少钱", null, off);
        c.That(!routedOff.ToolNames.Contains("market_orders"), "被用户关闭的工具不会被套餐带进来");
        c.That(!routedOff.ToolNames.Contains("esi_write"), "被用户关闭的核心工具也不会被声明（开关优先）");
        c.That(ToolRegistry.SchemasFor(routedOff.ToolNames, off).All(s => s.Name != "market_orders"),
            "SchemasFor 同样尊重开关");

        // ---------- ⑦ 英文意图 ----------
        c.That(ToolRouter.Resolve("how much is a Rifter", null, disabled).Bundles.Contains("Business"),
            "英文「how much」命中商业");
        c.That(ToolRouter.Resolve("where is Jita", null, disabled).Bundles.Contains("Map"),
            "英文「where」命中星图");
        c.That(ToolRouter.Resolve("my wallet balance", null, disabled).Bundles.Contains("Character"),
            "英文「my wallet」命中角色");

        // ---------- ⑧ 到底省了多少（字符口径，与 tokens 套件同一算法） ----------
        var singleChars = ToolRegistry.DeclarationChars(ToolRouter.Schemas(first, disabled));
        var doubleChars = ToolRegistry.DeclarationChars(ToolRouter.Schemas(multi, disabled));
        Console.WriteLine($"    [数据] 全量 {allChars} 字符 / 单套餐（{string.Join("+", first.Bundles)}）{singleChars} 字符"
                          + $" / 双套餐（{string.Join("+", multi.Bundles)}）{doubleChars} 字符");
        Console.WriteLine($"    [数据] 核心常驻 {ToolRegistry.DeclarationChars(ToolRegistry.SchemasFor(ToolRegistry.CoreToolNames, disabled))} 字符"
                          + $"，各套餐：" + string.Join("、", ToolRegistry.Categories
                              .Where(category => category != "Esi")
                              .Select(category => $"{category} {ToolRegistry.DeclarationChars(ToolRegistry.SchemasFor(ToolRegistry.NamesInCategory(category), disabled))}")));

        c.That(singleChars <= allChars * SingleBundleMaxRatio,
            $"单套餐只声明 {singleChars}/{allChars} 字符（≤{SingleBundleMaxRatio:P0}）");
        c.That(doubleChars <= allChars * DoubleBundleMaxRatio,
            $"双套餐只声明 {doubleChars}/{allChars} 字符（≤{DoubleBundleMaxRatio:P0}）");
        c.That(singleChars < doubleChars && doubleChars < allChars,
            "声明量随套餐数单调递增且都小于全量");

        // ---------- ⑨ 有限前缀：常见问法的不同声明集合是可枚举的 ----------
        var sets = new[]
        {
            "巨神兵级多少钱", "我的钱包", "怎么去吉他", "频道预警怎么配", "帮我测速",
            "我的订单被压单了吗", "星图切主权",
        }
            .Select(question => string.Join(",", ToolRouter.Resolve(question, null, disabled).ToolNames))
            .Distinct(StringComparer.Ordinal)
            .Count();
        c.That(sets <= 6, $"7 个典型问题只产生 {sets} 种不同声明集合（前缀数量有限 = 缓存能反复命中）");

        // ---------- ⑩ 阶段 150：核心-only / 通用组归位 / 手动钉选 ----------
        var translateRoute = ToolRouter.Resolve("帮我翻译这段英文", null, disabled);
        c.That(!translateRoute.FallbackToAll, "「翻译」不再回退全量");
        c.That(!translateRoute.Bundles.Contains("Channel"), "「翻译」不再把 Channel（channel_scan）带进来");
        c.That(translateRoute.ToolNames.Contains("translate"), "「翻译」仍声明核心常驻的 translate");

        var linkRoute = ToolRouter.Resolve("找个好用的 EVE 工具站", null, disabled);
        c.That(linkRoute.Bundles.Contains("General"), "「工具站 / 链接」命中通用组");
        c.That(linkRoute.ToolNames.Contains("links_search"), "通用组带上了 links_search");

        c.That(ToolRouter.Resolve("频道预警怎么配", null, disabled).Bundles.Contains("Channel"),
            "「频道」仍然命中 Channel");

        var pinned = new HashSet<string>(StringComparer.Ordinal) { "Map" };
        var pinnedRoute = ToolRouter.Resolve("我的钱包余额", null, disabled, pinnedBundles: pinned);
        c.That(pinnedRoute.ToolNames.Contains("map_route"), "手动钉选的 Map 套餐始终声明（与本次意图无关）");
        c.That(pinnedRoute.Bundles.Contains("Map") && pinnedRoute.Bundles.Contains("Character"),
            "钉选套餐与命中的套餐一起出现在路由结果里");

        var twoPinned = new HashSet<string>(StringComparer.Ordinal) { "Map", "General" };
        var twoPinnedRoute = ToolRouter.Resolve("巨神兵级多少钱", null, disabled, pinnedBundles: twoPinned);
        c.That(twoPinnedRoute.ToolNames.Contains("map_route") && twoPinnedRoute.ToolNames.Contains("links_search"),
            "钉选不受 MaxBundles 限制（两组钉选 + 命中的商业组同时生效）");
    }
}
