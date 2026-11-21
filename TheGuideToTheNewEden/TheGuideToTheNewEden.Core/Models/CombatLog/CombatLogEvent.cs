using System;

namespace TheGuideToTheNewEden.Core.Models.CombatLog
{
    /// <summary>战斗日志事件类型（由日志行结构判定，不依赖客户端语言）。</summary>
    public enum CombatEventKind
    {
        Unknown = 0,

        /// <summary>伤害（炮/导弹/无人机等）。</summary>
        Damage,

        /// <summary>远程维修（遥修）——装甲/护盾/结构。</summary>
        RemoteRepair,

        /// <summary>电容中和（毁电）。</summary>
        CapacitorNeutralize,

        /// <summary>电容吸取（吸电 / NOS）。</summary>
        CapacitorDrain,

        /// <summary>跃迁扰频（反跳）。</summary>
        WarpScramble,

        /// <summary>跃迁扰断 / 跃迁抑阻（长反跳）。</summary>
        WarpDisrupt,

        /// <summary>ECM 干扰。</summary>
        EcmJam,

        /// <summary>停滞缠绕光束（网）。</summary>
        StasisWeb,

        /// <summary>目标标记装置。</summary>
        TargetPainter,

        /// <summary>武器扰断器。</summary>
        WeaponDisrupt,

        /// <summary>远程感应抑阻器。</summary>
        SensorDamp,

        /// <summary>远程电容传输（传电）。</summary>
        RemoteCapacitor,
    }

    /// <summary>事件方向（相对日志所属角色）。</summary>
    public enum CombatEventDirection
    {
        /// <summary>无法判定（多半是中文客户端关键词未覆盖，需要补词表）。</summary>
        Unknown = 0,

        /// <summary>进：别人打你 / 修你 / 干扰你。</summary>
        Incoming,

        /// <summary>出：你打别人 / 你修别人。</summary>
        Outgoing,
    }

    /// <summary>遥修的三种类型（护盾 / 装甲 / 结构）；非遥修事件为 <see cref="None"/>。</summary>
    public enum RemoteRepairType
    {
        None = 0,

        /// <summary>护盾（远程护盾回充增量）。</summary>
        Shield,

        /// <summary>装甲（远程装甲维修）。</summary>
        Armor,

        /// <summary>结构（远程结构维修）。</summary>
        Hull,
    }

    /// <summary>
    /// 一条解析后的战斗日志事件。字段尽量保持"语言无关"（数值、装备名、方向），
    /// 展示文案由上层按分类生成。
    /// </summary>
    public sealed class CombatLogEvent
    {
        /// <summary>日志时间（EVE 时间为 UTC，展示时按需转换）。</summary>
        public DateTime Time { get; set; }

        public CombatEventKind Kind { get; set; }

        public CombatEventDirection Direction { get; set; }

        /// <summary>对方角色名（伤害的袭击者 / 修你的人 / 干扰你的人）。</summary>
        public string Pilot { get; set; }

        public string Corporation { get; set; }

        public string Ship { get; set; }

        /// <summary>装备名（日志里出现在最后一段；伤害行是武器，遥修行是维修器）。中文客户端已去掉末尾 *。</summary>
        public string ModuleName { get; set; }

        /// <summary>中文日志 &lt;localized hint="…"&gt; 里的英文装备名（反查分类优先用它）。</summary>
        public string ModuleHintName { get; set; }

        /// <summary>装备反查到的 TypeID / GroupID（查不到为 0）。</summary>
        public int ModuleTypeId { get; set; }

        public int ModuleGroupId { get; set; }

        /// <summary>数值（GJ / HP / 伤害量）；无值为 null。</summary>
        public double? Amount { get; set; }

        /// <summary>数值单位：GJ / HP / 空（伤害行按 HP 计）。</summary>
        public string Unit { get; set; }

        /// <summary>去掉 HTML 标签后的整行（排错用）。</summary>
        public string RawLine { get; set; } = string.Empty;

        /// <summary>分类 key（由 <c>CombatCategoryResolver</c> 填充）。</summary>
        public string CategoryKey { get; set; }

        /// <summary>遥修类型（护盾 / 装甲 / 结构）；其它分类为 <see cref="RemoteRepairType.None"/>。</summary>
        public RemoteRepairType RepairType { get; set; }
    }
}
