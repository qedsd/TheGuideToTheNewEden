using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Indusrty
{
    /// <summary>
    /// BOM 树的一个节点：要么是"制造"（带材料子节点），要么是"采购"（叶子）。
    /// 数量单位统一为"件"，<see cref="Runs"/> 为流程数。
    /// </summary>
    public class IndustryBuildNode
    {
        public int TypeID { get; set; }
        public string TypeName { get; set; }
        /// <summary>发明节点（数据核心 + T1 蓝图），用于 T2 产物。</summary>
        public bool IsInvention { get; set; }

        /// <summary>
        /// 该节点不计入成本：数量/流程照常展开，但成本字段被清零、不向上聚合。
        /// 来源有两种——用户手动"不计入成本"，或发明步骤的 T1 蓝图（由
        /// <see cref="IndustrySolverOptions.CountInventionBlueprintCost"/> 控制）。
        /// </summary>
        public bool CostIgnored { get; set; }

        public IndustryDecision Decision { get; set; }
        /// <summary>制造活动（1 制造 / 11 反应 / 8 发明）。</summary>
        public int ActivityID { get; set; }
        public int BlueprintTypeID { get; set; }

        /// <summary>本节点需要交付的数量（父节点向它要的量）。</summary>
        public long QuantityNeeded { get; set; }
        /// <summary>实际产出数量 = 每流程产出 × <see cref="Runs"/>。</summary>
        public long QuantityProduced { get; set; }
        public int Runs { get; set; }
        public int ME { get; set; }
        public int TE { get; set; }

        /// <summary>库存抵扣（资产 / 在制任务 / 虚拟挂机库）。</summary>
        public long QuantityFromAssets { get; set; }
        public long QuantityFromJobs { get; set; }
        public long QuantityFromHangar { get; set; }
        public long QuantityFromStock { get { return QuantityFromAssets + QuantityFromJobs + QuantityFromHangar; } }
        /// <summary>超额产出，存入虚拟挂机库供下游复用。</summary>
        public long QuantityToHangar { get; set; }
        /// <summary>采购节点：仍需购买的数量。</summary>
        public long QuantityToBuy { get; set; }

        /// <summary>作业耗时（秒，已套 TE）。</summary>
        public double JobTimeSeconds { get; set; }
        /// <summary>作业安装费（EIV × 系统成本指数 × 结构费用 + 设施税 + SCC）。</summary>
        public double JobCost { get; set; }
        public bool JobCostKnown { get; set; }
        /// <summary>行星产物 POCO 出口税（已计入 BuildCost/FullBuildCost）。</summary>
        public double PlanetTax { get; set; }

        // ── 行星开发：真实殖民地模型（阶段 276；仅 ActivityID == Planetary 且启用时填充） ──
        /// <summary>true = 该节点用了殖民地模型计算（时间/成本来自 PlanetaryPlanner，而不是简单配方估算）。</summary>
        public bool PlanetModeled { get; set; }
        /// <summary>需要几个殖民地（含 P0 提取与各级工厂殖民地）。</summary>
        public int PlanetColonies { get; set; }
        /// <summary>占用几个角色（殖民地按角色行星位上摊）。</summary>
        public int PlanetCharacterCount { get; set; }
        /// <summary>每天产出（目标产物）。</summary>
        public double PlanetOutputPerDay { get; set; }
        /// <summary>进口中间品的成本（仅 Import 模式 / 自给不满时可能非 0）。</summary>
        public double PlanetImportCost { get; set; }
        /// <summary>殖民地建造成本（CC 升级 + 设施），按 <c>CountSetupCost</c> 决定是否计入节点成本。</summary>
        public double PlanetSetupCost { get; set; }
        /// <summary>该节点目标数量的行星生产天数。</summary>
        public double PlanetProductionDays { get; set; }
        /// <summary>行星半径/行星类型是估算值（没有候选行星或半径缺失）。</summary>
        public bool PlanetEstimated { get; set; }

        /// <summary>市场参考单价（优先最低卖价，回退平均价）。</summary>
        public double ReferenceUnitPrice { get; set; }
        /// <summary>按实际采购量计算的参考花费（扣库存后的现金支出）。</summary>
        public double ReferenceCost { get; set; }
        /// <summary>按目标全量计算的参考花费（含库存部分，按市场价估值）。</summary>
        public double ReferenceCostFull { get; set; }
        /// <summary>自建本节点的现金支出（材料按扣库存后的采购量 + 安装费）。</summary>
        public double BuildCost { get; set; }
        /// <summary>自建本节点的全量成本（材料按全量市场价 + 安装费），用于买/建同口径对比。</summary>
        public double FullBuildCost { get; set; }
        public bool BuildCostKnown { get; set; }

        /// <summary>出售本节点数量的参考单价（优先最高买价，回退最低卖价/平均价）。</summary>
        public double RevenueUnitPrice { get; set; }
        public double Revenue { get; set; }
        /// <summary>扣除销售税/经纪人费后的收入。</summary>
        public double NetRevenue { get; set; }
        public double SalesTax { get; set; }
        public double BrokerFee { get; set; }
        public double Profit { get; set; }
        public double Margin { get; set; }

        public double SellMinPrice { get; set; }
        public double BuyMaxPrice { get; set; }
        public long SellVolume { get; set; }
        public long BuyVolume { get; set; }
        public long UnfilledQuantity { get; set; }
        public bool MarketVolumeLimited { get; set; }
        public List<IndustryMarketOrderLevel> SellLevels { get; set; } = new List<IndustryMarketOrderLevel>();
        public List<IndustryMarketOrderLevel> BuyLevels { get; set; } = new List<IndustryMarketOrderLevel>();

        /// <summary>发明成功率（仅发明节点）。</summary>
        public double InventionProbability { get; set; }
        /// <summary>发明产出的 BPC 可支持的制造流程数（仅发明节点）。</summary>
        public int InventionOutputRuns { get; set; }
        public int InventionOutputME { get; set; }
        public int InventionOutputTE { get; set; }
        public int DecryptorTypeID { get; set; }
        public string DecryptorName { get; set; }
        public double DecryptorProbabilityMultiplier { get; set; } = 1.0;

        public double UnitVolume { get; set; }
        public double PackagedVolume { get; set; }
        /// <summary>本节点数量的打包体积（约等于运输体积）。</summary>
        public double TotalPackagedVolume { get { return PackagedVolume * QuantityNeeded; } }

        /// <summary>角色持有 BPO（无限流程）；为 false 时看 BPC。</summary>
        public bool HasOwnedBpo { get; set; }
        /// <summary>是否有自有 BPC 或其剩余流程。</summary>
        public long BpcRunsAvailable { get; set; }
        public long BpcRunsUsed { get; set; }
        public long BpcRunsShortage { get; set; }
        public long BpcRunsRemaining { get; set; }
        public int BpcCount { get; set; }

        public List<IndustryBuildNode> Inputs { get; set; } = new List<IndustryBuildNode>();
    }
}
