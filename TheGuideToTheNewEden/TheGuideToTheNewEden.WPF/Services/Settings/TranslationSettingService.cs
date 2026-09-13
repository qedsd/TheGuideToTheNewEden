using System.Globalization;
using TheGuideToTheNewEden.WPF.Services.Translation;
using TheGuideToTheNewEden.WPF.Services.Translation.Llm;

namespace TheGuideToTheNewEden.WPF.Services.Settings;

/// <summary>
/// 翻译设置（存在共用的 settings.json 里，键名前缀 <c>TranslationPage.</c>）：
/// 翻译方向 + AI 翻译（大模型）的全部参数。
/// <para>
/// 两个历史键已不再读写，旧值留在 settings.json 里也不会被使用：
/// <c>TranslationPage.Provider</c>（"选中的翻译源"，改为左侧导航里的「AI 翻译 / 本地词库」两个子项）与
/// <c>TranslationPage.ActiveTab</c>（同一页里的页签位置，改为导航子项后没有"页签"了）。
/// </para>
/// <para>
/// WinUI 的 <c>TranslationSetting.FromLanguage/ToLanguage</c> 是给有道 API 用的语言代码，不再沿用。
/// API Key 目前以明文存在 settings.json（与 ESILicense.txt 同样的坦诚做法）；
/// 需要更强保护时可在本类里换成 DPAPI（<c>ProtectedData</c>）后再落盘。
/// </para>
/// </summary>
public static class TranslationSettingService
{
    private const string DirectionKey = "TranslationPage.Direction";
    private const string AiFromKey = "TranslationPage.Ai.From";
    private const string AiToKey = "TranslationPage.Ai.To";
    private const string AiPairAKey = "TranslationPage.Ai.PairA";
    private const string AiPairBKey = "TranslationPage.Ai.PairB";
    private const string LocalFromKey = "TranslationPage.Local.From";
    private const string LocalToKey = "TranslationPage.Local.To";

    private const string AiProtocolKey = "TranslationPage.Ai.Protocol";
    private const string AiBaseUrlKey = "TranslationPage.Ai.BaseUrl";
    private const string AiApiKeyKey = "TranslationPage.Ai.ApiKey";
    private const string AiModelKey = "TranslationPage.Ai.Model";
    private const string AiApiVersionKey = "TranslationPage.Ai.ApiVersion";
    private const string AiTemperatureKey = "TranslationPage.Ai.Temperature";
    private const string AiMaxTokensKey = "TranslationPage.Ai.MaxTokens";
    private const string AiTimeoutKey = "TranslationPage.Ai.TimeoutSeconds";
    private const string AiUseGlossaryKey = "TranslationPage.Ai.UseGlossary";
    private const string AiGlossaryLimitKey = "TranslationPage.Ai.GlossaryLimit";
    private const string AiStreamKey = "TranslationPage.Ai.Stream";
    private const string AiCacheKey = "TranslationPage.Ai.Cache";
    private const string AiCollectCandidatesKey = "TranslationPage.Ai.CollectCandidates";
    private const string AiMaxChunkCharsKey = "TranslationPage.Ai.MaxChunkChars";
    private const string AiThinkingModeKey = "TranslationPage.Ai.ThinkingMode";
    private const string AiSystemPromptKey = "TranslationPage.Ai.SystemPrompt";

    private static readonly object Sync = new();

    /// <summary>AI 翻译页的方向（源语言，可为 auto）。</summary>
    public static string AiFrom { get; private set; } = TranslationLanguages.Auto;

    /// <summary>AI 翻译页的方向（目标语言，可为 auto = 自动译成另一种语言）。</summary>
    public static string AiTo { get; private set; } = TranslationLanguages.Auto;

    /// <summary>自动互译语言对的第一种语言（通常设成自己的母语：其它语言都译成它，见 <see cref="TranslationLanguageHelper.Resolve(string?, string?, string?, string?, string?)"/>）。</summary>
    public static string AiPairA { get; private set; } = TranslationLanguages.Chinese;

    /// <summary>自动互译语言对的第二种语言。</summary>
    public static string AiPairB { get; private set; } = TranslationLanguages.English;

    /// <summary>本地词库页的方向（源语言，可为 auto；只支持中英）。</summary>
    public static string LocalFrom { get; private set; } = TranslationLanguages.Auto;

