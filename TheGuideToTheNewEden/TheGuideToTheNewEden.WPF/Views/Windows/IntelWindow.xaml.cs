using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.ChannelIntel;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.Core.Models.Map;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.ChannelIntel;
using TheGuideToTheNewEden.WPF.Helpers.Interop;
using TheGuideToTheNewEden.WPF.Services.GamePreview;
using TheGuideToTheNewEden.WPF.Services.Settings;
using System.Linq;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 频道预警置顶小窗：标题 / 星图 / 频道内容抽屉 / 地图右下角悬浮操作。
/// <para>
/// 星图数据始终是配置的 N 跳范围；频道抽屉默认收起，只显示最新摘要；展开后显示频道原文，
/// 支持全部/预警/解除/击杀过滤。原有清除、前置游戏、停止声音、自动解除/降级、
/// 必要时隐藏、位置/尺寸/不透明度记忆全部保留。
/// </para>
/// </summary>
public partial class IntelWindow : Window
{
    private const int MaxChatRows = 200;
    private const double CollapsedDrawerHeight = 43;
    /// <summary>头部行(28) + 过滤排(约 32) + 边距，抽屉内列表以外的固定高度。</summary>
    private const double DrawerFixedHeight = 68;
    private const double ExpandedDrawerHeight = 210;

    internal readonly ChannelIntelSetting _setting;
    private readonly IntelGlobalSetting _globalSetting;
    private readonly Func<IReadOnlyList<NativeMethods.RECT>>? _snapTargets;
    private readonly PreviewSnapSession _snapSession = new();
    private IntelSolarSystemMap _intelMap;
    private NativeMethods.RECT _dragStartRect;
    private NativeMethods.POINT _dragStartCursor;
    private bool _dragCandidate;
    private bool _dragging;
    private readonly Dictionary<int, DateTime> _startTimes = [];
    private readonly ObservableCollection<IntelChatRow> _chatRows = [];
    private readonly ICollectionView _chatView;

    private DispatcherTimer? _autoIntelTimer;
    private DispatcherTimer? _delayHideTimer;
    private bool _disposed;
    private bool _closing;
    private bool _drawerExpanded;
    /// <summary>阶段 344：用户拖动过分隔条 → 列表改为"填满抽屉"，不再按内容收紧。</summary>
    private bool _fillListHeight;
    private double _expandedDrawerHeight = ExpandedDrawerHeight;
    private string _filter = "All";

    /// <summary>用户点击标题栏关闭（会话据此停止预警）。</summary>
    public event EventHandler? StopRequested;

