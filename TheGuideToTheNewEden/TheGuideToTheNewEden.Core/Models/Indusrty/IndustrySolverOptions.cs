using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Indusrty
{
    /// <summary>一个制造目标（要造什么、造多少）。</summary>
    public class IndustryBuildTarget
    {
        public int TypeID { get; set; }
        public long Quantity { get; set; }

        public IndustryBuildTarget() { }
        public IndustryBuildTarget(int typeId, long quantity)
        {
            TypeID = typeId;
            Quantity = quantity;
        }
    }

    /// <summary>角色持有的蓝图聚合：BPO 无限使用；BPC 按剩余 runs 消耗。</summary>
    public sealed class IndustryBlueprintStock
    {
        public bool HasBpo { get; set; }
        public int BpoMaterialEfficiency { get; set; }
        public int BpoTimeEfficiency { get; set; }
        public long BpcRuns { get; set; }
        public int BpcCount { get; set; }
        public int BpcMaterialEfficiency { get; set; }
        public int BpcTimeEfficiency { get; set; }
        public int MaterialEfficiency => HasBpo ? BpoMaterialEfficiency : BpcMaterialEfficiency;
        public int TimeEfficiency => HasBpo ? BpoTimeEfficiency : BpcTimeEfficiency;
        public bool IsCopyOnly => !HasBpo && BpcRuns > 0;
    }

    /// <summary>按物类生效的 rig 加成。</summary>
    public class IndustryRigBonus
    {
        public int CategoryID { get; set; }
        /// <summary>可选：官方目标过滤器 ID；设置后优先按过滤器匹配分类/组。</summary>
        public int TargetFilterId { get; set; }
        /// <summary>材料加成（0.02 = 2%）。</summary>
        public double MeBonus { get; set; }
        /// <summary>时间加成（0.20 = 20%）。</summary>
        public double TeBonus { get; set; }
    }

    /// <summary>解密器对发明的影响（来自 typeDogma 1112/1113/1114/1124）。</summary>
    public class IndustryDecryptorInfo
    {
        public int TypeID { get; set; }
        public string Name { get; set; }
        public double ProbabilityMultiplier { get; set; } = 1.0;
        public int MeModifier { get; set; }
        public int TeModifier { get; set; }
        public int RunModifier { get; set; }
    }

    /// <summary>单物品的市场深度摘要。</summary>
    public class IndustryMarketStat
    {
        public int TypeID { get; set; }
        public double SellMin { get; set; }
        public double BuyMax { get; set; }
        public long SellVolume { get; set; }
        public long BuyVolume { get; set; }
        public double AveragePrice { get; set; }

        public double ReferencePrice => SellMin > 0 ? SellMin : AveragePrice;
        public double RevenuePrice => BuyMax > 0 ? BuyMax : ReferencePrice;
    }

    /// <summary>
    /// BOM 求解参数。<para>
    /// 默认口径：有蓝图就制造、没有就采购；ME 默认 10、TE 默认 20（可按产物 TypeID 覆盖）。
    /// 库存与价格/成本指数都是可选的——不提供时只做数量展开，不算安装费。
    /// </para>
    /// </summary>
    public class IndustrySolverOptions
    {
        public int DefaultME { get; set; } = 10;
        public int DefaultTE { get; set; } = 20;

        /// <summary>按产物 TypeID 覆盖 ME。</summary>
        public Dictionary<int, int> MEOverrides { get; set; } = new Dictionary<int, int>();
        /// <summary>按产物 TypeID 覆盖 TE。</summary>
        public Dictionary<int, int> TEOverrides { get; set; } = new Dictionary<int, int>();

        /// <summary>产物 TypeID → 角色持有的 BPO/BPC 状态；BPC runs 会在求解中消耗。</summary>
        public Dictionary<int, IndustryBlueprintStock> BlueprintStocks { get; set; } = new Dictionary<int, IndustryBlueprintStock>();

        /// <summary>强制采购的物品 TypeID（即使有蓝图）。</summary>
        public HashSet<int> Blacklist { get; set; } = new HashSet<int>();
        /// <summary>手动指定某物品制造/采购（优先级高于黑名单，低于深度/循环保护）。</summary>
        public Dictionary<int, IndustryDecision> Decisions { get; set; } = new Dictionary<int, IndustryDecision>();

        /// <summary>角色资产（TypeID → 数量），用于抵扣。</summary>
        public Dictionary<int, long> Assets { get; set; } = new Dictionary<int, long>();
        /// <summary>在制工业任务最终产出（TypeID → 数量），用于抵扣。</summary>
        public Dictionary<int, long> InProgressJobs { get; set; } = new Dictionary<int, long>();
        /// <summary>虚拟挂机库（TypeID → 数量）；求解过程中超产会写回这里。</summary>
        public Dictionary<int, long> Hangar { get; set; } = new Dictionary<int, long>();

        /// <summary>CCP 调整价（TypeID → adjusted_price），用于计算 EIV。</summary>
        public Dictionary<int, double> AdjustedPrices { get; set; } = new Dictionary<int, double>();
        /// <summary>市场深度摘要（TypeID → 最低卖价/最高买价/深度）；为空时回退平均价。</summary>
        public Dictionary<int, IndustryMarketStat> MarketStats { get; set; } = new Dictionary<int, IndustryMarketStat>();
        public double CostIndexManufacturing { get; set; }
        public double CostIndexReaction { get; set; }
        public double CostIndexInvention { get; set; }
        /// <summary>设施税（0.10 = 10%）。</summary>
        public double FacilityTax { get; set; } = 0.10;
        /// <summary>销售税（0.036 = 3.6%）。</summary>
        public double SalesTaxRate { get; set; }
        /// <summary>经纪人费（0.01 = 1%）；仅挂单卖出时计入。</summary>
        public double BrokerFeeRate { get; set; }
        /// <summary>true = 卖给买单（即时成交，不计经纪人费）；false = 挂卖单。</summary>
        public bool SellImmediately { get; set; } = true;

        /// <summary>角色技能等级（TypeID → 等级），用于发明成功率。为空则按 0 级计算。</summary>
        public Dictionary<int, int> CharacterSkills { get; set; } = new Dictionary<int, int>();

        /// <summary>排产用的角色能力（技能槽位、时间修正）；为空时调度器使用一个默认角色。</summary>
        public List<IndustryCharacterProfile> CharacterProfiles { get; set; } = new List<IndustryCharacterProfile>();

        /// <summary>结构空间加成（高安 1.0 / 低安·00 1.9 / 虫洞 2.1）。</summary>
        public double SpaceModifier { get; set; } = 1.0;

        /// <summary>全局 rig 材料加成（0.02 = 2%）；按物类的 <see cref="RigBonuses"/> 优先。</summary>
        public double RigMeBonus { get; set; }

        /// <summary>全局 rig 时间加成（0.20 = 20%）；按物类的 <see cref="RigBonuses"/> 优先。</summary>
        public double RigTeBonus { get; set; }

        /// <summary>按物类生效的 rig 加成；未命中时回退全局值。</summary>
        public List<IndustryRigBonus> RigBonuses { get; set; } = new List<IndustryRigBonus>();

        /// <summary>官方结构加成（活动 ID → 材料/时间/费用倍率）；为空表示 1.0。</summary>
        public Dictionary<int, IndustryActivityModifier> StructureModifiers { get; set; } = new Dictionary<int, IndustryActivityModifier>();

        /// <summary>反应是否套用 ME（默认否；反应没有 ME 研究）。</summary>
        public bool ApplyMeToReactions { get; set; }
        /// <summary>反应是否套用材料 rig（默认否；反应通常只有时间 rig）。</summary>
        public bool ApplyRigToReactions { get; set; }

        /// <summary>选中的发明解密器（TypeID）；null/0 表示不使用。</summary>
        public int? DecryptorTypeId { get; set; }

        /// <summary>手动指定发明尝试次数；null/<=0 表示按成功率自动估算。</summary>
        public int? InventionAttemptsOverride { get; set; }

        /// <summary>手动指定发明成功率（0-1）；null/<=0 表示按技能与解密器计算。</summary>
        public double? InventionProbabilityOverride { get; set; }

        /// <summary>手动指定每张发明 BPC 可支持的制造流程数；null/<=0 表示按 SDE + 解密器计算。</summary>
        public int? InventionBpcRunsOverride { get; set; }

        /// <summary>市场平均价（TypeID → average_price），用于买/建对比的回退值。</summary>
        public Dictionary<int, double> AveragePrices { get; set; } = new Dictionary<int, double>();

        /// <summary>递归深度上限（防坏数据无限递归）。</summary>
        public int MaxDepth { get; set; } = 64;
        /// <summary>是否为 T2 产物生成发明节点。</summary>
        public bool IncludeInvention { get; set; } = true;
    }
}