    /// <summary>本地词库页的方向（目标语言；只支持中英）。</summary>
    public static string LocalTo { get; private set; } = TranslationLanguages.Chinese;

    /// <summary>AI 翻译设置（设置页编辑它的副本后调用 <see cref="SetAi"/>）。</summary>
    public static AiTranslationSettings Ai { get; private set; } = new();

    /// <summary>AI 端点的只读快照（协议层用）。</summary>
    public static ChatEndpoint AiEndpoint => Ai.ToEndpoint();

    /// <summary>是否启用术语表注入（快捷访问）。</summary>
    public static bool AiUseGlossary => Ai.UseGlossary;

    /// <summary>单次注入的术语条数上限（快捷访问）。</summary>
    public static int AiGlossaryLimit => Ai.GlossaryLimit;

    /// <summary>是否流式输出（快捷访问）。</summary>
    public static bool AiStream => Ai.Stream;

    /// <summary>自定义系统提示词（空 = 内置）。</summary>
    public static string AiSystemPrompt => Ai.SystemPrompt;

    /// <summary>是否收集"AI 新词候选"（快捷访问）。</summary>
    public static bool AiCollectCandidates => Ai.CollectCandidates;

    /// <summary>单次请求最多带多少字符（0 = 不分片）。</summary>
    public static int AiMaxChunkChars => Ai.MaxChunkChars;

    /// <summary>思考模式（见 <see cref="ThinkingModes"/>）。</summary>
    public static string AiThinkingMode => Ai.ThinkingMode;

    public static void Initialize()
    {
        // 旧键 TranslationPage.Direction（0 自动 / 1 英→中 / 2 中→英）只用于"首次升级时把方向带过来"，
        // 之后两个页面各存自己的 (From, To)
        var legacy = MapLegacyDirection(SettingsService.GetInt(DirectionKey, 0));

        AiFrom = TranslationLanguages.IsSource(SettingsService.GetValue(AiFromKey)) ? TranslationLanguages.Normalize(SettingsService.GetValue(AiFromKey)) : legacy.From;
        AiTo = TranslationLanguages.IsTargetOption(SettingsService.GetValue(AiToKey)) ? TranslationLanguages.Normalize(SettingsService.GetValue(AiToKey)) : legacy.To;
        LocalFrom = TranslationLanguages.IsSource(SettingsService.GetValue(LocalFromKey)) ? TranslationLanguages.Normalize(SettingsService.GetValue(LocalFromKey)) : legacy.From;
        LocalTo = TranslationLanguages.IsTarget(SettingsService.GetValue(LocalToKey))
            ? TranslationLanguages.Normalize(SettingsService.GetValue(LocalToKey))
            : (legacy.To == TranslationLanguages.Auto ? TranslationLanguages.Chinese : legacy.To);

        AiPairA = TranslationLanguages.IsTarget(SettingsService.GetValue(AiPairAKey)) ? TranslationLanguages.Normalize(SettingsService.GetValue(AiPairAKey)) : TranslationLanguages.Chinese;
        AiPairB = TranslationLanguages.IsTarget(SettingsService.GetValue(AiPairBKey)) ? TranslationLanguages.Normalize(SettingsService.GetValue(AiPairBKey)) : TranslationLanguages.English;
        if (AiPairA == AiPairB)
        {
            AiPairB = AiPairA == TranslationLanguages.Chinese ? TranslationLanguages.English : TranslationLanguages.Chinese;
        }

        LoadAi();
    }

    /// <summary>保存"自动互译语言对"（两种不同的具体语言）。</summary>
    public static void SetAiPair(string pairA, string pairB)
    {
        var a = TranslationLanguages.IsTarget(pairA) ? TranslationLanguages.Normalize(pairA) : TranslationLanguages.Chinese;
        var b = TranslationLanguages.IsTarget(pairB) ? TranslationLanguages.Normalize(pairB) : TranslationLanguages.English;
        if (a == b)
        {
            b = a == TranslationLanguages.Chinese ? TranslationLanguages.English : TranslationLanguages.Chinese;
        }

        if (AiPairA == a && AiPairB == b)
        {
            return;
        }

        AiPairA = a;
        AiPairB = b;
        SettingsService.SetValue(AiPairAKey, a);
        SettingsService.SetValue(AiPairBKey, b);
    }

