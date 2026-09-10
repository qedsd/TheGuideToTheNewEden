using System.IO;

namespace TheGuideToTheNewEden.WPF.Services.Settings;

/// <summary>本地化数据库文件选择（Resources/Database/Local/*.db）。</summary>
public static class LocalDbSelectorService
{
    private const string Key = "LocalDbPath";

    public static string? Value { get; set; }

    public static void Initialize()
    {
        Value = Load();
        Core.Config.LocalDBPath = Value;
    }

    public static void Set(string? value)
    {
        Value = value;
        Core.Config.LocalDBPath = value;
        SettingsService.SetValue(Key, value);
    }

    private static string? Load()
    {
        var setting = SettingsService.GetValue(Key);
        return string.IsNullOrEmpty(setting) ? GetAll().FirstOrDefault() : setting;
    }

    /// <summary>获取所有本地数据库文件（完整路径）。</summary>
    public static List<string> GetAll()
    {
        var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Database", "Local");
        if (!Directory.Exists(folder))
        {
            return [];
        }

        return Directory.GetFiles(folder)
            .Where(p => p.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}