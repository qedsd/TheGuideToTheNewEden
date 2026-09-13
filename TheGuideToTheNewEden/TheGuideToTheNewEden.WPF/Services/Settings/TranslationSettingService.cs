using TheGuideToTheNewEden.WPF.Services.Translation;

namespace TheGuideToTheNewEden.WPF.Services.Settings;

/// <summary>
/// 翻译页设置（存在共用的 settings.json 里，键名与 WinUI 侧不冲突）：
/// 选中的翻译源与翻译方向。旧版 WinUI 的 <c>TranslationSetting.FromLanguage/ToLanguage</c>
/// 是给有道 API 用的语言代码，本地数据库翻译不需要，因此不再沿用。
/// </summary>
public static class TranslationSettingService
{
    private const string ProviderKey = "TranslationPage.Provider";
    private const string DirectionKey = "TranslationPage.Direction";

    /// <summary>选中的翻译源标识（见 <see cref="ITranslationProvider.Key"/>）。</summary>
    public static string Provider { get; private set; } = LocalDbTranslationProvider.ProviderKey;

    /// <summary>翻译方向，默认自动（按原文是否含中文判定）。</summary>
    public static TranslationDirection Direction { get; private set; } = TranslationDirection.Auto;

    public static void Initialize()
    {
        var provider = SettingsService.GetValue(ProviderKey);
        Provider = string.IsNullOrWhiteSpace(provider) ? LocalDbTranslationProvider.ProviderKey : provider;

        var direction = SettingsService.GetInt(DirectionKey, (int)TranslationDirection.Auto);
        Direction = Enum.IsDefined(typeof(TranslationDirection), direction)
            ? (TranslationDirection)direction
            : TranslationDirection.Auto;
    }

    public static void SetProvider(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || Provider == key)
        {
            return;
        }

        Provider = key;
        SettingsService.SetValue(ProviderKey, key);
    }

    public static void SetDirection(TranslationDirection direction)
    {
        if (Direction == direction)
        {
            return;
        }

        Direction = direction;
        SettingsService.SetInt(DirectionKey, (int)direction);
    }
}