    internal IntelWindow(ChannelIntelSetting setting, IntelSolarSystemMap intelMap, IntelGlobalSetting globalSetting, Func<IReadOnlyList<NativeMethods.RECT>>? snapTargets = null)
    {
        _setting = setting;
        _globalSetting = globalSetting;
        _snapTargets = snapTargets;
        _intelMap = intelMap;

        InitializeComponent();
        ApplyBackdrop();
        ThemeService.ThemeChanged += ApplyBackdrop;

        // 阶段 318：尺寸有效性收紧——过小的保存尺寸（旧版本/异常路径写入）视为无效，回退默认；
        // 没保存过位置（WinX/WinY=-1）则居中启动，不再顶到屏幕左上角 (-1,-1)。
        Width = setting.WinW >= 80 ? setting.WinW : 640;
        Height = setting.WinH >= 60 ? setting.WinH : 620;
        if (setting.WinX < 0 && setting.WinY < 0)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        else
        {
            Left = setting.WinX;
            Top = setting.WinY;
        }

        EnsureOnScreen();

        // 阶段 328：标题只显示角色名（去掉"频道预警"前缀）
        Title = setting.Listener;
        TitleText.Text = setting.Listener;
        TitleStatusText.Text = FindString("ChannelIntelPage_Running");

        _chatView = CollectionViewSource.GetDefaultView(_chatRows);
        _chatView.Filter = FilterChatRow;
        ChatList.ItemsSource = _chatView;

        MapView.Init(intelMap, setting);
        MapView.SystemClearRequested += OnSystemClearRequested;

        SetDrawer(false);
        UpdateStatus();
        UpdateChannelHeader();
        UpdateFilterVisual();
        InitTimer();

        LocationChanged += OnWindowBoundsChanged;
        SizeChanged += OnWindowBoundsChanged;
        Closed += (_, _) => Dispose();

        // 阶段 307：标题栏手动拖动 + 与其它预警小窗吸附（CaptionHeight=0，原生拖拽已关闭）
        TitleBarGrid.MouseLeftButtonDown += OnTitleDragStart;
        TitleBarGrid.MouseMove += OnTitleDragMove;
        TitleBarGrid.MouseLeftButtonUp += OnTitleDragEnd;
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;

    /// <summary>按“不透明度”设置生成背板画刷：只改 alpha，颜色仍取主题背景色（主题切换后重算）。</summary>
    private void ApplyBackdrop()
    {
        var color = (Application.Current?.TryFindResource("ApplicationBackgroundBrush") as SolidColorBrush)?.Color
            ?? (Application.Current?.TryFindResource("CardBackgroundFillColorDefaultBrush") as SolidColorBrush)?.Color
            ?? Colors.White;
        var alpha = (byte)(Math.Clamp(_setting.OverlapOpacity, 1, 100) * 255 / 100);
        BackdropPlate.Background = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
    }

    // ---------- 显示与位置 ----------

    public void ShowWindow()
    {
        try
        {
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            if (!IsVisible)
            {
                Show();
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private void OnWindowBoundsChanged(object? sender, EventArgs e)
    {
        if (_disposed || WindowState != WindowState.Normal)
        {
            return;
        }

        _setting.WinX = (int)Left;
        _setting.WinY = (int)Top;
        _setting.WinW = (int)Width;
        _setting.WinH = (int)Height;
        IntelSettingService.SetValue(_setting);
    }

    private void EnsureOnScreen()
    {
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
        var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;
        if (Left < virtualLeft - Width || Left > virtualRight || Top < virtualTop - Height || Top > virtualBottom)
        {
            Left = double.NaN;
            Top = double.NaN;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    public void RestoreWindowPos()
    {
        Left = double.NaN;
        Top = double.NaN;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    // ---------- 拖动与吸附（阶段 307，对齐多开预览） ----------

    internal IntPtr GetHwnd()
        => _hwnd == IntPtr.Zero ? (_hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle) : _hwnd;

    private IntPtr _hwnd;

    /// <summary>本窗物理像素矩形（供管理器做吸附候选/统一排列）。</summary>
    internal NativeMethods.RECT GetRectPhysical()
        => NativeMethods.GetWindowRect(GetHwnd(), out var rect) ? rect : new NativeMethods.RECT();

    private bool IsButtonSource(object originalSource)
    {
        for (var d = originalSource as System.Windows.DependencyObject; d is not null; d = System.Windows.Media.VisualTreeHelper.GetParent(d))
        {
            if (d is System.Windows.Controls.Primitives.ButtonBase)
            {
                return true;
            }
        }

        return false;
    }

    private void OnTitleDragStart(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (IsButtonSource(e.OriginalSource))
        {
            return;
        }

        if (e.ClickCount >= 2)
        {
            // 与原生标题栏双击一致：还原/最大化
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            e.Handled = true;
            return;
        }

        NativeMethods.GetCursorPos(out _dragStartCursor);
        _dragStartRect = GetRectPhysical();
        _snapSession.Reset();
        _dragCandidate = true;
        _dragging = false;
        TitleBarGrid.CaptureMouse();
        e.Handled = true;
    }

    private void OnTitleDragMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_dragCandidate || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed || _hwnd == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.GetCursorPos(out var cursor);
        var dx = cursor.X - _dragStartCursor.X;
        var dy = cursor.Y - _dragStartCursor.Y;
        if (!_dragging && Math.Abs(dx) + Math.Abs(dy) < 4)
        {
            return;
        }

        _dragging = true;
        var x = _dragStartRect.Left + dx;
        var y = _dragStartRect.Top + dy;

        // 吸附：只吸其它可见预警小窗（沿对齐边滑动由 PreviewSnapSession 维持）
        if (_globalSetting.SnapEnabled && _globalSetting.SnapDistance > 0 && _snapTargets is not null)
        {
            var proposed = new NativeMethods.RECT
            {
                Left = x,
                Top = y,
                Right = x + _dragStartRect.Width,
                Bottom = y + _dragStartRect.Height,
            };
            var snap = _snapSession.Snap(proposed, _snapTargets(), _globalSetting.SnapDistance);
            x = snap.X;
            y = snap.Y;
        }
        else
        {
            _snapSession.Reset();
        }

        NativeMethods.MoveWindow(_hwnd, x, y);
        e.Handled = true;
    }

    private void OnTitleDragEnd(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (TitleBarGrid.IsMouseCaptured)
        {
            TitleBarGrid.ReleaseMouseCapture();
        }

        _dragCandidate = false;
        _dragging = false;
        _snapSession.Reset();
    }

    /// <summary>统一尺寸：按全局设置的宽高（DIP）调整本窗（SizeChanged 自动持久化到每角色 WinW/WinH）。</summary>
    public void SetUniformSize(int width, int height)
    {
        if (width > 0)
        {
            Width = width;
        }

        if (height > 0)
        {
            Height = height;
        }
    }

    // ---------- 预警内容 ----------

    /// <summary>推送一条预警（更新星图与标题状态）。</summary>
    public void Intel(EarlyWarningContent content, string? channel = null)
    {
        MapView.Intel(content);
        UpdateStatus();

        if (!_disposed)
        {
            if (content.IntelType == Core.Enums.IntelChatType.Intel)
            {
                _startTimes[content.SolarSystemId] = DateTime.Now;
                ShowWindow();
            }
            else if (content.IntelType == Core.Enums.IntelChatType.Clear)
            {
                _startTimes.Remove(content.SolarSystemId);
                UpdateStatus();
                TryHideWindow();
            }
        }

        // ZKB 击杀不走频道日志，单独补一行到频道列表（频道行本身不重复）。
        if (string.Equals(channel, "KB", StringComparison.OrdinalIgnoreCase))
        {
            AppendChatRow(IntelChatRow.FromWarning(content, FindString("IntelWindow_KB"), FindString("EarlyWarningPage_Jumps")));
        }
    }

    /// <summary>把频道原文追加到本窗口的频道抽屉。</summary>
    public void ChatContent(IEnumerable<IntelChatContent> contents)
    {
        var list = contents?.ToList();
        if (list is not { Count: > 0 })
        {
            return;
        }

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => ChatContent(list));
            return;
        }

        var jumpsText = FindString("EarlyWarningPage_Jumps");
        var channelFallback = FindString("IntelWindow_ChannelFallback");
        foreach (var content in list)
        {
            AppendChatRow(IntelChatRow.FromChat(content, jumpsText, channelFallback), refresh: false);
        }

        RefreshChatView();
    }

    private void AppendChatRow(IntelChatRow row, bool refresh = true)
    {
        _chatRows.Add(row);
        while (_chatRows.Count > MaxChatRows)
        {
            _chatRows.RemoveAt(0);
        }

        if (refresh)
        {
            RefreshChatView();
        }
    }

    private void RefreshChatView()
    {
        _chatView.Refresh();
        UpdateChannelHeader();

        // 阶段 343：高度与滚动统一交给 UpdateChatListHeight
        // （先撑到上限让内容完整测量 → 按像素 ExtentHeight 收紧 → 滚到底）
        if (_drawerExpanded)
        {
            Dispatcher.BeginInvoke(UpdateChatListHeight, System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }


    /// <summary>阶段 334：深度优先枚举可视树（供 ScrollViewer 查找）。</summary>
    private static System.Collections.Generic.IEnumerable<System.Windows.DependencyObject> WpfTraversal(System.Windows.DependencyObject root)
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in WpfTraversal(child))
            {
                yield return nested;
            }
        }
    }

    private bool FilterChatRow(object item)
        => item is IntelChatRow row
           && (_filter == "All" || string.Equals(row.Kind, _filter, StringComparison.Ordinal));

    private void UpdateChannelHeader()
    {
        // 阶段 332：空态文字跟随实际条数（此前 XAML 常显，从不清隐）
        ChatEmptyText.Visibility = _chatRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var count = _chatRows.Count;
        // 阶段 330：DrawerCountText 已随头部"频道内容 0"文案移除

        var latest = _chatRows.LastOrDefault();
        if (latest is null)
        {
            LatestSnippetText.Text = FindString("IntelWindow_NoMessages");
            return;
        }

        var system = string.IsNullOrWhiteSpace(latest.SystemText) ? latest.Channel : latest.SystemText;
        var jump = string.IsNullOrWhiteSpace(latest.JumpText) ? string.Empty : $" {latest.JumpText}";
        LatestSnippetText.Text = $"{system}{jump} · {latest.Message}";
    }

    private void UpdateStatus()
    {
        var active = MapView.ActiveCount > 0;
        // 阶段 329：颜色圆点 + ToolTip 文字（隐藏文本作为绑定源）
        TitleStatusText.Text = active
            ? FindString("ChannelIntelPage_Running")
            : FindString("IntelWindow_NoActiveAlert");
        TitleStatusDot.ToolTip = TitleStatusText.Text;
        TitleStatusDot.Fill = active
            ? (Brush)FindResource("IntelCautionBrush")
            : (Brush)FindResource("IntelGreenBrush");
    }

    private void OnSystemClearRequested(object? sender, int systemId)
    {
        MapView.Clear([systemId]);
        _startTimes.Remove(systemId);
        UpdateStatus();
        TryHideWindow();
    }

    public void UpdateHome(IntelSolarSystemMap intelMap)
    {
        _intelMap = intelMap;
        _startTimes.Clear();
        MapView.UpdateHome(intelMap);
        UpdateStatus();
    }

    // ---------- 频道抽屉 / 过滤器 ----------

    private void SetDrawer(bool expanded)
    {
        // 阶段 340：不再把当前实际高记为记忆高——收紧后的高度会被当成新的天花板（棘轮越用越矮）；
        // 记忆高只由用户拖动分隔条（OnDrawerSplitterDragCompleted）更新。

        _drawerExpanded = expanded;
        DrawerSplitter.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        // 阶段 341：行高交给布局引擎——Auto 行 + ListBox.MaxHeight 封顶：
        // 抽屉高 = 头部 + 过滤排 + min(内容高, MaxHeight)。内容少 → 贴内容（无空白）；
        // 内容多 → ListBox 自身封顶并在内部滚动（不再由 Row 裁剪，滚轮/滚动条正常）。
        DrawerRow.MinHeight = 0;
        ChatList.MaxHeight = expanded
            ? Math.Max(80, _expandedDrawerHeight - DrawerFixedHeight)
            : double.PositiveInfinity;
        if (expanded)
        {
            // 阶段 344：每次（重新）展开回到“按内容收紧”模式
            _fillListHeight = false;
            // 阶段 342：MaxHeight 只是上限，Auto 行下 ListBox 会吃满上限；显式 Height 才贴合内容
            Dispatcher.BeginInvoke(UpdateChatListHeight, System.Windows.Threading.DispatcherPriority.Loaded);
        }
        DrawerRow.Height = new GridLength(1, GridUnitType.Auto);
        DrawerBody.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        DrawerChevron.RenderTransformOrigin = new Point(0.5, 0.5);
        DrawerChevron.RenderTransform = new RotateTransform(expanded ? 180 : 0);
    }

    /// <summary>
    /// 阶段 342：把列表高度设为【条数 × 实测行高】（封顶记忆高）。
    /// 探针实测：Auto 行 + MaxHeight 时 ListBox 会直接吃满 MaxHeight（内容少也留白），
    /// 因此上限之外还必须显式给 Height；行高取自已实化的首个 ListBoxItem。
    /// </summary>
    private void UpdateChatListHeight()
    {
        if (!_drawerExpanded || _disposed)
        {
            return;
        }

        if (_fillListHeight)
        {
            // 用户拖动过 → 保持填满（内容少时留空是用户自己设定的）
            FillChatListHeight();
            return;
        }

        var max = Math.Max(80, _expandedDrawerHeight - DrawerFixedHeight);
        // 阶段 343（探针实测）：CanContentScroll=true 时 ExtentHeight 单位是"项"（3 条 → 3），
        // 用它加固定高永远只有几十像素；设为 false 才是像素（3 条 → 153）。
        // 因此先撑到上限让内容完整测量，布局后按【像素 ExtentHeight】收紧。
        ChatList.Height = max;
        Dispatcher.BeginInvoke(() =>
        {
            if (!_drawerExpanded || _disposed)
            {
                return;
            }

            var scrollViewer = WpfTraversal(ChatList)
                .OfType<System.Windows.Controls.ScrollViewer>()
                .FirstOrDefault();
            if (scrollViewer is null)
            {
                return;
            }

            var desired = Math.Min(max, scrollViewer.ExtentHeight + 2);
            ChatList.Height = Math.Max(desired, 80);
            scrollViewer.ScrollToEnd();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }


    /// <summary>
    /// 阶段 344：拖动中实时同步（GridSplitter 只改行高，列表是显式高度不会跟着长），
    /// 否则拖动后抽屉变高、列表不变 → 下方留白。
    /// </summary>
    private void OnDrawerSplitterDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!_drawerExpanded || _disposed)
        {
            return;
        }

        if (DrawerRow.ActualHeight >= 80)
        {
            _expandedDrawerHeight = DrawerRow.ActualHeight;
        }

        FillChatListHeight();
    }

    private void OnDrawerSplitterDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (DrawerRow.ActualHeight >= 80)
        {
            _expandedDrawerHeight = DrawerRow.ActualHeight;
        }

        FillChatListHeight();
    }

    /// <summary>
    /// 阶段 344：把列表填满抽屉可用高度（拖动后用户已明确设定尺寸，内容少时留空是预期行为）。
    /// </summary>
    private void FillChatListHeight()
    {
        if (!_drawerExpanded || _disposed)
        {
            return;
        }

        var max = Math.Max(80, _expandedDrawerHeight - DrawerFixedHeight);
        ChatList.MaxHeight = max;
        _fillListHeight = true;
        ChatList.Height = max;
    }

    private void OnDrawerHeaderClick(object sender, RoutedEventArgs e)
        => SetDrawer(!_drawerExpanded);

    /// <summary>阶段 334：清空预警列表内容（不动星图上的预警状态）。</summary>
    private void OnClearListClick(object sender, RoutedEventArgs e)
    {
        _chatRows.Clear();
        RefreshChatView();
    }

    private void OnFilterClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string filter })
        {
            return;
        }

