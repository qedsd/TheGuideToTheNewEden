using System.Collections.Generic;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Models.Indusrty
{
    /// <summary>官方工业 modifier 的作用类型。</summary>
    public static class IndustryModifierType
    {
        public const int Cost = 1;
        public const int Time = 2;
        public const int Material = 3;
    }

    /// <summary>某个结构在某活动上的官方倍率；1.0 = 无加成。</summary>
    public sealed class IndustryActivityModifier
    {
        public double Cost { get; set; } = 1.0;
        public double Time { get; set; } = 1.0;
        public double Material { get; set; } = 1.0;
    }

    /// <summary>从 industryModifierSources + typeDogma 解析出的结构官方加成。</summary>
    public sealed class IndustryStructureModifiers
    {
        public int StructureTypeId { get; set; }
        public string StructureName { get; set; } = string.Empty;
        /// <summary>官方活动 ID → 倍率（反应为 9）。</summary>
        public Dictionary<int, IndustryActivityModifier> Activities { get; set; } = new Dictionary<int, IndustryActivityModifier>();
    }

    /// <summary>建筑档案可选的结构类型，以及官方加成摘要。</summary>
    public sealed class IndustryStructureOption
    {
        public int TypeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public double MaterialMultiplier { get; set; } = 1.0;
        public double TimeMultiplier { get; set; } = 1.0;
        public double CostMultiplier { get; set; } = 1.0;
        /// <summary>语言无关的加成摘要（M=材料 / T=时间 / C=费用）；界面文案由 UI 层生成。</summary>
        public string Summary =>
            $"M {Format(MaterialMultiplier)} · T {Format(TimeMultiplier)} · C {Format(CostMultiplier)}";

        private static string Format(double multiplier)
        {
            double percent = (multiplier - 1.0) * 100.0;
            return $"{percent:+0.#;-0.#;0}%";
        }
    }

    /// <summary>官方目标过滤器（分类 + 组）。</summary>
    public sealed class IndustryTargetFilterInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public HashSet<int> CategoryIds { get; set; } = new HashSet<int>();
        public HashSet<int> GroupIds { get; set; } = new HashSet<int>();
    }

    public sealed class IndustryAssemblyLineInfo
    {
        public IndustryAssemblyLine Line { get; set; }
        public List<IndustryAssemblyLineDetail> Details { get; set; } = new List<IndustryAssemblyLineDetail>();
    }
}
