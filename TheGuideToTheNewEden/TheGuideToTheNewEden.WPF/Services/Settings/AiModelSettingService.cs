using TheGuideToTheNewEden.WPF.Services.Translation;
using TheGuideToTheNewEden.WPF.Services.Translation.Llm;

namespace TheGuideToTheNewEden.WPF.Services.Settings;

/// <summary>
/// 大模型端点与生成参数的存储（阶段 125 新设）：键前缀 <c>Ai.</c>。
/// <para>
/// <b>为什么不放在 TranslationSettingService 里</b>：模型端点是**助手与翻译共用**的一份
/// （助手要它、翻译也要它），挂在"翻译设置"下面名不副实。阶段 121 把 UI 从「AI 翻译」搬到「AI 助手」时
/// 只是搬了界面，这一轮把数据也搬正：<c>Ai.*</c> = 模型，<c>Ai.Translation.*</c> = 翻译专属。
/// </para>
/// <para>
/// <b>旧键完全不读</b>：老版本把这一切存在 <c>TranslationPage.Ai.*</c>，这里**当作从未存在过**——
/// 不做迁移、不读旧值。旧值仍留在 settings.json 里（我们不动用户文件）但没有任何代码读它，
/// 用户只需在「设置 → AI → 模型」里重新填一次地址与 Key。
/// </para>
/// </summary>
public static class AiModelSettingService
{
    private const string ProtocolKey = "Ai.Protocol";
    private const string BaseUrlKey = "Ai.BaseUrl";
    private const string ApiKeyKey = "Ai.ApiKey";
    private const string ModelKey = "Ai.Model";
    private const string ApiVersionKey = "Ai.ApiVersion";
    private const string TemperatureKey = "Ai.Temperature";
    private const string MaxTokensKey = "Ai.MaxTokens";
    private const string TimeoutKey = "Ai.TimeoutSeconds";
    private const string StreamKey = "Ai.Stream";
    private const string ThinkingModeKey = "Ai.ThinkingMode";

    private static readonly object Sync = new();

    /// <summary>当前的模型端点与生成参数（设置页编辑它的副本后调用 <see cref="SetModel"/>）。</summary>
    public static AiModelSettings Model { get; private set; } = new();

    /// <summary>协议层用的端点快照（助手与翻译都用它）。</summary>
    public static ChatEndpoint Endpoint => Model.ToEndpoint();

    /// <summary>读设置。由 <c>CoreInitializer</c> 在启动时调用一次。</summary>
    public static void Initialize()
    {
        var settings = new AiModelSettings
        {
            Protocol = Text(ProtocolKey, ChatProtocolFactory.OpenAi),
            BaseUrl = Text(BaseUrlKey, string.Empty),
            ApiKey = Text(ApiKeyKey, string.Empty),
            Model = Text(ModelKey, string.Empty),
            ApiVersion = Text(ApiVersionKey, string.Empty),
            Temperature = SettingsService.GetDouble(TemperatureKey) ?? 0.2,
            MaxTokens = SettingsService.GetInt(MaxTokensKey, 0),
            TimeoutSeconds = SettingsService.GetInt(TimeoutKey, 60),
            Stream = SettingsService.GetBool(StreamKey, true),
            ThinkingMode = Text(ThinkingModeKey, ThinkingModes.Off),
        };

        Normalize(settings);
        lock (Sync)
        {
            Model = settings;
        }
    }

    /// <summary>保存模型设置（逐项写盘并立即刷新端点）。</summary>
    public static void SetModel(AiModelSettings settings)
    {
        var value = settings?.Clone() ?? new AiModelSettings();
        Normalize(value);

        lock (Sync)
        {
            Model = value;

            SettingsService.SetValue(ProtocolKey, value.Protocol);
            SettingsService.SetValue(BaseUrlKey, value.BaseUrl);
            SettingsService.SetValue(ApiKeyKey, value.ApiKey);
            SettingsService.SetValue(ModelKey, value.Model);
            SettingsService.SetValue(ApiVersionKey, value.ApiVersion);
            SettingsService.SetDouble(TemperatureKey, value.Temperature);
            SettingsService.SetInt(MaxTokensKey, value.MaxTokens);
            SettingsService.SetInt(TimeoutKey, value.TimeoutSeconds);
            SettingsService.SetBool(StreamKey, value.Stream);
            SettingsService.SetValue(ThinkingModeKey, value.ThinkingMode);
            SettingsService.Save();
        }
    }

    /// <summary>协议名首尾空格、地址末尾斜杠这类"手输噪声"与非法数值在入口处清掉，避免 404/401 排查困难。</summary>
    private static void Normalize(AiModelSettings value)
    {
        value.Protocol = string.IsNullOrWhiteSpace(value.Protocol) ? ChatProtocolFactory.OpenAi : value.Protocol.Trim();
        if (ChatProtocolFactory.All.All(p => !string.Equals(p.Key, value.Protocol, StringComparison.Ordinal)))
        {
            value.Protocol = ChatProtocolFactory.OpenAi;
        }

        value.BaseUrl = (value.BaseUrl ?? string.Empty).Trim();
        value.ApiKey = (value.ApiKey ?? string.Empty).Trim();
        value.Model = (value.Model ?? string.Empty).Trim();
        value.ApiVersion = (value.ApiVersion ?? string.Empty).Trim();
        value.Temperature = Math.Clamp(value.Temperature, 0, 2);
        value.MaxTokens = Math.Clamp(value.MaxTokens, 0, 32000);
        value.TimeoutSeconds = Math.Clamp(value.TimeoutSeconds <= 0 ? 60 : value.TimeoutSeconds, 5, 600);
        value.ThinkingMode = ThinkingModes.IsDefined(value.ThinkingMode) ? value.ThinkingMode : ThinkingModes.Auto;
    }

    /// <summary>读一个字符串键；缺失或空串时用默认值。</summary>
    private static string Text(string key, string fallback)
        => SettingsService.GetValue(key) is { Length: > 0 } value ? value : fallback;
}