        _filter = filter;
        _chatView.Refresh();
        UpdateFilterVisual();
    }

    private void UpdateFilterVisual()
    {
        foreach (var button in new[] { FilterAllButton, FilterAlertButton, FilterClearButton, FilterKillButton })
        {
            var active = string.Equals(button.Tag as string, _filter, StringComparison.Ordinal);
            if (button is Wpf.Ui.Controls.Button uiButton)
            {
                uiButton.Appearance = active
                    ? Wpf.Ui.Controls.ControlAppearance.Primary
                    : Wpf.Ui.Controls.ControlAppearance.Secondary;
            }
        }
    }

    // ---------- 自动解除 / 自动降级 ----------

    private void InitTimer()
    {
        _autoIntelTimer?.Stop();
        if (_setting.AutoClear || _setting.AutoDowngrade)
        {
            _autoIntelTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _autoIntelTimer.Tick += (_, _) =>
            {
                if (_setting.AutoClear)
                {
                    ClearElapsed();
                }

                if (_setting.AutoDowngrade)
                {
                    DowngradeElapsed();
                }
            };
            _autoIntelTimer.Start();
        }
    }

    private void ClearElapsed()
    {
        var now = DateTime.Now;
        var remove = _startTimes
            .Where(p => (now - p.Value).TotalMinutes >= _setting.AutoClearMinute)
            .Select(p => p.Key)
            .ToList();
        foreach (var id in remove)
        {
            _startTimes.Remove(id);
        }

        if (remove.Count > 0)
        {
            MapView.Clear(remove);
            UpdateStatus();
            TryHideWindow();
        }
    }

    private void DowngradeElapsed()
    {
        var now = DateTime.Now;
        var changed = _startTimes
            .Where(p => (now - p.Value).TotalMinutes >= _setting.AutoDowngradeMinute)
            .Select(p => p.Key)
            .ToList();
        if (changed.Count > 0)
        {
            MapView.Downgrade(changed);
            UpdateStatus();
        }
    }

    /// <summary>“必要时显示”模式：全部预警解除后延迟 30 秒隐藏窗口。</summary>
    private void TryHideWindow()
    {
        if (_setting.OverlapType != 1 || _startTimes.Count != 0)
        {
            return;
        }

        _delayHideTimer?.Stop();
        _delayHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _delayHideTimer.Tick += (_, _) =>
        {
            _delayHideTimer?.Stop();
            if (_startTimes.Count == 0 && IsVisible)
            {
                Hide();
                IntelWarningService.Current.StopSound(_setting.Listener);
            }
        };
        _delayHideTimer.Start();
    }

    // ---------- 地图悬浮按钮 / 缩放 ----------

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        // 工具窗口 ShowInTaskbar=False 时，Windows 会把最小化窗口丢到屏幕左下角，
        // 且任务栏没有恢复入口。最小化期间临时显示任务栏按钮，恢复后再隐藏。
        ShowInTaskbar = true;
        WindowState = WindowState.Minimized;
    }

    protected override void OnStateChanged(EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            ShowInTaskbar = true;
        }
        else if (WindowState == WindowState.Normal)
        {
            ShowInTaskbar = false;
        }

        base.OnStateChanged(e);
    }


    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        MapView.Clear();
        _startTimes.Clear();
        UpdateStatus();
    }

    private void OnStopSoundClick(object sender, RoutedEventArgs e)
        => IntelWarningService.Current.StopSound(_setting.Listener);

    private void OnActivateGameClick(object sender, RoutedEventArgs e)
        => GameWindowHelper.BringGameToFront(_setting.Listener);

    /// <summary>阶段 325：专注模式——隐藏频道内容抽屉/右侧按钮/颜色图例，只留标题、最小化、星图、本开关。</summary>
    private bool _focusMode;

    private void OnFocusClick(object sender, RoutedEventArgs e)
    {
        _focusMode = !_focusMode;
        FocusButton.Icon = new Wpf.Ui.Controls.SymbolIcon
        {
            Symbol = _focusMode ? Wpf.Ui.Controls.SymbolRegular.ArrowMaximize24 : Wpf.Ui.Controls.SymbolRegular.ArrowMinimize24,
        };
        FocusButton.ToolTip = FindString(_focusMode ? "IntelWindow_FocusRestore" : "IntelWindow_FocusHide");

        TitleStatusDot.Visibility = _focusMode ? Visibility.Collapsed : Visibility.Visible;
        ResetViewButton.Visibility = _focusMode ? Visibility.Collapsed : Visibility.Visible;
        ClearButton.Visibility = _focusMode ? Visibility.Collapsed : Visibility.Visible;
        StopSoundButton.Visibility = _focusMode ? Visibility.Collapsed : Visibility.Visible;
        ActivateGameButton.Visibility = _focusMode ? Visibility.Collapsed : Visibility.Visible;

        if (_focusMode)
        {
            SetDrawer(false);
            DrawerRow.Height = new GridLength(0);
            DrawerSplitter.Visibility = Visibility.Collapsed;
        }
        else
        {
            DrawerSplitter.Visibility = Visibility.Visible;
            SetDrawer(_drawerExpanded);
        }
    }

    private void OnResetViewClick(object sender, RoutedEventArgs e) => MapView.ResetView();

    // ---------- 关闭与释放 ----------

    protected override void OnClosing(CancelEventArgs e)
    {
        _closing = true;
        if (!_disposed)
        {
            StopRequested?.Invoke(this, EventArgs.Empty);
        }

        base.OnClosing(e);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ThemeService.ThemeChanged -= ApplyBackdrop;
        MapView.SystemClearRequested -= OnSystemClearRequested;
        MapView.DetachGlobalSetting();
        _autoIntelTimer?.Stop();
        _delayHideTimer?.Stop();
        LocationChanged -= OnWindowBoundsChanged;
        SizeChanged -= OnWindowBoundsChanged;

        if (!_closing)
        {
            Close();
        }
    }
}
