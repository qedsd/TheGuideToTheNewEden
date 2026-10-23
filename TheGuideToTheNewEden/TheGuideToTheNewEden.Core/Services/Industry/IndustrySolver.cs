using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        private readonly CancellationToken _cancellationToken;

        private readonly Dictionary<int, IndustryTypeInfo> _typeInfoCache = new Dictionary<int, IndustryTypeInfo>();
        private readonly Dictionary<int, Blueprint> _blueprintCache = new Dictionary<int, Blueprint>();
        private readonly Dictionary<int, IndustryPlanetaryRecipe> _planetaryCache = new Dictionary<int, IndustryPlanetaryRecipe>();
        private List<IndustryTargetFilterInfo> _targetFilterCache;
        private bool _targetFilterLoaded;
        private readonly Dictionary<int, (int BlueprintTypeID, int ActivityID)?> _producingCache = new Dictionary<int, (int, int)?>();
        private readonly Dictionary<long, List<BlueprintMaterial>> _materialCache = new Dictionary<long, List<BlueprintMaterial>>();
        private readonly Dictionary<long, List<BlueprintProduct>> _productCache = new Dictionary<long, List<BlueprintProduct>>();
        private readonly Dictionary<long, List<BlueprintSkill>> _skillCache = new Dictionary<long, List<BlueprintSkill>>();
        private readonly Dictionary<long, int> _activityTimeCache = new Dictionary<long, int>();
        private readonly Dictionary<int, IndustryDecryptorInfo> _decryptorCache = new Dictionary<int, IndustryDecryptorInfo>();

        private readonly HashSet<int> _stack = new HashSet<int>();
        private readonly Dictionary<int, long> _assets;
        private readonly Dictionary<int, long> _jobs;
        private readonly Dictionary<int, long> _hangar;

        public IndustrySolver(IndustrySolverOptions options, CancellationToken cancellationToken = default)
        {
            _options = options ?? new IndustrySolverOptions();
            _cancellationToken = cancellationToken;
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
                    _cancellationToken.ThrowIfCancellationRequested();
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
            _cancellationToken.ThrowIfCancellationRequested();
            if (typeId <= 0 || quantityNeeded <= 0)
            {
                return null;
            }

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
            if (decision == IndustryDecision.Build
                && GetProducingBlueprint(typeId) == null
                && GetPlanetaryRecipe(typeId) == null)
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
            return GetProducingBlueprint(typeId) != null || GetPlanetaryRecipe(typeId) != null
                ? IndustryDecision.Build
                : IndustryDecision.Buy;
        }

        private void FillBuyNode(IndustryBuildNode node)
        {
            node.Decision = IndustryDecision.Buy;
            long need = node.QuantityNeeded;
            node.QuantityFromAssets = ConsumeStock(_assets, node.TypeID, need);
            node.QuantityFromJobs = ConsumeStock(_jobs, node.TypeID, need - node.QuantityFromAssets);
            node.QuantityFromHangar = ConsumeStock(_hangar, node.TypeID, need - node.QuantityFromAssets - node.QuantityFromJobs);
            long covered = node.QuantityFromAssets + node.QuantityFromJobs + node.QuantityFromHangar;
            node.QuantityToBuy = Math.Max(0, need - covered);

            double reference = GetReferencePrice(node.TypeID);
            node.ReferenceUnitPrice = reference;
            node.ReferenceCost = reference > 0 ? reference * node.QuantityToBuy : 0;
            node.ReferenceCostFull = reference > 0 ? reference * node.QuantityNeeded : 0;
            node.BuildCost = node.ReferenceCost;
            node.FullBuildCost = node.ReferenceCostFull;
            node.BuildCostKnown = reference > 0;
            ApplyMarketInfo(node);
        }

        private void BuildNode(IndustryBuildNode node, int depth)
        {
            var link = GetProducingBlueprint(node.TypeID);
            if (link is null)
            {
                BuildPlanetaryNode(node, depth);
                return;
            }
            int blueprintTypeId = link.Value.BlueprintTypeID;
            int activityId = link.Value.ActivityID;
            bool isReaction = activityId == IndustryActivity.Reaction;

            node.Decision = IndustryDecision.Build;
            node.BlueprintTypeID = blueprintTypeId;
            node.ActivityID = activityId;
            var blueprintStock = GetBlueprintStock(node.TypeID);
            node.HasOwnedBpo = blueprintStock?.HasBpo == true;
            node.BpcRunsAvailable = blueprintStock?.BpcRuns ?? 0;
            node.BpcRunsRemaining = node.BpcRunsAvailable;
            node.BpcCount = blueprintStock?.BpcCount ?? 0;

            PrimeBlueprint(blueprintTypeId);
            var info = GetTypeInfo(node.TypeID);
            int categoryId = info?.CategoryID ?? 0;

            // ── 库存抵扣 / 有效需求 ─────────────────────────────────────────────
            long need = node.QuantityNeeded;
            long effectiveNeed;
            bool forcedBuild = _options.Decisions != null
                && _options.Decisions.TryGetValue(node.TypeID, out var forcedDecision)
                && forcedDecision == IndustryDecision.Build;
            if (depth == 0 || forcedBuild)
            {
                // 目标层 / 强制制造：库存只做展示，不用来抵扣（用户明确要造这么多）。
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
                effectiveNeed = Math.Max(0, need - node.QuantityFromAssets - node.QuantityFromJobs - node.QuantityFromHangar);
            }

            // 需求已被库存完全覆盖：保留节点用于展示库存抵扣，不再排流程/展开 0 数量材料。
            if (effectiveNeed == 0)
            {
                node.Runs = 0;
                node.QuantityProduced = 0;
                node.QuantityToHangar = 0;
                node.ME = 0;
                node.TE = 0;
                node.JobTimeSeconds = 0;
                node.JobCost = 0;
                node.JobCostKnown = false;
                node.BuildCost = 0;
                node.FullBuildCost = 0;
                node.BuildCostKnown = true;
                double coveredReference = GetReferencePrice(node.TypeID);
                node.ReferenceUnitPrice = coveredReference;
                node.ReferenceCost = 0;
                node.ReferenceCostFull = coveredReference > 0 ? coveredReference * node.QuantityNeeded : 0;
                ApplyMarketInfo(node);
                return;
            }

            int outputQty = GetOutputQuantity(blueprintTypeId, activityId, node.TypeID);
            node.Runs = IndustryCalculator.RunsNeeded(effectiveNeed, outputQty);
            node.QuantityProduced = (long)outputQty * node.Runs;
            node.QuantityToHangar = node.QuantityProduced - effectiveNeed;
            if (node.QuantityToHangar > 0)
            {
                AddStock(_hangar, node.TypeID, node.QuantityToHangar);
            }

            if (!node.HasOwnedBpo && blueprintStock != null && blueprintStock.BpcRuns > 0)
            {
                long used = Math.Min(node.Runs, blueprintStock.BpcRuns);
                blueprintStock.BpcRuns -= used;
                node.BpcRunsUsed = used;
                node.BpcRunsRemaining = blueprintStock.BpcRuns;
                node.BpcRunsShortage = Math.Max(0, node.Runs - used);
            }

            // ── 发明节点（先算，才能把发明产出的 ME/TE 传给 T2 制造）─────────────
            // 有 BPO 或已有 BPC runs 时优先直接制造，不再同时叠加发明；
            // 只有完全没有可用蓝图时才走发明来源。
            IndustryBuildNode inventionNode = null;
            bool hasUsableBpc = node.BpcRunsAvailable > 0;
            if (_options.IncludeInvention
                && activityId == IndustryActivity.Manufacturing
                && !node.HasOwnedBpo
                && !hasUsableBpc)
            {
                inventionNode = TryBuildInventionNode(node, blueprintTypeId);
            }

            // ── ME / TE / rig ──────────────────────────────────────────────────
            bool hasOwnedMe = _options.MEOverrides != null && _options.MEOverrides.ContainsKey(node.TypeID);
            bool hasOwnedTe = _options.TEOverrides != null && _options.TEOverrides.ContainsKey(node.TypeID);
            node.ME = isReaction && !_options.ApplyMeToReactions ? 0 : GetME(node.TypeID);
            node.TE = GetTE(node.TypeID);
            if (inventionNode != null)
            {
                if (!hasOwnedMe) node.ME = inventionNode.InventionOutputME;
                if (!hasOwnedTe) node.TE = inventionNode.InventionOutputTE;
            }
            double rigMe = GetRigMe(categoryId, info?.GroupID ?? 0, isReaction);
            double rigTe = GetRigTe(categoryId, info?.GroupID ?? 0, isReaction);
            var structureModifier = GetStructureModifier(activityId);

            node.JobTimeSeconds = IndustryCalculator.JobTimeSeconds(
                GetActivityTime(blueprintTypeId, activityId), node.Runs, node.TE, rigTe, structureModifier.Time);

            // ── 材料递归（用调用栈做环检测）────────────────────────────────────
            if (!_stack.Contains(node.TypeID))
            {
                _stack.Add(node.TypeID);
                var materials = GetMaterials(blueprintTypeId, activityId);
                PrimeTypeInfos(materials.Select(p => p.MaterialTypeID));
                PrimeProducingLinks(materials.Select(p => p.MaterialTypeID));
                foreach (var material in materials)
                {
                    long quantity = IndustryCalculator.ApplyME(material.Quantity, node.Runs, node.ME, rigMe, structureModifier.Material);
                    var child = SolveNodeInternal(material.MaterialTypeID, quantity, depth + 1);
                    if (child != null)
                    {
                        node.Inputs.Add(child);
                    }
                }
                _stack.Remove(node.TypeID);
            }

            if (inventionNode != null)
            {
                node.Inputs.Add(inventionNode);
            }

            // ── 作业安装费：EIV 用"套了 ME 的材料总量"，并排除发明子节点 ──────────
            var materialTotals = new List<KeyValuePair<int, long>>();
            foreach (var input in node.Inputs)
            {
                if (input.IsInvention) continue;
                materialTotals.Add(new KeyValuePair<int, long>(input.TypeID, input.QuantityNeeded));
            }
            double eiv = IndustryCalculator.EstimatedItemValue(materialTotals, _options.AdjustedPrices);
            double costIndex = isReaction ? _options.CostIndexReaction : _options.CostIndexManufacturing;
            if (eiv > 0 && costIndex > 0)
            {
                node.JobCost = IndustryCalculator.JobCost(eiv, costIndex, _options.FacilityTax, structureModifier.Cost);
                node.JobCostKnown = true;
            }

            // ── 买/建成本：现金支出（扣库存） vs 全量成本（同口径对比）────────────
            double cash = 0;
            double full = 0;
            foreach (var input in node.Inputs)
            {
                cash += input.BuildCost;
                full += input.FullBuildCost;
            }
            if (node.JobCostKnown)
            {
                cash += node.JobCost;
                full += node.JobCost;
            }
            node.BuildCost = cash;
            node.FullBuildCost = full;
            node.BuildCostKnown = true;

            double reference = GetReferencePrice(node.TypeID);
            node.ReferenceUnitPrice = reference;
            node.ReferenceCostFull = reference > 0 ? reference * node.QuantityNeeded : 0;
            node.ReferenceCost = node.ReferenceCostFull;
            ApplyMarketInfo(node);
        }

        private void BuildPlanetaryNode(IndustryBuildNode node, int depth)
        {
            var recipe = GetPlanetaryRecipe(node.TypeID);
            if (recipe is null)
            {
                FillBuyNode(node);
                return;
            }

            node.Decision = IndustryDecision.Build;
            node.ActivityID = IndustryActivity.Planetary;
            node.BlueprintTypeID = 0;

            long need = node.QuantityNeeded;
            long effectiveNeed;
            bool forcedBuild = _options.Decisions != null
                && _options.Decisions.TryGetValue(node.TypeID, out var forcedDecision)
                && forcedDecision == IndustryDecision.Build;
            if (depth == 0 || forcedBuild)
            {
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
                effectiveNeed = Math.Max(0, need - node.QuantityFromAssets - node.QuantityFromJobs - node.QuantityFromHangar);
            }

            if (effectiveNeed == 0)
            {
                node.Runs = 0;
                node.QuantityProduced = 0;
                node.QuantityToHangar = 0;
                node.JobTimeSeconds = 0;
                node.JobCost = 0;
                node.JobCostKnown = false;
                node.BuildCost = 0;
                node.FullBuildCost = 0;
                node.BuildCostKnown = true;
                double coveredReference = GetReferencePrice(node.TypeID);
                node.ReferenceUnitPrice = coveredReference;
                node.ReferenceCost = 0;
                node.ReferenceCostFull = coveredReference > 0 ? coveredReference * node.QuantityNeeded : 0;
                ApplyMarketInfo(node);
                return;
            }

            int outputQty = (int)Math.Max(1, recipe.OutputQuantity);
            node.Runs = IndustryCalculator.RunsNeeded(effectiveNeed, outputQty);
            node.QuantityProduced = (long)outputQty * node.Runs;
            node.QuantityToHangar = node.QuantityProduced - effectiveNeed;
            if (node.QuantityToHangar > 0)
            {
                AddStock(_hangar, node.TypeID, node.QuantityToHangar);
            }

            node.ME = 0;
            node.TE = 0;
            node.JobTimeSeconds = Math.Max(0, recipe.CycleTime) * (double)node.Runs;
            node.JobCost = 0;
            node.JobCostKnown = false;

            if (!_stack.Contains(node.TypeID))
            {
                _stack.Add(node.TypeID);
                foreach (var input in recipe.Inputs)
                {
                    long quantity = input.Quantity * node.Runs;
                    if (quantity <= 0) continue;
                    var child = SolveNodeInternal(input.TypeID, quantity, depth + 1);
                    if (child != null)
                    {
                        node.Inputs.Add(child);
                    }
                }
                _stack.Remove(node.TypeID);
            }

            double cash = 0;
            double full = 0;
            foreach (var input in node.Inputs)
            {
                cash += input.BuildCost;
                full += input.FullBuildCost;
            }
            node.BuildCost = cash;
            node.FullBuildCost = full;
            node.BuildCostKnown = true;

            double reference = GetReferencePrice(node.TypeID);
            node.ReferenceUnitPrice = reference;
            node.ReferenceCost = reference > 0 ? reference * node.QuantityNeeded : 0;
            node.ReferenceCostFull = node.ReferenceCost;
            ApplyMarketInfo(node);
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
            PrimeBlueprint(t1BlueprintTypeId);

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
            double baseProbability = inventionProduct.Probability ?? 0;
            if (baseProbability <= 0)
            {
                return null;
            }

            // 发明产物的 Quantity 是 BPC 可支持的制造流程数；比 T2 蓝图 MaxProductionLimit 更贴近新 SDE 语义。
            int baseOutputRuns = inventionProduct.Quantity > 0 ? inventionProduct.Quantity : 1;

            var decryptor = ResolveDecryptor();
            int decryptorRunModifier = decryptor?.RunModifier ?? 0;
            double decryptorMultiplier = decryptor?.ProbabilityMultiplier ?? 1.0;
            int decryptorMe = decryptor?.MeModifier ?? 0;
            int decryptorTe = decryptor?.TeModifier ?? 0;

            int outputRuns = _options.InventionBpcRunsOverride.HasValue && _options.InventionBpcRunsOverride.Value > 0
                ? _options.InventionBpcRunsOverride.Value
                : Math.Max(1, baseOutputRuns + decryptorRunModifier);
            int outputMe = IndustryCalculator.ClampME(2 + decryptorMe);
            int outputTe = IndustryCalculator.ClampTE(4 + decryptorTe);

            int productRuns = productNode.Runs > 0 ? productNode.Runs : 1;
            int bpcsNeeded = IndustryCalculator.RunsNeeded(productRuns, outputRuns);

            var inventionSkills = GetSkills(t1BlueprintTypeId, IndustryActivity.Invention);
            int encryptionSkillId = 0;
            var datacoreSkillIds = new List<int>();
            foreach (var skill in inventionSkills)
            {
                var name = GetTypeInfo(skill.SkillTypeID)?.TypeName ?? string.Empty;
                if (encryptionSkillId == 0 && name.IndexOf("Encryption", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    encryptionSkillId = skill.SkillTypeID;
                }
                else
                {
                    datacoreSkillIds.Add(skill.SkillTypeID);
                }
            }
            if (encryptionSkillId == 0 && inventionSkills.Count > 0)
            {
                // 兼容：技能名缺失时退回"第一个技能"，并记一次警告。
                encryptionSkillId = inventionSkills[0].SkillTypeID;
                datacoreSkillIds.Remove(encryptionSkillId);
                Log.Warn($"发明蓝图 {t1BlueprintTypeId} 未识别到加密学技能，回退首个技能 {encryptionSkillId}");
            }

            double probability = _options.InventionProbabilityOverride.HasValue && _options.InventionProbabilityOverride.Value > 0
                ? Math.Clamp(_options.InventionProbabilityOverride.Value, 0.0001, 1.0)
                : IndustryCalculator.InventionProbability(
                    baseProbability, encryptionSkillId, datacoreSkillIds, _options.CharacterSkills, decryptorMultiplier);
            int attempts = _options.InventionAttemptsOverride.HasValue && _options.InventionAttemptsOverride.Value > 0
                ? _options.InventionAttemptsOverride.Value
                : IndustryCalculator.InventionAttempts(bpcsNeeded, probability);
            if (attempts <= 0) attempts = 1;

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
                InventionOutputRuns = outputRuns,
                InventionOutputME = outputMe,
                InventionOutputTE = outputTe,
                DecryptorTypeID = decryptor?.TypeID ?? 0,
                DecryptorName = decryptor?.Name,
                DecryptorProbabilityMultiplier = decryptorMultiplier,
                UnitVolume = productNode.UnitVolume,
                PackagedVolume = productNode.PackagedVolume,
                JobTimeSeconds = IndustryCalculator.JobTimeSeconds(
                    GetActivityTime(t1BlueprintTypeId, IndustryActivity.Invention), attempts, 0, 0, 1.0),
            };
            var inventionModifier = GetStructureModifier(IndustryActivity.Invention);
            node.JobTimeSeconds = IndustryCalculator.JobTimeSeconds(
                GetActivityTime(t1BlueprintTypeId, IndustryActivity.Invention), attempts, 0, 0, inventionModifier.Time);

            // 数据核心（每次尝试按蓝图材料消耗）。
            var datacores = GetMaterials(t1BlueprintTypeId, IndustryActivity.Invention);
            PrimeTypeInfos(datacores.Select(p => p.MaterialTypeID));
            foreach (var datacore in datacores)
            {
                long quantity = (long)Math.Floor(datacore.Quantity * (double)attempts * inventionModifier.Material);
                if (quantity <= 0) continue;
                node.Inputs.Add(CreateBuyNode(datacore.MaterialTypeID, quantity));
            }

            // T1 蓝图（BPC）：自有 T1 蓝图时不计入采购（简化：按 BPO 处理）。
            long t1Owned = GetStock(_assets, t1BlueprintTypeId);
            long t1Need = t1Owned > 0 ? 0 : attempts;
            if (t1Need > 0)
            {
                node.Inputs.Add(CreateBuyNode(t1BlueprintTypeId, t1Need));
            }

            // 解密器：每次尝试消耗 1 个。
            if (decryptor != null && decryptor.TypeID > 0 && attempts > 0)
            {
                node.Inputs.Add(CreateBuyNode(decryptor.TypeID, attempts));
            }

            var eivMaterials = node.Inputs
                .Where(p => p.TypeID > 0 && p.QuantityNeeded > 0)
                .Select(p => new KeyValuePair<int, long>(p.TypeID, p.QuantityNeeded))
                .ToList();
            double eiv = IndustryCalculator.EstimatedItemValue(eivMaterials, _options.AdjustedPrices);
            if (eiv > 0 && _options.CostIndexInvention > 0)
            {
                node.JobCost = IndustryCalculator.JobCost(eiv, _options.CostIndexInvention, _options.FacilityTax, inventionModifier.Cost);
                node.JobCostKnown = true;
            }

            double cash = 0;
            double full = 0;
            foreach (var input in node.Inputs)
            {
                cash += input.BuildCost;
                full += input.FullBuildCost;
            }
            if (node.JobCostKnown)
            {
                cash += node.JobCost;
                full += node.JobCost;
            }
            node.BuildCost = cash;
            node.FullBuildCost = full;
            node.BuildCostKnown = true;
            return node;
        }

        private IndustryDecryptorInfo ResolveDecryptor()
        {
            int? typeId = _options.DecryptorTypeId;
            if (!typeId.HasValue || typeId.Value <= 0)
            {
                return null;
            }
            if (_decryptorCache.TryGetValue(typeId.Value, out var cached))
            {
                return cached;
            }
            var info = IndustryDbService.GetDecryptorInfo(typeId.Value);
            _decryptorCache[typeId.Value] = info;
            return info;
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

        // ───────────────────────── 市场信息 ─────────────────────────

        private void ApplyMarketInfo(IndustryBuildNode node)
        {
            var stat = GetMarketStat(node.TypeID);
            node.SellMinPrice = stat?.SellMin ?? 0;
            node.BuyMaxPrice = stat?.BuyMax ?? 0;
            node.SellVolume = stat?.SellVolume ?? 0;
            node.BuyVolume = stat?.BuyVolume ?? 0;
            node.RevenueUnitPrice = GetRevenuePrice(node.TypeID);
            long revenueQuantity = node.Decision == IndustryDecision.Build && node.QuantityProduced > 0
                ? node.QuantityProduced
                : node.QuantityNeeded;
            node.Revenue = node.RevenueUnitPrice > 0 ? node.RevenueUnitPrice * revenueQuantity : 0;
            double salesTaxRate = Math.Clamp(_options.SalesTaxRate, 0, 1);
            double brokerFeeRate = _options.SellImmediately ? 0 : Math.Clamp(_options.BrokerFeeRate, 0, 1);
            node.SalesTax = node.Revenue * salesTaxRate;
            node.BrokerFee = node.Revenue * brokerFeeRate;
            node.NetRevenue = Math.Max(0, node.Revenue - node.SalesTax - node.BrokerFee);
            node.Profit = node.NetRevenue - node.FullBuildCost;
            node.Margin = node.NetRevenue > 0 ? node.Profit / node.NetRevenue : 0;
        }

        private IndustryMarketStat GetMarketStat(int typeId)
        {
            return _options.MarketStats != null && _options.MarketStats.TryGetValue(typeId, out var stat) ? stat : null;
        }

        private double GetReferencePrice(int typeId)
        {
            var stat = GetMarketStat(typeId);
            if (stat != null && stat.ReferencePrice > 0)
            {
                return stat.ReferencePrice;
            }
            return GetAveragePrice(typeId);
        }

        private double GetRevenuePrice(int typeId)
        {
            var stat = GetMarketStat(typeId);
            if (stat != null)
            {
                if (_options.SellImmediately && stat.BuyMax > 0)
                {
                    return stat.BuyMax;
                }
                if (!_options.SellImmediately && stat.SellMin > 0)
                {
                    return stat.SellMin;
                }
                if (stat.RevenuePrice > 0)
                {
                    return stat.RevenuePrice;
                }
            }
            return GetReferencePrice(typeId);
        }

        private double GetAveragePrice(int typeId)
        {
            if (_options.AveragePrices != null && _options.AveragePrices.TryGetValue(typeId, out var price))
            {
                return price;
            }
            return 0;
        }

        // ───────────────────────── 查询缓存 / 批量预取 ─────────────────────────

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

        private void PrimeTypeInfos(IEnumerable<int> typeIds)
        {
            if (typeIds == null) return;
            var missing = typeIds.Where(p => p > 0 && !_typeInfoCache.ContainsKey(p)).Distinct().ToList();
            if (missing.Count == 0) return;
            foreach (var info in IndustryDbService.GetTypeInfos(missing))
            {
                _typeInfoCache[info.TypeID] = info;
            }
        }

        private void PrimeProducingLinks(IEnumerable<int> typeIds)
        {
            if (typeIds == null) return;
            var missing = typeIds.Where(p => p > 0 && !_producingCache.ContainsKey(p)).Distinct().ToList();
            if (missing.Count == 0) return;
            var manufacturing = IndustryDbService.GetBlueprintTypeIdByProducts(missing, IndustryActivity.Manufacturing);
            var reaction = IndustryDbService.GetBlueprintTypeIdByProducts(missing, IndustryActivity.Reaction);
            foreach (var id in missing)
            {
                if (manufacturing.TryGetValue(id, out var bp))
                {
                    _producingCache[id] = (bp, IndustryActivity.Manufacturing);
                }
                else if (reaction.TryGetValue(id, out var bp2))
                {
                    _producingCache[id] = (bp2, IndustryActivity.Reaction);
                }
                else
                {
                    _producingCache[id] = null;
                }
            }
        }

        private void PrimeBlueprint(int blueprintTypeId)
        {
            if (blueprintTypeId <= 0) return;
            if (!_blueprintCache.ContainsKey(blueprintTypeId))
            {
                _blueprintCache[blueprintTypeId] = IndustryDbService.GetBlueprint(blueprintTypeId);
            }
            foreach (var activity in IndustryDbService.GetActivities(blueprintTypeId))
            {
                _activityTimeCache[Key(blueprintTypeId, activity.ActivityID)] = activity.Time;
            }
        }

        private Blueprint GetBlueprintCached(int blueprintTypeId)
        {
            if (_blueprintCache.TryGetValue(blueprintTypeId, out var blueprint)) return blueprint;
            blueprint = IndustryDbService.GetBlueprint(blueprintTypeId);
            _blueprintCache[blueprintTypeId] = blueprint;
            return blueprint;
        }

        private IndustryPlanetaryRecipe GetPlanetaryRecipe(int typeId)
        {
            if (typeId <= 0) return null;
            if (_planetaryCache.TryGetValue(typeId, out var cached)) return cached;
            var recipe = IndustryDbService.GetPlanetaryRecipeByProduct(typeId);
            _planetaryCache[typeId] = recipe;
            return recipe;
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
            foreach (var activity in IndustryDbService.GetActivities(blueprintTypeId))
            {
                _activityTimeCache[Key(blueprintTypeId, activity.ActivityID)] = activity.Time;
                if (activity.ActivityID == activityId)
                {
                    time = activity.Time;
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

        private IndustryBlueprintStock GetBlueprintStock(int productTypeId)
        {
            if (_options.BlueprintStocks != null
                && _options.BlueprintStocks.TryGetValue(productTypeId, out var stock))
            {
                return stock;
            }
            return null;
        }

        private IndustryActivityModifier GetStructureModifier(int activityId)
        {
            if (_options.StructureModifiers != null
                && _options.StructureModifiers.TryGetValue(activityId, out var modifier)
                && modifier != null)
            {
                return modifier;
            }
            return new IndustryActivityModifier();
        }

        private double GetRigMe(int categoryId, int groupId, bool isReaction)
        {
            if (isReaction && !_options.ApplyRigToReactions)
            {
                return 0;
            }
            double baseBonus = _options.RigMeBonus;
            if (_options.RigBonuses != null)
            {
                var matched = FindRigBonus(categoryId, groupId);
                if (matched != null)
                {
                    baseBonus = matched.MeBonus;
                }
            }
            return Math.Clamp(baseBonus, 0, 0.99) * Math.Max(0, _options.SpaceModifier);
        }

        private double GetRigTe(int categoryId, int groupId, bool isReaction)
        {
            double baseBonus = _options.RigTeBonus;
            if (_options.RigBonuses != null)
            {
                var matched = FindRigBonus(categoryId, groupId);
                if (matched != null)
                {
                    baseBonus = matched.TeBonus;
                }
            }
            return Math.Clamp(baseBonus, 0, 0.99) * Math.Max(0, _options.SpaceModifier);
        }

        private IndustryRigBonus FindRigBonus(int categoryId, int groupId)
        {
            if (_options.RigBonuses == null) return null;
            foreach (var rig in _options.RigBonuses)
            {
                if (rig.TargetFilterId > 0)
                {
                    if (MatchesTargetFilter(rig.TargetFilterId, categoryId, groupId))
                    {
                        return rig;
                    }
                }
                else if (rig.CategoryID == categoryId)
                {
                    return rig;
                }
            }
            return null;
        }

        private bool MatchesTargetFilter(int filterId, int categoryId, int groupId)
        {
            if (!_targetFilterLoaded)
            {
                _targetFilterCache = IndustryDbService.GetIndustryTargetFilters();
                _targetFilterLoaded = true;
            }
            var filter = _targetFilterCache?.FirstOrDefault(p => p.Id == filterId);
            return filter != null
                && (filter.CategoryIds.Contains(categoryId) || filter.GroupIds.Contains(groupId));
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
