using System.IO;
using TheGuideToTheNewEden.Core.Services.CombatLog;
using TheGuideToTheNewEden.WPF.Services.CombatLog;

namespace TheGuideToTheNewEden.WPF.Services.Settings;

/// <summary>游戏日志相关设置（日志目录、频道文件有效天数、显示条数、战斗日志窗口/分类）。</summary>
public static class GameLogsSettingService
{
    public enum GameLogKey
    {
        EVELogsPath,
        EVELogsChannelDuration,
        MaxShowItems,
        CombatLogTtlSeconds,
        CombatLogCategories,
    }

    public static string EVELogsPathValue { get; set; } = string.Empty;
    public static int EVELogsChannelDurationValue { get; set; }
    public static int MaxShowItems { get; set; }

    /// <summary>战斗日志窗口/过期秒数（阶段 357，默认 5；可选 5/8/10）。</summary>
    public static int CombatLogTtlSeconds { get; set; } = 5;

    /// <summary>战斗日志启用的分类（空 = 全部）。</summary>
    public static string CombatLogCategories { get; set; } = string.Empty;

    public static void Initialize()
    {
        EVELogsPathValue = SettingsService.GetValue(GameLogKey.EVELogsPath.ToString()) ?? string.Empty;
        if (string.IsNullOrEmpty(EVELogsPathValue))
        {
            EVELogsPathValue = GetDefaultLogsPath();
        }

        EVELogsChannelDurationValue = SettingsService.GetInt(GameLogKey.EVELogsChannelDuration.ToString(), 7);
        MaxShowItems = SettingsService.GetInt(GameLogKey.MaxShowItems.ToString(), 100);

        CombatLogTtlSeconds = SettingsService.GetInt(GameLogKey.CombatLogTtlSeconds.ToString(), 5);
        if (CombatLogTtlSeconds <= 0)
        {
            CombatLogTtlSeconds = 5;
        }

        CombatLogCategories = SettingsService.GetValue(GameLogKey.CombatLogCategories.ToString()) ?? string.Empty;

        // 同步到状态中心（设置变化时也会调用 SyncCombatSettings）
        SyncCombatSettings();
    }

    /// <summary>把 TTL / 分类同步到 <see cref="CombatLogStateCenter"/>。</summary>
    public static void SyncCombatSettings()
    {
        var center = CombatLogStateCenter.Current;
        center.TtlSeconds = CombatLogTtlSeconds;
        var categories = ParseCategories(CombatLogCategories);
        // 空字符串 = 从未设置（默认全开）；"-" = 用户主动全部取消
        var enabled = string.IsNullOrWhiteSpace(CombatLogCategories)
            ? CombatCategories.All.Select(p => p.Key).ToList()
            : categories;
        center.SetEnabledCategories(enabled);
    }

    public static void SetCombatTtl(int seconds)
    {
        CombatLogTtlSeconds = seconds <= 0 ? 5 : seconds;
        SettingsService.SetValue(GameLogKey.CombatLogTtlSeconds.ToString(), CombatLogTtlSeconds.ToString());
        SyncCombatSettings();
    }

    public static void SetCombatCategories(IEnumerable<string> categories)
    {
        var list = categories.ToList();
        // 全部取消也要能记住：用 "-" 表示"一个都不显示"
        CombatLogCategories = list.Count == 0 ? "-" : string.Join(',', list);
        SettingsService.SetValue(GameLogKey.CombatLogCategories.ToString(), CombatLogCategories);
        SyncCombatSettings();
    }

    private static List<string> ParseCategories(string value)
        => string.IsNullOrWhiteSpace(value) || value == "-"
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(CombatCategories.IsKnown)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

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
            case GameLogKey.CombatLogTtlSeconds:
                if (int.TryParse(value, out var ttl))
                {
                    CombatLogTtlSeconds = ttl;
                    SyncCombatSettings();
                }
                else
                {
                    Core.Log.Error($"Set {key} invalid data type of {value}");
                }

                break;
            case GameLogKey.CombatLogCategories:
                CombatLogCategories = value;
                SyncCombatSettings();
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
