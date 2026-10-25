using System;
using System.Collections.Generic;
using System.Linq;
using TheGuideToTheNewEden.Core.Models.Planetary;

namespace TheGuideToTheNewEden.Core.Services.Planetary
{
    /// <summary>
    /// 需求驱动的行星开发规划：从目标产物（或蓝图导入后的 PI 需求）反推配方链，
    /// 再按行星产能折算殖民地、工厂、提取头、时间与成本。阶段 1 只到殖民地级。
    /// </summary>
    public sealed class PlanetaryPlanner
    {
        private readonly PlanetaryRecipeCatalog _recipes;
        private readonly PlanetTypeCatalog _planetTypes;

        public PlanetaryPlanner(PlanetaryRecipeCatalog recipes, PlanetTypeCatalog planetTypes)
        {
            _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
            _planetTypes = planetTypes ?? throw new ArgumentNullException(nameof(planetTypes));
        }

        public PlanetaryPlan Plan(PlanetaryPlanRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var plan = new PlanetaryPlan();
            var periodDays = Math.Max(0.01, request.PeriodDays);
            var cycleDays = Math.Max(0.01, request.CycleDays);
            var radius = request.RadiusKm;
            var usedEstimatedRadius = radius <= 0;
            if (usedEstimatedRadius)
            {
                radius = PlanetaryCapacityCalculator.DefaultRadiusKm;
            }

            var characters = BuildCharacters(request);
            var effectiveCcu = characters.Count > 0
                ? characters.Min(p => p.CcuLevel)
                : Math.Max(0, Math.Min(5, request.CcuLevel));
            effectiveCcu = Math.Max(0, Math.Min(5, effectiveCcu));
            var availablePlanetTypes = NormalizeAvailablePlanetTypes(request.AvailablePlanetTypes, plan);
            var characterSlots = characters
                .Select(p => new CharacterSlot
                {
                    Character = p,
                    Used = 0,
                })
                .ToList();

            var targets = new Dictionary<int, long>();
            var targetProducts = new List<PlanetaryProduct>();
            foreach (var target in request.Targets ?? new List<PlanetaryTarget>())
            {
                if (target == null || target.Quantity <= 0)
                {
                    continue;
                }
                var product = _recipes.GetProduct(target.TypeId);
                if (product == null)
                {
                    Warn(plan, "未找到行星产物 TypeID " + target.TypeId);
                    continue;
                }
                if (product.Tier == PlanetaryProductTier.P0)
                {
                    Warn(plan, product.Name + " 是 P0 原料，不能作为生产目标");
                    continue;
                }
                if (targets.ContainsKey(product.TypeId))
                {
                    targets[product.TypeId] += target.Quantity;
                }
                else
                {
                    targets[product.TypeId] = target.Quantity;
                    targetProducts.Add(product);
                }
            }

            if (targets.Count == 0)
            {
                Warn(plan, "没有可计算的目标产物");
                plan.Summary.PeriodDays = periodDays;
                plan.Summary.CcuLevel = effectiveCcu;
                plan.Summary.RadiusKm = radius;
                plan.Summary.UsedEstimatedRadius = usedEstimatedRadius;
                plan.Summary.CycleDays = cycleDays;
                return plan;
            }

            if (usedEstimatedRadius)
            {
                Warn(plan, "未提供行星半径，CPU/PG 产能按 " + PlanetaryCapacityCalculator.DefaultRadiusKm + " km 估算");
            }

            var required = new Dictionary<int, double>();
            foreach (var target in targets)
            {
                required[target.Key] = target.Value;
            }
            var targetSet = new HashSet<int>(targets.Keys);
            var productPlans = new Dictionary<int, PlanetaryProductPlan>();
            var imports = new Dictionary<int, double>();
            var targetOutputPerDay = new Dictionary<int, double>();

            // 从高层到低层展开，P4/P3/P2 的输入进入下一层；P4 直接吃 P1 的特例自然成立。
            for (var tier = 4; tier >= 1; tier--)
            {
                var tierProducts = _recipes.GetProducts((PlanetaryProductTier)tier);
                foreach (var product in tierProducts)
                {
                    if (!required.TryGetValue(product.TypeId, out var requiredPerPeriod) || requiredPerPeriod <= 0)
                    {
                        continue;
                    }

                    var isTarget = targetSet.Contains(product.TypeId);
                    var requiredPerDay = requiredPerPeriod / periodDays;
                    var productPlan = new PlanetaryProductPlan
                    {
                        TypeId = product.TypeId,
                        Name = product.Name,
                        Tier = product.Tier,
                        RequiredPerPeriod = requiredPerPeriod,
                        RequiredPerDay = requiredPerDay,
                        IsTarget = isTarget,
                        Inputs = BuildInputText(product, periodDays),
                    };

                    if (product.Recipe == null)
                    {
                        Warn(plan, product.Name + " 缺少配方");
                        continue;
                    }

                    if (tier == 1)
                    {
                        BuildP1Extraction(
                            plan, request, product, productPlan, availablePlanetTypes,
                            effectiveCcu, radius, cycleDays, periodDays, characterSlots, targetOutputPerDay);
                    }
                    else
                    {
                        BuildFactory(
                            plan, request, product, productPlan, availablePlanetTypes,
                            effectiveCcu, radius, cycleDays, periodDays, characterSlots,
                            required, imports, targetOutputPerDay);
                    }

                    productPlans[product.TypeId] = productPlan;
                }
            }

            // 目标产物如果因容量为 0 未产出，仍保留产品计划，方便 UI 解释缺口。
            plan.Products = productPlans.Values
                .OrderByDescending(p => p.Tier)
                .ThenBy(p => p.Name)
                .ToList();
            plan.Colonies = plan.Colonies
                .OrderByDescending(p => p.Tier)
                .ThenBy(p => p.CharacterName)
                .ThenBy(p => p.ProductName)
                .ToList();

            BuildImports(plan, request, imports, periodDays);
            BuildSummary(plan, request, targetProducts, targets, targetOutputPerDay, periodDays, effectiveCcu, radius, usedEstimatedRadius, cycleDays);
            if (usedEstimatedRadius)
            {
                plan.Summary.UsedEstimatedRadius = true;
            }
            return plan;
        }

