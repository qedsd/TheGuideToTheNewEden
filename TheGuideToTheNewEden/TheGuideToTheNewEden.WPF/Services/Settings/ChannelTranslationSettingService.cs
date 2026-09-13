using System.IO;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models.Channel.Translation;

namespace TheGuideToTheNewEden.WPF.Services.Settings;

/// <summary>
/// 频道翻译设置：每个角色一份，持久化到 <c>Configs/ChannelTranslationSettings.json</c>。
/// **与 WinUI 版同路径同格式**（JSON 数组，字段名一致），两边配置可以互相沿用。
/// </summary>
public static class ChannelTranslationSettingService
{
    private static readonly string FilePath = Path.Combine(SettingsService.DataPath, "Configs", "ChannelTranslationSettings.json");

    private static readonly object Sync = new();
    private static Dictionary<string, ChannelTranslationSetting> _values = new(StringComparer.Ordinal);
    private static bool _loaded;

    public static void Initialize()
    {
        lock (Sync)
        {
            Load();
        }
    }

    private static void Load()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        try
        {
            if (!File.Exists(FilePath))
            {
                _values = new Dictionary<string, ChannelTranslationSetting>(StringComparer.Ordinal);
                return;
            }

            var json = File.ReadAllText(FilePath);
            var list = JsonConvert.DeserializeObject<List<ChannelTranslationSetting>>(json);
            _values = list is null
                ? new Dictionary<string, ChannelTranslationSetting>(StringComparer.Ordinal)
                : list.Where(p => !string.IsNullOrWhiteSpace(p.CharacterName)).ToDictionary(p => p.CharacterName, StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            _values = new Dictionary<string, ChannelTranslationSetting>(StringComparer.Ordinal);
        }
    }

    /// <summary>取该角色的配置（返回深拷贝，调用方可随意改，改完 <see cref="SetValue"/> 落盘）。</summary>
    public static ChannelTranslationSetting? GetValue(string characterName)
    {
        lock (Sync)
        {
            Load();
            return _values.TryGetValue(characterName, out var value) ? value.DepthClone<ChannelTranslationSetting>() : null;
        }
    }

    public static void SetValue(ChannelTranslationSetting value)
    {
        if (value is null || string.IsNullOrWhiteSpace(value.CharacterName))
        {
            return;
        }

        lock (Sync)
        {
            Load();
            _values[value.CharacterName] = value.DepthClone<ChannelTranslationSetting>();
            try
            {
                var folder = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                File.WriteAllText(FilePath, JsonConvert.SerializeObject(_values.Values));
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        }
    }
}
