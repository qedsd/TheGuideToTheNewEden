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
}
