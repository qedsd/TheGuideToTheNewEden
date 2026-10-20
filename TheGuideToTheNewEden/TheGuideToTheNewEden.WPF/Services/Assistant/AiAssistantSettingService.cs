using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Services.Assistant;

/// <summary>
/// AI 助手的专属设置（存在共用的 <c>settings.json</c> 里，键名前缀 <c>Assistant.</c>）。
/// <para>
/// **模型端点不在这里**：助手复用「AI 翻译」的端点设置（协议 / 地址 / Key / 模型 / 超时 / 思考模式），
/// 这里只放助手自己的行为参数。
/// </para>
/// </summary>
public static class AiAssistantSettingService
{
    private const string MaxToolRoundsKey = "Assistant.MaxToolRounds";
    private const string SystemPromptKey = "Assistant.SystemPrompt";
    private const string ConfirmExpensiveKey = "Assistant.ConfirmExpensive";
    private const string HotkeyKey = "Assistant.Hotkey";
    private const string DefaultCharacterKey = "Assistant.DefaultCharacter";
    private const string EsiEnabledKey = "Assistant.Esi.Enabled";
    private const string EsiWritesKey = "Assistant.Esi.Writes";
    private const string EsiEnrichKey = "Assistant.Esi.EnrichNames";
    private const string EsiMaxPagesKey = "Assistant.Esi.MaxPages";
    private const string EsiMaxResultCharsKey = "Assistant.Esi.MaxResultChars";
    private const string EsiErrorLimitFloorKey = "Assistant.Esi.ErrorLimitFloor";
    private const string EsiDeniedPathsKey = "Assistant.Esi.DeniedPaths";
    private const string DisabledToolsKey = "Assistant.Tools.Disabled";
    private const string AutoRouteKey = "Assistant.Tools.AutoRoute";
    private const string PinnedBundlesKey = "Assistant.Tools.PinnedBundles";
    private const string ShowInMainWindowKey = "Assistant.ShowInMainWindow";
    private const string SidebarCollapsedKey = "Assistant.SidebarCollapsed";
    private const string ContextWindowTokensKey = "Assistant.ContextWindowTokens";
    private const string OutputReserveTokensKey = "Assistant.OutputReserveTokens";
    private const string MaxToolCallsPerRoundKey = "Assistant.MaxToolCallsPerRound";
    private const string MaxTurnToolTokensKey = "Assistant.MaxTurnToolTokens";
    private const string SummarizeContextKey = "Assistant.SummarizeContext";

    /// <summary>单次请求的最大工具轮数（防模型连环调工具烧 token）。</summary>
    public const int DefaultMaxToolRounds = 8;

    /// <summary>
    /// 全局呼出快捷键的**默认值：空 = 不启用**（阶段 130 用户要求「AI 默认不启用快捷方式」）。
    /// 想开启就在设置里填一个组合键（如 <see cref="HotkeyHint"/>）。
    /// </summary>
    public const string DefaultHotkey = "";

    /// <summary>
    /// 设置里那个输入框的**提示示例**（占位文本，不参与默认值）：让用户知道该填什么格式。
    /// 默认不启用 ≠ 不给提示（用户要求「保留输入框提示的默认快捷方式」）。
    /// </summary>
    public const string HotkeyHint = "Alt+Q";

    /// <summary>ESI 网关默认一次最多取的页数（分页端点）。</summary>
    /// <remarks>
    /// 阶段 119 由 3 提到 10：原来"逐个端点包工具"时，资产/蓝图这类多页数据由专用工具自己翻完页再聚合；
    /// 那些工具删掉后改由网关承担，默认页数太小会让助手给出"看着完整其实只有第一页"的答案。
    /// </remarks>
    public const int DefaultEsiMaxPages = 10;

    /// <summary>ESI 网关默认的单条结果字符上限。</summary>
    public const int DefaultEsiMaxResultChars = 3500;

