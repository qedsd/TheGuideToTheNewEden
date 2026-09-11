using System.Windows;

namespace TheGuideToTheNewEden.WPF.Services;

/// <summary>在主窗口的 NavigationView 上做页面跳转（子页面无法直接拿到它）。</summary>
public static class Navigation
{
    public static void Navigate(Type pageType)
    {
        App.MainWindow?.NavigationView.Navigate(pageType);
    }

    /// <summary>把主窗口带到前台（托盘恢复等场景复用）。</summary>
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
    }
}
