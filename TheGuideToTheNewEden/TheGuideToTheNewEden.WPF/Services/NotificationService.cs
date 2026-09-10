using H.NotifyIcon;

namespace TheGuideToTheNewEden.WPF.Services;

/// <summary>
/// 系统通知服务：基于托盘图标（H.NotifyIcon）显示气泡通知。
/// 与 WinUI 版所用的 H.NotifyIcon 同源，不需要额外的注册表/快捷方式注册。
/// </summary>
public static class NotificationService
{
    private static TaskbarIcon? _trayIcon;

    /// <summary>由主窗口在初始化托盘后注册。</summary>
    public static void Register(TaskbarIcon trayIcon)
    {
        _trayIcon = trayIcon;
    }

    public static bool IsAvailable => _trayIcon is not null;

    public static void Show(string title, string message)
    {
        try
        {
            _trayIcon?.ShowNotification(title, message);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }
}