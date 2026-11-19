using System.Drawing;
using System.IO;
using System.Text;
using System.Windows;
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
        center.TtlSeconds = 5;
        center.SetEnabledCategories(CombatCategories.All.Select(p => p.Key));
        center.Attach(listener);
        Check("战斗中心已挂载", center.IsAttached(listener), "Attach 后应为 true");

        center.Report(listener, new CombatLogEvent
        {
            Kind = CombatEventKind.Damage,
            Direction = CombatEventDirection.Incoming,
            CategoryKey = CombatCategories.Damage,
            Amount = 100,
            ModuleName = "Focused Medium Pulse Laser II",
            Pilot = "Guardian",
            Time = DateTime.UtcNow,
        });
        var damageRow = center.GetRows(listener).FirstOrDefault(p => p.Category == CombatCategories.Damage);
        Check("承伤=100HP 20DPS（5 秒窗口）", damageRow?.Text == "承伤 100HP 20DPS", $"实际 {damageRow?.Text}");

        center.Report(listener, new CombatLogEvent
        {
            Kind = CombatEventKind.CapacitorNeutralize,
            Direction = CombatEventDirection.Incoming,
            CategoryKey = CombatCategories.Neut,
            Amount = 45,
            ModuleName = "Medium Energy Neutralizer II",
            Pilot = "Guardian",
            Time = DateTime.UtcNow,
        });
        var neutRow = center.GetRows(listener).FirstOrDefault(p => p.Category == CombatCategories.Neut);
        Check("毁电=45GJ 45GJ", neutRow?.Text == "毁电 45GJ 45GJ", $"实际 {neutRow?.Text}");

        center.Report(listener, new CombatLogEvent
        {
            Kind = CombatEventKind.RemoteRepair,
            Direction = CombatEventDirection.Incoming,
            CategoryKey = CombatCategories.Rep,
            Amount = 123,
            Time = DateTime.UtcNow,
        });
        var repRow = center.GetRows(listener).FirstOrDefault(p => p.Category == CombatCategories.Rep);
        Check("遥修=123HP 123HP", repRow?.Text == "遥修 123HP 123HP", $"实际 {repRow?.Text}");

        center.SetCategoryEnabled(CombatCategories.Damage, false);
        Check("分类裁剪：承伤隐藏", center.GetRows(listener).All(p => p.Category != CombatCategories.Damage), "关闭后不应有承伤行");
        center.SetCategoryEnabled(CombatCategories.Damage, true);

        center.TtlSeconds = 1;
        Thread.Sleep(1200);
        Check("1 秒无事件后清空", center.GetRows(listener).Count == 0, string.Join(" | ", center.GetRows(listener).Select(p => p.Text)));
        center.TtlSeconds = 5;

        center.Detach(listener);
        Check("战斗中心已摘除", !center.IsAttached(listener), "Detach 后应为 false");
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
            30,
            System.Drawing.Color.FromArgb(255, 255, 214, 64),
            System.Drawing.Color.FromArgb(200, 120, 20, 20));
        Check("预警文字字号可独立设置", Math.Abs(window.InfoFontSize - 30) < 0.01, $"实际 {window.InfoFontSize}");
        Check("预警文字色可独立配色",
            window.InfoForeground is SolidColorBrush fg && fg.Color.G > 200 && fg.Color.R > 200,
            $"实际 {Describe(window.InfoForeground)}");
        Check("预警背景色可独立配色",
            window.InfoBackground is SolidColorBrush bg && bg.Color.R > 100 && bg.Color.A == 200,
            $"实际 {Describe(window.InfoBackground)}");
        Check("角色名配色不受预警配色影响",
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
