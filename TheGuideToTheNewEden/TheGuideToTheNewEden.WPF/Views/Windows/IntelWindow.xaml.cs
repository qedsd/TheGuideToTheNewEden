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
using TheGuideToTheNewEden.WPF.Services.Settings;

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
    private const double ExpandedDrawerHeight = 210;

    private readonly ChannelIntelSetting _setting;
    private IntelSolarSystemMap _intelMap;
    private readonly Dictionary<int, DateTime> _startTimes = [];
    private readonly ObservableCollection<IntelChatRow> _chatRows = [];
    private readonly ICollectionView _chatView;

    private DispatcherTimer? _autoIntelTimer;
    private DispatcherTimer? _delayHideTimer;
    private bool _disposed;
    private bool _closing;
    private bool _drawerExpanded;
    private double _expandedDrawerHeight = ExpandedDrawerHeight;
    private string _filter = "All";

    /// <summary>用户点击标题栏关闭（会话据此停止预警）。</summary>
    public event EventHandler? StopRequested;

    public IntelWindow(ChannelIntelSetting setting, IntelSolarSystemMap intelMap)
    {
        _setting = setting;
        _intelMap = intelMap;

        InitializeComponent();
        ApplyBackdrop();
        ThemeService.ThemeChanged += ApplyBackdrop;

        Width = setting.WinW > 0 ? Math.Max(setting.WinW, 560) : 640;
        Height = setting.WinH > 0 ? Math.Max(setting.WinH, 520) : 620;
        Left = setting.WinX;
        Top = setting.WinY;
        EnsureOnScreen();

        TitleText.Text = $"{FindString("IntelWindow_Title")} · {setting.Listener}";
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
        if (_drawerExpanded && _chatRows.Count > 0)
        {
            ChatList.ScrollIntoView(_chatRows[^1]);
        }
    }

    private bool FilterChatRow(object item)
        => item is IntelChatRow row
           && (_filter == "All" || string.Equals(row.Kind, _filter, StringComparison.Ordinal));

    private void UpdateChannelHeader()
    {
        var count = _chatRows.Count;
        DrawerCountText.Text = count.ToString();
        ChatEmptyText.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;

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
        TitleStatusText.Text = MapView.ActiveCount > 0
            ? FindString("ChannelIntelPage_Running")
            : FindString("IntelWindow_Standby");
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
        if (_drawerExpanded && DrawerRow.ActualHeight >= 80)
        {
            _expandedDrawerHeight = DrawerRow.ActualHeight;
        }

        _drawerExpanded = expanded;
        DrawerSplitter.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        DrawerRow.MinHeight = expanded ? 80 : 0;
        DrawerRow.Height = new GridLength(expanded ? _expandedDrawerHeight : CollapsedDrawerHeight);
        DrawerBody.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        DrawerChevron.RenderTransformOrigin = new Point(0.5, 0.5);
        DrawerChevron.RenderTransform = new RotateTransform(expanded ? 180 : 0);
    }

    private void OnDrawerSplitterDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (DrawerRow.ActualHeight >= 80)
        {
            _expandedDrawerHeight = DrawerRow.ActualHeight;
        }
    }

    private void OnDrawerHeaderClick(object sender, RoutedEventArgs e)
        => SetDrawer(!_drawerExpanded);

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
        => WindowState = WindowState.Minimized;

    private void OnCloseClick(object sender, RoutedEventArgs e)
        => Close();

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
