using TheGuideToTheNewEden.WPF.Services.Translation.Llm;

namespace TheGuideToTheNewEden.WPF.Services.Assistant;

/// <summary>一次路由的结果。</summary>
/// <param name="ToolNames">要声明给模型的工具名（**已按字典序排好**，保持缓存契约）。</param>
/// <param name="Bundles">命中的固定套餐键（回退到全部时为空表）。</param>
/// <param name="FallbackToAll">是否因为"看不出意图"而声明了全部工具。</param>
public sealed record ToolRoute(IReadOnlyList<string> ToolNames, IReadOnlyList<string> Bundles, bool FallbackToAll)
{
    /// <summary>
    /// 是不是"追问没有关键词 → 沿用本对话最近用过的套餐"（阶段 134）。
    /// 只影响日志文案；能力上是"核心常驻 + 粘性工具所在套餐"，比全量声明省一大截。
    /// </summary>
    public bool StickyFallback { get; init; }
}

/// <summary>
/// 按需声明（阶段 123）：**本地确定性**地决定这一次请求要声明哪些工具。
/// <para>
/// 为什么不是"每轮自由挑子集"：工具声明是请求的固定前缀，而 DeepSeek 的上下文缓存按**前缀逐字节匹配**
/// 计费（命中 0.02 元/M vs 未命中 1 元/M，差 50 倍）。每轮都换一套工具会让前缀一直变、缓存一直不命中，
/// 省下来的 token 钱还不够赔缓存。所以这里的做法是：
/// </para>
/// <list type="number">
///   <item><b>固定套餐</b>：套餐就是 <see cref="ToolRegistry.Categories"/> 里那五组（商业 / 角色 / 星图 / 频道 / 通用），
///     最多同时选两组 ⇒ 不同前缀的数量是有限的（几套 + 若干组合 + 全量），反复问同类问题必然命中缓存；</item>
///   <item><b>核心常驻</b>：<see cref="ToolRegistry.CoreToolNames"/>（ESI 网关四个 + 名字解析 + 翻译 + 角色清单）
///     任何路由都带上——它们保证"没有专用工具时也能通过网关做事"，把误判的代价压到最低；</item>
///   <item><b>粘性</b>：本对话（以及本轮）已经用过的工具始终保留，避免"上一轮能用、下一轮突然没了"；</item>
///   <item><b>看不出意图就声明全部</b>：一个关键词都没命中时直接回退全量——**省 token 只在有把握时省**。</item>
/// </list>
/// <para>本类是纯函数（同样的输入必然得到同样的输出），便于探针离线断言。</para>
/// </summary>
public static class ToolRouter
{
    /// <summary>最多同时选几个套餐（多了就接近全量，收益消失）。</summary>
    public const int MaxBundles = 2;

