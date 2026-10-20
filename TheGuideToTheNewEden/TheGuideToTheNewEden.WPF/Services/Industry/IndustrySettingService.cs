using System.IO;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.WPF.Services.Industry;

/// <summary>工业制造计划的持久化参数（<c>Configs/IndustrySetting.json</c>）。</summary>
public sealed class IndustrySetting
{
    /// <summary>旧版只有星系的配置（仅作迁移用；新配置以 <see cref="Location"/> 为准）。</summary>
    public long SolarSystemId { get; set; } = 30000142;

    /// <summary>成本指数所用的位置（星域 / 星系 / 建筑）；默认山岳—吉他。</summary>
    public MarketLocation? Location { get; set; }

    /// <summary>设施税（百分比，10 = 10%）。</summary>
    public double FacilityTaxPercent { get; set; } = 10;

    public int DefaultME { get; set; } = 10;

    public int DefaultTE { get; set; } = 20;

    /// <summary>结构空间加成（高安 1.0 / 低安·00 1.9 / 虫洞 2.1）。</summary>
    public double SpaceModifier { get; set; } = 1.0;

    /// <summary>结构 rig 材料加成（百分比）。</summary>
    public double RigMeBonusPercent { get; set; }

    /// <summary>结构 rig 时间加成（百分比）。</summary>
    public double RigTeBonusPercent { get; set; }

    public bool UseAssets { get; set; } = true;

    public bool UseInProgressJobs { get; set; } = true;

    public bool UseOwnedBlueprints { get; set; } = true;
}

/// <summary>工业设置读写（WPF 版专有文件，与 WinUI 无共享）。</summary>
public static class IndustrySettingService
{
    private static readonly string Folder = Path.Combine(SettingsService.DataPath, "Configs");

    private static readonly string FilePath = Path.Combine(Folder, "IndustrySetting.json");

    public static IndustrySetting Load()
    {
        IndustrySetting? setting = null;
        if (File.Exists(FilePath))
        {
            try
            {
                var json = File.ReadAllText(FilePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    setting = JsonConvert.DeserializeObject<IndustrySetting>(json);
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        }

        setting ??= new IndustrySetting();
        Migrate(setting);
        return setting;
    }

    /// <summary>旧版只有 <see cref="IndustrySetting.SolarSystemId"/>：迁移成星系位置。</summary>
    private static void Migrate(IndustrySetting setting)
    {
        if (setting.Location is not null || setting.SolarSystemId <= 0)
        {
            return;
        }

        var system = MapSolarSystemService.Query((int)setting.SolarSystemId);
        if (system is not null)
        {
            setting.Location = new MarketLocation(system);
        }
    }

    public static void Save(IndustrySetting setting)
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
}
