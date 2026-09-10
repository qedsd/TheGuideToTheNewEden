using TheGuideToTheNewEden.Core.Enums;

namespace TheGuideToTheNewEden.WPF.Services.Settings;

/// <summary>游戏服务器（Tranquility / Serenity）选择。</summary>
public static class GameServerSelectorService
{
    private const string Key = "GameServerType";

    public static GameServerType Value { get; set; } = GameServerType.Tranquility;

    public static void Initialize()
    {
        Value = Load();
        Core.Config.DefaultGameServer = Value;
    }

    public static void Set(GameServerType value)
    {
        Value = value;
        Core.Config.DefaultGameServer = value;
        SettingsService.SetValue(Key, value.ToString());
    }

    private static GameServerType Load()
    {
        var text = SettingsService.GetValue(Key);
        return !string.IsNullOrEmpty(text) && Enum.TryParse<GameServerType>(text, out var value)
            ? value
            : GameServerType.Tranquility;
    }
}