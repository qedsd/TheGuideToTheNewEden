using System;
using System.IO;
using System.Text;

namespace EVESimulation
{
    /// <summary>
    /// 无界面自检：按真实行式生成一份游戏日志（全部事件 × 进/出向），
    /// 供主程序的 CombatLogProbe 回归解析。命令行：<c>exefile.exe --selftest D:\temp\dir</c>。
    /// </summary>
    public static class SelfTest
    {
        public static void Generate(string root)
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }

            Directory.CreateDirectory(root);

            var self = new CharacterConfig
            {
                Listener = "QEDSD",
                ListenerID = 2113594914,
                Corporation = "I K N",
                ShipName = "伊毕斯号",
                ShipHint = "Ibis",
            };

            var actor = new CharacterConfig
            {
                Listener = "Arya D Stark",
                ListenerID = 2112494169,
                Corporation = "I K N",
                ShipName = "黑鸟级",
                ShipHint = "Blackbird",
            };

            var settings = new CombatSimulationSettings
            {
                DamageMin = 100,
                DamageMax = 200,
                NeutGj = 45,
                NosGj = 15,
                RepairHp = 64,
            };

            var log = new GameLog { Listener = self.Listener, ListenerID = self.ListenerID };
            log.Init(root);

            var random = new Random(20261120);
            var time = DateTime.Now;
            var index = 0;

            foreach (CombatEventType type in Enum.GetValues(typeof(CombatEventType)))
            {
                log.Write(time.AddSeconds(index++).ToString("yyyy.MM.dd HH:mm:ss"),
                    CombatLogSimulator.Build(self, actor, type, settings, random, outgoing: false));
                log.Write(time.AddSeconds(index++).ToString("yyyy.MM.dd HH:mm:ss"),
                    CombatLogSimulator.Build(self, actor, type, settings, random, outgoing: true));
            }

            for (var i = 0; i < 10; i++)
            {
                log.Write(time.AddSeconds(index++).ToString("yyyy.MM.dd HH:mm:ss"),
                    CombatLogSimulator.BuildAuto(self, actor, settings, random));
            }

            log.Dispose();

            var builder = new StringBuilder();
            builder.AppendLine($"self = {self.Listener} ({self.ListenerID})");
            builder.AppendLine($"actor = {actor.Listener} ({actor.ListenerID})");
            builder.AppendLine($"root = {root}");
            File.WriteAllText(Path.Combine(root, "selftest.txt"), builder.ToString(), Encoding.UTF8);
        }
    }
}
