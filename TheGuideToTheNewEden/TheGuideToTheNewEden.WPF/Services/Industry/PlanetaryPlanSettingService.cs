using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Models.Market;

namespace TheGuideToTheNewEden.WPF.Services.Industry;

/// <summary>每个角色在行星计划里的勾选与产能参数（持久化用）。</summary>
public sealed class PlanetaryCharacterSetting
{
    public long CharacterId { get; set; }
    public bool Selected { get; set; } = true;
    public int CcuLevel { get; set; } = 5;
    public int MaxPlanets { get; set; } = 6;
}

/// <summary>行星开发的计划设置（自动保存到 <c>Configs/PlanetaryPlanSetting.json</c>）。</summary>
public sealed class PlanetaryPlanSetting
{
    public double PeriodDays { get; set; } = 7;
    public int ModeIndex { get; set; }
    public int CcuLevel { get; set; } = 5;
    public int MaxPlanets { get; set; } = 6;
    public double RadiusKm { get; set; }
    public double CycleDays { get; set; } = 4;
    public double PlanetTaxPercent { get; set; } = 5;
    public double SalesTaxPercent { get; set; } = 3.6;
    public double BrokerFeePercent { get; set; }
    public bool UseSetupCost { get; set; } = true;
    public bool UseCurrentSupply { get; set; } = true;
    public bool UseMarketPrices { get; set; } = true;
    public MarketLocation? PriceLocation { get; set; }
    public List<PlanetaryCharacterSetting> Characters { get; set; } = new();
}

/// <summary>
/// 行星开发设置持久化：与制造计划共用的 <c>IndustrySetting</c> 分开，
/// 页面上改「角色与产能 / 价格与税」等参数后由 VM 防抖自动保存。
/// </summary>
public static class PlanetaryPlanSettingService
{
    private static readonly string Folder = Path.Combine(SettingsService.DataPath, "Configs");
    private static readonly string FilePath = Path.Combine(Folder, "PlanetaryPlanSetting.json");

    /// <summary>读取设置；文件不存在时返回 null（调用方用默认值）。</summary>
    public static PlanetaryPlanSetting? Load()
    {
        if (!File.Exists(FilePath))
        {
            return null;
        }
        try
        {
            return JsonConvert.DeserializeObject<PlanetaryPlanSetting>(File.ReadAllText(FilePath));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }

    public static bool Save(PlanetaryPlanSetting setting)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(setting, Formatting.Indented));
            return true;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return false;
        }
    }
}
