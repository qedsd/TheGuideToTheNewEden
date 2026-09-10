namespace TheGuideToTheNewEden.WPF.Services.Settings;

/// <summary>是否启用自动更新。</summary>
public static class AutoUpdateService
{
    private const string Key = "AutoUpdate";

    public static bool Value { get; set; } = true;

    public static void Initialize()
    {
        Value = SettingsService.GetBool(Key, true);
    }

    public static void Set(bool value)
    {
        Value = value;
        SettingsService.SetBool(Key, value);
    }
}