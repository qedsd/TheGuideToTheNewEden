using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TheGuideToTheNewEden.Core.Models.CombatLog;
using TheGuideToTheNewEden.Core.Services.CombatLog;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.CombatLog;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 日志监控小窗（阶段 359 重设计；阶段 360 修正显示与高度；阶段 361 可缩放 + 背板透明度；
/// 阶段 363/364/365 区分输入输出、按钮收进标题栏、两行同字号）：
/// <list type="bullet">
///   <item>上半部战斗信息：二值状态（反跳 / 干扰）用胶囊，数值（毁电 / 遥修 / 吸电 / 传电 / 承伤）用卡片；</item>
///   <item>下半部关键词命中：单行消息流，最新一条高亮；</item>
///   <item>战斗区高度固定（卡片 68 DIP、区域封顶），不再按剩余空间撑满；</item>
///   <item>每张卡都明确标「输入 / 输出」：输入红色、输出绿色；两行同字号、各占一半高度，只有一行时占满并居中；</item>
///   <item>卡片**不会自动消失**：超过「过期时间」没有新日志后整张卡**置灰**（卡面不写「已过期」），悬停提示改为「已过期」；只有手动清除 / 停止监控才清；</item>
///   <item>开启战斗日志后窗口就显示（没有战斗时显示「无战斗」），不会因为没有事件自动隐藏；</item>
///   <item>窗口可自由缩放、没有最小尺寸；<b>背板可调不透明度</b>（参考频道预警小窗：只让背板透明，文字/卡片始终实色）；</item>
///   <item>标题栏右侧：清除战斗状态 / 前置游戏 / 最小化 / 关闭；标题可带当前舰船类型（ESI 读取）。</item>
/// </list>
/// </summary>
public partial class GameLogMsgWindow : Window
{
    private bool _combatOnly;
    private bool _showShip;
    private string _baseTitle;
    private string? _shipName;
    private Func<string>? _titleProvider;
    private readonly ObservableCollection<CombatPill> _pills = new();
    private readonly ObservableCollection<CombatCard> _cards = new();
    private readonly ObservableCollection<FeedRow> _feed = new();
    private bool _closing;
    private bool _geometryLoaded;
    private bool _geometrySaveQueued;
    private bool _restoringGeometry;
    private int? _settingListenerId;
    private int _feedCount;

    /// <summary>背板不透明度（1–100，100 = 完全不透明）；只作用于背板，文字与卡片不受影响。</summary>
    private int _windowOpacity = 100;

    /// <summary>窗口被隐藏（用户关闭/手动隐藏）：通知服务停声音、清系统通知。</summary>
    public event EventHandler? OnHided;

    /// <summary>点击"前置游戏"按钮。</summary>
    public event EventHandler? OnShowGameButtonClick;

    public string ListenerName { get; }

    private bool _allowClose;

    public GameLogMsgWindow(
        string listenerName,
        string? displayTitle = null,
        bool combatOnly = false,
        bool showShip = false,
        Func<string>? titleProvider = null,
        int windowOpacity = 100,
        int? settingListenerId = null)
    {
        ListenerName = listenerName;
        _combatOnly = combatOnly;
        _showShip = showShip;
        _baseTitle = displayTitle ?? listenerName;
        _titleProvider = titleProvider;
        _windowOpacity = ClampOpacity(windowOpacity);
        _settingListenerId = settingListenerId;

        InitializeComponent();
        ApplyBackdrop();
        ThemeService.ThemeChanged += ApplyBackdrop;
        UpdateTitle();

        PillsList.ItemsSource = _pills;
        CardsList.ItemsSource = _cards;
        FeedList.ItemsSource = _feed;

        CombatLogStateCenter.Current.Changed += OnCombatChanged;
        LanguageService.LanguageChanged += OnLanguageChanged;
        RefreshCombat();
        UpdateFeedState();

        if (_showShip)
        {
            _ = LoadShipAsync();
        }
    }

    /// <summary>把"仅战斗"窗口升级成"战斗 + 监控"双区（关键词日志复用同一窗口时调用）。</summary>
    public void EnableMonitor()
    {
        if (!_combatOnly)
        {
            return;
        }

        _combatOnly = false;
        RefreshCombat();
        UpdateFeedState();
    }

