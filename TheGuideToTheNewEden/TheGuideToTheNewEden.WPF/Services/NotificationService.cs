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

    /// <summary>
    /// 最近一次 Show 携带的点击动作（单槽位）。Win32 气泡（含 Win10+ 操作中心的 toast）的点击回调
    /// 不带"是哪条通知"的身份，只能按"最后一次 Show 获胜"近似路由；下一次 Show 会覆盖上一次。
    /// </summary>
    private static volatile Action? _pendingClick;

    /// <summary>气泡通知被点击（频道预警用它停止报警声音；对所有通知生效）。</summary>
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
    {
        NotificationClicked?.Invoke(sender, e);
        var click = _pendingClick;
        _pendingClick = null;
        click?.Invoke();
    }

    public static bool IsAvailable => _trayIcon is not null;

    /// <summary>
    /// 显示气泡通知。<paramref name="onClick"/> 为可选的点击动作（如"打开对应 KB 详情"），
    /// 点击气泡时在 UI 线程触发；不传则本次通知点击只执行 <see cref="NotificationClicked"/> 的既有逻辑。
    /// </summary>
    public static void Show(string title, string message, Action? onClick = null)
    {
        _pendingClick = onClick;
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