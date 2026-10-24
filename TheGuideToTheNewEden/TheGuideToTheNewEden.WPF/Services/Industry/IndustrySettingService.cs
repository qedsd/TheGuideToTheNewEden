using System.IO;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.WPF.Services.Industry;

public sealed class IndustryRigSetting
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    /// <summary>可选官方目标过滤器；0 = 只按分类。</summary>
    public int TargetFilterId { get; set; }
    public string TargetFilterName { get; set; } = string.Empty;
    public double MePercent { get; set; }
    public double TePercent { get; set; }
}

/// <summary>可保存/应用的建筑档案：设施税、空间加成与 rig 组合。</summary>
public sealed class IndustryBuildingProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public double FacilityTaxPercent { get; set; } = 10;
    public double SpaceModifier { get; set; } = 1.0;
    /// <summary>官方结构类型 ID；0 = 不套官方结构加成。</summary>
    public int StructureTypeId { get; set; }
    /// <summary>最终建筑材料加成（百分比，2 = -2% 材料）。</summary>
    public double MaterialBonusPercent { get; set; }
    /// <summary>最终建筑时间加成（百分比，2 = -2% 时间）。</summary>
    public double TimeBonusPercent { get; set; }
    /// <summary>最终建筑费用加成（百分比，2 = -2% 任务费用）。</summary>
    public double CostBonusPercent { get; set; }
    public double RigMeBonusPercent { get; set; }
    public double RigTeBonusPercent { get; set; }
    public List<IndustryRigSetting> RigBonuses { get; set; } = new();
}

/// <summary>工业制造计划的持久化参数（<c>Configs/IndustrySetting.json</c>）。</summary>
public sealed class IndustrySetting
{
    /// <summary>旧版只有星系的配置（仅作迁移用；新配置以 <see cref="Location"/> 为准）。</summary>
    public long SolarSystemId { get; set; } = 30000142;

    /// <summary>成本指数所用的位置（星域 / 星系 / 建筑）；默认山岳—吉他。</summary>
    public MarketLocation? Location { get; set; }

    /// <summary>价格/详细订单所用的市场位置；null 时沿用 <see cref="Location"/> 的星域。</summary>
    public MarketLocation? PriceLocation { get; set; }

    /// <summary>设施税（百分比，10 = 10%）。</summary>
    public double FacilityTaxPercent { get; set; } = 10;

    /// <summary>SCC 附加费（百分比，4 = 4%）。</summary>
    public double SccSurchargePercent { get; set; } = 4.0;

    /// <summary>行星产物出口税（POCO，百分比，5 = 5%）。</summary>
    public double PlanetTaxPercent { get; set; } = 5.0;

    /// <summary>官方结构类型 ID；0 = 不套官方结构加成。</summary>
    public int StructureTypeId { get; set; }

    /// <summary>最终建筑材料加成（百分比，2 = -2% 材料）。</summary>
    public double MaterialBonusPercent { get; set; }
    /// <summary>最终建筑时间加成（百分比，2 = -2% 时间）。</summary>
    public double TimeBonusPercent { get; set; }
    /// <summary>最终建筑费用加成（百分比，2 = -2% 任务费用）。</summary>
    public double CostBonusPercent { get; set; }

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

    /// <summary>可保存/应用的建筑档案（设施税 + 空间加成 + rig 组合）。</summary>
    public List<IndustryBuildingProfile> BuildingProfiles { get; set; } = new();

    /// <summary>选中的发明解密器 TypeID；null/0 表示不使用。</summary>
    public int? DecryptorTypeId { get; set; }

    /// <summary>手动发明尝试次数；null/0 = 自动。</summary>
    public int? InventionAttemptsOverride { get; set; }

    /// <summary>手动发明成功率（百分比）；null/0 = 自动。</summary>
    public double? InventionProbabilityOverridePercent { get; set; }

    /// <summary>手动每张发明 BPC 的可用流程数；null/0 = 自动。</summary>
    public int? InventionBpcRunsOverride { get; set; }

    /// <summary>反应材料是否套用 ME / 建筑改装件。</summary>
    public bool ApplyMeToReactions { get; set; }

    public bool ApplyRigToReactions { get; set; }

    public bool UseAssets { get; set; } = true;

    public bool UseInProgressJobs { get; set; } = true;

    /// <summary>使用角色自有蓝图：应用 BPO/BPC 的 ME/TE，并消耗 BPC 剩余流程。</summary>
    public bool UseOwnedBlueprints { get; set; } = true;

    public bool UseCharacterSkills { get; set; } = true;

    /// <summary>是否拉取整星域详细订单做盈利/买建对比（首次较慢）。</summary>
    public bool UseMarketDepth { get; set; } = true;

    /// <summary>是否生成可选的复制/研究蓝图任务（默认关闭，不进入 BOM 材料）。</summary>
    public bool IncludeBlueprintJobs { get; set; }

    /// <summary>出货方式：true = 直接卖给买单；false = 挂卖单（默认）。</summary>
    public bool SellImmediately { get; set; }
    /// <summary>销售税（百分比，3.6 = 3.6%）。</summary>
    public double SalesTaxPercent { get; set; } = 3.6;
    /// <summary>经纪人费（百分比，1 = 1%）；仅挂卖单时计入。</summary>
    public double BrokerFeePercent { get; set; } = 1.0;

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

    /// <summary>不依赖 ESI 的自定义角色；Id 使用负数，与真实角色区分。</summary>
    public List<IndustryCustomCharacter> CustomCharacters { get; set; } = new();

    /// <summary>设置迁移版本；用于把旧默认值一次性迁到当前默认值。</summary>
    public int PreferencesVersion { get; set; }
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

    /// <summary>旧版默认值迁移：详细订单默认开启、出货方式默认挂卖单。</summary>
    private static void Migrate(IndustrySetting setting)
    {
        var changed = false;
        if (setting.PreferencesVersion < 1)
        {
            setting.UseMarketDepth = true;
            setting.SellImmediately = false;
            setting.PreferencesVersion = 1;
            changed = true;
        }

        if (setting.Location is null && setting.SolarSystemId > 0)
        {
            var system = MapSolarSystemService.Query((int)setting.SolarSystemId);
            if (system is not null)
            {
                setting.Location = new MarketLocation(system);
                changed = true;
            }
        }

        if (changed)
        {
            Save(setting);
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
