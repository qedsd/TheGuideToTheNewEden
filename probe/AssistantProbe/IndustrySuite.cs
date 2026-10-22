using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
        var scale = IndustryScheduleTimeScale.Create(new[]
        {
            new IndustryScheduleJob { Key = "long", TypeName = "Long", DurationHours = 60, StartHours = 0, EndHours = 60 },
            new IndustryScheduleJob { Key = "short", TypeName = "Short", DurationHours = 1, StartHours = 0, EndHours = 1 },
        }, IndustryScheduleViewModel.PixelsPerHour);
        c.That(scale.HasBreak && scale.Map(60) < 60 * IndustryScheduleViewModel.PixelsPerHour, "长任务触发时间轴压缩");
        c.That(scale.BreakText.Contains("10h") && scale.BreakText.Contains("60h"), "压缩标签 10h ... 60h");
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

    private sealed class ScheduleHost
    {
        public IndustryScheduleViewModel Schedule { get; set; } = null!;
        public bool HasSchedule => true;
        public int ScheduleObjectiveIndex { get; set; }
        public int DetailTabIndex { get; set; }
        public bool CanReassignSchedule => true;
    }
}