    /// <summary>ESI 错误配额的安全阈值（剩余次数低于它就不再调用）。</summary>
    public const int DefaultEsiErrorLimitFloor = 10;

    /// <summary>默认**不**把 AI 显示在主窗口里（阶段 128 用户要求）：所有入口都开独立窗口。</summary>
    public const bool DefaultShowInMainWindow = false;

    /// <summary>对话列表默认展开（阶段 133 追加的"收起/展开对话列表"，属于界面偏好；与显示位置同类）。</summary>
    public const bool DefaultSidebarCollapsed = false;

    /// <summary>
    /// 模型上下文窗口（token，阶段 134）：上下文预算从它里面扣掉系统提示词、工具声明、输出预留、
    /// 安全余量与本轮工具预留，剩下的才给历史记录。
    /// <para>
    /// 默认 1,000,000（与本项目主用的 DeepSeek 1M 窗口一致）；换 128k/200k 窗口的模型时**必须调小**，
    /// 否则 budget 算出来仍按 1M 走，到 provider 才报超窗。0 = 不限制（老行为，风险自负）。
    /// </para>
    /// </summary>
    public const int DefaultContextWindowTokens = 1_000_000;

    /// <summary>模型窗口的上限（防止手滑多打一个 0 把预算算成天文数字）。</summary>
    public const int MaxContextWindowTokensLimit = 4_000_000;

    /// <summary>输出预留（token）：给模型的回答留的空间；实际取它和 <c>Ai.MaxTokens</c> 的较大者。</summary>
    public const int DefaultOutputReserveTokens = 4096;

    /// <summary>输出预留的上限。</summary>
    public const int MaxOutputReserveTokensLimit = 200_000;

    /// <summary>单轮最多执行几个并行工具调用（多出来的回填"请拆分为后续轮次"，不执行）。</summary>
    public const int DefaultMaxToolCallsPerRound = 4;

    /// <summary>单轮并行工具调用上限的上限。</summary>
    public const int MaxToolCallsPerRoundLimit = 16;

    /// <summary>单轮工具结果累计的 token 上限（超出时从最旧的开始压到 300 字符）。</summary>
    public const int DefaultMaxTurnToolTokens = 12_000;

    /// <summary>单轮工具结果上限的上限。</summary>
    public const int MaxTurnToolTokensLimit = 200_000;

    /// <summary>是否启用摘要式压缩（阶段 134）：历史超预算时把较早轮次压成事实摘要，而不是整轮丢弃。</summary>
    public const bool DefaultSummarizeContext = true;

    /// <summary>单次请求的最大工具轮数（1–30）。</summary>
    public static int MaxToolRounds { get; private set; } = DefaultMaxToolRounds;

    /// <summary>自定义系统提示词（空 = 内置）。</summary>
    public static string SystemPrompt { get; private set; } = string.Empty;

    /// <summary>
    /// 是否允许"待确认的写操作"进入确认卡片。
    /// <b>关闭不等于免确认</b>：关闭后助手会**直接拒绝**这类操作并说明原因（写操作永远需要显式确认）。
    /// </summary>
    public static bool ConfirmExpensive { get; private set; } = true;

    /// <summary>全局呼出快捷键（如 <c>Alt+Q</c>；空 = 不注册）。</summary>
    public static string Hotkey { get; private set; } = DefaultHotkey;

    /// <summary>默认角色名（空 = 第一个角色）。</summary>
    public static string DefaultCharacter { get; private set; } = string.Empty;

    /// <summary>
    /// 模型上下文窗口（token；0 = 不限制）。预算与窗口护栏都从它推导，见 <see cref="AssistantContextBudget"/>。
    /// </summary>
    public static int ContextWindowTokens { get; private set; } = DefaultContextWindowTokens;

    /// <summary>输出预留（token；实际取它和 <c>Ai.MaxTokens</c> 的较大者）。</summary>
    public static int OutputReserveTokens { get; private set; } = DefaultOutputReserveTokens;

