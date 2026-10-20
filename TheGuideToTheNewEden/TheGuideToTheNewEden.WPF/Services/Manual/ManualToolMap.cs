namespace TheGuideToTheNewEden.WPF.Services.Manual;

/// <summary>
/// 页面/功能 → 说明书 id 的映射表（M2）。
/// <para>
/// 三个消费方：
/// ① <b>F1</b>：主窗口按键时按「当前页类型」反查手册 id（<see cref="TryGetManual"/>）；
/// ② <b>导航右键 / 设置分类行的「查看说明」</b>：同上；
/// ③ <b>AI 失败回填</b>：工具报错时在结果末尾提示「相关手册」（<see cref="TryGetManualForTool"/>，
/// 只进工具结果、不进固定前缀）。
/// </para>
/// <para>
/// 新增页面时在这里补一行即可；漏登记只会导致 F1 打开手册列表（不会报错）。
/// </para>
/// </summary>
public static class ManualToolMap
{
    /// <summary>
    /// 说明书自身的两个元工具：它们不隶属任何功能手册，失败回填时直接跳过。
    /// </summary>
    private static readonly HashSet<string> MetaTools = new(StringComparer.Ordinal)
    {
        "manual_search", "manual_read",
    };

    private static readonly Dictionary<Type, string> PageManuals = new()
    {
        // 主页与横切
        [typeof(Views.Pages.HomePage)] = "home",
        [typeof(Views.Pages.SettingsPage)] = "settings",
        // 商业
        [typeof(Views.Pages.MarketPage)] = "market",
        [typeof(Views.Pages.OrderPage)] = "order",
        [typeof(Views.Pages.ScalperPage)] = "scalper",
        [typeof(Views.Pages.CompareMarketPage)] = "compare-market",
        [typeof(Views.Pages.AppraisalPage)] = "appraisal",
        // 工业
        [typeof(Views.Pages.IndustryPlanPage)] = "industry/plan",
        [typeof(Views.Pages.IndustryBlueprintPage)] = "industry/blueprints",
        // 角色（壳页/卡片页/工作区共用 characters，工作区子页各归各）
        [typeof(Views.Pages.Characters.CharactersShellPage)] = "characters",
        [typeof(Views.Pages.Characters.CharacterCardsPage)] = "characters",
        [typeof(Views.Pages.Characters.CharacterWorkspacePage)] = "characters",
        [typeof(Views.Pages.Characters.OverviewPage)] = "characters/overview",
        [typeof(Views.Pages.Characters.SkillPage)] = "characters/skills",
        [typeof(Views.Pages.Characters.ClonePage)] = "characters/clone",
        [typeof(Views.Pages.Characters.WalletPage)] = "characters/wallet",
        [typeof(Views.Pages.Characters.MailPage)] = "characters/mail",
        [typeof(Views.Pages.Characters.ContractPage)] = "characters/contract",
        [typeof(Views.Pages.Characters.IndustryPage)] = "characters/industry",
        // 频道
        [typeof(Views.Pages.ChannelIntelPage)] = "channel/intel",
        [typeof(Views.Pages.ChannelMonitorPage)] = "channel/monitor",
        [typeof(Views.Pages.ChannelScanPage)] = "channel/scan",
        [typeof(Views.Pages.ChannelMarketPage)] = "channel/market",
        [typeof(Views.Pages.GameLogMonitorPage)] = "channel/log-monitor",
        // 翻译
        [typeof(Views.Pages.AiTranslationPage)] = "translation/ai",
        [typeof(Views.Pages.ChannelTranslationPage)] = "translation/channel",
        [typeof(Views.Pages.LocalTranslationPage)] = "translation/local",
        // 攻略 / 虫洞 / 链接 / 星图
        [typeof(Views.Pages.DEDPage)] = "guides/ded",
        [typeof(Views.Pages.MissionPage)] = "guides/mission",
        [typeof(Views.Pages.WormholePage)] = "wormhole",
        [typeof(Views.Pages.LinksPage)] = "links",
        [typeof(Views.Pages.MapPage)] = "map",
        // ZKB / 数据库 / 配置同步 / 多开 / 助手
        [typeof(Views.Pages.ZKBPage)] = "zkb",
        [typeof(Views.Pages.DatabasePage)] = "database",
        [typeof(Views.Pages.EveSettingsSyncPage)] = "eve-settings-sync",
        [typeof(Views.Pages.GamePreviewPage)] = "game-preview",
        [typeof(Views.Pages.AiAssistantPage)] = "assistant",
        // 工具窗口页（F1 在主窗口按，工具窗口不在主窗口内容区，这里仍登记以备窗口内复用）
        [typeof(Views.Pages.IMEPage)] = "tools/ime",
        [typeof(Views.Pages.ServerPingPage)] = "tools/server-ping",
        [typeof(Views.Pages.KeyboardSpanPage)] = "tools/keyboard-span",
        [typeof(Views.Pages.TaskbarPage)] = "tools/taskbar",
        // 设置子页
        [typeof(Views.Pages.Settings.GeneralSettingPage)] = "settings/general",
        [typeof(Views.Pages.Settings.HomeSettingPage)] = "settings/home",
        [typeof(Views.Pages.Settings.GameLogSettingPage)] = "settings/game-log",
        [typeof(Views.Pages.Settings.MarketSettingPage)] = "settings/market",
        [typeof(Views.Pages.Settings.StructuresSettingPage)] = "settings/structures",
        [typeof(Views.Pages.Settings.ESIScopeSettingPage)] = "settings/esi-scope",
        [typeof(Views.Pages.Settings.AiAssistantSettingPage)] = "settings/ai",
        [typeof(Views.Pages.Settings.GlossarySettingPage)] = "settings/glossary",
        [typeof(Views.Pages.Settings.KeyboardListPage)] = "settings/keyboard-list",
        [typeof(Views.Pages.Settings.TestSettingPage)] = "settings/test",
        [typeof(Views.Pages.Settings.UpdateSettingPage)] = "settings/update",
    };

