using System.IO;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Models.CharacterScan;

namespace TheGuideToTheNewEden.WPF.Services.Settings;

/// <summary>
/// 频道统计的配置持久化：<c>%LocalAppData%\TheGuideToTheNewEden\Configs\ChannelScanSetting.json</c>（单个配置对象）。
/// 与 WinUI 版同路径同格式，配置互相沿用。
/// </summary>
public static class ChannelScanSettingService
{
    private static readonly string FilePath = Path.Combine(SettingsService.DataPath, "Configs", "ChannelScanSetting.json");

    /// <summary>读取配置；文件不存在或损坏时返回默认配置。</summary>
    public static ChannelScanConfig GetChannelScanConfig()
    {
        if (File.Exists(FilePath))
        {
            try
            {
                var json = File.ReadAllText(FilePath);
                if (!string.IsNullOrWhiteSpace(json)
                    && JsonConvert.DeserializeObject<ChannelScanConfig>(json) is { } config)
                {
                    return config;
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        }

        return new ChannelScanConfig();
    }

    public static void Save(ChannelScanConfig config)
    {
        try
        {
            var folder = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(FilePath, JsonConvert.SerializeObject(config));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }
}