    /// <summary>单轮最多执行几个并行工具调用。</summary>
    public static int MaxToolCallsPerRound { get; private set; } = DefaultMaxToolCallsPerRound;

    /// <summary>单轮工具结果累计的 token 上限（0 = 不限制）。</summary>
    public static int MaxTurnToolTokens { get; private set; } = DefaultMaxTurnToolTokens;

    /// <summary>是否启用摘要式压缩（阶段 134；关掉就退回"整轮省略"的滑窗）。</summary>
    public static bool SummarizeContext { get; private set; } = DefaultSummarizeContext;

    // ------------------------------------------------------------------
    //  ESI 通用网关（esi_search / esi_describe / esi_read / esi_write，阶段 118）
    // ------------------------------------------------------------------

    /// <summary>是否启用 ESI 通用网关（关掉后 4 个 esi_* 工具会直接拒绝）。</summary>
    public static bool EsiEnabled { get; private set; } = true;

    /// <summary>是否允许 ESI 写操作（**仍然需要用户逐次确认**；关掉后 esi_write 直接拒绝）。</summary>
    public static bool EsiAllowWrites { get; private set; } = true;

    /// <summary>
    /// 是否把 ESI 响应里的 ID 富化成「名称 (id)」（默认开）。
    /// 关掉后模型看到的是裸 ID（省下本地库查询，但答案可读性差很多）。
    /// </summary>
    public static bool EsiEnrichNames { get; private set; } = true;

    /// <summary><c>allPages=true</c> 时最多取几页（1–10）。</summary>
    public static int EsiMaxPages { get; private set; } = DefaultEsiMaxPages;

    /// <summary>单条 ESI 结果的最大字符数（500–12000）。</summary>
    public static int EsiMaxResultChars { get; private set; } = DefaultEsiMaxResultChars;

    /// <summary>ESI 错误配额护栏阈值（0 = 不检查）。</summary>
    public static int EsiErrorLimitFloor { get; private set; } = DefaultEsiErrorLimitFloor;

    /// <summary>禁止调用的路径（每行一条，支持 <c>*</c> 通配；空 = 全部允许）。</summary>
    public static string EsiDeniedPaths { get; private set; } = string.Empty;

    // ------------------------------------------------------------------
    //  逐工具开关（阶段 121）
    // ------------------------------------------------------------------

    /// <summary>
    /// 被**关闭**的工具名（其余全部启用）。关掉的工具**不会下发给模型**（省输入 token），
    /// 且模型万一硬报这个名字也会在执行侧被拒绝。
    /// <para>只存"关掉的那些"：默认空集合 = 全部启用，新增工具自动启用，不需要迁移。</para>
    /// </summary>
    public static IReadOnlySet<string> DisabledTools { get; private set; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>该工具是否启用（未列进禁用集合即启用）。</summary>
    public static bool IsToolEnabled(string name)
        => string.IsNullOrEmpty(name) || !DisabledTools.Contains(name);

    /// <summary>
    /// **按需声明**（阶段 123）：<c>true</c>（默认）= 只声明与本次提问相关的套餐 + 核心常驻工具 + 本对话用过的工具；
    /// <c>false</c> = 始终声明全部工具（老行为）。
    /// <para>看不出意图时一定回退成全量——省 token 只在有把握时省。</para>
    /// </summary>
    public static bool AutoRouteTools { get; private set; } = true;

    /// <summary>
    /// 用户**手动钉选**的套餐（阶段 150）：无论关键词路由选出什么，这些套餐的工具都会声明。
    /// 用来纠正关键词表的偏差（"我主要在弄星图，别把它挤掉"）；空集合 = 不钉选。
    /// </summary>
    public static IReadOnlySet<string> PinnedBundles { get; private set; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>保存"按需声明"开关。</summary>
    public static void SaveRouting(bool autoRoute)
    {
        AutoRouteTools = autoRoute;
        SettingsService.SetBool(AutoRouteKey, AutoRouteTools);
        SettingsService.Save();
    }

    /// <summary>保存手动钉选的套餐（只接受真实套餐名；Esi 是核心常驻，不计入）。</summary>
    public static void SavePinnedBundles(IEnumerable<string>? bundles)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var category in bundles ?? [])
        {
            if (!string.IsNullOrWhiteSpace(category)
                && category != "Esi"
                && Array.IndexOf(ToolRegistry.Categories, category) >= 0)
            {
                set.Add(category.Trim());
            }
        }

        PinnedBundles = set;
        SettingsService.SetValue(PinnedBundlesKey, string.Join(",", set.OrderBy(n => n, StringComparer.Ordinal)));
        SettingsService.Save();
    }