    /// <summary>按页面类型取手册 id；没登记或说明书目录里没有这篇时返回 <c>null</c>。</summary>
    public static string? TryGetManual(Type? pageType)
    {
        if (pageType is null || !PageManuals.TryGetValue(pageType, out var id))
        {
            return null;
        }

        return ManualService.Find(id) is null ? null : id;
    }

    /// <summary>没有页面可映射时的兜底：首次使用手册（存在才返回）。</summary>
    public static string? TryGetDefault() => ManualService.Find("getting-started") is null ? null : "getting-started";

    /// <summary>
    /// 工具名 → 手册 id（工具失败时回填"去哪儿查怎么修"）。
    /// <para>
    /// 映射**不手写第二份**：直接反查各篇 front-matter 的 <c>tools:</c> 声明
    /// （<see cref="ManualService.FindManualIdForTool"/>）——这样"手册声明了哪些工具"与
    /// "工具去哪查手册"永远是同一份数据，不会两边漂移；`manuals` 探针另做双向断言兜底。
    /// 一个工具被多篇手册声明时取 id 字典序最靠前的那篇；没有手册声明它时返回 <c>null</c>。
    /// </para>
    /// </summary>
    public static string? TryGetManualForTool(string? toolName)
    {
        var name = (toolName ?? string.Empty).Trim();
        if (name.Length == 0 || MetaTools.Contains(name))
        {
            return null;
        }

        return ManualService.FindManualIdForTool(name);
    }

    /// <summary>
    /// "工具结果其实是失败"的启发式关键词（阶段 131，回填「相关手册」用；探针按这一份基线断言）。
    /// 刻意只收**能从手册里找到处置办法**的词：查不到数据（"找不到 XXX"）不算失败——
    /// 那是正常结果，手册帮不上忙，回填反而浪费 token 并诱导模型乱翻手册。
    /// </summary>
    public static readonly string[] FailureKeywords =
    [
        "失败", "需要授权", "权限不足", "没有权限", "未登录", "重新授权", "已过期",
    ];

    /// <summary>工具返回文本是否属于"失败语义"。</summary>
    public static bool LooksLikeFailure(string? text)
        => text is { Length: > 0 } && FailureKeywords.Any(keyword => text.Contains(keyword, StringComparison.Ordinal));

    /// <summary>
    /// 给模型的工具结果末尾追加的「相关手册」提示（失败时才对模型可见，约 30 字符/次）；
    /// 没有对应手册时原样返回。
    /// </summary>
    public static string WithManualHint(string? toolName, string text)
    {
        if (TryGetManualForTool(toolName) is not { Length: > 0 } id)
        {
            return text;
        }

        return text + $"\n[说明书参考: manual://{id}#errors] 可用 manual_read 读这篇的 errors 节，按手册给用户处置步骤。";
    }
}