        private void BuildP1Extraction(
            PlanetaryPlan plan,
            PlanetaryPlanRequest request,
            PlanetaryProduct product,
            PlanetaryProductPlan productPlan,
            IReadOnlyList<PlanetTypeProfile> availablePlanetTypes,
            int ccuLevel,
            double radius,
            double cycleDays,
            double periodDays,
            List<CharacterSlot> characters,
            Dictionary<int, double> targetOutputPerDay)
        {
            var recipe = product.Recipe;
            var p0Input = recipe.Inputs.FirstOrDefault();
            if (p0Input == null)
            {
                Warn(plan, product.Name + " 的提取配方没有 P0 输入");
                productPlan.IsImported = false;
                return;
            }

            var planetType = FindP1PlanetType(p0Input.TypeID, availablePlanetTypes, plan, product.Name);
            if (planetType == null)
            {
                Warn(plan, product.Name + " 找不到可提取 P0 的行星类型");
                return;
            }

            var capacity = PlanetaryCapacityCalculator.Calculate(
                planetType, ccuLevel, radius, PlanetaryColonySetup.ExtractionP1, 1);
            productPlan.PlanetType = planetType.Name;
            productPlan.FactoriesPerColony = capacity.MaxFactories;
            if (!capacity.Fits || capacity.MaxFactories <= 0)
            {
                Warn(plan, product.Name + " 的提取殖民地在当前 CCU/半径下无法容纳");
                return;
            }

            var cycleSeconds = Math.Max(1, recipe.CycleTime);
            var p1PerFactoryPerHour = recipe.OutputQuantity * 3600.0 / cycleSeconds;
            var yieldRatio = PlanetaryExtractionYield.YieldRatioVsBaseline(cycleDays * 24);
            var outputPerFactoryPerDay = p1PerFactoryPerHour * 24 * yieldRatio;
            if (outputPerFactoryPerDay <= 0)
            {
                Warn(plan, product.Name + " 提取产率为 0");
                return;
            }

            var outputPerColony = capacity.MaxFactories * outputPerFactoryPerDay;
            var colonies = (int)Math.Ceiling(productPlan.RequiredPerDay / outputPerColony);
            if (colonies <= 0)
            {
                colonies = 1;
            }

            productPlan.Colonies = colonies;
            productPlan.OutputPerDay = colonies * outputPerColony;
            productPlan.Factories = colonies * capacity.MaxFactories;
            productPlan.Inputs = string.Format(
                "{0} {1:N1}/天（P0，提取）",
                _recipes.GetProduct(p0Input.TypeID)?.Name ?? ("Type " + p0Input.TypeID),
                productPlan.RequiredPerDay * p0Input.Quantity / recipe.OutputQuantity);
            productPlan.SetupCost = 0;
            targetOutputPerDay[product.TypeId] = productPlan.OutputPerDay;

            var p0PerDay = productPlan.RequiredPerDay * p0Input.Quantity / recipe.OutputQuantity;
            for (var i = 0; i < colonies; i++)
            {
                var character = AssignCharacter(characters, plan);
                var setupCost = CalculateSetupCost(planetType, request, PlanetaryColonySetup.ExtractionP1,
                    capacity.MaxFactories, 0, capacity.Launchpads);
                var colony = new PlanetaryColonyPlan
                {
                    CharacterName = character,
                    PlanetType = planetType.Name,
                    Role = "提取",
                    ProductTypeId = product.TypeId,
                    ProductName = product.Name,
                    Tier = product.Tier,
                    Factories = capacity.MaxFactories,
                    ExtractorHeads = capacity.ExtractorHeads,
                    OutputPerDay = outputPerColony,
                    InputsPerDay = p0PerDay / colonies,
                    SetupCost = setupCost,
                    CcuLevel = ccuLevel,
                    UsedEstimatedRadius = capacity.UsedEstimatedRadius,
                };
                plan.Colonies.Add(colony);
                productPlan.SetupCost += setupCost;
            }
        }

