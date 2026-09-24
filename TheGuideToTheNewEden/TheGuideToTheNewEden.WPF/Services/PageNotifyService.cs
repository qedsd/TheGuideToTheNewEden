using System.Windows;
using TheGuideToTheNewEden.WPF.Controls;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.Services;

/// <summary>
/// 页面级等待态与即时通知的统一入口（对应 WinUI 版 <c>BaseViewModel</c> 的
/// <c>ShowWaiting</c> / <c>HideWaiting</c> / <c>ShowSuccess</c> / <c>ShowError</c>）：
/// 等待遮罩是全屏的，通知在右下角堆叠。控件注册在 <c>MainWindow</c>，任何页面/VM 都可调用，
/// 内部自动切回 UI 线程（进度回调来自线程池）。
/// </summary>
public static class PageNotifyService
{
    private const int InfoDuration = 4000;
    private const int ErrorDuration = 9000;

    private static WaitingOverlay? _overlay;
    private static MessageHost? _host;

    /// <summary>由 MainWindow 在构造函数里注册承载控件。</summary>
    public static void Register(WaitingOverlay overlay, MessageHost host)
    {
        _overlay = overlay;
        _host = host;
    }

    // ---------- 等待态 ----------

    /// <summary>显示全屏等待遮罩；<paramref name="cancelAction"/> 非空时遮罩上出现"取消"按钮。</summary>
    public static void ShowWaiting(string message, Action? cancelAction = null)
        => OnUi(() => _overlay?.Show(message, cancelAction));

    /// <summary>更新等待文案（如分页进度）。</summary>
    public static void UpdateWaiting(string message)
        => OnUi(() => _overlay?.UpdateText(message));

    public static void HideWaiting()
        => OnUi(() => _overlay?.Hide());

    // ---------- 通知 ----------

    public static void Success(string message)
        => OnUi(() => _host?.Show(message, SymbolRegular.CheckmarkCircle24, "SystemFillColorSuccessBrush", InfoDuration));

    public static void Error(string message)
        => OnUi(() => _host?.Show(message, SymbolRegular.ErrorCircle24, "SystemFillColorCriticalBrush", ErrorDuration));

    /// <summary>错误通知 + 可选动作按钮（如"重试"；点击后先移除通知再执行回调）。</summary>
    public static void Error(string message, string? actionText, Action? action)
        => OnUi(() => _host?.Show(message, SymbolRegular.ErrorCircle24, "SystemFillColorCriticalBrush", ErrorDuration, actionText, action));

    public static void Info(string message)
        => OnUi(() => _host?.Show(message, SymbolRegular.Info24, "SystemAccentColorPrimaryBrush", InfoDuration));

    public static void Warning(string message)
        => OnUi(() => _host?.Show(message, SymbolRegular.Warning24, "SystemFillColorCautionBrush", ErrorDuration));

    /// <summary>清空通知（如切换页面时）。</summary>
    public static void ClearMessages()
        => OnUi(() => _host?.Clear());

    private static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.BeginInvoke(action);
        }
    }
}