    /// <summary>旧的方向枚举值 → (源, 目标)。0（自动）= 中英双向自动互译。</summary>
    private static (string From, string To) MapLegacyDirection(int value) => value switch
    {
        1 => (TranslationLanguages.English, TranslationLanguages.Chinese),
        2 => (TranslationLanguages.Chinese, TranslationLanguages.English),
        _ => (TranslationLanguages.Auto, TranslationLanguages.Auto),
    };

    /// <summary>保存 AI 翻译页的方向（源语言可为 auto，目标语言也可为 auto = 自动译成另一种语言）。</summary>
    public static void SetAiDirection(string from, string to)
    {
        var source = TranslationLanguages.IsSource(from) ? TranslationLanguages.Normalize(from) : TranslationLanguages.Auto;
        var target = TranslationLanguages.IsTargetOption(to) ? TranslationLanguages.Normalize(to) : TranslationLanguages.Auto;
        if (AiFrom == source && AiTo == target)
        {
            return;
        }

        AiFrom = source;
        AiTo = target;
        SettingsService.SetValue(AiFromKey, source);
        SettingsService.SetValue(AiToKey, target);
    }

    /// <summary>保存本地词库页的方向（只做中英：目标语言非中/英退回中文，源语言非 auto/中/英退回 auto）。</summary>
    public static void SetLocalDirection(string from, string to)
    {
        var source = TranslationLanguages.IsSource(from) ? TranslationLanguages.Normalize(from) : TranslationLanguages.Auto;
        var target = TranslationLanguages.IsTarget(to) ? TranslationLanguages.Normalize(to) : TranslationLanguages.Chinese;

        if (target != TranslationLanguages.Chinese && target != TranslationLanguages.English)
        {
            target = TranslationLanguages.Chinese;
        }

        if (source != TranslationLanguages.Auto && source != TranslationLanguages.Chinese && source != TranslationLanguages.English)
        {
            source = TranslationLanguages.Auto;
        }

        if (LocalFrom == source && LocalTo == target)
        {
            return;
        }

        LocalFrom = source;
        LocalTo = target;
        SettingsService.SetValue(LocalFromKey, source);
        SettingsService.SetValue(LocalToKey, target);
    }

    /// <summary>保存 AI 设置（逐项写盘并立即刷新缓存开关）。</summary>
    public static void SetAi(AiTranslationSettings settings)
    {
        var value = settings ?? new AiTranslationSettings();

        // 协议名首尾空格、地址末尾斜杠这类"手输噪声"在入口处清掉，避免 404/401 排查困难
        value.Protocol = string.IsNullOrWhiteSpace(value.Protocol) ? ChatProtocolFactory.OpenAi : value.Protocol.Trim();
        value.BaseUrl = (value.BaseUrl ?? string.Empty).Trim();
        value.ApiKey = (value.ApiKey ?? string.Empty).Trim();
        value.Model = (value.Model ?? string.Empty).Trim();
        value.ApiVersion = (value.ApiVersion ?? string.Empty).Trim();
        value.Temperature = Math.Clamp(value.Temperature, 0, 2);
        value.MaxTokens = Math.Clamp(value.MaxTokens, 0, 32000);
        value.TimeoutSeconds = Math.Clamp(value.TimeoutSeconds <= 0 ? 60 : value.TimeoutSeconds, 5, 600);
        value.GlossaryLimit = Math.Clamp(value.GlossaryLimit, 0, 200);
        value.MaxChunkChars = Math.Clamp(value.MaxChunkChars, 0, 200_000);
        value.ThinkingMode = ThinkingModes.IsDefined(value.ThinkingMode) ? value.ThinkingMode : ThinkingModes.Auto;

        lock (Sync)
        {
            Ai = value;

            SettingsService.SetValue(AiProtocolKey, value.Protocol);
            SettingsService.SetValue(AiBaseUrlKey, value.BaseUrl);
            SettingsService.SetValue(AiApiKeyKey, value.ApiKey);
            SettingsService.SetValue(AiModelKey, value.Model);
            SettingsService.SetValue(AiApiVersionKey, value.ApiVersion);
            SettingsService.SetDouble(AiTemperatureKey, value.Temperature);
            SettingsService.SetInt(AiMaxTokensKey, value.MaxTokens);
            SettingsService.SetInt(AiTimeoutKey, value.TimeoutSeconds);
            SettingsService.SetBool(AiUseGlossaryKey, value.UseGlossary);
            SettingsService.SetInt(AiGlossaryLimitKey, value.GlossaryLimit);
            SettingsService.SetBool(AiStreamKey, value.Stream);
            SettingsService.SetBool(AiCacheKey, value.Cache);
            SettingsService.SetBool(AiCollectCandidatesKey, value.CollectCandidates);
            SettingsService.SetInt(AiMaxChunkCharsKey, value.MaxChunkChars);
            SettingsService.SetValue(AiThinkingModeKey, value.ThinkingMode);
            SettingsService.SetValue(AiSystemPromptKey, value.SystemPrompt);
            SettingsService.Save();
        }

        TranslationCache.IsEnabled = value.Cache;
    }