    /// <summary>
    /// 套餐 → 关键词（中英混排，**小写**比较，子串命中）。
    /// 只放"意图词"：物品/系统/角色的具体名字不在此列（那些由 search_sde / ESI 网关兜底）。
    /// </summary>
    private static readonly Dictionary<string, string[]> Keywords = new(StringComparer.Ordinal)
    {
        ["Business"] =
        [
            "价格", "多少钱", "值多少", "卖价", "买价", "均价", "行情", "走势", "涨", "跌", "订单", "买单", "卖单",
            "挂单", "被压", "估价", "估值", "倒货", "利润", "回报率", "利差", "价差", "比价", "购物车", "收藏",
            "市场", "market", "isk", "price", "order", "profit", "spread", "trend", "appraisal", "volume",
            "how much", "worth",
        ],
        ["Character"] =
        [
            "我的角色", "我的", "我有", "我在", "技能", "技能点", "钱包", "余额", "资产", "邮件", "合同", "工业",
            "制造", "研发", "声望", "联系人", "忠诚点", "勋章", "日历", "位置", "军团", "联盟", "危险", "战斗力",
            "战绩", "击杀", "损失", "单挑", "抱团", "wallet", "skill", "asset", "mail", "contract",
            "industry", "standing", "contact", "loyalty", "medal", "calendar", "location", "zkb", "kill", "corp",
            "my wallet", "my skill", "my asset", "my mail", "my contract",
        ],
        ["Map"] =
        [
            "星图", "星系", "星域", "跳桥", "路线", "怎么去", "多远", "距离", "跳数", "虫洞", "洞系", "洞口", "ded",
            "死亡空间", "定位", "主权", "安等", "安全等级", "resource", "route", "jump", "wormhole",
            "system", "sovereignty", "region", "where", "distance", "how far",
        ],
        // 注意：这里只放**意图词**，不要把"吉他""巨神兵级"这类**实体名**放进来——
        // 实体名到处出现（"在吉他多少钱"既有星系又有价格），会把问题拉进无关套餐。
        // 实体名的解析交给核心常驻的 search_sde 与 ESI 网关。
        // 阶段 150：这里只放**与 Channel 套餐里真实工具（channel_scan = 名单统计）相关**的词。
        // 「翻译 / 术语 / 链接 / 网站」曾经也在这里，但它们对应的工具（translate 常驻、glossary_write / links_search
        // 属通用组）并不在 Channel 套餐里——按旧表会把 channel_scan 无谓地声明出来。
        ["Channel"] =
        [
            "频道", "预警", "监控", "本地频道", "名单", "统计", "成员", "channel", "scan",
        ],
        ["General"] =
        [
            "测速", "延迟", "丢包", "服务器", "ping", "任务", "攻略", "怎么做", "mission", "任务攻略", "latency",
            // 阶段 150：链接库 / 术语表的工具都在通用组，词也要跟着归位（否则会被 Channel 误吞）
            "链接", "网站", "工具站", "link", "术语", "术语表", "glossary",
            // 阶段 152：改功能配置（config_* 工具属通用组）
            // 阶段 154：功能名本身也要能选中通用组，否则"多开预览"这类问题拿不到 config_read（它只会去查手册）
            "配置", "设置项", "改设置", "改成", "设为", "调整为", "启用", "禁用", "开关", "config", "setting",
            "多开", "预览", "浮窗", "进程",
            // 阶段 155：功能重启
            "重启", "重载", "重新开始", "restart",
        ],
    };

    /// <summary>
    /// 只意味着"用核心常驻工具就够"的词（阶段 150）：`translate` 本身就是核心常驻，
    /// 命中这些词时声明"核心 + 固定套餐 + 粘性"即可，既不拉 Channel，也不回退全量。
    /// </summary>
    private static readonly string[] CoreOnlyKeywords = ["翻译", "互译", "译成", "translate"];

