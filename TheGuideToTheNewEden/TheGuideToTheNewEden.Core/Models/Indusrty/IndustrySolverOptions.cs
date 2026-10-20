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
        public double CostIndexManufacturing { get; set; }
        public double CostIndexReaction { get; set; }
        public double CostIndexInvention { get; set; }
        /// <summary>设施税（0.10 = 10%）。</summary>
        public double FacilityTax { get; set; } = 0.10;

        /// <summary>角色技能等级（TypeID → 等级），用于发明成功率。为空则按 0 级计算。</summary>
        public Dictionary<int, int> CharacterSkills { get; set; } = new Dictionary<int, int>();

        /// <summary>结构空间加成（高安 1.0 / 低安·00 1.9 / 虫洞 2.1）。</summary>
        public double SpaceModifier { get; set; } = 1.0;

        /// <summary>结构 rig 材料加成（0.02 = 2%）；实际生效值 = 本值 × <see cref="SpaceModifier"/>。</summary>
        public double RigMeBonus { get; set; }

        /// <summary>结构 rig 时间加成（0.20 = 20%）；实际生效值 = 本值 × <see cref="SpaceModifier"/>。</summary>
        public double RigTeBonus { get; set; }

        /// <summary>市场平均价（TypeID → average_price），用于买/建对比。</summary>
        public Dictionary<int, double> AveragePrices { get; set; } = new Dictionary<int, double>();

        /// <summary>递归深度上限（防坏数据无限递归）。</summary>
        public int MaxDepth { get; set; } = 64;
        /// <summary>是否为 T2 产物生成发明节点。</summary>
        public bool IncludeInvention { get; set; } = true;
    }
}