        private void BuildFactory(
            PlanetaryPlan plan,
            PlanetaryPlanRequest request,
            PlanetaryProduct product,
            PlanetaryProductPlan productPlan,
            IReadOnlyList<PlanetTypeProfile> availablePlanetTypes,
            int ccuLevel,
            double radius,
            double cycleDays,
            double periodDays,
            List<CharacterSlot> characters,
            Dictionary<int, double> required,
            Dictionary<int, double> imports,
            Dictionary<int, double> targetOutputPerDay)
        {
            var recipe = product.Recipe;
            var setup = product.Tier == PlanetaryProductTier.P2
                ? PlanetaryColonySetup.P1ToP2
                : product.Tier == PlanetaryProductTier.P3
                    ? PlanetaryColonySetup.P2ToP3
                    : PlanetaryColonySetup.P3ToP4;
            var planetType = FindFactoryPlanetType(setup, availablePlanetTypes, plan, product.Name);
            if (planetType == null)
            {
                Warn(plan, product.Name + " 找不到可用工厂行星类型");
                return;
            }

            var capacity = PlanetaryCapacityCalculator.Calculate(
                planetType, ccuLevel, radius, setup, recipe.Inputs.Count);
            productPlan.PlanetType = planetType.Name;
            productPlan.FactoriesPerColony = capacity.MaxFactories;
            if (!capacity.Fits || capacity.MaxFactories <= 0)
            {
                Warn(plan, product.Name + " 的工厂殖民地在当前 CCU/半径下无法容纳");
                return;
            }

            var cycleSeconds = Math.Max(1, recipe.CycleTime);
            var outputPerFactoryPerHour = recipe.OutputQuantity * 3600.0 / cycleSeconds;
            var factories = (int)Math.Ceiling(productPlan.RequiredPerDay / (outputPerFactoryPerHour * 24));
            if (factories <= 0)
            {
                factories = 1;
            }
            var colonies = (int)Math.Ceiling(factories / (double)capacity.MaxFactories);
            if (colonies <= 0)
            {
                colonies = 1;
            }
            productPlan.Factories = factories;
            productPlan.Colonies = colonies;
            productPlan.SetupCost = 0;

            var remainingFactories = factories;
            for (var i = 0; i < colonies; i++)
            {
                var factoriesInColony = Math.Min(capacity.MaxFactories, Math.Max(0, remainingFactories));
                remainingFactories -= factoriesInColony;
                var character = AssignCharacter(characters, plan);
                var setupCost = CalculateSetupCost(planetType, request, setup,
                    factoriesInColony, 0, capacity.Launchpads);
                var inputPerDay = recipe.Inputs.Sum(p =>
                    p.Quantity * factoriesInColony * 24.0 * 3600 / cycleSeconds);
                var colony = new PlanetaryColonyPlan
                {
                    CharacterName = character,
                    PlanetType = planetType.Name,
                    Role = "工厂",
                    ProductTypeId = product.TypeId,
                    ProductName = product.Name,
                    Tier = product.Tier,
                    Factories = factoriesInColony,
                    ExtractorHeads = 0,
                    OutputPerDay = factoriesInColony * outputPerFactoryPerHour * 24,
                    InputsPerDay = inputPerDay,
                    SetupCost = setupCost,
                    CcuLevel = ccuLevel,
                    UsedEstimatedRadius = capacity.UsedEstimatedRadius,
                };
                plan.Colonies.Add(colony);
                productPlan.SetupCost += setupCost;
            }

            productPlan.OutputPerDay = plan.Colonies
                .Where(p => p.ProductTypeId == product.TypeId && p.Role == "工厂")
                .Sum(p => p.OutputPerDay);
            targetOutputPerDay[product.TypeId] = productPlan.OutputPerDay;

            var cycles = productPlan.RequiredPerPeriod / recipe.OutputQuantity;
            foreach (var input in recipe.Inputs)
            {
                var inputQuantity = input.Quantity * cycles;
                if (inputQuantity <= 0)
                {
                    continue;
                }

                var imported = request.Mode == PlanetaryPlanMode.Import;
                if (!imported && request.Mode == PlanetaryPlanMode.Hybrid
                    && request.CurrentSupplyPerDay != null
                    && request.CurrentSupplyPerDay.TryGetValue(input.TypeID, out var currentSupply))
                {
                    inputQuantity = Math.Max(0, inputQuantity - currentSupply * periodDays);
                }

                if (inputQuantity <= 0)
                {
                    continue;
                }

                var inputProduct = _recipes.GetProduct(input.TypeID);
                if (imported || inputProduct == null || inputProduct.Tier == PlanetaryProductTier.P0)
                {
                    if (!imports.ContainsKey(input.TypeID))
                    {
                        imports[input.TypeID] = 0;
                    }
                    imports[input.TypeID] += inputQuantity;
                }
                else
                {
                    if (!required.ContainsKey(input.TypeID))
                    {
                        required[input.TypeID] = 0;
                    }
                    required[input.TypeID] += inputQuantity;
                }
            }
        }

