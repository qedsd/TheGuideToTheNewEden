using TheGuideToTheNewEden.WPF.Services.Translation.Llm;

namespace TheGuideToTheNewEden.WPF.Services.Assistant;

/// <summary>
/// 工具注册表（静态注册）：<c>Name → ITool</c> + 给模型的 <see cref="ChatTool"/> 声明。
/// <para>
/// 静态构造函数注册内置工具（服务层惯例：静态类 + 静态注册）；
/// 新工具（星图/商业等）只需在 <see cref="ToolRegistry"/> 静态构造函数里加一行。
/// </para>
/// </summary>
public static class ToolRegistry
{
    private static readonly Dictionary<string, ITool> Tools = new(StringComparer.Ordinal);

    static ToolRegistry()
    {
        // 阶段 B：3 个代表工具
        Register(new Tools.MarketOrdersTool());
        Register(new Tools.TranslateTool());
        Register(new Tools.SystemNavigateTool());
        // 阶段 C：通用 9 个
        Register(new Tools.AppraisalTool());
        Register(new Tools.MarketNavigateTool());
        Register(new Tools.CharacterListTool());
        Register(new Tools.CharacterOverviewTool());
        Register(new Tools.CharacterWalletTool());
        Register(new Tools.CharacterSkillsTool());
        Register(new Tools.CharacterNavigateTool());
        Register(new Tools.SearchSdeTool());
        Register(new Tools.MarketHistoryTool());
        // 阶段 C2：星图 7 个
        Register(new Tools.MapRouteTool());
        Register(new Tools.MapSystemInfoTool());
        Register(new Tools.MapJumpBridgeListTool());
        Register(new Tools.MapJumpBridgeAddTool());
        Register(new Tools.MapJumpBridgeRemoveTool());
        Register(new Tools.MapJumpBridgeImportTool());
        Register(new Tools.MapLayerTool());
        // 阶段 C3：商业 4 个
        Register(new Tools.ScalperSettingsTool());
        Register(new Tools.ScalperAnalyseTool());
        Register(new Tools.CompareAnalyseTool());
        Register(new Tools.MarketTrendTool());
        // 阶段 114：v1.5 P1 九个（订单/购买文本/邮件/合同/工业/位置/ZKB/DED/虫洞）
        Register(new Tools.CharacterOrdersTool());
        Register(new Tools.BuildBuyOrderTextTool());
        Register(new Tools.CharacterMailTool());
        Register(new Tools.CharacterContractsTool());
        Register(new Tools.CharacterIndustryTool());
        Register(new Tools.CharacterLocationTool());
        Register(new Tools.ZkbEntityTool());
        Register(new Tools.DedInfoTool());
        Register(new Tools.WormholeInfoTool());
        // 阶段 116：v1.5 P2 五个只读（测速/名单统计/快速链接/查利差/任务资料）
        Register(new Tools.ServerPingTool());
        Register(new Tools.ChannelScanTool());
        Register(new Tools.LinksSearchTool());
        Register(new Tools.TradeSpreadTool());
        Register(new Tools.MissionInfoTool());
        // 阶段 116：写操作三个（术语表 / 市场收藏 / 倒货购物车；均 RequiresConfirmation）
        Register(new Tools.GlossaryWriteTool());
        Register(new Tools.MarketFavoriteTool());
        Register(new Tools.ShoppingCartTool());
        // 阶段 117 的"角色 ESI 直读七个"（资产/蓝图/声望/联系人/LP/日历/勋章）已于**阶段 119 删除**：
        // 它们存在的唯一理由是"这些端点原本没有消费方"，而阶段 118 的 ESI 网关已覆盖全部 197 个端点，
        // 阶段 119 又把它们的"ID→名称"富化下沉成网关的通用后处理（EsiResponseEnricher），
        // 因此工具与服务层（CharacterEsiExtraService，912 行）一并删除，能力不减反增（富化对所有端点生效）。
        // 阶段 118：ESI 通用网关四个元工具（检索 / 说明 / 只读调用 / 写调用）
        Register(new Tools.EsiSearchTool());
        Register(new Tools.EsiDescribeTool());
        Register(new Tools.EsiReadTool());
        Register(new Tools.EsiWriteTool());
        // 阶段 131：内置说明书两个元工具（检索 + 阅读；Core 常驻——说明书正文只进工具结果，
        // 不进系统提示词/固定前缀，见 MANUAL_DESIGN.md 的 token 契约）
        Register(new Tools.ManualSearchTool());
        Register(new Tools.ManualReadTool());
    }

