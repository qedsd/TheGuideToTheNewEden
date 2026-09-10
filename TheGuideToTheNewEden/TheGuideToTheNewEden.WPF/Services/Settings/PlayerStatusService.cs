namespace TheGuideToTheNewEden.WPF.Services.Settings;

/// <summary>玩家统计服务器 API 地址。</summary>
public static class PlayerStatusService
{
    private const string Key = "PlayerStatusAPI";

    public static string? Value { get; set; }

    public static void Initialize()
    {
        Value = SettingsService.GetValue(Key);
        Core.Config.PlayerStatusApi = Value;
    }

    public static void Set(string? api)
    {
        Value = api;
        Core.Config.PlayerStatusApi = api;
        SettingsService.SetValue(Key, api);
    }
}