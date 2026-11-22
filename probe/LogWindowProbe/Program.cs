using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TheGuideToTheNewEden.Core.Models.CombatLog;
using TheGuideToTheNewEden.Core.Services.CombatLog;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.CombatLog;
using TheGuideToTheNewEden.WPF.Services.Settings;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace LogWindowProbe;

/// <summary>
/// 阶段 378 探针：胶囊流（所有非伤害分类）+ 伤害卡片（受到/输出、DPS、可识别属性占比）。
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        try
        {
            LoadResources(app);
            Run();
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "probe-error.txt"), ex.ToString());
            return 1;
        }
    }

    private static void Run()
    {
        var center = CombatLogStateCenter.Current;
        center.ExpireSeconds = 30;
        center.StatusGreySeconds = 300;
        center.SetEnabledCategories(CombatCategories.All.Select(p => p.Key));

        var now = DateTime.UtcNow;
        const string listener = "Probe Chips";
        center.Attach(listener);

        // 数值类 + 状态类都进胶囊（伤害除外）
        Report(center, listener, CombatEventKind.CapacitorNeutralize, CombatCategories.Neut, 45, "Medium Energy Neutralizer II", now.AddSeconds(-4));
        Report(center, listener, CombatEventKind.CapacitorNeutralize, CombatCategories.Neut, 45, "Medium Energy Neutralizer II", now.AddSeconds(-2), CombatEventDirection.Outgoing);
        Report(center, listener, CombatEventKind.CapacitorDrain, CombatCategories.Nos, 15, "Small Nosferatu II", now.AddSeconds(-4));
        Report(center, listener, CombatEventKind.CapacitorDrain, CombatCategories.Nos, 15, "Small Nosferatu II", now.AddSeconds(-2), CombatEventDirection.Outgoing);
        Report(center, listener, CombatEventKind.RemoteCapacitor, CombatCategories.CapTransfer, 20, "Remote Capacitor Transmitter II", now.AddSeconds(-4));
        Report(center, listener, CombatEventKind.RemoteCapacitor, CombatCategories.CapTransfer, 20, "Remote Capacitor Transmitter II", now.AddSeconds(-2), CombatEventDirection.Outgoing);
        Report(center, listener, CombatEventKind.WarpScramble, CombatCategories.Scram, null, "Warp Scrambler II", now.AddSeconds(-5));
        Report(center, listener, CombatEventKind.WarpScramble, CombatCategories.Scram, null, "Warp Scrambler II", now.AddSeconds(-5), CombatEventDirection.Outgoing);
        Report(center, listener, CombatEventKind.EcmJam, CombatCategories.Ecm, null, "Multispectral Jammer II", now.AddSeconds(-3));
        Report(center, listener, CombatEventKind.Unknown, CombatCategories.EwOther, null, "未知力场发生器", now.AddSeconds(-2));
        Report(center, listener, CombatEventKind.Damage, CombatCategories.Damage, 120, "Focused Medium Pulse Laser II", now.AddSeconds(-3));

        // 遥修：护盾/装甲/结构 × 进/出 = 6 枚胶囊
        Report(center, listener, CombatEventKind.RemoteRepair, CombatCategories.Rep, 100, "Remote Shield Booster II", now.AddSeconds(-4) , CombatEventDirection.Incoming, null, RemoteRepairType.Shield);
        Report(center, listener, CombatEventKind.RemoteRepair, CombatCategories.Rep, 80, "Remote Shield Booster II", now.AddSeconds(-2), CombatEventDirection.Outgoing, null, RemoteRepairType.Shield);
        Report(center, listener, CombatEventKind.RemoteRepair, CombatCategories.Rep, 50, "Remote Armor Repairer II", now.AddSeconds(-3) , CombatEventDirection.Incoming, null, RemoteRepairType.Armor);
        Report(center, listener, CombatEventKind.RemoteRepair, CombatCategories.Rep, 30, "Remote Armor Repairer II", now.AddSeconds(-3), CombatEventDirection.Outgoing, null, RemoteRepairType.Armor);
        Report(center, listener, CombatEventKind.RemoteRepair, CombatCategories.Rep, 0, "Remote Hull Repairer II", now.AddSeconds(-2) , CombatEventDirection.Incoming, null, RemoteRepairType.Hull);
        Report(center, listener, CombatEventKind.RemoteRepair, CombatCategories.Rep, 0, "Remote Hull Repairer II", now.AddSeconds(-2), CombatEventDirection.Outgoing, null, RemoteRepairType.Hull);

        var window = new GameLogMsgWindow(listener, listener, combatOnly: true, showShip: false);
        window.ShowWindow();
        window.UpdateLayout();
        var chips = GetChips(window);
        Dump("chips", chips);

        Check("伤害不进胶囊", chips.All(p => p.Label != "伤害"), string.Join(",", chips.Select(p => p.Label)));
        var repChips = chips.Where(p => p.Label == "遥修").ToList();
        Check("遥修 6 枚独立胶囊", repChips.Count == 6,
            string.Join(" | ", repChips.Select(p => $"{p.Arrow}{p.Amount}{p.Rest}")));
        Check("遥修胶囊带类型后缀",
            repChips.Count(p => p.Rest.Contains("护盾")) == 2
            && repChips.Count(p => p.Rest.Contains("装甲")) == 2
            && repChips.Count(p => p.Rest.Contains("结构")) == 2,
            string.Join(" | ", repChips.Select(p => p.Rest)));
        Check("胶囊箭头：▼ 收到 / ▲ 给出",
            chips.All(p => p.Arrow is "▼" or "▲"),
            string.Join(",", chips.Select(p => p.Arrow).Distinct()));

        var repIn = repChips.FirstOrDefault(p => p.Arrow == "▼");
        var repOut = repChips.FirstOrDefault(p => p.Arrow == "▲");
        Check("遥修 收到=绿、给出=中性白",
            repIn is not null && repOut is not null && ToneOf(repIn.Brush) == "benefit" && ToneOf(repOut.Brush) == "neutral",
            $"in={ToneOf(repIn!.Brush)}, out={ToneOf(repOut!.Brush)}");

        var neutIn = chips.First(p => p.Label == "毁电" && p.Arrow == "▼");
        var neutOut = chips.First(p => p.Label == "毁电" && p.Arrow == "▲");
        Check("毁电 收到=红、给出=绿",
            ToneOf(neutIn.Brush) == "harm" && ToneOf(neutOut.Brush) == "benefit",
            $"in={ToneOf(neutIn.Brush)}, out={ToneOf(neutOut.Brush)}");

        var ecmChip = chips.First(p => p.Label == "ECM");
        Check("ECM 收到=红", ToneOf(ecmChip.Brush) == "harm", ToneOf(ecmChip.Brush));
        var ewChip = chips.First(p => p.Label == "其他电子战");
        Check("其他电子战 收到=红", ToneOf(ewChip.Brush) == "harm", ToneOf(ewChip.Brush));

        var nosChip = chips.First(p => p.Label == "吸电" && p.Arrow == "▼");
        Check("数值胶囊包含数量与速率", nosChip.Amount.Length > 0 && nosChip.Rest.Contains("GJ"), $"{nosChip.Amount} {nosChip.Rest}");
        Check("0 值也显示", repChips.Any(p => p.Amount == "0"), "应包含 0 HP 结构");

        // 数值胶囊按过期时间变灰，状态胶囊按状态变灰时间变灰
        center.StatusGreySeconds = 60;
        Report(center, listener, CombatEventKind.WarpScramble, CombatCategories.Scram, null, "Warp Scrambler II", DateTime.UtcNow.AddSeconds(-120));
        var window2 = new GameLogMsgWindow(listener, listener, combatOnly: true, showShip: false);
        window2.ShowWindow();
        window2.UpdateLayout();
        var chips2 = GetChips(window2);
        Check("状态胶囊超过状态变灰时间后置灰（仍显示）",
            chips2.Any(p => p.Label == "跃迁扰频器" && p.Expired),
            string.Join(",", chips2.Where(p => p.Label == "跃迁扰频器").Select(p => $"{p.Arrow}:{p.Expired}")));
        center.StatusGreySeconds = 300;
        Save(window, "logwindow-chips.png");
        window2.CloseWindow();

        // 伤害卡片：受到 / 输出、最近一次、DPS、可识别属性占比（≥5 次才显示）
        const string damageListener = "Probe Damage Card";
        center.Attach(damageListener);
        var cardNow = DateTime.UtcNow;
        for (var i = 0; i < 4; i++)
        {
            Report(center, damageListener, CombatEventKind.Damage, CombatCategories.Damage, 100, "Scourge Fury Heavy Missile", cardNow.AddSeconds(-4 + i * 0.5), CombatEventDirection.Incoming);
        }

        var beforeFive = center.GetDamageCard(damageListener);
        Check("可识别属性不足 5 次命中时不显示占比",
            !beforeFive.Incoming.HasTypes && beforeFive.Incoming.TypedHits == 4,
            $"hits={beforeFive.Incoming.TypedHits}, hasTypes={beforeFive.Incoming.HasTypes}");

        Report(center, damageListener, CombatEventKind.Damage, CombatCategories.Damage, 100, "Scourge Fury Heavy Missile", cardNow.AddSeconds(-1), CombatEventDirection.Incoming);
        Report(center, damageListener, CombatEventKind.Damage, CombatCategories.Damage, 300, "Focused Medium Pulse Laser II", cardNow.AddSeconds(-1), CombatEventDirection.Outgoing);
        var card = center.GetDamageCard(damageListener);
        Check("伤害卡片：最近一次 + DPS 5 秒 / 30 秒",
            card.Incoming.LastAmount > 0 && card.Incoming.DpsNow > 0 && card.Incoming.DpsAverage > 0,
            $"last={card.Incoming.LastAmount}, dps5={card.Incoming.DpsNow:0.#}, dps30={card.Incoming.DpsAverage:0.#}");
        Check("伤害卡片：输出侧独立统计",
            card.Outgoing.LastAmount == 300 && card.Outgoing.TypedHits == 0,
            $"last={card.Outgoing.LastAmount}, typed={card.Outgoing.TypedHits}");
        Check("可识别属性达到 5 次后显示占比（爆炸 100%）",
            card.Incoming.HasTypes && card.Incoming.Types.Count == 1 && card.Incoming.Types[0].Type == DamageType.Explosive
            && Math.Abs(card.Incoming.Types[0].Percent - 1) < 0.001,
            string.Join(",", card.Incoming.Types.Select(p => $"{p.Type}:{p.Percent:0.##}")));

        var damageWindow = new GameLogMsgWindow(damageListener, damageListener, combatOnly: true, showShip: false);
        damageWindow.ShowWindow();
        damageWindow.UpdateLayout();
        var damageCard = damageWindow.FindName("DamageCard") as FrameworkElement;
        Check("伤害卡片在窗口里显示", damageCard is { Visibility: Visibility.Visible }, $"visibility={damageCard?.Visibility}");
        Save(damageWindow, "logwindow-damage.png");

        // 清除按钮同时清掉胶囊与伤害卡片
        var clearButton = damageWindow.FindName("CombatClearButton") as Button;
        clearButton?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        damageWindow.UpdateLayout();
        var clearedCard = center.GetDamageCard(damageListener);
        Check("清除所有日志同时清掉伤害卡片",
            clearedCard.Incoming.LastAmount == 0 && GetChips(damageWindow).Count == 0,
            $"last={clearedCard.Incoming.LastAmount}, chips={GetChips(damageWindow).Count}");
        damageWindow.CloseWindow();

        // 仅监控窗口：命中列表只显示原文
        var monitor = new GameLogMsgWindow(listener, listener, showShip: false);
        monitor.Show("吉他 3 小鹰级", "本地");
        monitor.Show("6-CZ 5 黑鸟级", "test");
        monitor.ShowWindow();
        monitor.UpdateLayout();
        Check("关键词命中只显示原文",
            FindText(monitor, "吉他 3 小鹰级") is { IsVisible: true } && FindText(monitor, "本地") is null,
            "应只有原文，没有频道/时间");
        Save(monitor, "logwindow-monitor.png");
        monitor.CloseWindow();

        foreach (var name in new[] { listener, damageListener })
        {
            center.Detach(name);
        }

        File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "probe.log"), "DONE" + Environment.NewLine, Encoding.UTF8);
    }

    private static List<CombatChip> GetChips(GameLogMsgWindow window)
        => (typeof(GameLogMsgWindow)
            .GetField("_chips", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(window) as System.Collections.IEnumerable)?.Cast<CombatChip>().ToList() ?? [];

    /// <summary>把胶囊画刷转换成 harm / benefit / neutral 三种语义。</summary>
    private static string ToneOf(Brush brush)
    {
        if (brush is not SolidColorBrush solid)
        {
            return "unknown";
        }

        var color = solid.Color;
        if (color.R > color.G + 20)
        {
            return "harm";
        }

        if (color.G > color.R + 20)
        {
            return "benefit";
        }

        return "neutral";
    }

    private static void Report(
        CombatLogStateCenter center,
        string listener,
        CombatEventKind kind,
        string category,
        double? amount,
        string module,
        DateTime time,
        CombatEventDirection direction = CombatEventDirection.Incoming,
        string? hint = null,
        RemoteRepairType repairType = RemoteRepairType.None)
    {
        center.Report(listener, new CombatLogEvent
        {
            Time = time,
            Kind = kind,
            CategoryKey = category,
            Direction = direction,
            Amount = amount,
            Unit = amount.HasValue ? (category is CombatCategories.Damage or CombatCategories.Rep ? "HP" : "GJ") : null,
            ModuleName = module,
            ModuleHintName = hint,
            RepairType = repairType,
        });
    }

    private static void Dump(string tag, IReadOnlyList<CombatChip> chips)
    {
        var text = string.Join(" | ", chips.Select(p => $"{p.Label}{p.Arrow}{p.Amount} {p.Rest}({ToneOf(p.Brush)}{(p.Expired ? ",expired" : "")})"));
        File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "probe.log"), $"{tag}: {text}{Environment.NewLine}", Encoding.UTF8);
    }

    private static void LoadResources(Application app)
    {
        DataPathService.Initialize();
        SettingsService.Initialize();
        var themes = new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Light };
        var controls = new Wpf.Ui.Markup.ControlsDictionary();
        app.Resources.MergedDictionaries.Add(themes);
        app.Resources.MergedDictionaries.Add(controls);
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/TheGuideToTheNewEden;component/Resources/Languages/zh-CN.xaml"),
        });
    }

    private static void Check(string name, bool ok, string detail)
    {
        File.AppendAllText(
            Path.Combine(AppContext.BaseDirectory, "probe.log"),
            $"[{(ok ? "PASS" : "FAIL")}] {name}：{detail}{Environment.NewLine}",
            Encoding.UTF8);
    }

    private static TextBlock? FindText(DependencyObject root, string text)
    {
        if (root is TextBlock block && block.Text == text)
        {
            return block;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var found = FindText(VisualTreeHelper.GetChild(root, i), text);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static void Save(Window window, string name)
    {
        var dpi = VisualTreeHelper.GetDpi(window);
        var width = (int)Math.Ceiling(window.ActualWidth * dpi.DpiScaleX);
        var height = (int)Math.Ceiling(window.ActualHeight * dpi.DpiScaleY);
        var bitmap = new RenderTargetBitmap(width, height, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(AppContext.BaseDirectory, name));
        encoder.Save(stream);
        File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "probe.log"), $"{name}: {width}x{height}{Environment.NewLine}", Encoding.UTF8);
    }
}
