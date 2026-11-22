using System;
using System.Linq;

namespace TheGuideToTheNewEden.Core.Models.CombatLog
{
    /// <summary>伤害属性（阶段 378：只认日志里**明确写了**的导弹 / 无人机 / 炸弹）。</summary>
    public enum DamageType
    {
        None = 0,
        Em,
        Thermal,
        Kinetic,
        Explosive,
    }

    /// <summary>
    /// 伤害属性识别：日志里没有属性字段，只有武器/弹药名。只有当名字本身就是**具体弹药或无人机**
    /// 时才返回明确属性；炮台（激光/混合/射弹）一律返回 <see cref="DamageType.None"/>，不做推断。
    /// </summary>
    public static class DamageTypeResolver
    {
        private static readonly (DamageType Type, string[] Words)[] Rules =
        {
            (DamageType.Em, new[]
            {
                "mjolnir", "雷神", "acolyte", "warrior", "侍僧", "战士",
            }),
            (DamageType.Thermal, new[]
            {
                "inferno", "灼烧", "狱火", "hobgoblin", "hammerhead", "地精", "锤头",
            }),
            (DamageType.Kinetic, new[]
            {
                "nova", "新星", "hornet", "wasp", "黄蜂", "胡蜂",
            }),
            (DamageType.Explosive, new[]
            {
                "scourge", "灾祸", "天灾", "valkyrie", "女武神",
            }),
        };

        /// <summary>武器 / 弹药名 → 明确伤害属性；无法确定返回 <see cref="DamageType.None"/>。</summary>
        public static DamageType Resolve(string module, string hint)
        {
            var text = string.Join(" ", new[] { hint, module }.Where(p => !string.IsNullOrWhiteSpace(p)));
            if (text.Length == 0)
            {
                return DamageType.None;
            }

            foreach (var (type, words) in Rules)
            {
                foreach (var word in words)
                {
                    if (text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return type;
                    }
                }
            }

            return DamageType.None;
        }
    }
}
