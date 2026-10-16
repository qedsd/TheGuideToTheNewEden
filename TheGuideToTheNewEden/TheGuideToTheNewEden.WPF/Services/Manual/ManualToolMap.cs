namespace TheGuideToTheNewEden.WPF.Services.Manual;

/// <summary>
/// 页面/功能 → 说明书 id 的映射表（M2）。
/// <para>
/// 两个消费方：
/// ① <b>F1</b>：主窗口按键时按「当前页类型」反查手册 id（<see cref="TryGetManual"/>）；
/// ② <b>AI 失败回填</b>：工具报错时在结果末尾提示「相关手册」（后续阶段接入，只进工具结果、不进固定前缀）。
/// </para>
/// <para>
/// 新增页面时在这里补一行即可；漏登记只会导致 F1 打开手册列表（不会报错）。
/// </para>
/// </summary>
public static class ManualToolMap
{
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
}
