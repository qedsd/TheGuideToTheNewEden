using System.IO;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.WPF.Services.Industry;

public sealed class IndustryRigSetting
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public double MePercent { get; set; }
    public double TePercent { get; set; }
}

/// <summary>工业制造计划的持久化参数（<c>Configs/IndustrySetting.json</c>）。</summary>
public sealed class IndustrySetting
{
    /// <summary>旧版只有星系的配置（仅作迁移用；新配置以 <see cref="Location"/> 为准）。</summary>
    public long SolarSystemId { get; set; } = 30000142;

    /// <summary>成本指数所用的位置（星域 / 星系 / 建筑）；默认山岳—吉他。</summary>
    public MarketLocation? Location { get; set; }

    /// <summary>价格/市场深度所用的市场位置；null 时沿用 <see cref="Location"/> 的星域。</summary>
    public MarketLocation? PriceLocation { get; set; }

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

    /// <summary>按物类的 rig 加成（优先于全局值）。</summary>
    public List<IndustryRigSetting> RigBonuses { get; set; } = new();

    /// <summary>选中的发明解密器 TypeID；null/0 表示不使用。</summary>
    public int? DecryptorTypeId { get; set; }

    /// <summary>手动发明尝试次数；null/0 = 自动。</summary>
    public int? InventionAttemptsOverride { get; set; }

    /// <summary>手动发明成功率（百分比）；null/0 = 自动。</summary>
    public double? InventionProbabilityOverridePercent { get; set; }

    /// <summary>手动每张发明 BPC 的可用流程数；null/0 = 自动。</summary>
    public int? InventionBpcRunsOverride { get; set; }

    /// <summary>反应是否套用 ME / 材料 rig。</summary>
    public bool ApplyMeToReactions { get; set; }

    public bool ApplyRigToReactions { get; set; }

    public bool UseAssets { get; set; } = true;

    public bool UseInProgressJobs { get; set; } = true;

    public bool UseOwnedBlueprints { get; set; } = true;

    public bool UseCharacterSkills { get; set; } = true;

    /// <summary>是否拉取整星域订单做市场深度/盈利（首次较慢）。</summary>
    public bool UseMarketDepth { get; set; }

    /// <summary>手动买/造决策（0 制造 / 1 采购），按产物 TypeID。</summary>
    public Dictionary<int, int> ManualDecisions { get; set; } = new();

    /// <summary>强制采购黑名单。</summary>
    public List<int> Blacklist { get; set; } = new();

    /// <summary>虚拟挂机库（TypeID → 数量）。</summary>
    public Dictionary<int, long> VirtualHangar { get; set; } = new();

    /// <summary>参与计算的库存仓库 Id（在「角色与市场」中选择）。</summary>
    public List<string> SelectedWarehouseIds { get; set; } = new();

    /// <summary>参与计算的角色 Id（在「角色与市场」中勾选）。</summary>
    public List<long> SelectedCharacterIds { get; set; } = new();
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
