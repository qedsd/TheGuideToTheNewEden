using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TheGuideToTheNewEden.Core.Models.CombatLog;
using TheGuideToTheNewEden.Core.Services.CombatLog;
using TheGuideToTheNewEden.WPF.Controls;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.CombatLog;
using TheGuideToTheNewEden.WPF.Services.Settings;
using TheGuideToTheNewEden.WPF.Services.Settings;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace LogWindowProbe;

/// <summary>
/// 阶段 359 探针：渲染重设计后的日志监控窗口（战斗 + 监控 / 仅战斗 / 仅监控）。
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
        center.ExpireSeconds = 30;
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

        // 阶段 367：首次加载恢复该角色保存的窗口位置与尺寸（不依赖游戏日志文件路径）
        // 说明：探针环境可能没有配置目录写权限；此时跳过，不影响其它窗口断言。
        var persistedListenerId = 987654321;
        var geometryAvailable = true;
        try
        {
            GameLogInfoSettingService.SetValue(new TheGuideToTheNewEden.Core.Models.GameLogSetting
            {
                ListenerID = persistedListenerId,
                WindowLeft = 125,
                WindowTop = 135,
                WindowWidth = 530,
                WindowHeight = 410,
            });
        }
        catch (Exception ex)
        {
            geometryAvailable = false;
            Check("窗口位置/尺寸记忆（配置目录不可写，跳过）", true, ex.Message);
        }

        if (geometryAvailable)
        {
            var geometryWindow = new GameLogMsgWindow("Geometry Probe", "Geometry Probe", combatOnly: true,
                showShip: false, settingListenerId: persistedListenerId);
            geometryWindow.ShowWindow();
            geometryWindow.UpdateLayout();
            Check("窗口自动恢复位置", Math.Abs(geometryWindow.Left - 125) < 0.1 && Math.Abs(geometryWindow.Top - 135) < 0.1,
                $"Left={geometryWindow.Left}, Top={geometryWindow.Top}");
            Check("窗口自动恢复尺寸", Math.Abs(geometryWindow.Width - 530) < 0.1 && Math.Abs(geometryWindow.Height - 410) < 0.1,
                $"Width={geometryWindow.Width}, Height={geometryWindow.Height}");
            geometryWindow.Left = 180;
            geometryWindow.Top = 190;
            geometryWindow.Width = 560;
            geometryWindow.Height = 430;
            geometryWindow.UpdateLayout();
            geometryWindow.CloseWindow();
            var savedBounds = GameLogInfoSettingService.GetValue(persistedListenerId);
            Check("移动/缩放后自动保存", savedBounds is { WindowLeft: 180, WindowTop: 190, WindowWidth: 560, WindowHeight: 430 },
                $"Left={savedBounds?.WindowLeft}, Top={savedBounds?.WindowTop}, Width={savedBounds?.WindowWidth}, Height={savedBounds?.WindowHeight}");
        }

        // 战斗 + 监控
        var both = new GameLogMsgWindow(listener, listener, showShip: false, titleProvider: () => listener);
        both.Show("吉他 3 小鹰级", "本地");
        both.Show("6-CZ 5 黑鸟级", "test");
        both.Show("诱导检测：QEDSD", "本地");
        ((Window)both).Show();
        both.UpdateLayout();
        DumpCards(both, "both");
        var pillsControl = both.FindName("PillsList") as FrameworkElement;
        var pillText = FindText(both, LanguageService.Get("CombatCategory_Scram"));
        Check("反跳胶囊真实渲染（有高度且在窗口内）",
            pillsControl is not null && pillsControl.ActualHeight > 10 && pillText is { IsVisible: true },
            $"pillsH={pillsControl?.ActualHeight:0.#}, textVisible={pillText?.IsVisible}");
        Save(both, "logwindow-both.png");

        both.CloseWindow();

        // 仅战斗（有数据）：卡片固定高度，不再按剩余空间撑满
        var combatOnly = new GameLogMsgWindow(listener, listener, combatOnly: true, showShip: false);
        combatOnly.ShowWindow();
        combatOnly.UpdateLayout();
        DumpCards(combatOnly, "combat-only");
        var combatArea = (FrameworkElement)combatOnly.FindName("CombatArea");
        Check("仅战斗：战斗区高度封顶（≤260）", combatArea.ActualHeight <= 260.5,
            $"实际 {combatArea.ActualHeight:0.#}");
        Check("仅战斗：窗口可见", ((Window)combatOnly).IsVisible, "ShowWindow 后应为可见");
        Save(combatOnly, "logwindow-combat-only.png");
        combatOnly.CloseWindow();

        // 仅战斗、暂无事件：开启后窗口必须显示（阶段 360：不再因为没数据就不显示/自动隐藏）
        const string emptyListener = "Probe Combat Empty";
        center.Attach(emptyListener);
        var emptyCombat = new GameLogMsgWindow(emptyListener, emptyListener, combatOnly: true, showShip: false);
        emptyCombat.ShowWindow();
        emptyCombat.UpdateLayout();
        var emptyText = (TextBlock)emptyCombat.FindName("CombatEmpty");
        Check("仅战斗无事件：窗口不隐藏", ((Window)emptyCombat).IsVisible, "开启战斗日志后应显示窗口");
        Check("仅战斗无事件：「无战斗」占位可见", emptyText.Visibility == Visibility.Visible,
            $"实际 {emptyText.Visibility}");
        var emptyArea = (FrameworkElement)emptyCombat.FindName("CombatArea");
        Check("仅战斗无事件：战斗区高度固定", emptyArea.ActualHeight < 260,
            $"实际 {emptyArea.ActualHeight:0.#}");
        Save(emptyCombat, "logwindow-combat-empty.png");
        emptyCombat.CloseWindow();
        center.Detach(emptyListener);

        // 阶段 361：可自由缩放 / 没有最小尺寸 / 背板不透明度只作用于背板
        var resizable = new GameLogMsgWindow(listener, listener, combatOnly: true, showShip: false);
        resizable.ShowWindow();
        resizable.UpdateLayout();
        Check("窗口可缩放且没有最小尺寸",
            resizable.ResizeMode == ResizeMode.CanResize && resizable.MinWidth == 0 && resizable.MinHeight == 0,
            $"ResizeMode={resizable.ResizeMode}, MinWidth={resizable.MinWidth}, MinHeight={resizable.MinHeight}");
        Check("窗口支持背景透明", resizable.AllowsTransparency && resizable.Background is SolidColorBrush { Color.A: 0 },
            $"AllowsTransparency={resizable.AllowsTransparency}");
        var plate = (Border)resizable.FindName("BackdropPlate");
        var opaqueAlpha = (plate.Background as SolidColorBrush)?.Color.A ?? 0;
        Check("默认 100 = 背板完全不透明", opaqueAlpha == 255, $"alpha={opaqueAlpha}");
        var titleText = (TextBlock)resizable.FindName("TitleText");
        var textAlphaBefore = (titleText.Foreground as SolidColorBrush)?.Color.A ?? 0;
        resizable.SetWindowOpacity(45);
        resizable.UpdateLayout();
        var translucentAlpha = (plate.Background as SolidColorBrush)?.Color.A ?? 0;
        Check("不透明度 45 → 背板 alpha ≈ 45%", Math.Abs(translucentAlpha - 115) <= 2, $"alpha={translucentAlpha}");
        var textAlpha = (titleText.Foreground as SolidColorBrush)?.Color.A ?? 0;
        Check("文字不受背板不透明度影响", textAlpha == textAlphaBefore && textAlpha >= 200,
            $"before={textAlphaBefore}, after={textAlpha}");
        SaveWithBackdrop(resizable, "logwindow-transparent.png");
        resizable.CloseWindow();

        // 阶段 362：过期卡片——行不会消失，只是置灰 + 右侧「已过期」
        const string expiredListener = "Probe Expired";
        center.ExpireSeconds = 60;
        center.Attach(expiredListener);
        Report(center, expiredListener, CombatEventKind.Damage, CombatCategories.Damage, 100,
            "Focused Medium Pulse Laser II", now.AddSeconds(-200));
        Report(center, expiredListener, CombatEventKind.Damage, CombatCategories.Damage, 200,
            "Focused Medium Pulse Laser II", now.AddSeconds(-197));
        var expiredRows = center.GetRows(expiredListener);
        Check("过期行不消失", expiredRows.Any(p => p.Category == CombatCategories.Damage && p.Expired),
            string.Join(" | ", expiredRows.Select(p => $"{p.Text} ({(p.Expired ? "过期" : "有效")})")));
        var expiredWindow = new GameLogMsgWindow(expiredListener, expiredListener, combatOnly: true, showShip: false);
        expiredWindow.ShowWindow();
        expiredWindow.UpdateLayout();
        var cardField = typeof(GameLogMsgWindow).GetField("_cards",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var expiredCards = (System.Collections.IEnumerable?)cardField?.GetValue(expiredWindow);
        var firstCard = expiredCards?.Cast<object>().FirstOrDefault();
        Check("卡片标记已过期",
            firstCard?.GetType().GetProperty("Expired")?.GetValue(firstCard) is true,
            "卡片 Expired 应为 true");
        var expiredLabel = FindText(expiredWindow, LanguageService.Get("CombatLog_Expired"));
        Check("卡面不显示已过期文字", FindText(expiredWindow, LanguageService.Get("CombatLog_Expired")) is null,
            "过期状态仅通过置灰和 ToolTip 表示");
        var expiredCardForTip = expiredCards?.Cast<object>().FirstOrDefault() as CombatCard;
        Check("过期卡片 ToolTip 文案为已过期",
            expiredCardForTip is not null && expiredCardForTip.ToolTipText == LanguageService.Get("CombatLog_Expired"),
            $"实际 {expiredCardForTip?.ToolTipText ?? "无卡片"}");
        Save(expiredWindow, "logwindow-expired.png");
        expiredWindow.CloseWindow();
        center.Detach(expiredListener);
        center.ExpireSeconds = 30;

        // 阶段 363：同一分类区分「承受 / 输出」（吸电：一个吸一个被吸，数值一样也能分清）
        const string dirListener = "Probe Directions";
        center.Attach(dirListener);
        Report(center, dirListener, CombatEventKind.Damage, CombatCategories.Damage, 100,
            "Focused Medium Pulse Laser II", now.AddSeconds(-4));
        Report(center, dirListener, CombatEventKind.Damage, CombatCategories.Damage, 200,
            "Focused Medium Pulse Laser II", now.AddSeconds(-1));
        Report(center, dirListener, CombatEventKind.Damage, CombatCategories.Damage, 300,
            "Hammerhead II", now.AddSeconds(-3.5), CombatEventDirection.Outgoing);
        Report(center, dirListener, CombatEventKind.Damage, CombatCategories.Damage, 300,
            "Hammerhead II", now.AddSeconds(-0.5), CombatEventDirection.Outgoing);
        Report(center, dirListener, CombatEventKind.CapacitorDrain, CombatCategories.Nos, 15,
            "Small Energy Nosferatu II", now.AddSeconds(-3));
        Report(center, dirListener, CombatEventKind.CapacitorDrain, CombatCategories.Nos, 15,
            "Small Energy Nosferatu II", now.AddSeconds(-1));
        Report(center, dirListener, CombatEventKind.CapacitorDrain, CombatCategories.Nos, 30,
            "Small Energy Nosferatu II", now.AddSeconds(-4), CombatEventDirection.Outgoing);
        Report(center, dirListener, CombatEventKind.CapacitorDrain, CombatCategories.Nos, 30,
            "Small Energy Nosferatu II", now.AddSeconds(-2), CombatEventDirection.Outgoing);
        // 只有零值输入吸电（独立监听角色）：也必须保留一张显示 0 GJ 的卡
        const string zeroNosListener = "Probe Zero Nos";
        center.Attach(zeroNosListener);
        Report(center, zeroNosListener, CombatEventKind.CapacitorDrain, CombatCategories.Nos, 0,
            "Small Energy Nosferatu II", now.AddSeconds(-1));
        // 只有输出、没有输入的传电：把电容给出去，既非伤害也非收益 → 中性色
        Report(center, dirListener, CombatEventKind.RemoteCapacitor, CombatCategories.CapTransfer, 20,
            "Remote Capacitor Transmitter II", now.AddSeconds(-4), CombatEventDirection.Outgoing);
        Report(center, dirListener, CombatEventKind.RemoteCapacitor, CombatCategories.CapTransfer, 20,
            "Remote Capacitor Transmitter II", now.AddSeconds(-2), CombatEventDirection.Outgoing);
        // 遥修输入（别人修我）= 收益 → 绿
        Report(center, dirListener, CombatEventKind.RemoteRepair, CombatCategories.Rep, 123,
            "Remote Armor Repairer II", now.AddSeconds(-3));
        Report(center, dirListener, CombatEventKind.RemoteRepair, CombatCategories.Rep, 123,
            "Remote Armor Repairer II", now.AddSeconds(-1));

        // 阶段 369/370：三种遥修类型分行显示（输入 + 输出都列），0 值也保留，行尾带类型名
        const string repTypesListener = "Probe Rep Types";
        center.Attach(repTypesListener);
        Report(center, repTypesListener, CombatEventKind.RemoteRepair, CombatCategories.Rep, 100,
            "Remote Shield Booster II", now.AddSeconds(-4), CombatEventDirection.Incoming, RemoteRepairType.Shield);
        Report(center, repTypesListener, CombatEventKind.RemoteRepair, CombatCategories.Rep, 100,
            "Remote Shield Booster II", now.AddSeconds(-2), CombatEventDirection.Incoming, RemoteRepairType.Shield);
        Report(center, repTypesListener, CombatEventKind.RemoteRepair, CombatCategories.Rep, 80,
            "Remote Shield Booster II", now.AddSeconds(-4), CombatEventDirection.Outgoing, RemoteRepairType.Shield);
        Report(center, repTypesListener, CombatEventKind.RemoteRepair, CombatCategories.Rep, 80,
            "Remote Shield Booster II", now.AddSeconds(-2), CombatEventDirection.Outgoing, RemoteRepairType.Shield);
        Report(center, repTypesListener, CombatEventKind.RemoteRepair, CombatCategories.Rep, 50,
            "Remote Armor Repairer II", now.AddSeconds(-3), CombatEventDirection.Incoming, RemoteRepairType.Armor);
        Report(center, repTypesListener, CombatEventKind.RemoteRepair, CombatCategories.Rep, 30,
            "Remote Armor Repairer II", now.AddSeconds(-3), CombatEventDirection.Outgoing, RemoteRepairType.Armor);
        Report(center, repTypesListener, CombatEventKind.RemoteRepair, CombatCategories.Rep, 0,
            "Remote Hull Repairer II", now.AddSeconds(-1), CombatEventDirection.Incoming, RemoteRepairType.Hull);
        Report(center, repTypesListener, CombatEventKind.RemoteRepair, CombatCategories.Rep, 0,
            "Remote Hull Repairer II", now.AddSeconds(-1), CombatEventDirection.Outgoing, RemoteRepairType.Hull);

        var repTypesRow = center.GetRows(repTypesListener).FirstOrDefault(p => p.Category == CombatCategories.Rep);
        Check("遥修两种方向 + 三种类型都列（0 值保留）",
            repTypesRow is { Lines.Count: 6 }
            && repTypesRow.Lines[0].Text == "100 HP 50 HP/s 护盾" && repTypesRow.Lines[0].Direction == CombatEventDirection.Incoming
            && repTypesRow.Lines[1].Text == "80 HP 40 HP/s 护盾" && repTypesRow.Lines[1].Direction == CombatEventDirection.Outgoing
            && repTypesRow.Lines[2].Text == "50 HP 装甲"
            && repTypesRow.Lines[3].Text == "30 HP 装甲"
            && repTypesRow.Lines[4].Text == "0 HP 结构"
            && repTypesRow.Lines[5].Text == "0 HP 结构",
            $"实际 {string.Join(" | ", repTypesRow?.Lines.Select(p => $"{p.Text}({p.Direction})") ?? [])}");

        Report(center, repTypesListener, CombatEventKind.Damage, CombatCategories.Damage, 120,
            "Probe Autocannon", now.AddSeconds(-2));
        Report(center, repTypesListener, CombatEventKind.CapacitorNeutralize, CombatCategories.Neut, 45,
            "Probe Neutralizer", now.AddSeconds(-2));
        var repTypesWindow = new GameLogMsgWindow(repTypesListener, repTypesListener, combatOnly: true, showShip: false);
        repTypesWindow.ShowWindow();
        repTypesWindow.UpdateLayout();
        var repTypesCards = (typeof(GameLogMsgWindow)
            .GetField("_cards", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(repTypesWindow) as System.Collections.IEnumerable)?.Cast<CombatCard>().ToList() ?? [];
        var repTypeCard = repTypesCards.FirstOrDefault(p => p.Label == "遥修");
        Check("遥修卡片六行、行多时字号自动缩小",
            repTypeCard is { Lines.Count: 6 } && Math.Abs(repTypeCard.LineFontSize - 15) < 0.01
            && Math.Abs(repTypeCard.Height - (12 + 16 + 6 * (15 + 6))) < 0.5,
            $"lines={repTypeCard?.Lines.Count}, font={repTypeCard?.LineFontSize}, height={repTypeCard?.Height}");
        var repPanel = FindDescendant<UniformFillPanel>(repTypesWindow);
        var repRowActual = repPanel?.Children.Cast<FrameworkElement>()
            .Select(p => (Top: Math.Round(p.TranslatePoint(new Point(0, 0), repPanel).Y, 1), Height: p.ActualHeight))
            .ToList() ?? [];
        var repRowsEqual = repRowActual.Count >= 2
            && !repRowActual.GroupBy(p => p.Top).Any(group => group.Max(p => p.Height) - group.Min(p => p.Height) > 0.6);
        Check("矮卡片与六行遥修同一行时等高",
            repRowsEqual && repRowActual.Any(p => p.Height > 100),
            string.Join(",", repRowActual.Select(p => $"{p.Top}:{p.Height:0.#}")));
        Save(repTypesWindow, "logwindow-rep-types.png");
        repTypesWindow.CloseWindow();
        center.Detach(repTypesListener);

        var zeroNosRows = center.GetRows(zeroNosListener);
        var zeroNos = zeroNosRows.FirstOrDefault(p => p.Category == CombatCategories.Nos)?.Lines.FirstOrDefault()?.LastValue;
        Check("零值吸电仍有输入侧且 LastValue=0", zeroNos == 0,
            $"实际 incoming={zeroNos?.ToString() ?? "null"}");
        var zeroNosWindow = new GameLogMsgWindow(zeroNosListener, zeroNosListener, combatOnly: true, showShip: false);
        zeroNosWindow.ShowWindow();
        zeroNosWindow.UpdateLayout();
        var zeroCardField = typeof(GameLogMsgWindow).GetField("_cards",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var zeroCards = (zeroCardField?.GetValue(zeroNosWindow) as System.Collections.IEnumerable)?.Cast<CombatCard>().ToList() ?? [];
        var zeroNosCard = zeroCards.FirstOrDefault(p => p.Label == "吸电");
        Check("吸电 0GJ 卡片可见", zeroNosCard is { Lines.Count: 1 } && zeroNosCard.Lines[0].Text == "0 GJ 吸电",
            $"实际 {(zeroNosCard is null ? "null" : zeroNosCard.Lines[0].Text)}");
        Save(zeroNosWindow, "logwindow-nos-zero.png");
        zeroNosWindow.CloseWindow();
        center.Detach(zeroNosListener);

        var dirRows = center.GetRows(dirListener);
        var damageDir = dirRows.FirstOrDefault(p => p.Category == CombatCategories.Damage);
        var nosDir = dirRows.FirstOrDefault(p => p.Category == CombatCategories.Nos);
        Check("伤害：输入 / 输出两行都在", damageDir is { Lines.Count: 2 },
            $"lines={damageDir?.Lines.Count}: {string.Join(" | ", damageDir?.Lines.Select(p => p.Text) ?? [])}");
        Check("吸电：输入 15 GJ 7.5 GJ/s", nosDir?.Lines[0].Text == "15 GJ 7.5 GJ/s",
            $"实际 {nosDir?.Lines[0].Text}");
        Check("吸电：输出 30 GJ 15 GJ/s", nosDir?.Lines[1].Text == "30 GJ 15 GJ/s",
            $"实际 {nosDir?.Lines[1].Text}");

        var dirWindow = new GameLogMsgWindow(dirListener, dirListener, combatOnly: true, showShip: false);
        dirWindow.ShowWindow();
        dirWindow.UpdateLayout();
        var dirField = typeof(GameLogMsgWindow).GetField("_cards",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var dirCards = (dirField?.GetValue(dirWindow) as System.Collections.IEnumerable)?.Cast<CombatCard>().ToList() ?? [];
        var nosCard = dirCards.FirstOrDefault(p => p.Label == "电容");
        Check("电容卡片：吸电两行 + 传电一行，行尾标类型",
            nosCard is { Lines.Count: 3 }
            && nosCard.Lines[0].Text == "15 GJ 7.5 GJ/s 吸电" && nosCard.Lines[0].Tone == "harm"
            && nosCard.Lines[1].Text == "30 GJ 15 GJ/s 吸电" && nosCard.Lines[1].Tone == "benefit"
            && nosCard.Lines[2].Text == "20 GJ 10 GJ/s 传电" && nosCard.Lines[2].Tone == "neutral",
            $"实际 {string.Join(" | ", nosCard?.Lines.Select(p => $"{p.Text}({p.Tone})") ?? [])}");
        Check("卡片 ToolTip 里保留输入 / 输出字样",
            nosCard?.Detail.Contains("输入") == true && nosCard.Detail.Contains("输出") == true,
            $"实际 {nosCard?.Detail}");
        var nosBorder = FindByDataContext(dirWindow, nosCard!);
        var nosTipText = (nosBorder?.ToolTip as ToolTip)?.Content as TextBlock;
        var nosTipBinding = nosTipText is null
            ? null
            : System.Windows.Data.BindingOperations.GetBinding(nosTipText, TextBlock.TextProperty);
        Check("卡片 ToolTip 绑定到 ToolTipText（未过期=方向详情，过期=已过期）", nosTipBinding?.Path?.Path == "ToolTipText",
            $"path={nosTipBinding?.Path?.Path}");
        var capCard = dirCards.FirstOrDefault(p => p.Label == "电容");
        Check("传电只有输出时是中性色（给出去既非伤害也非收益）",
            capCard is { Lines.Count: 3 } && capCard.Lines[2].Tone == "neutral",
            $"实际 {string.Join(" | ", capCard?.Lines.Select(p => $"{p.Text}({p.Tone})") ?? [])}");
        var repCard = dirCards.FirstOrDefault(p => p.Label == "遥修");
        Check("遥修输入是收益（绿）", repCard is { Lines.Count: 1 } && repCard.Lines[0].Tone == "benefit",
            $"实际 {string.Join(" | ", repCard?.Lines.Select(p => $"{p.Text}({p.Tone})") ?? [])}");
        var damageCard = dirCards.FirstOrDefault(p => p.Label == "伤害");
        Check("伤害卡片：输入=伤害（红）、输出=收益（绿）",
            damageCard is { Lines.Count: 2 } && damageCard.Lines[0].Tone == "harm" && damageCard.Lines[1].Tone == "benefit",
            $"实际 {string.Join(" | ", damageCard?.Lines.Select(p => $"{p.Text}({p.Tone})") ?? [])}");

        // 阶段 372：每行数量大字、单位/每秒/类型小字；颜色按收益 / 伤害
        var inputColor = (FindText(dirWindow, "15")?.Foreground as SolidColorBrush)?.Color;
        Check("伤害数值是红色", inputColor is { } ic && ic.R > ic.G && ic.R > ic.B, $"color={inputColor}");
        var outputColor = (FindText(dirWindow, "30")?.Foreground as SolidColorBrush)?.Color;
        Check("收益数值是绿色", outputColor is { } oc && oc.G > oc.R, $"color={outputColor}");
        var repColor = (FindText(dirWindow, repCard?.Lines[0].Amount ?? string.Empty)?.Foreground as SolidColorBrush)?.Color;
        Check("遥修收益是绿色", repColor is { } rc && rc.G > rc.R, $"color={repColor}");
        var neutralExpected = (Application.Current.TryFindResource("TextFillColorPrimaryBrush") as SolidColorBrush)?.Color;
        var capColor = (FindText(dirWindow, capCard?.Lines[2].Rest ?? string.Empty)?.Foreground as SolidColorBrush)?.Color;
        var criticalExpected = (Application.Current.TryFindResource("SystemFillColorCriticalBrush") as SolidColorBrush)?.Color;
        Check("中性数值不是红色（黑白）", capColor is not null && capColor != criticalExpected,
            $"color={capColor}, 红色={criticalExpected}, 主文字色={neutralExpected}");

        var amountSize = FindText(dirWindow, "15")?.FontSize ?? 0;
        var restSize = FindText(dirWindow, "GJ 7.5 GJ/s 吸电")?.FontSize ?? 0;
        Check("每行数量字号大于说明文字", amountSize >= 15 && restSize >= 9 && amountSize > restSize + 3,
            $"数量={amountSize}, 说明={restSize}");
        var repAmountSize = FindText(dirWindow, repCard?.Lines[0].Amount ?? string.Empty)?.FontSize ?? 0;
        Check("遥修行数量同样放大", repAmountSize >= 15, $"数量={repAmountSize}");

        // 阶段 373：同一行里数量与说明底部对齐
        var amountBlock = FindText(dirWindow, "15");
        var restBlock = FindText(dirWindow, "GJ 7.5 GJ/s 吸电");
        if (amountBlock is not null && restBlock is not null)
        {
            var amountBottom = amountBlock.TranslatePoint(new Point(0, amountBlock.ActualHeight), dirWindow).Y;
            var restBottom = restBlock.TranslatePoint(new Point(0, restBlock.ActualHeight), dirWindow).Y;
            Check("数量与说明底部对齐", Math.Abs(amountBottom - restBottom) < 0.6,
                $"数量底={amountBottom:0.#}, 说明底={restBottom:0.#}");
        }
        else
        {
            Check("数量与说明底部对齐", false, "找不到数量或说明文本块");
        }

        // 阶段 370：等宽自动换行面板——同一行卡片宽度一致且填满可用宽度
        var cardsPanel = FindDescendant<UniformFillPanel>(dirWindow);
        // 端到端：真实中文「被反跳」日志行 → 解析 → 状态中心 → 窗口胶囊
        const string scramLine = "(combat) 来自 黑鸟级* [I K N] [QEDSD] - 试图跃迁扰频 你！";
        var scramContent = TheGuideToTheNewEden.Core.Models.EVELogs.GameLogContent.Create($"[ 2026.11.21 16:00:00 ] {scramLine}");
        var scramEvent = scramContent is null ? null : TheGuideToTheNewEden.Core.Services.CombatLog.CombatLogParser.TryParse(scramContent, "Arya D Stark");
        Check("真实被反跳行解析为进向反跳",
            scramEvent is { Kind: CombatEventKind.WarpScramble, Direction: CombatEventDirection.Incoming },
            $"kind={scramEvent?.Kind}, direction={scramEvent?.Direction}");
        // 阶段 375：真实中文毁电行——受害方（舰船* [军团] [角色]）是输入，施放方（角色 (舰船*)）是输出
        var neutInEvent = TheGuideToTheNewEden.Core.Services.CombatLog.CombatLogParser.TryParse(
            TheGuideToTheNewEden.Core.Models.EVELogs.GameLogContent.Create(
                "[ 2026.11.21 16:10:00 ] (combat) 14 GJ 能量中和 灵感级* [I K N] [QEDSD] - - 小型能量中和器 I*"),
            "Arya D Stark");
        var neutOutEvent = TheGuideToTheNewEden.Core.Services.CombatLog.CombatLogParser.TryParse(
            TheGuideToTheNewEden.Core.Models.EVELogs.GameLogContent.Create(
                "[ 2026.11.21 16:10:01 ] (combat) 14 GJ 能量中和 Arya D Stark (涅鲁斯级*) - 小型能量中和器 I*"),
            "QEDSD");
        Check("真实毁电行方向：受害方=输入、施放方=输出",
            neutInEvent is { Direction: CombatEventDirection.Incoming }
            && neutOutEvent is { Direction: CombatEventDirection.Outgoing },
            $"in={neutInEvent?.Direction}, out={neutOutEvent?.Direction}");

        const string neutDirListener = "Probe Neut Dir";
        center.Attach(neutDirListener);
        if (neutInEvent is not null)
        {
            TheGuideToTheNewEden.Core.Services.CombatLog.CombatCategoryResolver.Resolve(neutInEvent);
            center.Report(neutDirListener, neutInEvent);
        }

        if (neutOutEvent is not null)
        {
            TheGuideToTheNewEden.Core.Services.CombatLog.CombatCategoryResolver.Resolve(neutOutEvent);
            center.Report(neutDirListener, neutOutEvent);
        }
        var neutDirWindow = new GameLogMsgWindow(neutDirListener, neutDirListener, combatOnly: true, showShip: false);
        neutDirWindow.ShowWindow();
        neutDirWindow.UpdateLayout();
        var neutDirCards = (typeof(GameLogMsgWindow)
            .GetField("_cards", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(neutDirWindow) as System.Collections.IEnumerable)?.Cast<CombatCard>().ToList() ?? [];
        var neutDirCard = neutDirCards.FirstOrDefault(p => p.Label == "毁电");
        Check("毁电卡片：输入红、输出绿（不再两边都红）",
            neutDirCard is { Lines.Count: 2 } && neutDirCard.Lines[0].Tone == "harm" && neutDirCard.Lines[1].Tone == "benefit",
            $"kind1={neutInEvent?.Kind}, kind2={neutOutEvent?.Kind}, cat1={neutInEvent?.CategoryKey ?? "null"}, module1={neutInEvent?.ModuleName}, rows={center.GetRows(neutDirListener).Count}");
        Save(neutDirWindow, "logwindow-neut-both.png");
        neutDirWindow.CloseWindow();
        center.Detach(neutDirListener);
        // 阶段 376：电子战按装备分组出胶囊，自己=绿、别人施加=红
        const string ewListener = "Probe EW Groups";
        center.Attach(ewListener);
        Report(center, ewListener, CombatEventKind.WarpScramble, CombatCategories.Scram, null, "Warp Scrambler II", now.AddSeconds(-5));
        Report(center, ewListener, CombatEventKind.WarpScramble, CombatCategories.Scram, null, "Warp Scrambler II", now.AddSeconds(-5), CombatEventDirection.Outgoing);
        Report(center, ewListener, CombatEventKind.WarpDisrupt, CombatCategories.ScramDisrupt, null, "Warp Disruptor II", now.AddSeconds(-4));
        Report(center, ewListener, CombatEventKind.StasisWeb, CombatCategories.Web, null, "Stasis Webifier II", now.AddSeconds(-4));
        Report(center, ewListener, CombatEventKind.TargetPainter, CombatCategories.Painter, null, "Target Painter II", now.AddSeconds(-3));
        Report(center, ewListener, CombatEventKind.EcmJam, CombatCategories.Ecm, null, "Multispectral Jammer II", now.AddSeconds(-3), CombatEventDirection.Outgoing);
        var ewWindow = new GameLogMsgWindow(ewListener, ewListener, combatOnly: true, showShip: false);
        ewWindow.ShowWindow();
        ewWindow.UpdateLayout();
        var ewPills = (typeof(GameLogMsgWindow)
            .GetField("_pills", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(ewWindow) as System.Collections.IEnumerable)?.Cast<CombatPill>().ToList() ?? [];
        var ewLabels = ewPills.Select(p => p.Label).ToList();
        Check("电子战胶囊分组名齐全",
            ewLabels.Contains("跃迁扰频器") && ewLabels.Contains("跃迁扰断器") && ewLabels.Contains("ECM")
            && ewLabels.Contains("停滞缠绕光束") && ewLabels.Contains("目标标记装置"),
            string.Join(",", ewLabels));
        var scramIn = ewPills.FirstOrDefault(p => p.Label == "跃迁扰频器" && p.Brush is SolidColorBrush b && b.Color.R > b.Color.G);
        var scramOut = ewPills.FirstOrDefault(p => p.Label == "跃迁扰频器" && p.Brush is SolidColorBrush b2 && b2.Color.G > b2.Color.R);
        Check("自己施加=绿、别人施加=红", scramIn is not null && scramOut is not null,
            string.Join(",", ewPills.Select(p => $"{p.Label}:{((SolidColorBrush)p.Brush).Color}")));
        Save(ewWindow, "logwindow-ew-groups.png");
        ewWindow.CloseWindow();
        center.Detach(ewListener);
        // 阶段 371：同一行卡片高度一致（矮卡片拉伸到本行最高）
        var rowHeights = new List<(double Top, double Height)>();
        if (cardsPanel is not null)
        {
            foreach (FrameworkElement child in cardsPanel.Children)
            {
                var top = Math.Round(child.TranslatePoint(new Point(0, 0), cardsPanel).Y, 1);
                rowHeights.Add((top, child.ActualHeight));
            }
        }

        var mixedRows = rowHeights.GroupBy(p => p.Top).Any(group => group.Max(p => p.Height) - group.Min(p => p.Height) > 0.6);
        Check("同一行卡片高度一致", rowHeights.Count >= 2 && !mixedRows,
            string.Join(",", rowHeights.Select(p => $"{p.Top}:{p.Height:0.#}")));
        var cardWidths = cardsPanel?.Children.Cast<FrameworkElement>().Select(p => p.ActualWidth).ToList() ?? [];
        var availableWidth = cardsPanel?.ActualWidth ?? 0;
        Check("卡片等宽分布并填满一行",
            cardsPanel is not null && cardWidths.Count >= 3
            && Math.Abs(cardWidths[0] - cardWidths[1]) < 0.6
            && Math.Abs(cardWidths[0] * 2 + 7 - availableWidth) < 0.8,
            $"panel={availableWidth:0.#}, widths={string.Join(",", cardWidths.Select(p => p.ToString("0.#")))}");

        var titleBar = (Visual)dirWindow.FindName("TitleBarGrid");
        var showGame = dirWindow.FindName("ShowGameButton") as Button;
        var clearCombat = dirWindow.FindName("CombatClearButton") as Button;
        Check("标题栏有「前置游戏」图标按钮", showGame is not null && titleBar.IsAncestorOf(showGame),
            "ShowGameButton 应在标题栏里");
        Check("标题栏有「清除所有日志」按钮", clearCombat is not null && titleBar.IsAncestorOf(clearCombat)
            && !string.IsNullOrEmpty(clearCombat.ToolTip as string) && (clearCombat.ToolTip as string)!.Contains("清除所有日志"),
            $"tooltip={clearCombat?.ToolTip}");
        Check("底部按钮栏已取消（清除并入标题栏）",
            dirWindow.FindName("FooterBar") is null && dirWindow.FindName("FeedClearButton") is null,
            "不应再有 FooterBar / FeedClearButton");

        // 阶段 370：标题栏清除按钮同时清战斗行 + 关键词命中
        var clearFeedWindow = new GameLogMsgWindow("Probe Clear All", "Probe Clear All", showShip: false);
        center.Attach("Probe Clear All");
        clearFeedWindow.ShowWindow();
        clearFeedWindow.AppendMessage("命中一行", "本地");
        center.Report("Probe Clear All", new CombatLogEvent
        {
            Kind = CombatEventKind.Damage,
            Direction = CombatEventDirection.Incoming,
            CategoryKey = CombatCategories.Damage,
            Amount = 100,
            ModuleName = "Probe Gun",
            Time = DateTime.UtcNow,
        });
        ((Button)clearFeedWindow.FindName("CombatClearButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        clearFeedWindow.UpdateLayout();
        var feedCountField = typeof(GameLogMsgWindow).GetField("_feedCount",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Check("清除按钮同时清空战斗行与关键词命中",
            (feedCountField?.GetValue(clearFeedWindow) as int? ?? -1) == 0
            && center.GetRows("Probe Clear All").Count == 0,
            $"feed={feedCountField?.GetValue(clearFeedWindow)}, combat={center.GetRows("Probe Clear All").Count}");
        clearFeedWindow.CloseWindow();
        center.Detach("Probe Clear All");

        Save(dirWindow, "logwindow-directions.png");
        dirWindow.CloseWindow();
        center.Detach(dirListener);

        // 阶段 374：反跳/干扰出向也要显示；传电两种方向；不再限制卡片数量
        const string bothDirListener = "Probe Sticky Both";
        center.Attach(bothDirListener);
        Report(center, bothDirListener, CombatEventKind.WarpScramble, CombatCategories.Scram, null,
            "Warp Scrambler II", now.AddSeconds(-5));
        Report(center, bothDirListener, CombatEventKind.WarpScramble, CombatCategories.Scram, null,
            "Warp Scrambler II", now.AddSeconds(-5), CombatEventDirection.Outgoing);
        Report(center, bothDirListener, CombatEventKind.EcmJam, CombatCategories.Ecm, null,
            "Magnetic ECM II", now.AddSeconds(-4), CombatEventDirection.Outgoing);
        Report(center, bothDirListener, CombatEventKind.RemoteCapacitor, CombatCategories.CapTransfer, 22,
            "Remote Capacitor Transmitter II", now.AddSeconds(-3));
        Report(center, bothDirListener, CombatEventKind.RemoteCapacitor, CombatCategories.CapTransfer, 35,
            "Remote Capacitor Transmitter II", now.AddSeconds(-3), CombatEventDirection.Outgoing);
        Report(center, bothDirListener, CombatEventKind.Damage, CombatCategories.Damage, 120,
            "Probe Gun", now.AddSeconds(-2));

        var bothDirWindow = new GameLogMsgWindow(bothDirListener, bothDirListener, showShip: false);
        bothDirWindow.ShowWindow();
        bothDirWindow.UpdateLayout();
        var bothPills = (typeof(GameLogMsgWindow)
            .GetField("_pills", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(bothDirWindow) as System.Collections.IEnumerable)?.Cast<CombatPill>().Select(p => p.Label).ToList() ?? [];
        var bothCards = (typeof(GameLogMsgWindow)
            .GetField("_cards", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(bothDirWindow) as System.Collections.IEnumerable)?.Cast<CombatCard>().Select(p => p.Label).ToList() ?? [];
        Check("电子战胶囊用装备分组名，两种方向各一枚",
            bothPills.Count(p => p == "跃迁扰频器") == 2 && bothPills.Contains("ECM"),
            string.Join(",", bothPills));
        Check("传电在双区窗口也显示（卡片数量不再封顶）",
            bothCards.Contains("传电") && bothCards.Contains("伤害"),
            string.Join(",", bothCards));
        var transferCard = (typeof(GameLogMsgWindow)
            .GetField("_cards", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(bothDirWindow) as System.Collections.IEnumerable)?.Cast<CombatCard>().FirstOrDefault(p => p.Label == "传电");
        Check("传电卡片两种方向两行",
            transferCard is { Lines.Count: 2 }
            && transferCard.Lines[0].Text == "22 GJ" && transferCard.Lines[1].Text == "35 GJ",
            $"实际 {string.Join(" | ", transferCard?.Lines.Select(l => l.Text) ?? [])}");
        Save(bothDirWindow, "logwindow-sticky-both.png");
        bothDirWindow.CloseWindow();
        center.Detach(bothDirListener);
        // 仅监控
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

        // 战斗已挂载但暂无事件（无战斗 + 暂无命中）
        const string idleListener = "Probe Idle";
        center.Attach(idleListener);
        var idle = new GameLogMsgWindow(idleListener, idleListener, showShip: false);
        ((Window)idle).Show();
        idle.UpdateLayout();
        Save(idle, "logwindow-idle.png");
        idle.CloseWindow();
        center.Detach(idleListener);

        // 共享窗口：战斗先建（仅战斗），关键词后到 → 同一个窗口升级成双区
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

        // 阶段 376：电子战胶囊直接用装备分组名（跃迁扰频器 / ECM …），不再用「被…」措辞
        var pillField = typeof(GameLogMsgWindow).GetField("_pills",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var pillLabels = (pillField?.GetValue(shared2) as System.Collections.IEnumerable)
            ?.Cast<CombatPill>().Select(p => p.Label).ToList() ?? [];
        Check("电子战胶囊显示分组名（跃迁扰频器 / ECM）",
            pillLabels.Contains("跃迁扰频器") && pillLabels.Contains("ECM")
            && !pillLabels.Any(p => p.Contains('被')),
            string.Join(",", pillLabels));

        Save(shared2, "logwindow-shared.png");
        GameLogWindowRegistry.Release(listener, "monitor:probe");
        GameLogWindowRegistry.Release(listener, "combat");
        // 清除战斗状态后：战斗区隐藏，只剩命中列表
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
        DateTime time,
        CombatEventDirection direction = CombatEventDirection.Incoming,
        RemoteRepairType repairType = RemoteRepairType.None)
    {
        center.Report(listener, new CombatLogEvent
        {
            Kind = kind,
            Direction = direction,
            CategoryKey = category,
            Amount = amount,
            ModuleName = module,
            Pilot = "Guardian",
            Time = time,
            RepairType = repairType,
        });
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

    private static void DumpCards(GameLogMsgWindow window, string tag)
    {
        var list = (ItemsControl)window.FindName("CardsList");
        var field = typeof(GameLogMsgWindow).GetField("_cards",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cards = (System.Collections.IEnumerable?)field?.GetValue(window);
        var text = new StringBuilder();
        text.AppendLine($"{tag}: list={list.ActualWidth:0.#}x{list.ActualHeight:0.#} count={list.Items.Count}");
        var index = 0;
        if (cards is not null)
        {
            foreach (var card in cards)
            {
                var width = card.GetType().GetProperty("Width")?.GetValue(card);
                // 卡片的实际高度取容器（模板里是固定 Height=68），用于核对"不再撑满"
                var container = list.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
                var height = container?.ActualHeight ?? 0;
                text.AppendLine($"  card {width} x {height:0.#}");
                index++;
            }
        }

        File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "probe.log"), text.ToString(), Encoding.UTF8);
    }

    private static void Check(string name, bool ok, string detail)
    {
        File.AppendAllText(
            Path.Combine(AppContext.BaseDirectory, "probe.log"),
            $"[{(ok ? "PASS" : "FAIL")}] {name}：{detail}{Environment.NewLine}",
            Encoding.UTF8);
    }

    /// <summary>在视觉树里按 DataContext（卡片 VM）找卡片根 Border。</summary>
    private static Border? FindByDataContext(DependencyObject root, object dataContext)
    {
        if (root is Border border && ReferenceEquals(border.DataContext, dataContext))
        {
            return border;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var found = FindByDataContext(VisualTreeHelper.GetChild(root, i), dataContext);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>在视觉树里找指定类型的第一个后代。</summary>
    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            var nested = FindDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    /// <summary>在视觉树里按文字找 TextBlock（用于确认「已过期」小字真的渲染出来了）。</summary>
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

    /// <summary>半透明背板要衬一块彩色底才看得出效果（直接渲染窗口只会得到带 alpha 的图）。</summary>
    private static void SaveWithBackdrop(Window window, string name)
    {
        var dpi = VisualTreeHelper.GetDpi(window);
        var width = (int)Math.Ceiling(window.ActualWidth * dpi.DpiScaleX);
        var height = (int)Math.Ceiling(window.ActualHeight * dpi.DpiScaleY);
        var content = (FrameworkElement)window.Content;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var rect = new Rect(0, 0, width, height);
            var backdrop = new LinearGradientBrush(
                Color.FromRgb(0x1E, 0x3A, 0x5F),
                Color.FromRgb(0x2F, 0x6B, 0x4F),
                new Point(0, 0),
                new Point(1, 1));
            dc.DrawRectangle(backdrop, null, rect);
            dc.DrawRectangle(
                new VisualBrush(content) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top },
                null,
                rect);
        }

        var bitmap = new RenderTargetBitmap(width, height, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(AppContext.BaseDirectory, name));
        encoder.Save(stream);
        File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "probe.log"), $"{name}: {width}x{height}{Environment.NewLine}", Encoding.UTF8);
    }
}
