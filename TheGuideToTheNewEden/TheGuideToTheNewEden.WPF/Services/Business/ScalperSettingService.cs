using System.IO;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Services.Business;

/// <summary>
/// 倒货设置的持久化：<c>Configs/ScalperSetting.json</c>（与 WinUI 版同文件，可互相读取）。
/// </summary>
public static class ScalperSettingService
{
    private static readonly string Folder = Path.Combine(SettingsService.DataPath, "Configs");

    private static readonly string FilePath = Path.Combine(Folder, "ScalperSetting.json");

    /// <summary>读取设置；文件不存在或损坏时返回默认设置。同时补齐旧版本缺失的 SolarSystemId。</summary>
    public static ScalperSetting Load()
    {
        ScalperSetting? setting = null;
        if (File.Exists(FilePath))
        {
            try
            {
                var json = File.ReadAllText(FilePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    setting = JsonConvert.DeserializeObject<ScalperSetting>(json);
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        }

        setting ??= new ScalperSetting();

        // 旧版本配置没有 SolarSystemId（跳数计算依赖它）
        if (setting.SourceMarketLocation is { SolarSystemId: 0 } source)
        {
            source.SolarSystemId = ResolveSolarSystemId(source) ?? 0;
        }

        if (setting.DestinationMarketLocation is { SolarSystemId: 0 } destination)
        {
            destination.SolarSystemId = ResolveSolarSystemId(destination) ?? 0;
        }

        return setting;
    }

    public static void Save(ScalperSetting setting)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(setting));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private static int? ResolveSolarSystemId(MarketLocation location) => location.Type switch
    {
        MarketLocationType.SolarSystem => (int)location.Id,
        MarketLocationType.Region => Core.Services.DB.MapSolarSystemService.QueryByRegionID((int)location.Id).FirstOrDefault()?.SolarSystemID,
        MarketLocationType.Structure => StructureService.GetStructure(location.Id)?.SolarSystemId,
        _ => null,
    };
}
