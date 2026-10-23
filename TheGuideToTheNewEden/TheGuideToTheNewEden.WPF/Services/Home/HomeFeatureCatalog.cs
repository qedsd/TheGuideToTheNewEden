using System.Windows;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.Services.Home;

/// <summary>快捷方式被点击时的处理方式。</summary>
public enum HomeFeatureKind
{
    /// <summary>导航到主窗口里的功能页。</summary>
    Page,

    /// <summary>打开独立的工具窗口（<c>ToolWindowHost</c>）。</summary>
    ToolWindow,

    /// <summary>直接执行一个动作（打开目录 / 检查更新…），没有对应页面。</summary>
    Action,
}

/// <summary>
/// 一个可以固定到主页的功能。
/// <para>
/// <see cref="TitleKey"/> / <see cref="GroupKey"/> 一律复用导航已有的 <c>Nav.*</c> 键，
/// 因此磁贴标题与左侧菜单永远一致、且<b>零新增翻译</b>；只有描述（添加功能窗口里的副标题）
/// 用本模块自己的 <c>Home.Feature.*Desc</c> 键。
/// </para>
/// </summary>
public sealed record HomeFeature(
    string Id,
    SymbolRegular Icon,
    string IconKey,
    string TitleKey,
    string GroupKey,
    string DescriptionKey,
    HomeFeatureKind Kind,
    Type? PageType = null,
    string? ToolTitleKey = null)
{
    /// <summary>
    /// 彩色矢量图标的资源键后缀（对应 <c>Resources/NavIcons.xaml</c> 的 <c>NavIcon.&lt;IconKey&gt;</c>）。
    /// 主页磁贴用它；<see cref="Icon"/>（Segoe 字体图标）留给仍需单色图标的场景（如设置页）。
    /// </summary>
    public string IconResourceKey => "NavIcon." + IconKey;

    /// <summary>磁贴 / 列表里的标题（每次读取都重新查资源，语言切换后自动是新语言）。</summary>
    public string Title => Resolve(TitleKey);

    /// <summary>所属分组名（分组键就是导航键，如 <c>Nav.Business</c>）。</summary>
    public string GroupTitle => Resolve(GroupKey);

    public string Description => Resolve(DescriptionKey);

    private static string Resolve(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;
}

/// <summary>
/// 主页快捷方式的<b>功能目录</b>：所有可以被固定的功能都在这里定义一次。
/// <para>
/// 目录与左侧导航的一致性由 <c>MainWindow</c> 启动后的双向比对保证（缺项只写日志），
/// 因此新增导航页时不会"忘了给主页做磁贴"。
/// </para>
/// </summary>
public static class HomeFeatureCatalog
{
    /// <summary>全部功能，顺序 = 添加功能窗口里的默认顺序（按分组聚拢）。</summary>
    public static IReadOnlyList<HomeFeature> Features { get; } = Build();

    /// <summary>分组键（按首次出现顺序去重）；添加功能窗口的左侧列表用它。</summary>
    public static IReadOnlyList<string> GroupKeys { get; } =
        Features.Select(p => p.GroupKey).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>
    /// 刻意<b>不提供</b>快捷方式、也不参与"目录 ↔ 导航"比对的导航页。
    /// 目前只有主页自己：把主页固定到主页没有意义。
    /// </summary>
    public static IReadOnlyList<Type> NavigationExclusions { get; } =
    [
        typeof(Views.Pages.HomePage),
    ];

    public static HomeFeature? Find(string? id)
        => string.IsNullOrEmpty(id) ? null : Features.FirstOrDefault(p => p.Id == id);

    /// <summary>按页面类型找功能（导航右键菜单用）。</summary>
    public static HomeFeature? FindByPageType(Type? pageType)
        => pageType is null ? null : Features.FirstOrDefault(p => p.PageType == pageType);

    private static List<HomeFeature> Build() =>
    [
        // ---------- 角色 ----------
        new("characters", SymbolRegular.Person24, "characters", "Nav.Characters", "Nav.Characters",
            "Home.Feature.CharactersDesc", HomeFeatureKind.Page, typeof(Views.Pages.Characters.CharactersShellPage)),

        // ---------- 商业 ----------
        new("market", SymbolRegular.ArrowTrending24, "market", "Nav.Market", "Nav.Business",
            "Home.Feature.MarketDesc", HomeFeatureKind.Page, typeof(Views.Pages.MarketPage)),
        new("order", SymbolRegular.Receipt24, "order", "Nav.Order", "Nav.Business",
            "Home.Feature.OrderDesc", HomeFeatureKind.Page, typeof(Views.Pages.OrderPage)),
        new("scalper", SymbolRegular.ShoppingBag24, "scalper", "Nav.Scalper", "Nav.Business",
            "Home.Feature.ScalperDesc", HomeFeatureKind.Page, typeof(Views.Pages.ScalperPage)),
        new("compare-market", SymbolRegular.ArrowSwap24, "compareMarket", "Nav.CompareMarket", "Nav.Business",
            "Home.Feature.CompareMarketDesc", HomeFeatureKind.Page, typeof(Views.Pages.CompareMarketPage)),
        new("appraisal", SymbolRegular.Calculator24, "appraisal", "Nav.Appraisal", "Nav.Business",
            "Home.Feature.AppraisalDesc", HomeFeatureKind.Page, typeof(Views.Pages.AppraisalPage)),

        // ---------- 工业 ----------
        new("industry-plan", SymbolRegular.Building24, "industry", "Nav.IndustryPlan", "Nav.Industry",
            "Home.Feature.IndustryPlanDesc", HomeFeatureKind.Page, typeof(Views.Pages.IndustryPlanPage)),
        new("industry-blueprints", SymbolRegular.DocumentText24, "industry", "Nav.IndustryBlueprints", "Nav.Industry",
            "Home.Feature.IndustryBlueprintsDesc", HomeFeatureKind.Page, typeof(Views.Pages.IndustryBlueprintPage)),
        new("industry-inventory", SymbolRegular.Database24, "industry", "Nav.IndustryInventory", "Nav.Industry",
            "Home.Feature.IndustryInventoryDesc", HomeFeatureKind.Page, typeof(Views.Pages.IndustryInventoryPage)),
        new("industry-recycle", SymbolRegular.ArrowSync24, "industry", "Nav.IndustryRecycle", "Nav.Industry",
            "Home.Feature.IndustryRecycleDesc", HomeFeatureKind.Page, typeof(Views.Pages.IndustryRecyclePage)),

        // ---------- 多开 ----------
        new("game-preview", SymbolRegular.SquareMultiple24, "gamePreview", "Nav.GamePreview", "Nav.GamePreview",
            "Home.Feature.GamePreviewDesc", HomeFeatureKind.Page, typeof(Views.Pages.GamePreviewPage)),

        // ---------- AI 助手 ----------
        // 彩色磁贴图标用 NavIcons.xaml 里已有的 NavIcon.assistant（该文件由 design/branding/icons 生成）
        new("assistant", SymbolRegular.Sparkle24, "assistant", "Nav.Assistant", "Nav.Assistant",
            "Home.Feature.AssistantDesc", HomeFeatureKind.Page, typeof(Views.Pages.AiAssistantPage)),

        // ---------- 频道 ----------
        new("channel-intel", SymbolRegular.Alert24, "channelIntel", "Nav.ChannelIntel", "Nav.Channel",
            "Home.Feature.ChannelIntelDesc", HomeFeatureKind.Page, typeof(Views.Pages.ChannelIntelPage)),
        new("channel-monitor", SymbolRegular.Comment24, "channelMonitor", "Nav.ChannelMonitor", "Nav.Channel",
            "Home.Feature.ChannelMonitorDesc", HomeFeatureKind.Page, typeof(Views.Pages.ChannelMonitorPage)),
        new("channel-scan", SymbolRegular.People24, "channelScan", "Nav.ChannelScan", "Nav.Channel",
            "Home.Feature.ChannelScanDesc", HomeFeatureKind.Page, typeof(Views.Pages.ChannelScanPage)),
        new("channel-market", SymbolRegular.Money24, "channelMarket", "Nav.ChannelMarket", "Nav.Channel",
            "Home.Feature.ChannelMarketDesc", HomeFeatureKind.Page, typeof(Views.Pages.ChannelMarketPage)),
        new("game-log-monitor", SymbolRegular.DocumentText24, "gameLogMonitor", "Nav.GameLogMonitor", "Nav.Channel",
            "Home.Feature.GameLogMonitorDesc", HomeFeatureKind.Page, typeof(Views.Pages.GameLogMonitorPage)),

        // ---------- 翻译 ----------
        new("translation-ai", SymbolRegular.Translate24, "translationAi", "Nav.TranslationAi", "Nav.Translation",
            "Home.Feature.TranslationAiDesc", HomeFeatureKind.Page, typeof(Views.Pages.AiTranslationPage)),
        new("translation-channel", SymbolRegular.CommentMultiple24, "channelTranslation", "Nav.ChannelTranslation", "Nav.Translation",
            "Home.Feature.TranslationChannelDesc", HomeFeatureKind.Page, typeof(Views.Pages.ChannelTranslationPage)),
        new("translation-local", SymbolRegular.LocalLanguage24, "translationLocal", "Nav.TranslationLocal", "Nav.Translation",
            "Home.Feature.TranslationLocalDesc", HomeFeatureKind.Page, typeof(Views.Pages.LocalTranslationPage)),

        // ---------- 攻略 ----------
        new("ded", SymbolRegular.DataSunburst24, "ded", "Nav.DED", "Nav.Guides",
            "Home.Feature.DedDesc", HomeFeatureKind.Page, typeof(Views.Pages.DEDPage)),
        new("mission", SymbolRegular.DocumentOnePage24, "mission", "Nav.Mission", "Nav.Guides",
            "Home.Feature.MissionDesc", HomeFeatureKind.Page, typeof(Views.Pages.MissionPage)),

        // ---------- 虫洞 / 链接 / 星图 / KB / 数据库 ----------
        new("wormhole", SymbolRegular.Target24, "wormhole", "Nav.Wormhole", "Nav.Wormhole",
            "Home.Feature.WormholeDesc", HomeFeatureKind.Page, typeof(Views.Pages.WormholePage)),
        new("links", SymbolRegular.Link24, "links", "Nav.Links", "Nav.Links",
            "Home.Feature.LinksDesc", HomeFeatureKind.Page, typeof(Views.Pages.LinksPage)),
        new("map", SymbolRegular.Map24, "map", "Nav.Map", "Nav.Map",
            "Home.Feature.MapDesc", HomeFeatureKind.Page, typeof(Views.Pages.MapPage)),
        new("zkb", SymbolRegular.Scan24, "zkb", "Nav.ZKB", "Nav.ZKB",
            "Home.Feature.ZkbDesc", HomeFeatureKind.Page, typeof(Views.Pages.ZKBPage)),
        new("database", SymbolRegular.Database24, "database", "Nav.Database", "Nav.Database",
            "Home.Feature.DatabaseDesc", HomeFeatureKind.Page, typeof(Views.Pages.DatabasePage)),
        new("eve-settings-sync", SymbolRegular.ArrowSync24, "eveSettingsSync", "Nav.EveSettingsSync", "Nav.EveSettingsSync",
            "Home.Feature.EveSettingsSyncDesc", HomeFeatureKind.Page, typeof(Views.Pages.EveSettingsSyncPage)),

        // ---------- 小工具（独立工具窗口，不走主窗口导航） ----------
        new("tool-ime", SymbolRegular.Keyboard24, "toolsIME", "Nav.Tools_IME", "Nav.Tools",
            "Home.Feature.ToolImeDesc", HomeFeatureKind.ToolWindow, typeof(Views.Pages.IMEPage), "Nav.Tools_IME"),
        new("tool-server-ping", SymbolRegular.Gauge24, "serverPing", "Nav.Tools_ServerPing", "Nav.Tools",
            "Home.Feature.ToolServerPingDesc", HomeFeatureKind.ToolWindow, typeof(Views.Pages.ServerPingPage), "Nav.Tools_ServerPing"),
        new("tool-keyboard-span", SymbolRegular.Wrench24, "keyboardSpan", "Nav.Tools_KeyboardSpan", "Nav.Tools",
            "Home.Feature.ToolKeyboardSpanDesc", HomeFeatureKind.ToolWindow, typeof(Views.Pages.KeyboardSpanPage), "Nav.Tools_KeyboardSpan"),
        new("tool-taskbar", SymbolRegular.Window24, "taskbar", "Nav.Tools_Taskbar", "Nav.Tools",
            "Home.Feature.ToolTaskbarDesc", HomeFeatureKind.ToolWindow, typeof(Views.Pages.TaskbarPage), "Nav.Tools_Taskbar"),

        // ---------- 设置 ----------
        new("settings", SymbolRegular.Settings24, "settings", "Nav.Settings", "Nav.Settings",
            "Home.Feature.SettingsDesc", HomeFeatureKind.Page, typeof(Views.Pages.SettingsPage)),

        // ---------- 轻量动作（没有页面） ----------
        new("action-open-log", SymbolRegular.DocumentBulletList24, "gameLogMonitor", "Home.Action.OpenLog", "Home.Group.System",
            "Home.Feature.OpenLogDesc", HomeFeatureKind.Action),
        new("action-open-config", SymbolRegular.Folder24, "eveSettingsSync", "Home.Action.OpenConfigFolder", "Home.Group.System",
            "Home.Feature.OpenConfigDesc", HomeFeatureKind.Action),
        new("action-check-update", SymbolRegular.CloudArrowDown24, "assistant", "Home.Action.CheckUpdate", "Home.Group.System",
            "Home.Feature.CheckUpdateDesc", HomeFeatureKind.Action),
        new("action-manual", SymbolRegular.BookQuestionMark24, "guides", "Home.Action.Manual", "Home.Group.System",
            "Home.Feature.ManualDesc", HomeFeatureKind.Action),
        new("action-about", SymbolRegular.Info24, "home", "Home.Action.About", "Home.Group.System",
            "Home.Feature.AboutDesc", HomeFeatureKind.Action),

        // ---------- 设置子页（L3：直接落到某个设置分类） ----------
        new("setting-general", SymbolRegular.Settings24, "settings", "SettingPage_General", "Nav.Settings",
            "SettingPage_General_Desc", HomeFeatureKind.Action),
        new("setting-game-log", SymbolRegular.Document24, "gameLogMonitor", "SettingPage_GameLog", "Nav.Settings",
            "SettingPage_GameLog_Desc", HomeFeatureKind.Action),
        new("setting-market", SymbolRegular.DataTrending24, "market", "SettingPage_Market", "Nav.Settings",
            "SettingPage_Market_Desc", HomeFeatureKind.Action),
        new("setting-structures", SymbolRegular.Building24, "database", "SettingPage_Structures", "Nav.Settings",
            "SettingPage_Structures_Desc", HomeFeatureKind.Action),
        new("setting-esi-scope", SymbolRegular.Shield24, "characters", "SettingPage_ESIScope", "Nav.Settings",
            "SettingPage_ESIScope_Desc", HomeFeatureKind.Action),
        // 阶段 121：AI 相关设置合并到「AI 助手」页（原「AI 翻译」分类已取消）。
        // **故意沿用旧 id `setting-ai-translation`**：用户已经把它固定到主页的磁贴不会失效
        // （改成新 id 会让旧磁贴按"未知 id"被丢弃），只把图标/标题/描述与目标页换掉。
        new("setting-ai-translation", SymbolRegular.Sparkle24, "translationAi", "SettingPage_AiAssistant", "Nav.Settings",
            "SettingPage_AiAssistant_Desc", HomeFeatureKind.Action),
        new("setting-glossary", SymbolRegular.LocalLanguage24, "translationLocal", "SettingPage_Glossary", "Nav.Settings",
            "SettingPage_Glossary_Desc", HomeFeatureKind.Action),
        new("setting-keyboard", SymbolRegular.Keyboard24, "keyboardSpan", "SettingPage_KeyboardList", "Nav.Settings",
            "SettingPage_KeyboardList_Desc", HomeFeatureKind.Action),
        new("setting-test", SymbolRegular.Bug24, "tools", "SettingPage_Test", "Nav.Settings",
            "SettingPage_Test_Desc", HomeFeatureKind.Action),
        new("setting-update", SymbolRegular.ArrowSync24, "eveSettingsSync", "SettingPage_Update", "Nav.Settings",
            "SettingPage_Update_Desc", HomeFeatureKind.Action),
        // 没有 setting-home：主页本身不是设置子页，且"设置 → 主页"已删（阶段 94），
        // 快捷方式的编辑统一在主页的编辑模式与「⋯」菜单里完成。
    ];

    /// <summary>首次运行写入主页的推荐组合（见 HOME_DESIGN.md §4.6）。</summary>
    public static IReadOnlyList<string> DefaultSeed { get; } =
    [
        "characters", "market", "order", "scalper", "appraisal", "game-preview",
        "channel-intel", "channel-monitor", "channel-market", "channel-scan",
        "game-log-monitor", "map",
    ];
}
