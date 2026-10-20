using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Services.Industry
{
    /// <summary>
    /// 工业计算的纯函数口径（EVE 公式）。与数据库/UI 无关，便于单测与复算。
    /// </summary>
    public static class IndustryCalculator
    {
        public static int ClampME(int me)
        {
            if (me < 0) return 0;
            if (me > 10) return 10;
            return me;
        }

        public static int ClampTE(int te)
        {
            if (te < 0) return 0;
            if (te > 20) return 20;
            return te;
        }

        /// <summary>
        /// 套用 ME 后的材料数量：<c>max(runs, floor(qtyPerRun × runs × (1 − 0.01×ME)))</c>。
        /// 每流程至少保留 1 个（即总量不低于流程数）。
        /// </summary>
        public static long ApplyME(long quantityPerRun, int runs, int me)
            => ApplyME(quantityPerRun, runs, me, 0);

        /// <summary>
        /// 套用 ME 与结构 rig 材料加成：<c>floor(qtyPerRun × runs × (1 − 0.01×ME) × (1 − rigMe))</c>。
        /// 每流程至少保留 1 个（即总量不低于流程数）。<paramref name="rigMe"/> 已按空间加成放大。
        /// </summary>
        public static long ApplyME(long quantityPerRun, int runs, int me, double rigMe)
        {
            if (quantityPerRun <= 0 || runs <= 0)
            {
                return 0;
            }
            if (rigMe < 0) rigMe = 0;
            if (rigMe > 0.99) rigMe = 0.99;
            double factor = (1.0 - ClampME(me) * 0.01) * (1.0 - rigMe);
            long total = (long)Math.Floor(quantityPerRun * (double)runs * factor);
            return Math.Max(runs, total);
        }

        /// <summary>满足数量所需流程数：<c>ceil(quantity / outputPerRun)</c>。</summary>
        public static int RunsNeeded(long quantity, int outputPerRun)
        {
            if (quantity <= 0)
            {
                return 0;
            }
            if (outputPerRun <= 0)
            {
                outputPerRun = 1;
            }
            long runs = (quantity + outputPerRun - 1) / outputPerRun;
            return runs > int.MaxValue ? int.MaxValue : (int)runs;
        }

        /// <summary>作业耗时（秒）：<c>baseTime × (1 − 0.01×TE) × runs</c>。</summary>
        public static double JobTimeSeconds(int baseTimeSeconds, int runs, int te)
            => JobTimeSeconds(baseTimeSeconds, runs, te, 0);

        /// <summary>作业耗时（含结构 rig 时间加成）：<c>baseTime × (1 − 0.01×TE) × (1 − rigTe) × runs</c>。</summary>
        public static double JobTimeSeconds(int baseTimeSeconds, int runs, int te, double rigTe)
        {
            if (baseTimeSeconds <= 0 || runs <= 0)
            {
                return 0;
            }
            if (rigTe < 0) rigTe = 0;
            if (rigTe > 0.99) rigTe = 0.99;
            double factor = (1.0 - ClampTE(te) * 0.01) * (1.0 - rigTe);
            return baseTimeSeconds * factor * runs;
        }

        /// <summary>估算物品价值（EIV）：<c>Σ adjusted_price × 数量</c>。</summary>
        public static double EstimatedItemValue(IEnumerable<KeyValuePair<int, long>> materials, IDictionary<int, double> adjustedPrices)
        {
            if (materials == null || adjustedPrices == null || adjustedPrices.Count == 0)
            {
                return 0;
            }
            double sum = 0;
            foreach (var pair in materials)
            {
                if (pair.Value <= 0) continue;
                if (adjustedPrices.TryGetValue(pair.Key, out var price))
                {
                    sum += price * pair.Value;
                }
            }
            return sum;
        }

        /// <summary>作业安装费：<c>EIV × 系统成本指数 × (1 + 设施税)</c>。</summary>
        public static double JobCost(double eiv, double costIndex, double facilityTax)
        {
            if (eiv <= 0 || costIndex <= 0)
            {
                return 0;
            }
            if (facilityTax < 0) facilityTax = 0;
            return eiv * costIndex * (1.0 + facilityTax);
        }

        /// <summary>
        /// 发明成功率：<c>base × (1 + 0.01×加密学) × (1 + 0.1×Σ(数据核心技能/30)) × 解密器</c>。
        /// <paramref name="orderedSkillIds"/> 首个为加密学技能，其余为数据核心技能（与 SDE blueprintSkills 顺序一致）。
        /// </summary>
        public static double InventionProbability(double baseProbability, IEnumerable<int> orderedSkillIds, IDictionary<int, int> skills)
        {
            if (baseProbability <= 0)
            {
                return 0;
            }
            if (orderedSkillIds == null)
            {
                return baseProbability;
            }
            double enc = 0;
            double dcSum = 0;
            int index = 0;
            foreach (var skillId in orderedSkillIds)
            {
                double level = skills != null && skills.TryGetValue(skillId, out var lv) ? lv : 0;
                if (index == 0) enc = level; else dcSum += level;
                index++;
            }
            double probability = baseProbability * (1.0 + 0.01 * enc) * (1.0 + 0.1 * (dcSum / 30.0));
            if (probability > 1.0) probability = 1.0;
            return probability;
        }

        /// <summary>发明所需尝试次数：<c>ceil(所需 BPC 数 / 成功率)</c>。概率为 0 时按 1:1 估算。</summary>
        public static int InventionAttempts(int bpcsNeeded, double probability)
        {
            if (bpcsNeeded <= 0)
            {
                return 0;
            }
            if (probability <= 0)
            {
                return bpcsNeeded;
            }
            double attempts = Math.Ceiling(bpcsNeeded / probability);
            return attempts > int.MaxValue ? int.MaxValue : (int)attempts;
        }
    }
}
