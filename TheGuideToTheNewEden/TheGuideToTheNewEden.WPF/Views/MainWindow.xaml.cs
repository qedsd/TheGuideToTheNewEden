using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.Views;

/// <summary>
/// 主窗口：FluentWindow + 左侧导航菜单 + 右侧内容区，托盘图标支持后台运行。
/// 窗口位置/大小以物理像素持久化，跨显示器（含不同 DPI）也能恢复到上次位置。
/// </summary>
public partial class MainWindow : FluentWindow
{
    /// <summary>
    /// 导航内容相对窗口顶部的预留高度（即标题栏区域的高度）。
    /// 不能在 XAML 里写 FrameMargin：WPF-UI 的 NavigationView 在绑定 TitleBar 后
    /// 会用默认值 (0,50,0,0) 覆盖它，因此这里在加载后重新应用。
    /// </summary>
    private const double NavigationTopInset = 36;

    private bool _isShuttingDown;

    /// <summary>移动/缩放后的延迟落盘定时器，避免拖动过程中频繁写文件。</summary>
    private readonly System.Windows.Threading.DispatcherTimer _placementSaveTimer;

    /// <summary>位置恢复完成前不写入，避免把默认/居中位置当成用户位置保存。</summary>
    private bool _placementReady;

    public MainWindow()
    {
        InitializeComponent();

        _placementSaveTimer = new System.Windows.Threading.DispatcherTimer(
            System.Windows.Threading.DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(600),
        };
        _placementSaveTimer.Tick += (_, _) =>
        {
            _placementSaveTimer.Stop();
            SaveWindowPlacement();
        };

        // 位置完全由代码控制：有历史位置则恢复，否则居中到鼠标所在的显示器。
        WindowStartupLocation = WindowStartupLocation.Manual;

        SourceInitialized += (_, _) =>
        {
            RestoreWindowPlacement();
            _placementReady = true;
        };

        // 窗口移动/缩放/最大化都即时登记，进程被异常终止也不会丢位置。
        LocationChanged += (_, _) => SchedulePlacementSave();
        SizeChanged += (_, _) => SchedulePlacementSave();
        StateChanged += (_, _) => SchedulePlacementSave();

        // WPF-UI 在 TitleBar 属性生效时覆盖 FrameMargin，此处在其之后重新应用自定义值。
        DependencyPropertyDescriptor
            .FromProperty(NavigationView.TitleBarProperty, typeof(NavigationView))
            ?.AddValueChanged(NavigationView, (_, _) => ApplyFrameMargin());

        Loaded += (_, _) =>
        {
            ApplyFrameMargin();

            // 启动时打开首页。
            NavigationView.Navigate(typeof(Pages.HomePage));
        };

        Closing += MainWindow_Closing;

        // 托盘左键单击恢复主窗口；并把托盘实例交给通知服务（供系统通知使用）。
        TrayIcon.TrayLeftMouseUp += OnTrayLeftMouseUp;
        TrayMenuShow.Click += OnTrayShowClick;
        TrayMenuExit.Click += OnTrayExitClick;
        NotificationService.Register(TrayIcon);
    }

    private void ApplyFrameMargin()
    {
        NavigationView.FrameMargin = new Thickness(0, NavigationTopInset, 0, 0);
    }

    private void SchedulePlacementSave()
    {
        if (!_placementReady)
        {
            return;
        }

        _placementSaveTimer.Stop();
        _placementSaveTimer.Start();
    }

    // ---------- 窗口位置持久化（物理像素，DPI 安全） ----------

    private void RestoreWindowPlacement()
    {
        var handle = new WindowInteropHelper(this).Handle;

        var left = SettingsService.GetDouble(SettingsService.WindowLeftKey);
        var top = SettingsService.GetDouble(SettingsService.WindowTopKey);
        var width = SettingsService.GetDouble(SettingsService.WindowWidthKey);
        var height = SettingsService.GetDouble(SettingsService.WindowHeightKey);

        // 没有历史位置，或历史位置已越界（显示器变动）→ 居中到鼠标所在的显示器。
        if (left is null || top is null || width is null || height is null)
        {
            WindowPlacementHelper.TryCenterOnMonitor(handle, preferMonitorUnderCursor: true);
            return;
        }

        var rect = new WindowPlacementHelper.Rect
        {
            Left = (int)left.Value,
            Top = (int)top.Value,
            Right = (int)(left.Value + width.Value),
            Bottom = (int)(top.Value + height.Value),
        };

        if (!WindowPlacementHelper.IsVisibleOnAnyScreen(rect))
        {
            WindowPlacementHelper.TryCenterOnMonitor(handle, preferMonitorUnderCursor: true);
            return;
        }

        if (!WindowPlacementHelper.TryGet(handle, out var placement))
        {
            return;
        }

        placement.NormalPosition = rect;
        placement.ShowCmd = SettingsService.GetBool(SettingsService.WindowMaximizedKey)
            ? WindowPlacementHelper.SwShowMaximized
            : WindowPlacementHelper.SwShowNormal;
        WindowPlacementHelper.TrySet(handle, placement);
    }

    private void SaveWindowPlacement()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (!WindowPlacementHelper.TryGet(handle, out var placement))
        {
            return;
        }

        // NormalPosition 是还原后的矩形（最大化时也是），且为物理像素。
        var rect = placement.NormalPosition;
        SettingsService.SetDouble(SettingsService.WindowLeftKey, rect.Left);
        SettingsService.SetDouble(SettingsService.WindowTopKey, rect.Top);
        SettingsService.SetDouble(SettingsService.WindowWidthKey, rect.Right - rect.Left);
        SettingsService.SetDouble(SettingsService.WindowHeightKey, rect.Bottom - rect.Top);
        SettingsService.SetBool(
            SettingsService.WindowMaximizedKey,
            placement.ShowCmd == WindowPlacementHelper.SwShowMaximized);
    }

    // ---------- 托盘 ----------

    private void OnTrayLeftMouseUp(object sender, RoutedEventArgs e) => RestoreMainWindow();

    private void OnTrayShowClick(object sender, RoutedEventArgs e) => RestoreMainWindow();

    private void OnTrayExitClick(object sender, RoutedEventArgs e) => ShutdownApplication();

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        SaveWindowPlacement();

        // 关闭按钮：开启托盘后台时只隐藏窗口；托盘退出时 _isShuttingDown 为 true，不再拦截。
        if (!_isShuttingDown && SettingsService.GetBool(SettingsService.MinimizeToTrayKey))
        {
            e.Cancel = true;
            Hide();
            return;
        }

        SettingsService.Save();

        if (!_isShuttingDown)
        {
            System.Windows.Application.Current.Shutdown();
        }
    }

    private void RestoreMainWindow()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    private void ShutdownApplication()
    {
        _isShuttingDown = true;
        SaveWindowPlacement();
        SettingsService.Save();
        System.Windows.Application.Current.Shutdown();
    }
}