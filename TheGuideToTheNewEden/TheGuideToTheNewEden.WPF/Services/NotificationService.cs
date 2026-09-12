using System.Windows;
using H.NotifyIcon;

namespace TheGuideToTheNewEden.WPF.Services;

/// <summary>
/// 系统通知服务：基于托盘图标（H.NotifyIcon）显示气泡通知。
/// 与 WinUI 版所用的 H.NotifyIcon 同源，不需要额外的注册表/快捷方式注册。
/// </summary>
public static class NotificationService
{
    private static TaskbarIcon? _trayIcon;

    /// <summary>气泡通知被点击（频道预警用它停止报警声音）。</summary>
    public static event EventHandler? NotificationClicked;

    /// <summary>由主窗口在初始化托盘后注册。</summary>
    public static void Register(TaskbarIcon trayIcon)
    {
        if (_trayIcon is not null)
        {
            _trayIcon.TrayBalloonTipClicked -= OnTrayBalloonTipClicked;
        }

        _trayIcon = trayIcon;
        _trayIcon.TrayBalloonTipClicked += OnTrayBalloonTipClicked;
    }

    private static void OnTrayBalloonTipClicked(object sender, RoutedEventArgs e)
        => NotificationClicked?.Invoke(sender, e);

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