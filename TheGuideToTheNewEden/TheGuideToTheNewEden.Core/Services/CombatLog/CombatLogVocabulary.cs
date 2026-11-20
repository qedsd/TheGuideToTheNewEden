using System;
using System.Linq;

namespace TheGuideToTheNewEden.Core.Services.CombatLog
{
    /// <summary>
    /// 战斗日志识别用的**多语言词表**（阶段 357）。
    /// <para>
    /// 英文客户端与中文客户端的正文/装备名不同，词表把两种（以及以后补充的语言）都放进来，
    /// 命中任一即可。拿到真实日志后**只改这里**，解析逻辑不用动。
    /// </para>
    /// <para>英文词按词边界匹配，中文词按包含匹配（见 <c>CombatLogParser.IndexOfWord</c>）。</para>
    /// </summary>
    public static class CombatLogVocabulary
    {
        /// <summary>表示"来源/施加者"的介词（进）。</summary>
        public static readonly string[] FromWords =
        {
            "from", "by", "来自", "从", "由", "被",
        };

        /// <summary>表示"目标"的介词（出）。</summary>
        public static readonly string[] ToWords =
        {
            "to", "对", "向", "到", "给", "至",
        };

        /// <summary>passive/来源介词（遥修、传电用 by/被 表示"别人给你"）。</summary>
        public static readonly string[] ByWords =
        {
            "by", "被", "由",
        };

        /// <summary>日志里代表"日志所属角色自己"的词。</summary>
        public static readonly string[] YouWords =
        {
            "you", "your", "你自己", "你", "您", "您自己",
        };

        /// <summary>所有介词（用于截断"from X to you"里的 X）。</summary>
        public static readonly string[] AllPrepositions =
            FromWords.Concat(ToWords).Distinct().ToArray();

        /// <summary>跃迁扰断 / 扰频（反跳）。</summary>
        public static readonly string[] ScramWords =
        {
            "warp scramble", "warp scrambl", "warp disruption", "warp disrupt",
            "跃迁扰断", "跃迁扰频", "跃迁干扰", "反跳", "扰频器", "扰断器",
        };

        /// <summary>ECM 干扰。</summary>
        public static readonly string[] EcmWords =
        {
            "jammed", "jammer", "ecm", "electronic counter", "电子对抗", "干扰",
        };

        /// <summary>远程维修（遥修）。</summary>
        public static readonly string[] RepairWords =
        {
            "remote armor", "remote shield", "remote hull", "repaired", "boosted",
            "远程装甲", "远程护盾", "远程结构", "远程维修", "遥修", "维修",
        };

        /// <summary>远程电容传输（传电）。</summary>
        public static readonly string[] CapacitorTransferWords =
        {
            "remote capacitor", "capacitor transmitted", "capacitor transferred",
            "远程电容", "电容传输", "传电", "传电容",
        };

        /// <summary>电容中和（毁电）。</summary>
        public static readonly string[] NeutralizeWords =
        {
            "neutralized", "neutralizes", "neutralize", "中和", "毁电",
        };

        /// <summary>电容吸取（吸电 / NOS）。</summary>
        public static readonly string[] DrainWords =
        {
            "drained", "drains", "nosferatu", "吸血", "吸取", "吸电",
        };

        /// <summary>未命中（没有伤害数值，直接忽略）。</summary>
        public static readonly string[] MissWords =
        {
            "完全没有打中", "没有打中", "未命中", "misses", "miss",
        };

        /// <summary>命中质量（伤害行的最后一段）。</summary>
        public static readonly string[] QualityWords =
        {
            "hits", "grazes", "glances off", "penetrates", "smashes", "wrecks", "misses", "miss",
            "命中", "轻轻擦过", "擦过", "掠过", "擦边", "穿透", "击穿", "强力一击",
            "完美一击", "毁灭一击", "毁灭", "未命中", "偏斜", "偏转",
        };

        /// <summary>判断一段文本是否是命中质量。</summary>
        public static bool IsQuality(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                return false;
            }

            var text = segment.Trim();
            return QualityWords.Any(p => string.Equals(p, text, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>判断实体文本是否就是"你"。</summary>
        public static bool IsYou(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var value = text.Trim();
            return YouWords.Any(p => string.Equals(p, value, StringComparison.OrdinalIgnoreCase));
        }
    }
}
