using System.IO;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.WPF.Services.Settings;

/// <summary>按监听器 ID 保存的游戏日志监控配置。</summary>
public sealed class GameLogInfoSettingService
{
    private static GameLogInfoSettingService? _current;
    public static GameLogInfoSettingService Current => _current ??= new GameLogInfoSettingService();

    private static readonly string Path = System.IO.Path.Combine(
        SettingsService.DataPath, "Configs", "GameLogInfoSettings.json");

    private readonly Dictionary<int, GameLogSetting> _values = new();

    private GameLogInfoSettingService() => Load();

    /// <summary>强制重新解析文件（阶段 156：AI 改完配置后重启用）。</summary>
    public static void Reload() => Current.Load();

    private void Load()
    {
        _values.Clear();
        if (!File.Exists(Path))
        {
            return;
        }

        try
        {
            var values = JsonConvert.DeserializeObject<List<GameLogSetting>>(File.ReadAllText(Path));
            if (values is not null)
            {
                foreach (var value in values)
                {
                    _values[value.ListenerID] = value;
                }
            }
        }
        catch
        {
            _values.Clear();
        }
    }

    public static GameLogSetting? GetValue(int id)
    {
        return Current._values.TryGetValue(id, out var value) ? value.DepthClone<GameLogSetting>() : null;
    }

    public static void SetValue(GameLogSetting value)
    {
        Current._values[value.ListenerID] = value;
        Save();
    }

    public static void Save()
    {
        var folder = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllText(Path, JsonConvert.SerializeObject(Current._values.Select(p => p.Value)));
    }
}