        private List<PlanetaryCharacterInput> BuildCharacters(PlanetaryPlanRequest request)
        {
            var characters = (request.Characters ?? new List<PlanetaryCharacterInput>())
                .Where(p => p != null && p.MaxPlanets > 0)
                .Select(p => new PlanetaryCharacterInput
                {
                    CharacterId = p.CharacterId,
                    Name = string.IsNullOrWhiteSpace(p.Name) ? ("角色 " + p.CharacterId) : p.Name,
                    CcuLevel = Math.Max(0, Math.Min(5, p.CcuLevel)),
                    MaxPlanets = Math.Max(1, Math.Min(6, p.MaxPlanets)),
                })
                .ToList();
            if (characters.Count == 0)
            {
                characters.Add(new PlanetaryCharacterInput
                {
                    CharacterId = 0,
                    Name = "默认角色",
                    CcuLevel = Math.Max(0, Math.Min(5, request.CcuLevel)),
                    MaxPlanets = Math.Max(1, Math.Min(6, request.DefaultMaxPlanets)),
                });
            }
            return characters;
        }

        private List<PlanetTypeProfile> NormalizeAvailablePlanetTypes(
            IReadOnlyList<PlanetTypeProfile> requested,
            PlanetaryPlan plan)
        {
            var available = new List<PlanetTypeProfile>();
            if (requested != null)
            {
                foreach (var profile in requested)
                {
                    if (profile == null)
                    {
                        continue;
                    }
                    var resolved = _planetTypes.GetByTypeId(profile.TypeId);
                    if (resolved == null && profile.P0TypeIds != null && profile.P0TypeIds.Count > 0)
                    {
                        resolved = profile;
                    }
                    if (resolved != null && available.All(p => p.TypeId != resolved.TypeId))
                    {
                        available.Add(resolved);
                    }
                }
            }
            if (available.Count == 0)
            {
                available.AddRange(_planetTypes.All);
            }
            return available;
        }

