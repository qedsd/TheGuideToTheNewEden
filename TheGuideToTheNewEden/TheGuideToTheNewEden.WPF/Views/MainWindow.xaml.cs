using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Home;
using TheGuideToTheNewEden.WPF.Services.Tools;
using Wpf.Ui.Controls;
// WPF-UI 也有自己的 MenuItem，菜单项这里一律用 WPF 原生的那个。
using MenuItem = System.Windows.Controls.MenuItem;

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

    /// <summary>启动时托盘图标是否已创建成功（未成功则 Loaded 后重试，见 <see cref="TrayIconHelper"/>）。</summary>
    private bool _trayIconReady;

    /// <summary>
    /// 「工具」分组的页面 → 窗口标题的本地化键。这些页面**在独立工具窗口里打开**
    /// （对齐 WinUI 的 <c>ITool</c> 契约），不占用主窗口内容区；每个工具同时只开一个。
    /// </summary>
    private static readonly Dictionary<Type, string> ToolPages = new()
    {
        [typeof(Pages.IMEPage)] = "Nav.Tools_IME",
        [typeof(Pages.ServerPingPage)] = "Nav.Tools_ServerPing",
        [typeof(Pages.KeyboardSpanPage)] = "Nav.Tools_KeyboardSpan",
        [typeof(Pages.TaskbarPage)] = "Nav.Tools_Taskbar",
    };

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

        // 标题栏/系统标题都带上版本号；语言切换后名称要重新取，故两种时机各刷一次
        ApplyTitle();
        LanguageService.LanguageChanged += (_, _) => ApplyTitle();

        Loaded += (_, _) =>
        {
            ApplyFrameMargin();

            // 启动时打开首页。
            NavigationView.Navigate(typeof(Pages.HomePage));

            // 托盘图标没建起来就重试几次（通知区域在启动早期可能还没就绪）。
            if (!_trayIconReady)
            {
                EnsureTrayIconAsync();
            }
        };

        Closing += MainWindow_Closing;

        // 「工具」分组的页面不进主内容区：拦下导航，改为打开独立工具窗口。
        NavigationView.Navigating += OnNavigationViewNavigating;

        // 托盘左键单击恢复主窗口；并把托盘实例交给通知服务（供系统通知使用）。
        TrayIcon.TrayLeftMouseUp += OnTrayLeftMouseUp;
        TrayMenuShow.Click += OnTrayShowClick;
        TrayMenuExit.Click += OnTrayExitClick;
        NotificationService.Register(TrayIcon);

        // 托盘图标：**在 Loaded 之前先自己创建一次**。H.NotifyIcon 会在自己的 Loaded 里创建一次，
        // 失败时只在 catch 里 Debugger.Break()（Release 下静默、且真实 Win32 原因被库吞掉，见 TrayIconHelper）。
        // 先创建成功后，库在 Loaded 里的那次 ForceCreate 直接返回，调试时也不会中断。
        _trayIconReady = TrayIconHelper.TryCreate(TrayIcon, "启动时");

        // 页面级等待遮罩 / 右下角即时通知（ShowWaiting、ShowSuccess 等）
        PageNotifyService.Register(WaitingOverlay, MessageHost);

        // 左侧导航项右键：固定 / 取消固定到主页（主页快捷方式的最短入口）
        AttachHomeShortcutMenus();
    }

    private void ApplyFrameMargin()
    {
        NavigationView.FrameMargin = new Thickness(0, NavigationTopInset, 0, 0);
    }

    // ---------- 导航右键：固定到主页 ----------

    /// <summary>
    /// 给每个<b>叶子</b>导航项挂右键菜单（打开 / 固定到主页 / 从主页取消固定 / 管理快捷方式…）。
    /// 菜单文案在 <see cref="OnHomeShortcutMenuOpened"/> 里现取，因此语言切换后无需额外处理。
    /// <para>
    /// 同时做一次<b>目录 ↔ 导航的双向比对</b>：将来新增导航页却忘了在功能目录里登记
    /// （或反过来目录里写了一个不存在的页面）时，日志里会直接看到，而不是"右键没有菜单"这种哑故障。
    /// </para>
    /// </summary>
    private void AttachHomeShortcutMenus()
    {
        var navItems = EnumerateNavigationItems().ToList();
        var navPageTypes = new HashSet<Type>();

        foreach (var item in navItems)
        {
            if (item.TargetPageType is not { } pageType)
            {
                continue; // 分组父项（商业 / 频道 / …）
            }

            navPageTypes.Add(pageType);

            if (HomeFeatureCatalog.NavigationExclusions.Contains(pageType))
            {
                continue; // 主页自己不需要快捷方式
            }

            var feature = HomeFeatureCatalog.FindByPageType(pageType);
            if (feature is null)
            {
                Core.Log.Warn($"[主页] 导航项「{item.Content}」没有对应的功能目录条目（{pageType.Name}），它无法被固定到主页。");
                continue;
            }

            item.ContextMenu = BuildHomeShortcutMenu(feature);
        }

        foreach (var feature in HomeFeatureCatalog.Features.Where(p => p.PageType is not null))
        {
            if (!navPageTypes.Contains(feature.PageType!))
            {
                Core.Log.Warn($"[主页] 功能目录里的 {feature.Id}（{feature.PageType!.Name}）不在左侧导航中，主页磁贴点了会跳不过去。");
            }
        }
    }

    private IEnumerable<NavigationViewItem> EnumerateNavigationItems()
        => EnumerateNavigationItems(
            NavigationView.MenuItems.Cast<object>().Concat(NavigationView.FooterMenuItems.Cast<object>()));

    private static IEnumerable<NavigationViewItem> EnumerateNavigationItems(IEnumerable<object> items)
    {
        foreach (var entry in items)
        {
            if (entry is not NavigationViewItem item)
            {
                continue;
            }

            yield return item;

            // WPF-UI 的 NavigationViewItem.MenuItems 是非泛型 IList。
            foreach (var child in EnumerateNavigationItems(item.MenuItems.Cast<object>()))
            {
                yield return child;
            }
        }
    }

    private static ContextMenu BuildHomeShortcutMenu(HomeFeature feature)
    {
        var menu = new ContextMenu { Tag = feature };

        menu.Items.Add(CreateMenuItem("open", OnHomeShortcutMenuOpenClick));
        menu.Items.Add(CreateMenuItem("pin", OnHomeShortcutMenuPinClick));
        menu.Items.Add(CreateMenuItem("unpin", OnHomeShortcutMenuUnpinClick));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("manage", OnHomeShortcutMenuManageClick));
        menu.Opened += OnHomeShortcutMenuOpened;

        return menu;

        static MenuItem CreateMenuItem(string role, RoutedEventHandler handler)
        {
            var item = new MenuItem { Tag = role };
            item.Click += handler;
            return item;
        }
    }

    private static void OnHomeShortcutMenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu { Tag: HomeFeature feature } menu)
        {
            return;
        }

        var pinned = HomeShortcutService.Contains(feature.Id);
        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            switch (item.Tag)
            {
                case "open":
                    item.Header = Resolve("Home.Quick.Open");
                    break;

                case "pin":
                    item.Header = Resolve("Home.Quick.Pin");
                    item.Visibility = pinned ? Visibility.Collapsed : Visibility.Visible;
                    break;

                case "unpin":
                    item.Header = Resolve("Home.Quick.Unpin");
                    item.Visibility = pinned ? Visibility.Visible : Visibility.Collapsed;
                    break;

                case "manage":
                    item.Header = Resolve("Home.Quick.Manage");
                    break;
            }
        }
    }

    private static HomeFeature? FeatureFromMenu(object sender)
        => sender is MenuItem { Parent: ContextMenu { Tag: HomeFeature feature } } ? feature : null;

    private static void OnHomeShortcutMenuOpenClick(object sender, RoutedEventArgs e)
        => HomeFeatureLauncher.Open(FeatureFromMenu(sender));

    private static void OnHomeShortcutMenuPinClick(object sender, RoutedEventArgs e)
    {
        if (FeatureFromMenu(sender) is not { } feature)
        {
            return;
        }

        if (!HomeShortcutService.Add(feature.Id) && HomeShortcutService.IsFull)
        {
            PageNotifyService.Warning(string.Format(
                Resolve("Home.Quick.LimitReached"), HomeShortcutService.MaxItems));
        }
    }

    private static void OnHomeShortcutMenuUnpinClick(object sender, RoutedEventArgs e)
    {
        if (FeatureFromMenu(sender) is { } feature)
        {
            HomeShortcutService.Remove(feature.Id);
        }
    }

    private static void OnHomeShortcutMenuManageClick(object sender, RoutedEventArgs e)
        => Views.Windows.AddShortcutWindow.ShowWindow();

    private static string Resolve(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;

    /// <summary>
    /// 拦下「工具」分组页面的导航：取消这次导航，改为打开（或激活）对应的**独立工具窗口**。
    /// <para>
    /// <b>两个实测要点</b>（一次性探针驱动真实 WPF-UI <c>NavigationView</c> 得到，见 REFACTORING.md §9 第 75 条）：
    /// ① <c>args.Page</c> 是**页面实例**，不是 <see cref="Type"/>——按 <c>is Type</c> 判断会永远不命中，
    /// 于是工具页照样导航进主内容区（这正是"没弹窗、还是主窗口显示页面"的原因）；
    /// ② <c>args.Cancel = true</c> **确实被尊重**：Frame 不导航，导航选中项也不动（左侧高亮因此始终与内容区一致）。
    /// </para>
    /// <para>
    /// 顺手复用 WPF-UI 已经实例化好的那个页面做窗口内容（不取消的话它会被丢弃），避免多构造一份页面。
    /// </para>
    /// </summary>
    private void OnNavigationViewNavigating(NavigationView sender, NavigatingCancelEventArgs args)
    {
        var pageType = args.Page as Type ?? args.Page?.GetType();
        if (pageType is null || !ToolPages.TryGetValue(pageType, out var titleKey))
        {
            return;
        }

        args.Cancel = true;
        ToolWindowHost.Show(pageType, titleKey, args.Page as Pages.IToolPage);
    }

    /// <summary>
    /// 主窗口标题 = "应用名 版本号"。窗口自身的 Title（任务栏/Alt+Tab）与自绘标题栏的
    /// Title 都指向 XAML 里的 <c>{DynamicResource AppDisplayName}</c>，切语言时会回到"只有名称"，
    /// 所以在语言变化后也要重新套用一次。
    /// </summary>
    private void ApplyTitle()
    {
        var name = Application.Current?.TryFindResource("AppDisplayName") as string;
        var title = AppVersion.Format(name);
        Title = title;
        TitleBar.Title = title;
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

    /// <summary>托盘图标没建起来时重试；仍失败则写诊断日志（见 <see cref="TrayIconHelper"/>）。</summary>
    private async void EnsureTrayIconAsync()
    {
        _trayIconReady = await TrayIconHelper.EnsureCreatedAsync(TrayIcon, this);

        if (!_trayIconReady)
        {
            Core.Log.Warn(
                "[托盘] 托盘图标不可用：系统通知不会显示；即使设置里开了「关闭到托盘」，关闭窗口也会正常退出" +
                "（避免窗口被藏进没有任何出口的状态）。");
        }
    }

    private void OnTrayLeftMouseUp(object sender, RoutedEventArgs e) => RestoreMainWindow();

    private void OnTrayShowClick(object sender, RoutedEventArgs e) => RestoreMainWindow();

    private void OnTrayExitClick(object sender, RoutedEventArgs e) => ShutdownApplication();

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        SaveWindowPlacement();

        // 关闭按钮：开启托盘后台时只隐藏窗口；托盘退出时 _isShuttingDown 为 true，不再拦截。
        if (!_isShuttingDown && SettingsService.GetBool(SettingsService.MinimizeToTrayKey))
        {
            // 托盘图标不可用时**不能**隐藏窗口：那样用户就再也找不回界面了（只能靠重启程序唤起）。
            // 这里退回正常关闭，并在日志里说明原因。
            if (TrayIcon.IsCreated)
            {
                e.Cancel = true;
                Hide();
                return;
            }

            Core.Log.Warn("[托盘] 已开启「关闭到托盘」，但托盘图标不可用，本次按正常关闭处理。");
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