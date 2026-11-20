using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TheGuideToTheNewEden.Core.Models.CombatLog;
using TheGuideToTheNewEden.Core.Services.CombatLog;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.CombatLog;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace LogWindowProbe;

/// <summary>
/// 阶段 359 探针：渲染重设计后的日志监控窗口（战斗 + 监控 / 仅战斗 / 仅监控），
/// 用于核对卡片、胶囊、命中列表与自适应布局。
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
        center.TtlSeconds = 30;
        center.SetEnabledCategories(CombatCategories.All.Select(p => p.Key));
        const string listener = "Probe Combat";
        center.Attach(listener);

        var now = DateTime.UtcNow;
        Report(center, listener, CombatEventKind.Damage, CombatCategories.Damage, 100, "Focused Medium Pulse Laser II", now.AddSeconds(-4));
        Report(center, listener, CombatEventKind.Damage, CombatCategories.Damage, 200, "Focused Medium Pulse Laser II", now.AddSeconds(-1));
        Report(center, listener, CombatEventKind.CapacitorNeutralize, CombatCategories.Neut, 45, "Medium Energy Neutralizer II", now.AddSeconds(-4));
        Report(center, listener, CombatEventKind.CapacitorNeutralize, CombatCategories.Neut, 45, "Medium Energy Neutralizer II", now.AddSeconds(-2));
        Report(center, listener, CombatEventKind.RemoteRepair, CombatCategories.Rep, 64, "Remote Armor Repairer II", now.AddSeconds(-4));
        Report(center, listener, CombatEventKind.RemoteRepair, CombatCategories.Rep, 64, "Remote Armor Repairer II", now.AddSeconds(-1));
        Report(center, listener, CombatEventKind.CapacitorDrain, CombatCategories.Nos, 15, "Small Energy Nosferatu II", now.AddSeconds(-4));
        Report(center, listener, CombatEventKind.CapacitorDrain, CombatCategories.Nos, 15, "Small Energy Nosferatu II", now.AddSeconds(-2));
        Report(center, listener, CombatEventKind.RemoteCapacitor, CombatCategories.CapTransfer, 20, "Remote Capacitor Transmitter II", now.AddSeconds(-4));
        Report(center, listener, CombatEventKind.RemoteCapacitor, CombatCategories.CapTransfer, 20, "Remote Capacitor Transmitter II", now.AddSeconds(-1));
        Report(center, listener, CombatEventKind.WarpScramble, CombatCategories.Scram, null, "Warp Scrambler II", now.AddSeconds(-30));
        Report(center, listener, CombatEventKind.EcmJam, CombatCategories.Ecm, null, "Magnetic ECM II", now.AddSeconds(-20));

        // ① 战斗 + 监控
        var both = new GameLogMsgWindow(listener, listener, showShip: false, titleProvider: () => listener);
        both.Show("吉他 3 小鹰级", "本地");
        both.Show("6-CZ 5 黑鸟级", "test");
        both.Show("诱导检测：QEDSD", "本地");
        ((Window)both).Show();
        both.UpdateLayout();
        DumpCards(both, "both");
        Save(both, "logwindow-both.png");

        both.CloseWindow();

        // ② 仅战斗
        var combatOnly = new GameLogMsgWindow(listener, listener, combatOnly: true, showShip: false);
        combatOnly.ShowWindow();
        combatOnly.UpdateLayout();
        Save(combatOnly, "logwindow-combat-only.png");
        combatOnly.CloseWindow();

        // ③ 仅监控
        const string monitorOnlyListener = "Probe Monitor";
        var monitorOnly = new GameLogMsgWindow(monitorOnlyListener, monitorOnlyListener, showShip: false);
        monitorOnly.Show("吉他 3 小鹰级", "本地");
        monitorOnly.Show("6-CZ 5 黑鸟级", "test");
        monitorOnly.Show("诱导检测：QEDSD", "本地");
        monitorOnly.Show("JV-1 8 狂怒者级", "本地");
        monitorOnly.Show("3-T 2 灵感级", "test");
        monitorOnly.Show("吉他 0 因卡萨斯级", "本地");
        ((Window)monitorOnly).Show();
        monitorOnly.UpdateLayout();
        Save(monitorOnly, "logwindow-monitor-only.png");
        monitorOnly.CloseWindow();

        // ④ 战斗已挂载但暂无事件（无战斗 + 暂无命中）
        const string idleListener = "Probe Idle";
        center.Attach(idleListener);
        var idle = new GameLogMsgWindow(idleListener, idleListener, showShip: false);
        ((Window)idle).Show();
        idle.UpdateLayout();
        Save(idle, "logwindow-idle.png");
        idle.CloseWindow();
        center.Detach(idleListener);

        // ⑤ 共享窗口：战斗先建（仅战斗），关键词后到 → 同一个窗口升级成双区
        var shared = GameLogWindowRegistry.Acquire(
            listener,
            "combat",
            () => new GameLogMsgWindow(listener, listener, combatOnly: true, showShip: false));
        shared.ShowWindow();
        var shared2 = GameLogWindowRegistry.Acquire(
            listener,
            "monitor:probe",
            () => throw new InvalidOperationException("should reuse"));
        shared2.EnableMonitor();
        shared2.Show("关键词命中：共享窗口", "本地");
        ((Window)shared2).Show();
        shared2.UpdateLayout();
        Save(shared2, "logwindow-shared.png");
        GameLogWindowRegistry.Release(listener, "monitor:probe");
        GameLogWindowRegistry.Release(listener, "combat");
        // ⑥ 清除战斗状态后：战斗区隐藏，只剩命中列表
        center.Clear(listener);
        var hitsOnly = new GameLogMsgWindow(listener, listener, showShip: false);
        hitsOnly.Show("吉他 3 小鹰级", "本地");
        hitsOnly.Show("6-CZ 5 黑鸟级", "test");
        ((Window)hitsOnly).Show();
        hitsOnly.UpdateLayout();
        Save(hitsOnly, "logwindow-hits-only.png");
        hitsOnly.CloseWindow();

        center.Detach(listener);
    }

    private static void Report(
        CombatLogStateCenter center,
        string listener,
        CombatEventKind kind,
        string category,
        double? amount,
        string module,
        DateTime time)
    {
        center.Report(listener, new CombatLogEvent
        {
            Kind = kind,
            Direction = CombatEventDirection.Incoming,
            CategoryKey = category,
            Amount = amount,
            ModuleName = module,
            Pilot = "Guardian",
            Time = time,
        });
    }

    private static void LoadResources(Application app)
    {
        var themes = new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Light };
        var controls = new Wpf.Ui.Markup.ControlsDictionary();
        app.Resources.MergedDictionaries.Add(themes);
        app.Resources.MergedDictionaries.Add(controls);
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/TheGuideToTheNewEden;component/Resources/Languages/zh-CN.xaml"),
        });
    }

    private static void DumpCards(GameLogMsgWindow window, string tag)
    {
        var list = (ItemsControl)window.FindName("CardsList");
        var field = typeof(GameLogMsgWindow).GetField("_cards",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cards = (System.Collections.IEnumerable?)field?.GetValue(window);
        var text = new StringBuilder();
        text.AppendLine($"{tag}: list={list.ActualWidth:0.#}x{list.ActualHeight:0.#} count={list.Items.Count}");
        if (cards is not null)
        {
            foreach (var card in cards)
            {
                var width = card.GetType().GetProperty("Width")?.GetValue(card);
                var height = card.GetType().GetProperty("Height")?.GetValue(card);
                text.AppendLine($"  card {width} x {height}");
            }
        }

        File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "probe.log"), text.ToString(), Encoding.UTF8);
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