    /// <summary>
    /// **AI 是否显示在主窗口里**（阶段 128）：<c>false</c>（默认）= 所有入口（页脚常驻项、主页 banner 快捷提问、
    /// 主页磁贴、快捷路由转来的提问）都用**独立窗口**；<c>true</c> = 助手页显示在主窗口里
    /// （全局快捷键 <c>Alt+Q</c> 两种情况都弹独立窗口）。
    /// </summary>
    public static bool ShowInMainWindow { get; private set; } = DefaultShowInMainWindow;

    /// <summary>对话列表（左栏）是否收起（阶段 133 追加；界面偏好，会记住）。</summary>
    public static bool SidebarCollapsed { get; private set; } = DefaultSidebarCollapsed;

    /// <summary>保存"AI 是否显示在主窗口里"。</summary>
    public static void SaveShowInMainWindow(bool showInMainWindow)
    {
        ShowInMainWindow = showInMainWindow;
        SettingsService.SetBool(ShowInMainWindowKey, ShowInMainWindow);
        SettingsService.Save();
    }

    /// <summary>
    /// 记住"对话列表是否收起"（阶段 133 追加）。与显示位置一样属于**界面偏好**，
    /// 存在 settings.json 里（不单列设置项，用户直接点左栏顶部的按钮切换）。
    /// </summary>
    public static void SaveSidebarCollapsed(bool collapsed)
    {
        SidebarCollapsed = collapsed;
        SettingsService.SetBool(SidebarCollapsedKey, SidebarCollapsed);
        SettingsService.Save();
    }

