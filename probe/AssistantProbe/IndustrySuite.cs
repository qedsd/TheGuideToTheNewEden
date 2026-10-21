using System.Linq;
using TheGuideToTheNewEden.Core.Models.Indusrty;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.Core.Services.Industry;
using TheGuideToTheNewEden.WPF.ViewModels.Industry;
using TheGuideToTheNewEden.WPF.Views.Pages;

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
        }
        catch (System.Exception ex)
        {
            c.That(false, $"工业页面构造异常：{ex.Message}");
        }
    }
}
