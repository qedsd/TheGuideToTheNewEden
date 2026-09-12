using System.IO;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models.ChannelIntel;

namespace TheGuideToTheNewEden.WPF.Services.Settings;

/// <summary>
/// 频道预警的每角色配置持久化：<c>%LocalAppData%\TheGuideToTheNewEden\Configs\IntelSettings.json</c>。
/// 与 WinUI 版同一路径、同一格式（List&lt;ChannelIntelSetting&gt;，按 Listener 角色名索引），
/// 两个版本的预警配置可以直接互相沿用。
/// </summary>
public static class IntelSettingService
{
    private static readonly string FilePath = System.IO.Path.Combine(SettingsService.DataPath, "Configs", "IntelSettings.json");

    private static Dictionary<string, ChannelIntelSetting>? _values;

    private static Dictionary<string, ChannelIntelSetting> Values
    {
        get
        {
            if (_values is null)
            {
                if (File.Exists(FilePath))
                {
                    try
                    {
                        var values = JsonConvert.DeserializeObject<List<ChannelIntelSetting>>(File.ReadAllText(FilePath));
                        _values = values?.Where(p => !string.IsNullOrEmpty(p.Listener)).ToDictionary(p => p.Listener) ?? [];
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

    /// <summary>读取角色的预警配置；不存在返回 null（深拷贝，调用方可自由修改后再 SetValue 保存）。</summary>
    public static ChannelIntelSetting? GetValue(string characterName)
        => Values.TryGetValue(characterName, out var value) ? value.DepthClone<ChannelIntelSetting>() : null;

    public static void SetValue(ChannelIntelSetting value)
    {
        Values.Remove(value.Listener);
        Values.Add(value.Listener, value);
        Save();
    }

    public static void Save()
    {
        try
        {
            var folder = System.IO.Path.GetDirectoryName(FilePath);
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
