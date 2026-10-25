using System;
using System.Collections.Generic;

namespace TheGuideToTheNewEden.Core.Models.Planetary
{
    public enum PlanetaryPlanMode
    {
        /// <summary>从 P0 提取开始，自行生产每一层。</summary>
        SelfSufficient = 0,
        /// <summary>只生产目标产物，配方输入直接采购。</summary>
        Import = 1,
        /// <summary>自给生产，但用当前殖民产出抵扣需求。</summary>
        Hybrid = 2,
    }

    public sealed class PlanetaryTarget
    {
        public int TypeId { get; set; }
        public long Quantity { get; set; }
    }

    public sealed class PlanetaryCharacterInput
    {
        public long CharacterId { get; set; }
        public string Name { get; set; }
        public int CcuLevel { get; set; } = 5;
        public int MaxPlanets { get; set; } = 6;
    }

    public sealed class PlanetaryPlanRequest
    {
        public List<PlanetaryTarget> Targets { get; set; } = new List<PlanetaryTarget>();
        public double PeriodDays { get; set; } = 7;
        public PlanetaryPlanMode Mode { get; set; } = PlanetaryPlanMode.SelfSufficient;
        public int CcuLevel { get; set; } = 5;
        public int DefaultMaxPlanets { get; set; } = 6;
        /// <summary>行星半径；拿不到真实半径时由调用方传 0，规划器会用保守估值。</summary>
        public double RadiusKm { get; set; }
        public double CycleDays { get; set; } = 4;
        public double PlanetTaxRate { get; set; } = 0.05;
        public double SalesTaxRate { get; set; } = 0.036;
        public double BrokerFeeRate { get; set; }
        public List<PlanetaryCharacterInput> Characters { get; set; } = new List<PlanetaryCharacterInput>();
        /// <summary>物品参考单价；拿不到时返回 0，规划结果会标记价格未知。</summary>
        public Func<int, double> Price { get; set; }
        /// <summary>当前殖民的日产出，仅 Hybrid 模式用于抵扣。</summary>
        public IReadOnlyDictionary<int, long> CurrentSupplyPerDay { get; set; }
        /// <summary>可选：限定在某个星系的可用行星类型；为空时使用全部类型。</summary>
        public IReadOnlyList<PlanetTypeProfile> AvailablePlanetTypes { get; set; }
        public bool CountSetupCost { get; set; } = true;
    }

    public sealed class PlanetaryPlan
    {
        public List<PlanetaryProductPlan> Products { get; set; } = new List<PlanetaryProductPlan>();
        public List<PlanetaryColonyPlan> Colonies { get; set; } = new List<PlanetaryColonyPlan>();
        public List<PlanetaryImportPlan> Imports { get; set; } = new List<PlanetaryImportPlan>();
        public PlanetaryPlanSummary Summary { get; set; } = new PlanetaryPlanSummary();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public sealed class PlanetaryProductPlan
    {
        public int TypeId { get; set; }
        public string Name { get; set; }
        public PlanetaryProductTier Tier { get; set; }
        public double RequiredPerPeriod { get; set; }
        public double RequiredPerDay { get; set; }
        public double OutputPerDay { get; set; }
        public int Factories { get; set; }
        public int FactoriesPerColony { get; set; }
        public int Colonies { get; set; }
        public string PlanetType { get; set; }
        public bool IsTarget { get; set; }
        public bool IsImported { get; set; }
        public string Inputs { get; set; }
        public double SetupCost { get; set; }
    }

    public sealed class PlanetaryColonyPlan
    {
        public string CharacterName { get; set; }
        public string PlanetType { get; set; }
        public string Role { get; set; }
        public int ProductTypeId { get; set; }
        public string ProductName { get; set; }
        public PlanetaryProductTier Tier { get; set; }
        public int Factories { get; set; }
        public int ExtractorHeads { get; set; }
        public double OutputPerDay { get; set; }
        public double InputsPerDay { get; set; }
        public double SetupCost { get; set; }
        public int CcuLevel { get; set; }
        public bool UsedEstimatedRadius { get; set; }
    }

    public sealed class PlanetaryImportPlan
    {
        public int TypeId { get; set; }
        public string Name { get; set; }
        public PlanetaryProductTier Tier { get; set; }
        public double QuantityPerPeriod { get; set; }
        public double QuantityPerDay { get; set; }
        public double UnitPrice { get; set; }
        public double CostPerDay { get; set; }
    }

    public sealed class PlanetaryPlanSummary
    {
        public string TargetText { get; set; }
        public double TargetQuantity { get; set; }
        public double PeriodDays { get; set; } = 7;
        public double TargetDays { get; set; }
        public int TotalColonies { get; set; }
        public int CharacterCount { get; set; }
        public int ExtractionColonies { get; set; }
        public int P1FactoryColonies { get; set; }
        public int P2FactoryColonies { get; set; }
        public int P3FactoryColonies { get; set; }
        public int P4FactoryColonies { get; set; }
        public int FactoryColonies => P1FactoryColonies + P2FactoryColonies + P3FactoryColonies + P4FactoryColonies;
        public int P234FactoryColonies => P2FactoryColonies + P3FactoryColonies + P4FactoryColonies;
        public double OutputPerDay { get; set; }
        public double ImportCostPerDay { get; set; }
        public double PlanetTaxPerDay { get; set; }
        public double SalesTaxPerDay { get; set; }
        public double RevenuePerDay { get; set; }
        public double ProfitPerDay { get; set; }
        public double SetupCost { get; set; }
        public double PaybackDays { get; set; }
        public bool HasPrice { get; set; }
        public double CcuLevel { get; set; }
        public double RadiusKm { get; set; }
        public bool UsedEstimatedRadius { get; set; }
        public double CycleDays { get; set; }
    }
}