        private PlanetTypeProfile FindP1PlanetType(
            int p0TypeId,
            IReadOnlyList<PlanetTypeProfile> available,
            PlanetaryPlan plan,
            string productName)
        {
            var candidates = available
                .Where(p => p.P0TypeIds != null && p.P0TypeIds.Contains(p0TypeId))
                .ToList();
            if (candidates.Count == 0)
            {
                candidates = _planetTypes.GetByP0(p0TypeId).ToList();
                Warn(plan, productName + " 的 P0 不在所选星系行星类型中，已改用通用行星类型估算");
            }
            return candidates.FirstOrDefault();
        }

        private PlanetTypeProfile FindFactoryPlanetType(
            PlanetaryColonySetup setup,
            IReadOnlyList<PlanetTypeProfile> available,
            PlanetaryPlan plan,
            string productName)
        {
            if (setup == PlanetaryColonySetup.P3ToP4)
            {
                var candidates = available.Where(p => p.IsP4Capable).ToList();
                if (candidates.Count == 0)
                {
                    candidates = _planetTypes.GetP4Capable().ToList();
                    Warn(plan, productName + " 的 P4 工厂需要 Temperate/Barren，所选星系没有，已改用通用类型估算");
                }
                return candidates.FirstOrDefault();
            }

            var result = available.FirstOrDefault();
            if (result == null)
            {
                result = _planetTypes.All.FirstOrDefault();
                Warn(plan, productName + " 没有可用工厂行星类型，已改用通用类型估算");
            }
            return result;
        }

        private string AssignCharacter(List<CharacterSlot> characters, PlanetaryPlan plan)
        {
            var slot = characters.FirstOrDefault(p => p.Used < p.Character.MaxPlanets);
            if (slot == null)
            {
                Warn(plan, "角色可用的行星位不足，部分殖民地无法分配");
                return "未分配";
            }
            slot.Used++;
            return slot.Character.Name;
        }

        private double CalculateSetupCost(
            PlanetTypeProfile planetType,
            PlanetaryPlanRequest request,
            PlanetaryColonySetup setup,
            int factories,
            int extractorHeads,
            int launchpads)
        {
            if (!request.CountSetupCost || planetType?.Facilities == null || request.Price == null)
            {
                return 0;
            }
            var facilities = planetType.Facilities;
            var ids = new List<int> { facilities.CommandCenter };
            for (var i = 0; i < Math.Max(1, launchpads); i++)
            {
                ids.Add(facilities.Launchpad);
            }
            if (setup == PlanetaryColonySetup.ExtractionP1)
            {
                ids.Add(facilities.Extractor);
                for (var i = 0; i < factories; i++)
                {
                    ids.Add(facilities.BasicFactory);
                }
            }
            else if (setup == PlanetaryColonySetup.P3ToP4)
            {
                for (var i = 0; i < factories; i++)
                {
                    ids.Add(facilities.HighTechFactory);
                }
            }
            else
            {
                for (var i = 0; i < factories; i++)
                {
                    ids.Add(facilities.AdvancedFactory);
                }
            }

            double total = 0;
            foreach (var id in ids.Where(p => p > 0).GroupBy(p => p))
            {
                var price = request.Price(id.Key);
                if (price > 0)
                {
                    total += price * id.Count();
                }
            }
            return total;
        }

        private void BuildImports(
            PlanetaryPlan plan,
            PlanetaryPlanRequest request,
            Dictionary<int, double> imports,
            double periodDays)
        {
            if (imports.Count == 0)
            {
                return;
            }
            foreach (var item in imports.OrderBy(p => p.Key))
            {
                var product = _recipes.GetProduct(item.Key);
                var quantityPerDay = item.Value / Math.Max(0.01, periodDays);
                var price = request.Price?.Invoke(item.Key) ?? 0;
                var costPerDay = quantityPerDay * price * (1 + request.PlanetTaxRate);
                plan.Imports.Add(new PlanetaryImportPlan
                {
                    TypeId = item.Key,
                    Name = product?.Name ?? ("Type " + item.Key),
                    Tier = product?.Tier ?? PlanetaryProductTier.P0,
                    QuantityPerPeriod = item.Value,
                    QuantityPerDay = quantityPerDay,
                    UnitPrice = price,
                    CostPerDay = costPerDay,
                });
                if (price <= 0)
                {
                    Warn(plan, "缺少 " + (product?.Name ?? ("Type " + item.Key)) + " 的价格");
                }
            }
        }

