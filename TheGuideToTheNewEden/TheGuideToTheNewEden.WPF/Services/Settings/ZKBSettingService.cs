using System.IO;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Models.KB;

namespace TheGuideToTheNewEden.WPF.Services.Settings;

/// <summary>ZKB 实时流（过滤器、通知阈值等）设置。</summary>
public static class ZKBSettingService
{
    private static readonly string Path = System.IO.Path.Combine(
        SettingsService.DataPath, "Configs", "ZKBStreamConfig.json");

    private static ZKBStreamConfig? _setting;

    public static ZKBStreamConfig Setting
    {
        get
        {
            if (_setting is not null)
            {
                return _setting;
            }

            if (File.Exists(Path))
            {
                try
                {
                    _setting = JsonConvert.DeserializeObject<ZKBStreamConfig>(File.ReadAllText(Path));
                }
                catch
                {
                    _setting = null;
                }
            }

            return _setting ??= new ZKBStreamConfig();
        }
    }

    public static void Save()
    {
        var folder = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllText(Path, JsonConvert.SerializeObject(Setting));
    }
}