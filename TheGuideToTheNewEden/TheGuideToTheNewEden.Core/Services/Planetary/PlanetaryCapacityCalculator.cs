using System;
using TheGuideToTheNewEden.Core.Models.Planetary;

namespace TheGuideToTheNewEden.Core.Services.Planetary
{
    /// <summary>
    /// 行星殖民地 CPU/PG 产能。常量来自 CCP PI 设施数据（与 eve-pi 的 facilities.yaml 一致）。
    /// 阶段 1 只计算殖民地级产能，不做 pin 坐标/link 拓扑。
    /// </summary>
    public static class PlanetaryCapacityCalculator
    {
        public const double DefaultRadiusKm = 2000;

        private static readonly double[] CcuCpu = { 1675, 7057, 12136, 17215, 21315, 25415 };
        private static readonly double[] CcuPower = { 6000, 9000, 12000, 15000, 17000, 19000 };

        private const double BasicCpu = 200;
        private const double BasicPower = 800;
        private const double AdvancedCpu = 500;
        private const double AdvancedPower = 700;
        private const double HighTechCpu = 1100;
        private const double HighTechPower = 400;
        private const double LaunchpadCpu = 3600;
        private const double LaunchpadPower = 700;
        private const double ExtractorBaseCpu = 400;
        private const double ExtractorBasePower = 2600;
        private const double ExtractorHeadCpu = 110;
        private const double ExtractorHeadPower = 550;

        public static double MinLinkDistance(double radiusKm)
        {
            return Math.Max(0, -0.7716 + 0.012182 * radiusKm);
        }

        public static void LinkCosts(double distanceKm, out double power, out double cpu)
        {
            power = 10.9952 + 0.1433 * distanceKm;
            cpu = 15.6475 + 0.1995 * distanceKm;
        }

        public static int LaunchpadCount(PlanetaryColonySetup setup, int inputCount)
        {
            if (setup == PlanetaryColonySetup.ExtractionP1)
            {
                return 1;
            }
            return Math.Max(1, (int)Math.Ceiling(inputCount / 2.0));
        }

        public static PlanetaryCapacityResult Calculate(
            PlanetTypeProfile planetType,
            int ccuLevel,
            double radiusKm,
            PlanetaryColonySetup setup,
            int inputCount = 2)
        {
            var result = new PlanetaryCapacityResult
            {
                Setup = setup,
                UsedEstimatedRadius = radiusKm <= 0,
            };
            if (planetType == null)
            {
                result.Detail = "未选择行星类型";
                return result;
            }
            if (ccuLevel < 0)
            {
                ccuLevel = 0;
            }
            if (ccuLevel > 5)
            {
                ccuLevel = 5;
            }
            if (setup == PlanetaryColonySetup.P3ToP4 && !planetType.IsP4Capable)
            {
                result.Detail = planetType.Name + " 不能建造 High-Tech 工厂";
                return result;
            }
            if (radiusKm <= 0)
            {
                radiusKm = DefaultRadiusKm;
            }

            result.CpuUsed = 0;
            result.PowerUsed = 0;
            var distance = MinLinkDistance(radiusKm);
            LinkCosts(distance, out var linkPower, out var linkCpu);
            var linkPowerPerFactory = linkPower * 1.5;
            var linkCpuPerFactory = linkCpu * 1.5;
            var availableCpu = CcuCpu[ccuLevel];
            var availablePower = CcuPower[ccuLevel];

            if (setup == PlanetaryColonySetup.ExtractionP1)
            {
                // 1 LP + ECU base + heads + basic factories.
                var lpCpu = LaunchpadCpu;
                var lpPower = LaunchpadPower;
                var remainingCpu = availableCpu - lpCpu;
                var remainingPower = availablePower - lpPower;
                if (remainingCpu <= 0 || remainingPower <= 0)
                {
                    result.Detail = "Launchpad 已超过 CCU 容量";
                    return result;
                }

                remainingCpu -= ExtractorBaseCpu + linkCpuPerFactory;
                remainingPower -= ExtractorBasePower + linkPowerPerFactory;
                if (remainingCpu <= 0 || remainingPower <= 0)
                {
                    result.Detail = "ECU 超过 CCU 剩余容量";
                    return result;
                }

                var bestFactories = 0;
                var bestHeads = 0;
                var costCpu = BasicCpu + linkCpuPerFactory;
                var costPower = BasicPower + linkPowerPerFactory;
                for (var heads = 1; heads <= 10; heads++)
                {
                    var factoryCpuLeft = remainingCpu - ExtractorHeadCpu * heads;
                    var factoryPowerLeft = remainingPower - ExtractorHeadPower * heads;
                    if (factoryCpuLeft <= 0 || factoryPowerLeft <= 0)
                    {
                        break;
                    }
                    var maxByCpu = (int)(factoryCpuLeft / costCpu);
                    var maxByPower = (int)(factoryPowerLeft / costPower);
                    var factories = Math.Min(Math.Min(maxByCpu, maxByPower), heads);
                    if (factories > bestFactories)
                    {
                        bestFactories = factories;
                        bestHeads = heads;
                    }
                }

                result.MaxFactories = bestFactories;
                result.ExtractorHeads = bestHeads;
                result.Launchpads = 1;
                result.Fits = bestFactories > 0;
                result.CpuUsed = lpCpu + ExtractorBaseCpu + ExtractorHeadCpu * bestHeads + BasicCpu * bestFactories;
                result.PowerUsed = lpPower + ExtractorBasePower + ExtractorHeadPower * bestHeads + BasicPower * bestFactories;
                result.Detail = result.Fits
                    ? string.Format("提取：{0} heads / {1} basic factories", bestHeads, bestFactories)
                    : "无法容纳提取殖民地";
                return result;
            }

            var factoryCpu = setup == PlanetaryColonySetup.P3ToP4 ? HighTechCpu : AdvancedCpu;
            var factoryPower = setup == PlanetaryColonySetup.P3ToP4 ? HighTechPower : AdvancedPower;
            var factoryName = setup == PlanetaryColonySetup.P3ToP4 ? "High-Tech" : "Advanced";
            var lpCount = LaunchpadCount(setup, inputCount);

            // Reserve LP + its link, then add factories each with a link.
            var reservedCpu = LaunchpadCpu * lpCount + linkCpuPerFactory * lpCount;
            var reservedPower = LaunchpadPower * lpCount + linkPowerPerFactory * lpCount;
            var leftCpu = availableCpu - reservedCpu;
            var leftPower = availablePower - reservedPower;
            if (leftCpu <= 0 || leftPower <= 0)
            {
                result.Detail = "Launchpad 已超过 CCU 容量";
                return result;
            }

            var perFactoryCpu = factoryCpu + linkCpuPerFactory;
            var perFactoryPower = factoryPower + linkPowerPerFactory;
            var maxByFactoryCpu = (int)(leftCpu / perFactoryCpu);
            var maxByFactoryPower = (int)(leftPower / perFactoryPower);
            result.MaxFactories = Math.Min(maxByFactoryCpu, maxByFactoryPower);
            result.Launchpads = lpCount;
            result.Fits = result.MaxFactories > 0;
            result.CpuUsed = reservedCpu + perFactoryCpu * result.MaxFactories;
            result.PowerUsed = reservedPower + perFactoryPower * result.MaxFactories;
            result.Detail = result.Fits
                ? string.Format("{0}：{1} 个工厂（CCU {2}，{3} Launchpad）", factoryName, result.MaxFactories, ccuLevel, lpCount)
                : "CCU 容量不足以容纳工厂";
            return result;
        }
    }
}
