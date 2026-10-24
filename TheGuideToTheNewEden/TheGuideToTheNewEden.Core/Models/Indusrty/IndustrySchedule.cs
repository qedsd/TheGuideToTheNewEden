using System;
using System.Collections.Generic;

namespace TheGuideToTheNewEden.Core.Models.Indusrty
{
    /// <summary>排产优化目标。</summary>
    public enum IndustryScheduleObjective
    {
        Makespan = 0,
        Cost = 1,
        Balanced = 2,
    }

    /// <summary>
    /// 一个角色的工业能力：槽位来自技能，时间修正来自技能/植入体。
    /// 多角色总产能 = Σ 各角色实际槽位，不是角色数 × 11。
    /// </summary>
    public sealed class IndustryCharacterProfile
    {
        public long CharacterId { get; set; }
        public string CharacterName { get; set; } = string.Empty;

        public int ManufacturingSlots { get; set; } = 1;
        public int InventionSlots { get; set; } = 1;
        public int ReactionSlots { get; set; } = 1;

        public double ManufacturingTimeMultiplier { get; set; } = 1.0;
        public double ReactionTimeMultiplier { get; set; } = 1.0;
        public double InventionTimeMultiplier { get; set; } = 1.0;

        /// <summary>预留：不同建筑/税制造成的每角色成本系数。</summary>
        public double CostMultiplier { get; set; } = 1.0;

        public bool SkillsLoaded { get; set; }
        public string Source { get; set; } = string.Empty;

        public int TotalSlots => ManufacturingSlots + InventionSlots + ReactionSlots;

        public int SlotsForActivity(int activityId)
        {
            return activityId switch
            {
                8 => InventionSlots,
                11 => ReactionSlots,
                _ => ManufacturingSlots,
            };
        }

        public double TimeMultiplierForActivity(int activityId)
        {
            return activityId switch
            {
                8 => InventionTimeMultiplier,
                11 => ReactionTimeMultiplier,
                _ => ManufacturingTimeMultiplier,
            };
        }
    }

    public sealed class IndustryScheduleMaterial
    {
        public int TypeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public long Quantity { get; set; }
        /// <summary>buy / stock / upstream / target / downstream。</summary>
        public string StateKey { get; set; } = string.Empty;
        public string StateText { get; set; } = string.Empty;
    }

    public sealed class IndustryScheduleJob
    {
        /// <summary>聚合键：TypeID:ActivityID。</summary>
        public string Key { get; set; } = string.Empty;

        public int TypeId { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public int ActivityId { get; set; }
        public string ActivityKey { get; set; } = string.Empty;
        public string ActivityText { get; set; } = string.Empty;
        public bool IsInvention { get; set; }
        public bool IsTarget { get; set; }
        public bool IsInProgress { get; set; }
        public string Stage { get; set; } = string.Empty;

        public int Runs { get; set; }
        public long QuantityProduced { get; set; }
        public double BaseHours { get; set; }
        public double DurationHours { get; set; }
        public double StartHours { get; set; }
        public double EndHours { get; set; }

        public long CharacterId { get; set; }
        public string CharacterName { get; set; } = string.Empty;
        public string LineId { get; set; } = string.Empty;
        public string LineKind { get; set; } = string.Empty;
        public int LineIndex { get; set; }
        public double TimeMultiplier { get; set; } = 1.0;

        public bool IsCritical { get; set; }
        public bool IsPinned { get; set; }

        public double JobCost { get; set; }
        public double MaterialCost { get; set; }

        public List<IndustryScheduleMaterial> Inputs { get; set; } = new List<IndustryScheduleMaterial>();
        public List<IndustryScheduleMaterial> Outputs { get; set; } = new List<IndustryScheduleMaterial>();
        public List<string> Dependencies { get; set; } = new List<string>();
        public List<string> Dependents { get; set; } = new List<string>();
    }

    public sealed class IndustryScheduleLine
    {
        public string Id { get; set; } = string.Empty;
        /// <summary>制造 / 发明 / 反应。</summary>
        public string Kind { get; set; } = string.Empty;
        public int Index { get; set; }
        public List<string> JobKeys { get; set; } = new List<string>();
        public double BusyHours { get; set; }
    }

    public sealed class IndustryScheduleCharacter
    {
        public long CharacterId { get; set; }
        public string CharacterName { get; set; } = string.Empty;
        public int ManufacturingSlots { get; set; }
        public int InventionSlots { get; set; }
        public int ReactionSlots { get; set; }
        public double ManufacturingTimeMultiplier { get; set; } = 1.0;
        public double ReactionTimeMultiplier { get; set; } = 1.0;
        public double InventionTimeMultiplier { get; set; } = 1.0;
        public string Source { get; set; } = string.Empty;
        public bool SkillsLoaded { get; set; }

        public List<IndustryScheduleLine> Lines { get; set; } = new List<IndustryScheduleLine>();
        public double UsedSlotHours { get; set; }
        public double AvailableSlotHours { get; set; }
        public double UtilizationPercent { get; set; }
        public int TotalSlots => ManufacturingSlots + InventionSlots + ReactionSlots;
    }

    /// <summary>一次排产结果：任务 = 聚合后的制造/发明/反应作业。</summary>
    public sealed class IndustrySchedule
    {
        public List<IndustryScheduleCharacter> Characters { get; set; } = new List<IndustryScheduleCharacter>();
        public List<IndustryScheduleJob> Jobs { get; set; } = new List<IndustryScheduleJob>();
        public List<string> CriticalJobKeys { get; set; } = new List<string>();

        public double MakespanHours { get; set; }
        public double TotalJobCost { get; set; }
        public double TotalMaterialCost { get; set; }
        public IndustryScheduleObjective Objective { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
    }
}