    private static void LoadAi()
    {
        var settings = new AiTranslationSettings
        {
            Protocol = SettingsService.GetValue(AiProtocolKey) is { Length: > 0 } protocol ? protocol : ChatProtocolFactory.OpenAi,
            BaseUrl = SettingsService.GetValue(AiBaseUrlKey) ?? string.Empty,
            ApiKey = SettingsService.GetValue(AiApiKeyKey) ?? string.Empty,
            Model = SettingsService.GetValue(AiModelKey) ?? string.Empty,
            ApiVersion = SettingsService.GetValue(AiApiVersionKey) ?? string.Empty,
            Temperature = SettingsService.GetDouble(AiTemperatureKey) ?? 0.2,
            MaxTokens = SettingsService.GetInt(AiMaxTokensKey, 1024),
            TimeoutSeconds = SettingsService.GetInt(AiTimeoutKey, 60),
            UseGlossary = SettingsService.GetBool(AiUseGlossaryKey, true),
            GlossaryLimit = SettingsService.GetInt(AiGlossaryLimitKey, 30),
            Stream = SettingsService.GetBool(AiStreamKey, true),
            Cache = SettingsService.GetBool(AiCacheKey, true),
            CollectCandidates = SettingsService.GetBool(AiCollectCandidatesKey, true),
            MaxChunkChars = SettingsService.GetInt(AiMaxChunkCharsKey, 12000),
            ThinkingMode = SettingsService.GetValue(AiThinkingModeKey) is { Length: > 0 } thinking && ThinkingModes.IsDefined(thinking)
                ? thinking
                : ThinkingModes.Off,
            SystemPrompt = SettingsService.GetValue(AiSystemPromptKey) ?? string.Empty,
        };

        // 协议与数值做一次规整，避免手改 settings.json 后出现非法值
        if (ChatProtocolFactory.All.All(p => !string.Equals(p.Key, settings.Protocol, StringComparison.Ordinal)))
        {
            settings.Protocol = ChatProtocolFactory.OpenAi;
        }

        settings.Temperature = Math.Clamp(settings.Temperature, 0, 2);
        settings.MaxTokens = Math.Clamp(settings.MaxTokens, 0, 32000);
        settings.TimeoutSeconds = Math.Clamp(settings.TimeoutSeconds <= 0 ? 60 : settings.TimeoutSeconds, 5, 600);
        settings.GlossaryLimit = Math.Clamp(settings.GlossaryLimit, 0, 200);
        settings.MaxChunkChars = Math.Clamp(settings.MaxChunkChars, 0, 200_000);
        if (!ThinkingModes.IsDefined(settings.ThinkingMode))
        {
            settings.ThinkingMode = ThinkingModes.Off;
        }

        lock (Sync)
        {
            Ai = settings;
        }

        TranslationCache.IsEnabled = settings.Cache;
    }

    /// <summary>把 AI 设置格式化成一行摘要（设置页显示当前状态用）。</summary>
    public static string DescribeAi()
    {
        var endpoint = AiEndpoint;
        if (!endpoint.IsConfigured)
        {
            return "未配置";
        }

        var protocol = ChatProtocolFactory.Get(Ai.Protocol);
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} · {1} · 术语{2} · 缓存{3}",
            protocol.Key,
            Ai.Model,
            Ai.UseGlossary ? $"开({Ai.GlossaryLimit})" : "关",
            Ai.Cache ? "开" : "关");
    }
}
