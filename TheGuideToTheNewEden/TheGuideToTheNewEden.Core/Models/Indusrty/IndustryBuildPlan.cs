using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.Indusrty
{
    /// <summary>一次 BOM 求解的结果：若干目标树 + 汇总。</summary>
    public class IndustryBuildPlan
    {
        public List<IndustryBuildNode> Targets { get; set; } = new List<IndustryBuildNode>();
        public IndustryPlanSummary Summary { get; set; }
    }

    /// <summary>BOM 汇总（按 TypeID 聚合）。</summary>
    public class IndustryPlanSummary
    {
        /// <summary>需采购的物品 → 数量。</summary>
        public Dictionary<int, long> BuyQuantities { get; } = new Dictionary<int, long>();
        /// <summary>需制造的产物 → 产出数量。</summary>
        public Dictionary<int, long> BuildQuantities { get; } = new Dictionary<int, long>();
        /// <summary>需制造的产物 → 总流程数。</summary>
        public Dictionary<int, int> BuildRuns { get; } = new Dictionary<int, int>();
        /// <summary>作业安装费合计（仅对已知者求和）。</summary>
        public double TotalJobCost { get; set; }
        public bool JobCostKnown { get; set; }
        /// <summary>采购物打包体积合计（m³）。</summary>
        public double TotalBuyVolume { get; set; }
        /// <summary>直接购买全部目标产物的参考花费（按平均价）。</summary>
        public double DirectBuyCost { get; set; }
        /// <summary>自建全部目标产物的估算成本（材料 + 安装费）。</summary>
        public double SelfBuildCost { get; set; }
        /// <summary>买/建对比是否可用（至少一个目标有平均价）。</summary>
        public bool ComparisonKnown { get; set; }

        internal void AddBuy(int typeId, long quantity, double packagedVolume)
        {
            if (quantity <= 0) return;
            BuyQuantities[typeId] = Get(BuyQuantities, typeId) + quantity;
            TotalBuyVolume += packagedVolume * quantity;
        }

        internal void AddBuild(int typeId, long produced, int runs, double jobCost, bool jobCostKnown)
        {
            if (produced > 0) BuildQuantities[typeId] = Get(BuildQuantities, typeId) + produced;
            if (runs > 0) BuildRuns[typeId] = Get(BuildRuns, typeId) + runs;
            if (jobCostKnown)
            {
                TotalJobCost += jobCost;
                JobCostKnown = true;
            }
        }

        private static long Get(Dictionary<int, long> dict, int key)
        {
            return dict.TryGetValue(key, out var v) ? v : 0;
        }

        private static int Get(Dictionary<int, int> dict, int key)
        {
            return dict.TryGetValue(key, out var v) ? v : 0;
        }
    }

    /// <summary>BOM 树汇总器。</summary>
    public static class IndustryPlanAggregator
    {
        public static IndustryPlanSummary Summarize(IEnumerable<IndustryBuildNode> targets)
        {
            var summary = new IndustryPlanSummary();
            if (targets == null) return summary;
            foreach (var target in targets)
            {
                if (target == null) continue;
                if (target.ReferenceCost > 0)
                {
                    summary.DirectBuyCost += target.ReferenceCost;
                    summary.ComparisonKnown = true;
                }
                summary.SelfBuildCost += target.BuildCost;
                Walk(target, summary);
            }
            return summary;
        }

        private static void Walk(IndustryBuildNode node, IndustryPlanSummary summary)
        {
            if (node == null) return;
            if (node.Decision == IndustryDecision.Buy)
            {
                summary.AddBuy(node.TypeID, node.QuantityToBuy, node.PackagedVolume);
            }
            else
            {
                summary.AddBuild(node.TypeID, node.QuantityProduced, node.Runs, node.JobCost, node.JobCostKnown);
            }
            if (node.Inputs == null) return;
            foreach (var input in node.Inputs)
            {
                Walk(input, summary);
            }
        }
    }
}
