using System.Windows;
using TheGuideToTheNewEden.WPF.Services.Assistant;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>
/// 「AI 助手」的弹窗：把对话视图放进独立工具窗口，**与页面共享同一个 VM 实例**
/// （见 <see cref="AssistantHost"/>：全局快捷键要在助手页从未打开过时也能弹窗，所以实例不能由页面独占）。
/// <para>
/// 重复调用切换显示/隐藏（窗口已开则关闭）——快捷键与页面「弹窗」按钮共用这一语义。
/// **不设 Owner**：被拥有的窗口永远压在宿主之上，"取消置顶"看起来和置顶一样（阶段 46 实测）。
/// </para>
/// </summary>
internal static class AssistantPopup
{
    private static ToolWindow? _window;

    /// <summary>窗口当前是否打开。</summary>
    internal static bool IsOpen => _window is not null;

    /// <summary>打开/收起助手弹窗。</summary>
    internal static void Toggle(FrameworkElement anchor)
    {
        if (_window is { } existing)
        {
            existing.Close();
            _window = null;
            return;
        }

        var viewModel = AssistantHost.ViewModel;

        // 弹窗可能先于助手页出现（快捷键路径）：这里补一次 Init（幂等），顺带消费主页转来的待处理请求
        viewModel.Init();

        var window = new ToolWindow(
            new AiAssistantChatView { DataContext = viewModel },
            ToolWindowTitleStyle.Default,
            showTopmostButton: true,
            showInTaskbar: true,
            width: 1040,
            height: 760)
        {
            DisplayTitle = FindString("Nav.Assistant"),
        };
        window.Closed += (_, _) => _window = null;
        _window = window;
        CenterOverHost(window, anchor);
        window.Show();
        window.Activate();
    }

    /// <summary>把弹窗居中到宿主主窗口（不设 Owner，WindowStartupLocation.CenterOwner 不适用）。</summary>
    private static void CenterOverHost(Window window, FrameworkElement anchor)
    {
        var host = Window.GetWindow(anchor);
        if (host is null)
        {
            return;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        var workArea = SystemParameters.WorkArea;
        var left = host.Left + (host.ActualWidth - window.Width) / 2;
        var top = host.Top + (host.ActualHeight - window.Height) / 2;
        window.Left = Math.Clamp(left, workArea.Left, Math.Max(workArea.Left, workArea.Right - window.Width));
        window.Top = Math.Clamp(top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - window.Height));
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
