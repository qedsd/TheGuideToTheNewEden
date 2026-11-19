using System.Text;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.Core.Models.CombatLog;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.Core.Services.CombatLog;
using TheGuideToTheNewEden.Core.Services.DB;

namespace CombatLogProbe;

/// <summary>
/// 战斗日志解析探针（阶段 357）：
/// ① 用样本行回归 <see cref="CombatLogParser"/>（英文 + 中文结构样本）；
/// ② 用真实 SDE 校验装备 → GroupID → 分类（英文名 + 本地化中文名）；
/// ③ 传一个 Gamelogs 目录时，扫描并抽取真实战斗行（含未能识别的行），用于补词表。
/// </summary>
internal static class Program
{
    private static readonly StringBuilder Log = new();
    private static int _failed;

    private static int Main(string[] args)
    {
        InitCore();

        RunParserChecks();
        RunResolverChecks();

        if (args.Length > 0 && Directory.Exists(args[0]))
        {
            ScanFolder(args[0]);
        }

        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "combat-probe.log"), Log.ToString(), Encoding.UTF8);
        Console.WriteLine(Log.ToString());
        Console.WriteLine(_failed == 0 ? "ALL PASS" : $"FAILED: {_failed}");
        return _failed == 0 ? 0 : 1;
    }

    private static void InitCore()
    {
        var repo = FindRepositoryRoot();
        var database = Path.Combine(repo, "TheGuideToTheNewEden", "TheGuideToTheNewEden.WPF", "Resources", "Database");
        Config.DBPath = Path.Combine(database, "main.db");
        Config.StaticDBPath = Path.Combine(database, "static.db");
        Config.CacheDBPath = Path.Combine(Path.GetTempPath(), "combat-probe-cache.db");
        var local = Path.Combine(database, "Local", "zh.db");
        Config.LocalDBPath = File.Exists(local) ? local : null;
        Config.NeedLocalization = Config.LocalDBPath is not null;
        var ok = Config.InitDb();
        Log.AppendLine($"DB init: {ok} (localization={Config.NeedLocalization})");
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "TheGuideToTheNewEden")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("找不到仓库根目录");
    }

    // ---------- ① 解析器样本 ----------

    private static void RunParserChecks()
    {
        Log.AppendLine();
        Log.AppendLine("== 解析器样本 ==");

        // 英文（格式来自 eve-analytics 的战斗日志解析器）
        ExpectDamage(
            "(combat) <color=0xffcc0000><b>161</b></color> <color=0x77ffffff>from</color> <color=0xffffffff>Guardian<color=0x77ffffff>[CORP]</color>(Cynabal) <color=0x77ffffff>- </color><color=0xffffffff>Focused Medium Pulse Laser II</color> - <color=0x77ffffff>Penetrates</color>",
            CombatEventKind.Damage, CombatEventDirection.Incoming, 161, "Focused Medium Pulse Laser II", "Damage");
        ExpectDamage(
            "(combat) 123 to Guardian [CORP] (Cynabal) - Hammerhead II - Hits",
            CombatEventKind.Damage, CombatEventDirection.Outgoing, 123, "Hammerhead II", "Damage");
        Expect(
            "(combat) -15 GJ energy neutralized from Guardian [CORP] - Medium Energy Neutralizer II",
            CombatEventKind.CapacitorNeutralize, CombatEventDirection.Incoming, 15, "GJ", "Medium Energy Neutralizer II", "neut");
        Expect(
            "(combat) +15 GJ energy drained to Guardian [CORP] - Small Nosferatu II",
            CombatEventKind.CapacitorDrain, CombatEventDirection.Outgoing, 15, "GJ", "Small Nosferatu II", "nos");
        Expect(
            "(combat) Warp scramble attempt from Guardian [CORP] to you",
            CombatEventKind.WarpScramble, CombatEventDirection.Incoming, null, null, null, "scram");
        Expect(
            "(combat) Warp scramble attempt from you to Guardian [CORP]",
            CombatEventKind.WarpScramble, CombatEventDirection.Outgoing, null, null, null, "scram");
        Expect(
            "(combat) You're jammed by Guardian [CORP] - Multispectral Jammer II",
            CombatEventKind.EcmJam, CombatEventDirection.Incoming, null, null, "Multispectral Jammer II", "ecm");
        Expect(
            "(combat) 123 remote armor repaired by Guardian [CORP] - Large Remote Armor Repairer II",
            CombatEventKind.RemoteRepair, CombatEventDirection.Incoming, 123, "HP", "Large Remote Armor Repairer II", "rep");
        Expect(
            "(combat) 123 remote armor repaired to Guardian [CORP] - Large Remote Armor Repairer II",
            CombatEventKind.RemoteRepair, CombatEventDirection.Outgoing, 123, "HP", "Large Remote Armor Repairer II", "rep");
        Expect(
            "(combat) 40 GJ remote capacitor boosted by Guardian [CORP] - Medium Remote Capacitor Transmitter II",
            CombatEventKind.RemoteCapacitor, CombatEventDirection.Incoming, 40, "GJ", "Medium Remote Capacitor Transmitter II", "cap_transfer");

        // 中文结构样本（正文措辞待真实日志校准，这里先验证结构 / 词表 / 方向）
        Expect(
            "(combat) 161 来自 张三 [CORP](Cynabal) - 聚焦中型脉冲激光器 II - 穿透",
            CombatEventKind.Damage, CombatEventDirection.Incoming, 161, "HP", "聚焦中型脉冲激光器 II", "damage");
        Expect(
            "(combat) -15 GJ 能量中和 来自 张三 [CORP] - 中型能量中和器 II",
            CombatEventKind.CapacitorNeutralize, CombatEventDirection.Incoming, 15, "GJ", "中型能量中和器 II", "neut");
        Expect(
            "(combat) 123 远程装甲维修 由 李四 [CORP] - 大型远程装甲维修器 II",
            CombatEventKind.RemoteRepair, CombatEventDirection.Incoming, 123, "HP", "大型远程装甲维修器 II", "rep");
        Expect(
            "(combat) 跃迁扰断尝试 来自 张三 [CORP] 对你",
            CombatEventKind.WarpScramble, CombatEventDirection.Incoming, null, null, null, "scram");
        Expect(
            "(combat) 你被 张三 [CORP] 干扰 - 多谱干扰器 II",
            CombatEventKind.EcmJam, CombatEventDirection.Incoming, null, null, "多谱干扰器 II", "ecm");
    }

    private static void ExpectDamage(string line, CombatEventKind kind, CombatEventDirection direction, double amount, string module, string category)
        => Expect(line, kind, direction, amount, "HP", module, category);

    private static void Expect(
        string line,
        CombatEventKind kind,
        CombatEventDirection direction,
        double? amount,
        string unit,
        string module,
        string category)
    {
        // GameLogContent.Create 需要日志行前缀 [ 时间 ]；样本里省略，这里补上
        var logLine = line.TrimStart().StartsWith('[') ? line : $"[ 2026.11.19 17:00:00 ] {line}";
        var content = GameLogContent.Create(logLine);
        var ev = content is null ? null : CombatLogParser.TryParse(content, "QEDSD");
        if (ev is null)
        {
            Fail(line, "解析为 null");
            return;
        }

        var resolved = CombatCategoryResolver.Resolve(ev);
        var problems = new List<string>();
        if (ev.Kind != kind) problems.Add($"kind={ev.Kind} 期望 {kind}");
        if (ev.Direction != direction) problems.Add($"direction={ev.Direction} 期望 {direction}");
        if (amount.HasValue && (ev.Amount is null || Math.Abs(ev.Amount.Value - amount.Value) > 0.01))
        {
            problems.Add($"amount={ev.Amount?.ToString() ?? "null"} 期望 {amount}");
        }

        if (!string.IsNullOrEmpty(unit) && !string.Equals(ev.Unit, unit, StringComparison.OrdinalIgnoreCase))
        {
            problems.Add($"unit={ev.Unit} 期望 {unit}");
        }

        if (!string.IsNullOrEmpty(module) && !string.Equals(ev.ModuleName, module, StringComparison.Ordinal))
        {
            problems.Add($"module={ev.ModuleName} 期望 {module}");
        }

        if (!string.Equals(resolved, category, StringComparison.OrdinalIgnoreCase))
        {
            problems.Add($"category={resolved ?? "null"} 期望 {category}");
        }

        if (problems.Count > 0)
        {
            Fail(line, string.Join("; ", problems));
            return;
        }

        Log.AppendLine($"[PASS] {kind} {direction} amount={ev.Amount?.ToString() ?? "-"} unit={ev.Unit ?? "-"} " +
                       $"module={ev.ModuleName ?? "-"} category={resolved} (groupId={ev.ModuleGroupId})");
    }

    private static void Fail(string line, string reason)
    {
        _failed++;
        Log.AppendLine($"[FAIL] {reason}");
        Log.AppendLine($"       行：{line}");
    }

    // ---------- ② 装备 → 分类（真实 SDE） ----------

    private static void RunResolverChecks()
    {
        Log.AppendLine();
        Log.AppendLine("== 装备 → 分类（真实 SDE） ==");

        var english = new (string Name, string Category)[]
        {
            ("Medium Energy Neutralizer II", "neut"),
            ("Warp Scrambler II", "scram"),
            ("Large Remote Armor Repairer II", "rep"),
            ("Medium Remote Capacitor Transmitter II", "cap_transfer"),
            ("Small Nosferatu II", "nos"),
            ("Multispectral Jammer II", "ecm"),
        };

        foreach (var (name, expected) in english)
        {
            var ev = new CombatLogEvent { Kind = CombatEventKind.Damage, ModuleName = name };
            var category = CombatCategoryResolver.Resolve(ev);
            Check($"英文装备 {name}", category == expected,
                $"category={category}, typeId={ev.ModuleTypeId}, groupId={ev.ModuleGroupId} 期望 {expected}");
        }

        if (DBService.LocalDbReady)
        {
            foreach (var (part, expected) in new (string, string)[]
                     {
                         ("能量中和器", "neut"),
                         ("跃迁扰频器", "scram"),
                         ("远程装甲维修器", "rep"),
                         ("远程电容传输装置", "cap_transfer"),
                         ("干扰器", "ecm"),
                     })
            {
                var local = LocalDbService.SearchInvType(part);
                var sample = local.FirstOrDefault();
                if (sample is null)
                {
                    Log.AppendLine($"[SKIP] 本地库找不到含「{part}」的装备");
                    continue;
                }

                var ev = new CombatLogEvent { Kind = CombatEventKind.Damage, ModuleName = sample.TypeName };
                var category = CombatCategoryResolver.Resolve(ev);
                Check($"中文装备 {sample.TypeName}", category == expected,
                    $"category={category}, typeId={ev.ModuleTypeId}, groupId={ev.ModuleGroupId} 期望 {expected}");
            }
        }
    }

    private static void Check(string name, bool passed, string detail)
    {
        if (passed)
        {
            Log.AppendLine($"[PASS] {name}");
        }
        else
        {
            _failed++;
            Log.AppendLine($"[FAIL] {name}：{detail}");
        }
    }

    // ---------- ③ 扫描真实日志 ----------

    private static void ScanFolder(string folder)
    {
        Log.AppendLine();
        Log.AppendLine($"== 扫描真实日志：{folder} ==");

        var files = Directory.GetFiles(folder, "*.txt", SearchOption.TopDirectoryOnly);
        var parsed = new List<string>();
        var unmatched = new List<string>();
        foreach (var file in files)
        {
            foreach (var line in File.ReadLines(file, Encoding.Default))
            {
                if (!line.Contains("(combat)") && !line.Contains("(notify)"))
                {
                    continue;
                }

                var content = GameLogContent.Create(line);
                if (content is null)
                {
                    continue;
                }

                var ev = CombatLogParser.TryParse(content, "QEDSD");
                if (ev is null)
                {
                    unmatched.Add(CombatLogParser.NormalizeForDiagnostics(line));
                    continue;
                }

                var category = CombatCategoryResolver.Resolve(ev);
                parsed.Add($"{(int)ev.Kind}|{ev.Direction}|{category}|{ev.Amount?.ToString() ?? "-"}|{ev.Unit ?? "-"}|{ev.ModuleName ?? "-"}|{ev.RawLine}");
            }
        }

        var outDir = AppContext.BaseDirectory;
        File.WriteAllLines(Path.Combine(outDir, "scan-parsed.txt"), parsed.Distinct().OrderBy(p => p), Encoding.UTF8);
        File.WriteAllLines(Path.Combine(outDir, "scan-unmatched.txt"), unmatched.Distinct().OrderBy(p => p), Encoding.UTF8);
        Log.AppendLine($"文件 {files.Length} 个；可识别 {parsed.Distinct().Count()} 条，未识别 {unmatched.Distinct().Count()} 条。");
        Log.AppendLine($"  输出：{Path.Combine(outDir, "scan-parsed.txt")}");
        Log.AppendLine($"  输出：{Path.Combine(outDir, "scan-unmatched.txt")}");
    }
}
