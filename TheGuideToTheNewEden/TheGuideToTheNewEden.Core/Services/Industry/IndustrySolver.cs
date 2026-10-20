using System;
using System.Collections.Generic;
using System.Linq;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.Indusrty;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.Core.Services.Industry
{
    /// <summary>
    /// 递归 BOM 求解器（纯逻辑，只读 <see cref="IndustryDbService"/>）。
    /// <para>
    /// 一个求解器实例持有一份库存与缓存，<see cref="Solve"/> 多个目标时按顺序共享库存抵扣。
    /// 需要重新计算时请 new 一个新实例（或调用 <see cref="SolveNode"/> 前保证库存未被消耗）。
    /// </para>
    /// </summary>
    public sealed class IndustrySolver
    {
        private readonly IndustrySolverOptions _options;

        private readonly Dictionary<int, IndustryTypeInfo> _typeInfoCache = new Dictionary<int, IndustryTypeInfo>();
        private readonly Dictionary<int, Blueprint> _blueprintCache = new Dictionary<int, Blueprint>();
        private readonly Dictionary<int, (int BlueprintTypeID, int ActivityID)?> _producingCache = new Dictionary<int, (int, int)?>();
        private readonly Dictionary<long, List<BlueprintMaterial>> _materialCache = new Dictionary<long, List<BlueprintMaterial>>();
        private readonly Dictionary<long, List<BlueprintProduct>> _productCache = new Dictionary<long, List<BlueprintProduct>>();
        private readonly Dictionary<long, List<BlueprintSkill>> _skillCache = new Dictionary<long, List<BlueprintSkill>>();
        private readonly Dictionary<long, int> _activityTimeCache = new Dictionary<long, int>();

        private readonly HashSet<int> _stack = new HashSet<int>();
        private readonly Dictionary<int, long> _assets;
        private readonly Dictionary<int, long> _jobs;
        private readonly Dictionary<int, long> _hangar;

        public IndustrySolver(IndustrySolverOptions options)
        {
            _options = options ?? new IndustrySolverOptions();
            _assets = Copy(_options.Assets);
            _jobs = Copy(_options.InProgressJobs);
            _hangar = Copy(_options.Hangar);
        }

        private static Dictionary<int, long> Copy(Dictionary<int, long> source)
        {
            return source == null ? new Dictionary<int, long>() : new Dictionary<int, long>(source);
        }

        // ───────────────────────── 公开入口 ─────────────────────────

        /// <summary>求解一个或多个制造目标，返回计划树与汇总。</summary>
        public IndustryBuildPlan Solve(IEnumerable<IndustryBuildTarget> targets)
        {
            var plan = new IndustryBuildPlan();
            if (targets != null)
            {
                foreach (var target in targets)
                {
                    if (target == null || target.TypeID <= 0 || target.Quantity <= 0)
                    {
                        continue;
                    }
                    plan.Targets.Add(SolveNodeInternal(target.TypeID, target.Quantity, 0));
                }
            }
            plan.Summary = IndustryPlanAggregator.Summarize(plan.Targets);
            return plan;
        }

        /// <summary>求解单个目标（数量按件）。</summary>
        public IndustryBuildNode SolveNode(int typeId, long quantity)
        {
            if (typeId <= 0 || quantity <= 0)
            {
                return null;
            }
            return SolveNodeInternal(typeId, quantity, 0);
        }

        // ───────────────────────── 递归核心 ─────────────────────────

        private IndustryBuildNode SolveNodeInternal(int typeId, long quantityNeeded, int depth)
        {
            var info = GetTypeInfo(typeId);
            var node = new IndustryBuildNode
            {
                TypeID = typeId,
                TypeName = info != null && !string.IsNullOrEmpty(info.TypeName) ? info.TypeName : "Unknown [" + typeId + "]",
                UnitVolume = info != null ? info.Volume : 0,
                PackagedVolume = info != null ? info.PackagedVolume : 0,
                QuantityNeeded = quantityNeeded,
            };

            var decision = DetermineDecision(typeId, depth);
            if (decision == IndustryDecision.Build && GetProducingBlueprint(typeId) == null)
            {
                decision = IndustryDecision.Buy;
            }

            if (decision == IndustryDecision.Buy)
            {
                FillBuyNode(node);
            }
            else
            {
                BuildNode(node, depth);
            }
            return node;
        }

        private IndustryDecision DetermineDecision(int typeId, int depth)
        {
            if (depth >= _options.MaxDepth) return IndustryDecision.Buy;
            if (_stack.Contains(typeId)) return IndustryDecision.Buy;
            if (_options.Blacklist != null && _options.Blacklist.Contains(typeId)) return IndustryDecision.Buy;
            if (_options.Decisions != null && _options.Decisions.TryGetValue(typeId, out var decision)) return decision;
            return GetProducingBlueprint(typeId) != null ? IndustryDecision.Build : IndustryDecision.Buy;
        }

        private void FillBuyNode(IndustryBuildNode node)
        {
            node.Decision = IndustryDecision.Buy;
            long need = node.QuantityNeeded;
            node.QuantityFromAssets = ConsumeStock(_assets, node.TypeID, need);
            node.QuantityFromJobs = ConsumeStock(_jobs, node.TypeID, need - node.QuantityFromAssets);
            node.QuantityFromHangar = ConsumeStock(_hangar, node.TypeID, need - node.QuantityFromAssets - node.QuantityFromJobs);
            long covered = node.QuantityFromAssets + node.QuantityFromJobs + node.QuantityFromHangar;
            node.QuantityToBuy = need - covered;
            if (node.QuantityToBuy < 0) node.QuantityToBuy = 0;

            node.ReferenceUnitPrice = GetAveragePrice(node.TypeID);
            node.ReferenceCost = node.ReferenceUnitPrice > 0 ? node.ReferenceUnitPrice * node.QuantityToBuy : 0;
            node.BuildCost = node.ReferenceCost;
            node.BuildCostKnown = node.ReferenceUnitPrice > 0;
        }

        private void BuildNode(IndustryBuildNode node, int depth)
        {
            var link = GetProducingBlueprint(node.TypeID).Value;
            int blueprintTypeId = link.BlueprintTypeID;
            int activityId = link.ActivityID;

            node.Decision = IndustryDecision.Build;
            node.BlueprintTypeID = blueprintTypeId;
            node.ActivityID = activityId;
            node.ME = GetME(node.TypeID);
            node.TE = GetTE(node.TypeID);

            long need = node.QuantityNeeded;
            long effectiveNeed;
            if (depth == 0)
            {
                // 目标层：库存只做展示，不用来抵扣（用户明确要造这么多）。
                node.QuantityFromAssets = Math.Min(GetStock(_assets, node.TypeID), need);
                node.QuantityFromJobs = Math.Min(GetStock(_jobs, node.TypeID), need);
                node.QuantityFromHangar = Math.Min(GetStock(_hangar, node.TypeID), need);
                effectiveNeed = need;
            }
            else
            {
                node.QuantityFromAssets = ConsumeStock(_assets, node.TypeID, need);
                node.QuantityFromJobs = ConsumeStock(_jobs, node.TypeID, need - node.QuantityFromAssets);
                node.QuantityFromHangar = ConsumeStock(_hangar, node.TypeID, need - node.QuantityFromAssets - node.QuantityFromJobs);
                effectiveNeed = need - node.QuantityFromAssets - node.QuantityFromJobs - node.QuantityFromHangar;
                if (effectiveNeed < 0) effectiveNeed = 0;
            }

            int outputQty = GetOutputQuantity(blueprintTypeId, activityId, node.TypeID);
            node.Runs = IndustryCalculator.RunsNeeded(effectiveNeed, outputQty);
            node.QuantityProduced = (long)outputQty * node.Runs;
            node.QuantityToHangar = node.QuantityProduced - effectiveNeed;
            if (node.QuantityToHangar > 0)
            {
                AddStock(_hangar, node.TypeID, node.QuantityToHangar);
            }

            double rigMe = _options.RigMeBonus * _options.SpaceModifier;
            double rigTe = _options.RigTeBonus * _options.SpaceModifier;

            node.JobTimeSeconds = IndustryCalculator.JobTimeSeconds(
                GetActivityTime(blueprintTypeId, activityId), node.Runs, node.TE, rigTe);

            // 材料递归（用调用栈做环检测）。
            if (!_stack.Contains(node.TypeID))
            {
                _stack.Add(node.TypeID);
                var materials = GetMaterials(blueprintTypeId, activityId);
                foreach (var material in materials)
                {
                    long quantity = IndustryCalculator.ApplyME(material.Quantity, node.Runs, node.ME, rigMe);
                    var child = SolveNodeInternal(material.MaterialTypeID, quantity, depth + 1);
                    if (child != null)
                    {
                        node.Inputs.Add(child);
                    }
                }
                _stack.Remove(node.TypeID);
            }

            // 发明节点（T2 产物）。
            if (_options.IncludeInvention && activityId == IndustryActivity.Manufacturing)
            {
                var invention = TryBuildInventionNode(node, blueprintTypeId);
                if (invention != null)
                {
                    node.Inputs.Add(invention);
                }
            }

            // 作业安装费：EIV 用"套了 ME 的材料总量"，并排除发明子节点。
            var materialTotals = new List<KeyValuePair<int, long>>();
            foreach (var input in node.Inputs)
            {
                if (input.IsInvention) continue;
                materialTotals.Add(new KeyValuePair<int, long>(input.TypeID, input.QuantityNeeded));
            }
            double eiv = IndustryCalculator.EstimatedItemValue(materialTotals, _options.AdjustedPrices);
            double costIndex = activityId == IndustryActivity.Reaction
                ? _options.CostIndexReaction
                : _options.CostIndexManufacturing;
            if (eiv > 0 && costIndex > 0)
            {
                node.JobCost = IndustryCalculator.JobCost(eiv, costIndex, _options.FacilityTax);
                node.JobCostKnown = true;
            }

            // 买/建对比：参考价 = 直接买成品的平均价；自建成本 = 子材料 + 安装费。
            node.ReferenceUnitPrice = GetAveragePrice(node.TypeID);
            node.ReferenceCost = node.ReferenceUnitPrice > 0 ? node.ReferenceUnitPrice * node.QuantityNeeded : 0;
            double buildCost = 0;
            foreach (var input in node.Inputs)
            {
                buildCost += input.BuildCost;
            }
            if (node.JobCostKnown)
            {
                buildCost += node.JobCost;
            }
            node.BuildCost = buildCost;
            node.BuildCostKnown = true;
        }

        // ───────────────────────── 发明 ─────────────────────────

        private IndustryBuildNode TryBuildInventionNode(IndustryBuildNode productNode, int t2BlueprintTypeId)
        {
            var t1Blueprint = IndustryDbService.GetBlueprintTypeIdByProduct(t2BlueprintTypeId, IndustryActivity.Invention);
            if (!t1Blueprint.HasValue)
            {
                return null;
            }
            int t1BlueprintTypeId = t1Blueprint.Value;

            var inventionProducts = GetProducts(t1BlueprintTypeId, IndustryActivity.Invention);
            BlueprintProduct inventionProduct = null;
            foreach (var product in inventionProducts)
            {
                if (product.ProductTypeID == t2BlueprintTypeId)
                {
                    inventionProduct = product;
                    break;
                }
            }
            if (inventionProduct == null)
            {
                return null;
            }
            double baseProbability = inventionProduct.Probability.HasValue ? inventionProduct.Probability.Value : 0;
            if (baseProbability <= 0)
            {
                return null;
            }

            var t2Blueprint = GetBlueprintCached(t2BlueprintTypeId);
            int outputRuns = t2Blueprint != null ? t2Blueprint.MaxProductionLimit : 0;
            if (outputRuns <= 0) outputRuns = 1;
            int productRuns = productNode.Runs > 0 ? productNode.Runs : 1;
            int bpcsNeeded = IndustryCalculator.RunsNeeded(productRuns, outputRuns);

            var inventionSkills = GetSkills(t1BlueprintTypeId, IndustryActivity.Invention);
            var skillIds = new List<int>();
            foreach (var skill in inventionSkills)
            {
                skillIds.Add(skill.SkillTypeID);
            }
            double probability = IndustryCalculator.InventionProbability(baseProbability, skillIds, _options.CharacterSkills);
            int attempts = IndustryCalculator.InventionAttempts(bpcsNeeded, probability);

            var node = new IndustryBuildNode
            {
                TypeID = productNode.TypeID,
                TypeName = productNode.TypeName + " (Invention)",
                IsInvention = true,
                Decision = IndustryDecision.Build,
                ActivityID = IndustryActivity.Invention,
                BlueprintTypeID = t1BlueprintTypeId,
                QuantityNeeded = bpcsNeeded,
                QuantityProduced = bpcsNeeded,
                Runs = attempts,
                InventionProbability = probability,
                UnitVolume = productNode.UnitVolume,
                JobTimeSeconds = IndustryCalculator.JobTimeSeconds(
                    GetActivityTime(t1BlueprintTypeId, IndustryActivity.Invention), attempts, 0),
            };

            // 数据核心（每次尝试按蓝图材料消耗）。
            var datacores = GetMaterials(t1BlueprintTypeId, IndustryActivity.Invention);
            var eivMaterials = new List<KeyValuePair<int, long>>();
            foreach (var datacore in datacores)
            {
                long quantity = (long)datacore.Quantity * attempts;
                if (quantity <= 0) continue;
                eivMaterials.Add(new KeyValuePair<int, long>(datacore.MaterialTypeID, quantity));
                node.Inputs.Add(CreateBuyNode(datacore.MaterialTypeID, quantity));
            }

            // T1 蓝图（BPC）：自有 BPO 时不计入采购。
            long t1Owned = GetStock(_assets, t1BlueprintTypeId);
            long t1Need = t1Owned > 0 ? 0 : attempts;
            if (t1Need > 0)
            {
                node.Inputs.Add(CreateBuyNode(t1BlueprintTypeId, t1Need));
            }

            double eiv = IndustryCalculator.EstimatedItemValue(eivMaterials, _options.AdjustedPrices);
            if (eiv > 0 && _options.CostIndexInvention > 0)
            {
                node.JobCost = IndustryCalculator.JobCost(eiv, _options.CostIndexInvention, _options.FacilityTax);
                node.JobCostKnown = true;
            }

            double inventionCost = 0;
            foreach (var input in node.Inputs)
            {
                inventionCost += input.BuildCost;
            }
            if (node.JobCostKnown)
            {
                inventionCost += node.JobCost;
            }
            node.BuildCost = inventionCost;
            node.BuildCostKnown = true;
            return node;
        }

        private IndustryBuildNode CreateBuyNode(int typeId, long quantity)
        {
            var info = GetTypeInfo(typeId);
            var node = new IndustryBuildNode
            {
                TypeID = typeId,
                TypeName = info != null && !string.IsNullOrEmpty(info.TypeName) ? info.TypeName : "Unknown [" + typeId + "]",
                UnitVolume = info != null ? info.Volume : 0,
                PackagedVolume = info != null ? info.PackagedVolume : 0,
                QuantityNeeded = quantity,
            };
            FillBuyNode(node);
            return node;
        }

        // ───────────────────────── 查询缓存 ─────────────────────────

        private static long Key(int blueprintTypeId, int activityId)
        {
            return ((long)blueprintTypeId << 32) | (uint)activityId;
        }

        private IndustryTypeInfo GetTypeInfo(int typeId)
        {
            if (_typeInfoCache.TryGetValue(typeId, out var info)) return info;
            info = IndustryDbService.GetTypeInfo(typeId);
            _typeInfoCache[typeId] = info;
            return info;
        }

        private Blueprint GetBlueprintCached(int blueprintTypeId)
        {
            if (_blueprintCache.TryGetValue(blueprintTypeId, out var blueprint)) return blueprint;
            blueprint = IndustryDbService.GetBlueprint(blueprintTypeId);
            _blueprintCache[blueprintTypeId] = blueprint;
            return blueprint;
        }

        private (int BlueprintTypeID, int ActivityID)? GetProducingBlueprint(int typeId)
        {
            if (_producingCache.TryGetValue(typeId, out var cached)) return cached;
            (int, int)? result = null;
            var manufacturing = IndustryDbService.GetBlueprintTypeIdByProduct(typeId, IndustryActivity.Manufacturing);
            if (manufacturing.HasValue)
            {
                result = (manufacturing.Value, IndustryActivity.Manufacturing);
            }
            else
            {
                var reaction = IndustryDbService.GetBlueprintTypeIdByProduct(typeId, IndustryActivity.Reaction);
                if (reaction.HasValue)
                {
                    result = (reaction.Value, IndustryActivity.Reaction);
                }
            }
            _producingCache[typeId] = result;
            return result;
        }

        private List<BlueprintMaterial> GetMaterials(int blueprintTypeId, int activityId)
        {
            long key = Key(blueprintTypeId, activityId);
            if (_materialCache.TryGetValue(key, out var list)) return list;
            list = IndustryDbService.GetMaterials(blueprintTypeId, activityId);
            _materialCache[key] = list;
            return list;
        }

        private List<BlueprintProduct> GetProducts(int blueprintTypeId, int activityId)
        {
            long key = Key(blueprintTypeId, activityId);
            if (_productCache.TryGetValue(key, out var list)) return list;
            list = IndustryDbService.GetProducts(blueprintTypeId, activityId);
            _productCache[key] = list;
            return list;
        }

        private List<BlueprintSkill> GetSkills(int blueprintTypeId, int activityId)
        {
            long key = Key(blueprintTypeId, activityId);
            if (_skillCache.TryGetValue(key, out var list)) return list;
            list = IndustryDbService.GetSkills(blueprintTypeId, activityId);
            _skillCache[key] = list;
            return list;
        }

        private int GetActivityTime(int blueprintTypeId, int activityId)
        {
            long key = Key(blueprintTypeId, activityId);
            if (_activityTimeCache.TryGetValue(key, out var time)) return time;
            time = 0;
            var activities = IndustryDbService.GetActivities(blueprintTypeId);
            foreach (var activity in activities)
            {
                if (activity.ActivityID == activityId)
                {
                    time = activity.Time;
                    break;
                }
            }
            _activityTimeCache[key] = time;
            return time;
        }

        private int GetOutputQuantity(int blueprintTypeId, int activityId, int productTypeId)
        {
            var products = GetProducts(blueprintTypeId, activityId);
            int quantity = 0;
            foreach (var product in products)
            {
                if (product.ProductTypeID == productTypeId)
                {
                    quantity = product.Quantity;
                    break;
                }
            }
            if (quantity <= 0 && products.Count > 0)
            {
                quantity = products[0].Quantity;
            }
            return quantity > 0 ? quantity : 1;
        }

        private double GetAveragePrice(int typeId)
        {
            if (_options.AveragePrices != null && _options.AveragePrices.TryGetValue(typeId, out var price))
            {
                return price;
            }
            return 0;
        }

        private int GetME(int typeId)
        {
            if (_options.MEOverrides != null && _options.MEOverrides.TryGetValue(typeId, out var me))
            {
                return IndustryCalculator.ClampME(me);
            }
            return IndustryCalculator.ClampME(_options.DefaultME);
        }

        private int GetTE(int typeId)
        {
            if (_options.TEOverrides != null && _options.TEOverrides.TryGetValue(typeId, out var te))
            {
                return IndustryCalculator.ClampTE(te);
            }
            return IndustryCalculator.ClampTE(_options.DefaultTE);
        }

        // ───────────────────────── 库存工具 ─────────────────────────

        private static long GetStock(Dictionary<int, long> stock, int typeId)
        {
            return stock.TryGetValue(typeId, out var value) ? value : 0;
        }

        private static long ConsumeStock(Dictionary<int, long> stock, int typeId, long limit)
        {
            if (limit <= 0) return 0;
            if (!stock.TryGetValue(typeId, out var available) || available <= 0) return 0;
            long consumed = Math.Min(available, limit);
            stock[typeId] = available - consumed;
            return consumed;
        }

        private static void AddStock(Dictionary<int, long> stock, int typeId, long quantity)
        {
            if (quantity <= 0) return;
            stock[typeId] = GetStock(stock, typeId) + quantity;
        }
    }
}
