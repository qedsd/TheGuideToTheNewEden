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

    /// <summary>单次请求的最大工具轮数（防模型连环调工具烧 token）。</summary>
    public const int DefaultMaxToolRounds = 8;

    /// <summary>全局呼出快捷键的默认值。</summary>
    public const string DefaultHotkey = "Alt+Q";

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

    /// <summary>从 <c>settings.json</c> 载入（由 <c>CoreInitializer</c> 调用；幂等）。</summary>
    public static void Initialize()
    {
        MaxToolRounds = Math.Clamp(SettingsService.GetInt(MaxToolRoundsKey, DefaultMaxToolRounds), 1, 30);
        SystemPrompt = SettingsService.GetValue(SystemPromptKey) ?? string.Empty;
        ConfirmExpensive = SettingsService.GetBool(ConfirmExpensiveKey, true);

        var hotkey = SettingsService.GetValue(HotkeyKey);
        Hotkey = hotkey is null ? DefaultHotkey : hotkey.Trim();

        DefaultCharacter = SettingsService.GetValue(DefaultCharacterKey) ?? string.Empty;
    }

    /// <summary>保存全部助手设置（设置页统一提交）。</summary>
    public static void Save(int maxToolRounds, string? systemPrompt, bool confirmExpensive, string? hotkey, string? defaultCharacter)
    {
        MaxToolRounds = Math.Clamp(maxToolRounds, 1, 30);
        SystemPrompt = (systemPrompt ?? string.Empty).Trim();
        ConfirmExpensive = confirmExpensive;
        Hotkey = (hotkey ?? string.Empty).Trim();
        DefaultCharacter = (defaultCharacter ?? string.Empty).Trim();

        SettingsService.SetInt(MaxToolRoundsKey, MaxToolRounds);
        SettingsService.SetValue(SystemPromptKey, SystemPrompt);
        SettingsService.SetBool(ConfirmExpensiveKey, ConfirmExpensive);
        SettingsService.SetValue(HotkeyKey, Hotkey);
        SettingsService.SetValue(DefaultCharacterKey, DefaultCharacter);
        SettingsService.Save();
    }

    /// <summary>恢复默认值（提示词 / 工具轮数 / 确认开关 / 默认角色），快捷键保持不变。</summary>
    public static void RestoreDefaults()
        => Save(DefaultMaxToolRounds, string.Empty, true, Hotkey, string.Empty);
}