    /// <summary>关键词日志停止、但战斗日志还在跑时，收掉监控区，恢复成仅战斗窗口。</summary>
    public void DisableMonitor()
    {
        if (_combatOnly)
        {
            return;
        }

        _combatOnly = true;
        RefreshCombat();
        UpdateFeedState();
    }

    /// <summary>复用窗口时更新标题（关键词配置名可能和战斗日志窗口不同）。</summary>
    public void SetTitle(string title)
    {
        _titleProvider = null;
        _baseTitle = title;
        UpdateTitle();
    }

    /// <summary>复用窗口时传入标题生成器，切换语言后标题跟着变。</summary>
    public void SetTitleProvider(Func<string> titleProvider)
    {
        _titleProvider = titleProvider;
        UpdateTitle();
    }

    /// <summary>切换标题里的舰船显示（不重建窗口）。</summary>
    public void SetShowShip(bool show)
    {
        if (_showShip == show)
        {
            return;
        }

        _showShip = show;
        if (show)
        {
            _ = LoadShipAsync();
        }
        else
        {
            _shipName = null;
            UpdateTitle();
        }
    }

    /// <summary>
    /// 设置背板不透明度（阶段 361，参考频道预警小窗）：只改背板画刷的 alpha，文字与卡片始终实色。
    /// </summary>
    public void SetWindowOpacity(int percent)
    {
        var value = ClampOpacity(percent);
        if (_windowOpacity == value)
        {
            return;
        }

        _windowOpacity = value;
        ApplyBackdrop();
    }

    private static int ClampOpacity(int percent) => Math.Clamp(percent, 1, 100);    /// <summary>首次加载时恢复该角色上次保存的位置和尺寸。</summary>
    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (_geometryLoaded) return;
        _geometryLoaded = true;
        var id = FindListenerId();
        var setting = id.HasValue ? GameLogInfoSettingService.GetValue(id.Value) : null;
        if (setting is null) return;

