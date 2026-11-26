using System.Drawing;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.ChannelIntel;
using TheGuideToTheNewEden.Core.Models.CombatLog;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using TheGuideToTheNewEden.Core.Services.CombatLog;
using TheGuideToTheNewEden.WPF.Services.ChannelIntel;
using TheGuideToTheNewEden.WPF.Services.CombatLog;
using TheGuideToTheNewEden.WPF.Services.GamePreview.Overlay;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace OverlayProbe;

/// <summary>
/// 阶段 355 探针：
/// ① 状态中心的"最近/最新/解除/注销"逻辑；
/// ② provider 产出的两行文字（合并规则）；
/// ③ 叠加窗在 0/1/2 行时的实际尺寸 + 截图。
/// 无游戏客户端、无网络依赖。
/// </summary>
internal static class Program
{
    private static readonly StringBuilder Log = new();

    [STAThread]
    private static int Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        LoadResources(app);

        try
        {
            RunCenterChecks();
            RunCombatCenterChecks();
            var png = RunOverlayWindowChecks();
            RunProviderChipChecks();
            RunChipOverlayChecks();
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "probe.log"), Log.ToString());
            Console.WriteLine(Log.ToString());
            Console.WriteLine($"PNG: {png}");
            return 0;
        }
        catch (Exception ex)
        {
            Log.AppendLine("EXCEPTION: " + ex);
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "probe.log"), Log.ToString());
            Console.WriteLine(Log.ToString());
            return 1;
        }
    }

    private static void LoadResources(Application app)
    {
        // 应用级资源：WPF-UI 主题/控件样式 + 语言字典（缺资源不会抛异常，只是颜色/文案缺失）
        try
        {
            var wpfUi = typeof(Wpf.Ui.Controls.SymbolIcon).Assembly;
            var themes = new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Dark };
            var controls = new Wpf.Ui.Markup.ControlsDictionary();
            app.Resources.MergedDictionaries.Add(themes);
            app.Resources.MergedDictionaries.Add(controls);
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/TheGuideToTheNewEden;component/Resources/Languages/zh-CN.xaml"),
            });
        }
        catch (Exception ex)
        {
            Log.AppendLine("资源加载失败（继续）：" + ex.Message);
        }
    }

    // ---------- ① 状态中心 ----------

    private static void RunCenterChecks()
    {
        var center = ChannelIntelAlertCenter.Current;
        var changes = new List<string>();
        center.Changed += (_, c) => changes.Add($"{c.Kind}:{string.Join(',', c.Alerts.Select(a => a.SolarSystemId))}");

        var setting = new ChannelIntelSetting { Listener = "Probe Pilot" };
        center.Register(setting);
        Check("已登记监听", center.IsListening("probe pilot"), "IsListening 应忽略大小写");

        Report(30000100, "3-XX", 3, IntelChatType.Intel);
        Report(30000200, "5-YY", 5, IntelChatType.Intel);
        var nearest = center.GetNearest("Probe Pilot");
        var latest = center.GetLatest("Probe Pilot");
        Check("最近=跳数最小", nearest?.SolarSystemId == 30000100, $"实际 {nearest?.SolarSystemId}");
        Check("最新=最后收到", latest?.SolarSystemId == 30000200, $"实际 {latest?.SolarSystemId}");
        Check("活跃计数=2", center.ActiveCount("Probe Pilot") == 2, $"实际 {center.ActiveCount("Probe Pilot")}");

        // 同星系再来一条更小的（模拟"更近的预警"）：跳数原地刷新，仍是同一条
        Report(30000200, "5-YY", 1, IntelChatType.Intel);
        Check("同星系原地刷新跳数", center.GetNearest("Probe Pilot")?.SolarSystemId == 30000200,
            $"实际 {center.GetNearest("Probe Pilot")?.SolarSystemId}");

        // 解除最新那条 → 最近/最新合并回另一条
        Report(30000200, "5-YY", 1, IntelChatType.Clear);
        Check("解除后只剩一条", center.ActiveCount("Probe Pilot") == 1, $"实际 {center.ActiveCount("Probe Pilot")}");
        Check("解除后最近=最新", center.GetNearest("Probe Pilot")?.SolarSystemId == 30000100
                                 && center.GetLatest("Probe Pilot")?.SolarSystemId == 30000100, "解除后应只剩 30000100");

        // provider：两行文字（这里一个系统不可能既最近又最新，先造两条）
        Report(30000300, "9-ZZ", 9, IntelChatType.Intel);
        var hub = PreviewOverlayHub.Current;
        var item = new PreviewItem { Name = "Probe Pilot", ShowIntelAlert = true };
        var global = new PreviewSetting { ShowIntelAlert = true };
        var rows = hub.GetItems(new PreviewOverlayContext("Probe Pilot", item, global));
        Check("provider 两行", rows.Count == 2, $"实际 {rows.Count}: {string.Join(" | ", rows.Select(r => r.Text))}");
        Check("每行都带悬停详情", rows.All(r => !string.IsNullOrWhiteSpace(r.Detail)),
            string.Join(" / ", rows.Select(r => (r.Detail ?? "null").Replace("\n", " ⏎ "))));
        Log.AppendLine("  行内容：" + string.Join(" | ", rows.Select(r => $"[{r.Severity}] {r.Text}")));

        item.ShowIntelAlert = false;
        Check("单项关 → 无行", hub.GetItems(new PreviewOverlayContext("Probe Pilot", item, global)).Count == 0, "单项关闭应无行");
        item.ShowIntelAlert = true;
        global.ShowIntelAlert = false;
        Check("全局关 → 无行", hub.GetItems(new PreviewOverlayContext("Probe Pilot", item, global)).Count == 0, "全局关闭应覆盖单项");
        global.ShowIntelAlert = true;

        Check("未登记角色 → 无行", hub.GetItems(new PreviewOverlayContext("Nobody", item, global)).Count == 0, "无会话应无行");

        center.Unregister("Probe Pilot");
        Check("注销后不监听", !center.IsListening("Probe Pilot"), "注销后应为 false");
        Check("注销触发 ClearedAll", changes.Any(p => p.StartsWith("ClearedAll")), string.Join(" / ", changes));
        Check("注销后无行", hub.GetItems(new PreviewOverlayContext("Probe Pilot", item, global)).Count == 0, "注销后应无行");
    }

    private static void RunCombatCenterChecks()
    {
        var center = CombatLogStateCenter.Current;
        var listener = "Probe Combat";
        center.ExpireSeconds = 5;
        center.SetEnabledCategories(CombatCategories.All.Select(p => p.Key));
        center.Attach(listener);
        Check("战斗中心已挂载", center.IsAttached(listener), "Attach 后应为 true");

        var now = DateTime.UtcNow;

        // 伤害：第一轮只有单次值；第二轮出现后速率 = 本轮合计 ÷ 两轮实际间隔（阶段 358 不再除以写死的 5 秒）
        ReportCombat(center, listener, CombatEventKind.Damage, CombatCategories.Damage, 100,
            "Focused Medium Pulse Laser II", "Guardian", now.AddSeconds(-4));
        var firstDamage = RowOf(center, listener, CombatCategories.Damage);
        Check("首轮伤害只显示单次值", firstDamage?.Text == "伤害 100 HP", $"实际 {firstDamage?.Text}");
        Check("首轮没有速率（不足两轮）", firstDamage?.Lines[0].Rate is null, $"实际 {firstDamage?.Lines[0].Rate}");

        ReportCombat(center, listener, CombatEventKind.Damage, CombatCategories.Damage, 200,
            "Focused Medium Pulse Laser II", "Guardian", now.AddSeconds(-1));
        var damageRow = RowOf(center, listener, CombatCategories.Damage);
        Check("伤害速率按实测两轮间隔（200 ÷ 3 秒）", damageRow?.Text == "伤害 200 HP 66.7 HP/s",
            $"实际 {damageRow?.Text}");
        Check("承伤记录实测间隔", Math.Abs((damageRow?.Lines[0].IntervalSeconds ?? 0) - 3) < 0.01,
            $"实际 {damageRow?.Lines[0].IntervalSeconds}");

        // 同一秒内又来两条（同一次齐射的多门炮）：并进同一轮，第一段变成这一轮合计 300HP
        ReportCombat(center, listener, CombatEventKind.Damage, CombatCategories.Damage, 50,
            "Focused Medium Pulse Laser II", "Guardian", now.AddSeconds(-1));
        ReportCombat(center, listener, CombatEventKind.Damage, CombatCategories.Damage, 50,
            "Focused Medium Pulse Laser II", "Guardian", now.AddSeconds(-1));
        var volleyRow = RowOf(center, listener, CombatCategories.Damage);
        Check("同秒多条并成一轮（300HP ÷ 3 秒）", volleyRow?.Text == "伤害 300 HP 100 HP/s",
            $"实际 {volleyRow?.Text}");

        // 毁电：45GJ 每 2 秒一轮 → 22.5GJ/s
        ReportCombat(center, listener, CombatEventKind.CapacitorNeutralize, CombatCategories.Neut, 45,
            "Medium Energy Neutralizer II", "Guardian", now.AddSeconds(-4));
        ReportCombat(center, listener, CombatEventKind.CapacitorNeutralize, CombatCategories.Neut, 45,
            "Medium Energy Neutralizer II", "Guardian", now.AddSeconds(-2));
        var neutRow = RowOf(center, listener, CombatCategories.Neut);
        Check("毁电=45GJ 22.5GJ/s", neutRow?.Text == "毁电 45 GJ 22.5 GJ/s", $"实际 {neutRow?.Text}");

        // 遥修：123HP 每 3 秒一轮 → 41HP/s
        ReportCombat(center, listener, CombatEventKind.RemoteRepair, CombatCategories.Rep, 123,
            "Remote Armor Repairer II", "Guardian", now.AddSeconds(-4));
        ReportCombat(center, listener, CombatEventKind.RemoteRepair, CombatCategories.Rep, 123,
            "Remote Armor Repairer II", "Guardian", now.AddSeconds(-1));
        var repRow = RowOf(center, listener, CombatCategories.Rep);
        Check("遥修=123HP 41HP/s", repRow?.Text == "遥修 123 HP 41 HP/s", $"实际 {repRow?.Text}");

        // 阶段 363：吸电的输出侧单独记（我吸别人）——与"被吸"分开，叠加层仍只显示承受侧
        ReportCombat(center, listener, CombatEventKind.CapacitorDrain, CombatCategories.Nos, 30,
            "Small Energy Nosferatu II", "Guardian", now.AddSeconds(-4), CombatEventDirection.Outgoing);
        ReportCombat(center, listener, CombatEventKind.CapacitorDrain, CombatCategories.Nos, 30,
            "Small Energy Nosferatu II", "Guardian", now.AddSeconds(-2), CombatEventDirection.Outgoing);
        var nosOutOnly = RowOf(center, listener, CombatCategories.Nos);
        Check("吸电：只有输出时没有承受侧", nosOutOnly is { Lines.Count: 1 } && nosOutOnly.Lines[0].Direction == CombatEventDirection.Outgoing,
            $"lines={nosOutOnly?.Lines.Count}: {string.Join(" | ", nosOutOnly?.Lines.Select(l => l.Text) ?? [])}");
        Check("吸电：输出侧 30GJ 15GJ/s", nosOutOnly?.Text == "吸电 30 GJ 15 GJ/s",
            $"实际 {nosOutOnly?.Text}");
        Check("纯输出行没有输入侧（叠加层不显示）", nosOutOnly is { Lines.Count: 1 } && nosOutOnly.Lines[0].Direction == CombatEventDirection.Outgoing, $"实际 {nosOutOnly?.Text}");

        ReportCombat(center, listener, CombatEventKind.CapacitorDrain, CombatCategories.Nos, 15,
            "Small Energy Nosferatu II", "Guardian", now.AddSeconds(-3));
        ReportCombat(center, listener, CombatEventKind.CapacitorDrain, CombatCategories.Nos, 15,
            "Small Energy Nosferatu II", "Guardian", now.AddSeconds(-1));
        var nosBoth = RowOf(center, listener, CombatCategories.Nos);
        Check("吸电：承受 / 输出两侧都在", nosBoth is { Lines.Count: 2 },
            $"lines={nosBoth?.Lines.Count}: {string.Join(" | ", nosBoth?.Lines.Select(l => l.Text) ?? [])}");
        Check("吸电：承受侧 15GJ 7.5GJ/s", nosBoth?.Text.Contains("吸电 15 GJ 7.5 GJ/s") == true,
            $"实际 {nosBoth?.Text}");

        // 反跳：只在作用上时有一条日志，两分钟前那条也要一直留着（过期只影响数值卡片）
        ReportCombat(center, listener, CombatEventKind.WarpScramble, CombatCategories.Scram, null,
            "Warp Scrambler II", "Guardian", now.AddSeconds(-120));
        var scramRow = RowOf(center, listener, CombatCategories.Scram);
        Check("反跳不按秒数消失（显示分组名 跃迁扰频器）", scramRow?.Text == "跃迁扰频器", $"实际 {scramRow?.Text}");
        Check("反跳不参与过期判断", scramRow?.Expired == false, $"实际 Expired={scramRow?.Expired}");

        center.SetCategoryEnabled(CombatCategories.Damage, false);
        Check("分类裁剪：承伤隐藏", center.GetRows(listener).All(p => p.Category != CombatCategories.Damage),
            "关闭后不应有承伤行");
        center.SetCategoryEnabled(CombatCategories.Damage, true);

        // 阶段 362：过期只置灰、不消失——行一直在，只是 Expired=true 且数值停在最后一次
        center.ExpireSeconds = 60;
        Check("未过期：承伤不置灰", RowOf(center, listener, CombatCategories.Damage)?.Expired == false,
            "60 秒内应为未过期");
        center.ExpireSeconds = 1;
        Thread.Sleep(1200);
        var afterExpire = center.GetRows(listener);
        var expiredDamage = afterExpire.FirstOrDefault(p => p.Category == CombatCategories.Damage);
        Check("过期后行不消失（承伤仍在）", expiredDamage is not null,
            string.Join(" | ", afterExpire.Select(p => p.Text)));
        Check("过期后标记 Expired", expiredDamage?.Expired == true, $"实际 Expired={expiredDamage?.Expired}");
        Check("过期后数值停在最后一次", expiredDamage?.Text == "伤害 300 HP 100 HP/s", $"实际 {expiredDamage?.Text}");
        Check("过期后所有数值卡片都还在", afterExpire.Count >= 4,
            string.Join(" | ", afterExpire.Select(p => p.Text)));
        Check("反跳在低过期时间下仍不过期",
            afterExpire.FirstOrDefault(p => p.Category == CombatCategories.Scram)?.Expired == false,
            "sticky 分类不参与过期");
        center.ExpireSeconds = 5;

        center.Clear(listener);
        Check("手动清除后无行", center.GetRows(listener).Count == 0,
            string.Join(" | ", center.GetRows(listener).Select(p => p.Text)));

        center.Detach(listener);
        Check("战斗中心已摘除", !center.IsAttached(listener), "Detach 后应为 false");
    }

    private static CombatLogRow? RowOf(CombatLogStateCenter center, string listener, string category)
        => center.GetRows(listener).FirstOrDefault(p => p.Category == category);

    private static void ReportCombat(
        CombatLogStateCenter center,
        string listener,
        CombatEventKind kind,
        string category,
        double? amount,
        string module,
        string pilot,
        DateTime time,
        CombatEventDirection direction = CombatEventDirection.Incoming)
    {
        center.Report(listener, new CombatLogEvent
        {
            Kind = kind,
            Direction = direction,
            CategoryKey = category,
            Amount = amount,
            ModuleName = module,
            Pilot = pilot,
            Time = time,
        });
    }

    private static void Report(int systemId, string name, int jumps, IntelChatType type)
    {
        ChannelIntelAlertCenter.Current.Report(new EarlyWarningContent
        {
            Listener = "Probe Pilot",
            SolarSystemId = systemId,
            SolarSystemName = name,
            Jumps = jumps,
            IntelType = type,
            Content = $"{name} hostile {jumps}j",
            Time = DateTime.UtcNow,
        }, "intel");
    }

    // ---------- ②③ 叠加窗 ----------

    private static string RunOverlayWindowChecks()
    {
        var window = new PreviewNameOverlayWindow();
        window.DisplayName = "Probe Pilot";
        window.SetNameVisible(true);
        window.ApplyAppearance("Microsoft YaHei UI", 46,
            System.Drawing.Color.FromArgb(255, 255, 255, 255),
            System.Drawing.Color.FromArgb(153, 0, 0, 0),
            "Microsoft YaHei UI",
            30);
        Check("叠加层字号可独立设置", Math.Abs(window.InfoFontSize - 30) < 0.01, $"实际 {window.InfoFontSize}");
        Check("叠加层字体可独立设置", window.InfoFontFamily.Source == "Microsoft YaHei UI", $"实际 {window.InfoFontFamily.Source}");
        Check("角色名配色不受叠加层设置影响",
            window.NameForeground is SolidColorBrush nfg && nfg.Color.R == 255 && nfg.Color.G == 255 && nfg.Color.B == 255,
            $"实际 {Describe(window.NameForeground)}");

        // 无信息行
        window.SetInfoItems([]);
        window.Show();
        Pump();
        var nameOnly = (window.ActualWidth, window.ActualHeight);
        Log.AppendLine($"  无信息行：{nameOnly.ActualWidth:F1} x {nameOnly.ActualHeight:F1} DIP");

        // 两行
        window.SetInfoItems(
        [
            new PreviewOverlayItem("intel.nearest", "最近 3 跳 · 2min", PreviewOverlaySeverity.Critical, 0),
            new PreviewOverlayItem("intel.latest", "最新 5 跳 · 0min", PreviewOverlaySeverity.Caution, 1),
        ]);
        Pump();
        var twoRows = (window.ActualWidth, window.ActualHeight);
        Log.AppendLine($"  两行：{twoRows.ActualWidth:F1} x {twoRows.ActualHeight:F1} DIP");
        Check("信息行让窗口变高", twoRows.ActualHeight > nameOnly.ActualHeight + 20,
            $"{nameOnly.ActualHeight:F1} -> {twoRows.ActualHeight:F1}");

        // 一行
        window.SetInfoItems(
        [
            new PreviewOverlayItem("intel.both", "最近/最新 3 跳 · 2min", PreviewOverlaySeverity.Critical, 0),
        ]);
        Pump();
        var oneRow = (window.ActualWidth, window.ActualHeight);
        Log.AppendLine($"  一行：{oneRow.ActualWidth:F1} x {oneRow.ActualHeight:F1} DIP");
        Check("一行高度介于两者之间", oneRow.ActualHeight > nameOnly.ActualHeight
                                      && oneRow.ActualHeight < twoRows.ActualHeight,
            $"{nameOnly.ActualHeight:F1} < {oneRow.ActualHeight:F1} < {twoRows.ActualHeight:F1}");

        // 回到两行，再展开悬停详情并截图
        window.SetInfoItems(
        [
            new PreviewOverlayItem("intel.nearest", "最近 3 跳 · 2min", PreviewOverlaySeverity.Critical, 0,
                "Jita · 3 跳 · 2min\n本地频道 · QEDSD: 3 跳 Jita 有敌对"),
            new PreviewOverlayItem("intel.latest", "最新 5 跳 · 0min", PreviewOverlaySeverity.Caution, 1,
                "Tama · 5 跳 · 0min\n情报频道 · Aria: Tama 一条截击"),
        ]);
        Pump();
        var rowsOnly = window.ActualHeight;
        window.SetDetails("巴尔 · 4 跳 · 0min\n本地 · QEDSD: 巴尔 22221212121212121212呃呃打撒大厦1212121电动车四川省成都市测试测试测试到处是\n\nJita · 3 跳 · 2min\n本地频道 · QEDSD: 3 跳 Jita 有敌对");
        Pump();
        Check("悬停详情（ToolTip）展开", window.HasDetails, $"HasDetails={window.HasDetails}");
        Check("浮层不占叠加窗高度", Math.Abs(window.ActualHeight - rowsOnly) < 0.5,
            $"高度 {rowsOnly:F1} -> {window.ActualHeight:F1}");
        var path = Path.Combine(AppContext.BaseDirectory, "overlay.png");
        SavePng(window, path);
        Log.AppendLine($"  截图：{path}");

        // 详情排版单独渲染（ToolTip 是独立 popup，视觉树截图拿不到，屏幕抓图又被别的窗口遮挡）
        SaveDetailVisual(window, Path.Combine(AppContext.BaseDirectory, "detail.png"));

        // 额外截一张包含 ToolTip 浮层的屏幕图（ToolTip 是独立 popup，不能靠视觉树渲染）
        window.Topmost = true;
        SaveScreenShot(window, Path.Combine(AppContext.BaseDirectory, "overlay-tooltip.png"));

        window.Close();
        return path;
    }

    /// <summary>
    /// 阶段 382：两个 provider 真的产出了胶囊（战斗：状态 / 伤害 / 遥修聚合；预警：用户配色标签）。
    /// </summary>
    private static void RunProviderChipChecks()
    {
        Log.AppendLine();
        Log.AppendLine("=== 阶段 382：provider 胶囊 ===");

        var center = CombatLogStateCenter.Current;
        var listener = "Chip Combat";
        center.SetEnabledCategories(CombatCategories.All.Select(p => p.Key));
        center.Attach(listener);
        var now = DateTime.UtcNow;

        ReportCombat(center, listener, CombatEventKind.Damage, CombatCategories.Damage, 200,
            "Focused Medium Pulse Laser II", "Guardian", now.AddSeconds(-4));
        ReportCombat(center, listener, CombatEventKind.Damage, CombatCategories.Damage, 100,
            "Focused Medium Pulse Laser II", "Guardian", now.AddSeconds(-1));
        ReportCombat(center, listener, CombatEventKind.CapacitorNeutralize, CombatCategories.Neut, 45,
            "Medium Energy Neutralizer II", "Guardian", now.AddSeconds(-2));
        ReportCombat(center, listener, CombatEventKind.WarpScramble, CombatCategories.Scram, null,
            "Warp Scrambler II", "Guardian", now.AddSeconds(-5));

        center.Report(listener, new CombatLogEvent
        {
            Kind = CombatEventKind.RemoteRepair,
            Direction = CombatEventDirection.Incoming,
            CategoryKey = CombatCategories.Rep,
            Amount = 45,
            RepairType = RemoteRepairType.Shield,
            ModuleName = "Remote Shield Booster II",
            Pilot = "Logi",
            Time = now.AddSeconds(-4),
        });
        center.Report(listener, new CombatLogEvent
        {
            Kind = CombatEventKind.RemoteRepair,
            Direction = CombatEventDirection.Incoming,
            CategoryKey = CombatCategories.Rep,
            Amount = 78,
            RepairType = RemoteRepairType.Armor,
            ModuleName = "Remote Armor Repairer II",
            Pilot = "Logi",
            Time = now.AddSeconds(-2),
        });

        var item = new PreviewItem { Name = listener, ShowCombatInfo = true, ShowIntelAlert = false };
        var global = new PreviewSetting { ShowCombatInfo = true, ShowIntelAlert = false };
        var rows = PreviewOverlayHub.Current
            .GetItems(new PreviewOverlayContext(listener, item, global))
            .Where(p => p.Key.StartsWith("combat:", StringComparison.Ordinal))
            .ToList();

        Check("战斗 provider：每行都带 Chip", rows.Count >= 4 && rows.All(p => p.Chip is not null),
            $"{rows.Count} 行 / {rows.Count(p => p.Chip is not null)} 带 Chip");

        var damage = rows.FirstOrDefault(p => p.Key == "combat:damage")?.Chip;
        Check("战斗 provider：伤害 = Damage 胶囊（短标签 + HP + 速率）",
            damage is { Kind: PreviewOverlayChipKind.Damage, Label: "伤害", Unit: "HP" } && damage.Meta.EndsWith("/s"),
            $"{damage?.Kind} / {damage?.Label} / {damage?.Value}{damage?.Unit} {damage?.Meta}");

        var scram = rows.FirstOrDefault(p => p.Key == "combat:scram")?.Chip;
        Check("战斗 provider：反跳 = 状态胶囊（无值、受损色）",
            scram is { Kind: PreviewOverlayChipKind.State, Label: "反跳", Tone: "harm" } && string.IsNullOrEmpty(scram.Value),
            $"{scram?.Kind} / {scram?.Label} / tone={scram?.Tone} / value={scram?.Value}");

        var rep = rows.FirstOrDefault(p => p.Key == "combat:rep")?.Chip;
        Check("战斗 provider：遥修聚合（主值 45+78=123，微条两段）",
            rep is { Kind: PreviewOverlayChipKind.Rep, Value: "123", Tone: "benefit" }
            && rep.LayerShare is { Count: 3 } shares && shares.Count(p => p > 0) == 2,
            $"{rep?.Value}{rep?.Unit} / shares={string.Join(",", rep?.LayerShare?.Select(p => p.ToString("F2")) ?? [])}");

        center.Detach(listener);

        // 预警 provider：注册一个监听并报两条
        var intelCenter = ChannelIntelAlertCenter.Current;
        intelCenter.Register(new ChannelIntelSetting { Listener = "Chip Intel" });
        intelCenter.Report(new EarlyWarningContent
        {
            Listener = "Chip Intel",
            SolarSystemId = 30000400,
            SolarSystemName = "4-AA",
            Jumps = 4,
            IntelType = IntelChatType.Intel,
            Content = "4-AA hostile",
            Time = DateTime.UtcNow,
        }, "intel");
        intelCenter.Report(new EarlyWarningContent
        {
            Listener = "Chip Intel",
            SolarSystemId = 30000500,
            SolarSystemName = "5-BB",
            Jumps = 5,
            IntelType = IntelChatType.Intel,
            Content = "5-BB hostile",
            Time = DateTime.UtcNow,
        }, "intel");

        var intelItem = new PreviewItem { Name = "Chip Intel", ShowIntelAlert = true, ShowCombatInfo = false };
        var intelGlobal = new PreviewSetting { ShowIntelAlert = true, ShowCombatInfo = false };
        var intelRows = PreviewOverlayHub.Current
            .GetItems(new PreviewOverlayContext("Chip Intel", intelItem, intelGlobal))
            .Where(p => p.Key.StartsWith("intel.", StringComparison.Ordinal))
            .ToList();

        Check("预警 provider：带 Chip 且 Tone=alert",
            intelRows.Count > 0 && intelRows.All(p => p.Chip is { Kind: PreviewOverlayChipKind.Intel, Tone: "alert" }),
            string.Join(" | ", intelRows.Select(p => $"{p.Chip?.Label} {p.Chip?.Value}{p.Chip?.Unit} · {p.Chip?.Meta}")));
        Check("预警 provider：最近 / 最新两枚，标签与跳数正确",
            intelRows.Count == 2
            && intelRows.Any(p => p.Chip is { Label: "最近", Value: "4", Unit: "跳" } && p.Chip.Meta.EndsWith("min"))
            && intelRows.Any(p => p.Chip is { Label: "最新", Value: "5", Unit: "跳" }),
            string.Join(" | ", intelRows.Select(p => $"{p.Chip?.Label}/{p.Chip?.Value}/{p.Chip?.Meta}")));

        intelCenter.Unregister("Chip Intel");
    }

    /// <summary>
    /// 阶段 382：胶囊 HUD——三段分区、宽度夹紧、行数预算与 +N 折叠、预警配色 / 降级。
    /// </summary>
    private static void RunChipOverlayChecks()
    {
        Log.AppendLine();
        Log.AppendLine("=== 阶段 382：胶囊 HUD ===");

        var window = new PreviewNameOverlayWindow
        {
            DisplayName = "Chip Pilot",
        };
        window.SetNameVisible(true);
        window.ApplyAppearance(
            "Microsoft YaHei UI",
            40,
            System.Drawing.Color.FromArgb(255, 255, 255, 255),
            System.Drawing.Color.FromArgb(153, 0, 0, 0),
            "Microsoft YaHei UI",
            15);
        window.SetMaxContentWidth(420);
        window.SetMaxValueRows(2);
        window.Show();
        Pump();

        var items = new List<PreviewOverlayItem>
        {
            new("intel.nearest", "最近 3 跳 · 2min", PreviewOverlaySeverity.Critical, 0,
                "Jita · 3 跳 · 2min\n本地频道 · QEDSD: 3 跳 Jita 有敌对",
                Chip: new PreviewOverlayChip(PreviewOverlayChipKind.Intel, "最近", "3", "跳", "2min", "alert")),
            new("intel.latest", "最新 5 跳 · 0min", PreviewOverlaySeverity.Caution, 1,
                "Tama · 5 跳 · 0min\n情报频道 · Aria: Tama 一条截击",
                Chip: new PreviewOverlayChip(PreviewOverlayChipKind.Intel, "最新", "5", "跳", "0min", "alert", Dimmed: true)),
            new("combat:scram", "跃迁扰频器", PreviewOverlaySeverity.Critical, 110, "反跳 · 跃迁扰频器 II · Aria (Loki)",
                Chip: new PreviewOverlayChip(PreviewOverlayChipKind.State, "反跳", Tone: "harm")),
            new("combat:damage", "伤害 209 HP 41.8 HP/s", PreviewOverlaySeverity.Critical, 110, "伤害 · 雷神重型导弹 · 209 HP / 5s",
                Chip: new PreviewOverlayChip(PreviewOverlayChipKind.Damage, "伤害", "209", "HP", "41.8/s", "harm")),
            new("combat:neut", "毁电 45 GJ 22.5 GJ/s", PreviewOverlaySeverity.Critical, 120, "毁电 · 中型能量中和器 II · 45GJ/2s",
                Chip: new PreviewOverlayChip(PreviewOverlayChipKind.Value, "毁电", "45", "GJ", "22.5/s", "harm")),
            new("combat:rep", "遥修 123 HP 41 HP/s", PreviewOverlaySeverity.Normal, 140, "遥修 · 护盾 45 · 装甲 78 · 41HP/s",
                Chip: new PreviewOverlayChip(PreviewOverlayChipKind.Rep, "遥修", "123", "HP", "41/s", "benefit", [0.37, 0.63, 0])),
            new("combat:cap", "传电 12 GJ 4 GJ/s", PreviewOverlaySeverity.Normal, 150, "传电 · 远程电容传输器 II · 12GJ/3s",
                Chip: new PreviewOverlayChip(PreviewOverlayChipKind.Value, "传电", "12", "GJ", "4/s", "benefit")),
        };

        window.SetInfoItems(items);
        Pump();

        var counts = window.GetChipCounts();
        Check("三段分区：预警 2 / 状态 1 / 数值 4", counts.Intel == 2 && counts.State == 1 && counts.Value == 4,
            $"intel={counts.Intel} state={counts.State} value={counts.Value} overflow={counts.Overflow}");
        // 阶段 385：恢复分类色，并去掉单独的文字色 / 背景色设置
        Check("预警胶囊按严重度着色（最近=红 / 最新=黄）",
            window.GetChipForegroundByLabel("最近") is SolidColorBrush nearestFg
            && nearestFg.Color.R == 255 && nearestFg.Color.G == 153
            && window.GetChipForegroundByLabel("最新") is SolidColorBrush latestFg
            && latestFg.Color.G > 200 && latestFg.Color.B < 60,
            Describe(window.GetChipForegroundByLabel("最近")) + " / " + Describe(window.GetChipForegroundByLabel("最新")));
        Check("战斗胶囊恢复分类色（伤害红 / 遥修绿 / 反跳红）",
            window.GetChipForegroundByLabel("伤害") is SolidColorBrush damageFg
            && damageFg.Color.R > 200 && damageFg.Color.G < 200
            && window.GetChipForegroundByLabel("遥修") is SolidColorBrush repFg
            && repFg.Color.G > 180 && repFg.Color.R < 160
            && window.GetChipForegroundByLabel("反跳") is SolidColorBrush scramFg
            && scramFg.Color.R > 200,
            string.Join(" ", new[]
            {
                Describe(window.GetChipForegroundByLabel("伤害")),
                Describe(window.GetChipForegroundByLabel("遥修")),
                Describe(window.GetChipForegroundByLabel("反跳")),
            }));
        var style = window.GetChipStyleSnapshot();
        Check("胶囊外观与日志监控一致（圆角 10 / 无边框 / 同字号）",
            style is { Radius: 10, BorderThickness: 0 } && style.Value.FontSizes.Distinct().Count() == 1,
            style is null ? "null" : $"r={style.Value.Radius} border={style.Value.BorderThickness} fonts={string.Join("/", style.Value.FontSizes.Distinct())}");
        Check("胶囊底色按 tone 混合（不透明深色）",
            style?.Background is { } bg && bg.A == 0xE6 && bg.R > bg.G && bg.R > bg.B,
            style?.Background?.ToString() ?? "null");
        Check("降级预警变淡（0.55）", window.GetIntelChipOpacities().Any(p => Math.Abs(p - 0.55) < 0.01),
            string.Join(" / ", window.GetIntelChipOpacities().Select(p => p.ToString("F2"))));

        var wide = window.GetPlateSize();
        Check("底板宽度不超出可用宽（420）", wide.Width <= 420.5, $"{wide.Width:F1} x {wide.Height:F1}");

        var wideTexts = CollectVisibleTexts(window);
        Check("三段内容都在（名字 + 标签 + 数值）",
            wideTexts.Contains("最近") && wideTexts.Contains("最新") && wideTexts.Contains("反跳")
            && wideTexts.Contains("伤害") && wideTexts.Contains("遥修") && wideTexts.Contains("传电"),
            string.Join(" | ", wideTexts));
        Check("胶囊文本带单位与速率", wideTexts.Contains("HP 41.8/s") && wideTexts.Contains("GJ 22.5/s"),
            string.Join(" | ", wideTexts));

        var widePng = Path.Combine(AppContext.BaseDirectory, "chips-wide.png");
        SavePng(window, widePng);
        Log.AppendLine($"  宽态胶囊截图：{widePng}");

        // 窄窗 + 数值只留 1 行：数值折叠成 +N，预警 / 状态 / 伤害不受影响
        window.SetMaxContentWidth(300);
        window.SetMaxValueRows(1);
        Pump();

        counts = window.GetChipCounts();
        var narrow = window.GetPlateSize();
        var narrowTexts = CollectVisibleTexts(window);
        Check("数值区折叠到 1 行并出现 +N", counts.Overflow > 0, $"overflow={counts.Overflow} value={counts.Value}");
        Check("折叠后底板仍不超出可用宽（300）", narrow.Width <= 300.5, $"{narrow.Width:F1} x {narrow.Height:F1}");
        Check("伤害永不折叠", narrowTexts.Contains("伤害"), string.Join(" | ", narrowTexts));
        Check("预警与状态不被数值挤掉", narrowTexts.Contains("最近") && narrowTexts.Contains("反跳"),
            string.Join(" | ", narrowTexts));

        var chipPng = Path.Combine(AppContext.BaseDirectory, "chips.png");
        SavePng(window, chipPng);
        Log.AppendLine($"  胶囊截图：{chipPng}");

        // 空：底板整块隐藏（不是空底板）
        window.SetInfoItems([]);
        Pump();
        Check("没有胶囊时底板隐藏", !window.HasInfoItems, $"HasInfoItems={window.HasInfoItems}");
        Check("空状态不占高度", window.GetPlateSize().Height < 0.5, $"{window.GetPlateSize().Height:F1}");

        window.Close();
    }

    /// <summary>收集当前可见的文字（用来断言"哪几枚胶囊被折叠了"）。</summary>
    private static List<string> CollectVisibleTexts(Window window)
    {
        var texts = new List<string>();
        if (window.Content is not DependencyObject root)
        {
            return texts;
        }

        void Walk(DependencyObject node)
        {
            if (node is TextBlock { IsVisible: true } block && !string.IsNullOrWhiteSpace(block.Text))
            {
                texts.Add(block.Text);
            }

            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++)
            {
                Walk(VisualTreeHelper.GetChild(node, i));
            }
        }

        Walk(root);
        return texts;
    }

    /// <summary>把 ToolTip 里的详情文字单独渲染成图（核对折行 / 间距是否正常）。</summary>
    private static void SaveDetailVisual(Window window, string path)
    {
        if (window is not PreviewNameOverlayWindow overlay)
        {
            return;
        }

        var visual = overlay.DetailsVisual;
        Pump();
        var width = Math.Max(1, (int)Math.Ceiling(visual.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(visual.ActualHeight));

        // 用一个 VisualBrush 复制内容，避免把 DetailsVisual 从 ToolTip 里搬走；
        // 深色底只是为了看清浅色文字
        var background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 32, 32));
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(background, null, new Rect(0, 0, width + 16, height + 16));
            dc.DrawRectangle(
                new VisualBrush(visual) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top },
                null,
                new Rect(8, 8, width, height));
        }

        var bmp = new RenderTargetBitmap(width + 16, height + 16, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(dv);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(path);
        encoder.Save(fs);
        Log.AppendLine($"  详情排版图：{path}（{width}x{height}）");
    }

    /// <summary>ToolTip 是独立 popup，视觉树渲染不到，只能抓屏幕（截取叠加窗周围一小块）。</summary>
    private static void SaveScreenShot(Window window, string path)
    {
        try
        {
            Pump();
            System.Threading.Thread.Sleep(200);
            var origin = window.PointToScreen(new System.Windows.Point(0, 0));
            var x = Math.Max(0, (int)origin.X - 10);
            var y = Math.Max(0, (int)origin.Y - 10);
            const int width = 780;
            const int height = 560;
            using var bmp = new System.Drawing.Bitmap(width, height);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height));
            }

            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            Log.AppendLine($"  屏幕截图（含 ToolTip）：{path}");
        }
        catch (Exception ex)
        {
            Log.AppendLine("  屏幕截图失败（忽略）：" + ex.Message);
        }
    }

    private static void SavePng(Window window, string path)    {
        var content = (FrameworkElement)window.Content;
        var width = Math.Max(1, (int)Math.Ceiling(content.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(content.ActualHeight));

        // 截图时补一块深色底，方便肉眼看清半透明角标（窗口本身是透明的）
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 44, 52)),
                null,
                new Rect(0, 0, width, height));
            dc.DrawRectangle(
                new VisualBrush(content) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top },
                null,
                new Rect(0, 0, width, height));
        }

        var bmp = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(path);
        encoder.Save(fs);
    }

    private static void Pump()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        Application.Current.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.ContextIdle,
            new Action(() => frame.Continue = false));
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        Application.Current.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private static void Check(string name, bool ok, string detail)
        => Log.AppendLine($"[{(ok ? "PASS" : "FAIL")}] {name}：{detail}");

    private static string Describe(System.Windows.Media.Brush? brush)
        => brush is SolidColorBrush solid
            ? $"#{solid.Color.A:X2}{solid.Color.R:X2}{solid.Color.G:X2}{solid.Color.B:X2}"
            : brush?.ToString() ?? "null";
}
