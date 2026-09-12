using System.IO;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.WPF.Services.Settings;

/// <summary>
/// 频道监控的每角色配置持久化：<c>%LocalAppData%\TheGuideToTheNewEden\Configs\ChannelMonitorSetting.json</c>。
/// 与 WinUI 版同路径同格式（List&lt;ChannelMonitorSetting&gt;，按 Name 角色名索引），配置互相沿用。
/// </summary>
public static class ChannelMonitorSettingService
{
    private static readonly string FilePath = Path.Combine(SettingsService.DataPath, "Configs", "ChannelMonitorSetting.json");

    private static Dictionary<string, ChannelMonitorSetting>? _values;

    private static Dictionary<string, ChannelMonitorSetting> Values
    {
        get
        {
            if (_values is null)
            {
                if (File.Exists(FilePath))
                {
                    try
                    {
                        var values = JsonConvert.DeserializeObject<List<ChannelMonitorSetting>>(File.ReadAllText(FilePath));
                        _values = values?.Where(p => !string.IsNullOrEmpty(p.Name)).ToDictionary(p => p.Name) ?? [];
                    }
                    catch (Exception ex)
                    {
                        Core.Log.Error(ex);
                        _values = [];
                    }
                }
                else
                {
                    _values = [];
                }
            }

            return _values;
        }
    }

    /// <summary>读取角色的监控配置；不存在返回 null（深拷贝）。</summary>
    public static ChannelMonitorSetting? GetValue(string characterName)
        => Values.TryGetValue(characterName, out var value) ? value.DepthClone<ChannelMonitorSetting>() : null;

    public static void SetValue(ChannelMonitorSetting value)
    {
        Values.Remove(value.Name);
        Values.Add(value.Name, value);
        Save();
    }

    public static void Save()
    {
        try
        {
            var folder = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(FilePath, JsonConvert.SerializeObject(Values.Values.ToList()));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }
}