        _restoringGeometry = true;
        try
        {
            if (setting.WindowWidth is > 0)
                Width = Math.Clamp(setting.WindowWidth.Value, 300, SystemParameters.VirtualScreenWidth);
            if (setting.WindowHeight is > 0)
                Height = Math.Clamp(setting.WindowHeight.Value, 180, SystemParameters.VirtualScreenHeight);
            if (setting.WindowLeft.HasValue && setting.WindowTop.HasValue)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = setting.WindowLeft.Value;
                Top = setting.WindowTop.Value;
                ClampToVirtualScreen();
            }
        }
        finally { _restoringGeometry = false; }
    }

    private int? FindListenerId()
    {
        if (_settingListenerId.HasValue) return _settingListenerId.Value;
        try
        {
            return Core.Helpers.GameLogHelper.GetLatestGameLogInfos(GameLogsSettingService.GetGamelogsPath())
                ?.FirstOrDefault(p => string.Equals(p.ListenerName, ListenerName, StringComparison.OrdinalIgnoreCase))
                ?.ListenerID;
        }
        catch { return null; }
    }

    private void ClampToVirtualScreen()
    {
        var left = SystemParameters.VirtualScreenLeft;
        var top = SystemParameters.VirtualScreenTop;
        var right = left + SystemParameters.VirtualScreenWidth;
        var bottom = top + SystemParameters.VirtualScreenHeight;
        Left = Math.Clamp(Left, left, Math.Max(left, right - Math.Min(Width, right - left)));
        Top = Math.Clamp(Top, top, Math.Max(top, bottom - Math.Min(Height, bottom - top)));
    }

    private void OnWindowGeometryChanged(object sender, EventArgs e)
    {
        QueueGeometrySave();
        UpdateCardsViewport(!_combatOnly);
    }

    private void OnWindowGeometryStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Normal) QueueGeometrySave();
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e) => SaveWindowGeometry();

    private void QueueGeometrySave()
    {
        if (!_geometryLoaded || _restoringGeometry || _closing || WindowState != WindowState.Normal || _geometrySaveQueued) return;
        _geometrySaveQueued = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _geometrySaveQueued = false;
            SaveWindowGeometry();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void SaveWindowGeometry()
    {
        if (!_geometryLoaded || _restoringGeometry || WindowState != WindowState.Normal || !IsLoaded) return;
        var id = FindListenerId();
        if (id.HasValue && Width > 0 && Height > 0 && double.IsFinite(Left) && double.IsFinite(Top))
            GameLogInfoSettingService.SetWindowBounds(id.Value, Left, Top, Width, Height);
    }

    /// <summary>按不透明度生成背板画刷：颜色取主题背景色（主题切换后重算），alpha 由设置决定。</summary>
    private void ApplyBackdrop()
    {
        if (_closing)
        {
            return;
        }

        var color = (Application.Current?.TryFindResource("ApplicationBackgroundBrush") as SolidColorBrush)?.Color
            ?? (Application.Current?.TryFindResource("CardBackgroundFillColorDefaultBrush") as SolidColorBrush)?.Color
            ?? Colors.White;
        var alpha = (byte)(ClampOpacity(_windowOpacity) * 255 / 100);
        BackdropPlate.Background = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
    }

    private void UpdateTitle()
    {
        var title = _titleProvider?.Invoke();
        if (!string.IsNullOrWhiteSpace(title))
        {
            _baseTitle = title;
        }

        var text = string.IsNullOrWhiteSpace(_shipName) ? _baseTitle : $"{_baseTitle} · {_shipName}";
        Title = text;
        TitleText.Text = text;
    }

    private async Task LoadShipAsync()
    {
        try
        {
            var ship = await CharacterShipNameService.GetAsync(ListenerName);
            if (!_closing && !string.IsNullOrWhiteSpace(ship))
            {
                _shipName = ship;
                UpdateTitle();
            }
        }
        catch
        {
            // 取不到舰船就只显示角色名
        }
    }

    /// <summary>关闭即隐藏（窗口要复用，直接关掉会导致下次 Show() 抛异常）。</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideWindow();
        }

        base.OnClosing(e);
    }

    /// <summary>真正销毁窗口（通知服务 Remove 时调用）。</summary>
    public void CloseWindow()
    {
        _allowClose = true;
        CombatLogStateCenter.Current.Changed -= OnCombatChanged;
        LanguageService.LanguageChanged -= OnLanguageChanged;
        ThemeService.ThemeChanged -= ApplyBackdrop;
        _closing = true;
        Close();
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;

    /// <summary>显示窗口并追加一条消息。</summary>
    public void Show(string content, string? channel = null)
    {
        AppendMessage(content, channel);
        if (!IsVisible)
        {
            Show();
        }
    }

    /// <summary>只显示窗口，不追加消息（战斗日志小窗用；没有战斗行时显示「无战斗」占位，不隐藏窗口）。</summary>
    public void ShowWindow()
    {
        if (!IsVisible)
        {
            Show();
        }
    }

    public void AppendMessage(string content, string? channel = null)
    {
        if (_combatOnly)
        {
            return;
        }

        foreach (var row in _feed)
        {
            row.IsNew = false;
        }

        _feed.Insert(0, new FeedRow
        {
            Time = DateTime.Now.ToString("HH:mm"),
            Channel = channel ?? string.Empty,
            Text = content,
            IsNew = true,
        });

        while (_feed.Count > 200)
        {
            _feed.RemoveAt(_feed.Count - 1);
        }

        UpdateFeedState();
        FeedScroll.ScrollToTop();
    }

    /// <summary>清空命中列表（标题栏的清除按钮会连同战斗行一起清）。</summary>
    public void Clear()
    {
        _feed.Clear();
        UpdateFeedState();
    }

    /// <summary>隐藏窗口（不关闭，等下一条消息再显示），并通知停声音、清系统通知。</summary>
    public void HideWindow()
    {
        if (IsVisible)
        {
            Hide();
            OnHided?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// 标题栏的「前置游戏」图标（阶段 364）：没挂外部处理器时（战斗日志专属窗口）
    /// 直接把对应角色名的游戏窗口切到前台；监控会话自己挂了处理器（还要停声音/系统通知），交给它。
    /// </summary>
    private void OnShowGameClick(object sender, RoutedEventArgs e)
    {
        if (OnShowGameButtonClick is { } handler)
        {
            handler.Invoke(this, EventArgs.Empty);
            return;
        }

        try
        {
            GameWindowHelper.BringGameToFront(ListenerName);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    // ---------- 标题栏（阶段 361：改成自绘窗口后需要自己处理拖动 / 最小化 / 隐藏） ----------

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch
        {
            // 鼠标已经抬起 / 系统取消拖动时会抛，忽略即可
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void UpdateFeedState()
    {
        _feedCount = _feed.Count;
        FeedEmpty.Visibility = _feedCount == 0 ? Visibility.Visible : Visibility.Collapsed;

        // 阶段 370：底部那条按钮栏取消，清除按钮统一在标题栏；这里同步它的可见性。
        UpdateClearButtonVisibility(
            _combatOnly || CombatLogStateCenter.Current.IsAttached(ListenerName),
            _cards.Count > 0);
    }

    // ---------- 战斗信息 ----------

    private void OnLanguageChanged(object? sender, string language)
    {
        if (_closing)
        {
            return;
        }

        UpdateTitle();
        RefreshCombat();
        UpdateFeedState();
    }

    private void OnCombatChanged(string listener)
    {
        if (string.Equals(listener, ListenerName, StringComparison.OrdinalIgnoreCase))
        {
            RefreshCombat();
        }
    }

    private void RefreshCombat()
    {
        if (_closing)
        {
            return;
        }

        var attached = _combatOnly || CombatLogStateCenter.Current.IsAttached(ListenerName);
        var rows = attached ? CombatLogStateCenter.Current.GetRows(ListenerName) : Array.Empty<CombatLogRow>();

        _pills.Clear();
        _cards.Clear();
        foreach (var row in rows)
        {
            var definition = CombatCategories.Find(row.Category);
            if (definition is null)
            {
                continue;
            }

            if (!definition.HasValue)
            {
                // 阶段 376：电子战胶囊直接用**装备分组名**（跃迁扰频器 / 跃迁扰断器 / ECM / 停滞缠绕光束 /
                // 目标标记装置 / 武器扰断器 / 远程感应抑阻器）；自己的装备绿色，被别人施加红色。
                var harm = GetResourceBrush("SystemFillColorCriticalBrush", Brushes.IndianRed);
                var good = GetResourceBrush("SystemFillColorSuccessBrush", Brushes.SeaGreen);
                foreach (var line in row.Lines)
                {
                    var outgoing = line.Direction == CombatEventDirection.Outgoing;
                    var brush = outgoing ? good : harm;
                    _pills.Add(new CombatPill
                    {
                        Label = row.Label,
                        Brush = brush,
                        Background = TintOf(brush),
                    });
                }
            }
        }

        // 阶段 374：不再限制卡片数量（布局会换行 + 滚动），所有启用的分类都显示。
        var valueRows = rows
            .Where(p => CombatCategories.Find(p.Category) is { HasValue: true })
            .ToList();

        // 阶段 375：吸电 + 传电合并成一张「电容」卡（每行末尾标 吸电 / 传电），与遥修同一呈现方式。
        var mergedAway = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in valueRows)
        {
            if (mergedAway.Contains(row.Category))
            {
                continue;
            }

            // 吸电行也要带类型名（合并卡里要能分清 吸电 / 传电）。
            var lines = BuildCardLines(row, string.Equals(row.Category, CombatCategories.Nos, StringComparison.OrdinalIgnoreCase)
                ? LanguageService.Get("CombatCategory_Nos")
                : null);
            if (lines.Count == 0)
            {
                continue;
            }

            var label = row.Label;
            if (string.Equals(row.Category, CombatCategories.Nos, StringComparison.OrdinalIgnoreCase))
            {
                var transferRow = valueRows.FirstOrDefault(p => string.Equals(p.Category, CombatCategories.CapTransfer, StringComparison.OrdinalIgnoreCase));
                var transferLines = transferRow is null ? [] : BuildCardLines(transferRow, LanguageService.Get("CombatCategory_CapTransfer"));
                if (transferLines.Count > 0)
                {
                    label = LanguageService.Get("CombatLog_CapGroup");
                    lines = [.. lines, .. transferLines];
                    mergedAway.Add(CombatCategories.CapTransfer);
                }
            }

            _cards.Add(new CombatCard
            {
                Label = label,
                Detail = row.Detail,
                Lines = lines,
                LineFontSize = lines[0].FontSize,
                Height = CardHeight(lines),
                Brush = BrushOf(ColorForCategory(row.Category)),
                Expired = row.Expired,
            });
        }

        PillsList.Visibility = _pills.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        CardsList.Visibility = _cards.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateClearButtonVisibility(attached, rows.Count > 0);

        // 仅战斗窗口：即使没有战斗也占住固定高度并显示「无战斗」（阶段 360：开启后不隐藏）。
        // 双区窗口：没有战斗行时把战斗区让给命中列表，窗口本身仍然可见。
        var showCombat = attached && (_combatOnly || rows.Count > 0);
        CombatEmpty.Visibility = showCombat && rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        ApplyLayout(showCombat);

        // 阶段 360：不再因为"没有战斗行"自动隐藏窗口。
        // 只保留反方向：窗口被用户手动隐藏/最小化时，有新战斗行再叫回来（告警本意）。
        if (_combatOnly && rows.Count > 0 && !IsVisible && WindowState != WindowState.Minimized)
        {
            ShowWindow();
        }
    }

    /// <summary>把一个分类的数值行转成卡片行（阶段 372：每行数量大字、说明小字；可选类型后缀）。</summary>
    private static List<CombatCardLine> BuildCardLines(CombatLogRow row, string? typeSuffix)
    {
        var visible = row.Lines.Where(p => !p.Expired).ToList();
        if (visible.Count == 0)
        {
            visible = row.Lines.ToList();
        }

        var amountFontSize = AmountFontSize(visible.Count);
        var restFontSize = RestFontSize();
        return visible
            .Select(p => new CombatCardLine
            {
                Amount = p.AmountText,
                Rest = string.IsNullOrWhiteSpace(typeSuffix) ? p.RestText : $"{p.RestText} {typeSuffix}".Trim(),
                Tone = ToneOf(row.Category, p.Direction == CombatEventDirection.Outgoing),
                FontSize = amountFontSize,
                RestFontSize = restFontSize,
            })
            .ToList();
    }

    /// <summary>每行「数量」的字号：行数多时略降（16 / 15）。</summary>
    private static double AmountFontSize(int lineCount) => lineCount <= 3 ? 16 : 15;

    /// <summary>每行「单位 / 每秒 / 类型」的小字号。</summary>
    private static double RestFontSize() => 10;

    /// <summary>卡片高度：上下内边距 12 + 标签 16 + 各行（数量字号 + 6）之和。</summary>
    private static double CardHeight(IReadOnlyList<CombatCardLine> lines)
        => 12 + 16 + lines.Sum(p => p.FontSize + 6);

    private void ApplyLayout(bool showCombat)
    {
        var monitor = !_combatOnly;
        CombatArea.Visibility = showCombat ? Visibility.Visible : Visibility.Collapsed;
        MonitorArea.Visibility = monitor ? Visibility.Visible : Visibility.Collapsed;

        // 战斗区高度自适应：卡片由等宽换行面板排布，行数多时区域自己变高（窗口够高就能全部显示）。
        CombatRow.Height = showCombat ? GridLength.Auto : new GridLength(0);
        MonitorRow.Height = monitor ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        UpdateCardsViewport(monitor);
    }

    /// <summary>
    /// 卡片区的可视高度跟随窗口（不再写死 320/260）：窗口越高能看到的卡片越多；
    /// 双区窗口给关键词列表留一点空间，所以卡片区最多占 70%。
    /// </summary>
    private void UpdateCardsViewport(bool hasMonitor)
    {
        var available = ActualHeight - 28 - 16;
        if (available <= 0)
        {
            return;
        }

        if (hasMonitor)
        {
            available *= 0.7;
        }

        CardsScroll.MaxHeight = Math.Max(80, available);
    }

    /// <summary>
    /// 标题栏的清除按钮：一次清掉**战斗行 + 关键词命中**（阶段 370 合并，按钮文案为「清除所有日志」）。
    /// 反跳 / ECM 这类一次性状态没有"结束"日志，也只能靠它收掉。
    /// </summary>
    private void OnCombatClearClick(object sender, RoutedEventArgs e)
    {
        CombatLogStateCenter.Current.Clear(ListenerName);
        Clear();
    }

    /// <summary>清除按钮只在"有战斗行或有关键词命中"时出现。</summary>
    private void UpdateClearButtonVisibility(bool attached, bool hasCombatRows)
    {
        var hasFeed = _feed.Count > 0;
        var hasCombat = attached && hasCombatRows;
        CombatClearButton.Visibility = hasCombat || hasFeed ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------- 最小化 ----------

    protected override void OnStateChanged(EventArgs e)
    {
        // 工具窗口 ShowInTaskbar=False 时最小化会丢到屏幕外且没有恢复入口：
        // 最小化期间临时显示任务栏按钮，恢复后再隐藏（与 IntelWindow 一致）。
        ShowInTaskbar = WindowState == WindowState.Minimized;
        base.OnStateChanged(e);
    }

    /// <summary>
    /// 阶段 366：数值颜色按"对启用方是收益还是伤害"决定，而不是按输入 / 输出。
    /// <list type="bullet">
    ///   <item>伤害 / 毁电 / 吸电：输出 = 收益（绿），输入 = 伤害（红）；</item>
    ///   <item>遥修 / 传电：输入 = 收益（绿），输出 = 把资源给出去，既不算伤害也不算收益（中性）；</item>
    ///   <item>反跳 / 干扰（只有输入）：伤害（红）。</item>
    /// </list>
    /// </summary>
    private static string ToneOf(string category, bool outgoing) => category switch
    {
        CombatCategories.Damage => outgoing ? "benefit" : "harm",
        CombatCategories.Neut => outgoing ? "benefit" : "harm",
        CombatCategories.Nos => outgoing ? "benefit" : "harm",
        CombatCategories.Rep => outgoing ? "neutral" : "benefit",
        CombatCategories.CapTransfer => outgoing ? "neutral" : "benefit",
        _ => outgoing ? "benefit" : "harm",
    };

    private static string CategoryLabelKey(string category) => category switch
    {
        CombatCategories.Scram => "CombatCategory_Scram",
        CombatCategories.ScramDisrupt => "CombatCategory_ScramDisrupt",
        CombatCategories.Neut => "CombatCategory_Neut",
        CombatCategories.Nos => "CombatCategory_Nos",
        CombatCategories.Ecm => "CombatCategory_Ecm",
        CombatCategories.Web => "CombatCategory_Web",
        CombatCategories.Painter => "CombatCategory_Painter",
        CombatCategories.WeaponDisrupt => "CombatCategory_WeaponDisrupt",
        CombatCategories.SensorDamp => "CombatCategory_SensorDamp",
        CombatCategories.Rep => "CombatCategory_Rep",
        CombatCategories.CapTransfer => "CombatCategory_CapTransfer",
        CombatCategories.Damage => "CombatCategory_Damage",
        _ => category,
    };

    private static Color ColorForCategory(string category) => category switch
    {
        CombatCategories.Scram => Color.FromRgb(0xE5, 0x48, 0x4D),
        CombatCategories.ScramDisrupt => Color.FromRgb(0xE5, 0x48, 0x4D),
        CombatCategories.Neut => Color.FromRgb(0xF5, 0xA5, 0x24),
        CombatCategories.Nos => Color.FromRgb(0x3B, 0x82, 0xF6),
        CombatCategories.Ecm => Color.FromRgb(0xA8, 0x55, 0xF7),
        CombatCategories.Web => Color.FromRgb(0xA8, 0x55, 0xF7),
        CombatCategories.Painter => Color.FromRgb(0xA8, 0x55, 0xF7),
        CombatCategories.WeaponDisrupt => Color.FromRgb(0xA8, 0x55, 0xF7),
        CombatCategories.SensorDamp => Color.FromRgb(0xA8, 0x55, 0xF7),
        CombatCategories.Rep => Color.FromRgb(0x30, 0xA4, 0x6C),
        CombatCategories.CapTransfer => Color.FromRgb(0x06, 0xB6, 0xD4),
        CombatCategories.Damage => Color.FromRgb(0xF9, 0x73, 0x16),
        _ => Color.FromRgb(0x6B, 0x72, 0x80),
    };

    private static SolidColorBrush BrushOf(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush TintOf(Brush brush)
    {
        var color = brush is SolidColorBrush solid ? solid.Color : Colors.Gray;
        var tint = new SolidColorBrush(Color.FromArgb(0x1F, color.R, color.G, color.B));
        tint.Freeze();
        return tint;
    }

    private static string FormatAmount(double? value)
        => value is { } number ? number.ToString("0") : "—";

    /// <summary>速率保留一位小数（与状态中心/叠加层的文本口径一致）。</summary>
    private static string FormatRate(double value)
        => value >= 100 ? value.ToString("0") : value.ToString("0.#");

    private static Brush GetResourceBrush(string key, Brush fallback)
        => Application.Current?.TryFindResource(key) as Brush ?? fallback;
}

public sealed class CombatPill
{
    public string Label { get; set; } = string.Empty;

    public Brush Brush { get; set; } = Brushes.Gray;

    public Brush Background { get; set; } = Brushes.Transparent;
}

/// <summary>卡片里的一行数值：数量（大字号）+ 其余说明（小字号）。</summary>
public sealed class CombatCardLine
{
    /// <summary>数量（如 <c>100</c>）。</summary>
    public string Amount { get; set; } = string.Empty;

    /// <summary>单位 / 每秒 / 遥修类型（如 <c>HP 50 HP/s 护盾</c>）。</summary>
    public string Rest { get; set; } = string.Empty;

    /// <summary>harm（红）/ benefit（绿）/ neutral（黑白）。</summary>
    public string Tone { get; set; } = "harm";

    /// <summary>数量字号（阶段 372：每行数量放大）。</summary>
    public double FontSize { get; set; } = 16;

    /// <summary>数量后面说明的字号。</summary>
    public double RestFontSize { get; set; } = 10;

    /// <summary>整行文本（探针 / 兼容用）。</summary>
    public string Text => string.IsNullOrWhiteSpace(Rest) ? Amount : $"{Amount} {Rest}";
}

public sealed class CombatCard : INotifyPropertyChanged
{
    private double _height = 70;

    public string Label { get; set; } = string.Empty;

    /// <summary>悬停详情（每行一条：方向、分类、装备、来源、实测间隔、时间）。</summary>
    public string Detail { get; set; } = string.Empty;

    public string ToolTipText => Expired ? LanguageService.Get("CombatLog_Expired") : Detail;

    /// <summary>数值行（遥修最多六行：护盾/装甲/结构的输入与输出；其它最多两行）。</summary>
    public IReadOnlyList<CombatCardLine> Lines { get; set; } = [];

    /// <summary>行字号（行多时自动缩小，见 <c>LineFontSize</c>）。</summary>
    public double LineFontSize { get; set; } = 15;

    /// <summary>卡片高度（随行数与字号变化，见 <c>CardHeight</c>）。</summary>
    public double Height
    {
        get => _height;
        set
        {
            if (Math.Abs(_height - value) < 0.5)
            {
                return;
            }

            _height = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Height)));
        }
    }

    public Brush Brush { get; set; } = Brushes.Gray;

    /// <summary>阶段 362/368：超过过期时间没有新日志（卡片只置灰，悬停提示显示「已过期」）。</summary>
    public bool Expired { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class FeedRow : INotifyPropertyChanged
{
    private bool _isNew;

    public string Time { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public bool IsNew
    {
        get => _isNew;
        set
        {
            if (_isNew == value)
            {
                return;
            }

            _isNew = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNew)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