    /// <summary>
    /// 路由：按 <paramref name="userText"/> 选套餐（最多 <see cref="MaxBundles"/> 组），加上核心常驻与
    /// <paramref name="sticky"/>（本对话/本轮用过的工具），最后剔除 <paramref name="disabled"/>（用户关掉的）。
    /// </summary>
    /// <param name="preferStickyWhenNoIntent">
    /// 看不出意图时是否**沿用粘性工具所在的套餐**（阶段 134）：长对话的追问（"那第二条呢"）往往一个关键词都不命中，
    /// 老行为是回退全量（46 个工具、2.4 万字符），既贵又稀释注意力；打开后最多选两组最近用过的套餐，
    /// 没有粘性工具才回退全量。
    /// </param>
    /// <param name="pinnedBundles">
    /// 用户在设置里**手动钉选**的套餐（阶段 150）：永远并入声明，不受 <see cref="MaxBundles"/> 限制。
    /// 用于覆盖关键词表的判断（"我这段时间主要用星图，别老把它挤掉"）。
    /// </param>
    public static ToolRoute Resolve(
        string? userText,
        IReadOnlySet<string>? sticky,
        IReadOnlySet<string>? disabled,
        bool preferStickyWhenNoIntent = false,
        IReadOnlySet<string>? pinnedBundles = null)
    {
        var text = (userText ?? string.Empty).ToLowerInvariant();
        var scored = new List<(string Category, int Score)>();
        foreach (var category in ToolRegistry.Categories)
        {
            if (category == "Esi")
            {
                continue;   // 网关是核心常驻，不参与套餐竞争
            }

            if (!Keywords.TryGetValue(category, out var keywords))
            {
                continue;
            }

            var score = keywords.Count(keyword => text.Contains(keyword, StringComparison.Ordinal));
            if (score > 0)
            {
                scored.Add((category, score));
            }
        }

        // 排序：命中多的优先；同分按固定的套餐顺序（保证确定性）
        var chosen = scored
            .OrderByDescending(s => s.Score)
            .ThenBy(s => Array.IndexOf(ToolRegistry.Categories, s.Category))
            .Take(MaxBundles)
            .Select(s => s.Category)
            .ToList();

        var productNames = ToolRegistry.Product.Select(t => t.Name).ToList();
        var knownNames = ToolRegistry.All.Select(t => t.Name).ToList();

        // 手动钉选的套餐（阶段 150）：只接受真实套餐名（Esi 是核心常驻，不算套餐），顺序固定。
        var pinned = (pinnedBundles ?? new HashSet<string>(StringComparer.Ordinal))
            .Where(category => category != "Esi" && Array.IndexOf(ToolRegistry.Categories, category) >= 0)
            .OrderBy(category => Array.IndexOf(ToolRegistry.Categories, category))
            .ToList();
        var bundles = chosen
            .Concat(pinned)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(category => Array.IndexOf(ToolRegistry.Categories, category))
            .ToList();

        if (chosen.Count == 0)
        {
            // 兜底 1（阶段 150）：只命中"核心工具就够"的词（翻译）→ 核心 + 钉选 + 粘性，不拉套餐也不回退全量
            if (CoreOnlyKeywords.Any(keyword => text.Contains(keyword, StringComparison.Ordinal)))
            {
                var coreNames = new HashSet<string>(ToolRegistry.CoreToolNames, StringComparer.Ordinal);
                foreach (var category in pinned)
                {
                    coreNames.UnionWith(ToolRegistry.NamesInCategory(category));
                }

                if (sticky is not null)
                {
                    coreNames.UnionWith(sticky);
                }

                return new ToolRoute(Sort(coreNames, disabled, knownNames), bundles, false);
            }

            // 兜底 2（追问）：粘性工具所属的套餐（最多两组），没有则回退全量
            var stickyBundles = preferStickyWhenNoIntent ? BundlesOf(sticky) : [];
            if (stickyBundles.Count > 0)
            {
                var stickyNames = new HashSet<string>(ToolRegistry.CoreToolNames, StringComparer.Ordinal);
                foreach (var category in stickyBundles.Concat(pinned))
                {
                    stickyNames.UnionWith(ToolRegistry.NamesInCategory(category));
                }

                if (sticky is not null)
                {
                    stickyNames.UnionWith(sticky);
                }

                return new ToolRoute(
                    Sort(stickyNames, disabled, knownNames),
                    stickyBundles.Concat(pinned).Distinct(StringComparer.Ordinal)
                        .OrderBy(category => Array.IndexOf(ToolRegistry.Categories, category))
                        .ToList(),
                    false)
                {
                    StickyFallback = true,
                };
            }

            // 兜底 3：看不出意图 → 声明全部（只省有把握的地方）；粘性里的非产品工具（探针替身）也一并带上
            var fallback = ToolRegistry.MergeNames(productNames, sticky);
            return new ToolRoute(Sort(fallback, disabled, knownNames), [], true);
        }

        var names = new HashSet<string>(ToolRegistry.CoreToolNames, StringComparer.Ordinal);
        foreach (var category in chosen.Concat(pinned))
        {
            names.UnionWith(ToolRegistry.NamesInCategory(category));
        }

        if (sticky is not null)
        {
            names.UnionWith(sticky);
        }

        return new ToolRoute(Sort(names, disabled, knownNames), bundles, false);
    }

    /// <summary>粘性工具按套餐计数，取最多的 <see cref="MaxBundles"/> 组（同分按固定套餐顺序）。</summary>
    private static List<string> BundlesOf(IReadOnlySet<string>? sticky)
    {
        if (sticky is not { Count: > 0 })
        {
            return [];
        }

        return sticky
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .GroupBy(ToolRegistry.CategoryOf, StringComparer.Ordinal)
            .Where(group => group.Key != "Esi")   // 网关本来就常驻
            .OrderByDescending(group => group.Count())
            .ThenBy(group => Array.IndexOf(ToolRegistry.Categories, group.Key))
            .Take(MaxBundles)
            .Select(group => group.Key)
            .ToList();
    }

    /// <summary>只保留注册表里真实存在的产品工具、剔除用户关闭的、并按字典序输出（缓存契约）。</summary>
    private static IReadOnlyList<string> Sort(IEnumerable<string> names, IReadOnlySet<string>? disabled, IReadOnlyList<string> all)
    {
        var known = new HashSet<string>(all, StringComparer.Ordinal);
        return names
            .Where(known.Contains)
            .Where(name => disabled is null || !disabled.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>按名字集合取声明（供引擎直接用；顺序仍是全局字典序）。</summary>
    public static IReadOnlyList<ChatTool> Schemas(ToolRoute route, IReadOnlySet<string>? disabled)
        => ToolRegistry.SchemasFor(route.ToolNames, disabled);
}
