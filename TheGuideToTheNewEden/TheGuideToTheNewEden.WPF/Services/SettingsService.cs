using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Media;

namespace TheGuideToTheNewEden.WPF.Services;

/// <summary>
/// 应用设置存储：与 WinUI 版共用同一份键值字典文件
/// (%LocalAppData%\TheGuideToTheNewEden\Configs\settings.json)，键名保持一致，
/// 因此从 WinUI 迁移过来的配置可以直接沿用。
/// WPF 专有项（主题色、托盘、窗口位置）也存于同一文件。
/// </summary>
public static class SettingsService
{
    private static readonly object Sync = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public const string ThemeKey = "AppTheme";
    public const string AccentColorKey = "AppAccentColor";
    public const string LanguageKey = "AppPrimaryLanguage";
    public const string MinimizeToTrayKey = "AppMinimizeToTray";

    public const string WindowLeftKey = "WindowLeft";
    public const string WindowTopKey = "WindowTop";
    public const string WindowWidthKey = "WindowWidth";
    public const string WindowHeightKey = "WindowHeight";
    public const string WindowMaximizedKey = "WindowMaximized";

    public static string DataPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TheGuideToTheNewEden");

    private static string ConfigFolder => Path.Combine(DataPath, "Configs");

    private static string SettingsFile => Path.Combine(ConfigFolder, "settings.json");

    /// <summary>早期 WPF 版使用的独立设置文件，首次运行时导入后即可废弃。</summary>
    private static string LegacySettingsFile => Path.Combine(DataPath, "settings.wpf.json");

    public static Dictionary<string, string?> Values { get; private set; } = new(StringComparer.Ordinal);

    public static void Initialize()
    {
        Directory.CreateDirectory(ConfigFolder);

        if (File.Exists(SettingsFile))
        {
            Values = ReadDictionary(SettingsFile) ?? new Dictionary<string, string?>(StringComparer.Ordinal);
            return;
        }

        // 首次运行：若存在旧版 WPF 设置文件，导入其内容。
        Values = File.Exists(LegacySettingsFile)
            ? ReadDictionary(LegacySettingsFile) ?? new Dictionary<string, string?>(StringComparer.Ordinal)
            : new Dictionary<string, string?>(StringComparer.Ordinal);

        Save();
    }

    private static Dictionary<string, string?>? ReadDictionary(string file)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(File.ReadAllText(file));
        }
        catch
        {
            // 设置损坏时退回默认值，不阻塞启动。
            return null;
        }
    }

    public static void Save()
    {
        try
        {
            Dictionary<string, string?> snapshot;
            lock (Sync)
            {
                snapshot = new Dictionary<string, string?>(Values, StringComparer.Ordinal);
            }

            Directory.CreateDirectory(ConfigFolder);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(snapshot, JsonOptions));
        }
        catch
        {
            // 忽略持久化失败。
        }
    }

    public static string? GetValue(string key)
    {
        lock (Sync)
        {
            return Values.TryGetValue(key, out var value) ? value : null;
        }
    }

    /// <summary>写入并立即持久化（与 WinUI 版 SettingService.SetValue 行为一致）。</summary>
    public static void SetValue(string key, string? value)
    {
        lock (Sync)
        {
            Values[key] = value;
        }

        Save();
    }

    public static Task SetValueAsync(string key, string? value)
    {
        return Task.Run(() => SetValue(key, value));
    }

    // ---------- 类型化读写 ----------

    public static bool GetBool(string key, bool defaultValue = false)
    {
        var text = GetValue(key);
        return text is null ? defaultValue : bool.TryParse(text, out var value) ? value : defaultValue;
    }

    public static void SetBool(string key, bool value)
    {
        SetValue(key, value ? "true" : "false");
    }

    public static int GetInt(string key, int defaultValue = 0)
    {
        var text = GetValue(key);
        return text is null ? defaultValue : int.TryParse(text, out var value) ? value : defaultValue;
    }

    public static void SetInt(string key, int value)
    {
        SetValue(key, value.ToString(CultureInfo.InvariantCulture));
    }

    public static void SetDouble(string key, double value)
    {
        SetValue(key, value.ToString(CultureInfo.InvariantCulture));
    }

    public static double? GetDouble(string key)
    {
        var text = GetValue(key);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    public static void SetColor(string key, Color color)
    {
        SetValue(key, color.ToString());
    }

    public static Color? GetColor(string key)
    {
        var text = GetValue(key);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return (Color)ColorConverter.ConvertFromString(text);
        }
        catch
        {
            return null;
        }
    }
}