using System.IO;

namespace TheGuideToTheNewEden.WPF.Services.Settings;

/// <summary>游戏日志相关设置（日志目录、频道文件有效天数、显示条数）。</summary>
public static class GameLogsSettingService
{
    public enum GameLogKey
    {
        EVELogsPath,
        EVELogsChannelDuration,
        MaxShowItems,
    }

    public static string EVELogsPathValue { get; set; } = string.Empty;
    public static int EVELogsChannelDurationValue { get; set; }
    public static int MaxShowItems { get; set; }

    public static void Initialize()
    {
        EVELogsPathValue = SettingsService.GetValue(GameLogKey.EVELogsPath.ToString()) ?? string.Empty;
        if (string.IsNullOrEmpty(EVELogsPathValue))
        {
            EVELogsPathValue = GetDefaultLogsPath();
        }

        EVELogsChannelDurationValue = SettingsService.GetInt(GameLogKey.EVELogsChannelDuration.ToString(), 7);
        MaxShowItems = SettingsService.GetInt(GameLogKey.MaxShowItems.ToString(), 100);
    }

    public static void SetValue(GameLogKey key, string value)
    {
        switch (key)
        {
            case GameLogKey.EVELogsPath:
                EVELogsPathValue = value;
                break;
            case GameLogKey.EVELogsChannelDuration:
                if (int.TryParse(value, out var duration))
                {
                    EVELogsChannelDurationValue = duration;
                }
                else
                {
                    Core.Log.Error($"Set {key} invalid data type of {value}");
                }

                break;
            case GameLogKey.MaxShowItems:
                if (int.TryParse(value, out var maxShowItems))
                {
                    MaxShowItems = maxShowItems;
                }
                else
                {
                    Core.Log.Error($"Set {key} invalid data type of {value}");
                }

                break;
        }

        SettingsService.SetValue(key.ToString(), value);
    }

    public static string GetDefaultLogsPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EVE", "logs");
    }

    /// <summary>聊天频道日志目录。</summary>
    public static string GetChatlogsPath() => Path.Combine(EVELogsPathValue, "Chatlogs");

    /// <summary>游戏日志目录。</summary>
    public static string GetGamelogsPath() => Path.Combine(EVELogsPathValue, "Gamelogs");
}