        private void BuildSummary(
            PlanetaryPlan plan,
            PlanetaryPlanRequest request,
            List<PlanetaryProduct> targetProducts,
            Dictionary<int, long> targetQuantities,
            Dictionary<int, double> targetOutputPerDay,
            double periodDays,
            int ccuLevel,
            double radius,
            bool usedEstimatedRadius,
            double cycleDays)
        {
            var summary = plan.Summary;
            summary.TargetText = string.Join("、", targetProducts.Select(p =>
                string.Format("{0} × {1}", p.Name, targetQuantities.TryGetValue(p.TypeId, out var qty) ? qty : 0)));
            summary.TargetQuantity = targetQuantities.Values.Sum();
            summary.PeriodDays = periodDays;
            summary.TotalColonies = plan.Colonies.Count;
            summary.ExtractionColonies = plan.Colonies.Count(p => p.Role == "提取");
            summary.P2FactoryColonies = plan.Colonies.Count(p => p.Role == "工厂" && p.Tier == PlanetaryProductTier.P2);
            summary.P3FactoryColonies = plan.Colonies.Count(p => p.Role == "工厂" && p.Tier == PlanetaryProductTier.P3);
            summary.P4FactoryColonies = plan.Colonies.Count(p => p.Role == "工厂" && p.Tier == PlanetaryProductTier.P4);
            summary.CharacterCount = plan.Colonies
                .Select(p => p.CharacterName)
                .Where(p => !string.IsNullOrEmpty(p) && p != "未分配")
                .Distinct()
                .Count();
            summary.SetupCost = plan.Colonies.Sum(p => p.SetupCost);
            summary.CcuLevel = ccuLevel;
            summary.RadiusKm = radius;
            summary.UsedEstimatedRadius = usedEstimatedRadius;
            summary.CycleDays = cycleDays;

            var price = request.Price;
            double revenue = 0;
            var targetPriceKnown = true;
            foreach (var target in targetProducts)
            {
                var unitPrice = price?.Invoke(target.TypeId) ?? 0;
                if (unitPrice <= 0)
                {
                    targetPriceKnown = false;
                    Warning(plan, "缺少 " + target.Name + " 的价格");
                }
                var output = targetOutputPerDay.TryGetValue(target.TypeId, out var value) ? value : 0;
                revenue += output * unitPrice;
                var targetQty = targetQuantities.TryGetValue(target.TypeId, out var qty) ? qty : 0;
                if (output > 0)
                {
                    summary.TargetDays = Math.Max(summary.TargetDays, targetQty / output);
                }
                summary.OutputPerDay += output;
            }

            var importRaw = plan.Imports.Sum(p => p.QuantityPerDay * p.UnitPrice);
            var importTax = importRaw * request.PlanetTaxRate;
            var exportTax = revenue * request.PlanetTaxRate;
            var salesTax = revenue * (request.SalesTaxRate + request.BrokerFeeRate);
            summary.ImportCostPerDay = importRaw + importTax;
            summary.PlanetTaxPerDay = importTax + exportTax;
            summary.SalesTaxPerDay = salesTax;
            summary.RevenuePerDay = revenue;
            summary.ProfitPerDay = revenue - summary.ImportCostPerDay - exportTax - salesTax;
            summary.HasPrice = targetPriceKnown && price != null;
            summary.PaybackDays = summary.ProfitPerDay > 0 && summary.SetupCost > 0
                ? summary.SetupCost / summary.ProfitPerDay
                : 0;
        }

        private string BuildInputText(PlanetaryProduct product, double periodDays)
        {
            if (product.Recipe == null)
            {
                return string.Empty;
            }
            return string.Join("、", product.Recipe.Inputs.Select(p =>
            {
                var input = _recipes.GetProduct(p.TypeID);
                return string.Format("{0} {1:N0}", input?.Name ?? ("Type " + p.TypeID), p.Quantity);
            }));
        }

        private static void Warn(PlanetaryPlan plan, string message)
        {
            if (plan != null && !string.IsNullOrEmpty(message) && !plan.Warnings.Contains(message))
            {
                plan.Warnings.Add(message);
            }
        }

        private static void Warning(PlanetaryPlan plan, string message) => Warn(plan, message);

        private sealed class CharacterSlot
        {
            public PlanetaryCharacterInput Character { get; set; }
            public int Used { get; set; }
        }
    }
}
