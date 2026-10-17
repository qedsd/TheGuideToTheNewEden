using System.Windows;
using TheGuideToTheNewEden.WPF.Services.Assistant;
using TheGuideToTheNewEden.WPF.Views.UserControls;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 「AI 助手」的**独立窗口**：把对话视图放进工具窗口，**与助手页共享同一个 VM 实例**
/// （见 <see cref="AssistantHost"/>：全局快捷键要在助手页从未打开过时也能弹窗，所以实例不能由页面独占）。
/// <para>
/// 它服务两种用途：
/// <list type="number">
///   <item><b>弹窗</b>（快捷键 <c>Alt+Q</c> / 对话页的「弹窗」按钮）：<see cref="Toggle"/>，再按一次收起；</item>
///   <item><b>窗口模式</b>（设置 → AI → 行为里「在主窗口中显示 AI」<b>未勾选</b>，这是默认值，阶段 128）：
///     所有入口（页脚常驻项、主页 banner 快捷提问、主页磁贴、快捷路由转来的提问）都调 <see cref="Show"/>
///     打开本窗口，而**不**把助手页塞进主窗口。</item>
/// </list>
/// </para>
/// <para>
/// 重复 <see cref="Show"/> 只激活已有窗口（不会开一堆）。**不设 Owner**：被拥有的窗口永远压在宿主之上，
/// "取消置顶"看起来和置顶一样（阶段 46 实测）。
/// </para>
/// </summary>
public static class AssistantWindow
{
    private static ToolWindow? _window;

    /// <summary>窗口当前是否打开。</summary>
    public static bool IsOpen => _window is not null;

    /// <summary>当前窗口实例（未打开时为 <c>null</c>）——复用语义与核验都看它。</summary>
    public static Window? CurrentWindow => _window;

    /// <summary>打开/收起助手窗口（快捷键与对话页「弹窗」按钮共用这一语义）。</summary>
    public static void Toggle(FrameworkElement anchor)
    {
        if (_window is { } existing)
        {
            existing.Close();
            _window = null;
            return;
        }

        Create(anchor)?.Activate();
    }

    /// <summary>
    /// 打开助手窗口（已开则只激活），并可选地**立即提一条问题**。
    /// <para>问题走 <see cref="AiAssistantViewModel.SubmitExternalQuery"/>：这是"外部随手一问"，
    /// 按阶段 127 的约定**新开一个对话**，不续在上一轮里。</para>
    /// </summary>
    public static void Show(FrameworkElement? anchor = null, string? query = null)
    {
        var window = _window ?? Create(anchor);
        if (window is null)
        {
            return;
        }

        if (!window.IsVisible)
        {
            window.Show();
        }

        window.Activate();
        if (!string.IsNullOrWhiteSpace(query))
        {
            AssistantHost.ViewModel.SubmitExternalQuery(query);
        }
    }

    /// <summary>关掉助手窗口（没有则什么都不做）。</summary>
    public static void Close()
    {
        _window?.Close();
        _window = null;
    }

    private static ToolWindow? Create(FrameworkElement? anchor)
    {
        var viewModel = AssistantHost.ViewModel;

        // 窗口可能先于助手页出现（快捷键 / 窗口模式入口）：这里补一次 Init（幂等），顺带消费待处理请求
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
            ManualId = "assistant", // F1 → 「AI 助手」那篇说明书
        };
        window.Closed += (_, _) => _window = null;
        _window = window;
        CenterOverHost(window, anchor);
        window.Show();
        return window;
    }

    /// <summary>把窗口居中到宿主主窗口（不设 Owner，WindowStartupLocation.CenterOwner 不适用）。</summary>
    private static void CenterOverHost(Window window, FrameworkElement? anchor)
    {
        var host = anchor is null ? null : Window.GetWindow(anchor);
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
