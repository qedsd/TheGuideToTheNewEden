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

    /// <summary>只写设置并同步 <c>Config.DefaultGameServer</c>。</summary>
    /// <remarks>
    /// 运行时切换请调用 <c>CoreInitializer.SwitchGameServer</c>：ESI 的 SSO/客户端在构造时已固化
    /// 所属服务器与凭据，只改这个值不会生效（国服会授权失败，角色列表也不会换）。
    /// </remarks>
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