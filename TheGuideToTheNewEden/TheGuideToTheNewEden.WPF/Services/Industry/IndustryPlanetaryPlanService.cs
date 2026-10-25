using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Models.Planetary;

namespace TheGuideToTheNewEden.WPF.Services.Industry;

public sealed class PlanetarySavedPlan
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "行星计划";
    public DateTime Created { get; set; } = DateTime.Now;
    public DateTime Updated { get; set; } = DateTime.Now;
    public List<PlanetaryTarget> Targets { get; set; } = new();
    public double PeriodDays { get; set; } = 7;
    public PlanetaryPlanMode Mode { get; set; }
    public int CcuLevel { get; set; } = 5;
    public int DefaultMaxPlanets { get; set; } = 6;
    public double RadiusKm { get; set; }
    public double CycleDays { get; set; } = 4;
    public double PlanetTaxPercent { get; set; } = 5;
    public double SalesTaxPercent { get; set; } = 3.6;
    public double BrokerFeePercent { get; set; }
    public bool CountSetupCost { get; set; } = true;
    public bool UseCurrentSupply { get; set; } = true;
    public List<PlanetaryCharacterInput> Characters { get; set; } = new();
    /// <summary>保存时的完整计算结果快照；读取后不需要重新计算。</summary>
    public PlanetaryPlan? Result { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string UpdatedText => Updated.ToString("yyyy-MM-dd HH:mm");
}

public static class IndustryPlanetaryPlanService
{
    private static readonly string Folder = Path.Combine(SettingsService.DataPath, "Configs");
    private static readonly string FilePath = Path.Combine(Folder, "IndustryPlanetaryPlans.json");

    public static List<PlanetarySavedPlan> Load()
    {
        if (!File.Exists(FilePath)) return new List<PlanetarySavedPlan>();
        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonConvert.DeserializeObject<List<PlanetarySavedPlan>>(json) ?? new List<PlanetarySavedPlan>();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return new List<PlanetarySavedPlan>();
        }
    }

    public static void Save(IEnumerable<PlanetarySavedPlan> plans)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(plans.ToList(), Formatting.Indented));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }
}
