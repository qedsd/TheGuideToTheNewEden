using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TheGuideToTheNewEden.WPF.Services;

/// <summary>在主窗口的 NavigationView 上做页面跳转（子页面无法直接拿到它）。</summary>
public static class Navigation
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    public static void Navigate(Type pageType)
    {
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
}
