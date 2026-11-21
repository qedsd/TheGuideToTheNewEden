using System.IO;
using TheGuideToTheNewEden.Core.Services.CombatLog;
using TheGuideToTheNewEden.WPF.Services.CombatLog;

namespace TheGuideToTheNewEden.WPF.Services.Settings;

/// <summary>游戏日志相关设置（日志目录、频道文件有效天数、显示条数、战斗日志过期时间/分类）。</summary>
public static class GameLogsSettingService
{
    public enum GameLogKey
    {
        EVELogsPath,
        EVELogsChannelDuration,
        MaxShowItems,
        CombatLogExpireSeconds,
        CombatLogCategories,
        CombatLogDisabledCategories,
        CombatLogStatusGreySeconds,
    }

    /// <summary>过期时间的合法范围（默认 60 秒，可手输任意秒数）。</summary>
    public const int MinCombatLogExpireSeconds = 1;
    public const int MaxCombatLogExpireSeconds = 86400;

    public static string EVELogsPathValue { get; set; } = string.Empty;
    public static int EVELogsChannelDurationValue { get; set; }
    public static int MaxShowItems { get; set; }

    /// <summary>
    /// 战斗日志的「过期时间」（秒，默认 60）：某分类超过该秒数没有新日志，卡片置灰并提示「已过期」；
    /// 行不会消失（只有手动清除 / 停止监控才清）。反跳 / ECM 不参与过期判断。
    /// </summary>
    public static int CombatLogExpireSeconds { get; set; } = 60;

    /// <summary>被用户关掉的分类（阶段 377：存"关闭列表"，空 = 全部开启，新分类自动开启）。</summary>
    public static string CombatLogDisabledCategories { get; set; } = string.Empty;

    /// <summary>状态类（电子战 / 反跳…）多久没有新日志就变灰（秒，默认 300 = 5 分钟）。</summary>
    public static int CombatLogStatusGreySeconds { get; set; } = 300;

    /// <summary>状态变灰时间的合法范围。</summary>
    public const int MinStatusGreySeconds = 10;
    public const int MaxStatusGreySeconds = 86400;

    public static void Initialize()
    {
        EVELogsPathValue = SettingsService.GetValue(GameLogKey.EVELogsPath.ToString()) ?? string.Empty;
        if (string.IsNullOrEmpty(EVELogsPathValue))
        {
            EVELogsPathValue = GetDefaultLogsPath();
        }

        EVELogsChannelDurationValue = SettingsService.GetInt(GameLogKey.EVELogsChannelDuration.ToString(), 7);
        MaxShowItems = SettingsService.GetInt(GameLogKey.MaxShowItems.ToString(), 100);

        CombatLogExpireSeconds = ClampExpire(
            SettingsService.GetInt(GameLogKey.CombatLogExpireSeconds.ToString(), 60));

        CombatLogDisabledCategories = SettingsService.GetValue(GameLogKey.CombatLogDisabledCategories.ToString()) ?? string.Empty;
        CombatLogStatusGreySeconds = Math.Clamp(
            SettingsService.GetInt(GameLogKey.CombatLogStatusGreySeconds.ToString(), 300),
            MinStatusGreySeconds,
            MaxStatusGreySeconds);

        // 同步到状态中心（设置变化时也会调用 SyncCombatSettings）
        SyncCombatSettings();
    }

    /// <summary>把过期时间 / 分类同步到 <see cref="CombatLogStateCenter"/>。</summary>
    public static void SyncCombatSettings()
    {
        var center = CombatLogStateCenter.Current;
        center.ExpireSeconds = CombatLogExpireSeconds;
        center.StatusGreySeconds = CombatLogStatusGreySeconds;
        var disabled = ParseCategories(CombatLogDisabledCategories).ToHashSet(StringComparer.OrdinalIgnoreCase);
        center.SetEnabledCategories(CombatCategories.All
            .Select(p => p.Key)
            .Where(p => !disabled.Contains(p)));
    }

    public static void SetCombatExpire(int seconds)
    {
        CombatLogExpireSeconds = ClampExpire(seconds);
        SettingsService.SetValue(GameLogKey.CombatLogExpireSeconds.ToString(), CombatLogExpireSeconds.ToString());
        SyncCombatSettings();
    }

    private static int ClampExpire(int seconds)
        => Math.Clamp(seconds, MinCombatLogExpireSeconds, MaxCombatLogExpireSeconds);

    /// <summary>保存"启用"的分类：内部换算成关闭列表，保证以后新增的分类默认是开启的。</summary>
    public static void SetCombatCategories(IEnumerable<string> categories)
    {
        var enabled = categories.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var disabled = CombatCategories.All
            .Select(p => p.Key)
            .Where(p => !enabled.Contains(p))
            .ToList();
        CombatLogDisabledCategories = string.Join(',', disabled);
        SettingsService.SetValue(GameLogKey.CombatLogDisabledCategories.ToString(), CombatLogDisabledCategories);
        SyncCombatSettings();
    }

    public static void SetCombatStatusGreySeconds(int seconds)
    {
        CombatLogStatusGreySeconds = Math.Clamp(seconds, MinStatusGreySeconds, MaxStatusGreySeconds);
        SettingsService.SetValue(
            GameLogKey.CombatLogStatusGreySeconds.ToString(),
            CombatLogStatusGreySeconds.ToString());
        SyncCombatSettings();
    }

    private static List<string> ParseCategories(string value)
        => string.IsNullOrWhiteSpace(value)
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
            case GameLogKey.CombatLogExpireSeconds:
                if (int.TryParse(value, out var expire))
                {
                    CombatLogExpireSeconds = ClampExpire(expire);
                    SyncCombatSettings();
                }
                else
                {
                    Core.Log.Error($"Set {key} invalid data type of {value}");
                }

                break;
            case GameLogKey.CombatLogDisabledCategories:
                CombatLogDisabledCategories = value ?? string.Empty;
                SyncCombatSettings();
                break;
            case GameLogKey.CombatLogStatusGreySeconds:
                if (int.TryParse(value, out var grey))
                {
                    CombatLogStatusGreySeconds = Math.Clamp(grey, MinStatusGreySeconds, MaxStatusGreySeconds);
                    SyncCombatSettings();
                }
                else
                {
                    Core.Log.Error($"Set {key} invalid data type of {value}");
                }

                break;
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
