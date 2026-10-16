using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using TheGuideToTheNewEden.WPF.Services.Assistant;

namespace TheGuideToTheNewEden.WPF.Services;

/// <summary>在主窗口的 NavigationView 上做页面跳转（子页面无法直接拿到它）。</summary>
public static class Navigation
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>
    /// 导航到某页；**助手页是例外**（阶段 128）：设置里「在主窗口中显示 AI」不勾（默认）时，
    /// 程序化导航（主页磁贴、快捷路由…）也一律改成打开独立窗口，免得绕过导航项的拦截。
    /// （导航项自身的点击由 <c>MainWindow.OnNavigationViewNavigating</c> 拦截，两条路都通向同一个窗口。）
    /// </summary>
    public static void Navigate(Type pageType)
    {
        if (AssistantEntryRouter.ShouldOpenWindow(AiAssistantSettingService.ShowInMainWindow, pageType))
        {
            Views.Windows.AssistantWindow.Show(App.MainWindow);
            return;
        }

        App.MainWindow?.NavigationView.Navigate(pageType);
    }

    /// <summary>把主窗口带到前台（托盘恢复、从工具窗口跳回主界面等场景复用）。</summary>
    public static void Activate()
    {
        var window = App.MainWindow;
        if (window is null)
        {
            return;
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();

        // Activate() 在“调用方不是前台进程”时可能只闪烁任务栏；再补一次 Win32 置前
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle != IntPtr.Zero)
            {
                SetForegroundWindow(handle);
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    // ==================================================================
    //  带物品跳转到市场页
    // ==================================================================

    private static long? _pendingMarketTypeId;

    /// <summary>
    /// 市场页请求"选中某个物品"。市场页 VM 订阅本事件，页面已就绪时立即选中；
    /// 页面尚未创建（首次导航）时由 <see cref="TakePendingMarketType"/> 在页面加载后取走。
    /// </summary>
    public static event Action<long>? MarketTypeSelectionRequested;

    /// <summary>跳转到市场页并选中指定物品（同时把主窗口置前）。</summary>
    public static void NavigateToMarket(long typeId)
    {
        if (typeId <= 0)
        {
            return;
        }

        _pendingMarketTypeId = typeId;
        Navigate(typeof(Views.Pages.MarketPage));
        MarketTypeSelectionRequested?.Invoke(typeId);
        Activate();
    }

    /// <summary>取走待选中的物品 ID（页面加载完成后调用；取到即清空）。</summary>
    public static long? TakePendingMarketType()
    {
        var pending = _pendingMarketTypeId;
        _pendingMarketTypeId = null;
        return pending;
    }

    // ==================================================================
    //  带参数跳转到星图（主页参数化磁贴 / 其它页面的"定位主图"）
    // ==================================================================

    private static int? _pendingSystemId;

    /// <summary>星图页请求"定位到某个星系"。星图页已就绪时立即定位，否则由页面加载后取走。</summary>
    public static event Action<int>? SystemLocationRequested;

    /// <summary>跳转到星图页并定位到指定星系（同时把主窗口置前）。</summary>
    public static void NavigateToSystem(int systemId)
    {
        if (systemId <= 0)
        {
            return;
        }

        _pendingSystemId = systemId;
        Navigate(typeof(Views.Pages.MapPage));
        SystemLocationRequested?.Invoke(systemId);
        Activate();
    }

    /// <summary>重新压入一个待定位的星系（页面尚未装载完成时由星图页回存）。</summary>
    public static void SetPendingSystem(int systemId) => _pendingSystemId = systemId;

    /// <summary>取走待定位的星系 ID（星图页装载完成后调用；取到即清空）。</summary>
    public static int? TakePendingSystem()
    {
        var pending = _pendingSystemId;
        _pendingSystemId = null;
        return pending;
    }

    // ==================================================================
    //  带参数跳转到角色工作区（主页参数化磁贴）
    // ==================================================================

    private static long? _pendingCharacterId;

    /// <summary>角色壳页请求"打开某个角色的工作区标签"。</summary>
    public static event Action<long>? CharacterWorkspaceRequested;

    /// <summary>跳转到角色页并打开指定角色的工作区（同时把主窗口置前）。</summary>
    public static void NavigateToCharacter(long characterId)
    {
        if (characterId <= 0)
        {
            return;
        }

        _pendingCharacterId = characterId;
        Navigate(typeof(Views.Pages.Characters.CharactersShellPage));
        CharacterWorkspaceRequested?.Invoke(characterId);
        Activate();
    }

    /// <summary>重新压入一个待打开的角色（角色卡片还没加载出来时由壳页回存）。</summary>
    public static void SetPendingCharacter(long characterId) => _pendingCharacterId = characterId;

    /// <summary>取走待打开的角色 ID（角色卡片加载完成后调用；取到即清空）。</summary>
    public static long? TakePendingCharacter()
    {
        var pending = _pendingCharacterId;
        _pendingCharacterId = null;
        return pending;
    }

    // ==================================================================
    //  星图命令通道（AI 助手星图工具：路径/定位/图层）
    // ==================================================================

    private static MapViewCommand? _pendingMapCommand;

    /// <summary>
    /// 一次"把结果显示到星图"的视图命令。命令只是叠加应用，不与用户手动操作互斥。
    /// <see cref="MapColorMode"/> 与 <see cref="ShowBridges"/> 未指定项保持星图当前值（工具先读当前配置再改）。
    /// </summary>
    public sealed record MapViewCommand
    {
        /// <summary>路径星系 ID 序列（StarMapCanvas.SetRoute 第一参）。</summary>
        public IReadOnlyList<int>? Route { get; init; }

        /// <summary>途经点索引（SetRoute 第二参）。</summary>
        public IReadOnlyList<int>? Waypoints { get; init; }

        /// <summary>居中定位的星系 ID（ToSystem）。</summary>
        public int? CenterSystemId { get; init; }

        /// <summary>图层配色（安等/主权/资源/击杀/跳数）。</summary>
        public Views.UserControls.Map.MapColorMode? ColorMode { get; init; }

        /// <summary>跳桥层显示开关（持久化，走 JumpBridgeSettingService）。</summary>
        public bool? ShowBridges { get; init; }
    }

    /// <summary>星图页已就绪时立即收到命令。</summary>
    public static event Action<MapViewCommand>? MapCommandRequested;

    /// <summary>跳转到星图页并应用视图命令（同时把主窗口置前）。</summary>
    public static void NavigateToMap(MapViewCommand command)
    {
        _pendingMapCommand = command;
        Navigate(typeof(Views.Pages.MapPage));
        MapCommandRequested?.Invoke(command);
        Activate();
    }

    /// <summary>重新压入一个待应用的星图命令（页面尚未装载完成时由星图页回存）。</summary>
    public static void SetPendingMapCommand(MapViewCommand command) => _pendingMapCommand = command;

    /// <summary>取走待应用的星图命令（星图页装载完成后调用；取到即清空）。</summary>
    public static MapViewCommand? TakePendingMapCommand()
    {
        var pending = _pendingMapCommand;
        _pendingMapCommand = null;
        return pending;
    }

    // ==================================================================
    //  带请求跳转到 AI 助手（主页快捷输入的 handoff）
    // ==================================================================

    private static string? _pendingAssistantQuery;

    /// <summary>AI 助手页请求"处理这条自然语言请求"。助手页已就绪时立即收到，否则由页面加载后取走。</summary>
    public static event Action<string>? AssistantQueryRequested;

    /// <summary>
    /// 打开 AI 助手并让它处理这条请求（同时把主窗口/助手窗口置前）。
    /// <para>
    /// 走哪一边由 <see cref="AssistantEntryRouter"/> 决定（阶段 128）：设置里「在主窗口中显示 AI」勾选时
    /// 仍旧导航到主窗口里的助手页；**不勾（默认）时改用独立窗口**，请求交给它直接发送
    /// （<c>SubmitExternalQuery</c> ⇒ 按阶段 127 的约定新开一个对话）。
    /// </para>
    /// </summary>
    public static void NavigateToAssistant(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        if (AssistantEntryRouter.ShouldOpenWindow(AiAssistantSettingService.ShowInMainWindow, typeof(Views.Pages.AiAssistantPage)))
        {
            Views.Windows.AssistantWindow.Show(App.MainWindow, query);
            Activate();
            return;
        }

        _pendingAssistantQuery = query;
        Navigate(typeof(Views.Pages.AiAssistantPage));
        AssistantQueryRequested?.Invoke(query);
        Activate();
    }

    /// <summary>重新压入一条待处理的助手请求（助手页尚未就绪时由页面回存）。</summary>
    public static void SetPendingAssistantQuery(string query) => _pendingAssistantQuery = query;

    /// <summary>取走待处理的助手请求（助手页就绪后调用；取到即清空）。</summary>
    public static string? TakePendingAssistantQuery()
    {
        var pending = _pendingAssistantQuery;
        _pendingAssistantQuery = null;
        return pending;
    }

    // ==================================================================
    //  带搜索跳转到快速链接（AI 助手 links_search）
    // ==================================================================

    private static string? _pendingLinksSearch;

    /// <summary>
    /// 跳转到「快速链接」页并把搜索框设成指定关键词（同时把主窗口置前）。
    /// 页面是常驻缓存的，所以待办由页面在**每次** <c>Loaded</c> 时消费（不像星图/市场那样只在首次加载后取一次）。
    /// </summary>
    public static void NavigateToLinks(string? searchText)
    {
        _pendingLinksSearch = string.IsNullOrWhiteSpace(searchText) ? null : searchText.Trim();
        Navigate(typeof(Views.Pages.LinksPage));
        Activate();
    }

    /// <summary>取走待应用的链接搜索词（快速链接页每次 Loaded 调用；取到即清空）。</summary>
    public static string? TakePendingLinksSearch()
    {
        var pending = _pendingLinksSearch;
        _pendingLinksSearch = null;
        return pending;
    }
}
