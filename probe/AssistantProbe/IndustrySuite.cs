using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.Indusrty;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.Core.Services.Industry;
using TheGuideToTheNewEden.WPF.Services.Industry;
using TheGuideToTheNewEden.WPF.ViewModels.Industry;
using TheGuideToTheNewEden.WPF.Views.Pages;
using TheGuideToTheNewEden.WPF.Views.UserControls;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.Probe;

/// <summary>
/// 工业求解器探针（阶段 158）：用真实主库验证买/建成本口径、发明技能识别、
/// 解密器、T2 ME/TE 回写、反应 ME 规则与多目标。
/// </summary>
internal static class IndustrySuite
{
    public static void Run(Check c)
    {
        ProbeCore.Ensure();

        c.Section("IndustrySuite · 环境");
        c.That(IndustryDbService.IsAvailable, "主库已载入（IndustryDbService.IsAvailable）");
        if (!IndustryDbService.IsAvailable)
        {
            return;
        }

        c.Section("IndustrySuite · 计算取消");
        using (var cts = new CancellationTokenSource())
        {
            cts.Cancel();
            var canceled = false;
            try
            {
                new IndustrySolver(new IndustrySolverOptions(), cts.Token)
                    .Solve(new[] { new IndustryBuildTarget(34, 1) });
            }
            catch (OperationCanceledException)
            {
                canceled = true;
            }
            c.That(canceled, "取消令牌中断 BOM 求解");
        }

        c.Section("IndustrySuite · 工业默认设置");
        var defaultSetting = new IndustrySetting();
        c.That(defaultSetting.UseMarketDepth, "默认拉取详细订单");
        c.That(!defaultSetting.SellImmediately, "默认挂卖单");
        c.That(defaultSetting.UseOwnedBlueprints, "默认使用角色自有蓝图");

        c.Section("IndustrySuite · 建筑档案");
        var profile = new IndustryBuildingProfile
        {
            Name = "测试建筑",
            FacilityTaxPercent = 7,
            SpaceModifier = 1.9,
            RigMeBonusPercent = 2,
            RigTeBonusPercent = 20,
            RigBonuses = new List<IndustryRigSetting>
            {
                new() { CategoryId = 6, CategoryName = "Ship", MePercent = 1, TePercent = 10 },
            },
        };
        var profileRoundTrip = JsonConvert.DeserializeObject<IndustryBuildingProfile>(
            JsonConvert.SerializeObject(profile));
        c.Equal(profileRoundTrip?.Name, "测试建筑", "建筑档案名称往返");
        c.Equal(profileRoundTrip?.FacilityTaxPercent, 7.0, "建筑档案设施税往返");
        c.Equal(profileRoundTrip?.RigBonuses.Count, 1, "建筑档案 rig 往返");

        c.Section("IndustrySuite · 官方工业元数据");
        var activityDefinitions = IndustryDbService.GetIndustryActivities();
        c.That(activityDefinitions.Count >= 6, $"官方活动定义可读（{activityDefinitions.Count} 条）");
        c.That(activityDefinitions.Any(p => p.Id == 9), "官方反应活动 ID = 9");
        var assemblyLines = IndustryDbService.GetIndustryAssemblyLines();
        c.That(assemblyLines.Count > 0, $"装配线可读（{assemblyLines.Count} 条）");
        c.That(assemblyLines.Any(p => p.Details.Count > 0), "装配线分类/组倍率可读");
        var installationLines = IndustryDbService.GetInstallationAssemblyLineIds(16216);
        c.That(installationLines.Count >= 2, "设施类型 → 装配线可解析");
        var targetFilters = IndustryDbService.GetIndustryTargetFilters();
        c.That(targetFilters.Count > 0, $"目标过滤器可读（{targetFilters.Count} 个）");
        var shipsFilter = targetFilters.FirstOrDefault(p => p.Id == 3);
        c.That(shipsFilter != null && shipsFilter.CategoryIds.Contains(6), "Ships 过滤器命中分类 6");
        var structures = IndustryDbService.GetIndustryStructureOptions();
        c.That(structures.Count > 0, $"结构类型可读（{structures.Count} 个）");
        var raitaru = IndustryDbService.GetStructureModifiers(35825);
        c.NotNull(raitaru, "Raitaru 官方 modifier 可解析");
        var equipmentRig = IndustryDbService.GetIndustryModifierSources(43920);
        c.That(equipmentRig.Any(p => p.FilterID == 2), "Equipment rig 的 SDE filterID=2");
        if (raitaru is not null)
        {
            c.That(raitaru.Activities.TryGetValue(1, out var manufacturing), "Raitaru 制造活动存在");
            if (manufacturing is not null)
            {
                c.That(Math.Abs(manufacturing.Material - 0.99) < 0.0001, "Raitaru 材料倍率 0.99");
                c.That(Math.Abs(manufacturing.Time - 0.85) < 0.0001, "Raitaru 时间倍率 0.85");
                c.That(Math.Abs(manufacturing.Cost - 0.97) < 0.0001, "Raitaru 费用倍率 0.97");
            }
        }
        c.That(Math.Abs(IndustryCalculator.ApplyME(100, 1, 0, 0, 0.99) - 99) < 0.001, "结构材料倍率参与 ApplyME");
        c.That(Math.Abs(IndustryCalculator.JobTimeSeconds(100, 1, 0, 0, 0.85) - 85) < 0.001, "结构时间倍率参与 JobTime");
        c.That(Math.Abs(IndustryCalculator.JobCost(1000, 0.01, 0.1, 0.97) - 149.7) < 0.001,
            "结构费用/设施税/SCC 按加法口径参与 JobCost");
        var tatara = IndustryDbService.GetStructureModifiers(35836);
        c.NotNull(tatara, "Tatara 官方 modifier 可解析");
        c.That(tatara?.Activities.ContainsKey(9) == true, "Tatara 带官方反应活动 9");
        var baseline = new IndustrySolver(new IndustrySolverOptions { DefaultME = 0, DefaultTE = 0 })
            .Solve(new[] { new IndustryBuildTarget(24495, 1) }).Targets[0];
        var withStructure = new IndustrySolver(new IndustrySolverOptions
        {
            DefaultME = 0,
            DefaultTE = 0,
            StructureModifiers =
            {
                [IndustryActivity.Manufacturing] = new IndustryActivityModifier { Time = 0.5, Material = 0.5, Cost = 0.5 }
            }
        }).Solve(new[] { new IndustryBuildTarget(24495, 1) }).Targets[0];
        c.That(baseline.JobTimeSeconds > 0 && Math.Abs(withStructure.JobTimeSeconds - baseline.JobTimeSeconds * 0.5) < 0.01,
            "求解器套用结构时间倍率");

        c.Section("IndustrySuite · BPO / BPC 与净利");
        var bpcOptions = new IndustrySolverOptions
        {
            DefaultME = 0,
            DefaultTE = 0,
            BlueprintStocks =
            {
                [24495] = new IndustryBlueprintStock
                {
                    BpcRuns = 1,
                    BpcCount = 1,
                    BpcMaterialEfficiency = 5,
                    BpcTimeEfficiency = 10,
                }
            }
        };
        var bpcNode = new IndustrySolver(bpcOptions).Solve(new[] { new IndustryBuildTarget(24495, 100000) }).Targets[0];
        c.Equal(bpcNode.BpcRunsAvailable, 1L, "BPC 可用流程来自角色蓝图");
        c.Equal(bpcNode.BpcRunsUsed, 1L, "BPC 流程被消耗");
        c.Equal(bpcNode.BpcRunsRemaining, 0L, "BPC 剩余流程归零");
        c.That(bpcNode.BpcRunsShortage > 0, "BPC 不足被标记");

        var netOptions = new IndustrySolverOptions
        {
            DefaultME = 0,
            DefaultTE = 0,
            SalesTaxRate = 0.1,
            SellImmediately = true,
            MarketStats =
            {
                [24495] = new IndustryMarketStat { BuyMax = 1000, SellMin = 1100 }
            }
        };
        var netNode = new IndustrySolver(netOptions).Solve(new[] { new IndustryBuildTarget(24495, 1) }).Targets[0];
        c.That(netNode.Revenue > 0 && Math.Abs(netNode.NetRevenue - netNode.Revenue * 0.9) < 0.01,
            "净收入扣除销售税");

        var reprocessed = IndustryOreCalculator.Reprocess(1230, 1000, 1.0);
        c.That(reprocessed.Any(p => p.TypeID == 34 && p.Quantity == 4000), "回收：凡晶石 1000 → 三钛 4000");
        var compressed = IndustryOreCalculator.Compress(1230, 1000);
        c.That(compressed != null && compressed.Quantity == 1000, "压缩：单位数量 1:1");

        c.Section("IndustrySuite · 本地 SDE 版本");
        var sdeBuild = IndustryDbService.GetSdeBuildNumber();
        c.NotNull(sdeBuild, "主库 sdeInfo.buildNumber 可读");
        c.That(sdeBuild is > 0, $"本地 SDE buildNumber = {sdeBuild}");

        c.Section("IndustrySuite · 行星开发");
        var planetaryRecipe = IndustryDbService.GetPlanetaryRecipeByProduct(9830);
        c.NotNull(planetaryRecipe, "火箭燃料行星配方可读");
        if (planetaryRecipe is not null)
        {
            c.Equal(planetaryRecipe.OutputQuantity, 5L, "行星配方每周期产出 5");
            c.That(planetaryRecipe.Inputs.Count > 0, "行星配方有输入");
            var planetaryNode = new IndustrySolver(new IndustrySolverOptions()).SolveNode(9830, 5);
            c.Equal(planetaryNode.ActivityID, IndustryActivity.Planetary, "行星产物走 Planetary 活动");
            c.Equal(planetaryNode.Runs, 1, "5 需求 / 5 产出 = 1 周期");
            c.Equal(planetaryNode.QuantityProduced, 5L, "行星周期产出 5");
            c.That(planetaryNode.Inputs.Count > 0, "行星节点展开输入");

            var planetaryOptions = new IndustrySolverOptions
            {
                PlanetTaxRate = 0.05,
                AveragePrices = new Dictionary<int, double> { [9830] = 1000 },
            };
            var planetaryPlan = new IndustrySolver(planetaryOptions)
                .Solve(new[] { new IndustryBuildTarget(9830, 10) });
            var planetaryTarget = planetaryPlan.Targets[0];
            c.That(planetaryTarget.PlanetTax > 0, "行星出口税计入节点成本");
            var planetarySchedule = IndustryScheduler.Build(planetaryPlan, planetaryOptions);
            c.That(planetarySchedule.Jobs.Any(p => p.ActivityId == 100 && p.LineId == "P1"),
                "行星任务进入排产（不占工业槽位）");
        }

        c.Section("IndustrySuite · T3 制造 / 逆向");
        var t3Plan = new IndustrySolver(new IndustrySolverOptions { IncludeInvention = true })
            .Solve(new[] { new IndustryBuildTarget(29984, 1) });
        var t3Target = t3Plan.Targets.FirstOrDefault();
        c.NotNull(t3Target, "T3 目标 Tengu 可求解");
        if (t3Target is not null)
        {
            c.Equal(t3Target.BlueprintTypeID, 29985, "T3 制造蓝图 29985");
            var t3Invention = FindIndustryNode(t3Target, p => p.IsInvention && p.TypeID == 29984);
            c.NotNull(t3Invention, "T3 逆向走 invention 节点");
            if (t3Invention is not null)
            {
                c.Equal(t3Invention.BlueprintTypeID, 30752, "T3 逆向源是 relic 30752");
                c.That(t3Invention.InventionProbability > 0, "T3 逆向概率来自 SDE");
                c.Equal(t3Invention.InventionOutputRuns, 20, "T3 BPC 20 runs");
                c.That(t3Invention.Inputs.Any(p => p.TypeID == 30752 && p.QuantityNeeded > 0), "T3 relic 作为消耗输入计入");
            }
        }

        c.Section("IndustrySuite · 库存覆盖 / 共享半成品");
        var noStockPlan = new IndustrySolver(new IndustrySolverOptions { DefaultME = 2, DefaultTE = 4 })
            .Solve(new[] { new IndustryBuildTarget(24495, 1) });
        var phenolic = noStockPlan.Targets[0].Inputs.FirstOrDefault(p => p.TypeID == 16680);
        c.NotNull(phenolic, "导弹计划包含酚合成物");
        if (phenolic is not null)
        {
            var firstOxygen = phenolic.Inputs.FirstOrDefault(p => p.TypeID == 4312);
            var silicon = phenolic.Inputs.FirstOrDefault(p => p.TypeID == 16658);
            c.NotNull(firstOxygen, "酚合成物包含氧燃料块");
            c.NotNull(silicon, "酚合成物包含二硼硅");
            if (silicon is not null)
            {
                var sharedOxygen = silicon.Inputs.FirstOrDefault(p => p.TypeID == 4312);
                c.NotNull(sharedOxygen, "二硼硅包含共享氧燃料块");
                if (sharedOxygen is not null)
                {
                    c.Equal(sharedOxygen.Runs, 0, "共享氧燃料块不排流程");
                    c.Equal(sharedOxygen.QuantityFromStock, 5L, "共享氧燃料块库存抵扣 5");
                    c.Equal(sharedOxygen.Inputs.Count, 0, "共享覆盖节点不再展开 0 数量材料");
                }
            }
        }

        var stockPlan = new IndustrySolver(new IndustrySolverOptions
        {
            DefaultME = 2,
            DefaultTE = 4,
            Assets = new Dictionary<int, long> { [16680] = 11 },
        }).Solve(new[] { new IndustryBuildTarget(24495, 1) });
        var stockPhenolic = stockPlan.Targets[0].Inputs.FirstOrDefault(p => p.TypeID == 16680);
        c.NotNull(stockPhenolic, "库存计划包含酚合成物");
        if (stockPhenolic is not null)
        {
            c.Equal(stockPhenolic.Runs, 0, "库存覆盖的酚合成物不排流程");
            c.Equal(stockPhenolic.QuantityFromStock, 11L, "酚合成物库存抵扣 11");
            c.Equal(stockPhenolic.Inputs.Count, 0, "库存覆盖节点不再展开 0 数量材料");
            var stockVm = new IndustryPlanNodeViewModel(stockPhenolic);
            c.Equal(stockVm.DecisionKey, "stock", "角色资产覆盖决策为 stock");
            c.Equal(stockVm.DecisionText, "库存", "角色资产覆盖显示为库存");
            c.That(stockVm.QuantityText.Contains("角色资产 11"), "数量行标明角色资产 11");
        }

        var sharedNode = noStockPlan.Targets[0].Inputs.FirstOrDefault(p => p.TypeID == 16680)?
            .Inputs.FirstOrDefault(p => p.TypeID == 16658)?
            .Inputs.FirstOrDefault(p => p.TypeID == 4312);
        if (sharedNode is not null)
        {
            var sharedVm = new IndustryPlanNodeViewModel(sharedNode);
            c.Equal(sharedVm.DecisionKey, "surplus", "上游多产出覆盖决策为 surplus");
            c.Equal(sharedVm.DecisionText, "上游剩余", "上游多产出显示为上游剩余");
            c.That(sharedVm.QuantityText.Contains("上游剩余 5"), "数量行标明上游剩余 5");
        }

        var forcedPlan = new IndustrySolver(new IndustrySolverOptions
        {
            DefaultME = 2,
            DefaultTE = 4,
            Assets = new Dictionary<int, long> { [16680] = 11 },
            Decisions = new Dictionary<int, IndustryDecision> { [16680] = IndustryDecision.Build },
        }).Solve(new[] { new IndustryBuildTarget(24495, 1) });
        var forcedPhenolic = forcedPlan.Targets[0].Inputs.FirstOrDefault(p => p.TypeID == 16680);
        c.NotNull(forcedPhenolic, "强制制造计划包含酚合成物");
        if (forcedPhenolic is not null)
        {
            c.Equal(forcedPhenolic.Runs, 1, "强制制造忽略库存排 1 流程");
            c.Equal(forcedPhenolic.QuantityProduced, 2200L, "强制制造产出 2200");
            c.That(forcedPhenolic.Inputs.Count > 0, "强制制造仍展开材料");
        }

        c.Section("IndustrySuite · 买/建成本口径");
        var buyOptions = new IndustrySolverOptions
        {
            AveragePrices = new Dictionary<int, double> { [34] = 5 },
            Assets = new Dictionary<int, long> { [34] = 40 },
        };
        var buyPlan = new IndustrySolver(buyOptions).Solve(new[] { new IndustryBuildTarget(34, 100) });
        var buyNode = buyPlan.Targets[0];
        c.Equal(buyNode.QuantityToBuy, 60L, "资产抵扣后采购 60");
        c.Equal(buyNode.ReferenceCostFull, 500.0, "全量参考成本 500");
        c.Equal(buyNode.ReferenceCost, 300.0, "现金参考成本 300");
        c.Equal(buyPlan.Summary.CashOutlay, 300.0, "汇总现金支出 300");
        c.Equal(buyPlan.Summary.StockValue, 200.0, "汇总库存抵扣 200");

        var coveredBuyPlan = new IndustrySolver(new IndustrySolverOptions
        {
            AveragePrices = new Dictionary<int, double> { [34] = 5 },
            Assets = new Dictionary<int, long> { [34] = 40 },
        }).Solve(new[] { new IndustryBuildTarget(34, 40) });
        var coveredBuy = coveredBuyPlan.Targets[0];
        c.Equal(coveredBuy.Decision, IndustryDecision.Buy, "纯采购物品决策仍为 Buy");
        c.Equal(coveredBuy.QuantityToBuy, 0L, "采购物品被资产完全覆盖");
        var coveredBuyVm = new IndustryPlanNodeViewModel(coveredBuy);
        c.Equal(coveredBuyVm.IsStockCovered, true, "采购节点完全覆盖识别为库存");
        c.Equal(coveredBuyVm.DecisionKey, "stock", "采购覆盖决策为 stock");
        c.Equal(coveredBuyVm.DecisionText, "库存", "采购覆盖显示为库存");
        c.Equal(coveredBuyVm.FullCostLabel, "库存市值", "采购覆盖显示库存市值");
        c.That(coveredBuyVm.QuantityText.Contains("角色资产 40"), "采购覆盖数量行标明角色资产 40");

        c.Section("IndustrySuite · 发明技能顺序 / T2 ME-TE");
        var inventionOptions = new IndustrySolverOptions
        {
            CharacterSkills = new Dictionary<int, int> { [21790] = 5 }, // 只给加密学，数据核心为 0
            DefaultME = 10,
            DefaultTE = 20,
            AveragePrices = new Dictionary<int, double> { [37458] = 1_000_000 },
        };
        var inventionPlan = new IndustrySolver(inventionOptions).Solve(new[] { new IndustryBuildTarget(37458, 1) });
        var root = inventionPlan.Targets[0];
        var invention = root.Inputs.FirstOrDefault(p => p.IsInvention);
        c.NotNull(invention, "T2 目标生成发明节点");
        if (invention is not null)
        {
            c.Between(invention.InventionProbability, 0.3149, 0.3151, "加密学 5、数据核心 0 → 0.315");
            c.Equal(root.ME, 2, "T2 制造 ME 取自发明产出 2");
            c.Equal(root.TE, 4, "T2 制造 TE 取自发明产出 4");
            c.Equal(invention.InventionOutputRuns, 1, "Kirin BPC 1 run");
        }

        c.Section("IndustrySuite · 产出收入 / 发明自定义");
        var outputPlan = new IndustrySolver(new IndustrySolverOptions
        {
            DefaultME = 2,
            DefaultTE = 4,
            AveragePrices = new Dictionary<int, double> { [24495] = 100 },
        }).Solve(new[] { new IndustryBuildTarget(24495, 1) });
        var outputTarget = outputPlan.Targets[0];
        c.Equal(outputTarget.QuantityProduced, 5000L, "导弹单次产出 5,000");
        c.Equal(outputTarget.Revenue, 500_000.0, "目标收入按实际产出 5,000 计算");
        c.Equal(outputPlan.Summary.Revenue, 500_000.0, "汇总收入按实际产出计算");

        var inventionOverridePlan = new IndustrySolver(new IndustrySolverOptions
        {
            CharacterSkills = new Dictionary<int, int> { [21790] = 5 },
            DefaultME = 10,
            DefaultTE = 20,
            AveragePrices = new Dictionary<int, double> { [37458] = 1_000_000 },
            InventionAttemptsOverride = 2,
            InventionProbabilityOverride = 0.5,
            InventionBpcRunsOverride = 10,
        }).Solve(new[] { new IndustryBuildTarget(37458, 1) });
        var overrideInvention = inventionOverridePlan.Targets[0].Inputs.FirstOrDefault(p => p.IsInvention);
        c.NotNull(overrideInvention, "自定义发明生成发明节点");
        if (overrideInvention is not null)
        {
            c.Equal(overrideInvention.Runs, 2, "手动尝试次数 2");
            c.Equal(overrideInvention.InventionProbability, 0.5, "手动成功率 0.5");
            c.Equal(overrideInvention.InventionOutputRuns, 10, "手动 BPC 每张 10 run");
            c.Equal(overrideInvention.QuantityNeeded, 1L, "每张 10 run 时只需 1 张 BPC");
        }

        c.Section("IndustrySuite · 解密器");
        var decryptorOptions = new IndustrySolverOptions
        {
            CharacterSkills = new Dictionary<int, int> { [21790] = 5 },
            DecryptorTypeId = 21576, // Esoteric Augmentation: ×0.6 / ME -2 / TE +2 / run +9
            AveragePrices = new Dictionary<int, double> { [37458] = 1_000_000 },
        };
        var decryptorPlan = new IndustrySolver(decryptorOptions).Solve(new[] { new IndustryBuildTarget(37458, 1) });
        var decryptorRoot = decryptorPlan.Targets[0];
        var decryptorInvention = decryptorRoot.Inputs.FirstOrDefault(p => p.IsInvention);
        c.NotNull(decryptorInvention, "解密器方案生成发明节点");
        if (decryptorInvention is not null)
        {
            c.Equal(decryptorInvention.DecryptorTypeID, 21576, "解密器已应用");
            c.Between(decryptorInvention.InventionProbability, 0.1889, 0.1891, "解密器 0.6× → 0.189");
            c.Equal(decryptorRoot.ME, 0, "解密器 ME -2 → 0");
            c.Equal(decryptorRoot.TE, 6, "解密器 TE +2 → 6");
            c.Equal(decryptorInvention.InventionOutputRuns, 10, "解密器 run +9 → 10");
            var decryptorInput = decryptorInvention.Inputs.FirstOrDefault(p => p.TypeID == 21576);
            c.NotNull(decryptorInput, "解密器进入采购输入");
            if (decryptorInput is not null)
            {
                c.Equal(decryptorInput.QuantityNeeded, (long)decryptorInvention.Runs, "解密器数量 = 尝试次数");
            }
        }

        var targetOverrideOptions = new IndustrySolverOptions
        {
            CharacterSkills = new Dictionary<int, int> { [21790] = 5 },
            DecryptorTypeId = 21576,
            DecryptorOverrides = new Dictionary<int, int> { [37458] = 0 },
            AveragePrices = new Dictionary<int, double> { [37458] = 1_000_000 },
        };
        var targetOverridePlan = new IndustrySolver(targetOverrideOptions)
            .Solve(new[] { new IndustryBuildTarget(37458, 1) });
        var targetOverrideInvention = targetOverridePlan.Targets[0].Inputs.FirstOrDefault(p => p.IsInvention);
        c.Equal(targetOverrideInvention?.DecryptorTypeID ?? -1, 0, "逐目标解密器可覆盖为无");

        c.Section("IndustrySuite · 反应规则");
        var reactionPlan = new IndustrySolver(new IndustrySolverOptions { DefaultME = 10 })
            .Solve(new[] { new IndustryBuildTarget(16672, 1) });
        var reactionNode = reactionPlan.Targets[0];
        c.Equal(reactionNode.ActivityID, 11, "目标走反应活动 11");
        c.Equal(reactionNode.ME, 0, "反应默认不套 ME");

        c.Section("IndustrySuite · 多目标");
        var multiPlan = new IndustrySolver(new IndustrySolverOptions())
            .Solve(new[] { new IndustryBuildTarget(34, 1), new IndustryBuildTarget(35, 1) });
        c.Equal(multiPlan.Targets.Count, 2, "两个目标各自成树");

        c.Section("IndustrySuite · 结果快照序列化");
        var snapshotJson = Newtonsoft.Json.JsonConvert.SerializeObject(noStockPlan);
        var snapshot = Newtonsoft.Json.JsonConvert.DeserializeObject<IndustryBuildPlan>(snapshotJson);
        c.NotNull(snapshot, "结果树可序列化/反序列化");
        if (snapshot is not null && snapshot.Targets.Count > 0 && noStockPlan.Targets.Count > 0)
        {
            c.Equal(snapshot.Targets.Count, noStockPlan.Targets.Count, "结果目标数往返一致");
            c.Equal(snapshot.Targets[0].Inputs.Count, noStockPlan.Targets[0].Inputs.Count, "结果子节点往返一致");
            c.Equal(snapshot.Summary.BuildRuns.Count, noStockPlan.Summary.BuildRuns.Count, "结果流程汇总往返一致");
            c.Equal(snapshot.Summary.BuyQuantities.Count, noStockPlan.Summary.BuyQuantities.Count, "结果采购汇总往返一致");
            c.That(snapshot.Targets[0].ReferenceUnitPrice >= 0, "结果价格字段可读");
        }

        c.Section("IndustrySuite · 技能槽位排产");
        c.That(InvTypeService.QueryByName("Mass Production", false).Count > 0, "技能名 Mass Production 可解析");
        var skillNames = new[]
        {
            "Mass Production", "Advanced Mass Production",
            "Laboratory Operation", "Advanced Laboratory Operation",
            "Mass Reactions", "Advanced Mass Reactions",
            "Industry", "Advanced Industry",
            "Reactions", "Advanced Reactions",
        };
        var skillIds = new Dictionary<int, int>();
        foreach (var name in skillNames)
        {
            var id = InvTypeService.QueryByName(name, false).FirstOrDefault()?.TypeID ?? 0;
            if (id > 0 && !skillIds.ContainsKey(id))
            {
                skillIds[id] = 5;
            }
        }
        var maxProfile = IndustryCharacterProfileFactory.Create(9003, "Test", skillIds);
        c.Equal(maxProfile.ManufacturingSlots, 11, "满制造技能 11 槽");
        c.Equal(maxProfile.InventionSlots, 11, "满发明技能 11 槽");
        c.Equal(maxProfile.ReactionSlots, 11, "满反应技能 11 槽");
        c.That(maxProfile.ManufacturingTimeMultiplier < 1.0, "制造技能时间修正 <1");
        var defaultProfile = IndustryCharacterProfileFactory.CreateDefault();
        c.That(defaultProfile.ManufacturingSlots == 11
            && defaultProfile.InventionSlots == 11
            && defaultProfile.ReactionSlots == 11, "默认角色按满技能 11/11/11");
        var customProfile = IndustryCharacterProfileFactory.CreateCustom(new IndustryCustomCharacter
        {
            Id = -9001,
            Name = "自定义测试",
            MassProduction = 5,
            AdvancedMassProduction = 5,
            LaboratoryOperation = 5,
            AdvancedLaboratoryOperation = 5,
            MassReactions = 5,
            AdvancedMassReactions = 5,
            Industry = 5,
            AdvancedIndustry = 5,
            Reactions = 5,
            AdvancedReactions = 5,
        });
        c.Equal(customProfile.ManufacturingSlots, 11, "自定义角色满制造 11 槽");
        c.Equal(customProfile.InventionSlots, 11, "自定义角色满发明 11 槽");
        c.Equal(customProfile.ReactionSlots, 11, "自定义角色满反应 11 槽");
        c.That(customProfile.ManufacturingTimeMultiplier < 1.0, "自定义角色制造时间修正 <1");
        var customSetting = new IndustrySetting
        {
            CustomCharacters = new List<IndustryCustomCharacter>
            {
                new()
                {
                    Id = -5,
                    Name = "JSON角色",
                    MassProduction = 4,
                    AdvancedReactions = 3,
                },
            },
        };
        var customSettingJson = Newtonsoft.Json.JsonConvert.SerializeObject(customSetting);
        var customSettingRound = Newtonsoft.Json.JsonConvert.DeserializeObject<IndustrySetting>(customSettingJson);
        c.That(customSettingRound?.CustomCharacters.Count == 1
            && customSettingRound.CustomCharacters[0].Name == "JSON角色", "自定义角色设置 JSON 往返");
        var customDefault = IndustryCustomCharacter.CreateDefault(-7);
        c.That(customDefault.MassProduction == 5
            && customDefault.AdvancedMassProduction == 5
            && customDefault.LaboratoryOperation == 5
            && customDefault.AdvancedLaboratoryOperation == 5
            && customDefault.MassReactions == 5
            && customDefault.AdvancedMassReactions == 5
            && customDefault.Industry == 5
            && customDefault.AdvancedIndustry == 5
            && customDefault.Reactions == 5
            && customDefault.AdvancedReactions == 5, "新建自定义角色默认全部 5 级");
        var scheduleOptions = new IndustrySolverOptions
        {
            DefaultME = 2,
            DefaultTE = 4,
            CharacterProfiles = new List<IndustryCharacterProfile>
            {
                new IndustryCharacterProfile
                {
                    CharacterId = 9001,
                    CharacterName = "Aria",
                    ManufacturingSlots = 8,
                    InventionSlots = 1,
                    ReactionSlots = 1,
                    ManufacturingTimeMultiplier = 0.88,
                    ReactionTimeMultiplier = 0.95,
                    InventionTimeMultiplier = 1.0,
                },
                new IndustryCharacterProfile
                {
                    CharacterId = 9002,
                    CharacterName = "Borealis",
                    ManufacturingSlots = 4,
                    InventionSlots = 1,
                    ReactionSlots = 1,
                    ManufacturingTimeMultiplier = 1.0,
                    ReactionTimeMultiplier = 0.95,
                    InventionTimeMultiplier = 1.0,
                },
            },
        };
        var schedulePlan = new IndustrySolver(scheduleOptions)
            .Solve(new[] { new IndustryBuildTarget(37458, 1) });
        var schedule = IndustryScheduler.Build(schedulePlan, scheduleOptions);
        c.That(schedule.Jobs.Count > 0, $"排产生成任务（{schedule.Jobs.Count} 个）");
        c.That(schedule.MakespanHours > 0, $"排产完成时间 > 0（{schedule.MakespanHours:0.0}h）");
        c.Equal(schedule.Characters.Count, 2, "排产保留两个角色");
        c.That(schedule.Jobs.All(p => !string.IsNullOrEmpty(p.CharacterName) && !string.IsNullOrEmpty(p.LineId)), "每个任务都分配了角色/产线");
        c.That(schedule.CriticalJobKeys.Count > 0, "排产标出关键任务");

        var scheduleByKey = schedule.Jobs.ToDictionary(p => p.Key);
        var dependencyRespected = true;
        foreach (var job in schedule.Jobs)
        {
            foreach (var dependencyKey in job.Dependencies)
            {
                if (scheduleByKey.TryGetValue(dependencyKey, out var dependency)
                    && dependency.EndHours > job.StartHours + 0.0001)
                {
                    dependencyRespected = false;
                }
            }
        }
        c.That(dependencyRespected, "排产尊重上游完成时间");

        var slotOverlap = false;
        foreach (var group in schedule.Jobs.GroupBy(p => new { p.CharacterId, p.LineId }))
        {
            var ordered = group.OrderBy(p => p.StartHours).ToList();
            for (int i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].StartHours + 0.0001 < ordered[i - 1].EndHours)
                {
                    slotOverlap = true;
                }
            }
        }
        c.That(!slotOverlap, "同一角色同一槽位不重叠");
        c.That(schedule.Characters.Sum(p => p.ManufacturingSlots) == 12, "制造槽位来自角色配置（8 + 4）");

        c.Section("IndustrySuite · 市场档位与成交量");
        var volumeStat = new IndustryMarketStat
        {
            BuyMax = 100,
            BuyVolume = 130,
            SellMin = 110,
            BuyLevels = new List<IndustryMarketOrderLevel>
            {
                new IndustryMarketOrderLevel { Price = 100, Volume = 30 },
                new IndustryMarketOrderLevel { Price = 90, Volume = 100 },
            },
        };
        var immediateRevenue = volumeStat.QuoteImmediate(50, out var immediateUnfilled);
        c.That(Math.Abs(immediateRevenue - 4800) < 0.1 && immediateUnfilled == 0,
            "立即卖出按买单档位成交量计价");
        volumeStat.QuoteImmediate(200, out var overflowUnfilled);
        c.Equal(overflowUnfilled, 70L, "超出可见买单档位的数量被标记");

        scheduleOptions.InProgressJobDetails.Add(new IndustryInProgressJob
        {
            JobId = 90001,
            CharacterId = 9001,
            CharacterName = "Aurora",
            ProductTypeId = 34,
            ProductName = "Tritanium",
            ActivityId = 1,
            Runs = 1,
            QuantityProduced = 1,
            RemainingHours = 5,
            EndDate = DateTime.Now.AddHours(5),
            Status = "active",
        });
        var activeSchedule = IndustryScheduler.Build(schedulePlan, scheduleOptions);
        c.That(activeSchedule.Jobs.Any(p => p.IsInProgress && p.Key == "active:90001"), "在制任务进入排产");
        c.That(activeSchedule.Jobs
                .Where(p => !p.IsInProgress && p.CharacterId == 9001 && p.LineId == "M1")
                .All(p => p.StartHours >= 5 - 0.001),
            "在制任务占用槽位，后续任务从剩余时间之后开始");

        var customOnlyOptions = new IndustrySolverOptions
        {
            DefaultME = 2,
            DefaultTE = 4,
            CharacterProfiles = new List<IndustryCharacterProfile> { customProfile },
        };
        var customOnlyPlan = new IndustrySolver(customOnlyOptions)
            .Solve(new[] { new IndustryBuildTarget(37458, 1) });
        var customOnlySchedule = IndustryScheduler.Build(customOnlyPlan, customOnlyOptions);
        var customJobKey = customOnlySchedule.Jobs.First().Key;
        var customPinnedSchedule = IndustryScheduler.Build(customOnlyPlan, customOnlyOptions,
            IndustryScheduleObjective.Makespan,
            new Dictionary<string, long> { [customJobKey] = customProfile.CharacterId });
        var customPinnedJob = customPinnedSchedule.Jobs.FirstOrDefault(p => p.Key == customJobKey);
        c.That(customPinnedJob is not null
            && customPinnedJob.CharacterId == customProfile.CharacterId
            && customPinnedJob.IsPinned, "负 Id 自定义角色可手动改派");

        var scheduleVm = new IndustryScheduleViewModel(schedule);
        c.Equal(scheduleVm.Characters.Count, 2, "排产 VM 角色数");
        c.That(scheduleVm.ActiveCharacters.Count > 0
            && scheduleVm.ActiveCharacters.All(ch => ch.ActiveLines.Count > 0
                && ch.ActiveLines.All(line => line.HasJobs)), "排程 / 工位只显示有任务的产线");
        c.That(scheduleVm.Characters.All(ch => ch.LineGroups.All(g =>
                g.Lines.Count > 0 && g.Lines.All(line => line.HasJobs))), "工位只按类型分组显示有任务产线");
        c.That(scheduleVm.Characters.SelectMany(ch => ch.LineGroups).All(g =>
                g.Lines.Select(line => line.Kind).Distinct().Count() == 1), "工位分组不混制造/发明/反应");
        var longJob = new IndustryScheduleJob
        {
            Key = "long",
            TypeName = "Long",
            DurationHours = 60,
            StartHours = 0,
            EndHours = 60,
        };
        var scale = IndustryScheduleTimeScale.Create(new[]
        {
            longJob,
            new IndustryScheduleJob { Key = "short", TypeName = "Short", DurationHours = 1, StartHours = 0, EndHours = 1 },
        }, IndustryScheduleViewModel.PixelsPerHour);
        c.That(scale.HasCompression && scale.TimelineWidth < 60 * IndustryScheduleViewModel.PixelsPerHour,
            "长任务触发不等距时间轴压缩");
        c.That(scale.TimelineWidth <= 3600, "时间轴宽度有界");
        c.That(scale.CreateTicks().Count < 30, "时间轴刻度自适应，不逐小时展开");
        var compressedJob = new IndustryScheduleJobViewModel(longJob, scale);
        c.That(compressedJob.HasCompressedGap, "长任务条标记为时间轴压缩");
        var multiLongJobs = Enumerable.Range(0, 6)
            .Select(i => new IndustryScheduleJob
            {
                Key = $"long{i}",
                TypeName = "Long",
                DurationHours = 100,
                StartHours = i * 100,
                EndHours = (i + 1) * 100,
            })
            .ToList();
        var multiLongScale = IndustryScheduleTimeScale.Create(multiLongJobs, IndustryScheduleViewModel.PixelsPerHour);
        c.That(multiLongScale.HasCompression && multiLongScale.TimelineWidth <= 3600,
            "多个长任务（600h）仍限制时间轴宽度");
        c.That(multiLongScale.CreateTicks().Count < 40, "600h 时间轴刻度仍有界");
        c.That(scheduleVm.TimelineWidth <= 3600 && scheduleVm.TimelineTicks.Count < 60,
            "排产甘特宽度与刻度有界");
        c.That(scheduleVm.GraphNodes.Count == schedule.Jobs.Count, "关系图节点完整");
        c.That(scheduleVm.GraphNodes.OrderByDescending(p => p.X).First().Job.Model.IsTarget, "关系图目标节点在最右侧");
        var ganttJob = scheduleVm.Steps.Select(p => p.Job).First();
        c.That(!string.IsNullOrWhiteSpace(ganttJob.ToolTip)
            && ganttJob.ToolTip.Contains(ganttJob.TypeName), "甘特短任务悬浮显示完整名称");
        c.Equal(scheduleVm.Steps.Count, schedule.Jobs.Count, "排产 VM 步骤数");
        c.Equal(scheduleVm.GraphNodes.Count, schedule.Jobs.Count, "排产 VM 关系节点数");
        c.That(scheduleVm.Candidates.Count > 0, "排产 VM 候选角色");
        c.That(scheduleVm.HasSchedule, "排产 VM 有结果");

        c.Section("IndustrySuite · 步骤 / 物料");
        var targetJob = schedule.Jobs.FirstOrDefault(p => p.IsTarget);
        c.NotNull(targetJob, "排产包含目标总装任务");
        if (targetJob is not null)
        {
            c.That(targetJob.Inputs.Count > 0, "总装任务有投入物料");
            c.That(targetJob.Outputs.Count > 0, "总装任务有产出");
        }

        c.Section("IndustrySuite · 常驻物品树");
        var products = IndustryDbService.GetAllManufacturableProducts();
        c.That(products.Count > 0, $"可制造物品树有数据（{products.Count} 条）");
        c.That(products.All(p => p.ProductTypeId > 0), "常驻树不含 TypeID<=0 的产物");
        c.That(products.All(p => !string.IsNullOrWhiteSpace(p.ProductName) && !p.ProductName.StartsWith("#")), "常驻树不含 # 系统名");
        if (DBService.LocalDbReady)
        {
            c.NotNull(LocalDbService.TranInvType(34), "本地化类型可读");
            c.NotNull(LocalDbService.TranInvGroup(25), "本地化分组可读");
            c.That(LocalDbService.TranInvCategories(new List<int> { 6, 7 }).Count > 0, "本地化分类可读");
        }

        c.Section("IndustrySuite · 蓝图浏览器物品树");
        var blueprintViewModel = new IndustryBlueprintPageViewModel();
        Task.Run(() => blueprintViewModel.LoadAllProductsAsync()).GetAwaiter().GetResult();
        c.That(blueprintViewModel.ProductTree.Count > 0, "蓝图浏览器加载分类树");
        c.That(blueprintViewModel.ProductCountText.Contains("可制造物品"), "蓝图浏览器显示物品计数");
        var firstTreeProduct = blueprintViewModel.ProductTree
            .SelectMany(p => p.Groups)
            .SelectMany(p => p.Items)
            .FirstOrDefault();
        if (firstTreeProduct is not null)
        {
            blueprintViewModel.ProductSearchText = firstTreeProduct.Name;
            c.That(blueprintViewModel.ProductTree.Count > 0, "蓝图浏览器搜索过滤");
        }

        c.Section("IndustrySuite · UI 构造");
        try
        {
            ProbeApplication.Ensure();
            var planPage = new IndustryPlanPage();
            c.That(planPage.DataContext is IndustryPlanPageViewModel, "制造计划页 DataContext");
            var blueprintPage = new IndustryBlueprintPage();
            c.That(blueprintPage.DataContext is IndustryBlueprintPageViewModel, "蓝图浏览器页 DataContext");
            var inventoryPage = new IndustryInventoryPage();
            c.That(inventoryPage.DataContext is IndustryInventoryPageViewModel, "库存页 DataContext");
            var customWindow = new IndustryCustomCharacterWindow(new IndustryCustomCharacter
            {
                Id = -1,
                Name = "窗口测试",
                MassProduction = 5,
                AdvancedMassProduction = 5,
                LaboratoryOperation = 5,
                AdvancedLaboratoryOperation = 5,
                MassReactions = 5,
                AdvancedMassReactions = 5,
                Industry = 5,
                AdvancedIndustry = 5,
                Reactions = 5,
                AdvancedReactions = 5,
            });
            c.That(customWindow.Content is not null, "自定义角色窗口 XAML 构造");
            c.Equal(customWindow.BuildModel(-1).MassProduction, 5, "自定义角色窗口回填技能");
            if (customWindow.Content is FrameworkElement customRoot)
            {
                customRoot.Measure(new Size(600, 660));
                customRoot.Arrange(new Rect(0, 0, 600, 660));
                customRoot.UpdateLayout();
                var titleBar = customWindow.FindName("TitleBar") as FrameworkElement;
                var titleTop = titleBar?.TranslatePoint(new Point(0, 0), customRoot).Y ?? -1;
                c.That(titleTop < 1, "自定义角色标题栏在窗口顶部");
                c.That(titleBar?.ActualHeight is > 0 and <= 64, "自定义角色标题栏高度正常");
                var levelBox = customWindow.FindName("MassProductionBox") as ComboBox;
                c.That(levelBox?.Style?.BasedOn is not null, "自定义角色下拉框保留 Fluent 基样式");
            }
            var scheduleView = new IndustryScheduleView
            {
                DataContext = new ScheduleHost { Schedule = scheduleVm },
            };
            scheduleView.Measure(new Size(1200, 760));
            scheduleView.Arrange(new Rect(0, 0, 1200, 760));
            scheduleView.UpdateLayout();
            var scheduleTabs = scheduleView.FindName("ScheduleTabs") as TabControl;
            if (scheduleTabs is not null)
            {
                for (var i = 1; i < scheduleTabs.Items.Count; i++)
                {
                    scheduleTabs.SelectedIndex = i;
                    scheduleView.Measure(new Size(1200, 760));
                    scheduleView.Arrange(new Rect(0, 0, 1200, 760));
                    scheduleView.UpdateLayout();
                }
            }
            c.That(scheduleTabs is not null && scheduleTabs.Items.Count == 5, "调度五个视图（含生产链）");
            c.That(scheduleView.Content is not null, "调度页 XAML / 绑定构造");
        }
        catch (System.Exception ex)
        {
            c.That(false, $"工业页面构造异常：{ex.Message}");
        }
    }

    private static IndustryBuildNode FindIndustryNode(IndustryBuildNode node, Func<IndustryBuildNode, bool> predicate)
    {
        if (node is null) return null;
        if (predicate(node)) return node;
        foreach (var input in node.Inputs)
        {
            var found = FindIndustryNode(input, predicate);
            if (found is not null) return found;
        }
        return null;
    }

    private sealed class ScheduleHost
    {
        public IndustryScheduleViewModel Schedule { get; set; } = null!;
        public bool HasSchedule => true;
        public int ScheduleObjectiveIndex { get; set; }
        public int DetailTabIndex { get; set; }
        public bool CanReassignSchedule => true;
    }
}
