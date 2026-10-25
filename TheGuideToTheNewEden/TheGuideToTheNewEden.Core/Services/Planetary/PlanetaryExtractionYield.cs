using System;
using System.Collections.Generic;

namespace TheGuideToTheNewEden.Core.Services.Planetary
{
    /// <summary>
    /// CCP 提取衰减与程序周期。用于把“提取多久”折算成有效 P1/小时。
    /// </summary>
    public static class PlanetaryExtractionYield
    {
        public const double DecayFactor = 0.012;
        public const double NoiseFactor = 0.8;
        public const int DefaultQtyPerCycle = 6965;

        public static int ProgramCycleSeconds(double durationHours)
        {
            if (durationHours <= 25) return 1800;
            if (durationHours <= 50) return 3600;
            if (durationHours <= 100) return 7200;
            if (durationHours <= 200) return 14400;
            return 28800;
        }

        public static long TotalYield(int qtyPerCycle, double durationHours, bool simplified = true)
        {
            if (qtyPerCycle <= 0 || durationHours <= 0)
            {
                return 0;
            }
            var cycleSeconds = ProgramCycleSeconds(durationHours);
            var totalCycles = (int)(durationHours * 3600 / cycleSeconds);
            long total = 0;
            for (var cycle = 0; cycle < totalCycles; cycle++)
            {
                total += CycleOutput(qtyPerCycle, cycleSeconds, cycle, simplified);
            }
            return total;
        }

        public static double EffectiveHourlyRate(int qtyPerCycle, double durationHours, bool simplified = true)
        {
            if (durationHours <= 0)
            {
                return 0;
            }
            return TotalYield(qtyPerCycle, durationHours, simplified) / durationHours;
        }

        public static double YieldRatioVsBaseline(
            double durationHours,
            double baselineHours = 24,
            int qtyPerCycle = DefaultQtyPerCycle,
            bool simplified = true)
        {
            var baseline = EffectiveHourlyRate(qtyPerCycle, baselineHours, simplified);
            if (baseline <= 0)
            {
                return 0;
            }
            return EffectiveHourlyRate(qtyPerCycle, durationHours, simplified) / baseline;
        }

        public static int CycleOutput(int qtyPerCycle, int cycleSeconds, int cycleIndex, bool simplified)
        {
            var barWidth = cycleSeconds / 900.0;
            var t = (cycleIndex + 0.5) * barWidth;
            var decayValue = qtyPerCycle / (1.0 + t * DecayFactor);
            if (simplified)
            {
                return (int)(barWidth * decayValue);
            }

            var phaseShift = Math.Pow(qtyPerCycle, 0.7);
            var sinA = Math.Cos(phaseShift + t / 12.0);
            var sinB = Math.Cos(phaseShift / 2.0 + t * 0.2);
            var sinC = Math.Cos(t * 0.5);
            var noise = Math.Max((sinA + sinB + sinC) / 3.0, 0);
            var barHeight = decayValue * (1 + NoiseFactor * noise);
            return (int)(barWidth * barHeight);
        }
    }
}