    public static void Register(ITool tool)
    {
        Tools[tool.Name] = tool;
    }

    public static ITool? Find(string name)
        => Tools.TryGetValue(name, out var tool) ? tool : null;

    /// <summary>
    /// 宽松查找：先精确，再"去掉下划线/连字符并忽略大小写"后精确（DeepSeek 的 DSML 文本里
    /// 常把 <c>map_system_info</c> 写成 <c>mapsysteminfo</c>），最后做唯一前缀匹配。
    /// 多命中一律返回 null（宁可不认，也不要调错工具）。
    /// </summary>
    public static ITool? FindLoose(string? name)
    {
        var exact = Find(name ?? string.Empty);
        if (exact is not null)
        {
            return exact;
        }

        var normalized = Normalize(name);
        if (normalized.Length == 0)
        {
            return null;
        }

        var matches = Tools.Values.Where(t => Normalize(t.Name) == normalized).ToList();
        if (matches.Count == 1)
        {
            return matches[0];
        }

        var prefixed = Tools.Values.Where(t => Normalize(t.Name).StartsWith(normalized, StringComparison.Ordinal)).ToList();
        return prefixed.Count == 1 ? prefixed[0] : null;
    }

    private static string Normalize(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        var buffer = new System.Text.StringBuilder(name.Length);
        foreach (var ch in name)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer.Append(char.ToLowerInvariant(ch));
            }
        }

        return buffer.ToString();
    }

    /// <summary>
    /// 全部工具，**按名字字典序**——这个顺序是"缓存契约"的一部分：
    /// <para>
    /// 工具声明是**每次工具轮请求的固定前缀**（实测 44 条 ≈ 2.4 万字符），而 DeepSeek 的上下文缓存按
    /// **前缀逐字节匹配**计费（命中 0.02 元/M vs 未命中 1 元/M，差 50 倍）。所以：
    /// ① 顺序必须稳定（不要改成按"常用度""最近使用"或时间排序）；
    /// ② 描述里不要放会变的值（日期/版本/计数）；
    /// ③ 预算与文体由探针 `tokens` 套件守住（单条描述 ≤140 字符、参数描述 ≤24/网关 ≤32、总字符 ≤26,000）。
    /// </para>
    /// </summary>
    public static IReadOnlyList<ITool> All => Tools.Values.OrderBy(t => t.Name).ToList();

    /// <summary>给模型的 tools 声明（Name/Description/Parameters 的协议无关形状，各适配器转自己的格式）。</summary>
    public static IReadOnlyList<ChatTool> AllSchemas
        => All.Select(t => new ChatTool
        {
            Name = t.Name,
            Description = t.Description,
            Parameters = t.Parameters,
        }).ToList();

    /// <summary>按用户开关过滤后的 tools 声明（**发给模型的就是这一份**）。</summary>
    public static IReadOnlyList<ChatTool> EnabledSchemas
        => FilterSchemas(All, AiAssistantSettingService.DisabledTools);

    /// <summary>该工具当前是否启用（关掉的工具不下发、也不执行）。</summary>
    public static bool IsEnabled(string? name)
        => AiAssistantSettingService.IsToolEnabled(name ?? string.Empty);

    /// <summary>
    /// 纯函数版本的过滤（探针直接断言，不碰 settings.json）：从 <paramref name="tools"/> 里剔除
    /// <paramref name="disabled"/> 中的名字（精确匹配；集合来自设置）。
    /// 不做"探针工具特判"——<c>probe_*</c> 只是探针注册的替身，用户设置里不可能出现它们。
    /// </summary>
    public static IReadOnlyList<ChatTool> FilterSchemas(IEnumerable<ITool> tools, IReadOnlySet<string>? disabled)
        => tools
            .Where(t => disabled is null || !disabled.Contains(t.Name))
            .Select(t => new ChatTool
            {
                Name = t.Name,
                Description = t.Description,
                Parameters = t.Parameters,
            })
            .ToList();

    /// <summary>当前启用（未被用户关闭）的工具列表。</summary>
    public static IReadOnlyList<ITool> Enabled
        => All.Where(t => AiAssistantSettingService.IsToolEnabled(t.Name)).ToList();

    // ==================================================================
    //  分类与"按需声明"的套餐（阶段 123）
    // ==================================================================

    /// <summary>产品工具（不含探针自己的 <c>probe_*</c> 替身）。</summary>
    public static IReadOnlyList<ITool> Product
        => All.Where(t => !t.Name.StartsWith("probe_", StringComparison.Ordinal)).ToList();

    /// <summary>
    /// 套餐/分组顺序（设置页分组、按需声明的路由都用它）：
    /// <c>Esi</c> 是网关、**永远常驻**，其余五个是可被路由选中的"固定套餐"。
    /// </summary>
    public static readonly string[] Categories = ["Esi", "Character", "Business", "Map", "Channel", "General"];

    /// <summary>
    /// 工具名 → 套餐键（**前缀归类**；改工具名要同步这里，否则会落到 General）。
    /// 设置页的分组与 <see cref="ToolRouter"/> 的套餐共用这一份定义。
    /// </summary>
    public static string CategoryOf(string name)
    {
        if (name.StartsWith("esi_", StringComparison.Ordinal))
        {
            return "Esi";
        }

        if (name.StartsWith("map_", StringComparison.Ordinal))
        {
            return "Map";
        }

        if (name.StartsWith("channel_", StringComparison.Ordinal))
        {
            return "Channel";
        }

        if (name.StartsWith("character_", StringComparison.Ordinal))
        {
            return "Character";
        }

        return name switch
        {
            "market_orders" or "market_history" or "market_trend" or "market_navigate" or "market_favorite"
                or "scalper_analyse" or "scalper_settings" or "compare_analyse" or "trade_spread"
                or "appraisal" or "build_buy_order_text" or "shopping_cart" => "Business",
            _ => "General",
        };
    }

    /// <summary>
    /// 常驻工具（任何路由都必须带上）：ESI 网关四个（覆盖全部 197 个 ESI 端点 = 长尾兜底）、
    /// 名字解析（<c>search_sde</c>）、翻译（<c>translate</c>）、角色清单（<c>character_list</c>）、
    /// 说明书检索与阅读（<c>manual_search</c> / <c>manual_read</c>——所有套餐里都要能查手册）。
    /// 前四类是"没有专用工具时也能做事"的保证，其余是最常用的入口。
    /// </summary>
    public static readonly string[] CoreToolNames =
    [
        "character_list", "esi_describe", "esi_read", "esi_search", "esi_write",
        "manual_read", "manual_search", "search_sde", "translate",
    ];

    /// <summary>某个套餐里的工具名。</summary>
    public static IReadOnlyList<string> NamesInCategory(string category)
        => Product.Where(t => CategoryOf(t.Name) == category).Select(t => t.Name).ToList();

    /// <summary>按名字集合取声明（**顺序仍是全局字典序**，保持缓存契约），并剔除用户关闭的工具。</summary>
    public static IReadOnlyList<ChatTool> SchemasFor(IEnumerable<string> names, IReadOnlySet<string>? disabled)
    {
        var wanted = new HashSet<string>(names, StringComparer.Ordinal);
        return FilterSchemas(All.Where(t => wanted.Contains(t.Name)), disabled);
    }

    /// <summary>一份声明的字符数（名字 + 描述 + schema + JSON 骨架），用于日志与探针核对省了多少。</summary>
    public static int DeclarationChars(IEnumerable<ChatTool> schemas)
        => schemas.Sum(s => (s.Description?.Length ?? 0) + (s.Parameters?.ToJsonString().Length ?? 2) + s.Name.Length + 70);

    /// <summary>把若干组工具名并成集合（忽略空白、去重）。</summary>
    public static HashSet<string> MergeNames(params IEnumerable<string>?[] groups)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            foreach (var name in group ?? [])
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    set.Add(name.Trim());
                }
            }
        }

        return set;
    }
}