    /// <summary>保存禁用集合（设置页提交；只写这一个键）。</summary>
    public static void SaveDisabledTools(IEnumerable<string>? disabledTools)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in disabledTools ?? [])
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                set.Add(name.Trim());
            }
        }

        DisabledTools = set;
        SettingsService.SetValue(DisabledToolsKey, string.Join(",", set.OrderBy(n => n, StringComparer.Ordinal)));
        SettingsService.Save();
    }

    /// <summary>从 <c>settings.json</c> 载入（由 <c>CoreInitializer</c> 调用；幂等）。</summary>
    public static void Initialize()
    {
        MaxToolRounds = Math.Clamp(SettingsService.GetInt(MaxToolRoundsKey, DefaultMaxToolRounds), 1, 30);
        SystemPrompt = SettingsService.GetValue(SystemPromptKey) ?? string.Empty;
        ConfirmExpensive = SettingsService.GetBool(ConfirmExpensiveKey, true);

        var hotkey = SettingsService.GetValue(HotkeyKey);
        Hotkey = hotkey is null ? DefaultHotkey : hotkey.Trim();

        DefaultCharacter = SettingsService.GetValue(DefaultCharacterKey) ?? string.Empty;

        ContextWindowTokens = Math.Clamp(
            SettingsService.GetInt(ContextWindowTokensKey, DefaultContextWindowTokens),
            0,
            MaxContextWindowTokensLimit);
        OutputReserveTokens = Math.Clamp(
            SettingsService.GetInt(OutputReserveTokensKey, DefaultOutputReserveTokens),
            0,
            MaxOutputReserveTokensLimit);
        MaxToolCallsPerRound = Math.Clamp(
            SettingsService.GetInt(MaxToolCallsPerRoundKey, DefaultMaxToolCallsPerRound),
            1,
            MaxToolCallsPerRoundLimit);
        MaxTurnToolTokens = Math.Clamp(
            SettingsService.GetInt(MaxTurnToolTokensKey, DefaultMaxTurnToolTokens),
            0,
            MaxTurnToolTokensLimit);
        SummarizeContext = SettingsService.GetBool(SummarizeContextKey, DefaultSummarizeContext);

        EsiEnabled = SettingsService.GetBool(EsiEnabledKey, true);
        EsiAllowWrites = SettingsService.GetBool(EsiWritesKey, true);
        EsiEnrichNames = SettingsService.GetBool(EsiEnrichKey, true);
        EsiMaxPages = Math.Clamp(SettingsService.GetInt(EsiMaxPagesKey, DefaultEsiMaxPages), 1, 10);
        EsiMaxResultChars = Math.Clamp(
            SettingsService.GetInt(EsiMaxResultCharsKey, DefaultEsiMaxResultChars), 500, 12000);
        EsiErrorLimitFloor = Math.Clamp(
            SettingsService.GetInt(EsiErrorLimitFloorKey, DefaultEsiErrorLimitFloor), 0, 100);
        EsiDeniedPaths = SettingsService.GetValue(EsiDeniedPathsKey) ?? string.Empty;

        var disabled = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in (SettingsService.GetValue(DisabledToolsKey) ?? string.Empty)
                 .Split([',', ';', '\n', '\r', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            disabled.Add(name);
        }

        DisabledTools = disabled;
        AutoRouteTools = SettingsService.GetBool(AutoRouteKey, true);

        var pinned = new HashSet<string>(StringComparer.Ordinal);
        foreach (var category in (SettingsService.GetValue(PinnedBundlesKey) ?? string.Empty)
                 .Split([',', ';', '\n', '\r', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (category != "Esi" && Array.IndexOf(ToolRegistry.Categories, category) >= 0)
            {
                pinned.Add(category);
            }
        }

        PinnedBundles = pinned;
        ShowInMainWindow = SettingsService.GetBool(ShowInMainWindowKey, DefaultShowInMainWindow);
        SidebarCollapsed = SettingsService.GetBool(SidebarCollapsedKey, DefaultSidebarCollapsed);

        // 阶段 151：载入按模型记录的估算校准系数（没有就保持 1.0）
        AssistantUsageCalibration.Load();
    }

    /// <summary>保存 ESI 网关设置（设置页与主保存同一处提交）。</summary>
    public static void SaveEsi(
        bool enabled,
        bool allowWrites,
        bool enrichNames,
        int maxPages,
        int maxResultChars,
        int errorLimitFloor,
        string? deniedPaths)
    {
        EsiEnabled = enabled;
        EsiAllowWrites = allowWrites;
        EsiEnrichNames = enrichNames;
        EsiMaxPages = Math.Clamp(maxPages, 1, 10);
        EsiMaxResultChars = Math.Clamp(maxResultChars, 500, 12000);
        EsiErrorLimitFloor = Math.Clamp(errorLimitFloor, 0, 100);
        EsiDeniedPaths = (deniedPaths ?? string.Empty).Replace("\r\n", "\n").Trim();

        SettingsService.SetBool(EsiEnabledKey, EsiEnabled);
        SettingsService.SetBool(EsiWritesKey, EsiAllowWrites);
        SettingsService.SetBool(EsiEnrichKey, EsiEnrichNames);
        SettingsService.SetInt(EsiMaxPagesKey, EsiMaxPages);
        SettingsService.SetInt(EsiMaxResultCharsKey, EsiMaxResultChars);
        SettingsService.SetInt(EsiErrorLimitFloorKey, EsiErrorLimitFloor);
        SettingsService.SetValue(EsiDeniedPathsKey, EsiDeniedPaths);
        SettingsService.Save();
    }

    /// <summary>保存全部助手设置（设置页统一提交）。</summary>
    public static void Save(
        int maxToolRounds,
        string? systemPrompt,
        bool confirmExpensive,
        string? hotkey,
        string? defaultCharacter,
        int contextWindowTokens = DefaultContextWindowTokens,
        int outputReserveTokens = DefaultOutputReserveTokens,
        int maxToolCallsPerRound = DefaultMaxToolCallsPerRound,
        int maxTurnToolTokens = DefaultMaxTurnToolTokens,
        bool summarizeContext = DefaultSummarizeContext)
    {
        MaxToolRounds = Math.Clamp(maxToolRounds, 1, 30);
        SystemPrompt = (systemPrompt ?? string.Empty).Trim();
        ConfirmExpensive = confirmExpensive;
        Hotkey = (hotkey ?? string.Empty).Trim();
        DefaultCharacter = (defaultCharacter ?? string.Empty).Trim();
        ContextWindowTokens = Math.Clamp(contextWindowTokens, 0, MaxContextWindowTokensLimit);
        OutputReserveTokens = Math.Clamp(outputReserveTokens, 0, MaxOutputReserveTokensLimit);
        MaxToolCallsPerRound = Math.Clamp(maxToolCallsPerRound, 1, MaxToolCallsPerRoundLimit);
        MaxTurnToolTokens = Math.Clamp(maxTurnToolTokens, 0, MaxTurnToolTokensLimit);
        SummarizeContext = summarizeContext;

        SettingsService.SetInt(MaxToolRoundsKey, MaxToolRounds);
        SettingsService.SetValue(SystemPromptKey, SystemPrompt);
        SettingsService.SetBool(ConfirmExpensiveKey, ConfirmExpensive);
        SettingsService.SetValue(HotkeyKey, Hotkey);
        SettingsService.SetValue(DefaultCharacterKey, DefaultCharacter);
        SettingsService.SetInt(ContextWindowTokensKey, ContextWindowTokens);
        SettingsService.SetInt(OutputReserveTokensKey, OutputReserveTokens);
        SettingsService.SetInt(MaxToolCallsPerRoundKey, MaxToolCallsPerRound);
        SettingsService.SetInt(MaxTurnToolTokensKey, MaxTurnToolTokens);
        SettingsService.SetBool(SummarizeContextKey, SummarizeContext);
        SettingsService.Save();
    }

    /// <summary>
    /// 恢复默认值（提示词 / 工具轮数 / 确认开关 / 默认角色 / 快捷键 / 上下文窗口与摘要 / ESI 网关 / 工具开关 /
    /// 按需声明 / 显示位置）。
    /// <para>
    /// 阶段 130 起**快捷键也一并复位**（回到 <see cref="DefaultHotkey"/> = 空 = 不启用）——
    /// 既然"默认不启用"是默认值，那"恢复默认"就该把它恢复成不启用（早先的"保持快捷键不变"已不适用）。
    /// </para>
    /// </summary>
    public static void RestoreDefaults()
    {
        Save(
            DefaultMaxToolRounds,
            string.Empty,
            true,
            DefaultHotkey,
            string.Empty,
            DefaultContextWindowTokens,
            DefaultOutputReserveTokens,
            DefaultMaxToolCallsPerRound,
            DefaultMaxTurnToolTokens,
            DefaultSummarizeContext);
        SaveEsi(true, true, true, DefaultEsiMaxPages, DefaultEsiMaxResultChars, DefaultEsiErrorLimitFloor, string.Empty);
        SaveDisabledTools([]);
        SaveRouting(true);
        SavePinnedBundles([]);
        SaveShowInMainWindow(DefaultShowInMainWindow);
        SaveSidebarCollapsed(DefaultSidebarCollapsed);
        AssistantUsageCalibration.Reset();
    }
}
