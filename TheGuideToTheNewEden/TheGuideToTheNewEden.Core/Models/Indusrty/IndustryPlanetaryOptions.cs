using System;
using System.Collections.Generic;
using TheGuideToTheNewEden.Core.Models.Planetary;

namespace TheGuideToTheNewEden.Core.Models.Indusrty
{
    /// <summary>行星产能口径（阶段 277）。</summary>
    public enum IndustryPlanetarySizingMode
    {
        /// <summary>
        /// 按可用殖民地（默认）：先按"1 天产出需求数量"算出每个殖民地的日产能，再用
        /// 角色数 × 每角色殖民地（最多 6）反推需要几个殖民地、多少天——殖民地越多工期越短。
        /// </summary>
        AvailableColonies = 1,

        /// <summary>按产出周期：给定"在 N 天内出齐"，由规划器凑出所需殖民地数（周期越短所需殖民地越多）。</summary>
        FitPeriod = 0,
    }

    /// <summary>
    /// 制造计划里的「行星开发」口径（阶段 276）。
    /// <para>
    /// 打开 <see cref="Enabled"/> 后，BOM 里的行星产物不再按"一条线串行跑完所有周期"估算，
    /// 而是交给 <c>PlanetaryPlanner</c> 用真实殖民地模型折算：需要几个殖民地、每天产多少、
    /// 建造成本、POCO 出口税、进口中间品成本，并把排程的行星产线按殖民地产能展开。
    /// </para>
    /// </summary>
    public sealed class IndustryPlanetaryOptions
    {
        /// <summary>true = 使用真实殖民地模型（默认）；false = 旧的"单条行星线"估算。</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>产能口径：按可用殖民地（默认）/ 按产出周期。</summary>
        public IndustryPlanetarySizingMode SizingMode { get; set; } = IndustryPlanetarySizingMode.AvailableColonies;

        /// <summary>产出周期（天）：仅 <see cref="IndustryPlanetarySizingMode.FitPeriod"/> 口径使用。</summary>
        public double PeriodDays { get; set; } = 7;

        /// <summary>指挥中心等级 0–5（影响 CPU/PG 与殖民地容量）。默认 5。</summary>
        public int CcuLevel { get; set; } = 5;

        /// <summary>每个角色最多几个殖民地（游戏上限 6）。</summary>
        public int MaxPlanetsPerCharacter { get; set; } = 6;

        /// <summary>行星半径（km）；0 = 用规划器的保守估算值。</summary>
        public double RadiusKm { get; set; }

        /// <summary>是否把殖民地建造成本（CC 升级 + 设施）计入该节点成本。默认计入。</summary>
        public bool CountSetupCost { get; set; } = true;

        /// <summary>生产模式：自给（从 P0 提取链路自建）/ 只产目标产物（输入直接进口）。</summary>
        public PlanetaryPlanMode Mode { get; set; } = PlanetaryPlanMode.SelfSufficient;

        /// <summary>参与规划的角色（空 = 规划器按默认行星位自动生成占位角色）。</summary>
        public List<PlanetaryCharacterInput> Characters { get; set; } = new List<PlanetaryCharacterInput>();

        /// <summary>候选行星（空 = 不做"具体行星"约束，按行星类型 + 半径估算）。</summary>
        public List<PlanetaryPlanetInput> Planets { get; set; } = new List<PlanetaryPlanetInput>();

        /// <summary>复制一份（求解过程中会被规划器读取但不会修改，这里主要防外部改动）。</summary>
        public IndustryPlanetaryOptions Clone() => new IndustryPlanetaryOptions
        {
            Enabled = Enabled,
            SizingMode = SizingMode,
            PeriodDays = PeriodDays,
            CcuLevel = CcuLevel,
            MaxPlanetsPerCharacter = MaxPlanetsPerCharacter,
            RadiusKm = RadiusKm,
            CountSetupCost = CountSetupCost,
            Mode = Mode,
            Characters = new List<PlanetaryCharacterInput>(Characters ?? new List<PlanetaryCharacterInput>()),
            Planets = new List<PlanetaryPlanetInput>(Planets ?? new List<PlanetaryPlanetInput>()),
        };
    }
}
