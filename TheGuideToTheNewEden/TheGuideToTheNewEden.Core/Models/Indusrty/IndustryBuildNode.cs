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
        /// <summary>作业安装费（EIV × 系统成本指数 × (1 + 设施税)）。</summary>
        public double JobCost { get; set; }
        public bool JobCostKnown { get; set; }

        /// <summary>市场参考单价（average_price）。</summary>
        public double ReferenceUnitPrice { get; set; }
        /// <summary>直接购买本节点数量所需的参考花费。</summary>
        public double ReferenceCost { get; set; }
        /// <summary>自建本节点（材料递归 + 安装费）的估算成本；采购节点等于其市场参考价。</summary>
        public double BuildCost { get; set; }
        public bool BuildCostKnown { get; set; }

        /// <summary>发明成功率（仅发明节点）。</summary>
        public double InventionProbability { get; set; }

        public double UnitVolume { get; set; }
        public double PackagedVolume { get; set; }
        /// <summary>本节点数量的打包体积（约等于运输体积）。</summary>
        public double TotalPackagedVolume { get { return PackagedVolume * QuantityNeeded; } }

        public List<IndustryBuildNode> Inputs { get; set; } = new List<IndustryBuildNode>();
    }
}
