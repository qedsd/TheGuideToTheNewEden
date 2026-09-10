namespace TheGuideToTheNewEden.WPF.Services.Settings;

/// <summary>是否需要数据库本地化（翻译）。</summary>
public static class DBLocalizationSettingService
{
    private const string Key = "NeedLocalization";

    public static bool Value { get; set; } = true;

    public static void Initialize()
    {
        Value = SettingsService.GetBool(Key, true);
        Core.Config.NeedLocalization = Value;
    }

    public static void Set(bool value)
    {
        Value = value;
        Core.Config.NeedLocalization = value;
        SettingsService.SetBool(Key, value);
    }
}