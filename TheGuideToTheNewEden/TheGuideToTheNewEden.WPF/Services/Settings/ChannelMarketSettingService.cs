using System.IO;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models.ChannelMarket;

namespace TheGuideToTheNewEden.WPF.Services.Settings;

/// <summary>
/// 频道查价的每角色配置持久化：<c>%LocalAppData%\TheGuideToTheNewEden\Configs\ChannelMarketSettings.json</c>。
/// 与 WinUI 版同路径同格式（List&lt;ChannelMarketSetting&gt;，按 CharacterName 索引），配置互相沿用。
/// </summary>
public static class ChannelMarketSettingService
{
    private static readonly string FilePath = Path.Combine(SettingsService.DataPath, "Configs", "ChannelMarketSettings.json");

    private static Dictionary<string, ChannelMarketSetting>? _values;

    private static Dictionary<string, ChannelMarketSetting> Values
    {
        get
        {
            if (_values is null)
            {
                if (File.Exists(FilePath))
                {
                    try
                    {
                        var values = JsonConvert.DeserializeObject<List<ChannelMarketSetting>>(File.ReadAllText(FilePath));
                        _values = values?.Where(p => !string.IsNullOrEmpty(p.CharacterName)).ToDictionary(p => p.CharacterName) ?? [];
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

    public static ChannelMarketSetting? GetValue(string characterName)
        => Values.TryGetValue(characterName, out var value) ? value.DepthClone<ChannelMarketSetting>() : null;

    public static void SetValue(ChannelMarketSetting value)
    {
        Values.Remove(value.CharacterName);
        Values.Add(value.CharacterName, value);
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
