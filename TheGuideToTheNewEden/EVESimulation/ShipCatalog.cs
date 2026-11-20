using System;
using System.Collections.Generic;
using System.Linq;

namespace EVESimulation
{
    /// <summary>
    /// 真实舰船 / 装备名对照（中文名 + 英文 hint），
    /// 用于生成和真实客户端一致的 &lt;localized hint="英文"&gt;中文*&lt;/localized&gt;。
    /// </summary>
    public static class ShipCatalog
    {
        public sealed class Ship
        {
            public Ship(string name, string hint)
            {
                Name = name;
                Hint = hint;
            }

            public string Name { get; }

            public string Hint { get; }

            public override string ToString() => $"{Name}（{Hint}）";
        }

        public static IReadOnlyList<Ship> Ships { get; } = new List<Ship>
        {
            new Ship("小鹰级", "Merlin"),
            new Ship("黑鸟级", "Blackbird"),
            new Ship("狂怒者级", "Vexor"),
            new Ship("实践级", "Praxis"),
            new Ship("灵感级", "Gnosis"),
            new Ship("涅鲁斯级", "Nemesis"),
            new Ship("伊毕斯号", "Ibis"),
            new Ship("因卡萨斯级", "Incursus"),
            new Ship("海燕级海军型", "Cormorant Navy Issue"),
            new Ship("末日沙场级海军型", "Armageddon Navy Issue"),
            new Ship("促进级", "Catalyst"),
            new Ship("长尾鲛级", "Thrasher"),
        };

        /// <summary>伤害类（炮/导弹/无人机），每项：中文名, 英文 hint。</summary>
        public static IReadOnlyList<(string Zh, string En)> DamageModules { get; } = new List<(string, string)>
        {
            ("阳极轻型中子加农炮 I", "Anode Light Neutron Particle Cannon I"),
            ("200mm自动加农炮 II", "200mm AutoCannon II"),
            ("重型离子疾速炮 II", "Heavy Ion Blaster II"),
            ("鞭挞轻型导弹", "Scourge Light Missile"),
            ("胡蜂 II", "Vespa II"),
            ("小型聚焦脉冲激光器 II", "Small Focused Pulse Laser II"),
        };

        public static readonly (string Zh, string En) NeutModule = ("小型能量中和器 I", "Small Energy Neutralizer I");

        public static readonly (string Zh, string En) NeutModule2 = ("中型能量中和器 II", "Medium Energy Neutralizer II");

        public static readonly (string Zh, string En) NosModule = ("小型掠能器 I", "Small Energy Nosferatu I");

        public static readonly (string Zh, string En) NosModule2 = ("中型掠能器 II", "Medium Energy Nosferatu II");

        public static readonly (string Zh, string En) ShieldRepModule =
            ("小型辅助远程护盾回充增量器", "Small Ancillary Remote Shield Booster");

        public static readonly (string Zh, string En) ArmorRepModule =
            ("大型远程装甲维修器 II", "Large Remote Armor Repairer II");

        public static readonly (string Zh, string En) ScramModule = ("跃迁扰频器 II", "Warp Scrambler II");

        public static readonly (string Zh, string En) DisruptModule = ("跃迁扰断器 II", "Warp Disruptor II");

        public static readonly (string Zh, string En) EcmModule = ("磁力ECM I", "Magnetometric ECM I");

        public static readonly (string Zh, string En) EcmModule2 = ("多谱干扰器 II", "Multispectral Jammer II");

        public static readonly string[] Qualities = { "命中", "命中", "命中", "强力一击", "轻轻擦过" };

        public static Ship FindShip(string name)
            => Ships.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

        public static string HintOfShip(string name, string fallback = "Merlin")
            => FindShip(name)?.Hint ?? (string.IsNullOrWhiteSpace(fallback) ? "Merlin" : fallback);

        public static (string Zh, string En) RandomDamage(Random random)
            => DamageModules[random.Next(DamageModules.Count)];

        public static string RandomQuality(Random random) => Qualities[random.Next(Qualities.Length)];
    }
}
