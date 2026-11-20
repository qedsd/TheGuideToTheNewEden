using System;
using System.Collections.Generic;
using System.Linq;

namespace EVESimulation
{
    public enum CombatEventType
    {
        Damage,
        Miss,
        Neut,
        Nos,
        Repair,
        Scram,
        Ecm,
    }

    /// <summary>
    /// 按真实中文客户端日志行式生成 <c>(combat)</c> 行（2026-11-19 日志校准）：
    /// 模块/舰船名用 <c>&lt;localized hint="英文"&gt;中文*&lt;/localized&gt;</c>，
    /// 行首 <c>(combat)</c> 由这里加，时间前缀由 <see cref="GameLog.Write"/> 加。
    /// </summary>
    public static class CombatLogSimulator
    {
        public static string Localized((string Zh, string En) name)
            => $"<localized hint=\"{name.En}\">{name.Zh}*</localized>";

        public static string ShipTag(CharacterConfig character)
        {
            var hint = ShipCatalog.HintOfShip(character.ShipName, character.ShipHint);
            return $"<localized hint=\"{hint}\">{character.ShipName}*</localized>";
        }

        /// <summary>舰船 [军团] [角色]（真实日志里反跳/毁电/遥修的"来源"就是这个形状）。</summary>
        public static string ActorTag(CharacterConfig character)
            => $"{ShipTag(character)} [{Corp(character)}] [{character.Listener}]";

        private static string Corp(CharacterConfig character)
            => string.IsNullOrWhiteSpace(character.Corporation) ? "I K N" : character.Corporation.Trim();

        /// <summary>手动生成一条指定类型的事件行。</summary>
        public static string Build(
            CharacterConfig self,
            CharacterConfig actor,
            CombatEventType type,
            CombatSimulationSettings settings,
            Random random,
            bool outgoing = false)
        {
            self ??= new CharacterConfig { Listener = "QEDSD" };
            actor ??= new CharacterConfig { Listener = "Arya D Stark", ShipName = "黑鸟级", ShipHint = "Blackbird" };
            settings ??= new CombatSimulationSettings();
            random ??= new Random();

            var line = type switch
            {
                CombatEventType.Damage => outgoing
                    ? $"{Amount(settings.DamageMin, settings.DamageMax, random)} 对 {actor.Listener} - {DamageModule(random)} - {ShipCatalog.RandomQuality(random)}"
                    : $"{Amount(settings.DamageMin, settings.DamageMax, random)} 来自 {ActorTag(actor)} - {DamageModule(random)} - {ShipCatalog.RandomQuality(random)}",

                CombatEventType.Miss => outgoing
                    ? $"你的一组 {Strip(Localized(ShipCatalog.RandomDamage(random)))}完全没有打中{actor.Listener} - {DamageModule(random)}"
                    : $"{actor.Listener}完全没有打中你 - {DamageModule(random)}",

                CombatEventType.Neut => outgoing
                    ? $"{settings.NeutGj} GJ能量中和{self.Listener} - {Localized(ShipCatalog.NeutModule)}"
                    : $"{settings.NeutGj} GJ能量中和 {ActorTag(actor)} - - {Localized(ShipCatalog.NeutModule)}",

                CombatEventType.Nos => outgoing
                    ? $"+{settings.NosGj} GJ被从{actor.Listener} ({ShipTag(actor)})吸取 - {Localized(ShipCatalog.NosModule)}"
                    : $"-{settings.NosGj} GJ被吸取到 {ActorTag(actor)} - - {Localized(ShipCatalog.NosModule)}",

                CombatEventType.Repair => outgoing
                    ? $"{settings.RepairHp}远程护盾回充增量至{actor.Listener} ({ShipTag(actor)}) - {Localized(ShipCatalog.ShieldRepModule)}"
                    : $"{settings.RepairHp}远程护盾回充增量由 {ActorTag(actor)} - - {Localized(ShipCatalog.ShieldRepModule)}",

                CombatEventType.Scram => outgoing
                    ? $"你 - 试图跃迁扰频 {actor.Listener}"
                    : $"来自 {ActorTag(actor)} - 试图跃迁扰频 你！",

                CombatEventType.Ecm => outgoing
                    ? $"{Localized(ShipCatalog.EcmModule)}对 {actor.Listener} -干扰"
                    : $"{actor.Listener} ({ShipTag(actor)})的{Localized(ShipCatalog.EcmModule)}正在干扰你",

                _ => string.Empty,
            };

            return $"(combat) {line}";
        }

        /// <summary>自动战斗：按权重挑一个已启用的事件（可带出向）。</summary>
        public static string BuildAuto(
            CharacterConfig self,
            CharacterConfig actor,
            CombatSimulationSettings settings,
            Random random)
        {
            var pool = new List<(CombatEventType Type, int Weight)>();
            if (settings.Damage) pool.Add((CombatEventType.Damage, 55));
            if (settings.Miss) pool.Add((CombatEventType.Miss, 10));
            if (settings.Neut) pool.Add((CombatEventType.Neut, 12));
            if (settings.Nos) pool.Add((CombatEventType.Nos, 6));
            if (settings.Repair) pool.Add((CombatEventType.Repair, 12));
            if (settings.Scram) pool.Add((CombatEventType.Scram, 2));
            if (settings.Ecm) pool.Add((CombatEventType.Ecm, 3));

            if (pool.Count == 0)
            {
                return null;
            }

            var total = pool.Sum(p => p.Weight);
            var roll = random.Next(total);
            var type = pool[0].Type;
            foreach (var item in pool)
            {
                if (roll < item.Weight)
                {
                    type = item.Type;
                    break;
                }

                roll -= item.Weight;
            }

            var outgoing = settings.IncludeOutgoing && random.NextDouble() < 0.2;
            return Build(self, actor, type, settings, random, outgoing);
        }

        private static string DamageModule(Random random) => Localized(ShipCatalog.RandomDamage(random));

        private static int Amount(int min, int max, Random random)
        {
            if (min < 1)
            {
                min = 1;
            }

            if (max < min)
            {
                max = min;
            }

            return random.Next(min, max + 1);
        }

        /// <summary>去掉 localized 包装（用于"你的一组 xx"这种直接写模块名的行）。</summary>
        private static string Strip(string localized)
        {
            var start = localized.IndexOf('>');
            var end = localized.LastIndexOf("</localized>", StringComparison.Ordinal);
            return start >= 0 && end > start ? localized.Substring(start + 1, end - start - 1) : localized;
        }
    }
}
